using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Files;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class ManagerPaymentRepository(
    WpsmDbContext db,
    IFileStorage storage,
    INotificationRepository notifications) : IManagerPaymentRepository
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png"
    };

    public async Task<ManagerPaymentOverviewView?> GetProjectPaymentsAsync(
        string projectCode,
        int userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;

        var today = PaymentCalculator.GetVietnamToday();
        return BuildOverview(project, today);
    }

    public async Task<RecordPaymentResult> RecordPaymentAsync(
        string projectCode,
        int managerId,
        string role,
        RecordPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, managerId, role, cancellationToken);
        if (project is null)
            return new RecordPaymentResult(false, "Không tìm thấy dự án trong phạm vi quyền hạn.");

        if (string.Equals(project.Status, "Settled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            return new RecordPaymentResult(false, "Dự án đã hoàn tất quyết toán hoặc đã đóng, không thể ghi nhận thêm thanh toán milestone.");
        }

        // 1. Payment Type validation
        var rawType = (command.PaymentType ?? "").Trim().ToUpperInvariant();
        if (rawType is "NRD")
        {
            return new RecordPaymentResult(false, "Khoản đặt cọc NRD đã được ghi nhận khi khởi tạo hợp đồng và không thể ghi nhận lại qua M-14.");
        }

        string canonicalType = rawType switch
        {
            "T30" or "T-30" => "T30",
            "FINAL" or "SETTLEMENT" => "FINAL",
            _ => ""
        };

        if (string.IsNullOrEmpty(canonicalType))
        {
            return new RecordPaymentResult(false, "Loại thanh toán không hợp lệ. Chỉ chấp nhận T-30 hoặc Final Settlement.");
        }

        var today = PaymentCalculator.GetVietnamToday();

        // 2. Event completion check for FINAL payment
        if (canonicalType == "FINAL")
        {
            if (!PaymentCalculator.IsEventCompleted(project.EventDate, project.Status, today))
            {
                return new RecordPaymentResult(false, "Chỉ có thể ghi nhận thanh toán quyết toán sau khi sự kiện cưới đã diễn ra hoàn tất.");
            }
        }

        // 3. Payment Date validation (cannot be in the future)
        if (command.PaymentDate > today)
        {
            return new RecordPaymentResult(false, "Ngày thanh toán không được là ngày trong tương lai (theo giờ Việt Nam).");
        }

        // 4. Financial breakdown & balance check
        var crVariance = PaymentCalculator.CalculateApprovedCrVariance(
            project.ChangeRequests.Select(c => (c.Status, c.TotalVariance, c.CostImpact, c.PenaltyFee)));
        var currentTotal = PaymentCalculator.CalculateCurrentTotal(project.BaseContractValue, crVariance);
        var totalPaid = PaymentCalculator.CalculateTotalPaid(
            project.Payments.Select(p => (p.Status, p.Amount)));
        var outstandingBalance = PaymentCalculator.CalculateOutstandingBalance(currentTotal, totalPaid);

        // 5. Amount validation (MSG11)
        if (command.Amount <= 0)
        {
            return new RecordPaymentResult(false, "Số tiền thanh toán phải lớn hơn 0.");
        }

        if (command.Amount > outstandingBalance)
        {
            return new RecordPaymentResult(false, $"Số tiền thanh toán ({command.Amount:N0} đ) vượt quá số dư còn lại ({outstandingBalance:N0} đ) của dự án. (MSG11)");
        }

        // 6. Method & Receipt validation
        var rawMethod = (command.Method ?? "").Trim();
        string method = rawMethod.ToLowerInvariant() switch
        {
            "bank" or "bank transfer" or "chuyen khoan" => "Bank",
            "cash" or "tien mat" => "Cash",
            _ => "Bank"
        };

        var hasReceiptFile = command.ReceiptBytes is { Length: > 0 };
        if (method == "Bank" && !hasReceiptFile)
        {
            return new RecordPaymentResult(false, "Phương thức chuyển khoản ngân hàng bắt buộc phải tải lên chứng từ / ủy nhiệm chi (JPG/PNG/PDF).");
        }

        string? storageKey = null;
        string? originalFileName = command.ReceiptFileName;
        string? contentType = command.ReceiptContentType ?? "application/pdf";

        if (hasReceiptFile)
        {
            var ext = Path.GetExtension(originalFileName ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            {
                return new RecordPaymentResult(false, "Định dạng file biên nhận không hợp lệ. Chỉ chấp nhận JPG, PNG hoặc PDF.");
            }

            if (command.ReceiptBytes!.Length > 10 * 1024 * 1024)
            {
                return new RecordPaymentResult(false, "Dung lượng file biên nhận vượt quá giới hạn cho phép (tối đa 10 MB).");
            }

            storageKey = $"payments/{project.ProjectCode}/{canonicalType}_{Guid.NewGuid():N}{ext}";
            using var fileStream = new MemoryStream(command.ReceiptBytes);
            await storage.UploadAsync(fileStream, storageKey, cancellationToken);

            var storedFile = new StoredFile
            {
                FileId = Guid.NewGuid(),
                ProjectId = project.ProjectId,
                Purpose = canonicalType == "FINAL" ? "SettlementReceipt" : "PaymentReceipt",
                StorageKey = storageKey,
                OriginalName = originalFileName ?? $"{canonicalType}_{project.ProjectCode}{ext}",
                ContentType = contentType,
                SizeBytes = command.ReceiptBytes.Length,
                UploadedBy = managerId,
                UploadedAt = DateTime.UtcNow
            };
            db.StoredFiles.Add(storedFile);
        }

        // 7. Update or Insert Payment row
        var payment = project.Payments.FirstOrDefault(p =>
            string.Equals(p.PaymentType, canonicalType, StringComparison.OrdinalIgnoreCase) ||
            (canonicalType == "T30" && string.Equals(p.PaymentType, "T-30", StringComparison.OrdinalIgnoreCase)));

        if (payment is null)
        {
            payment = new Payment
            {
                ProjectId = project.ProjectId,
                PaymentType = canonicalType,
                Status = "Paid",
                DueDate = canonicalType == "T30" ? project.EventDate.AddDays(-30) : project.EventDate,
                Amount = command.Amount,
                PaymentMethod = method,
                PaidDate = command.PaymentDate,
                RecordedBy = managerId,
                ReceiptContent = command.ReceiptBytes,
                ReceiptFileName = originalFileName,
                ReceiptContentType = contentType,
                ReceiptFilePath = storageKey ?? (method == "Cash" ? null : $"payments/{canonicalType}_{project.ProjectCode}")
            };
            db.Payments.Add(payment);
            project.Payments.Add(payment);
        }
        else
        {
            payment.Status = "Paid";
            payment.Amount = command.Amount;
            payment.PaymentMethod = method;
            payment.PaidDate = command.PaymentDate;
            payment.RecordedBy = managerId;
            if (hasReceiptFile)
            {
                payment.ReceiptContent = command.ReceiptBytes;
                payment.ReceiptFileName = originalFileName;
                payment.ReceiptContentType = contentType;
                payment.ReceiptFilePath = storageKey;
            }
        }

        // 8. Re-evaluate totals
        var newTotalPaid = totalPaid + command.Amount;
        var newBalance = PaymentCalculator.CalculateOutstandingBalance(currentTotal, newTotalPaid);
        project.CurrentTotal = currentTotal;

        var isSettled = false;
        // 9. If FINAL and balance reaches 0, transition to Settled & generate settlement receipt
        if (canonicalType == "FINAL" && newBalance == 0)
        {
            project.Status = "Settled";
            isSettled = true;

            var managerUser = await db.Users.FindAsync(new object[] { managerId }, cancellationToken);
            var managerName = managerUser?.FullName ?? "Ban Quan Ly Tiec Cuoi";

            var receiptModel = BuildReceiptPdfModel(project, currentTotal, newTotalPaid, managerName, DateTime.UtcNow);
            var receiptPdfBytes = ReceiptPdfGenerator.GenerateFinalSettlementReceiptPdf(receiptModel);

            var settlementStorageKey = $"settlements/{project.ProjectCode}/Receipt_{project.ProjectCode}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            using var pdfStream = new MemoryStream(receiptPdfBytes);
            await storage.UploadAsync(pdfStream, settlementStorageKey, cancellationToken);

            db.StoredFiles.Add(new StoredFile
            {
                FileId = Guid.NewGuid(),
                ProjectId = project.ProjectId,
                Purpose = "SettlementReceipt",
                StorageKey = settlementStorageKey,
                OriginalName = $"Bien_nhan_quyet_toan_{project.ProjectCode}.pdf",
                ContentType = "application/pdf",
                SizeBytes = receiptPdfBytes.Length,
                UploadedBy = managerId,
                UploadedAt = DateTime.UtcNow
            });

            payment.ReceiptContent = receiptPdfBytes;
            payment.ReceiptFileName = $"Bien_nhan_quyet_toan_{project.ProjectCode}.pdf";
            payment.ReceiptContentType = "application/pdf";
            payment.ReceiptFilePath = settlementStorageKey;
        }

        await db.SaveChangesAsync(cancellationToken);

        // 10. Send G-04 Notifications to Customer
        try
        {
            var milestoneTitle = PaymentCalculator.GetMilestoneName(canonicalType);
            await notifications.CreateNotificationAsync(
                recipientId: project.CustomerId,
                type: NotificationTypes.Payment,
                message: $"Đã ghi nhận thanh toán đợt {canonicalType} ({milestoneTitle}): {command.Amount:N0} đ. Phương thức: {method}.",
                linkUrl: "/customer/payments",
                relatedEntityType: "Payment",
                relatedEntityId: payment.PaymentId.ToString(),
                cancellationToken: cancellationToken);

            if (isSettled)
            {
                await notifications.CreateNotificationAsync(
                    recipientId: project.CustomerId,
                    type: NotificationTypes.Payment,
                    message: $"Dự án cưới {project.ProjectCode} đã hoàn tất quyết toán hợp đồng (Settled). Biên nhận quyết toán chính thức đã sẵn sàng tải về.",
                    linkUrl: "/customer/payments",
                    relatedEntityType: "Payment",
                    relatedEntityId: payment.PaymentId.ToString(),
                    cancellationToken: cancellationToken);
            }
        }
        catch { }

        var updatedOverview = BuildOverview(project, today);
        return new RecordPaymentResult(
            Success: true,
            Message: $"Ghi nhận thanh toán đợt {canonicalType} ({command.Amount:N0} đ) thành công.",
            PaymentId: payment.PaymentId,
            NewBalance: newBalance,
            IsSettled: isSettled,
            Overview: updatedOverview
        );
    }

    public async Task<SettlementCalculationView?> GetSettlementCalculationAsync(
        string projectCode,
        int userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;

        var today = PaymentCalculator.GetVietnamToday();
        return BuildSettlementCalculation(project, today);
    }

    public async Task<FinalSettlementReceiptView?> GenerateSettlementReceiptAsync(
        string projectCode,
        int managerId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, managerId, role, cancellationToken);
        if (project is null) return null;

        var today = PaymentCalculator.GetVietnamToday();
        var calc = BuildSettlementCalculation(project, today);

        if (!calc.IsEventCompleted)
            throw new InvalidOperationException("Chỉ có thể phát hành biên nhận quyết toán sau khi sự kiện cưới đã hoàn tất.");

        if (calc.FinalAmountDue > 0)
            throw new InvalidOperationException($"Không thể quyết toán hợp đồng khi còn dư nợ ({calc.FinalAmountDue:N0} đ).");

        var managerUser = await db.Users.FindAsync(new object[] { managerId }, cancellationToken);
        var managerName = managerUser?.FullName ?? "Ban Quản Lý Tiệc Cưới";

        var receiptModel = BuildReceiptPdfModel(project, calc.CurrentProjectTotal, calc.TotalRecordedPayments, managerName, DateTime.UtcNow);
        var receiptPdfBytes = ReceiptPdfGenerator.GenerateFinalSettlementReceiptPdf(receiptModel);

        var settlementStorageKey = $"settlements/{project.ProjectCode}/Receipt_{project.ProjectCode}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        using var pdfStream = new MemoryStream(receiptPdfBytes);
        await storage.UploadAsync(pdfStream, settlementStorageKey, cancellationToken);

        db.StoredFiles.Add(new StoredFile
        {
            FileId = Guid.NewGuid(),
            ProjectId = project.ProjectId,
            Purpose = "SettlementReceipt",
            StorageKey = settlementStorageKey,
            OriginalName = $"Bien_nhan_quyet_toan_{project.ProjectCode}.pdf",
            ContentType = "application/pdf",
            SizeBytes = receiptPdfBytes.Length,
            UploadedBy = managerId,
            UploadedAt = DateTime.UtcNow
        });

        // Ensure Final payment row has receipt attached
        var finalPayment = project.Payments.FirstOrDefault(p =>
            string.Equals(p.PaymentType, "FINAL", StringComparison.OrdinalIgnoreCase));
        if (finalPayment != null)
        {
            finalPayment.ReceiptContent = receiptPdfBytes;
            finalPayment.ReceiptFileName = $"Bien_nhan_quyet_toan_{project.ProjectCode}.pdf";
            finalPayment.ReceiptContentType = "application/pdf";
            finalPayment.ReceiptFilePath = settlementStorageKey;
        }

        project.Status = "Settled";
        await db.SaveChangesAsync(cancellationToken);

        // Send G-04 notification to Customer
        try
        {
            await notifications.CreateNotificationAsync(
                recipientId: project.CustomerId,
                type: NotificationTypes.Payment,
                message: $"Biên nhận quyết toán dự án {project.ProjectCode} đã được phát hành. Chúc mừng hai bạn đã hoàn tất thanh toán hợp đồng.",
                linkUrl: "/customer/payments",
                relatedEntityType: "Payment",
                relatedEntityId: finalPayment?.PaymentId.ToString(),
                cancellationToken: cancellationToken);
        }
        catch { }

        return MapFinalReceiptView(project, calc, managerName, DateTime.UtcNow);
    }

    public async Task<FileContentResultModel?> GetSettlementReceiptFileAsync(
        string projectCode,
        int userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;

        // 1. Try FINAL payment ReceiptContent
        var finalPayment = project.Payments.FirstOrDefault(p =>
            string.Equals(p.PaymentType, "FINAL", StringComparison.OrdinalIgnoreCase));
        if (finalPayment?.ReceiptContent is { Length: > 0 } content)
        {
            return new FileContentResultModel(
                content,
                finalPayment.ReceiptContentType ?? "application/pdf",
                finalPayment.ReceiptFileName ?? $"Bien_nhan_quyet_toan_{project.ProjectCode}.pdf");
        }

        // 2. Try StoredFiles with SettlementReceipt
        var stored = await db.StoredFiles.AsNoTracking()
            .Where(f => f.ProjectId == project.ProjectId && f.Purpose == "SettlementReceipt")
            .OrderByDescending(f => f.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is not null)
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, cancellationToken);
            if (stream is not null)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                return new FileContentResultModel(ms.ToArray(), stored.ContentType, stored.OriginalName);
            }
        }

        // 3. If project is Settled and balance is 0, generate on the fly
        var today = PaymentCalculator.GetVietnamToday();
        var calc = BuildSettlementCalculation(project, today);
        if (calc.IsSettled || (calc.IsEventCompleted && calc.FinalAmountDue == 0))
        {
            var managerName = project.Manager?.FullName ?? "Ban Quan Ly WPSM";
            var receiptModel = BuildReceiptPdfModel(project, calc.CurrentProjectTotal, calc.TotalRecordedPayments, managerName, DateTime.UtcNow);
            var generatedBytes = ReceiptPdfGenerator.GenerateFinalSettlementReceiptPdf(receiptModel);
            return new FileContentResultModel(
                generatedBytes,
                "application/pdf",
                $"Bien_nhan_quyet_toan_{project.ProjectCode}.pdf");
        }

        return null;
    }

    public async Task<FileContentResultModel?> GetPaymentReceiptFileAsync(
        string projectCode,
        string paymentType,
        int userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;

        Payment? payment = project.Payments.FirstOrDefault(p =>
            string.Equals(p.PaymentType, paymentType, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(paymentType, "T30", StringComparison.OrdinalIgnoreCase) && string.Equals(p.PaymentType, "T-30", StringComparison.OrdinalIgnoreCase)));

        if (payment?.ReceiptContent is { Length: > 0 } content)
        {
            return new FileContentResultModel(
                content,
                payment.ReceiptContentType ?? "application/pdf",
                payment.ReceiptFileName ?? $"Receipt_{project.ProjectCode}_{payment.PaymentType}.pdf");
        }

        var purpose = paymentType.ToUpperInvariant() switch
        {
            "NRD" => "NrdReceipt",
            "T30" or "T-30" => "PaymentReceipt",
            "FINAL" => "SettlementReceipt",
            _ => "PaymentReceipt"
        };

        var stored = await db.StoredFiles.AsNoTracking()
            .Where(f => f.ProjectId == project.ProjectId && f.Purpose == purpose)
            .OrderByDescending(f => f.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is not null)
        {
            await using var stream = await storage.OpenReadAsync(stored.StorageKey, cancellationToken);
            if (stream is not null)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                return new FileContentResultModel(ms.ToArray(), stored.ContentType, stored.OriginalName);
            }
        }

        return null;
    }

    private async Task<WeddingProject?> ResolveProjectAsync(
        string projectCode,
        int userId,
        string role,
        CancellationToken cancellationToken)
    {
        var query = db.WeddingProjects
            .Include(p => p.Customer)
            .Include(p => p.Manager)
            .Include(p => p.Hall)
            .Include(p => p.Slot)
            .Include(p => p.Payments).ThenInclude(p => p.RecordedByNavigation)
            .Include(p => p.ChangeRequests)
            .Where(p => p.ProjectCode == projectCode);

        if (role == "Customer")
        {
            query = query.Where(p => p.CustomerId == userId);
        }
        else if (role == "Manager")
        {
            query = query.Where(p => p.ManagerId == userId);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private static ManagerPaymentOverviewView BuildOverview(
        WeddingProject project,
        DateOnly today)
    {
        var crVariance = PaymentCalculator.CalculateApprovedCrVariance(
            project.ChangeRequests.Select(c => (c.Status, c.TotalVariance, c.CostImpact, c.PenaltyFee)));
        var currentTotal = PaymentCalculator.CalculateCurrentTotal(project.BaseContractValue, crVariance);
        var totalPaid = PaymentCalculator.CalculateTotalPaid(
            project.Payments.Select(p => (p.Status, p.Amount)));
        var outstandingBalance = PaymentCalculator.CalculateOutstandingBalance(currentTotal, totalPaid);

        var isEventCompleted = PaymentCalculator.IsEventCompleted(project.EventDate, project.Status, today);
        var isSettled = string.Equals(project.Status, "Settled", StringComparison.OrdinalIgnoreCase);

        var summary = new ManagerPaymentSummaryView(
            ProjectId: project.ProjectId,
            ProjectCode: project.ProjectCode,
            CoupleName: project.Customer.FullName,
            HallName: project.Hall.Name,
            SlotName: project.Slot.SlotName,
            EventDate: project.EventDate,
            ProjectStatus: project.Status,
            BaseContractValue: project.BaseContractValue,
            TotalApprovedCrVariance: crVariance,
            CurrentProjectTotal: currentTotal,
            TotalAmountPaid: totalPaid,
            OutstandingBalance: outstandingBalance,
            IsEventCompleted: isEventCompleted,
            IsSettled: isSettled
        );

        var milestones = BuildMilestones(project, summary, today);
        var history = BuildHistory(project);
        var settlement = BuildSettlementCalculation(project, today);

        return new ManagerPaymentOverviewView(summary, milestones, history, settlement);
    }

    private static IReadOnlyList<ManagerPaymentMilestoneView> BuildMilestones(
        WeddingProject project,
        ManagerPaymentSummaryView summary,
        DateOnly today)
    {
        var payments = project.Payments.ToList();
        var nrdPayment = payments.FirstOrDefault(p => string.Equals(p.PaymentType, "NRD", StringComparison.OrdinalIgnoreCase));
        var t30Payment = payments.FirstOrDefault(p =>
            string.Equals(p.PaymentType, "T30", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.PaymentType, "T-30", StringComparison.OrdinalIgnoreCase));
        var finalPayment = payments.FirstOrDefault(p => string.Equals(p.PaymentType, "FINAL", StringComparison.OrdinalIgnoreCase));

        var nrdPaid = nrdPayment?.Amount ?? project.Nrdamount;
        var nrdStatus = PaymentCalculator.ResolveMilestoneStatus(nrdPayment?.Status ?? "Paid", project.EventDate.AddDays(-90), today, project.Status, "NRD");

        var t30Amount = t30Payment?.Amount ?? PaymentCalculator.CalculateT30Amount(summary.BaseContractValue, nrdPaid);
        var t30PaidAmount = t30Payment?.Status == "Paid" ? (t30Payment.Amount ?? 0m) : 0m;
        var t30DueDate = t30Payment?.DueDate ?? project.EventDate.AddDays(-30);
        var t30Status = PaymentCalculator.ResolveMilestoneStatus(t30Payment?.Status, t30DueDate, today, project.Status, "T30");

        var finalDueDate = project.EventDate;
        var finalAmount = finalPayment?.Amount ?? summary.OutstandingBalance;
        var finalPaidAmount = finalPayment?.Status == "Paid" ? (finalPayment.Amount ?? 0m) : 0m;
        var finalStatus = summary.IsSettled || (finalPayment?.Status == "Paid")
            ? "Paid"
            : (summary.IsEventCompleted ? "Due" : "Upcoming");

        return new List<ManagerPaymentMilestoneView>
        {
            new(
                PaymentId: nrdPayment?.PaymentId,
                PaymentType: "NRD",
                MilestoneName: PaymentCalculator.GetMilestoneName("NRD"),
                DueDate: nrdPayment?.DueDate ?? project.EventDate.AddDays(-90),
                Amount: nrdPaid,
                PaidAmount: nrdPayment?.Status == "Paid" ? nrdPaid : 0m,
                Status: nrdStatus,
                PaymentMethod: nrdPayment?.PaymentMethod ?? "Cash",
                PaidDate: nrdPayment?.PaidDate,
                RecordedByName: nrdPayment?.RecordedByNavigation?.FullName,
                ReceiptAvailable: nrdPayment?.ReceiptContent != null || nrdPayment?.ReceiptFilePath != null,
                ReceiptUrl: $"/api/wpsm/projects/{project.ProjectCode}/payments/NRD/receipt"
            ),
            new(
                PaymentId: t30Payment?.PaymentId,
                PaymentType: "T30",
                MilestoneName: PaymentCalculator.GetMilestoneName("T30"),
                DueDate: t30DueDate,
                Amount: t30Amount,
                PaidAmount: t30PaidAmount,
                Status: t30Status,
                PaymentMethod: t30Payment?.PaymentMethod,
                PaidDate: t30Payment?.PaidDate,
                RecordedByName: t30Payment?.RecordedByNavigation?.FullName,
                ReceiptAvailable: t30Payment?.ReceiptContent != null || t30Payment?.ReceiptFilePath != null,
                ReceiptUrl: t30Payment?.ReceiptContent != null || t30Payment?.ReceiptFilePath != null
                    ? $"/api/wpsm/projects/{project.ProjectCode}/payments/T30/receipt"
                    : null
            ),
            new(
                PaymentId: finalPayment?.PaymentId,
                PaymentType: "FINAL",
                MilestoneName: PaymentCalculator.GetMilestoneName("FINAL"),
                DueDate: finalDueDate,
                Amount: finalAmount,
                PaidAmount: finalPaidAmount,
                Status: finalStatus,
                PaymentMethod: finalPayment?.PaymentMethod,
                PaidDate: finalPayment?.PaidDate,
                RecordedByName: finalPayment?.RecordedByNavigation?.FullName,
                ReceiptAvailable: finalPayment?.ReceiptContent != null || finalPayment?.ReceiptFilePath != null || summary.IsSettled,
                ReceiptUrl: finalPayment?.ReceiptContent != null || finalPayment?.ReceiptFilePath != null || summary.IsSettled
                    ? $"/api/wpsm/projects/{project.ProjectCode}/settlement/receipt"
                    : null
            )
        };
    }

    private static IReadOnlyList<ManagerPaymentHistoryItemView> BuildHistory(WeddingProject project)
    {
        return project.Payments
            .OrderByDescending(p => p.PaidDate ?? p.DueDate)
            .ThenByDescending(p => p.PaymentId)
            .Select(p => new ManagerPaymentHistoryItemView(
                PaymentId: p.PaymentId,
                PaymentType: p.PaymentType,
                Amount: p.Amount ?? 0m,
                PaymentDate: p.PaidDate,
                Method: p.PaymentMethod,
                Status: p.Status,
                RecordedByName: p.RecordedByNavigation?.FullName,
                ReceiptAvailable: p.ReceiptContent != null || p.ReceiptFilePath != null,
                ReceiptUrl: $"/api/wpsm/projects/{project.ProjectCode}/payments/{p.PaymentType}/receipt",
                CreatedAt: p.PaidDate.HasValue ? p.PaidDate.Value.ToDateTime(TimeOnly.MinValue) : null
            ))
            .ToList();
    }

    private static SettlementCalculationView BuildSettlementCalculation(WeddingProject project, DateOnly today)
    {
        var crVariance = PaymentCalculator.CalculateApprovedCrVariance(
            project.ChangeRequests.Select(c => (c.Status, c.TotalVariance, c.CostImpact, c.PenaltyFee)));
        var currentTotal = PaymentCalculator.CalculateCurrentTotal(project.BaseContractValue, crVariance);

        var payments = project.Payments.Where(p => p.Status == "Paid").ToList();
        var nrdPaid = payments.FirstOrDefault(p => string.Equals(p.PaymentType, "NRD", StringComparison.OrdinalIgnoreCase))?.Amount ?? 0m;
        var t30Paid = payments.FirstOrDefault(p =>
            string.Equals(p.PaymentType, "T30", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.PaymentType, "T-30", StringComparison.OrdinalIgnoreCase))?.Amount ?? 0m;
        var finalPaid = payments.FirstOrDefault(p => string.Equals(p.PaymentType, "FINAL", StringComparison.OrdinalIgnoreCase))?.Amount ?? 0m;
        var otherPaid = payments.Where(p =>
            !string.Equals(p.PaymentType, "NRD", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(p.PaymentType, "T30", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(p.PaymentType, "T-30", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(p.PaymentType, "FINAL", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Amount ?? 0m);

        var totalPaid = nrdPaid + t30Paid + finalPaid + otherPaid;
        var finalDue = PaymentCalculator.CalculateOutstandingBalance(currentTotal, totalPaid);
        var isEventCompleted = PaymentCalculator.IsEventCompleted(project.EventDate, project.Status, today);
        var isSettled = string.Equals(project.Status, "Settled", StringComparison.OrdinalIgnoreCase);

        return new SettlementCalculationView(
            BaseContractValue: project.BaseContractValue,
            ApprovedCrVariance: crVariance,
            CurrentProjectTotal: currentTotal,
            NrdPaid: nrdPaid,
            T30Paid: t30Paid,
            OtherRecordedPayments: otherPaid + finalPaid,
            TotalRecordedPayments: totalPaid,
            FinalAmountDue: finalDue,
            IsEventCompleted: isEventCompleted,
            CanSettle: isEventCompleted && finalDue == 0,
            IsSettled: isSettled,
            CalculatedAt: DateTime.UtcNow
        );
    }

    private static FinalSettlementReceiptPdfModel BuildReceiptPdfModel(
        WeddingProject project,
        decimal currentTotal,
        decimal totalPaid,
        string managerName,
        DateTime issueDate)
    {
        var crVariance = PaymentCalculator.CalculateApprovedCrVariance(
            project.ChangeRequests.Select(c => (c.Status, c.TotalVariance, c.CostImpact, c.PenaltyFee)));

        var paymentItems = project.Payments
            .Where(p => p.Status == "Paid")
            .OrderBy(p => p.PaidDate ?? p.DueDate)
            .Select(p => (
                Type: PaymentCalculator.GetMilestoneName(p.PaymentType),
                Date: p.PaidDate,
                Method: p.PaymentMethod,
                Amount: p.Amount ?? 0m,
                RecordedBy: (string?)(p.RecordedByNavigation?.FullName ?? managerName)
            ))
            .ToList();

        var receiptNo = $"FSR-{project.ProjectCode}-{issueDate:yyyyMMddHHmm}";

        return new FinalSettlementReceiptPdfModel(
            ReceiptNumber: receiptNo,
            ProjectCode: project.ProjectCode,
            CoupleName: project.Customer.FullName,
            CustomerEmail: project.Customer.Email,
            CustomerPhone: project.Customer.Phone ?? "",
            EventDate: project.EventDate,
            HallName: project.Hall.Name,
            BaseContractValue: project.BaseContractValue,
            TotalApprovedCrVariance: crVariance,
            CurrentProjectTotal: currentTotal,
            TotalAmountPaid: totalPaid,
            FinalBalance: 0m,
            Status: "Settled",
            IssuingManagerName: managerName,
            IssueDate: issueDate,
            Payments: paymentItems
        );
    }

    private static FinalSettlementReceiptView MapFinalReceiptView(
        WeddingProject project,
        SettlementCalculationView calc,
        string managerName,
        DateTime issueDate)
    {
        var paymentHistory = project.Payments
            .Where(p => p.Status == "Paid")
            .OrderBy(p => p.PaidDate ?? p.DueDate)
            .Select(p => new SettlementPaymentItemView(
                PaymentType: PaymentCalculator.GetMilestoneName(p.PaymentType),
                PaidDate: p.PaidDate,
                Method: p.PaymentMethod,
                Amount: p.Amount ?? 0m,
                RecordedBy: p.RecordedByNavigation?.FullName ?? managerName
            ))
            .ToList();

        var receiptNo = $"FSR-{project.ProjectCode}-{issueDate:yyyyMMddHHmm}";

        return new FinalSettlementReceiptView(
            ReceiptNumber: receiptNo,
            ProjectCode: project.ProjectCode,
            CoupleName: project.Customer.FullName,
            CustomerEmail: project.Customer.Email,
            CustomerPhone: project.Customer.Phone ?? "",
            EventDate: project.EventDate,
            HallName: project.Hall.Name,
            BaseContractValue: calc.BaseContractValue,
            TotalApprovedCrVariance: calc.ApprovedCrVariance,
            CurrentProjectTotal: calc.CurrentProjectTotal,
            TotalAmountPaid: calc.TotalRecordedPayments,
            FinalBalance: 0m,
            Status: "Settled",
            IssuingManagerName: managerName,
            IssueDate: issueDate,
            PaymentHistory: paymentHistory,
            DownloadUrl: $"/api/wpsm/projects/{project.ProjectCode}/settlement/receipt"
        );
    }
}
