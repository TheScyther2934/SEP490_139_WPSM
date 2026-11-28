using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Files;
using Infrastructure.Persistence;
using Domain.Entities;

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
        var finalMilestone = milestones.FirstOrDefault(m => string.Equals(m.PaymentType, "Settlement", StringComparison.OrdinalIgnoreCase));
        var finalReceiptAvailable = isSettled && (finalMilestone?.ReceiptAvailable ?? false);

        var finalReceiptInfo = new FinalSettlementReceiptInfo(
            IsAvailable: finalReceiptAvailable,
            ProjectStatus: project.Status,
            FileName: finalReceiptAvailable ? $"Bien_nhan_tat_toan_{project.ProjectCode}.pdf" : null,
            DownloadUrl: finalReceiptAvailable
                ? $"/api/wpsm/customer/payments/final-receipt?projectCode={Uri.EscapeDataString(project.ProjectCode)}"
                : null,
            Message: !isSettled
                ? "Biên nhận tất toán có sau khi bạn thanh toán phần còn lại theo hóa đơn cuối (phát hành sau ngày cưới)."
                : finalReceiptAvailable
                    ? "Biên nhận tất toán đã sẵn sàng để tải về."
                    : "Sự kiện đã tất toán nhưng biên nhận chưa được tải lên."
        );

        return new CustomerPaymentsOverviewView(summary, milestones, finalReceiptInfo)
        {
            Invoice = await EventFlowSupport.LoadInvoiceViewAsync(db, project.ProjectId, project.ProjectCode, cancellationToken)
        };
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
            payment = project.Payments.FirstOrDefault(p => string.Equals(p.PaymentType, paymentType, StringComparison.OrdinalIgnoreCase));
        }

        // The receipt recorded on the payment (its storage key), else the latest stored file of that kind.
        if (payment?.ReceiptFilePath is { Length: > 0 } key && await ReadAsync(key, cancellationToken) is { } content)
        {
            return new FileContentResultModel(
                content,
                payment.ReceiptContentType ?? "application/pdf",
                payment.ReceiptFileName ?? $"Receipt_{project.ProjectCode}_{payment.PaymentType}.pdf");
        }

        // Check StoredFiles
        var purpose = paymentType switch
        {
            PaymentKinds.Deposit => "DepositReceipt",
            PaymentKinds.Settlement => "SettlementReceipt",
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
            throw new InvalidOperationException("Biên nhận tất toán chỉ tải được khi sự kiện đã tất toán.");
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
            string.Equals(p.PaymentType, PaymentKinds.Settlement, StringComparison.OrdinalIgnoreCase));

        if (finalPayment?.ReceiptFilePath is { Length: > 0 } receiptKey && await ReadAsync(receiptKey, cancellationToken) is { } bytes)
        {
            return new FileContentResultModel(
                bytes,
                finalPayment.ReceiptContentType ?? "application/pdf",
                finalPayment.ReceiptFileName ?? $"Bien_nhan_tat_toan_{project.ProjectCode}.pdf");
        }

        return null;
    }

    // ── Internal Helpers ──────────────────────────────────────────────────────

    private async Task<byte[]?> ReadAsync(string key, CancellationToken cancellationToken)
    {
        await using var stream = await storage.OpenReadAsync(key, cancellationToken);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private async Task<WeddingProject?> ResolveCustomerProjectAsync(
        int customerId,
        string? projectCode,
        CancellationToken cancellationToken)
    {
        var query = db.WeddingProjects
            .AsNoTracking()
            .Include(p => p.Payments)
            .Where(p => p.CustomerId == customerId && p.Status != EventStatus.Cancelled);

        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            query = query.Where(p => p.ProjectCode == projectCode);
        }

        return await query.OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<CustomerPaymentSummaryView> BuildSummaryAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        var addonTotal = await EventFlowSupport.ConfirmedAddonTotalAsync(db, project.ProjectId, cancellationToken);
        var grandTotal = PaymentCalculator.CalculateGrandTotal(project.CurrentTotal, addonTotal);
        var totalPaid = PaymentCalculator.CalculateTotalPaid(project.Payments.Select(p => (p.Status, p.Amount)));
        return new CustomerPaymentSummaryView(project.ProjectId, project.ProjectCode, project.Status, project.CurrentTotal, addonTotal,
            grandTotal, totalPaid, PaymentCalculator.CalculateOutstandingBalance(grandTotal, totalPaid), "VND");
    }

    /// <summary>Two payments: the deposit paid at signing and the settlement due after the event.</summary>
    private async Task<IReadOnlyList<CustomerPaymentMilestoneView>> BuildMilestonesAsync(
        WeddingProject project, CustomerPaymentSummaryView summary, DateOnly today, CancellationToken cancellationToken)
    {
        var policy = await EventFlowSupport.LoadEventPolicyAsync(db, project.ProjectId, cancellationToken);   // terms signed with
        var code = Uri.EscapeDataString(project.ProjectCode);

        var deposit = project.Payments.FirstOrDefault(p => p.PaymentType == PaymentKinds.Deposit);
        var depositAmount = deposit?.Amount ?? project.DepositAmount;
        var depositDue = deposit?.DueDate ?? DateOnly.FromDateTime(project.CreatedAt);
        var depositStatus = PaymentCalculator.ResolvePaymentStatus(deposit?.Status, depositDue, today);
        var depositReceipt = !string.IsNullOrEmpty(deposit?.ReceiptFilePath) ||
            await db.StoredFiles.AnyAsync(f => f.ProjectId == project.ProjectId && f.Purpose == "DepositReceipt", cancellationToken);

        var settlement = project.Payments.FirstOrDefault(p => p.PaymentType == PaymentKinds.Settlement);
        var settled = project.Status == EventStatus.Settled;
        var settlementDue = settlement?.DueDate ?? EventStageRules.SettlementDueDate(project.EventDate, policy);
        var depositPaid = depositStatus == "Paid" ? depositAmount : 0m;
        var settlementAmount = settlement?.Status == "Paid" ? settlement.Amount ?? 0m
            : PaymentCalculator.CalculateSettlementAmount(summary.BaseContractValue, summary.ConfirmedAddonTotal, depositPaid);
        var completed = PaymentCalculator.IsEventCompleted(project.EventDate, project.Status, today);
        var settlementStatus = settled || settlement?.Status == "Paid" ? "Paid" : completed ? "Due" : "Upcoming";
        var settlementReceipt = settled && (!string.IsNullOrEmpty(settlement?.ReceiptFilePath) ||
            await db.StoredFiles.AnyAsync(f => f.ProjectId == project.ProjectId && f.Purpose == "SettlementReceipt", cancellationToken));

        return
        [
            new(deposit?.PaymentId, PaymentKinds.Deposit, PaymentKinds.Label(PaymentKinds.Deposit), depositDue, depositAmount, depositPaid, depositStatus,
                deposit?.PaymentMethod, deposit?.PaidDate, depositReceipt,
                depositReceipt ? $"/api/wpsm/customer/payments/{PaymentKinds.Deposit}/receipt?projectCode={code}" : null),
            new(settlement?.PaymentId, PaymentKinds.Settlement, PaymentKinds.Label(PaymentKinds.Settlement), settlementDue, settlementAmount,
                settlementStatus == "Paid" ? settlementAmount : 0m, settlementStatus, settlement?.PaymentMethod, settlement?.PaidDate, settlementReceipt,
                settlementReceipt ? $"/api/wpsm/customer/payments/final-receipt?projectCode={code}" : null)
        ];
    }
}
