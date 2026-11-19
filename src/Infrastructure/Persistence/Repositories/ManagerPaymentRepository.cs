using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Files;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Payments of an event seen by its operators: the deposit (stage 1) and the settlement (stage 5).
/// Settlement = contract total after adjustments + confirmed addon contracts - deposit, recorded once after the event.
/// </summary>
public sealed class ManagerPaymentRepository(
    WpsmDbContext db,
    IFileStorage storage,
    INotificationRepository notifications) : IManagerPaymentRepository
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png" };

    private sealed record Money(decimal ContractTotal, decimal AddonTotal, decimal GrandTotal, decimal DepositPaid, decimal SettlementPaid,
        decimal TotalPaid, decimal Outstanding);

    public async Task<ManagerPaymentOverviewView?> GetProjectPaymentsAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        return project is null ? null : await BuildOverviewAsync(project, cancellationToken);
    }

    public async Task<RecordPaymentResult> RecordPaymentAsync(string projectCode, int managerId, string role, RecordPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, managerId, role, cancellationToken);
        if (project is null) return new RecordPaymentResult(false, "Không tìm thấy sự kiện trong phạm vi quyền hạn.");
        if (project.Status is EventStatus.Settled or EventStatus.Closed)
            return new RecordPaymentResult(false, "Sự kiện đã tất toán hoặc đã đóng, không thể ghi nhận thêm thanh toán.");
        if (project.Status == EventStatus.Cancelled)
            return new RecordPaymentResult(false, "Sự kiện đã hủy, không ghi nhận thanh toán.");

        var type = (command.PaymentType ?? "").Trim();
        if (string.Equals(type, PaymentKinds.Deposit, StringComparison.OrdinalIgnoreCase))
            return new RecordPaymentResult(false, "Tiền đặt cọc được ghi nhận khi ký hợp đồng, không ghi nhận lại ở đây.");
        if (!string.Equals(type, PaymentKinds.Settlement, StringComparison.OrdinalIgnoreCase))
            return new RecordPaymentResult(false, "Chỉ ghi nhận được khoản tất toán.");

        var today = PaymentCalculator.GetVietnamToday();
        if (!PaymentCalculator.IsEventCompleted(project.EventDate, project.Status, today))
            return new RecordPaymentResult(false, "Chỉ ghi nhận tất toán sau ngày cưới.");
        if (command.PaymentDate > today)
            return new RecordPaymentResult(false, "Ngày thanh toán không được ở tương lai (giờ Việt Nam).");

        // The invoice fixes the amount: issue it now if the daily job has not run yet.
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        if (project.Status == EventStatus.Locked) project.Status = EventStatus.Completed;
        var invoice = await EventFlowSupport.IssueInvoiceAsync(db, project, managerId, policy, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(project).Collection(p => p.Payments).LoadAsync(cancellationToken);

        var money = await ComputeMoneyAsync(project, cancellationToken);
        if (command.Amount <= 0) return new RecordPaymentResult(false, "Số tiền thanh toán phải lớn hơn 0.");
        if (command.Amount != money.Outstanding)
            return new RecordPaymentResult(false, $"Tất toán thanh toán một lần đúng số còn lại {EventStageRules.Vnd(money.Outstanding)} (đã nhập {EventStageRules.Vnd(command.Amount)}).");

        var method = (command.Method ?? "").Trim().ToLowerInvariant() is "cash" or "tien mat" ? "Cash" : "Bank";
        var hasReceipt = command.ReceiptBytes is { Length: > 0 };
        if (method == "Bank" && !hasReceipt)
            return new RecordPaymentResult(false, "Chuyển khoản bắt buộc tải lên chứng từ (JPG, PNG hoặc PDF).");

        string? storageKey = null;
        var contentType = command.ReceiptContentType ?? "application/pdf";
        if (hasReceipt)
        {
            var ext = Path.GetExtension(command.ReceiptFileName ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                return new RecordPaymentResult(false, "Chứng từ chỉ nhận JPG, PNG hoặc PDF.");
            if (command.ReceiptBytes!.Length > 10 * 1024 * 1024)
                return new RecordPaymentResult(false, "Chứng từ tối đa 10 MB.");
            storageKey = $"payments/{project.ProjectCode}/Settlement_{Guid.NewGuid():N}{ext}";
            using var fileStream = new MemoryStream(command.ReceiptBytes);
            await storage.UploadAsync(fileStream, storageKey, cancellationToken);
            db.StoredFiles.Add(new StoredFile
            {
                FileId = Guid.NewGuid(), ProjectId = project.ProjectId, Purpose = "PaymentReceipt", StorageKey = storageKey,
                OriginalName = command.ReceiptFileName ?? $"Settlement_{project.ProjectCode}{ext}", ContentType = contentType,
                SizeBytes = command.ReceiptBytes.Length, UploadedBy = managerId, UploadedAt = DateTime.UtcNow
            });
        }

        var payment = project.Payments.FirstOrDefault(p => p.PaymentType == PaymentKinds.Settlement);
        if (payment is null)
        {
            payment = new Payment { ProjectId = project.ProjectId, PaymentType = PaymentKinds.Settlement, DueDate = invoice.DueDate };
            db.Payments.Add(payment);
            project.Payments.Add(payment);
        }
        payment.Status = "Paid";
        payment.Amount = command.Amount;
        payment.PaymentMethod = method;
        payment.PaidDate = command.PaymentDate;
        payment.RecordedBy = managerId;
        if (hasReceipt)
        {
            payment.ReceiptContent = command.ReceiptBytes;
            payment.ReceiptFileName = command.ReceiptFileName;
            payment.ReceiptContentType = contentType;
            payment.ReceiptFilePath = storageKey;
        }

        // One payment closes the event: settled, invoice paid, settlement receipt generated.
        project.Status = EventStatus.Settled;
        invoice.Status = "Paid";
        invoice.PaidAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var managerName = (await db.Users.FindAsync([managerId], cancellationToken))?.FullName ?? "Ban quản lý tiệc cưới";
        money = await ComputeMoneyAsync(project, cancellationToken);
        await StoreSettlementReceiptAsync(project, money, managerName, managerId, cancellationToken);

        try
        {
            await notifications.CreateNotificationAsync(project.CustomerId, NotificationTypes.Payment,
                $"Sự kiện {project.ProjectCode} đã tất toán {EventStageRules.Vnd(command.Amount)}. Biên nhận tất toán đã sẵn sàng tải về.",
                "/customer/payments", "Payment", payment.PaymentId.ToString(), cancellationToken);
        }
        catch (Exception) { /* a failed notification must not undo the recorded payment */ }

        return new RecordPaymentResult(true, $"Đã ghi nhận tất toán {EventStageRules.Vnd(command.Amount)}. Sự kiện chuyển sang Đã tất toán.",
            payment.PaymentId, 0m, true, await BuildOverviewAsync(project, cancellationToken));
    }

    public async Task<SettlementCalculationView?> GetSettlementCalculationAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        return project is null ? null : BuildSettlement(project, await ComputeMoneyAsync(project, cancellationToken));
    }

    public async Task<FinalSettlementReceiptView?> GenerateSettlementReceiptAsync(string projectCode, int managerId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, managerId, role, cancellationToken);
        if (project is null) return null;
        var money = await ComputeMoneyAsync(project, cancellationToken);
        var calc = BuildSettlement(project, money);
        if (!calc.IsEventCompleted) throw new InvalidOperationException("Chỉ phát hành biên nhận tất toán sau ngày cưới.");
        if (calc.FinalAmountDue > 0) throw new InvalidOperationException($"Còn {EventStageRules.Vnd(calc.FinalAmountDue)} chưa thanh toán.");

        var managerName = (await db.Users.FindAsync([managerId], cancellationToken))?.FullName ?? "Ban quản lý tiệc cưới";
        await StoreSettlementReceiptAsync(project, money, managerName, managerId, cancellationToken);
        project.Status = EventStatus.Settled;
        await db.SaveChangesAsync(cancellationToken);
        return MapReceiptView(project, money, managerName, DateTime.UtcNow);
    }

    public async Task<FileContentResultModel?> GetSettlementReceiptFileAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;
        var stored = await db.StoredFiles.AsNoTracking().Where(f => f.ProjectId == project.ProjectId && f.Purpose == "SettlementReceipt")
            .OrderByDescending(f => f.UploadedAt).FirstOrDefaultAsync(cancellationToken);
        if (stored is not null && await ReadStoredAsync(stored, cancellationToken) is { } file) return file;

        var money = await ComputeMoneyAsync(project, cancellationToken);
        if (project.Status != EventStatus.Settled && money.Outstanding > 0) return null;
        var pdf = ReceiptPdfGenerator.GenerateFinalSettlementReceiptPdf(BuildPdfModel(project, money, project.Manager?.FullName ?? "Ban quản lý", DateTime.UtcNow));
        return new FileContentResultModel(pdf, "application/pdf", $"Bien_nhan_tat_toan_{project.ProjectCode}.pdf");
    }

    public async Task<FileContentResultModel?> GetPaymentReceiptFileAsync(string projectCode, string paymentType, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;
        var payment = project.Payments.FirstOrDefault(p => string.Equals(p.PaymentType, paymentType, StringComparison.OrdinalIgnoreCase));
        if (payment?.ReceiptContent is { Length: > 0 } content)
            return new FileContentResultModel(content, payment.ReceiptContentType ?? "application/pdf", payment.ReceiptFileName ?? $"Receipt_{project.ProjectCode}_{payment.PaymentType}.pdf");
        var purpose = string.Equals(paymentType, PaymentKinds.Deposit, StringComparison.OrdinalIgnoreCase) ? "DepositReceipt" : "PaymentReceipt";
        var stored = await db.StoredFiles.AsNoTracking().Where(f => f.ProjectId == project.ProjectId && f.Purpose == purpose)
            .OrderByDescending(f => f.UploadedAt).FirstOrDefaultAsync(cancellationToken);
        return stored is null ? null : await ReadStoredAsync(stored, cancellationToken);
    }

    private async Task<FileContentResultModel?> ReadStoredAsync(StoredFile stored, CancellationToken cancellationToken)
    {
        await using var stream = await storage.OpenReadAsync(stored.StorageKey, cancellationToken);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return new FileContentResultModel(ms.ToArray(), stored.ContentType, stored.OriginalName);
    }

    private async Task StoreSettlementReceiptAsync(WeddingProject project, Money money, string managerName, int managerId, CancellationToken cancellationToken)
    {
        var pdf = ReceiptPdfGenerator.GenerateFinalSettlementReceiptPdf(BuildPdfModel(project, money, managerName, DateTime.UtcNow));
        var key = $"settlements/{project.ProjectCode}/Receipt_{project.ProjectCode}_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid():N}.pdf";
        using var pdfStream = new MemoryStream(pdf);
        await storage.UploadAsync(pdfStream, key, cancellationToken);
        db.StoredFiles.Add(new StoredFile
        {
            FileId = Guid.NewGuid(), ProjectId = project.ProjectId, Purpose = "SettlementReceipt", StorageKey = key,
            OriginalName = $"Bien_nhan_tat_toan_{project.ProjectCode}.pdf", ContentType = "application/pdf", SizeBytes = pdf.Length,
            UploadedBy = managerId, UploadedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<WeddingProject?> ResolveProjectAsync(string projectCode, int userId, string role, CancellationToken cancellationToken)
    {
        var query = db.WeddingProjects.Include(p => p.Customer).Include(p => p.Manager).Include(p => p.Hall).Include(p => p.Slot)
            .Include(p => p.Payments).ThenInclude(p => p.RecordedByNavigation)
            .Where(p => p.ProjectCode == projectCode);
        if (role == "Customer") query = query.Where(p => p.CustomerId == userId);
        else if (role == "Manager") query = query.Where(p => p.ManagerId == userId || p.ProjectManagers.Any(pm => pm.ManagerId == userId && pm.RemovedAt == null));
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Money> ComputeMoneyAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        var addonTotal = await EventFlowSupport.ConfirmedAddonTotalAsync(db, project.ProjectId, cancellationToken);
        var grand = PaymentCalculator.CalculateGrandTotal(project.CurrentTotal, addonTotal);
        var paid = project.Payments.Where(p => p.Status == "Paid").ToList();
        var deposit = paid.Where(p => p.PaymentType == PaymentKinds.Deposit).Sum(p => p.Amount ?? 0m);
        var settlement = paid.Where(p => p.PaymentType == PaymentKinds.Settlement).Sum(p => p.Amount ?? 0m);
        var total = deposit + settlement;
        return new Money(project.CurrentTotal, addonTotal, grand, deposit, settlement, total, PaymentCalculator.CalculateOutstandingBalance(grand, total));
    }

    private async Task<ManagerPaymentOverviewView> BuildOverviewAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        var today = PaymentCalculator.GetVietnamToday();
        var money = await ComputeMoneyAsync(project, cancellationToken);
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var completed = PaymentCalculator.IsEventCompleted(project.EventDate, project.Status, today);
        var settled = project.Status == EventStatus.Settled;
        var summary = new ManagerPaymentSummaryView(project.ProjectId, project.ProjectCode, project.Customer.FullName, project.Hall.Name, project.Slot.SlotName,
            project.EventDate, project.Status, project.CurrentTotal, money.AddonTotal, money.GrandTotal, money.TotalPaid, money.Outstanding, completed, settled);

        var deposit = project.Payments.FirstOrDefault(p => p.PaymentType == PaymentKinds.Deposit);
        var settlement = project.Payments.FirstOrDefault(p => p.PaymentType == PaymentKinds.Settlement);
        var settlementDue = EventStageRules.SettlementDueDate(project.EventDate, policy);
        var settlementAmount = settlement?.Status == "Paid" ? settlement.Amount ?? 0m
            : PaymentCalculator.CalculateSettlementAmount(money.ContractTotal, money.AddonTotal, money.DepositPaid);
        var milestones = new List<ManagerPaymentMilestoneView>
        {
            new(deposit?.PaymentId, PaymentKinds.Deposit, PaymentKinds.Label(PaymentKinds.Deposit), deposit?.DueDate ?? DateOnly.FromDateTime(project.CreatedAt),
                deposit?.Amount ?? project.DepositAmount, deposit?.Status == "Paid" ? deposit.Amount ?? 0m : 0m,
                PaymentCalculator.ResolvePaymentStatus(deposit?.Status, deposit?.DueDate, today), deposit?.PaymentMethod, deposit?.PaidDate,
                deposit?.RecordedByNavigation?.FullName, HasReceipt(deposit), $"/api/wpsm/projects/{project.ProjectCode}/payments/{PaymentKinds.Deposit}/receipt"),
            new(settlement?.PaymentId, PaymentKinds.Settlement, PaymentKinds.Label(PaymentKinds.Settlement), settlement?.DueDate ?? settlementDue,
                settlementAmount, settlement?.Status == "Paid" ? settlement.Amount ?? 0m : 0m,
                settled || settlement?.Status == "Paid" ? "Paid" : completed ? "Due" : "Upcoming", settlement?.PaymentMethod, settlement?.PaidDate,
                settlement?.RecordedByNavigation?.FullName, HasReceipt(settlement) || settled,
                HasReceipt(settlement) || settled ? $"/api/wpsm/projects/{project.ProjectCode}/settlement/receipt" : null)
        };
        var history = project.Payments.Where(p => p.Status == "Paid").OrderByDescending(p => p.PaidDate).ThenByDescending(p => p.PaymentId)
            .Select(p => new ManagerPaymentHistoryItemView(p.PaymentId, p.PaymentType, p.Amount ?? 0m, p.PaidDate, p.PaymentMethod, p.Status,
                p.RecordedByNavigation?.FullName, HasReceipt(p), $"/api/wpsm/projects/{project.ProjectCode}/payments/{p.PaymentType}/receipt",
                p.PaidDate?.ToDateTime(TimeOnly.MinValue))).ToList();
        return new ManagerPaymentOverviewView(summary, milestones, history, BuildSettlement(project, money))
        {
            Invoice = await EventFlowSupport.LoadInvoiceViewAsync(db, project.ProjectId, project.ProjectCode, cancellationToken)
        };
    }

    private static bool HasReceipt(Payment? p) => p is not null && (p.ReceiptContent != null || p.ReceiptFilePath != null);

    private static SettlementCalculationView BuildSettlement(WeddingProject project, Money money)
    {
        var completed = PaymentCalculator.IsEventCompleted(project.EventDate, project.Status, PaymentCalculator.GetVietnamToday());
        return new SettlementCalculationView(project.CurrentTotal, money.AddonTotal, money.GrandTotal, money.DepositPaid, money.SettlementPaid,
            money.TotalPaid, money.Outstanding, completed, completed && money.Outstanding == 0, project.Status == EventStatus.Settled, DateTime.UtcNow);
    }

    private static FinalSettlementReceiptPdfModel BuildPdfModel(WeddingProject project, Money money, string managerName, DateTime issueDate) =>
        new($"BN-{project.ProjectCode}-{issueDate:yyyyMMddHHmm}", project.ProjectCode, project.Customer.FullName, project.Customer.Email,
            project.Customer.Phone ?? "", project.EventDate, project.Hall.Name, money.ContractTotal, money.AddonTotal, money.GrandTotal, money.TotalPaid,
            0m, EventStatus.Settled, managerName, issueDate,
            project.Payments.Where(p => p.Status == "Paid").OrderBy(p => p.PaidDate)
                .Select(p => (PaymentKinds.Label(p.PaymentType), p.PaidDate, p.PaymentMethod, p.Amount ?? 0m, (string?)(p.RecordedByNavigation?.FullName ?? managerName)))
                .ToList());

    private static FinalSettlementReceiptView MapReceiptView(WeddingProject project, Money money, string managerName, DateTime issueDate) =>
        new($"BN-{project.ProjectCode}-{issueDate:yyyyMMddHHmm}", project.ProjectCode, project.Customer.FullName, project.Customer.Email,
            project.Customer.Phone ?? "", project.EventDate, project.Hall.Name, money.ContractTotal, money.AddonTotal, money.GrandTotal, money.TotalPaid,
            0m, EventStatus.Settled, managerName, issueDate,
            project.Payments.Where(p => p.Status == "Paid").OrderBy(p => p.PaidDate)
                .Select(p => new SettlementPaymentItemView(PaymentKinds.Label(p.PaymentType), p.PaidDate, p.PaymentMethod, p.Amount ?? 0m,
                    p.RecordedByNavigation?.FullName ?? managerName)).ToList(),
            $"/api/wpsm/projects/{project.ProjectCode}/settlement/receipt");
}
