using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// Customer dashboard, proof photos, post-event ratings and complaints (own events only). The booked event view is in EventFlowEndpoints.
public static class CustomerEndpoints
{
    public static RouteGroupBuilder MapCustomerEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/customer/projects/{projectCode}/dashboard",
            async (string projectCode, ICustomerDashboardRepository dashboard, PhotoLinks links, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var result = await dashboard.GetDashboardAsync(UserId(user), projectCode, ct);
                return result is null ? Results.NotFound() : Results.Ok(WithPhotoUrl(result, links));
            })
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapGet("/customer/photos/{photoId:int}",
            async (int photoId, WpsmDbContext db, IFileService files, HttpContext context, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var userId = UserId(user);
                var photo = await db.ExecutionPhotos
                    .AsNoTracking()
                    .Include(ep => ep.DayOfTask)
                        .ThenInclude(dot => dot.Project)
                    .FirstOrDefaultAsync(ep => ep.PhotoId == photoId, ct);

                if (photo is null) return Results.NotFound();
                if (photo.DayOfTask.Project.CustomerId != userId)
                    return Results.Forbid();

                // Customers see photos only, never internal documents (PDF proof).
                if (photo.ContentType is { } type && !type.StartsWith("image/", StringComparison.Ordinal)) return Results.NotFound();
                return await ServeStoredFile(files, new StoredFileRef(photo.ImageFilePath, photo.ContentType ?? "image/jpeg", photo.FileName ?? "anh"), context, ct);
            })
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapGet("/customer/dashboard",
            async (IProjectRepository projects, ICustomerDashboardRepository dashboard, PhotoLinks links, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var userId = UserId(user);
                var myProjects = await projects.GetProjectsAsync(userId, "Customer", ct);
                if (myProjects.Count == 0) return Results.NotFound();
                var result = await dashboard.GetDashboardAsync(userId, myProjects[0].Id, ct);
                return result is null ? Results.NotFound() : Results.Ok(WithPhotoUrl(result, links));
            })
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        // C-12, C-13: Post-event summary & Rate Services (UC-14, GBR-10, MSG16/26)
        api.MapGet("/customer/post-event",
            async (string? projectCode, IComplaintRatingRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                await repo.GetPostEventSummaryAsync(UserId(user), projectCode, ct) is { } summary ? Results.Ok(summary) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapPost("/customer/ratings",
            async (SubmitRatingsRequest request, IComplaintRatingRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                await repo.SubmitRatingsAsync(UserId(user), request.ProjectCode, request.Ratings, ct) is { } summary ? Results.Ok(summary) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        // C-14, C-15, C-16: Customer Complaints List, File Complaint, Complaint Status (UC-15, UC-16, MSG25)
        api.MapGet("/customer/complaints",
            async (string? projectCode, IComplaintRatingRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                Results.Ok(await repo.GetCustomerComplaintsAsync(UserId(user), projectCode, ct)))
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapGet("/customer/complaints/beo-options",
            async (string? projectCode, IComplaintRatingRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                Results.Ok(await repo.GetCustomerBeoOptionsAsync(UserId(user), projectCode, ct)))
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapPost("/customer/complaints",
            async (CreateComplaintRequest request, IComplaintRatingRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                Results.Created("/api/wpsm/customer/complaints", await repo.CreateCustomerComplaintAsync(UserId(user), request.ProjectCode, request.BeoId, request.Description, ct)))
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapGet("/customer/complaints/{complaintCode}",
            async (string complaintCode, IComplaintRatingRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                await repo.GetCustomerComplaintAsync(UserId(user), complaintCode, ct) is { } item ? Results.Ok(item) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        return api;
    }

    private static CustomerDashboardView WithPhotoUrl(CustomerDashboardView dashboard, PhotoLinks links) =>
        dashboard.LatestProofPhoto is { } photo
            ? dashboard with { LatestProofPhoto = photo with { ImageUrl = links.Url(photo.PhotoId) } }
            : dashboard;
}
