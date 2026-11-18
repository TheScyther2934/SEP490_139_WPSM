using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Application.DayOfTasks;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

/// <summary>
/// Day-of Operations endpoints (FE-08, FE-09, M-16, M-17, UC-30, UC-31).
/// </summary>
public static class DayOfTaskEndpoints
{
    public static RouteGroupBuilder MapDayOfTaskEndpoints(this RouteGroupBuilder api)
    {
        // ── Get Day-of Tasks for a Wedding Project (M-16) ───────────────────
        api.MapGet("/projects/{id}/day-of-tasks", async (
            string id,
            IDayOfTaskService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var tasks = await service.GetDayOfTasksAsync(id, UserId(user), Role(user), ct);
            if (tasks is null)
            {
                return Results.NotFound(new { message = "Không tìm thấy dự án hoặc bạn không có quyền truy cập." });
            }

            return Results.Ok(tasks);
        }).RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        // ── Complete Day-of Task with Proof Photos (M-17) ──────────────────
        api.MapPost("/day-of-tasks/{id:int}/complete", async (
            int id,
            HttpRequest request,
            IDayOfTaskService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var files = form.Files;

            var uploadFiles = new List<DayOfTaskUploadFile>();
            foreach (var f in files)
            {
                uploadFiles.Add(new DayOfTaskUploadFile(
                    Stream: f.OpenReadStream(),
                    FileName: f.FileName,
                    ContentType: f.ContentType ?? "image/jpeg",
                    Length: f.Length
                ));
            }

            var command = new CompleteDayOfTaskCommand(
                TaskId: id,
                UserId: UserId(user),
                UserRole: Role(user),
                Photos: uploadFiles);

            var result = await service.CompleteDayOfTaskAsync(command, ct);

            if (!result.Success)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(new
            {
                message = result.Message,
                task = result.Task
            });
        })
        .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"))
        .DisableAntiforgery();

        // Convenience route with project code
        api.MapPost("/projects/{projectCode}/day-of-tasks/{id:int}/complete", async (
            string projectCode,
            int id,
            HttpRequest request,
            IDayOfTaskService service,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var files = form.Files;

            var uploadFiles = new List<DayOfTaskUploadFile>();
            foreach (var f in files)
            {
                uploadFiles.Add(new DayOfTaskUploadFile(
                    Stream: f.OpenReadStream(),
                    FileName: f.FileName,
                    ContentType: f.ContentType ?? "image/jpeg",
                    Length: f.Length
                ));
            }

            var command = new CompleteDayOfTaskCommand(
                TaskId: id,
                UserId: UserId(user),
                UserRole: Role(user),
                Photos: uploadFiles);

            var result = await service.CompleteDayOfTaskAsync(command, ct);

            if (!result.Success)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(new
            {
                message = result.Message,
                task = result.Task
            });
        })
        .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"))
        .DisableAntiforgery();

        return api;
    }
}
