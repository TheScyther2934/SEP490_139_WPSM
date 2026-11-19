namespace Application.Persistence;

// Contracts for BEO (event coordination sheet), day-of, feedback and resources.
// Payments: IManagerPaymentRepository / ICustomerPaymentRepository; notifications: INotificationRepository.
// Project access (GBR-12) is checked by the /projects/{id} filter.

// ── Day-of (UC-30..UC-32, SDS IV.4.3) ──────────────────────────────────────
public sealed record DayOfPhotoView(int PhotoId, DateTime UploadedAt)
{
    public string ImageUrl { get; init; } = "";
    public string? UploaderName { get; init; }
}
/// <summary>MissingProof: the task is done but has no photo although it requires one (photos can still be added).</summary>
public sealed record DayOfTaskView(int TaskId, string TaskName, DateTime Deadline, string Status, bool PhotoRequired, DateTime? CompletedAt,
    bool IsLate, IReadOnlyList<DayOfPhotoView> Photos)
{
    public string? CompletedBy { get; init; }
    public bool MissingProof => Status == "Done" && PhotoRequired && Photos.Count == 0;
}
/// <summary>CanAddPhotos: proof photos can be added from the event day until the event is settled.</summary>
public sealed record DayOfBoardView(string ProjectCode, string CoupleName, DateOnly EventDate, string SlotTime, string BeoStatus, IReadOnlyList<DayOfTaskView> Tasks)
{
    public string ProjectStatus { get; init; } = "";
    public bool CanAddPhotos { get; init; }
    public int MaxPhotosPerTask { get; init; }
}
public sealed record ProofPhotoUpload(byte[] Content, string FileName);
public sealed record ProjectPhotoView(int PhotoId, string TaskName, DateTime UploadedAt, string UploaderName)
{
    public string ImageUrl { get; init; } = "";
}

