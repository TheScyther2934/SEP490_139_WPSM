using System.Security.Claims;
using static API.Endpoints.EndpointHelpers;
using Application.Persistence;


namespace API.Endpoints;

// A-01, A-09..A-18: Admin dashboard, reports, photo feed, escalations, exceptions (GBR-12: Admin only).
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization(policy => policy.RequireRole("Admin"));
        admin.MapGet("/dashboard", async (IAdminRepository repo, PhotoLinks links, CancellationToken ct) =>
        {
            var dashboard = await repo.GetDashboardAsync(ct);
            return Results.Ok(dashboard with { LatestPhotos = dashboard.LatestPhotos.Select(p => p with { ImageUrl = links.Url(p.PhotoId) }).ToList() });
        });
        admin.MapGet("/reports", async (DateOnly from, DateOnly to, IAdminRepository repo, CancellationToken ct) => Results.Ok(await repo.GetReportAsync(from, to, ct)));
        admin.MapGet("/photos", async (string? projectCode, int? hallId, string? taskStatus, DateOnly? date, IAdminRepository repo, PhotoLinks links, CancellationToken ct) =>
            Results.Ok((await repo.GetPhotosAsync(new PhotoFeedFilter(projectCode, hallId, taskStatus, date), ct)).Select(p => p with { ImageUrl = links.Url(p.PhotoId) })));
        admin.MapGet("/photo-feed", async (string? projectCode, int? hallId, string? taskStatus, DateOnly? date, IAdminRepository repo, PhotoLinks links, CancellationToken ct) =>
            Results.Ok((await repo.GetPhotosAsync(new PhotoFeedFilter(projectCode, hallId, taskStatus, date), ct)).Select(p => p with { ImageUrl = links.Url(p.PhotoId) })));
        admin.MapGet("/photos/{photoId:int}", async (int photoId, IAdminRepository repo, PhotoLinks links, CancellationToken ct) =>
            await repo.GetPhotoAsync(photoId, ct) is { } photo ? Results.Ok(photo with { ImageUrl = links.Url(photo.PhotoId) }) : Results.NotFound());
        admin.MapGet("/photos/{photoId:int}/content", async (int photoId, IAdminRepository repo, IWebHostEnvironment environment, CancellationToken ct) =>
            await repo.GetPhotoPathAsync(photoId, ct) is { } path ? PhotoFile(path, environment) : Results.NotFound());
        admin.MapGet("/overdue-alerts", async (IAdminRepository repo, CancellationToken ct) => Results.Ok(await repo.GetOverdueAlertsAsync(ct)));
        admin.MapGet("/complaints", async (IAdminRepository repo, CancellationToken ct) => Results.Ok(await repo.GetEscalatedComplaintsAsync(ct)));
        admin.MapPost("/complaints/{complaintId:int}/resolve", async (int complaintId, ResolveComplaintRequest request, IAdminRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.ResolveComplaintAsync(complaintId, request.Decision, UserId(user), ct) ? Results.NoContent() : Results.NotFound());
        admin.MapGet("/exception-requests", async (IAdminRepository repo, CancellationToken ct) => Results.Ok(await repo.GetExceptionRequestsAsync(ct)));
        admin.MapPost("/exception-requests/{requestId:int}/decision", async (int requestId, ExceptionDecisionRequest request, IAdminRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.DecideExceptionAsync(requestId, request.Approve, request.Note, UserId(user), ct) ? Results.NoContent() : Results.NotFound());
        return api;
    }
}
