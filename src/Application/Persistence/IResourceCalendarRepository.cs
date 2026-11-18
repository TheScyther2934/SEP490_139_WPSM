namespace Application.Persistence;

// ── FE-07 Resource Calendar (M-21, M-22) ─────────────────────────────────────
// The calendar is built from Wedding Projects (hall + slot + date) and Resource Allocations.
// GBR-09 is enforced here: a resource cannot be allocated beyond Resource.TotalQuantity in
// overlapping time ranges unless an Admin approves a special exception (UC-47).

public sealed record ResourceTypeOptionView(string Type, string Label, int ResourceCount, int TotalQuantity);

public sealed record ResourceOptionView(int ResourceId, string Name, string Type, string TypeLabel, int TotalQuantity);

public sealed record CalendarSlotView(int SlotId, string SlotName, TimeOnly StartTime, TimeOnly EndTime);

public sealed record CalendarHallView(int HallId, string Name, int MinCapacity, int MaxCapacity, IReadOnlyList<CalendarSlotView> Slots);

/// <summary>An active project the Manager may allocate resources for (dialog project picker).</summary>
public sealed record CalendarProjectView(int ProjectId, string Code, string CoupleName, int HallId, string HallName, int SlotId, string SlotName, DateOnly EventDate);

public sealed record CalendarBookingView(int ProjectId, string Code, string CoupleName, string HallName, int HallId, int SlotId, DateOnly EventDate, string Status);

public sealed record AllocationBriefView(
    int AllocationId,
    int ProjectId,
    string ProjectCode,
    string CoupleName,
    string HallName,
    DateTime StartTime,
    DateTime EndTime,
    int Quantity,
    string Status,
    bool HasPendingException);

/// <summary>One resource on one calendar day: how much is committed and whether it is over capacity.</summary>
public sealed record ResourceUsageView(
    int ResourceId,
    DateOnly Date,
    int ReservedQuantity,
    int ApprovedQuantity,
    int PendingQuantity,
    bool HasConflict,
    IReadOnlyList<AllocationBriefView> Allocations)
{
    public int CommittedQuantity => ReservedQuantity + ApprovedQuantity;
}

public sealed record ResourceLaneView(
    int ResourceId,
    string Name,
    string Type,
    string TypeLabel,
    int TotalQuantity,
    IReadOnlyList<ResourceUsageView> Usages);

public sealed record CalendarSummaryView(
    int Events,
    int Allocations,
    int Conflicts,
    int PendingExceptions,
    int BookedSlots,
    int AvailableSlots,
    decimal HallUtilization);

public sealed record ResourceCalendarView(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<CalendarHallView> Halls,
    IReadOnlyList<CalendarProjectView> Projects,
    IReadOnlyList<CalendarBookingView> Bookings,
    IReadOnlyList<ResourceOptionView> Resources,
    IReadOnlyList<ResourceTypeOptionView> Types,
    IReadOnlyList<ResourceLaneView> Lanes,
    CalendarSummaryView Summary);

/// <summary>Save command for M-21. Times are UTC (the client sends ISO strings).</summary>
public sealed record ResourceAllocationCommand(
    int ResourceId,
    int ProjectId,
    DateTime StartTime,
    DateTime EndTime,
    int Quantity,
    bool RequestException = false,
    string? ExceptionNote = null);

public sealed record ResourceConflictView(
    int ResourceId,
    string ResourceName,
    string ResourceTypeLabel,
    int TotalQuantity,
    int RequestedQuantity,
    int OtherQuantity,
    int Shortage,
    IReadOnlyList<AllocationBriefView> Clashing);

public sealed record ResourceAllocationDetailView(
    int AllocationId,
    int ResourceId,
    string ResourceName,
    string ResourceType,
    string ResourceTypeLabel,
    int TotalQuantity,
    int ProjectId,
    string ProjectCode,
    string CoupleName,
    int HallId,
    string HallName,
    DateOnly EventDate,
    DateTime StartTime,
    DateTime EndTime,
    int Quantity,
    string Status,
    bool IsExceptionApproved,
    DateTime? ExceptionApprovedAt,
    string? ExceptionNote,
    string? PendingRequestCode,
    string? PendingRequestNote,
    int OtherQuantity,
    int Shortage,
    bool HasConflict,
    bool CanRequestException,
    IReadOnlyList<AllocationBriefView> Clashing);

/// <summary>A save that hit GBR-09 returns the conflict so M-21 can open the M-22 breakdown.</summary>
public sealed record SaveAllocationResult(ResourceAllocationDetailView? Allocation, ResourceConflictView? Conflict);

/// <summary>
/// M-21/M-22 resource scheduling for the Manager's own projects (GBR-12). Methods return
/// <c>null</c> when the project/allocation is outside the caller's scope; rule violations throw
/// <see cref="InvalidOperationException"/> (HTTP 400 + SRS message).
/// </summary>
public interface IResourceCalendarRepository
{
    Task<ResourceCalendarView?> GetCalendarAsync(DateOnly from, DateOnly to, int? hallId, int managerId, CancellationToken cancellationToken = default);

    Task<ResourceAllocationDetailView?> GetAllocationAsync(int allocationId, int managerId, CancellationToken cancellationToken = default);

    /// <summary>Resource picker for the M-21 dialog and the M-22 substitute action.</summary>
    Task<IReadOnlyList<ResourceOptionView>> GetResourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates (<paramref name="allocationId"/> null) or reschedules/substitutes a Reserved allocation.</summary>
    Task<SaveAllocationResult?> SaveAsync(int? allocationId, ResourceAllocationCommand command, int managerId, CancellationToken cancellationToken = default);

    Task<ResourceAllocationDetailView?> ReleaseAsync(int allocationId, int managerId, CancellationToken cancellationToken = default);

    /// <summary>UC-47: asks the Admin to allow an over-capacity allocation (A-18).</summary>
    Task<ResourceAllocationDetailView?> RequestExceptionAsync(int allocationId, string note, int managerId, CancellationToken cancellationToken = default);
}
