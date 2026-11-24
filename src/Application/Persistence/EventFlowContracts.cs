using Domain.Services;

namespace Application.Persistence;

// Event stage flow: notes per stage, addon contracts, supplier evidence, the settlement invoice and the customer view.

public sealed record OperatorContactView(string FullName, string Role, string? Phone, string Email);

public sealed record StageNoteView(int Id, int Stage, string StageTitle, string NoteType, string Content,
    decimal? ValueBefore, decimal? ValueAfter, string CreatedBy, DateTime CreatedAt);
public sealed record StageNoteCommand(string NoteType, string Content);

public sealed record AddonItemCommand(int? CatalogItemId, string Description, int Quantity, decimal UnitPrice);
public sealed record AddonCreateCommand(string Title, string? Note, IReadOnlyList<AddonItemCommand> Items);
public sealed record AddonItemView(int? CatalogItemId, string Description, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record AddonContractView(string Code, string EventCode, string Title, string? Note, string Status, decimal Total,
    string CreatedBy, DateTime CreatedAt, DateTime? ConfirmedAt, DateTime? CancelledAt, string? CancelReason, DateTime? RemindedAt,
    IReadOnlyList<AddonItemView> Items);

public sealed record SupplierEvidenceView(int Id, string SupplierName, string EvidenceType, int? SpecItemId, string? ServiceName,
    string? Note, Guid FileId, string FileName, string UploadedBy, DateTime UploadedAt);
public sealed record SupplierChecklistLine(int SpecItemId, string ServiceName, string Category, int EvidenceCount);
public sealed record SupplierBoardView(string EventCode, string Status, DateOnly LockDate, DateOnly SupplierDeadline, bool CanUpload, bool Overdue,
    IReadOnlyList<SupplierChecklistLine> Lines, IReadOnlyList<SupplierEvidenceView> Evidences);
public sealed record SupplierEvidenceCommand(string SupplierName, string EvidenceType, int? SpecItemId, string? Note,
    byte[] Content, string FileName, string ContentType);

public sealed record InvoiceLineView(string LineType, string Description, decimal Amount, string? AddonCode);
public sealed record SettlementInvoiceView(string Code, string EventCode, decimal ContractTotal, decimal AddonTotal, decimal DepositPaid,
    decimal AmountDue, DateOnly DueDate, string Status, DateTime IssuedAt, DateTime? PaidAt, IReadOnlyList<InvoiceLineView> Lines);

/// <summary>Operator view of an event: stages, the money limits of stage 2, notes and addon contracts.</summary>
public sealed record EventWorkspaceView(string EventCode, string CoupleName, string Status, string StatusLabel, int CurrentStage,
    IReadOnlyList<EventStageView> Stages, decimal SignedValue, decimal CurrentTotal, decimal MinimumTotal, decimal DepositAmount,
    DateOnly EventDate, DateOnly LockDate, DateOnly SupplierDeadline, bool CanAdjust, bool CanAddAddon, bool CanCancel,
    IReadOnlyList<StageNoteView> Notes, IReadOnlyList<AddonContractView> Addons, IReadOnlyList<OperatorContactView> Contacts,
    SettlementInvoiceView? Invoice);

/// <summary>Customer view of one booked event: read-only services, notes, addon contracts to confirm, who to contact.</summary>
public sealed record CustomerEventView(string EventCode, string CoupleName, string Hall, string Slot, string SlotTime, DateOnly EventDate,
    int CountdownDays, string Status, string StatusLabel, int CurrentStage, IReadOnlyList<EventStageView> Stages,
    decimal SignedValue, decimal CurrentTotal, decimal DepositAmount, decimal ConfirmedAddonTotal, bool CanAdjust, DateOnly LockDate,
    IReadOnlyList<SpecificationLineView> Services, IReadOnlyList<StageNoteView> Notes, IReadOnlyList<AddonContractView> Addons,
    IReadOnlyList<OperatorContactView> Contacts, string Hotline, SettlementInvoiceView? Invoice);

public sealed record CustomerEventRef(string EventCode, DateOnly EventDate, string Hall, string Status, string StatusLabel, bool CanAddAddon);
public sealed record CustomerLookupView(int UserId, string FullName, string Email, string? Phone, IReadOnlyList<CustomerEventRef> Events);

public interface IEventFlowRepository
{
    Task<EventWorkspaceView?> GetWorkspaceAsync(string eventCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerEventRef>> GetCustomerEventsAsync(int customerId, CancellationToken cancellationToken = default);
    Task<CustomerEventView?> GetCustomerEventAsync(int customerId, string? eventCode, CancellationToken cancellationToken = default);
    /// <summary>Customers (with their events) for the "addon contract for an existing event" choice of the create form.</summary>
    Task<IReadOnlyList<CustomerLookupView>> SearchCustomersAsync(string? query, int userId, string role, CancellationToken cancellationToken = default);
}

public interface IAddonContractRepository
{
    Task<IReadOnlyList<AddonContractView>> ListAsync(string eventCode, CancellationToken cancellationToken = default);
    Task<AddonContractView?> GetAsync(string eventCode, string addonCode, CancellationToken cancellationToken = default);
}

public interface ISupplierEvidenceRepository
{
    Task<SupplierBoardView?> GetBoardAsync(string eventCode, CancellationToken cancellationToken = default);
    Task<(byte[] Content, string ContentType, string FileName)?> GetFileAsync(string eventCode, int evidenceId, CancellationToken cancellationToken = default);
}
