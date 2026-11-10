namespace Domain.Events;

/// <summary>
/// Domain event emitted when a DayOfTask transitions from Pending to Overdue (FE-09, Report 4 SDS).
/// Handled by creating notifications for the assigned Manager and all active Admins, and queueing alert emails.
/// </summary>
public sealed record DayOfTaskOverdueEvent(
    int TaskId,
    int ProjectId,
    string ProjectCode,
    string TaskName,
    DateTime DeadlineTime,
    DateTime OverdueAt,
    int ManagerId,
    string HallName,
    DateOnly EventDate);
