using System.Security.Claims;
using Application.Dashboard;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

/// <summary>
/// M-01 Manager Dashboard endpoints (FE-03, FE-09).
/// </summary>
public static class ManagerDashboardEndpoints
{
    public static RouteGroupBuilder MapManagerDashboardEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/manager/dashboard", async (
            IManagerDashboardService dashboardService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var response = await dashboardService.GetDashboardAsync(UserId(user), ct);
            return Results.Ok(response);
        }).RequireAuthorization(policy => policy.RequireRole("Manager"));

        return api;
    }
}
