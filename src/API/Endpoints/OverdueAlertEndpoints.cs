using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Application.OverdueAlerts;
using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

/// <summary>
/// FE-09: Overdue Operational Alerts endpoints (M-23, A-15, UC-32, UC-45).
/// </summary>
public static class OverdueAlertEndpoints
{
    public static RouteGroupBuilder MapOverdueAlertEndpoints(this RouteGroupBuilder api)
    {
        // Role-adaptive endpoint per Report 4 SDS (Admin gets all, Manager gets assigned projects)
        api.MapGet("/overdue-alerts", async (
            IAdminRepository adminRepo,
            IManagerOverdueAlertService managerRepo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var role = Role(user);
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var alerts = await adminRepo.GetOverdueAlertsAsync(ct);
                return Results.Ok(alerts);
            }

            if (string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase))
            {
                var alerts = await managerRepo.GetManagerOverdueAlertsAsync(UserId(user), ct);
                return Results.Ok(alerts);
            }

            return Results.Json(new { message = "Bạn không có quyền truy cập cảnh báo quá hạn." }, statusCode: StatusCodes.Status403Forbidden);
        }).RequireAuthorization(policy => policy.RequireRole("Admin", "Manager"));

        // Explicit Manager-scoped endpoint for M-23
        api.MapGet("/manager/overdue-alerts", async (
            IManagerOverdueAlertService repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var alerts = await repo.GetManagerOverdueAlertsAsync(UserId(user), ct);
            return Results.Ok(alerts);
        }).RequireAuthorization(policy => policy.RequireRole("Manager"));

        return api;
    }
}
