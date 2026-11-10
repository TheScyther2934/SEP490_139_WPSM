using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Enums;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed partial class NotificationRepository(WpsmDbContext db) : INotificationRepository
{
    public async Task<NotificationListResponse> GetNotificationsAsync(
        int userId,
        string? filter = "All",
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = db.Notifications.AsNoTracking().Where(n => n.RecipientId == userId);
        var unreadCount = await baseQuery.CountAsync(n => !n.IsRead, cancellationToken);

        var query = baseQuery;
        if (string.Equals(filter, "Unread", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(n => !n.IsRead);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.NotificationId,
                n.Type,
                n.Message,
                n.LinkUrl,
                n.IsRead,
                n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var mapped = items.Select(n =>
        {
            var (entityType, entityId) = ExtractEntityReference(n.Type, n.LinkUrl);
            return new NotificationView(
                n.NotificationId,
                n.Type,
                n.Message,
                n.LinkUrl,
                n.IsRead,
                DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc),
                entityType,
                entityId);
        }).ToList();

        return new NotificationListResponse(mapped, totalCount, unreadCount, page, pageSize);
    }

    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientId == userId && !n.IsRead, cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.RecipientId == userId, cancellationToken);

        if (notification is null) return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        var unread = await db.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0) return 0;

        foreach (var item in unread)
        {
            item.IsRead = true;
        }

        await db.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    public async Task<NotificationView> CreateNotificationAsync(
        int recipientId,
        string type,
        string message,
        string? linkUrl = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = NotificationTypes.Normalize(type);
        var trimmedMessage = message?.Trim() ?? "";
        if (trimmedMessage.Length > 300)
        {
            trimmedMessage = trimmedMessage[..297] + "...";
        }

        var entity = new Notification
        {
            RecipientId = recipientId,
            Type = normalizedType,
            Message = trimmedMessage,
            LinkUrl = linkUrl,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var (detectedType, detectedId) = ExtractEntityReference(entity.Type, entity.LinkUrl);

        return new NotificationView(
            entity.NotificationId,
            entity.Type,
            entity.Message,
            entity.LinkUrl,
            entity.IsRead,
            DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc),
            relatedEntityType ?? detectedType,
            relatedEntityId ?? detectedId);
    }

    private static (string? EntityType, string? EntityId) ExtractEntityReference(string type, string? linkUrl)
    {
        if (string.IsNullOrWhiteSpace(linkUrl))
        {
            return (type switch
            {
                NotificationTypes.RoadmapReminder => "Roadmap",
                NotificationTypes.Payment => "Payment",
                NotificationTypes.CrUpdate => "ChangeRequest",
                NotificationTypes.Beo => "BEO",
                NotificationTypes.OverdueAlert => "OverdueAlert",
                NotificationTypes.Complaint => "Complaint",
                _ => null
            }, null);
        }

        // Pattern: /manager/projects/{code}/change-requests/{crCode}
        var crMatch = Regex.Match(linkUrl, @"/change-requests/([^/?]+)", RegexOptions.IgnoreCase);
        if (crMatch.Success) return ("ChangeRequest", crMatch.Groups[1].Value);

        // Pattern: /complaints/{complaintCode}
        var compMatch = Regex.Match(linkUrl, @"/complaints/([^/?]+)", RegexOptions.IgnoreCase);
        if (compMatch.Success) return ("Complaint", compMatch.Groups[1].Value);

        // Pattern: /manager/projects/{code} or ?id={code}
        var projMatch = Regex.Match(linkUrl, @"/projects/([^/?]+)", RegexOptions.IgnoreCase);
        if (projMatch.Success) return ("Project", projMatch.Groups[1].Value);

        var queryMatch = Regex.Match(linkUrl, @"[?&]id=([^&#]+)", RegexOptions.IgnoreCase);
        if (queryMatch.Success) return ("Project", queryMatch.Groups[1].Value);

        return (type switch
        {
            NotificationTypes.RoadmapReminder => "Roadmap",
            NotificationTypes.Payment => "Payment",
            NotificationTypes.CrUpdate => "ChangeRequest",
            NotificationTypes.Beo => "BEO",
            NotificationTypes.OverdueAlert => "OverdueAlert",
            NotificationTypes.Complaint => "Complaint",
            _ => "General"
        }, null);
    }
}
