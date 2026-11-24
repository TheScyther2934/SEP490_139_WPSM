using System.Security.Claims;
using Application.Persistence;
using Application.UseCases.EventStages;
using Application.UseCases.AddonContracts;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// Event stage flow. /projects/{id}/... routes pass the event access filter (operators of the event, Admin) in WpsmApiEndpoints.
//   Operators: workspace (stages, notes), cancel, addon contracts, supplier evidence, customer lookup for addon contracts.
//   Customer:  booked events (read-only), confirm an addon contract.
public static class EventFlowEndpoints
{
    private const long EvidenceLimit = 10 * 1024 * 1024;

    public static RouteGroupBuilder MapEventFlowEndpoints(this RouteGroupBuilder api)
    {
        var staff = api.MapGroup("/projects/{id}").RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));
        var manager = api.MapGroup("/projects/{id}").RequireAuthorization(policy => policy.RequireRole("Manager"));

        staff.MapGet("/workspace", async (string id, IEventFlowRepository repo, CancellationToken ct) =>
            await repo.GetWorkspaceAsync(id, ct) is { } view ? Results.Ok(view) : Results.NotFound());
        staff.MapPost("/notes", async (string id, StageNoteRequest request, AddStageNote useCase, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await useCase.HandleAsync(id, UserId(user), new StageNoteCommand(request.NoteType, request.Content), ct)));
        manager.MapPost("/cancel", async (string id, ReasonRequest request, CancelEvent useCase, ClaimsPrincipal user, CancellationToken ct) =>
            await useCase.HandleAsync(id, UserId(user), request.Reason, ct) is { } view ? Results.Ok(view) : Results.NotFound());

        staff.MapGet("/addons", async (string id, IAddonContractRepository repo, CancellationToken ct) => Results.Ok(await repo.ListAsync(id, ct)));
        // Writes go through use cases (Application/UseCases/AddonContracts); reads through the repository.
        manager.MapPost("/addons", async (string id, AddonRequest request, CreateAddonContract create, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var created = await create.HandleAsync(id, UserId(user), new AddonCreateCommand(request.Title, request.Note, request.Items ?? []), ct);
            return Results.Created($"/api/wpsm/projects/{id}/addons/{created.Code}", created);
        });
        manager.MapPost("/addons/{addonCode}/remind", async (string id, string addonCode, RemindAddonContract remind, CancellationToken ct) =>
            await remind.HandleAsync(id, addonCode, ct) is { } view ? Results.Ok(view) : Results.NotFound());
        manager.MapPost("/addons/{addonCode}/cancel", async (string id, string addonCode, ReasonRequest request, CancelAddonContract cancel, CancellationToken ct) =>
            await cancel.HandleAsync(id, addonCode, request.Reason, ct) is { } view ? Results.Ok(view) : Results.NotFound());

        staff.MapGet("/suppliers", async (string id, ISupplierEvidenceRepository repo, CancellationToken ct) =>
            await repo.GetBoardAsync(id, ct) is { } board ? Results.Ok(board) : Results.NotFound());
        manager.MapPost("/suppliers", async (string id, HttpRequest request, AddSupplierEvidence addEvidence, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest(new { message = "Hãy gửi biểu mẫu kèm tệp minh chứng." });
            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length is <= 0 or > EvidenceLimit) return Results.BadRequest(new { message = "Hãy chọn tệp PDF, JPG hoặc PNG tối đa 10 MB." });
            await using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            int? specItemId = int.TryParse(form["specItemId"], out var parsed) ? parsed : null;
            var evidence = await addEvidence.HandleAsync(id, UserId(user), new SupplierEvidenceCommand(form["supplierName"].ToString(), form["evidenceType"].ToString(),
                specItemId, form["note"].ToString(), buffer.ToArray(), file.FileName, file.ContentType ?? "application/octet-stream"), ct);
            return Results.Ok(evidence);
        }).DisableAntiforgery();
        staff.MapGet("/suppliers/{evidenceId:int}/file", async (string id, int evidenceId, ISupplierEvidenceRepository repo, CancellationToken ct) =>
            await repo.GetFileAsync(id, evidenceId, ct) is { } file ? Results.File(file.Content, file.ContentType, file.FileName) : Results.NotFound());

        // The create form's "addon contract for an existing event" choice: find the customer, then pick one of their locked events.
        api.MapGet("/customers/lookup", async (string? q, IEventFlowRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await repo.SearchCustomersAsync(q, UserId(user), user.FindFirstValue(ClaimTypes.Role) ?? "", ct)))
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        var customer = api.MapGroup("/customer").RequireAuthorization(policy => policy.RequireRole("Customer"));
        customer.MapGet("/events", async (IEventFlowRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await repo.GetCustomerEventsAsync(UserId(user), ct)));
        customer.MapGet("/event", async (string? code, IEventFlowRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetCustomerEventAsync(UserId(user), code, ct) is { } view ? Results.Ok(view) : Results.NotFound());
        customer.MapPost("/addons/{addonCode}/confirm", async (string addonCode, ConfirmAddonContract confirm, ClaimsPrincipal user, CancellationToken ct) =>
            await confirm.HandleAsync(UserId(user), addonCode, ct) is { } view ? Results.Ok(view) : Results.NotFound());
        return api;
    }
}
