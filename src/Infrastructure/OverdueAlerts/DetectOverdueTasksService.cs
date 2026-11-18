using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Events;
using Application.OverdueAlerts;
using Domain.Events;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.OverdueAlerts;

/// <summary>
/// Implements JOB-05: Detect Overdue Day-of Tasks according to Report 3 (SRS) &amp; Report 4 (SDS).
/// Periodically evaluates pending Day-of tasks on active event dates, applies fast skip,
/// triggers domain transition Pending -> Overdue, raises DayOfTaskOverdueEvent, persists changes,
/// and dispatches domain events to notify Manager and Admins.
/// </summary>
public sealed class DetectOverdueTasksService(
    WpsmDbContext db,
    IDomainEventDispatcher domainEventDispatcher,
    ILogger<DetectOverdueTasksService> logger) : IDetectOverdueTasksService
{
    private static readonly HashSet<string> InactiveProjectStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Closed", "Settled", "Cancelled"
    };

    public async Task<DetectOverdueTasksResult> DetectOverdueTasksAsync(
        DetectOverdueTasksCommand? command = null,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;
        var today = command?.TargetDate ?? RoadmapRules.GetVietnamToday();
        var nowUtc = command?.CurrentUtc ?? DateTime.UtcNow;

        logger.LogInformation(
            "JOB-05 [ExecutionId: {ExecutionId}] Starting overdue day-of tasks check for date {Today:yyyy-MM-dd} at {NowUtc:O}.",
            executionId, today, nowUtc);

        // 1. Fast skip optimization: check if any active Wedding Project has EventDate == Today
        var hasEventToday = await db.WeddingProjects
            .AsNoTracking()
            .AnyAsync(p => p.EventDate == today && !InactiveProjectStatuses.Contains(p.Status), cancellationToken);

        if (!hasEventToday)
        {
            logger.LogInformation(
                "JOB-05 [ExecutionId: {ExecutionId}] Fast skip: No active wedding events found for today ({Today:yyyy-MM-dd}).",
                executionId, today);

            return new DetectOverdueTasksResult(
                ExecutionId: executionId,
                StartedAt: startedAt,
                CompletedAt: DateTime.UtcNow,
                HasEventToday: false,
                TotalTasksChecked: 0,
                TotalOverdueDetected: 0,
                TotalAlertsCreated: 0,
                TotalNotificationsCreated: 0,
                TotalEmailsQueued: 0,
                TotalSkipped: 0,
                TotalFailed: 0,
                Errors: Array.Empty<string>());
        }

        // 2. Query pending day-of tasks for today's active wedding projects that exceeded deadline
        var candidateTasks = await db.DayOfTasks
            .Include(t => t.Project)
                .ThenInclude(p => p.Manager)
            .Include(t => t.Project)
                .ThenInclude(p => p.Hall)
            .Where(t => t.Project.EventDate == today
                     && t.Status == "Pending"
                     && t.CompletedAt == null
                     && t.DeadlineTime < nowUtc
                     && !InactiveProjectStatuses.Contains(t.Project.Status))
            .OrderBy(t => t.DeadlineTime)
            .ThenBy(t => t.DayOfTaskId)
            .ToListAsync(cancellationToken);

        int totalChecked = candidateTasks.Count;
        int overdueDetected = 0;
        int alertsCreated = 0;
        int skipped = 0;
        int failed = 0;
        var errors = new List<string>();
        var eventsToDispatch = new List<DayOfTaskOverdueEvent>();

        foreach (var task in candidateTasks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 3. State re-verification (idempotency & concurrent worker safety)
                if (!string.Equals(task.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                    task.CompletedAt != null)
                {
                    skipped++;
                    continue;
                }

                // 4. Domain transition via MarkOverdue(nowUtc)
                var transitioned = task.MarkOverdue(nowUtc);
                if (!transitioned)
                {
                    skipped++;
                    continue;
                }

                overdueDetected++;
                alertsCreated++;

                eventsToDispatch.AddRange(task.DomainEvents);
                task.ClearDomainEvents();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                var errorMsg = $"Task {task.DayOfTaskId} transition error: {ex.Message}";
                errors.Add(errorMsg);
                logger.LogError(ex, "Error processing overdue transition for Task {TaskId}.", task.DayOfTaskId);
            }
        }

        // 5. Persist status transitions to database
        if (overdueDetected > 0)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "JOB-05 Error saving overdue transitions to database.");
                errors.Add($"SaveChanges error: {ex.Message}");
            }
        }

        // 6. Dispatch domain events (MSG21 notifications & alert emails)
        int notificationsDispatched = 0;
        foreach (var evt in eventsToDispatch)
        {
            try
            {
                await domainEventDispatcher.DispatchAsync(evt, cancellationToken);
                notificationsDispatched++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "JOB-05 Error dispatching overdue event for Task {TaskId}.", evt.TaskId);
                errors.Add($"Dispatch error for Task {evt.TaskId}: {ex.Message}");
            }
        }

        var completedAt = DateTime.UtcNow;

        logger.LogInformation(
            "JOB-05 [ExecutionId: {ExecutionId}] Finished check. HasEventToday: {HasEventToday}, Checked: {Checked}, OverdueDetected: {Overdue}, Alerts: {Alerts}, Dispatched: {Dispatched}, Skipped: {Skipped}, Failed: {Failed}.",
            executionId, hasEventToday, totalChecked, overdueDetected, alertsCreated, notificationsDispatched, skipped, failed);

        return new DetectOverdueTasksResult(
            ExecutionId: executionId,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            HasEventToday: hasEventToday,
            TotalTasksChecked: totalChecked,
            TotalOverdueDetected: overdueDetected,
            TotalAlertsCreated: alertsCreated,
            TotalNotificationsCreated: notificationsDispatched,
            TotalEmailsQueued: notificationsDispatched,
            TotalSkipped: skipped,
            TotalFailed: failed,
            Errors: errors);
    }
}
