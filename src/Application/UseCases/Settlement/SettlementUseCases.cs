using Application.Persistence;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;

namespace Application.UseCases.Settlement;

/// <summary>A receipt file once stored (Cloudinary): the database keeps its key, not its bytes.</summary>
public sealed record StoredReceipt(string Key, string Name, string ContentType);

/// <summary>Data access for the settlement use cases; implemented in Infrastructure. Entities come back tracked.</summary>
public interface ISettlementStore
{
    /// <summary>The event with its customer, operator, hall, slot and payments, within the caller's scope; null otherwise.</summary>
    Task<WeddingProject?> GetEventAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default);
    Task<decimal> GetConfirmedAddonTotalAsync(int projectId, CancellationToken cancellationToken = default);
    /// <summary>The settlement invoice of the event, staged now when the daily job (JOB-06) has not issued it yet.</summary>
    Task<SettlementInvoice> StageInvoiceAsync(WeddingProject project, int? issuedBy, CancellationToken cancellationToken = default);
    /// <summary>Locked events held on or before this day, with customer and payments (JOB-06 catches up missed days).</summary>
    Task<IReadOnlyList<WeddingProject>> GetLockedEventsHeldUntilAsync(DateOnly lastEventDay, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetOperatorIdsAsync(int projectId, CancellationToken cancellationToken = default);
    /// <summary>Checks the file by its content (JPG, PNG, HEIC, WebP, PDF) and stores it. Saves its own row: call inside the transaction.</summary>
    Task<StoredReceipt> StoreReceiptAsync(int projectId, string fileName, byte[] content, int uploaderId, CancellationToken cancellationToken = default);
    void Add(Payment payment);
}

/// <summary>The settlement receipt PDF (generated and stored in Infrastructure).</summary>
public interface ISettlementDocuments
{
    Task StoreSettlementReceiptAsync(WeddingProject project, int managerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case (UC-27, M-12): the operator records the settlement after the event. One payment for the exact remainder closes the event:
/// the invoice is paid, the event settled, the settlement receipt generated, the customer told. All database changes in one transaction.
/// </summary>
public sealed class RecordSettlement(ISettlementStore store, ISettlementDocuments documents, IEventMessages messages, IManagerPaymentRepository reads,
    IUnitOfWork unitOfWork)
{
    public async Task<RecordPaymentResult> HandleAsync(string projectCode, int managerId, string role, RecordPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventAsync(projectCode, managerId, role, cancellationToken);
        if (project is null) return new RecordPaymentResult(false, "Không tìm thấy sự kiện trong phạm vi quyền hạn.");

        var today = VietnamTime.GetVietnamToday();
        SettlementPolicy.EnsureCanRecord(project.Status, command.PaymentType, project.EventDate, command.PaymentDate, today);
        var addonTotal = await store.GetConfirmedAddonTotalAsync(project.ProjectId, cancellationToken);
        SettlementPolicy.EnsureExactAmount(command.Amount,
            SettlementPolicy.Outstanding(project.CurrentTotal, addonTotal, project.Payments.Select(p => (p.Status, p.Amount))));
        var hasReceipt = command.ReceiptBytes is { Length: > 0 };
        var method = SettlementPolicy.Method(command.Method, hasReceipt);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var receipt = hasReceipt
            ? await store.StoreReceiptAsync(project.ProjectId, command.ReceiptFileName ?? $"Settlement_{project.ProjectCode}", command.ReceiptBytes!, managerId, cancellationToken)
            : null;

        project.MarkHeld();
        var invoice = await store.StageInvoiceAsync(project, managerId, cancellationToken);
        var payment = project.Payments.FirstOrDefault(p => p.PaymentType == PaymentKinds.Settlement);
        if (payment is null)
        {
            payment = Payment.NewSettlement(project.ProjectId, invoice.DueDate);
            store.Add(payment);
        }
        payment.RecordSettlement(command.Amount, method, command.PaymentDate, managerId, receipt?.Key, receipt?.Name, receipt?.ContentType);
        project.MarkSettled();
        invoice.MarkPaid(DateTime.UtcNow);
        await messages.SettlementRecordedAsync(project.CustomerId, project.ProjectCode, command.Amount, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await documents.StoreSettlementReceiptAsync(project, managerId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RecordPaymentResult(true, $"Đã ghi nhận tất toán {EventStageRules.Vnd(command.Amount)}. Sự kiện chuyển sang Đã tất toán.",
            payment.PaymentId, 0m, true, await reads.GetProjectPaymentsAsync(projectCode, managerId, role, cancellationToken));
    }
}

/// <summary>Use case (M-12): the operator issues the settlement receipt once everything is paid; the event becomes settled.</summary>
public sealed class IssueSettlementReceipt(ISettlementStore store, ISettlementDocuments documents, IManagerPaymentRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<FinalSettlementReceiptView?> HandleAsync(string projectCode, int managerId, string role, CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventAsync(projectCode, managerId, role, cancellationToken);
        if (project is null) return null;
        var addonTotal = await store.GetConfirmedAddonTotalAsync(project.ProjectId, cancellationToken);
        SettlementPolicy.EnsureCanIssueReceipt(PaymentCalculator.IsEventCompleted(project.EventDate, project.Status, VietnamTime.GetVietnamToday()),
            SettlementPolicy.Outstanding(project.CurrentTotal, addonTotal, project.Payments.Select(p => (p.Status, p.Amount))));

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await documents.StoreSettlementReceiptAsync(project, managerId, cancellationToken);
        project.MarkSettled();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await reads.GetSettlementReceiptAsync(projectCode, managerId, role, cancellationToken);
    }
}
