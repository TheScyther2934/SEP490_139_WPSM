using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Application.OverdueAlerts;
using Application.Persistence;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.OverdueAlerts;

/// <summary>
/// Implements JOB-05: Detect Overdue Day-of Tasks according to Report 3 (SRS) &amp; Report 4 (SDS).
/// Periodically evaluates pending Day-of tasks on active event dates, applies fast skip,
/// triggers domain transition Pending -> Overdue, raises DayOfTaskOverdueEvent, creates in-app
/// notifications for Manager &amp; Admins, and queues alert emails for JOB-07 dispatch.
/// </summary>
public sealed class DetectOverdueTasksService(
    WpsmDbContext db,
    INotificationRepository notificationRepository,
    IEmailOutbox emailOutbox,
    EmailOptions emailOptions,
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
        int notificationsCreated = 0;
        int emailsQueued = 0;
        int skipped = 0;
        int failed = 0;
        var errors = new List<string>();

        // Preload active Admin recipients once
        var activeAdmins = await db.Users
            .AsNoTracking()
            .Where(u => u.Role == "Admin" && u.Status == "Active")
            .ToListAsync(cancellationToken);

        var baseUrl = emailOptions.PublicBaseUrl?.TrimEnd('/') ?? "http://localhost:5173";

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

                var project = task.Project;
                var hallName = project.Hall?.Name ?? "Sảnh tiệc";
                var projectCode = project.ProjectCode;

                // 5. In-app notifications & emails
                // A. Assigned Manager
                var manager = project.Manager;
                if (manager is { Status: "Active" })
                {
                    var managerLink = $"/manager/projects/{Uri.EscapeDataString(projectCode)}/day-of?taskId={task.DayOfTaskId}";
                    var managerMsg = $"[{projectCode}] Nhiệm vụ \"{task.TaskName}\" đã quá hạn từ lúc {task.DeadlineTime:HH:mm dd/MM/yyyy} ({hallName}).";

                    var alreadyNotified = await db.Notifications
                        .AsNoTracking()
                        .AnyAsync(n => n.RecipientId == manager.UserId
                                    && n.Type == NotificationTypes.OverdueAlert
                                    && n.LinkUrl != null
                                    && n.LinkUrl.Contains($"taskId={task.DayOfTaskId}"), cancellationToken);

                    if (!alreadyNotified)
                    {
                        await notificationRepository.CreateNotificationAsync(
                            recipientId: manager.UserId,
                            type: NotificationTypes.OverdueAlert,
                            message: managerMsg,
                            linkUrl: managerLink,
                            relatedEntityType: "DayOfTask",
                            relatedEntityId: task.DayOfTaskId.ToString(),
                            cancellationToken: cancellationToken);

                        notificationsCreated++;

                        // Queue email to Manager
                        try
                        {
                            var emailSubject = $"[{projectCode}] Cảnh báo nhiệm vụ quá hạn: {task.TaskName}";
                            var alertUrl = $"{baseUrl}{managerLink}";
                            var emailBody = EmailTemplates.OverdueAlert(
                                recipientName: manager.FullName,
                                role: "Manager",
                                projectCode: projectCode,
                                taskName: task.TaskName,
                                deadline: task.DeadlineTime,
                                hallName: hallName,
                                eventDate: project.EventDate,
                                alertUrl: alertUrl);

                            emailOutbox.Enqueue(
                                toEmail: manager.Email,
                                subject: emailSubject,
                                bodyHtml: emailBody,
                                kind: "OverdueAlert",
                                projectId: project.ProjectId);

                            emailsQueued++;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.LogWarning(ex, "Failed to queue overdue email for Manager {Email} on Task {TaskId}.", manager.Email, task.DayOfTaskId);
                        }
                    }
                }

                // B. All active Admins (A-15 & UC-45)
                foreach (var admin in activeAdmins)
                {
                    var adminLink = $"/admin/photo-feed?projectCode={Uri.EscapeDataString(projectCode)}";
                    var adminMsg = $"[{projectCode}] Nhiệm vụ \"{task.TaskName}\" đã quá hạn từ lúc {task.DeadlineTime:HH:mm dd/MM/yyyy} ({hallName}).";

                    var alreadyNotified = await db.Notifications
                        .AsNoTracking()
                        .AnyAsync(n => n.RecipientId == admin.UserId
                                    && n.Type == NotificationTypes.OverdueAlert
                                    && n.Message.Contains(projectCode)
                                    && n.Message.Contains(task.TaskName), cancellationToken);

                    if (!alreadyNotified)
                    {
                        await notificationRepository.CreateNotificationAsync(
                            recipientId: admin.UserId,
                            type: NotificationTypes.OverdueAlert,
                            message: adminMsg,
                            linkUrl: adminLink,
                            relatedEntityType: "DayOfTask",
                            relatedEntityId: task.DayOfTaskId.ToString(),
                            cancellationToken: cancellationToken);

                        notificationsCreated++;

                        // Queue email to Admin
                        try
                        {
                            var emailSubject = $"[{projectCode}] Cảnh báo nhiệm vụ quá hạn: {task.TaskName}";
                            var alertUrl = $"{baseUrl}/admin/overdue-alerts";
                            var emailBody = EmailTemplates.OverdueAlert(
                                recipientName: admin.FullName,
                                role: "Admin",
                                projectCode: projectCode,
                                taskName: task.TaskName,
                                deadline: task.DeadlineTime,
                                hallName: hallName,
                                eventDate: project.EventDate,
                                alertUrl: alertUrl);

                            emailOutbox.Enqueue(
                                toEmail: admin.Email,
                                subject: emailSubject,
                                bodyHtml: emailBody,
                                kind: "OverdueAlert",
                                projectId: project.ProjectId);

                            emailsQueued++;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.LogWarning(ex, "Failed to queue overdue email for Admin {Email} on Task {TaskId}.", admin.Email, task.DayOfTaskId);
                        }
                    }
                }

                // 6. Persist status transition to Overdue
                await db.SaveChangesAsync(cancellationToken);
                task.ClearDomainEvents();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                var errorMsg = $"Task {task.DayOfTaskId} error: {ex.Message}";
                errors.Add(errorMsg);
                logger.LogError(ex, "Error processing overdue transition for Task {TaskId}.", task.DayOfTaskId);
            }
        }

        var completedAt = DateTime.UtcNow;

        logger.LogInformation(
            "JOB-05 [ExecutionId: {ExecutionId}] Finished check. HasEventToday: {HasEventToday}, Checked: {Checked}, OverdueDetected: {Overdue}, Alerts: {Alerts}, Notifications: {Notifications}, Emails: {Emails}, Skipped: {Skipped}, Failed: {Failed}.",
            executionId, hasEventToday, totalChecked, overdueDetected, alertsCreated, notificationsCreated, emailsQueued, skipped, failed);

        return new DetectOverdueTasksResult(
            ExecutionId: executionId,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            HasEventToday: hasEventToday,
            TotalTasksChecked: totalChecked,
            TotalOverdueDetected: overdueDetected,
            TotalAlertsCreated: alertsCreated,
            TotalNotificationsCreated: notificationsCreated,
            TotalEmailsQueued: emailsQueued,
            TotalSkipped: skipped,
            TotalFailed: failed,
            Errors: errors);
    }
}
