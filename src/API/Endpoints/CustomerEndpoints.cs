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
            async (string projectCode, ICustomerDashboardRepository dashboard, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var result = await dashboard.GetDashboardAsync(UserId(user), projectCode, ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
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
            async (IProjectRepository projects, ICustomerDashboardRepository dashboard, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var userId = UserId(user);
                var myProjects = await projects.GetProjectsAsync(userId, "Customer", ct);
                if (myProjects.Count == 0) return Results.NotFound();
                var result = await dashboard.GetDashboardAsync(userId, myProjects[0].Id, ct);
                return result is null ? Results.NotFound() : Results.Ok(result);
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

        return api;
    }
}
