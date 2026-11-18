using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Persistence.Entities;

namespace API.Endpoints;

public static class EndpointHelpers
{
    public static bool ShowsPasswordOnScreen(IWebHostEnvironment environment, EmailOptions email) =>
        environment.IsDevelopment() && string.IsNullOrWhiteSpace(email.Host);

    public static PasswordVerificationResult VerifyPassword(IPasswordHasher<User> hasher, AccountView account, string password)
    {
        try
        {
            return hasher.VerifyHashedPassword(new User { UserId = account.UserId, Email = account.Email, Role = account.Role }, account.PasswordHash, password);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

    /// <summary>
    /// Serves a proof photo. ImageFilePath is either an absolute path (older uploads), a storage key under Files:RootPath
    /// (M-17 DayOfTaskService uploads through IFileStorage), or a path relative to the content root (legacy).
    /// </summary>
    public static IResult PhotoFile(string filePath, IWebHostEnvironment environment, Infrastructure.Files.FileStorageOptions storage)
    {
        var resolved = ResolvePhotoPath(filePath, environment, storage);
        if (resolved is null) return Results.NotFound();
        filePath = resolved;
        var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
        return Results.File(filePath, contentType, enableRangeProcessing: true);
    }

    private static string? ResolvePhotoPath(string filePath, IWebHostEnvironment environment, Infrastructure.Files.FileStorageOptions storage)
    {
        if (Path.IsPathRooted(filePath)) return File.Exists(filePath) ? filePath : null;
        // Storage keys stay inside the storage root (same guard as LocalDiskFileStorage).
        var root = Path.GetFullPath(storage.RootPath);
        var underStorage = Path.GetFullPath(Path.Combine(root, filePath));
        if (underStorage.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(underStorage))
            return underStorage;
        var underContent = Path.Combine(environment.ContentRootPath, filePath);
        return File.Exists(underContent) ? underContent : null;
    }

    public static int UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new InvalidOperationException("Phiên đăng nhập không hợp lệ.");

    public static string Role(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "";
}
