using System.Security.Claims;
using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// FE-07 Resource Calendar: M-21 (calendar grid, allocation dialog) and M-22 (conflict detail and
// rescheduling). Manager only; every call is scoped to the caller's own projects (GBR-12).
public static class ResourceCalendarEndpoints
{
    public static RouteGroupBuilder MapResourceCalendarEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/resource-calendar", async (DateOnly from, DateOnly to, int? hallId, IResourceCalendarRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetCalendarAsync(from, to, hallId, UserId(user), ct) is { } calendar ? Results.Ok(calendar) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapGet("/resource-allocations/{allocationId:int}", async (int allocationId, IResourceCalendarRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetAllocationAsync(allocationId, UserId(user), ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapGet("/resources", async (IResourceCalendarRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetResourcesAsync(ct)))
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        // POST/PUT/DELETE /resource-allocations are handled by OperationsEndpoints (IResourceRepository)
        // to support the ProjectCode-based allocation payload used by M-21 AllocationDialog and e2e operations.
        /*
        api.MapPost("/resource-allocations", async (ResourceAllocationRequest request, IResourceCalendarRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await repo.SaveAsync(null, ToCommand(request), UserId(user), ct);
            return result is null ? Results.NotFound()
                : result.Conflict is not null ? Conflict(result.Conflict)
                : Results.Created($"/api/wpsm/resource-allocations/{result.Allocation!.AllocationId}", result.Allocation);
        }).RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapPut("/resource-allocations/{allocationId:int}", async (int allocationId, ResourceAllocationRequest request, IResourceCalendarRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await repo.SaveAsync(allocationId, ToCommand(request), UserId(user), ct);
            return result is null ? Results.NotFound()
                : result.Conflict is not null ? Conflict(result.Conflict)
                : Results.Ok(result.Allocation);
        }).RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapDelete("/resource-allocations/{allocationId:int}", async (int allocationId, IResourceCalendarRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.ReleaseAsync(allocationId, UserId(user), ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));
        */

        api.MapPost("/resource-allocations/{allocationId:int}/exception", async (int allocationId, ResourceExceptionRequest request, IResourceCalendarRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.RequestExceptionAsync(allocationId, request.Note, UserId(user), ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        return api;
    }

    private static ResourceAllocationCommand ToCommand(ResourceAllocationRequest request) =>
        new(request.ResourceId, request.ProjectId, request.StartTime, request.EndTime, request.Quantity, request.RequestException, request.ExceptionNote);

    /// <summary>GBR-09 / MSG08: the allocation is not saved; the SPA opens the M-22 breakdown.</summary>
    private static IResult Conflict(ResourceConflictView conflict) =>
        Results.Json(new
        {
            message = $"Xung đột tài nguyên: {conflict.ResourceName} chỉ còn thiếu {conflict.Shortage} so với nhu cầu trong khung giờ đã chọn (MSG08).",
            conflict
        }, statusCode: StatusCodes.Status409Conflict);
}
