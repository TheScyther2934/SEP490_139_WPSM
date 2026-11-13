using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

public sealed record CrItemsRequest(IReadOnlyList<CrItemCommand> Items, string? Note = null);
public sealed record CreateCrRequest(string Type, string Reason, IReadOnlyList<CrItemCommand> Items, bool Submit = true);
public sealed record NoteRequest(string? Note);
public sealed record ReasonRequest(string? Reason);
public sealed record RatingsRequest(IReadOnlyList<RatingCommand> Ratings);
public sealed record ComplaintRequest(int BeoVersion, string Description);
public sealed record ResponseRequest(string Response);
public sealed record AllocationRequest(int ResourceId, string ProjectCode, DateTime StartTime, DateTime EndTime, int Quantity, string? Note = null);
public sealed record CatalogStatusRequest(bool Active);
public sealed record PenaltyRulesRequest(IReadOnlyList<PenaltyRuleCommand> Rules, DateOnly? EffectiveFrom = null);

// Report 4 IV.2 endpoints that were missing: G-04, M-01/M-06/M-23, CR, BEO, day-of, payments, feedback, resources, admin config.
// /projects/{id}/... routes pass the GBR-12 filter in WpsmApiEndpoints before reaching here.
public static class OperationsEndpoints
{
    private const long UploadLimit = 12 * 1024 * 1024;

    public static RouteGroupBuilder MapOperationsEndpoints(this RouteGroupBuilder api)
    {
        var manager = api.MapGroup("").RequireAuthorization(policy => policy.RequireRole("Manager"));
        manager.MapGet("/manager/work", async (IManagerWorkRepository repo, ClaimsPrincipal user, CancellationToken ct) => Results.Ok(await repo.GetWorkAsync(UserId(user), ct)));

        MapBeoAndDayOf(api);
        MapFeedback(api);
        MapResources(api);
        MapConfiguration(api);
        return api;
    }

