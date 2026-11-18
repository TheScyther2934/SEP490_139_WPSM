namespace Application.Persistence;

// Contracts for the use cases added from Report 4 (SDS II, IV.2): notifications, Manager work, change requests,
// BEO, day-of, payments, feedback and resources. Project access (GBR-12) is checked by the /projects/{id} filter.

// ── G-04 Notifications (UC-03) ──────────────────────────────────────────────
public sealed record OpNotificationView(int Id, string Type, string Message, string? LinkUrl, bool IsRead, DateTime CreatedAt);

public interface IOperationsNotificationRepository
{
    Task<IReadOnlyList<OpNotificationView>> GetAsync(int userId, bool unreadOnly, CancellationToken cancellationToken = default);
    Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);
}

// ── M-01, M-06, M-23 Manager work (UC-21, UC-32) ───────────────────────────
public sealed record ManagerTaskView(int TaskId, string ProjectCode, string CoupleName, string Milestone, string Title, string AssigneeRole,
    DateOnly DueDate, string Status, DateTime? CompletedAt);
public sealed record OverdueTaskView(int TaskId, string ProjectCode, string CoupleName, string TaskName, DateTime Deadline, int OverdueMinutes, string Status);
public sealed record StaleBeoView(string ProjectCode, string CoupleName, int Version, DateOnly EventDate);
public sealed record ManagerWorkView(int ActiveProjects, int TasksDueToday, int OpenChangeRequests, int StaleBeoCount,
    IReadOnlyList<ManagerTaskView> MyTasks, IReadOnlyList<OverdueTaskView> Overdue, IReadOnlyList<StaleBeoView> StaleBeos);