public interface IDayOfRepository
{
    Task<DayOfBoardView?> GetBoardAsync(string projectCode, CancellationToken cancellationToken = default);
    /// <summary>UC-13 / C-11: every proof photo of the project, newest first.</summary>
    Task<IReadOnlyList<ProjectPhotoView>> GetPhotosAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<DayOfBoardView?> LoadTimelineAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<DayOfBoardView?> CompleteTaskAsync(string projectCode, int taskId, int managerId, IReadOnlyList<ProofPhotoUpload> photos, CancellationToken cancellationToken = default);
    /// <summary>Adds 1–3 proof photos to a task (done or not), e.g. a task completed without its photos. Up to MaxPhotosPerTask per task.</summary>
    Task<DayOfBoardView?> AddPhotosAsync(string projectCode, int taskId, int managerId, IReadOnlyList<ProofPhotoUpload> photos, CancellationToken cancellationToken = default);
    /// <summary>JOB-04: loads the timeline of every frozen project whose event is today.</summary>
    Task<int> LoadTodayAsync(DateOnly today, CancellationToken cancellationToken = default);
    /// <summary>JOB-05: flags Pending tasks past their deadline (GBR-07) and notifies the Manager (MSG21).</summary>
    Task<int> DetectOverdueAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

// ── Ratings and complaints (UC-14..UC-16, UC-35, UC-36) ────────────────────
public sealed record RatingView(string Module, int Stars, string? Comment, DateTime CreatedAt);
public sealed record RatingCommand(string Module, int Stars, string? Comment);
public sealed record PostEventView(string ProjectCode, string ProjectStatus, bool CanRate, bool CanComplain, IReadOnlyList<RatingView> Ratings,
    IReadOnlyList<int> FrozenBeoVersions);
public sealed record BeoSheetView(string Department, DateTime GeneratedAt, Guid? FileId);
public sealed record ComplaintSummaryView(string Code, int BeoVersion, string Description, string Status, DateTime CreatedAt, DateTime? ResolvedAt);
public sealed record ComplaintDetailView(string Code, string ProjectCode, string CoupleName, int BeoVersion, string Description, string Status,
    string? EscalationNote, string? ResolutionNote, DateTime CreatedAt, DateTime? EscalatedAt, DateTime? ResolvedAt,
    IReadOnlyList<BeoSheetView> BeoSheets, IReadOnlyList<DayOfPhotoView> Photos);

public interface IFeedbackRepository
{
    Task<PostEventView?> GetPostEventAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<PostEventView?> SubmitRatingsAsync(string projectCode, IReadOnlyList<RatingCommand> ratings, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComplaintSummaryView>> ListComplaintsAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<ComplaintDetailView?> GetComplaintAsync(string projectCode, string complaintCode, CancellationToken cancellationToken = default);
    Task<ComplaintDetailView> FileComplaintAsync(string projectCode, int beoVersion, string description, CancellationToken cancellationToken = default);
    Task<ComplaintDetailView?> ResolveAsync(string projectCode, string complaintCode, int managerId, string response, CancellationToken cancellationToken = default);
    Task<ComplaintDetailView?> EscalateAsync(string projectCode, string complaintCode, string note, CancellationToken cancellationToken = default);
}

// ── Resource calendar (UC-25, UC-26, SDS III.2.2) ──────────────────────────
public sealed record ResourceView(int ResourceId, string Name, string Type, int TotalQuantity);
public sealed record AllocationView(int AllocationId, int ResourceId, string ResourceName, string ResourceType, string ProjectCode, string Hall,
    DateTime StartTime, DateTime EndTime, int Quantity, string Status, string? ExceptionStatus, bool Mine);
public sealed record HallBookingView(string ProjectCode, string Hall, string Slot, DateOnly EventDate, string Status, bool Mine);
public sealed record OpResourceCalendarView(IReadOnlyList<ResourceView> Resources, IReadOnlyList<HallBookingView> Bookings, IReadOnlyList<AllocationView> Allocations);
public sealed record ConflictView(string ProjectCode, DateTime StartTime, DateTime EndTime, int Quantity);
public sealed record AllocationCommand(int ResourceId, string ProjectCode, DateTime StartTime, DateTime EndTime, int Quantity);
public sealed record AllocationResult(AllocationView? Allocation, string? Message, ResourceView? Resource, IReadOnlyList<ConflictView> Conflicts);

public interface IResourceRepository
{
    Task<OpResourceCalendarView> GetCalendarAsync(int managerId, DateOnly from, DateOnly to, int? hallId, string? type, CancellationToken cancellationToken = default);
    /// <summary>Saves the allocation, or returns the conflicts (MSG08) without saving. Null when the project is not the Manager's.</summary>
    Task<AllocationResult?> AllocateAsync(int managerId, AllocationCommand command, CancellationToken cancellationToken = default);
    Task<AllocationResult?> UpdateAsync(int managerId, int allocationId, AllocationCommand command, CancellationToken cancellationToken = default);
    Task<bool> ReleaseAsync(int managerId, int allocationId, CancellationToken cancellationToken = default);
    Task<AllocationResult?> GetConflictAsync(int managerId, int allocationId, CancellationToken cancellationToken = default);
    /// <summary>UC-47 from UC-25: keeps the allocation out of the conflict check until the Admin decides.</summary>
    Task<AllocationResult?> RequestExceptionAsync(int managerId, AllocationCommand command, string note, CancellationToken cancellationToken = default);
}

// ── PDF (SDS I.1 QuestPDF) ─────────────────────────────────────────────────
public sealed record PdfTable(string? Heading, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);
public sealed record PdfDocumentModel(string Title, string? Subtitle, IReadOnlyList<(string Label, string Value)> Facts, IReadOnlyList<PdfTable> Tables,
    IReadOnlyList<string> Notes);

public interface IPdfGenerator
{
    byte[] Render(PdfDocumentModel model);
}
