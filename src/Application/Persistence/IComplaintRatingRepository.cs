namespace Application.Persistence;

public sealed record ModuleRatingInput(string Module, int Stars, string? Comment);

public sealed record ModuleRatingView(
    string Module,
    string ModuleLabel,
    int? Stars,
    string? Comment,
    DateTime? RatedAt);

public sealed record PostEventSummaryView(
    string ProjectCode,
    string CoupleName,
    string HallName,
    string SlotName,
    DateOnly EventDate,
    string ProjectStatus,
    bool CanRate,
    bool IsLocked,
    decimal FinalContractTotal,
    decimal PaidTotal,
    int CompletedTasksCount,
    int TotalTasksCount,
    int OpenComplaintsCount,
    IReadOnlyList<ModuleRatingView> Ratings);

public sealed record BeoOptionView(
    int BeoId,
    int Version,
    string Status,
    DateTime? FrozenAt,
    string? Notes);

public sealed record ComplaintView(
    int ComplaintId,
    string ComplaintCode,
    string ProjectCode,
    string CoupleName,
    string HallName,
    DateOnly EventDate,
    int BeoId,
    int BeoVersion,
    string BeoStatus,
    string? BeoNotes,
    string Description,
    string Status,
    string? EscalationNote,
    DateTime? EscalatedAt,
    string? ResolutionNote,
    DateTime? ResolvedAt,
    string? ResolvedByName,
    DateTime CreatedAt,
    int ProofPhotosCount,
    IReadOnlyList<BeoItemLineView> DisputedBeoItems);

public interface IComplaintRatingRepository
{
    // C-12, C-13: Post-event summary & Rate Services (UC-14 | GBR-10 | MSG16/26)
    Task<PostEventSummaryView?> GetPostEventSummaryAsync(int customerId, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<PostEventSummaryView?> SubmitRatingsAsync(int customerId, string? projectCode, IReadOnlyList<ModuleRatingInput> ratings, CancellationToken cancellationToken = default);

    // C-14, C-15, C-16: Customer Complaints (UC-15, UC-16 | MSG25)
    Task<IReadOnlyList<ComplaintView>> GetCustomerComplaintsAsync(int customerId, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<ComplaintView?> GetCustomerComplaintAsync(int customerId, string complaintCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BeoOptionView>> GetCustomerBeoOptionsAsync(int customerId, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<ComplaintView> CreateCustomerComplaintAsync(int customerId, string? projectCode, int? beoId, string description, CancellationToken cancellationToken = default);

    // M-18, M-19, M-20: Manager Complaints (UC-35, UC-36 | BF-08)
    Task<IReadOnlyList<ComplaintView>> GetManagerComplaintsAsync(int managerId, string? projectCode = null, CancellationToken cancellationToken = default);
    Task<ComplaintView?> GetManagerComplaintAsync(int managerId, string complaintCode, CancellationToken cancellationToken = default);
    Task<ComplaintView?> RespondOrEscalateComplaintAsync(int managerId, string complaintCode, string action, string note, CancellationToken cancellationToken = default);
}