    private static string Role(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "";
    private static IResult OkOrNotFound(object? value) => value is null ? Results.NotFound() : Results.Ok(value);

    private static void MapNotifications(RouteGroupBuilder api)
    {
        var group = api.MapGroup("/notifications").RequireAuthorization(policy => policy.RequireRole("Customer", "Manager"));
        group.MapGet("", async (bool? unread, IOperationsNotificationRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await repo.GetAsync(UserId(user), unread == true, ct)));
        group.MapPost("/{notificationId:int}/read", async (int notificationId, IOperationsNotificationRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.MarkReadAsync(UserId(user), notificationId, ct) ? Results.NoContent() : Results.NotFound());
        group.MapPost("/read-all", async (IOperationsNotificationRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await repo.MarkAllReadAsync(UserId(user), ct);
            return Results.NoContent();
        });
    }

    private static void MapManagerWork(RouteGroupBuilder api)
    {
        var manager = api.MapGroup("").RequireAuthorization(policy => policy.RequireRole("Manager"));
        manager.MapGet("/manager/work", async (IManagerWorkRepository repo, ClaimsPrincipal user, CancellationToken ct) => Results.Ok(await repo.GetWorkAsync(UserId(user), ct)));
        manager.MapGet("/manager/overdue-alerts", async (IManagerWorkRepository repo, ClaimsPrincipal user, CancellationToken ct) => Results.Ok(await repo.GetOverdueAsync(UserId(user), ct)));
        // /projects/{id}/roadmap/tasks routes are handled by ManagerRoadmapEndpoints
    }

    private static void MapChangeRequests(RouteGroupBuilder api)
    {
        var crs = api.MapGroup("/projects/{id}/change-requests").RequireAuthorization(policy => policy.RequireRole("Customer", "Manager"));
        crs.MapGet("", async (string id, string? status, IOperationsChangeRequestRepository repo, CancellationToken ct) => Results.Ok(await repo.ListAsync(id, status, ct)));
        crs.MapGet("/spec-items", async (string id, IOperationsChangeRequestRepository repo, CancellationToken ct) => Results.Ok(await repo.GetSpecItemsAsync(id, ct)));
        crs.MapPost("/preview", async (string id, CrItemsRequest request, IOperationsChangeRequestRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.PreviewAsync(id, request.Items, ct)));
        crs.MapPost("", async (string id, CreateCrRequest request, IOperationsChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var created = await repo.CreateAsync(id, UserId(user), Role(user), new CreateCrCommand(request.Type, request.Reason, request.Items, request.Submit), ct);
            return Results.Created($"/api/wpsm/projects/{id}/change-requests/{created.Code}", created);
        });
        crs.MapGet("/{crCode}", async (string id, string crCode, IOperationsChangeRequestRepository repo, CancellationToken ct) => OkOrNotFound(await repo.GetAsync(id, crCode, ct)));
        crs.MapPost("/{crCode}/submit", async (string id, string crCode, IOperationsChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.SubmitAsync(id, crCode, UserId(user), Role(user), ct)));
        crs.MapPut("/{crCode}/items", async (string id, string crCode, CrItemsRequest request, IOperationsChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.CounterProposeAsync(id, crCode, UserId(user), Role(user), request.Items, request.Note, ct)));
        crs.MapPost("/{crCode}/approve", async (string id, string crCode, IOperationsChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.ApproveAsync(id, crCode, UserId(user), Role(user), ct)));
        crs.MapPost("/{crCode}/reject", async (string id, string crCode, ReasonRequest request, IOperationsChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.RejectAsync(id, crCode, UserId(user), Role(user), request.Reason, ct)));
        crs.MapPost("/{crCode}/comments", async (string id, string crCode, NoteRequest request, IOperationsChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.CommentAsync(id, crCode, UserId(user), Role(user), request.Note ?? "", ct)));
        crs.MapPost("/{crCode}/exception-requests", async (string id, string crCode, NoteRequest request, IOperationsChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.RequestExceptionAsync(id, crCode, UserId(user), request.Note ?? "", ct)))
            .RequireAuthorization(policy => policy.RequireRole("Manager"));
        crs.MapGet("/{crCode}/addendum", async (string id, string crCode, IOperationsChangeRequestRepository repo, CancellationToken ct) => OkOrNotFound(await repo.GetAddendumAsync(id, crCode, ct)));
        crs.MapPost("/{crCode}/addendum/signed-copy", async (string id, string crCode, IFormFile file, IOperationsChangeRequestRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (file.Length is <= 0 or > 10 * 1024 * 1024) return Results.BadRequest(new { message = "Chỉ chấp nhận tệp JPG, PNG hoặc PDF, tối đa 10 MB (MSG17)." });
            return OkOrNotFound(await repo.AttachSignedAsync(id, crCode, UserId(user), await ReadAsync(file, ct), file.FileName, ct));
        }).RequireAuthorization(policy => policy.RequireRole("Manager")).DisableAntiforgery();
    }

    private static void MapBeoAndDayOf(RouteGroupBuilder api)
    {
        var manager = api.MapGroup("/projects/{id}").RequireAuthorization(policy => policy.RequireRole("Manager"));
        manager.MapGet("/beo", async (string id, IOperationsBeoRepository repo, CancellationToken ct) => OkOrNotFound(await repo.GetAsync(id, ct)));
        manager.MapPut("/beo/notes", async (string id, NoteRequest request, IOperationsBeoRepository repo, CancellationToken ct) => OkOrNotFound(await repo.SaveNotesAsync(id, request.Note, ct)));
        manager.MapPost("/beo/freeze", async (string id, IOperationsBeoRepository repo, ClaimsPrincipal user, CancellationToken ct) => OkOrNotFound(await repo.FreezeAsync(id, UserId(user), ct)));
        manager.MapPost("/beo/reapprove", async (string id, IOperationsBeoRepository repo, ClaimsPrincipal user, CancellationToken ct) => OkOrNotFound(await repo.ReapproveAsync(id, UserId(user), ct)));

        manager.MapGet("/day-of", async (string id, IDayOfRepository repo, PhotoLinks links, CancellationToken ct) => OkOrNotFound(WithUrls(await repo.GetBoardAsync(id, ct), links)));
        manager.MapPost("/day-of/load", async (string id, IDayOfRepository repo, PhotoLinks links, CancellationToken ct) => OkOrNotFound(WithUrls(await repo.LoadTimelineAsync(id, ct), links)));
        manager.MapPost("/day-of/{taskId:int}/complete", async (string id, int taskId, HttpRequest request, IDayOfRepository repo, PhotoLinks links, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (request.ContentLength > 3 * UploadLimit) return Results.BadRequest(new { message = "Vui lòng tải lên 1–3 ảnh để hoàn thành công việc này (MSG10)." });
            var form = request.HasFormContentType ? await request.ReadFormAsync(ct) : null;
            var photos = new List<ProofPhotoUpload>();
            foreach (var file in form?.Files ?? (IReadOnlyList<IFormFile>)[])
            {
                if (file.Length is <= 0 or > 10 * 1024 * 1024) return Results.BadRequest(new { message = "Ảnh minh chứng chỉ nhận JPG hoặc PNG, tối đa 10 MB (MSG17)." });
                photos.Add(new ProofPhotoUpload(await ReadAsync(file, ct), file.FileName));
            }
            return OkOrNotFound(WithUrls(await repo.CompleteTaskAsync(id, taskId, UserId(user), photos, ct), links));
        }).DisableAntiforgery();
    }

    private static void MapPayments(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id}/payments", async (string id, IPaymentRepository repo, CancellationToken ct) => OkOrNotFound(await repo.GetAsync(id, ct)))
            .RequireAuthorization(policy => policy.RequireRole("Customer", "Manager"));
        api.MapPost("/projects/{id}/payments", async (string id, [FromForm] string type, [FromForm] decimal amount, [FromForm] string method, [FromForm] DateOnly paidDate,
            IFormFile? receipt, IPaymentRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (receipt is { Length: > 10 * 1024 * 1024 }) return Results.BadRequest(new { message = "Chỉ chấp nhận tệp JPG, PNG hoặc PDF, tối đa 10 MB (MSG17)." });
            var bytes = receipt is { Length: > 0 } ? await ReadAsync(receipt, ct) : null;
            return OkOrNotFound(await repo.RecordAsync(id, UserId(user), new OpRecordPaymentCommand(type, amount, method, paidDate, bytes, receipt?.FileName), ct));
        }).RequireAuthorization(policy => policy.RequireRole("Manager")).DisableAntiforgery();
        api.MapPost("/projects/{id}/payments/settlement-receipt", async (string id, IPaymentRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            OkOrNotFound(await repo.IssueSettlementReceiptAsync(id, UserId(user), ct)))
            .RequireAuthorization(policy => policy.RequireRole("Manager"));
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
        admin.MapPut("/policies/penalty-rules", async (PenaltyRulesRequest request, IConfigurationAdminRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await repo.UpdatePenaltyRulesAsync(request.Rules ?? [], request.EffectiveFrom, UserId(user), ct)));
        api.MapGet("/policies/penalty-rules", async (IConfigurationAdminRepository repo, CancellationToken ct) => Results.Ok(await repo.GetPenaltyRulesAsync(ct)))
            .RequireAuthorization();
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
