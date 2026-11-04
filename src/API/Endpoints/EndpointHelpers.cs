using System.Security.Claims;
using System.Security.Cryptography;
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

    public static IResult PhotoFile(string filePath, IWebHostEnvironment environment)
    {
        if (!Path.IsPathRooted(filePath)) filePath = Path.Combine(environment.ContentRootPath, filePath);
        if (!File.Exists(filePath)) return Results.NotFound();
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

    public static string PasswordVersion(string passwordHash) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(passwordHash)));

    public static string Role(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role) ?? "";

    public static int UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new InvalidOperationException("Phiên đăng nhập không hợp lệ.");
}
