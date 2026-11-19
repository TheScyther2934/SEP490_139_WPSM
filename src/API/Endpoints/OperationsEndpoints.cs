using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

public sealed record NoteRequest(string? Note);
public sealed record ReasonRequest(string? Reason);
public sealed record RatingsRequest(IReadOnlyList<RatingCommand> Ratings);
public sealed record ComplaintRequest(int BeoVersion, string Description);
public sealed record ResponseRequest(string Response);
public sealed record AllocationRequest(int ResourceId, string ProjectCode, DateTime StartTime, DateTime EndTime, int Quantity, string? Note = null);
public sealed record CatalogStatusRequest(bool Active);

// Day-of, feedback, resources and admin catalog configuration.
// Notifications, Manager dashboard/overdue alerts and payments have their own endpoint classes (one route, one handler).
// /projects/{id}/... routes pass the GBR-12 filter in WpsmApiEndpoints before reaching here.
public static class OperationsEndpoints
{
    private const long UploadLimit = 12 * 1024 * 1024;

    public static RouteGroupBuilder MapOperationsEndpoints(this RouteGroupBuilder api)
    {
        MapBeoAndDayOf(api);
        MapFeedback(api);
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
        manager.MapPost("/day-of/load", async (string id, IDayOfRepository repo, PhotoLinks links, CancellationToken ct) => OkOrNotFound(WithUrls(await repo.LoadTimelineAsync(id, ct), links)));
        manager.MapPost("/day-of/{taskId:int}/complete", async (string id, int taskId, HttpRequest request, IDayOfRepository repo, PhotoLinks links, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var (photos, error) = await ReadProofPhotosAsync(request, ct);
            return error ?? OkOrNotFound(WithUrls(await repo.CompleteTaskAsync(id, taskId, UserId(user), photos, ct), links));
        }).DisableAntiforgery();
        // Adds proof photos to a task afterwards (a task completed without them, or more angles of the same work).
        manager.MapPost("/day-of/{taskId:int}/photos", async (string id, int taskId, HttpRequest request, IDayOfRepository repo, PhotoLinks links, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var (photos, error) = await ReadProofPhotosAsync(request, ct);
            return error ?? OkOrNotFound(WithUrls(await repo.AddPhotosAsync(id, taskId, UserId(user), photos, ct), links));
        }).DisableAntiforgery();
    }

    private static async Task<(List<ProofPhotoUpload> Photos, IResult? Error)> ReadProofPhotosAsync(HttpRequest request, CancellationToken ct)
    {
        var photos = new List<ProofPhotoUpload>();
        if (request.ContentLength > 3 * UploadLimit) return (photos, Results.BadRequest(new { message = "Mỗi lần tải lên 1–3 ảnh, tối đa 10 MB mỗi ảnh." }));
        var form = request.HasFormContentType ? await request.ReadFormAsync(ct) : null;
        foreach (var file in form?.Files ?? (IReadOnlyList<IFormFile>)[])
        {
            if (file.Length is <= 0 or > 10 * 1024 * 1024) return (photos, Results.BadRequest(new { message = "Ảnh minh chứng chỉ nhận JPG hoặc PNG, tối đa 10 MB." }));
            photos.Add(new ProofPhotoUpload(await ReadAsync(file, ct), file.FileName));
        }
        return (photos, null);
    }

