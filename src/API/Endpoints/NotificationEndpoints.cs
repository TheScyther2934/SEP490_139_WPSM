using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder api)
    {
        var notifications = api.MapGroup("/notifications")
            .RequireAuthorization(policy => policy.RequireRole("Customer", "Manager"));

        // GET /api/wpsm/notifications?filter=All&page=1&pageSize=20
        notifications.MapGet("", async (
            [FromQuery] string? filter,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            INotificationRepository repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var p = Math.Max(1, page ?? 1);
            var ps = Math.Clamp(pageSize ?? 20, 1, 100);
            var result = await repo.GetNotificationsAsync(UserId(user), filter, p, ps, ct);
            return Results.Ok(result);
        });

        // GET /api/wpsm/notifications/unread-count
        notifications.MapGet("/unread-count", async (
            INotificationRepository repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var count = await repo.GetUnreadCountAsync(UserId(user), ct);
            return Results.Ok(new UnreadCountResponse(count));
        });

        // PATCH /api/wpsm/notifications/{notificationId}/read
        notifications.MapPatch("/{notificationId:int}/read", async (
            int notificationId,
            INotificationRepository repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var success = await repo.MarkAsReadAsync(UserId(user), notificationId, ct);
            if (!success)
            {
                return Results.NotFound(new { message = "Thông báo không tồn tại hoặc bạn không có quyền thao tác." });
            }
            return Results.Ok(new { success = true, notificationId });
        });

        // Also support POST for broader client compatibility
        notifications.MapPost("/{notificationId:int}/read", async (
            int notificationId,
            INotificationRepository repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var success = await repo.MarkAsReadAsync(UserId(user), notificationId, ct);
            if (!success)
            {
                return Results.NotFound(new { message = "Thông báo không tồn tại hoặc bạn không có quyền thao tác." });
            }
            return Results.Ok(new { success = true, notificationId });
        });

        // PATCH /api/wpsm/notifications/read-all
        notifications.MapPatch("/read-all", async (
            INotificationRepository repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var count = await repo.MarkAllAsReadAsync(UserId(user), ct);
            return Results.Ok(new { success = true, updatedCount = count });
        });

        // Also support POST for broader client compatibility
        notifications.MapPost("/read-all", async (
            INotificationRepository repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var count = await repo.MarkAllAsReadAsync(UserId(user), ct);
            return Results.Ok(new { success = true, updatedCount = count });
        });

        return api;
    }
}
