using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Application.Persistence;
using Infrastructure.Persistence.Entities;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// G-01, G-02, G-03: login, session and own profile.
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/profile", async (IAccountProfileRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetProfileAsync(UserId(user), ct) is { } profile ? Results.Ok(profile) : Results.NotFound())
            .RequireAuthorization();
        api.MapPut("/profile", async (ProfileRequest request, IAccountProfileRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.UpdateProfileAsync(UserId(user), new ProfileCommand(request.FullName, request.Phone), ct) is { } profile
                ? Results.Ok(profile) : Results.BadRequest())
            .RequireAuthorization();

        api.MapPost("/auth/login", async (LoginRequest request, IAccountProfileRepository repo, IPasswordHasher<User> hasher, HttpContext context, CancellationToken ct) =>
        {
            var account = await repo.FindAccountAsync(request.Email, ct);
            if (account is null || account.Status != "Active" || VerifyPassword(hasher, account, request.Password) == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.UserId.ToString()),
                new Claim(ClaimTypes.Name, account.Email),
                new Claim(ClaimTypes.Role, account.Role),
                new Claim("FullName", account.FullName),
                new Claim("PasswordVersion", PasswordVersion(account.PasswordHash)),
                new Claim("MustChangePassword", account.MustChangePassword.ToString())
            };
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Ok(new { account.UserId, account.Email, account.FullName, account.Role, account.MustChangePassword });
        });

        api.MapPost("/auth/logout", async (HttpContext context) => { await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.NoContent(); })
            .RequireAuthorization();

        api.MapGet("/auth/me", async (IAccountProfileRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var email = user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email)) return Results.Unauthorized();
            var account = await repo.FindAccountAsync(email, ct);
            return account is null || account.Status != "Active" ? Results.Unauthorized() : Results.Ok(new
            {
                authenticated = true,
                user = new { account.FullName, account.Email, Phone = account.Phone ?? "", account.Role },
                mustChangePassword = account.MustChangePassword
            });
        }).RequireAuthorization();

        api.MapPost("/auth/change-password", async (ChangePasswordRequest request, IAccountProfileRepository repo, IPasswordHasher<User> hasher, ClaimsPrincipal user, HttpContext context, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8 || request.NewPassword != request.ConfirmPassword)
                return Results.BadRequest(new { message = "Mật khẩu mới phải dài ít nhất 8 ký tự và hai lần nhập phải trùng nhau." });
            if (request.NewPassword == request.CurrentPassword)
                return Results.BadRequest(new { message = "Mật khẩu mới không được trùng với mật khẩu hiện tại." });
            var account = await repo.FindAccountAsync(user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name) ?? "", ct);
            if (account is null || VerifyPassword(hasher, account, request.CurrentPassword) == PasswordVerificationResult.Failed)
                return Results.BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
            var hash = hasher.HashPassword(new User { UserId = account.UserId, Email = account.Email }, request.NewPassword);
            if (!await repo.UpdatePasswordAsync(account.UserId, hash, ct)) return Results.Unauthorized();
            var refreshed = await repo.FindAccountAsync(account.Email, ct);
            if (refreshed is not null)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, refreshed.UserId.ToString()),
                    new Claim(ClaimTypes.Name, refreshed.Email),
                    new Claim(ClaimTypes.Role, refreshed.Role),
                    new Claim("FullName", refreshed.FullName),
                    new Claim("PasswordVersion", PasswordVersion(refreshed.PasswordHash)),
                    new Claim("MustChangePassword", refreshed.MustChangePassword.ToString())
                };
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            }
            return Results.NoContent();
        }).RequireAuthorization();

        return api;
    }
}