    private static void MapFeedback(RouteGroupBuilder api)
    {
        var both = api.MapGroup("/projects/{id}").RequireAuthorization(policy => policy.RequireRole("Customer", "Manager"));
        both.MapGet("/proof-photos", async (string id, IDayOfRepository repo, PhotoLinks links, CancellationToken ct) =>
            Results.Ok((await repo.GetPhotosAsync(id, ct)).Select(p => p with { ImageUrl = links.Url(p.PhotoId) })));
        both.MapGet("/post-event", async (string id, IFeedbackRepository repo, CancellationToken ct) => OkOrNotFound(await repo.GetPostEventAsync(id, ct)));
        both.MapGet("/complaints", async (string id, IFeedbackRepository repo, CancellationToken ct) => Results.Ok(await repo.ListComplaintsAsync(id, ct)));
        both.MapGet("/complaints/{complaintCode}", async (string id, string complaintCode, IFeedbackRepository repo, PhotoLinks links, CancellationToken ct) =>
            await repo.GetComplaintAsync(id, complaintCode, ct) is { } complaint
                ? Results.Ok(complaint with { Photos = complaint.Photos.Select(p => p with { ImageUrl = links.Url(p.PhotoId) }).ToList() })
                : Results.NotFound());

        var customer = api.MapGroup("/projects/{id}").RequireAuthorization(policy => policy.RequireRole("Customer"));
        customer.MapPost("/ratings", async (string id, RatingsRequest request, IFeedbackRepository repo, CancellationToken ct) =>
            OkOrNotFound(await repo.SubmitRatingsAsync(id, request.Ratings ?? [], ct)));
        customer.MapPost("/complaints", async (string id, ComplaintRequest request, IFeedbackRepository repo, CancellationToken ct) =>
        {
            var created = await repo.FileComplaintAsync(id, request.BeoVersion, request.Description, ct);
            return Results.Created($"/api/wpsm/projects/{id}/complaints/{created.Code}", created);
        });

        var manager = api.MapGroup("/projects/{id}/complaints/{complaintCode}").RequireAuthorization(policy => policy.RequireRole("Manager"));
        manager.MapPost("/resolve", async (string id, string complaintCode, ResponseRequest request, IFeedbackRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.ResolveAsync(id, complaintCode, UserId(user), request.Response, ct)));
        manager.MapPost("/escalate", async (string id, string complaintCode, NoteRequest request, IFeedbackRepository repo, CancellationToken ct) =>
            OkOrNotFound(await repo.EscalateAsync(id, complaintCode, request.Note ?? "", ct)));
    }

    private static void MapResources(RouteGroupBuilder api)
    {
        var manager = api.MapGroup("").RequireAuthorization(policy => policy.RequireRole("Manager"));
        manager.MapGet("/resources/calendar", async (DateOnly? from, DateOnly? to, int? hallId, string? type, IResourceRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.Today);
            return Results.Ok(await repo.GetCalendarAsync(UserId(user), start, to ?? start.AddDays(6), hallId, type, ct));
        });
        manager.MapPost("/resource-allocations", async (AllocationRequest request, IResourceRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            AllocationResult(await repo.AllocateAsync(UserId(user), ToCommand(request), ct)));
        manager.MapPut("/resource-allocations/{allocationId:int}", async (int allocationId, AllocationRequest request, IResourceRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            AllocationResult(await repo.UpdateAsync(UserId(user), allocationId, ToCommand(request), ct)));
        manager.MapDelete("/resource-allocations/{allocationId:int}", async (int allocationId, IResourceRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.ReleaseAsync(UserId(user), allocationId, ct) ? Results.NoContent() : Results.NotFound());
        manager.MapGet("/resource-allocations/{allocationId:int}/conflict", async (int allocationId, IResourceRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.GetConflictAsync(UserId(user), allocationId, ct)));
        manager.MapPost("/resource-allocations/exception-requests", async (AllocationRequest request, IResourceRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.RequestExceptionAsync(UserId(user), ToCommand(request), request.Note ?? "", ct)));
    }

    private static void MapConfiguration(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("").RequireAuthorization(policy => policy.RequireRole("Admin"));
        admin.MapPut("/catalog/{itemId:int}", async (int itemId, CatalogItemRequest request, IConfigurationAdminRepository repo, CancellationToken ct) =>
            OkOrNotFound(await repo.UpdateCatalogItemAsync(itemId, new CatalogItemCommand(request.Name, request.Category, request.Unit, request.BasePrice, request.Active), ct)));
        admin.MapPost("/catalog/{itemId:int}/status", async (int itemId, CatalogStatusRequest request, IConfigurationAdminRepository repo, CancellationToken ct) =>
            OkOrNotFound(await repo.SetCatalogStatusAsync(itemId, request.Active, ct)));
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
