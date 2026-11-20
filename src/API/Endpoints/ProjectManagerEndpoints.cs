using System.Security.Claims;
using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

public sealed record AddProjectManagerRequest(int ManagerId, string? Reason);
public sealed record RemoveProjectManagerRequest(string? Reason);

// Co-management of a project: list operators, add or remove co-managers (Primary Manager or Admin).
// /projects/{id}/... passes the GBR-12 filter first, so any operator (and Admin) can read; changes are checked in the repository.
public static class ProjectManagerEndpoints
{
    public static RouteGroupBuilder MapProjectManagerEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/projects/{id}/managers").RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        group.MapGet("", async (string id, IProjectManagerRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetAsync(id, UserId(user), Role(user), ct) is { } view ? Results.Ok(view) : Results.NotFound());

        group.MapGet("/candidates", async (string id, IProjectManagerRepository repo, CancellationToken ct) => Results.Ok(await repo.GetCandidatesAsync(id, ct)));

        group.MapPost("", async (string id, AddProjectManagerRequest request, IProjectManagerRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            ToResult(await repo.AddAsync(id, request.ManagerId, request.Reason, UserId(user), Role(user), ct)));

        group.MapPost("/{projectManagerId:int}/remove", async (string id, int projectManagerId, RemoveProjectManagerRequest request,
            IProjectManagerRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            ToResult(await repo.RemoveAsync(id, projectManagerId, request.Reason, UserId(user), Role(user), ct)));

        return api;
    }

    private static IResult ToResult(ProjectManagerResult result) => result switch
    {
        { NotFound: true } => Results.NotFound(new { message = "Không tìm thấy sự kiện hoặc người điều hành." }),
        { Forbidden: true } => Results.Json(new { message = result.Error }, statusCode: StatusCodes.Status403Forbidden),
        { Error: not null } => Results.BadRequest(new { message = result.Error }),
        _ => Results.Ok(result.View)
    };
}
