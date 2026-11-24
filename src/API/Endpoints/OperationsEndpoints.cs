using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Application.Persistence;
using Application.UseCases.Configuration;
using Application.UseCases.Resources;
using Application.UseCases.CoordinationTasks;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

public sealed record ReasonRequest(string? Reason);
public sealed record AllocationRequest(int ResourceId, string ProjectCode, DateTime StartTime, DateTime EndTime, int Quantity, string? Note = null);
public sealed record CatalogStatusRequest(bool Active);
public sealed record CoordinationTaskRequest(string? TaskName, string? Department, DateTime Deadline, bool PhotoRequired, string? Note);

// Day-of, feedback, resources and admin catalog configuration.
// Notifications, Manager dashboard/overdue alerts and payments have their own endpoint classes (one route, one handler).
// /projects/{id}/... routes pass the GBR-12 filter in WpsmApiEndpoints before reaching here.
public static class OperationsEndpoints
{
    private const long UploadLimit = 12 * 1024 * 1024;

    public static RouteGroupBuilder MapOperationsEndpoints(this RouteGroupBuilder api)
    {
        MapBeoAndDayOf(api);
        MapProofPhotos(api);
        MapResources(api);
        MapConfiguration(api);
        return api;
    }

    private static string Role(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "";
    private static IResult OkOrNotFound(object? value) => value is null ? Results.NotFound() : Results.Ok(value);

    private static void MapBeoAndDayOf(RouteGroupBuilder api)
    {
        var manager = api.MapGroup("/projects/{id}").RequireAuthorization(policy => policy.RequireRole("Manager"));
        // Keeping them here causes AmbiguousMatchException in ASP.NET Core routing.

        manager.MapGet("/day-of", async (string id, IDayOfRepository repo, PhotoLinks links, CancellationToken ct) => OkOrNotFound(WithUrls(await repo.GetBoardAsync(id, ct), links)));
        manager.MapPost("/day-of/load", async (string id, GenerateCoordinationTasks generate, PhotoLinks links, CancellationToken ct) => OkOrNotFound(WithUrls(await generate.HandleAsync(id, ct), links)));
        // Coordination tasks by department, open from the lock: added by hand, edited (deadline, proof required), deleted.
        manager.MapPost("/day-of/tasks", async (string id, CoordinationTaskRequest request, AddCoordinationTask add, PhotoLinks links, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(WithUrls(await add.HandleAsync(id, UserId(user), ToCommand(request), ct), links)));
        manager.MapPut("/day-of/{taskId:int}", async (string id, int taskId, CoordinationTaskRequest request, EditCoordinationTask edit, PhotoLinks links, CancellationToken ct) =>
            OkOrNotFound(WithUrls(await edit.HandleAsync(id, taskId, ToCommand(request), ct), links)));
        manager.MapDelete("/day-of/{taskId:int}", async (string id, int taskId, DeleteCoordinationTask delete, PhotoLinks links, CancellationToken ct) =>
            OkOrNotFound(WithUrls(await delete.HandleAsync(id, taskId, ct), links)));
        manager.MapPost("/day-of/{taskId:int}/complete", async (string id, int taskId, HttpRequest request, CompleteCoordinationTask complete, PhotoLinks links, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var (photos, note, error) = await ReadProofPhotosAsync(request, ct);
            return error ?? OkOrNotFound(WithUrls(await complete.HandleAsync(id, taskId, UserId(user), photos, note, ct), links));
        }).DisableAntiforgery();
        // Adds proof to a task afterwards (a task completed without it, or more angles of the same work).
        manager.MapPost("/day-of/{taskId:int}/photos", async (string id, int taskId, HttpRequest request, AddCoordinationProof addProof, PhotoLinks links, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var (photos, _, error) = await ReadProofPhotosAsync(request, ct);
            return error ?? OkOrNotFound(WithUrls(await addProof.HandleAsync(id, taskId, UserId(user), photos, ct), links));
        }).DisableAntiforgery();
    }

    private static CoordinationTaskCommand ToCommand(CoordinationTaskRequest x) =>
        new(x.TaskName ?? "", x.Department ?? "", x.Deadline, x.PhotoRequired, x.Note);

    // Proof files (photos or PDF documents) plus an optional note from a multipart form.
    private static async Task<(List<ProofPhotoUpload> Photos, string? Note, IResult? Error)> ReadProofPhotosAsync(HttpRequest request, CancellationToken ct)
    {
        var photos = new List<ProofPhotoUpload>();
        if (request.ContentLength > 3 * UploadLimit) return (photos, null, Results.BadRequest(new { message = "Mỗi lần tải lên 1–3 tệp, tối đa 10 MB mỗi tệp." }));
        var form = request.HasFormContentType ? await request.ReadFormAsync(ct) : null;
        foreach (var file in form?.Files ?? (IReadOnlyList<IFormFile>)[])
        {
            if (file.Length is <= 0 or > 10 * 1024 * 1024) return (photos, null, Results.BadRequest(new { message = "Minh chứng chỉ nhận ảnh JPG, PNG hoặc tài liệu PDF, tối đa 10 MB." }));
            photos.Add(new ProofPhotoUpload(await ReadAsync(file, ct), file.FileName));
        }
        var note = form?["note"].ToString();
        return (photos, string.IsNullOrWhiteSpace(note) ? null : note, null);
    }

    // Proof photos for the customer and the operator (ratings and complaints: CustomerEndpoints, ProjectEndpoints).
    private static void MapProofPhotos(RouteGroupBuilder api)
    {
        var both = api.MapGroup("/projects/{id}").RequireAuthorization(policy => policy.RequireRole("Customer", "Manager"));
        both.MapGet("/proof-photos", async (string id, IDayOfRepository repo, PhotoLinks links, CancellationToken ct) =>
            Results.Ok((await repo.GetPhotosAsync(id, ct)).Select(p => p with { ImageUrl = links.Url(p.PhotoId) })));
    }

    private static void MapResources(RouteGroupBuilder api)
    {
        var manager = api.MapGroup("").RequireAuthorization(policy => policy.RequireRole("Manager"));
        manager.MapGet("/resources/calendar", async (DateOnly? from, DateOnly? to, int? hallId, string? type, IResourceRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.Today);
            return Results.Ok(await repo.GetCalendarAsync(UserId(user), start, to ?? start.AddDays(6), hallId, type, ct));
        });
        manager.MapPost("/resource-allocations", async (AllocationRequest request, AllocateResource useCase, ClaimsPrincipal user, CancellationToken ct) =>
            AllocationResult(await useCase.HandleAsync(UserId(user), ToCommand(request), ct)));
        manager.MapPut("/resource-allocations/{allocationId:int}", async (int allocationId, AllocationRequest request, UpdateResourceAllocation useCase, ClaimsPrincipal user, CancellationToken ct) =>
            AllocationResult(await useCase.HandleAsync(UserId(user), allocationId, ToCommand(request), ct)));
        manager.MapDelete("/resource-allocations/{allocationId:int}", async (int allocationId, ReleaseResourceAllocation useCase, ClaimsPrincipal user, CancellationToken ct) =>
            await useCase.HandleAsync(UserId(user), allocationId, ct) ? Results.NoContent() : Results.NotFound());
        manager.MapGet("/resource-allocations/{allocationId:int}/conflict", async (int allocationId, IResourceRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.GetConflictAsync(UserId(user), allocationId, ct)));
        manager.MapPost("/resource-allocations/exception-requests", async (AllocationRequest request, RequestResourceException useCase, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await useCase.HandleAsync(UserId(user), ToCommand(request), request.Note, ct)));
    }

    private static void MapConfiguration(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("").RequireAuthorization(policy => policy.RequireRole("Admin"));
        admin.MapPut("/catalog/{itemId:int}", async (int itemId, CatalogItemRequest request, UpdateCatalogItem useCase, CancellationToken ct) =>
            OkOrNotFound(await useCase.HandleAsync(itemId, new CatalogItemCommand(request.Name, request.Category, request.Unit, request.BasePrice, request.Active), ct)));
        admin.MapPost("/catalog/{itemId:int}/status", async (int itemId, CatalogStatusRequest request, SetCatalogItemStatus useCase, CancellationToken ct) =>
            OkOrNotFound(await useCase.HandleAsync(itemId, request.Active, ct)));
        admin.MapGet("/price-frames/history/{itemId:int}", async (int itemId, IConfigurationAdminRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetPriceFrameHistoryAsync(itemId, ct)));
    }

    // A conflict is not an error of the request but of the schedule: 409 with the clashing allocations (MSG08).
    private static IResult AllocationResult(AllocationResult? result) => result switch
    {
        null => Results.NotFound(),
        { Allocation: null } => Results.Json(result, statusCode: StatusCodes.Status409Conflict),
        _ => Results.Ok(result)
    };

    private static AllocationCommand ToCommand(AllocationRequest x) => new(x.ResourceId, x.ProjectCode ?? "", x.StartTime, x.EndTime, x.Quantity);

    private static DayOfBoardView? WithUrls(DayOfBoardView? board, PhotoLinks links) => board is null ? null : board with
    {
        Tasks = board.Tasks.Select(t => t with { Photos = t.Photos.Select(p => p with { ImageUrl = links.Url(p.PhotoId) }).ToList() }).ToList()
    };

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken ct)
    {
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
