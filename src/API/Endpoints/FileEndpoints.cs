using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Application.Persistence;
using Infrastructure.Email;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// API-01, API-02 (files) and API-04 (email delivery status webhook).
public static class FileEndpoints
{
    public static RouteGroupBuilder MapFileEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/files", async (IFormFile file, [FromForm] string purpose, [FromForm] string projectCode, IFileService files, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await using var content = file.OpenReadStream();
            return await files.UploadAsync(new FileUploadCommand(content, file.FileName, purpose, projectCode, UserId(user)), ct) is { } stored
                ? Results.Created($"/api/wpsm/files/{stored.Id}", stored) : Results.NotFound();
        }).RequireAuthorization(policy => policy.RequireRole("Manager")).DisableAntiforgery();

        api.MapGet("/files/{id:guid}", async (Guid id, IFileService files, ClaimsPrincipal user, CancellationToken ct) =>
            await files.GetSignedUrlAsync(id, UserId(user), user.FindFirstValue(ClaimTypes.Role), ct) is { } url ? Results.Ok(url) : Results.NotFound())
            .RequireAuthorization();

        api.MapGet("/files/{id:guid}/content", async (Guid id, long expires, string sig, IFileService files, HttpContext context, CancellationToken ct) =>
        {
            if (await files.OpenSignedAsync(id, expires, sig, ct) is not { } file) return Results.StatusCode(StatusCodes.Status403Forbidden);
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(file.Content, file.ContentType, file.OriginalName);
        }).AllowAnonymous();

        // Proof photo behind a signed URL from PhotoLinks; the signature replaces the Bearer token.
        api.MapGet("/photos/{photoId:int}/content", async (int photoId, long expires, string sig, PhotoLinks links, IAdminRepository photos,
            IWebHostEnvironment environment, HttpContext context, CancellationToken ct) =>
        {
            if (!links.Verify(photoId, expires, sig)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (await photos.GetPhotoPathAsync(photoId, ct) is not { } path) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, max-age=300";
            return PhotoFile(path, environment);
        }).AllowAnonymous();

        api.MapPost("/notifications/email-status", async (HttpRequest request, IEmailDeliveryService delivery, CancellationToken ct) =>
        {
            if (!delivery.IsConfigured) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, ct);
            if (buffer.Length > 64 * 1024) return Results.BadRequest();
            var body = buffer.ToArray();
            if (!delivery.VerifySignature(body, request.Headers["X-Wpsm-Signature"])) return Results.Unauthorized();
            var payload = JsonSerializer.Deserialize<EmailStatusRequest>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (string.IsNullOrWhiteSpace(payload?.MessageId) || string.IsNullOrWhiteSpace(payload.Status)) return Results.BadRequest();
            return await delivery.ApplyStatusAsync(payload.MessageId, payload.Status, payload.Error, ct) ? Results.NoContent() : Results.NotFound();
        }).AllowAnonymous();

        return api;
    }
}
