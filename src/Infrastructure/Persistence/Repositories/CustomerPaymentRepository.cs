using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Files;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class CustomerPaymentRepository(
    WpsmDbContext db,
    IFileStorage storage) : ICustomerPaymentRepository
{
    public async Task<CustomerPaymentsOverviewView?> GetPaymentsOverviewAsync(
        int customerId,
        string? projectCode = null,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveCustomerProjectAsync(customerId, projectCode, cancellationToken);
        if (project is null) return null;

        var today = PaymentCalculator.GetVietnamToday();
        var summary = await BuildSummaryAsync(project, cancellationToken);
        var milestones = await BuildMilestonesAsync(project, summary, today, cancellationToken);

        var isSettled = string.Equals(project.Status, "Settled", StringComparison.OrdinalIgnoreCase);
        var finalMilestone = milestones.FirstOrDefault(m => string.Equals(m.PaymentType, "FINAL", StringComparison.OrdinalIgnoreCase));
        var finalReceiptAvailable = isSettled && (finalMilestone?.ReceiptAvailable ?? false);

        var finalReceiptInfo = new FinalSettlementReceiptInfo(
            IsAvailable: finalReceiptAvailable,
            ProjectStatus: project.Status,
            FileName: finalReceiptAvailable ? $"Bien_nhan_quyet_toan_{project.ProjectCode}.pdf" : null,
            DownloadUrl: finalReceiptAvailable
                ? $"/api/wpsm/customer/payments/final-receipt?projectCode={Uri.EscapeDataString(project.ProjectCode)}"
                : null,
            Message: !isSettled
                ? "Biên nhận quyết toán sẽ được Ban điều phối phát hành sau khi tiệc cưới hoàn tất và dự án được xác nhận quyết toán (Settled)."
                : finalReceiptAvailable
                    ? "Biên nhận quyết toán chính thức đã sẵn sàng để tải về."
                    : "Hợp đồng đã quyết toán nhưng Ban điều phối chưa tải lên biên nhận quyết toán số hóa."
        );

        return new CustomerPaymentsOverviewView(summary, milestones, finalReceiptInfo);
    }

    public async Task<CustomerPaymentSummaryView?> GetPaymentSummaryAsync(
        int customerId,
        string? projectCode = null,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveCustomerProjectAsync(customerId, projectCode, cancellationToken);
        if (project is null) return null;

        return await BuildSummaryAsync(project, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerPaymentMilestoneView>?> GetPaymentMilestonesAsync(
        int customerId,
        string? projectCode = null,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveCustomerProjectAsync(customerId, projectCode, cancellationToken);
        if (project is null) return null;

        var today = PaymentCalculator.GetVietnamToday();
        var summary = await BuildSummaryAsync(project, cancellationToken);
        return await BuildMilestonesAsync(project, summary, today, cancellationToken);
    }

    public async Task<FileContentResultModel?> GetPaymentReceiptAsync(
        int customerId,
        int? paymentId = null,
        string? paymentType = null,
        string? projectCode = null,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveCustomerProjectAsync(customerId, projectCode, cancellationToken);
        if (project is null) return null;

        Payment? payment = null;
        if (paymentId.HasValue)
        {
            payment = project.Payments.FirstOrDefault(p => p.PaymentId == paymentId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(paymentType))
        {
            payment = project.Payments.FirstOrDefault(p =>
                string.Equals(p.PaymentType, paymentType, StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(paymentType, "T30", StringComparison.OrdinalIgnoreCase) && string.Equals(p.PaymentType, "T-30", StringComparison.OrdinalIgnoreCase)));
        }

        if (payment?.ReceiptContent is { Length: > 0 } content)
        {
            return new FileContentResultModel(
                content,
                payment.ReceiptContentType ?? "application/pdf",
                payment.ReceiptFileName ?? $"Receipt_{project.ProjectCode}_{payment.PaymentType}.pdf");
        }

        // Check StoredFiles
        var purpose = paymentType?.ToUpperInvariant() switch
        {
            "NRD" => "NrdReceipt",
            "T30" or "T-30" => "PaymentReceipt",
            "FINAL" => "SettlementReceipt",
            _ => "PaymentReceipt"
        };

        var storedFile = await db.StoredFiles.AsNoTracking()
            .Where(f => f.ProjectId == project.ProjectId && f.Purpose == purpose)
            .OrderByDescending(f => f.UploadedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedFile is not null)
        {
            await using var stream = await storage.OpenReadAsync(storedFile.StorageKey, cancellationToken);
            if (stream is not null)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                return new FileContentResultModel(ms.ToArray(), storedFile.ContentType, storedFile.OriginalName);
            }
        }

        return null;
    }

    public async Task<FileContentResultModel?> GetFinalSettlementReceiptAsync(
        int customerId,
        string? projectCode = null,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveCustomerProjectAsync(customerId, projectCode, cancellationToken);
        if (project is null) return null;

        // Security & business rule: Project MUST be Settled
        if (!string.Equals(project.Status, "Settled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Biên nhận quyết toán chỉ có thể tải xuống khi dự án đã hoàn tất quyết toán (Settled).");
        }

        // 1. Try StoredFiles with SettlementReceipt
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

        // 2. Try FINAL Payment row
        var finalPayment = project.Payments.FirstOrDefault(p =>
            string.Equals(p.PaymentType, "FINAL", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.PaymentType, "Final", StringComparison.OrdinalIgnoreCase));

        if (finalPayment?.ReceiptContent is { Length: > 0 } bytes)
        {
            return new FileContentResultModel(
                bytes,
                finalPayment.ReceiptContentType ?? "application/pdf",
                finalPayment.ReceiptFileName ?? $"Bien_nhan_quyet_toan_{project.ProjectCode}.pdf");
        }

        return null;
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    private async Task<WeddingProject?> ResolveCustomerProjectAsync(
        int customerId,
        string? projectCode,
        CancellationToken cancellationToken)
    {
        var query = db.WeddingProjects
            .AsNoTracking()
            .Include(p => p.Payments)
            .Include(p => p.ChangeRequests)
            .Where(p => p.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            query = query.Where(p => p.ProjectCode == projectCode);
        }

        return await query.OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    private Task<CustomerPaymentSummaryView> BuildSummaryAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        var crTuples = project.ChangeRequests.Select(c => (c.Status, c.TotalVariance, c.CostImpact, c.PenaltyFee));
        var approvedCrVariance = PaymentCalculator.CalculateApprovedCrVariance(crTuples);
        var currentTotal = PaymentCalculator.CalculateCurrentTotal(project.BaseContractValue, approvedCrVariance);

        var paymentTuples = project.Payments.Select(p => (p.Status, p.Amount));
        var totalPaid = PaymentCalculator.CalculateTotalPaid(paymentTuples);
        var outstandingBalance = PaymentCalculator.CalculateOutstandingBalance(currentTotal, totalPaid);

        var summary = new CustomerPaymentSummaryView(
            project.ProjectId,
            project.ProjectCode,
            project.Status,
            project.BaseContractValue,
            approvedCrVariance,
            currentTotal,
            totalPaid,
            outstandingBalance,
            "VND");

        return Task.FromResult(summary);
    }

    private async Task<IReadOnlyList<CustomerPaymentMilestoneView>> BuildMilestonesAsync(
        WeddingProject project,
        CustomerPaymentSummaryView summary,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var todayDate = today;
        var t30Ratio = await db.PolicyConfigs.AsNoTracking()
            .Where(p => p.PolicyType == "T30_RATIO" && p.EffectiveFrom <= todayDate)
            .OrderByDescending(p => p.EffectiveFrom)
            .Select(p => p.RatePercent)
            .FirstOrDefaultAsync(cancellationToken);

        if (t30Ratio <= 0) t30Ratio = 70m;

        var milestones = new List<CustomerPaymentMilestoneView>();

        // 1. NRD Deposit
        var nrd = project.Payments.FirstOrDefault(p => string.Equals(p.PaymentType, "NRD", StringComparison.OrdinalIgnoreCase));
        var nrdAmount = nrd?.Amount ?? project.Nrdamount;
        var nrdDueDate = nrd?.DueDate ?? project.EventDate.AddDays(-90);
        var nrdStatus = PaymentCalculator.ResolveMilestoneStatus(nrd?.Status, nrdDueDate, today, project.Status, "NRD");
        var nrdPaid = string.Equals(nrdStatus, "Paid", StringComparison.OrdinalIgnoreCase) ? nrdAmount : 0m;
        var nrdReceiptAvailable = nrd?.ReceiptContent != null || !string.IsNullOrEmpty(nrd?.ReceiptFilePath) ||
                                  await db.StoredFiles.AnyAsync(f => f.ProjectId == project.ProjectId && f.Purpose == "NrdReceipt", cancellationToken);

        milestones.Add(new CustomerPaymentMilestoneView(
            nrd?.PaymentId,
            "NRD",
            PaymentCalculator.GetMilestoneName("NRD"),
            nrdDueDate,
            nrdAmount,
            nrdPaid,
            nrdStatus,
            nrd?.PaymentMethod ?? "Cash",
            nrd?.PaidDate ?? (nrdStatus == "Paid" ? nrdDueDate : null),
            nrdReceiptAvailable,
            nrdReceiptAvailable ? $"/api/wpsm/customer/payments/NRD/receipt?projectCode={Uri.EscapeDataString(project.ProjectCode)}" : null
        ));

        // 2. T-30 Second Payment
        var t30 = project.Payments.FirstOrDefault(p =>
            string.Equals(p.PaymentType, "T30", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.PaymentType, "T-30", StringComparison.OrdinalIgnoreCase));

        var t30Amount = t30?.Amount ?? PaymentCalculator.CalculateT30Amount(summary.CurrentProjectTotal, nrdAmount, t30Ratio);
        var t30DueDate = t30?.DueDate ?? project.EventDate.AddDays(-30);
        var t30Status = PaymentCalculator.ResolveMilestoneStatus(t30?.Status, t30DueDate, today, project.Status, "T30");
        var t30Paid = string.Equals(t30Status, "Paid", StringComparison.OrdinalIgnoreCase) ? t30Amount : 0m;
        var t30ReceiptAvailable = t30?.ReceiptContent != null || !string.IsNullOrEmpty(t30?.ReceiptFilePath) ||
                                  await db.StoredFiles.AnyAsync(f => f.ProjectId == project.ProjectId && f.Purpose == "PaymentReceipt", cancellationToken);

        milestones.Add(new CustomerPaymentMilestoneView(
            t30?.PaymentId,
            "T30",
            PaymentCalculator.GetMilestoneName("T30"),
            t30DueDate,
            t30Amount,
            t30Paid,
            t30Status,
            t30?.PaymentMethod,
            t30?.PaidDate,
            t30ReceiptAvailable,
            t30ReceiptAvailable ? $"/api/wpsm/customer/payments/T30/receipt?projectCode={Uri.EscapeDataString(project.ProjectCode)}" : null
        ));

        // 3. Final Settlement
        var final = project.Payments.FirstOrDefault(p =>
            string.Equals(p.PaymentType, "FINAL", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.PaymentType, "Final", StringComparison.OrdinalIgnoreCase));

        var finalDueDate = final?.DueDate ?? project.EventDate;
        var finalStatus = PaymentCalculator.ResolveMilestoneStatus(final?.Status, finalDueDate, today, project.Status, "FINAL");
        var isSettled = string.Equals(project.Status, "Settled", StringComparison.OrdinalIgnoreCase);

        var finalAmount = final?.Amount ?? (isSettled ? Math.Max(0m, summary.CurrentProjectTotal - nrdPaid - t30Paid) : summary.OutstandingBalance);
        var finalPaid = (isSettled || string.Equals(finalStatus, "Paid", StringComparison.OrdinalIgnoreCase)) ? finalAmount : 0m;

        var finalReceiptAvailable = isSettled && (final?.ReceiptContent != null || !string.IsNullOrEmpty(final?.ReceiptFilePath) ||
                                                 await db.StoredFiles.AnyAsync(f => f.ProjectId == project.ProjectId && f.Purpose == "SettlementReceipt", cancellationToken));

        milestones.Add(new CustomerPaymentMilestoneView(
            final?.PaymentId,
            "FINAL",
            PaymentCalculator.GetMilestoneName("FINAL"),
            finalDueDate,
            finalAmount,
            finalPaid,
            finalStatus,
            final?.PaymentMethod,
            final?.PaidDate ?? (isSettled ? finalDueDate : null),
            finalReceiptAvailable,
            finalReceiptAvailable ? $"/api/wpsm/customer/payments/final-receipt?projectCode={Uri.EscapeDataString(project.ProjectCode)}" : null
        ));

        return milestones;
    }
}
