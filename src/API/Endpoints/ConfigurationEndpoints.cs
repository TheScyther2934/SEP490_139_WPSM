using System.Security.Claims;
using Application.Persistence;
using Application.UseCases.Configuration;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// A-02..A-08: service catalog, PriceFrames and policies (read by all roles, changed by Admin).
public static class ConfigurationEndpoints
{
    public static RouteGroupBuilder MapConfigurationEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/catalog", async (IConfigurationRepository repo, CancellationToken ct) => Results.Ok(await repo.GetCatalogAsync(ct)))
            .RequireAuthorization();
        api.MapPost("/catalog", async (CatalogItemRequest request, AddCatalogItem useCase, CancellationToken ct) =>
            Results.Ok(await useCase.HandleAsync(new CatalogItemCommand(request.Name, request.Category, request.Unit, request.BasePrice, request.Active, request.RequiresSupplier), ct)))
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        api.MapGet("/price-frames", async (IConfigurationRepository repo, CancellationToken ct) => Results.Ok(await repo.GetPriceFramesAsync(ct)))
            .RequireAuthorization();
        api.MapPut("/price-frames/{id:int}", async (int id, PriceFrameRequest request, UpdatePriceFrame useCase, ClaimsPrincipal user, CancellationToken ct) =>
            await useCase.HandleAsync(id, new PriceFrameCommand(request.MinPrice, request.MaxPrice, request.EffectiveFrom), UserId(user), ct) is { } frame
                ? Results.Ok(frame) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        api.MapGet("/policies", async (IConfigurationRepository repo, CancellationToken ct) => Results.Ok(await repo.GetPoliciesAsync(ct)))
            .RequireAuthorization();
        api.MapPut("/policies", async (PolicyRequest request, UpdateEventPolicies useCase, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await useCase.HandleAsync(new PolicyCommand(request.DepositRatio, request.MaxDecreaseRatio, request.BookingMinDays, request.BookingMaxDays,
                request.LockDays, request.SupplierDeadlineDays, request.SettlementDueDays, request.Hotline, request.EffectiveFrom), UserId(user), ct)))
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        return api;
    }
}
