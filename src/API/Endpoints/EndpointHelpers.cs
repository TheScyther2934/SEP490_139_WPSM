using System.Security.Claims;
using Application.Persistence;
using Infrastructure.Email;

namespace API.Endpoints;

public static class EndpointHelpers
{
    public static bool ShowsPasswordOnScreen(IWebHostEnvironment environment, EmailOptions email) =>
        environment.IsDevelopment() && string.IsNullOrWhiteSpace(email.Host);


    /// <summary>
    /// Serves a stored file after the caller checked access: a photo on Cloudinary is a redirect to a download link valid 5 minutes,
    /// a PDF (or any file on the local disk) is streamed by the API. Shown inline (the browser displays it).
    /// </summary>
    public static async Task<IResult> ServeStoredFile(IFileService files, StoredFileRef file, HttpContext context, CancellationToken ct)
    {
        if (await files.OpenAsync(file.StorageKey, file.ContentType, file.FileName, ct) is not { } content) return Results.NotFound();
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.CacheControl = "private, max-age=240";
        return content.RedirectUrl is { } url ? Results.Redirect(url) : Results.File(content.Content!, content.ContentType, enableRangeProcessing: true);
    }

    public static int UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new InvalidOperationException("Phiên đăng nhập không hợp lệ.");

    public static string Role(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "";
}
