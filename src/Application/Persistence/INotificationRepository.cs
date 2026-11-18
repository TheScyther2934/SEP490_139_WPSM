namespace Application.Persistence;

public sealed record NotificationView(
    int NotificationId,
    string Type,
    string Message,
    string? LinkUrl,
    bool IsRead,
    DateTime CreatedAt,
    string? RelatedEntityType = null,
    string? RelatedEntityId = null);

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationView> Items,
    int TotalCount,
    int UnreadCount,
    int Page,
    int PageSize);

public sealed record UnreadCountResponse(int UnreadCount);

public sealed record MarkAsReadResult(bool Success, string Message);

public sealed record MarkAllAsReadResult(bool Success, int UpdatedCount);

public interface INotificationRepository
{
    Task<NotificationListResponse> GetNotificationsAsync(
        int userId,
        string? filter = "All",
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(
        int userId,
        int notificationId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<NotificationView> CreateNotificationAsync(
        int recipientId,
        string type,
        string message,
        string? linkUrl = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        CancellationToken cancellationToken = default);
}