public interface IManagerWorkRepository
{
    Task<ManagerWorkView> GetWorkAsync(int managerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManagerTaskView>> GetRoadmapAsync(string projectCode, CancellationToken cancellationToken = default);
    /// <summary>UC-21: only Manager-owned tasks; returns false when the task is not in the project.</summary>
    Task<bool> CompleteTaskAsync(string projectCode, int taskId, int managerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OverdueTaskView>> GetOverdueAsync(int managerId, CancellationToken cancellationToken = default);
}

// ── Change Requests (UC-07..UC-11, UC-22..UC-24, SDS II.2) ─────────────────
public sealed record CrSpecItemView(int SpecItemId, string Name, string Category, string Unit, string Description, int Quantity,
    decimal UnitPrice, decimal? MinPrice, decimal? MaxPrice);
public sealed record CrItemCommand(int SpecItemId, int NewQuantity, decimal NewUnitPrice);
public sealed record CreateCrCommand(string Type, string Reason, IReadOnlyList<CrItemCommand> Items, bool Submit);
public sealed record OpCrCostView(decimal PriceDelta, int TDay, decimal PenaltyRate, decimal PenaltyFee, bool PenaltyWaived,
    decimal TotalImpact, decimal CurrentTotal, decimal NewTotal);
public sealed record OpCrItemView(int SpecItemId, string Name, string Unit, int OldQuantity, decimal OldUnitPrice, int NewQuantity, decimal NewUnitPrice, decimal Delta);
public sealed record OpCrHistoryView(string Actor, string ActorRole, string Action, string? Note, DateTime CreatedAt);
public sealed record OpCrSummaryView(string Code, string Type, string Reason, string CreatedByRole, string Status, decimal TotalImpact, DateTime CreatedAt);
public sealed record CrExceptionView(string Code, string Status, string Note, string? DecisionNote);
public sealed record OpCrDetailView(string Code, string ProjectCode, string Type, string Reason, string CreatedByRole, string CreatedByName, string Status,
    DateTime CreatedAt, DateTime? CustomerApprovedAt, DateTime? ManagerApprovedAt, IReadOnlyList<OpCrItemView> Items, OpCrCostView Cost,
    IReadOnlyList<OpCrHistoryView> History, IReadOnlyList<CrExceptionView> Exceptions, string? AddendumCode, string? BeoEffect);
public sealed record OpCrAddendumView(string AddendumCode, string CrCode, string Status, decimal TotalVariance, DateTime? SignedAt,
    Guid? PdfFileId, Guid? SignedFileId, decimal BaseValue, decimal CurrentTotal, decimal TotalPaid, decimal Outstanding, string? BeoEffect,
    IReadOnlyList<OpCrItemView> Items, OpCrCostView Cost);

public interface IOperationsChangeRequestRepository
{
    Task<IReadOnlyList<OpCrSummaryView>> ListAsync(string projectCode, string? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrSpecItemView>> GetSpecItemsAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<OpCrCostView> PreviewAsync(string projectCode, IReadOnlyList<CrItemCommand> items, CancellationToken cancellationToken = default);
    Task<OpCrDetailView> CreateAsync(string projectCode, int userId, string role, CreateCrCommand command, CancellationToken cancellationToken = default);
    Task<OpCrDetailView?> GetAsync(string projectCode, string crCode, CancellationToken cancellationToken = default);
    Task<OpCrDetailView?> SubmitAsync(string projectCode, string crCode, int userId, string role, CancellationToken cancellationToken = default);
    Task<OpCrDetailView?> CounterProposeAsync(string projectCode, string crCode, int userId, string role, IReadOnlyList<CrItemCommand> items, string? note, CancellationToken cancellationToken = default);
    Task<OpCrDetailView?> ApproveAsync(string projectCode, string crCode, int userId, string role, CancellationToken cancellationToken = default);
    Task<OpCrDetailView?> RejectAsync(string projectCode, string crCode, int userId, string role, string? reason, CancellationToken cancellationToken = default);
    Task<OpCrDetailView?> CommentAsync(string projectCode, string crCode, int userId, string role, string note, CancellationToken cancellationToken = default);
    Task<OpCrDetailView?> RequestExceptionAsync(string projectCode, string crCode, int managerId, string note, CancellationToken cancellationToken = default);
    Task<OpCrAddendumView?> GetAddendumAsync(string projectCode, string crCode, CancellationToken cancellationToken = default);
    Task<OpCrAddendumView?> AttachSignedAsync(string projectCode, string crCode, int managerId, byte[] content, string fileName, CancellationToken cancellationToken = default);
}

// ── BEO (UC-27..UC-29, SDS IV.4.2) ─────────────────────────────────────────
public sealed record BeoLineView(string Category, string Item, string Description, int Quantity, string Unit);
public sealed record BeoTimelineView(string Time, string Activity, string Department);
public sealed record BeoDepartmentView(string Department, IReadOnlyList<BeoLineView> Lines);
public sealed record BeoSheetView(string Department, DateTime GeneratedAt, Guid? FileId);
public sealed record BeoVersionView(int Version, string Status, DateTime? FrozenAt, string? ApprovedBy);
public sealed record BeoChangeView(string AddendumCode, string CrCode, decimal TotalVariance, DateTime ApprovedAt, IReadOnlyList<OpCrItemView> Items);
public sealed record BeoView(string ProjectCode, string CoupleName, DateOnly EventDate, int TDay, int? Version, string Status, DateTime? FrozenAt,
    string? ApprovedBy, string? Notes, bool CanFreeze, string? FreezeBlockReason, IReadOnlyList<string> PendingCrCodes,
    IReadOnlyList<BeoTimelineView> Timeline, IReadOnlyList<BeoLineView> Menu, IReadOnlyList<BeoDepartmentView> Departments,
    IReadOnlyList<string> AddendumCodes, IReadOnlyList<BeoSheetView> Sheets, IReadOnlyList<BeoVersionView> Versions,
    IReadOnlyList<BeoChangeView> ChangesSinceFreeze);

public interface IOperationsBeoRepository
{
    Task<BeoView?> GetAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<BeoView?> SaveNotesAsync(string projectCode, string? notes, CancellationToken cancellationToken = default);
    /// <summary>UC-27/UC-28: T-7 reached, no pending CR (GBR-05); stores four department PDFs.</summary>
    Task<BeoView?> FreezeAsync(string projectCode, int managerId, CancellationToken cancellationToken = default);
    /// <summary>UC-29: a Stale BEO becomes a new Frozen version; the old version stays for audit.</summary>
    Task<BeoView?> ReapproveAsync(string projectCode, int managerId, CancellationToken cancellationToken = default);
    /// <summary>JOB-03: creates the Draft for projects at T-7.</summary>
    Task<int> CreateDueDraftsAsync(DateOnly today, CancellationToken cancellationToken = default);
}

// ── Day-of (UC-30..UC-32, SDS IV.4.3) ──────────────────────────────────────
public sealed record DayOfPhotoView(int PhotoId, DateTime UploadedAt)
{
    public string ImageUrl { get; init; } = "";
}
public sealed record DayOfTaskView(int TaskId, string TaskName, DateTime Deadline, string Status, bool PhotoRequired, DateTime? CompletedAt,
    bool IsLate, IReadOnlyList<DayOfPhotoView> Photos);
public sealed record DayOfBoardView(string ProjectCode, string CoupleName, DateOnly EventDate, string SlotTime, string BeoStatus, IReadOnlyList<DayOfTaskView> Tasks);
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
    /// <summary>JOB-04: loads the timeline of every frozen project whose event is today.</summary>
    Task<int> LoadTodayAsync(DateOnly today, CancellationToken cancellationToken = default);
    /// <summary>JOB-05: flags Pending tasks past their deadline (GBR-07) and notifies the Manager (MSG21).</summary>
    Task<int> DetectOverdueAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

// ── Payments and settlement (UC-12, UC-33, UC-34, SDS IV.4.4) ──────────────
public sealed record PaymentLineView(int PaymentId, string Type, string Status, decimal? Amount, DateOnly? DueDate, DateOnly? PaidDate,
    string? Method, bool HasReceipt, Guid? ReceiptFileId);
public sealed record PaymentsView(string ProjectCode, string CoupleName, string ProjectStatus, DateOnly EventDate, decimal BaseValue,
    decimal ApprovedCrVariance, decimal CurrentTotal, decimal TotalPaid, decimal Outstanding, IReadOnlyList<PaymentLineView> Payments,
    bool CanRecordFinal, Guid? SettlementReceiptFileId, DateTime? SettledAt);
public sealed record OpRecordPaymentCommand(string Type, decimal Amount, string Method, DateOnly PaidDate, byte[]? Receipt, string? ReceiptName);

public interface IPaymentRepository
{
    Task<PaymentsView?> GetAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<PaymentsView?> RecordAsync(string projectCode, int managerId, OpRecordPaymentCommand command, CancellationToken cancellationToken = default);
    Task<PaymentsView?> IssueSettlementReceiptAsync(string projectCode, int managerId, CancellationToken cancellationToken = default);
    /// <summary>JOB-02: T-30 payment becomes Due with amount = round(Current Total × T30 ratio) − NRD.</summary>
    Task<int> IssueT30PaymentsAsync(DateOnly today, CancellationToken cancellationToken = default);
    /// <summary>JOB-06: events held yesterday become Event Completed; FINAL becomes Due.</summary>
    Task<int> PrepareFinalSettlementsAsync(DateOnly today, CancellationToken cancellationToken = default);
}

// ── Ratings and complaints (UC-14..UC-16, UC-35, UC-36) ────────────────────
public sealed record RatingView(string Module, int Stars, string? Comment, DateTime CreatedAt);
public sealed record RatingCommand(string Module, int Stars, string? Comment);
public sealed record PostEventView(string ProjectCode, string ProjectStatus, bool CanRate, bool CanComplain, IReadOnlyList<RatingView> Ratings,
    IReadOnlyList<int> FrozenBeoVersions);
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
