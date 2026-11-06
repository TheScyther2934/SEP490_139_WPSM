using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Application.Auth;
using Application.Persistence;
using Infrastructure.Persistence.Entities;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// Login, rotating refresh-token session and own profile for the React client.
public static class AuthEndpoints
{
    public const string SignInRateLimit = "sign-in";
    private const string RefreshCookie = "WPSM.Refresh";
    private const string RefreshCookiePath = "/api/wpsm/auth/token";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/token", async (LoginRequest request, IAccountProfileRepository repo, IPasswordHasher<User> hasher,
            ITokenService tokens, IRefreshTokenService refreshTokens, HttpContext context, CancellationToken ct) =>
        {
            var account = await repo.FindAccountAsync(request.Email ?? "", ct);
            // GBR-11: one generic answer whether the email, the password or the account status is wrong.
            if (account is null || account.Status != "Active" || VerifyPassword(hasher, account, request.Password ?? "") == PasswordVerificationResult.Failed)
                return Results.Json(new { message = "Email hoặc mật khẩu không đúng. Vui lòng kiểm tra lại." }, statusCode: StatusCodes.Status401Unauthorized);
            return await IssueTokensAsync(account, tokens, refreshTokens, context, ct);
        }).RequireRateLimiting(SignInRateLimit);

        api.MapPost("/auth/token/refresh", async (IAccountProfileRepository repo, ITokenService tokens, IRefreshTokenService refreshTokens,
            HttpContext context, CancellationToken ct) =>
        {
            if (!SameOrigin(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var rotated = await refreshTokens.RotateAsync(context.Request.Cookies[RefreshCookie] ?? "", ClientIp(context), ct);
            var account = rotated is null ? null : await repo.FindAccountByIdAsync(rotated.UserId, ct);
            if (rotated is null || account is null || account.Status != "Active")
            {
                if (rotated is not null) await refreshTokens.RevokeAllAsync(rotated.UserId, ct);
                context.Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = RefreshCookiePath });
                return Results.Json(new { message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." }, statusCode: StatusCodes.Status401Unauthorized);
            }
            SetRefreshCookie(context, rotated);
            return Results.Ok(TokenBody(account, tokens.CreateAccessToken(account)));
        });

        api.MapPost("/auth/token/revoke", async (IRefreshTokenService refreshTokens, HttpContext context, CancellationToken ct) =>
        {
            if (!SameOrigin(context)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            await refreshTokens.RevokeAsync(context.Request.Cookies[RefreshCookie] ?? "", ct);
            context.Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = RefreshCookiePath });
            return Results.NoContent();
        });

        api.MapGet("/profile", async (IAccountProfileRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetProfileAsync(UserId(user), ct) is { } profile ? Results.Ok(profile) : Results.NotFound())
            .RequireAuthorization();
        api.MapPut("/profile", async (ProfileRequest request, IAccountProfileRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.UpdateProfileAsync(UserId(user), new ProfileCommand(request.FullName, request.Phone), ct) is { } profile
                ? Results.Ok(profile) : Results.BadRequest())
            .RequireAuthorization();

        api.MapPost("/auth/change-password", async (ChangePasswordRequest request, IAccountProfileRepository repo, IPasswordHasher<User> hasher,
            ITokenService tokens, IRefreshTokenService refreshTokens, ClaimsPrincipal user, HttpContext context, CancellationToken ct) =>
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
            if (refreshed is null) return Results.Unauthorized();
            // A new password ends every refresh-token session; this client continues with a fresh token pair.
            await refreshTokens.RevokeAllAsync(refreshed.UserId, ct);
            return await IssueTokensAsync(refreshed, tokens, refreshTokens, context, ct);
        }).RequireAuthorization();

        return api;
    }

    private static async Task<IResult> IssueTokensAsync(AccountView account, ITokenService tokens, IRefreshTokenService refreshTokens, HttpContext context, CancellationToken ct)
    {
        SetRefreshCookie(context, await refreshTokens.IssueAsync(account.UserId, ClientIp(context), ct));
        return Results.Ok(TokenBody(account, tokens.CreateAccessToken(account)));
    }

    private static object TokenBody(AccountView account, AccessToken token) => new
    {
        accessToken = token.Token,
        expiresAt = token.ExpiresAt,
        user = new { account.UserId, account.Email, account.FullName, account.Role, account.MustChangePassword }
    };

    // HttpOnly so scripts never read it; Strict + a narrow path so only the token endpoints receive it.
    private static void SetRefreshCookie(HttpContext context, IssuedRefreshToken token) =>
        context.Response.Cookies.Append(RefreshCookie, token.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = token.ExpiresAt
        });

    private static bool SameOrigin(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        return string.IsNullOrEmpty(origin) || Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Authority == context.Request.Host.Value;
    }

    private static string? ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString();
}
