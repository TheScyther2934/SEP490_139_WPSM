using Domain.Services;

namespace Application.UseCases;

/// <summary>
/// Commits everything a use case changed (data, notifications, queued emails) at once, so a failure leaves nothing half done.
/// Stores and messages only stage their changes; the use case decides when to save.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    /// <summary>For a use case that must save twice (a new row's key is needed before the rest): both saves commit or neither.</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>Rolled back when disposed without <see cref="CommitAsync"/>.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>The event as the use cases see it: who the customer is and where the event stands.</summary>
public sealed record EventParty(int ProjectId, string ProjectCode, string Status, DateOnly EventDate, int CustomerId, string CustomerName, string CustomerEmail);

/// <summary>
/// In-app notifications and emails of the business events. Wording, links and email templates are a delivery detail (Infrastructure);
/// the use cases only say what happened. Staged with the use case's changes and committed by IUnitOfWork.
/// </summary>
public interface IEventMessages
{
    Task CoordinationSheetApprovedAsync(EventParty party, int version, CancellationToken cancellationToken = default);
    Task AddonAwaitingConfirmationAsync(EventParty party, string addonCode, string title, decimal total, CancellationToken cancellationToken = default);
    Task AddonCancelledAsync(EventParty party, string addonCode, string reason, CancellationToken cancellationToken = default);
    Task AddonConfirmedAsync(EventParty party, string addonCode, decimal total, CancellationToken cancellationToken = default);
    Task ServicesLockedAsync(EventParty party, EventPolicy policy, CancellationToken cancellationToken = default);
    /// <summary>The event is signed: the customer is told (deposit paid or due); a new account also gets its temporary password by email.</summary>
    Task EventBookedAsync(EventParty party, bool depositPaid, string? temporaryPassword, CancellationToken cancellationToken = default);
    Task DepositReceiptRecordedAsync(EventParty party, CancellationToken cancellationToken = default);
    Task CredentialsResentAsync(EventParty party, string temporaryPassword, CancellationToken cancellationToken = default);
    /// <summary>The settlement was recorded: the customer can download the settlement receipt.</summary>
    Task SettlementRecordedAsync(int customerId, string projectCode, decimal amount, CancellationToken cancellationToken = default);
    /// <summary>The event was cancelled: the customer is told the deposit is kept.</summary>
    Task EventCancelledAsync(int customerId, string projectCode, CancellationToken cancellationToken = default);
    /// <summary>JOB-01: the supplier deadline passed and these services still have no evidence.</summary>
    Task SupplierEvidenceMissingAsync(int operatorId, string projectCode, IReadOnlyList<string> services, CancellationToken cancellationToken = default);
    /// <summary>JOB-06 issued the settlement invoice: the operators and the customer are told what is due and when.</summary>
    Task SettlementInvoiceIssuedAsync(IReadOnlyList<int> operatorIds, int customerId, string projectCode, string invoiceCode, decimal amountDue, DateOnly dueDate,
        CancellationToken cancellationToken = default);
    /// <summary>The settlement invoice by email to the customer (may fail without undoing the invoice).</summary>
    Task SettlementInvoiceEmailAsync(Domain.Entities.WeddingProject project, Domain.Entities.SettlementInvoice invoice, decimal amountDue,
        CancellationToken cancellationToken = default);
    Task CoOperatorAddedAsync(int managerId, string projectCode, string reason, CancellationToken cancellationToken = default);
    Task CoOperatorRemovedAsync(int managerId, string projectCode, string reason, CancellationToken cancellationToken = default);
    /// <summary>JOB-03 compiled the v1 draft of a locked event: the operator checks and approves it.</summary>
    Task CoordinationDraftReadyAsync(int managerId, string projectCode, CancellationToken cancellationToken = default);
    Task ComplaintFiledAsync(int managerId, string projectCode, string complaintCode, int sheetVersion, CancellationToken cancellationToken = default);
    Task ComplaintResolvedByOperatorAsync(int customerId, string complaintCode, CancellationToken cancellationToken = default);
    /// <summary>The active Admins and the customer are told the complaint now waits for the Admin.</summary>
    Task ComplaintEscalatedAsync(int customerId, string projectCode, string complaintCode, CancellationToken cancellationToken = default);
    Task ComplaintResolvedByAdminAsync(int managerId, int customerId, string projectCode, string complaintCode, CancellationToken cancellationToken = default);
    /// <summary>The Admin decided a special exception request (UC-47): the operator who asked is told.</summary>
    Task ExceptionDecidedAsync(int requestedBy, string requestCode, string projectCode, bool approved, CancellationToken cancellationToken = default);
}
