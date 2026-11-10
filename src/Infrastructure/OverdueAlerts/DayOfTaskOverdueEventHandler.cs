using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Events;
using Application.Persistence;
using Domain.Enums;
using Domain.Events;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.OverdueAlerts;

/// <summary>
/// Domain event handler for DayOfTaskOverdueEvent.
/// Dispatches in-app notifications and alert emails to assigned Manager and active Admins (MSG21, GBR-12).
/// Strictly excludes Customer recipients.
/// </summary>
public sealed class DayOfTaskOverdueEventHandler(
    WpsmDbContext db,
    INotificationRepository notificationRepository,
    IEmailOutbox emailOutbox,
    EmailOptions emailOptions,
    ILogger<DayOfTaskOverdueEventHandler> logger) : IDomainEventHandler<DayOfTaskOverdueEvent>
{
    public async Task HandleAsync(DayOfTaskOverdueEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var deadlineVn = RoadmapRules.ConvertUtcToVietnam(domainEvent.DeadlineTime);
        var msg21 = $"[MSG21] Task \"{domainEvent.TaskName}\" is overdue since {deadlineVn:HH:mm dd/MM/yyyy}. Dự án {domainEvent.ProjectCode} ({domainEvent.HallName}) ngày {domainEvent.EventDate:dd/MM/yyyy}.";

        var baseUrl = emailOptions.PublicBaseUrl?.TrimEnd('/') ?? "http://localhost:5173";

        // 1. Manager In-App & Email (UC-32, M-23)
        if (domainEvent.ManagerId > 0)
        {
            var manager = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == domainEvent.ManagerId && u.Status == "Active", cancellationToken);

            if (manager != null)
            {
                var managerLink = $"/manager/projects/{Uri.EscapeDataString(domainEvent.ProjectCode)}/day-of?taskId={domainEvent.TaskId}";
                var alreadyNotified = await db.Notifications
                    .AsNoTracking()
                    .AnyAsync(n => n.RecipientId == manager.UserId
                                && n.Type == NotificationTypes.OverdueAlert
                                && n.LinkUrl != null
                                && n.LinkUrl.Contains($"taskId={domainEvent.TaskId}"), cancellationToken);

                if (!alreadyNotified)
                {
                    await notificationRepository.CreateNotificationAsync(
                        recipientId: manager.UserId,
                        type: NotificationTypes.OverdueAlert,
                        message: msg21,
                        linkUrl: managerLink,
                        relatedEntityType: "DayOfTask",
                        relatedEntityId: domainEvent.TaskId.ToString(),
                        cancellationToken: cancellationToken);

                    try
                    {
                        var emailSubject = $"[{domainEvent.ProjectCode}] Cảnh báo nhiệm vụ quá hạn: {domainEvent.TaskName}";
                        var alertUrl = $"{baseUrl}{managerLink}";
                        var emailBody = EmailTemplates.OverdueAlert(
                            recipientName: manager.FullName,
                            role: "Manager",
                            projectCode: domainEvent.ProjectCode,
                            taskName: domainEvent.TaskName,
                            deadline: deadlineVn,
                            hallName: domainEvent.HallName,
                            eventDate: domainEvent.EventDate,
                            alertUrl: alertUrl);

                        emailOutbox.Enqueue(
                            toEmail: manager.Email,
                            subject: emailSubject,
                            bodyHtml: emailBody,
                            kind: "OverdueAlert",
                            projectId: domainEvent.ProjectId);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Failed to queue overdue email for Manager {Email} on Task {TaskId}.", manager.Email, domainEvent.TaskId);
                    }
                }
            }
        }

        // 2. All Active Admins In-App & Email (UC-45, A-15)
        var activeAdmins = await db.Users
            .AsNoTracking()
            .Where(u => u.Role == "Admin" && u.Status == "Active")
            .ToListAsync(cancellationToken);

        foreach (var admin in activeAdmins)
        {
            var adminLink = $"/admin/photo-feed?projectCode={Uri.EscapeDataString(domainEvent.ProjectCode)}";
            var alreadyNotified = await db.Notifications
                .AsNoTracking()
                .AnyAsync(n => n.RecipientId == admin.UserId
                            && n.Type == NotificationTypes.OverdueAlert
                            && n.Message.Contains(domainEvent.ProjectCode)
                            && n.Message.Contains(domainEvent.TaskName), cancellationToken);

            if (!alreadyNotified)
            {
                await notificationRepository.CreateNotificationAsync(
                    recipientId: admin.UserId,
                    type: NotificationTypes.OverdueAlert,
                    message: msg21,
                    linkUrl: adminLink,
                    relatedEntityType: "DayOfTask",
                    relatedEntityId: domainEvent.TaskId.ToString(),
                    cancellationToken: cancellationToken);

                try
                {
                    var emailSubject = $"[{domainEvent.ProjectCode}] Cảnh báo nhiệm vụ quá hạn: {domainEvent.TaskName}";
                    var alertUrl = $"{baseUrl}/admin/overdue-alerts";
                    var emailBody = EmailTemplates.OverdueAlert(
                        recipientName: admin.FullName,
                        role: "Admin",
                        projectCode: domainEvent.ProjectCode,
                        taskName: domainEvent.TaskName,
                        deadline: deadlineVn,
                        hallName: domainEvent.HallName,
                        eventDate: domainEvent.EventDate,
                        alertUrl: alertUrl);

                    emailOutbox.Enqueue(
                        toEmail: admin.Email,
                        subject: emailSubject,
                        bodyHtml: emailBody,
                        kind: "OverdueAlert",
                        projectId: domainEvent.ProjectId);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Failed to queue overdue email for Admin {Email} on Task {TaskId}.", admin.Email, domainEvent.TaskId);
                }
            }
        }
    }
}
