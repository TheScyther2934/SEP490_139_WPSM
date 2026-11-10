using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.OverdueAlerts;

public sealed record DetectOverdueTasksCommand(
    DateOnly? TargetDate = null,
    DateTime? CurrentUtc = null);

public sealed record DetectOverdueTasksResult(
    Guid ExecutionId,
    DateTime StartedAt,
    DateTime CompletedAt,
    bool HasEventToday,
    int TotalTasksChecked,
    int TotalOverdueDetected,
    int TotalAlertsCreated,
    int TotalNotificationsCreated,
    int TotalEmailsQueued,
    int TotalSkipped,
    int TotalFailed,
    IReadOnlyList<string> Errors);

/// <summary>
/// JOB-05 service contract (FE-09, Report 3 SRS &amp; Report 4 SDS).
/// Detects overdue day-of tasks for today's wedding events every 1 minute.
/// </summary>
public interface IDetectOverdueTasksService
{
    Task<DetectOverdueTasksResult> DetectOverdueTasksAsync(
        DetectOverdueTasksCommand? command = null,
        CancellationToken cancellationToken = default);
}
