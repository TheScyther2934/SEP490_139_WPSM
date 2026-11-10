using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>UC-03, G-04: a user sees only their own notifications.</summary>
public sealed class NotificationRepository(WpsmDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<NotificationView>> GetAsync(int userId, bool unreadOnly, CancellationToken cancellationToken = default)
    {
        var rows = await db.Notifications.AsNoTracking().Where(x => x.RecipientId == userId && (!unreadOnly || !x.IsRead))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.NotificationId).Take(200)
            .Select(x => new NotificationView(x.NotificationId, x.Type, x.Message, x.LinkUrl, x.IsRead, x.CreatedAt)).ToListAsync(cancellationToken);
        return rows.Select(x => x with { CreatedAt = Utc(x.CreatedAt) }).ToList();
    }

    public async Task<bool> MarkReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default) =>
        await db.Notifications.Where(x => x.NotificationId == notificationId && x.RecipientId == userId)
            .ExecuteUpdateAsync(x => x.SetProperty(n => n.IsRead, true), cancellationToken) > 0;

    public Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Notifications.Where(x => x.RecipientId == userId && !x.IsRead).ExecuteUpdateAsync(x => x.SetProperty(n => n.IsRead, true), cancellationToken);
}
