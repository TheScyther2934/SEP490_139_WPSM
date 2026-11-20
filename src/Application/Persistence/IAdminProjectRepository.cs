namespace Application.Persistence;

// Admin oversight of every wedding project: list with search/filters and a read-only detail that shows who created
// the project, who operates it, its status history, contract content, payments and field evidence.

public sealed record AdminProjectQuery(string? Search, string? Status, int? HallId, int? ManagerId, DateOnly? From, DateOnly? To,
    string? Sort, int Page = 1, int PageSize = 20);

public sealed record PersonRef(int UserId, string Name, string Email, string? Role = null);
public sealed record OptionItem(int Id, string Name);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record AdminProjectRow(string ProjectCode, string CoupleName, string CustomerEmail, string? CustomerPhone, DateOnly EventDate,
    string Hall, string Slot, string Status, decimal CurrentTotal, decimal Paid, decimal Outstanding, PersonRef? CreatedBy,
    IReadOnlyList<PersonRef> Managers, DateTime CreatedAt);

public sealed record AdminProjectFilterOptions(IReadOnlyList<OptionItem> Halls, IReadOnlyList<OptionItem> Managers, IReadOnlyList<string> Statuses);

public sealed record AdminTimelineStep(string Key, string Label, DateTime? At, bool Done, string? By);
public sealed record AdminMoneySummary(decimal SignedValue, decimal DepositAmount, decimal ContractTotal, decimal MinimumTotal, decimal ConfirmedAddonTotal,
    decimal GrandTotal, decimal Paid, decimal Outstanding);
public sealed record AdminSpecLine(string Category, string Name, string Description, int Quantity, string Unit, decimal UnitPrice, decimal LineTotal);
public sealed record AdminEvidencePhoto(int PhotoId, DateTime UploadedAt, string UploadedBy, bool Late)
{
    public string ImageUrl { get; init; } = "";
}
public sealed record AdminEvidenceTask(int TaskId, string TaskName, DateTime Deadline, string Status, bool PhotoRequired, DateTime? CompletedAt,
    string? CompletedBy, IReadOnlyList<AdminEvidencePhoto> Photos);
public sealed record AdminComplaintRow(string Code, string Status, string Description, DateTime CreatedAt, string? ResolutionNote);

public sealed record AdminProjectDetail(string ProjectCode, string CoupleName, string CustomerEmail, string? CustomerPhone, string Hall, string Slot,
    string? SlotTime, DateOnly EventDate, string Status, DateTime CreatedAt, DateTime? LockedAt, PersonRef? CreatedBy,
    IReadOnlyList<PersonRef> Managers, IReadOnlyList<AdminTimelineStep> Timeline, AdminMoneySummary Money,
    IReadOnlyList<AdminSpecLine> Specification, IReadOnlyList<AddonContractView> Addons, IReadOnlyList<StageNoteView> Notes,
    IReadOnlyList<SupplierEvidenceView> Suppliers, SettlementInvoiceView? Invoice,
    IReadOnlyList<AdminEvidenceTask> Evidence, IReadOnlyList<AdminComplaintRow> Complaints)
{
    public string StatusLabel { get; init; } = "";
    public string? CancelReason { get; init; }
}

public interface IAdminProjectRepository
{
    Task<PagedResult<AdminProjectRow>> SearchAsync(AdminProjectQuery query, CancellationToken cancellationToken = default);
    Task<AdminProjectFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
    Task<AdminProjectDetail?> GetDetailAsync(string projectCode, CancellationToken cancellationToken = default);
}
