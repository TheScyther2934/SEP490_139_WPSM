using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// C-01, C-03, C-11: Customer dashboard, roadmap and proof photos (own project only).
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
            async (int photoId, WpsmDbContext db, IWebHostEnvironment environment, ClaimsPrincipal user, CancellationToken ct) =>
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

                return PhotoFile(photo.ImageFilePath, environment);
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

        api.MapGet("/customer/projects/{projectCode}/roadmap",
            async (string projectCode, ICustomerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var result = await repo.GetRoadmapAsync(UserId(user), projectCode, ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapPost("/customer/projects/{projectCode}/roadmap/tasks/{taskId:int}/complete",
            async (string projectCode, int taskId, ICustomerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var result = await repo.CompleteTaskAsync(UserId(user), projectCode, taskId, ct);
                if (!result.Success)
                    return Results.BadRequest(new { error = result.Message });
                return Results.Ok(result);
            })
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapGet("/customer/roadmap",
            async (IProjectRepository projects, ICustomerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var userId = UserId(user);
                var myProjects = await projects.GetProjectsAsync(userId, "Customer", ct);
                if (myProjects.Count == 0) return Results.NotFound();
                var result = await repo.GetRoadmapAsync(userId, myProjects[0].Id, ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        // C-06, C-07, C-08: Customer CR list, Create CR, Cost Impact detail & Approve / Reject / Request Adjustment (UC-08, UC-09, UC-10)
        api.MapGet("/customer/change-requests",
            async (string? projectCode, IAddendumRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                await repo.GetProjectChangeRequestsAsync(projectCode, UserId(user), "Customer", ct) is { } crs ? Results.Ok(crs) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapGet("/customer/change-requests/creation-context",
            async (string? projectCode, IAddendumRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                await repo.GetCrCreationContextAsync(projectCode, UserId(user), ct) is { } ctx ? Results.Ok(ctx) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapPost("/customer/change-requests",
            async (CreateCustomerCrRequest request, IAddendumRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                Results.Created("/api/wpsm/customer/change-requests",
                    await repo.CreateCustomerChangeRequestAsync(UserId(user), request.ProjectCode, request.Type, request.Description, request.Items, ct)))
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapGet("/customer/change-requests/{crCode}",
            async (string crCode, string? projectCode, IAddendumRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                await repo.GetCostImpactAsync(projectCode, crCode, UserId(user), "Customer", ct) is { } cr ? Results.Ok(cr) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Customer"));

        api.MapPost("/customer/change-requests/{crCode}/decision",
            async (string crCode, CustomerCrDecisionRequest request, IAddendumRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
                await repo.DecideCustomerCrAsync(crCode, UserId(user), request.Action, request.Note, ct) is { } cr ? Results.Ok(cr) : Results.NotFound())
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
