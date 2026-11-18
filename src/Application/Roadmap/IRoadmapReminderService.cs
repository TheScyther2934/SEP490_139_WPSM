namespace Application.Roadmap;

/// <summary>
/// Command to trigger JOB-01: Send Roadmap Milestone Reminders.
/// Allows specifying an optional TargetDate for testing or on-demand execution.
/// </summary>
public sealed record SendRoadmapMilestoneRemindersCommand(DateOnly? TargetDate = null);

/// <summary>
/// Execution metrics and audit log for JOB-01.
/// </summary>
public sealed record RoadmapMilestoneReminderResult(
    Guid ExecutionId,
    DateTime StartedAt,
    DateTime CompletedAt,
    int TotalTasksFound,
    int TotalRemindersCreated,
    int TotalEmailsQueued,
    int TotalSkipped,
    int TotalFailed,
    IReadOnlyList<string> Errors);

/// <summary>
/// Service responsible for identifying upcoming roadmap milestone tasks,
/// verifying ownership, creating notifications, and queuing outbox emails.
/// </summary>
public interface IRoadmapReminderService
{
    Task<RoadmapMilestoneReminderResult> SendMilestoneRemindersAsync(
        SendRoadmapMilestoneRemindersCommand? command = null,
        CancellationToken cancellationToken = default);
}
