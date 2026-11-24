namespace Application.Persistence;

// Contracts for BEO (event coordination sheet), day-of, feedback and resources.
// Payments: IManagerPaymentRepository / ICustomerPaymentRepository; notifications: INotificationRepository.
// Project access (GBR-12) is checked by the /projects/{id} filter.

// ── Coordination tasks (UC-30..UC-32, SDS IV.4.3) ──────────────────────────
// Every coordination task of an event by department: preparation tasks between the lock and the event day, the event-day
// timeline and tasks added by the operator. Proof is a photo (JPG/PNG) or a document (PDF); customers only see photos.
public sealed record DayOfPhotoView(int PhotoId, DateTime UploadedAt)
{
    public string ImageUrl { get; init; } = "";
    public string? UploaderName { get; init; }
    public string? FileName { get; init; }
    /// <summary>False for a PDF proof (opened in a new tab instead of the photo viewer).</summary>
    public bool IsImage { get; init; } = true;
}
/// <summary>MissingProof: the task is done but has no proof although it requires one (proof can still be added).</summary>
public sealed record DayOfTaskView(int TaskId, string TaskName, DateTime Deadline, string Status, bool PhotoRequired, DateTime? CompletedAt,
    bool IsLate, IReadOnlyList<DayOfPhotoView> Photos)
{
    public string? CompletedBy { get; init; }
    public bool MissingProof => Status == "Done" && PhotoRequired && Photos.Count == 0;
    public string Department { get; init; } = "Service";
    public string? Note { get; init; }
    /// <summary>Added by an operator (can be deleted while not done); otherwise generated from the services.</summary>
    public bool IsCustom { get; init; }
    /// <summary>Deadline on the event day (the event-day board shows these).</summary>
    public bool IsEventDay { get; init; }
}
/// <summary>CanAddPhotos: tasks can be completed and proof added from the lock until the event is settled.</summary>
public sealed record DayOfBoardView(string ProjectCode, string CoupleName, DateOnly EventDate, string SlotTime, string BeoStatus, IReadOnlyList<DayOfTaskView> Tasks)
{
    public string ProjectStatus { get; init; } = "";
    public bool CanAddPhotos { get; init; }
    public int MaxPhotosPerTask { get; init; }
    public DateTime? LockedAt { get; init; }
}
public sealed record ProofPhotoUpload(byte[] Content, string FileName);
/// <summary>Task added or edited by the operator. Deadline in UTC; Department: Kitchen, Service, Decor or Tech-MC.</summary>
public sealed record CoordinationTaskCommand(string TaskName, string Department, DateTime Deadline, bool PhotoRequired, string? Note);
public sealed record ProjectPhotoView(int PhotoId, string TaskName, DateTime UploadedAt, string UploaderName)
{
    public string ImageUrl { get; init; } = "";
}

public interface IDayOfRepository
{
    Task<DayOfBoardView?> GetBoardAsync(string projectCode, CancellationToken cancellationToken = default);
    /// <summary>UC-13 / C-11: every proof photo of the project (photos only, no documents), newest first.</summary>
    Task<IReadOnlyList<ProjectPhotoView>> GetPhotosAsync(string projectCode, CancellationToken cancellationToken = default);
    // Changes (generate, add, edit, delete, complete, add proof) are use cases: Application/UseCases/CoordinationTasks.
    // JOB-04 and JOB-05 run through ILoadDayOfTimelineService and IDetectOverdueTasksService.
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
    Task<AllocationResult?> GetConflictAsync(int managerId, int allocationId, CancellationToken cancellationToken = default);
    /// <summary>One allocation of the caller's events; null otherwise. Writes go through Application/UseCases/Resources.</summary>
    Task<AllocationView?> GetAllocationAsync(int managerId, int allocationId, CancellationToken cancellationToken = default);
}

// ── PDF (SDS I.1 QuestPDF) ─────────────────────────────────────────────────
public sealed record PdfTable(string? Heading, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);
public sealed record PdfDocumentModel(string Title, string? Subtitle, IReadOnlyList<(string Label, string Value)> Facts, IReadOnlyList<PdfTable> Tables,
    IReadOnlyList<string> Notes);

public interface IPdfGenerator
{
    byte[] Render(PdfDocumentModel model);
}
