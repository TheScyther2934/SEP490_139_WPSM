using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Application.Persistence;
using Application.Roadmap;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Roadmap;

/// <summary>
/// Implements JOB-01: Send Roadmap Milestone Reminders according to Report 3 (SRS) & Report 4 (SDS).
/// Queries upcoming pending roadmap tasks at milestones (T-90, T-30, T-7, Event Day)
/// within [today, today + 3 days], verifies owner, prevents duplicate reminders, creates in-app notifications,
/// and queues emails for JOB-07 dispatch.
/// </summary>
public sealed class RoadmapReminderService(
    WpsmDbContext db,
    INotificationRepository notificationRepository,
    IEmailOutbox emailOutbox,
    EmailOptions emailOptions,
    ILogger<RoadmapReminderService> logger) : IRoadmapReminderService
{
    private static readonly HashSet<string> InactiveProjectStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Closed", "Settled", "Event Completed", "Cancelled"
    };

    public async Task<RoadmapMilestoneReminderResult> SendMilestoneRemindersAsync(
        SendRoadmapMilestoneRemindersCommand? command = null,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;
        var today = command?.TargetDate ?? RoadmapRules.GetVietnamToday();
        var maxDueDate = today.AddDays(3);

        logger.LogInformation(
            "JOB-01 [ExecutionId: {ExecutionId}] Starting reminder check for window {Today:yyyy-MM-dd} to {MaxDueDate:yyyy-MM-dd}.",
            executionId, today, maxDueDate);

        var validMilestones = RoadmapRules.MilestoneOrder;

        // Query eligible pending tasks nearing due date from active projects
        var candidateTasks = await db.RoadmapTasks
            .Include(t => t.Project)
                .ThenInclude(p => p.Customer)
            .Include(t => t.Project)
                .ThenInclude(p => p.Manager)
            .Where(t => t.Status == "Pending"
                     && t.CompletedAt == null
                     && t.RemindedAt == null
                     && t.DueDate >= today
                     && t.DueDate <= maxDueDate
                     && validMilestones.Contains(t.Milestone)
                     && !InactiveProjectStatuses.Contains(t.Project.Status))
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.TaskId)
            .ToListAsync(cancellationToken);

        int totalFound = candidateTasks.Count;
        int remindersCreated = 0;
        int emailsQueued = 0;
        int skipped = 0;
        int failed = 0;
        var errors = new List<string>();

        var baseUrl = emailOptions.PublicBaseUrl?.TrimEnd('/') ?? "http://localhost:5173";

        foreach (var task in candidateTasks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 1. Task state validation: must still be pending and not completed/reminded
                if (!string.Equals(task.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                    task.CompletedAt != null ||
                    task.RemindedAt != null)
                {
                    skipped++;
                    continue;
                }

                // 2. Project validation
                var project = task.Project;
                if (project == null || InactiveProjectStatuses.Contains(project.Status))
                {
                    logger.LogWarning("Task {TaskId} belongs to an invalid or inactive project. Skipping.", task.TaskId);
                    skipped++;
                    continue;
                }

                // 3. Recipient determination & owner validation
                User? recipient = null;
                string linkUrl;
                string roadmapUrl;

                if (string.Equals(task.AssigneeRole, "Customer", StringComparison.OrdinalIgnoreCase))
                {
                    var customer = project.Customer;
                    if (customer == null ||
                        customer.UserId != project.CustomerId ||
                        !string.Equals(customer.Role, "Customer", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning(
                            "Task {TaskId} on project {ProjectCode} has invalid customer owner (CustomerId: {CustomerId}). Skipping.",
                            task.TaskId, project.ProjectCode, project.CustomerId);
                        skipped++;
                        continue;
                    }

                    recipient = customer;
                    linkUrl = "/customer/roadmap";
                    roadmapUrl = $"{baseUrl}/customer/roadmap";
                }
                else if (string.Equals(task.AssigneeRole, "Manager", StringComparison.OrdinalIgnoreCase))
                {
                    var manager = project.Manager;
                    if (manager == null ||
                        manager.UserId != project.ManagerId ||
                        !string.Equals(manager.Role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(manager.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning(
                            "Task {TaskId} on project {ProjectCode} has invalid manager owner (ManagerId: {ManagerId}). Skipping.",
                            task.TaskId, project.ProjectCode, project.ManagerId);
                        skipped++;
                        continue;
                    }

                    recipient = manager;
                    var encodedProjectCode = Uri.EscapeDataString(project.ProjectCode);
                    linkUrl = $"/manager/projects/{encodedProjectCode}/roadmap";
                    roadmapUrl = $"{baseUrl}/manager/projects/{encodedProjectCode}/roadmap";
                }
                else
                {
                    logger.LogWarning(
                        "Task {TaskId} on project {ProjectCode} has unsupported AssigneeRole '{AssigneeRole}'. Skipping.",
                        task.TaskId, project.ProjectCode, task.AssigneeRole);
                    skipped++;
                    continue;
                }

                // 4. Duplicate prevention & idempotency:
                // Verify the task wasn't already marked reminded by another process or previously notified
                var isAlreadyReminded = await db.RoadmapTasks
                    .AsNoTracking()
                    .Where(t => t.TaskId == task.TaskId && t.RemindedAt != null)
                    .AnyAsync(cancellationToken);

                if (isAlreadyReminded)
                {
                    logger.LogInformation("Task {TaskId} has already been reminded in database. Skipping.", task.TaskId);
                    skipped++;
                    continue;
                }

                var taskMarker = $"(Task #{task.TaskId})";
                var duplicateNotification = await db.Notifications
                    .AsNoTracking()
                    .AnyAsync(n => n.RecipientId == recipient.UserId
                                && n.Type == NotificationTypes.RoadmapReminder
                                && n.Message.Contains(taskMarker),
                              cancellationToken);

                if (duplicateNotification)
                {
                    logger.LogInformation("Notification already exists for Task {TaskId} and Recipient {RecipientId}. Skipping.",
                        task.TaskId, recipient.UserId);
                    task.RemindedAt ??= DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    skipped++;
                    continue;
                }

                // 5. Build notification message
                var daysRemaining = Math.Max(0, task.DueDate.DayNumber - today.DayNumber);
                var remainingText = daysRemaining == 0 ? "hôm nay" : $"còn {daysRemaining} ngày";
                var notificationMessage = $"[{project.ProjectCode}] Nhắc việc mốc {task.Milestone} (Task #{task.TaskId}): \"{task.Title}\" đến hạn {task.DueDate:dd/MM/yyyy} ({remainingText}).";

                // 6. Create in-app notification
                await notificationRepository.CreateNotificationAsync(
                    recipientId: recipient.UserId,
                    type: NotificationTypes.RoadmapReminder,
                    message: notificationMessage,
                    linkUrl: linkUrl,
                    relatedEntityType: "RoadmapTask",
                    relatedEntityId: task.TaskId.ToString(),
                    cancellationToken: cancellationToken);

                remindersCreated++;

                // 7. Enqueue email via JOB-07 outbox (isolated: failures must not abort reminder or job)
                try
                {
                    var emailSubject = string.Equals(task.AssigneeRole, "Manager", StringComparison.OrdinalIgnoreCase)
                        ? $"[{project.ProjectCode}] Quản lý lộ trình {task.Milestone}: {task.Title}"
                        : $"[{project.ProjectCode}] Nhắc nhở lộ trình {task.Milestone}: {task.Title}";

                    var emailBody = EmailTemplates.RoadmapMilestoneReminder(
                        fullName: recipient.FullName,
                        role: task.AssigneeRole,
                        projectCode: project.ProjectCode,
                        taskTitle: task.Title,
                        milestone: task.Milestone,
                        dueDate: task.DueDate,
                        remainingDays: daysRemaining,
                        roadmapUrl: roadmapUrl);

                    emailOutbox.Enqueue(
                        toEmail: recipient.Email,
                        subject: emailSubject,
                        bodyHtml: emailBody,
                        kind: "Reminder",
                        projectId: project.ProjectId);

                    emailsQueued++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to queue reminder email for Task {TaskId} to recipient {Email}. In-app notification was successfully created.",
                        task.TaskId, recipient.Email);
                }

                // 8. Mark task as reminded only after notification successfully saved
                task.RemindedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                var errorMsg = $"Task {task.TaskId} error: {ex.Message}";
                errors.Add(errorMsg);
                logger.LogError(ex, "Unexpected error processing reminder for Task {TaskId}.", task.TaskId);
            }
        }

        var completedAt = DateTime.UtcNow;

        logger.LogInformation(
            "JOB-01 [ExecutionId: {ExecutionId}] Summary - StartedAt: {StartedAt:O}, CompletedAt: {CompletedAt:O}, TotalTasksFound: {TotalTasksFound}, TotalRemindersCreated: {TotalRemindersCreated}, TotalEmailsQueued: {TotalEmailsQueued}, TotalSkipped: {TotalSkipped}, TotalFailed: {TotalFailed}",
            executionId, startedAt, completedAt, totalFound, remindersCreated, emailsQueued, skipped, failed);

        if (errors.Count > 0)
        {
            logger.LogWarning(
                "JOB-01 [ExecutionId: {ExecutionId}] Completed with {FailedCount} errors: {Errors}",
                executionId, failed, string.Join("; ", errors));
        }

        return new RoadmapMilestoneReminderResult(
            ExecutionId: executionId,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            TotalTasksFound: totalFound,
            TotalRemindersCreated: remindersCreated,
            TotalEmailsQueued: emailsQueued,
            TotalSkipped: skipped,
            TotalFailed: failed,
            Errors: errors);
    }
}
