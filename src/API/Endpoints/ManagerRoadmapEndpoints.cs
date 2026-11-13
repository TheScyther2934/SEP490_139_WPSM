using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Enums;
using Infrastructure.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

/// <summary>
/// M-06: Manager Master Roadmap &amp; Tasks (FE-03, UC-21).
/// </summary>
public static class ManagerRoadmapEndpoints
{
    public static RouteGroupBuilder MapManagerRoadmapEndpoints(this RouteGroupBuilder api)
    {
        // ── Load Roadmap with Milestones & Header Metadata ───────────────────
        api.MapGet("/manager/projects/{id}/roadmap",
            async (string id, [FromQuery] string? owner, IManagerRoadmapRepository repo, WpsmDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var filter = TaskOwnerFilter.All;
                if (!string.IsNullOrWhiteSpace(owner) && !Enum.TryParse(owner, true, out filter))
                    return Results.BadRequest(new { message = "Bộ lọc owner không hợp lệ. Giá trị hợp lệ: All, Manager, Customer" });

                var result = await repo.GetRoadmapAsync(UserId(user), Role(user), id, filter, ct);
                if (result is null)
                {
                    var isNum = int.TryParse(id, out var intId);
                    var exists = await db.WeddingProjects.AnyAsync(p => p.ProjectCode == id || (isNum && p.ProjectId == intId), ct);
                    if (!exists) return Results.NotFound(new { message = "Dự án không tồn tại." });
                    return Results.Json(new { message = "Bạn không có quyền quản lý dự án này." }, statusCode: StatusCodes.Status403Forbidden);
                }
                return Results.Ok(result);
            })
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        // ── Load Flat Task List ──────────────────────────────────────────────
        api.MapGet("/manager/projects/{id}/roadmap/tasks",
            async (string id, [FromQuery] string? owner, IManagerRoadmapRepository repo, WpsmDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var filter = TaskOwnerFilter.All;
                if (!string.IsNullOrWhiteSpace(owner) && !Enum.TryParse(owner, true, out filter))
                    return Results.BadRequest(new { message = "Bộ lọc owner không hợp lệ. Giá trị hợp lệ: All, Manager, Customer" });

                var result = await repo.GetRoadmapTasksAsync(UserId(user), Role(user), id, filter, ct);
                if (result is null)
                {
                    var isNum = int.TryParse(id, out var intId);
                    var exists = await db.WeddingProjects.AnyAsync(p => p.ProjectCode == id || (isNum && p.ProjectId == intId), ct);
                    if (!exists) return Results.NotFound(new { message = "Dự án không tồn tại." });
                    return Results.Json(new { message = "Bạn không có quyền quản lý dự án này." }, statusCode: StatusCodes.Status403Forbidden);
                }
                return Results.Ok(result);
            })
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        // ── Mark Manager Task Done (UC-21) ───────────────────────────────────
        api.MapPost("/manager/projects/{id}/roadmap/tasks/{taskId:int}/complete",
            async (string id, int taskId, HttpContext context, IManagerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            {
                CompleteManagerTaskRequest? request = null;
                if (context.Request.HasJsonContentType())
                {
                    try { request = await context.Request.ReadFromJsonAsync<CompleteManagerTaskRequest>(cancellationToken: ct); } catch { }
                }
                var result = await repo.CompleteTaskAsync(UserId(user), Role(user), id, taskId, request?.ProofPhotoPaths, ct);
                if (!result.Success)
                {
                    if (result.Message.Contains("không tồn tại"))
                        return Results.NotFound(new { message = result.Message });
                    if (result.Message.Contains("không có quyền quản lý"))
                        return Results.Json(new { message = result.Message }, statusCode: StatusCodes.Status403Forbidden);
                    return Results.BadRequest(new { message = result.Message });
                }
                return Results.Ok(result);
            })
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        // ── Load First Manager Project Roadmap (Shortcut) ───────────────────
        api.MapGet("/manager/roadmap",
            async ([FromQuery] string? owner, IProjectRepository projects, IManagerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var filter = TaskOwnerFilter.All;
                if (!string.IsNullOrWhiteSpace(owner) && !Enum.TryParse(owner, true, out filter))
                    return Results.BadRequest(new { message = "Bộ lọc owner không hợp lệ. Giá trị hợp lệ: All, Manager, Customer" });

                var userId = UserId(user);
                var role = Role(user);
                var myProjects = await projects.GetProjectsAsync(userId, role, ct);
                if (myProjects.Count == 0) return Results.NotFound(new { message = "Chưa có dự án nào." });

                var result = await repo.GetRoadmapAsync(userId, role, myProjects[0].Id, filter, ct);
                return result is null ? Results.NotFound(new { message = "Không tìm thấy lộ trình." }) : Results.Ok(result);
            })
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        // ── Aliases without /manager prefix for route flexibility ───────────
        api.MapGet("/projects/{id}/roadmap",
            async (string id, [FromQuery] string? owner, IManagerRoadmapRepository repo, WpsmDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var filter = TaskOwnerFilter.All;
                if (!string.IsNullOrWhiteSpace(owner) && !Enum.TryParse(owner, true, out filter))
                    return Results.BadRequest(new { message = "Bộ lọc owner không hợp lệ. Giá trị hợp lệ: All, Manager, Customer" });

                var result = await repo.GetRoadmapAsync(UserId(user), Role(user), id, filter, ct);
                if (result is null)
                {
                    var isNum = int.TryParse(id, out var intId);
                    var exists = await db.WeddingProjects.AnyAsync(p => p.ProjectCode == id || (isNum && p.ProjectId == intId), ct);
                    if (!exists) return Results.NotFound(new { message = "Dự án không tồn tại." });
                    return Results.Json(new { message = "Bạn không có quyền quản lý dự án này." }, statusCode: StatusCodes.Status403Forbidden);
                }
                return Results.Ok(result);
            })
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        api.MapGet("/projects/{id}/roadmap/tasks",
            async (string id, [FromQuery] string? owner, IManagerRoadmapRepository repo, WpsmDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var filter = TaskOwnerFilter.All;
                if (!string.IsNullOrWhiteSpace(owner) && !Enum.TryParse(owner, true, out filter))
                    return Results.BadRequest(new { message = "Bộ lọc owner không hợp lệ. Giá trị hợp lệ: All, Manager, Customer" });

                var result = await repo.GetRoadmapTasksAsync(UserId(user), Role(user), id, filter, ct);
                if (result is null)
                {
                    var isNum = int.TryParse(id, out var intId);
                    var exists = await db.WeddingProjects.AnyAsync(p => p.ProjectCode == id || (isNum && p.ProjectId == intId), ct);
                    if (!exists) return Results.NotFound(new { message = "Dự án không tồn tại." });
                    return Results.Json(new { message = "Bạn không có quyền quản lý dự án này." }, statusCode: StatusCodes.Status403Forbidden);
                }
                return Results.Ok(result);
            })
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        api.MapPost("/projects/{id}/roadmap/tasks/{taskId:int}/complete",
            async (string id, int taskId, HttpContext context, IManagerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            {
                CompleteManagerTaskRequest? request = null;
                if (context.Request.HasJsonContentType())
                {
                    try { request = await context.Request.ReadFromJsonAsync<CompleteManagerTaskRequest>(cancellationToken: ct); } catch { }
                }
                var result = await repo.CompleteTaskAsync(UserId(user), Role(user), id, taskId, request?.ProofPhotoPaths, ct);
                if (!result.Success)
                {
                    if (result.Message.Contains("không tồn tại"))
                        return Results.NotFound(new { message = result.Message });
                    if (result.Message.Contains("không có quyền quản lý"))
                        return Results.Json(new { message = result.Message }, statusCode: StatusCodes.Status403Forbidden);
                    return Results.BadRequest(new { message = result.Message });
                }
                return Results.Ok(result);
            })
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        return api;
    }
}
