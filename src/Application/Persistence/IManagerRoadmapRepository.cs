using Domain.Enums;

namespace Application.Persistence;

// ── M-06 Manager Master Roadmap & Tasks (FE-03, UC-21) ───────────────────────

public sealed record ManagerRoadmapView(
    string ProjectCode,
    string CustomerName,
    string Hall,
    string Slot,
    DateOnly EventDate,
    int CountdownDays,
    IReadOnlyList<ManagerRoadmapMilestoneGroup> MilestoneGroups,
    IReadOnlyList<ManagerRoadmapTaskView> Tasks,
    int TotalTasks,
    int DoneTasks,
    int OverdueTasks,
    int ManagerTasksCount,
    int CustomerTasksCount,
    int ProgressPercent);

public sealed record ManagerRoadmapMilestoneGroup(
    string Milestone,
    DateOnly MilestoneDate,
    IReadOnlyList<ManagerRoadmapTaskView> Tasks,
    int TotalCount,
    int DoneCount);

public sealed record ManagerRoadmapTaskView(
    int TaskId,
    string Milestone,
    string Title,
    string AssigneeRole,    // "Manager" | "Customer"
    DateOnly DueDate,
    string Status,          // "Pending" | "Done" | "Overdue"
    DateTime? CompletedAt,
    bool RequiresProof = false);

public sealed record CompleteManagerTaskResult(
    bool Success,
    string Message,
    ManagerRoadmapTaskView? UpdatedTask);

/// <summary>
/// Persistence contract for Manager Master Roadmap &amp; Tasks (M-06 / UC-21).
/// All operations verify manager authorization server-side.
/// </summary>
public interface IManagerRoadmapRepository
{
    /// <summary>
    /// Checks whether the manager (or admin) has rights to manage the specified project.
    /// </summary>
    Task<bool> CanManageProjectAsync(
        int managerId,
        string role,
        string projectIdentifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the roadmap with milestone groups and summary metadata for an authorized project.
    /// Preserves all 4 canonical milestones (T-90, T-30, T-7, Event Day) regardless of filter.
    /// Returns <c>null</c> if project does not exist or user is not authorized.
    /// </summary>
    Task<ManagerRoadmapView?> GetRoadmapAsync(
        int managerId,
        string role,
        string projectIdentifier,
        TaskOwnerFilter filter = TaskOwnerFilter.All,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the filtered flat list of tasks for an authorized project, sorted by DueDate.
    /// Returns <c>null</c> if project does not exist or user is not authorized.
    /// </summary>
    Task<IReadOnlyList<ManagerRoadmapTaskView>?> GetRoadmapTasksAsync(
        int managerId,
        string role,
        string projectIdentifier,
        TaskOwnerFilter filter = TaskOwnerFilter.All,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a Manager-owned task as Done (UC-21).
    /// Enforces:
    ///   - Project authorization
    ///   - Task must belong to the project
    ///   - Task must belong to Manager (cannot complete Customer tasks)
    ///   - Server-side timestamp (client timestamp not accepted)
    ///   - Idempotent if already Done
    ///   - Proof-of-Execution verification if task requires proof under FE-08
    /// </summary>
    Task<CompleteManagerTaskResult> CompleteTaskAsync(
        int managerId,
        string role,
        string projectIdentifier,
        int taskId,
        IReadOnlyList<string>? proofPhotoPaths = null,
        CancellationToken cancellationToken = default);
}
