using System.Security.Claims;
using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// FE-05 Change Request: C-05 (CR list), C-06 (create CR), M-07 (Manager CR list) and M-08 (CR detail
// and negotiation). The Customer routes resolve the caller's own project (GBR-12); the Manager routes
// carry the project code and are also guarded by the /projects access filter in WpsmApiEndpoints.
public static class ChangeRequestEndpoints
{
    private const string Customer = "Customer";
    private const string Manager = "Manager";

    public static RouteGroupBuilder MapChangeRequestEndpoints(this RouteGroupBuilder api)
    {
        // ── C-05 / C-06 (Customer) ────────────────────────────────────────────
        // GET and POST /customer/change-requests are handled by CustomerEndpoints.cs (IAddendumRepository)
        /*
        api.MapGet("/customer/change-requests", async (IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetListAsync(null, UserId(user), Role(user), ct) is { } list ? Results.Ok(list) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Customer));
        */

        api.MapGet("/customer/change-requests/form", async (IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetFormAsync(null, UserId(user), Role(user), ct) is { } form ? Results.Ok(form) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Customer));

        /*
        api.MapPost("/customer/change-requests", async (CrSaveRequest request, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var created = await repo.CreateAsync(null, UserId(user), Role(user), ToCommand(request), ct);
            return created is null
                ? Results.NotFound()
                : Results.Created($"/api/wpsm/customer/change-requests/{created.Code}", created);
        }).RequireAuthorization(policy => policy.RequireRole(Customer));
        */

        api.MapGet("/customer/change-requests/{crCode}", async (string crCode, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetDetailAsync(null, crCode, UserId(user), Role(user), ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Customer));

        api.MapPut("/customer/change-requests/{crCode}", async (string crCode, CrSaveRequest request, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.UpdateDraftAsync(null, crCode, UserId(user), Role(user), ToCommand(request), ct) is { } detail
                ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Customer));

        api.MapPost("/customer/change-requests/{crCode}/submit", async (string crCode, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.SubmitAsync(null, crCode, UserId(user), Role(user), ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Customer));

        // C-08 lives in FE-06; the decision itself is part of the GBR-04 state machine handled here.
        api.MapPost("/customer/change-requests/{crCode}/approve", async (string crCode, CrApproveRequest request, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.ApproveAsync(null, crCode, UserId(user), Role(user), request.Note, ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Customer));

        api.MapPost("/customer/change-requests/{crCode}/reject", async (string crCode, CrRejectRequest request, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.RejectAsync(null, crCode, UserId(user), Role(user), request.Reason, ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Customer));

        // ── M-07 / M-08 (Manager) ─────────────────────────────────────────────
        // GET /projects/{id}/change-requests is handled by ProjectEndpoints.cs (IAddendumRepository)
        /*
        api.MapGet("/projects/{id}/change-requests", async (string id, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetListAsync(id, UserId(user), Role(user), ct) is { } list ? Results.Ok(list) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Manager));
        */

        api.MapGet("/projects/{id}/change-requests/form", async (string id, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetFormAsync(id, UserId(user), Role(user), ct) is { } form ? Results.Ok(form) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Manager));

        api.MapPost("/projects/{id}/change-requests", async (string id, CrSaveRequest request, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var created = await repo.CreateAsync(id, UserId(user), Role(user), ToCommand(request), ct);
            return created is null
                ? Results.NotFound()
                : Results.Created($"/api/wpsm/projects/{id}/change-requests/{created.Code}", created);
        }).RequireAuthorization(policy => policy.RequireRole(Manager));

        api.MapGet("/projects/{id}/change-requests/{crCode}", async (string id, string crCode, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetDetailAsync(id, crCode, UserId(user), Role(user), ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Manager));

        api.MapPut("/projects/{id}/change-requests/{crCode}", async (string id, string crCode, CrSaveRequest request, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.UpdateDraftAsync(id, crCode, UserId(user), Role(user), ToCommand(request), ct) is { } detail
                ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Manager));

        api.MapPost("/projects/{id}/change-requests/{crCode}/submit", async (string id, string crCode, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.SubmitAsync(id, crCode, UserId(user), Role(user), ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Manager));

        api.MapPost("/projects/{id}/change-requests/{crCode}/counter", async (string id, string crCode, CrCounterRequest request, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.CounterAsync(id, crCode, UserId(user), new CrCounterCommand(request.Items ?? [], request.Note), ct) is { } detail
                ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Manager));

        api.MapPost("/projects/{id}/change-requests/{crCode}/approve", async (string id, string crCode, CrApproveRequest request, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.ApproveAsync(id, crCode, UserId(user), Role(user), request.Note, ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Manager));

        api.MapPost("/projects/{id}/change-requests/{crCode}/reject", async (string id, string crCode, CrRejectRequest request, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.RejectAsync(id, crCode, UserId(user), Role(user), request.Reason, ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Manager));

        api.MapPost("/projects/{id}/change-requests/{crCode}/exception", async (string id, string crCode, CrExceptionRequest request, IChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.RequestExceptionAsync(id, crCode, UserId(user), request.Note, ct) is { } detail ? Results.Ok(detail) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(Manager));

        return api;
    }

    private static CrSaveCommand ToCommand(CrSaveRequest request) =>
        new(request.Type, request.Description, request.Items ?? [], request.Submit);

    private static string Role(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "";
}
