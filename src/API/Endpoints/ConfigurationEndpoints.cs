using System.Security.Claims;
using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// A-02..A-08: service catalog, PriceFrames and policies (read by all roles, changed by Admin).
public static class ConfigurationEndpoints
{
    public static RouteGroupBuilder MapConfigurationEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/catalog", async (IConfigurationRepository repo, CancellationToken ct) => Results.Ok(await repo.GetCatalogAsync(ct)))
            .RequireAuthorization();
        api.MapPost("/catalog", async (CatalogItemRequest request, IConfigurationRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.AddCatalogItemAsync(new CatalogItemCommand(request.Name, request.Category, request.Unit, request.BasePrice, request.Active), ct)))
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        api.MapGet("/price-frames", async (IConfigurationRepository repo, CancellationToken ct) => Results.Ok(await repo.GetPriceFramesAsync(ct)))
            .RequireAuthorization();
        api.MapPut("/price-frames/{id:int}", async (int id, PriceFrameRequest request, IConfigurationRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.UpdatePriceFrameAsync(id, new PriceFrameCommand(request.MinPrice, request.MaxPrice, request.EffectiveFrom), UserId(user), ct) is { } frame
                ? Results.Ok(frame) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        api.MapGet("/policies", async (IConfigurationRepository repo, CancellationToken ct) => Results.Ok(await repo.GetPoliciesAsync(ct)))
            .RequireAuthorization();
        api.MapPut("/policies", async (PolicyRequest request, IConfigurationRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await repo.UpdatePoliciesAsync(new PolicyCommand(request.NrdRatio, request.T30Ratio, request.CrPenaltyDays, request.CrPenaltyPercent, request.EffectiveFrom), UserId(user), ct)))
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        return api;
    }
}
