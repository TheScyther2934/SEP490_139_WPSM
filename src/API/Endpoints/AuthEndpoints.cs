using System.Security.Claims;
using Application.Auth;
using Application.UseCases.Accounts;
using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// Login, device sessions with rotating refresh tokens, and own profile for the React client.
// One account may be signed in on several devices; each sign-in is a session that can be signed out on its own.
public static class AuthEndpoints
{
    public const string SignInRateLimit = "sign-in";
    private const string RefreshCookie = "WPSM.Refresh";
    private const string RefreshCookiePath = "/api/wpsm/auth/token";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/token", async (LoginRequest request, IAccountProfileRepository repo, IPasswordService passwords,
            ITokenService tokens, IRefreshTokenService refreshTokens, IAuditLogRepository audit, HttpContext context, CancellationToken ct) =>
        {
            var account = await repo.FindAccountAsync(request.Email ?? "", ct);
            // GBR-11: one generic answer whether the email, the password or the account status is wrong.
            if (account is null || account.Status != "Active" || !passwords.Verify(account.PasswordHash, request.Password ?? ""))
                return Results.Json(new { message = "Email hoặc mật khẩu không đúng. Vui lòng kiểm tra lại." }, statusCode: StatusCodes.Status401Unauthorized);
            await audit.LogLoginAsync(new LoginAuditCommand(account.UserId, account.FullName, account.Email, account.Role, ClientIp(context)), ct);
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
                if (rotated is not null) await refreshTokens.RevokeAllAsync(rotated.UserId, SessionEndReasons.AccountLocked, cancellationToken: ct);
                context.Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = RefreshCookiePath });
                return Results.Json(new { message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." }, statusCode: StatusCodes.Status401Unauthorized);
            }
            SetRefreshCookie(context, rotated);
            return Results.Ok(TokenBody(account, tokens.CreateAccessToken(account, rotated.SessionId)));
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
        api.MapPut("/profile", async (ProfileRequest request, UpdateProfile useCase, ClaimsPrincipal user, CancellationToken ct) =>
            await useCase.HandleAsync(UserId(user), new ProfileCommand(request.FullName, request.Phone), ct) is { } profile
                ? Results.Ok(profile) : Results.BadRequest())
            .RequireAuthorization();

        api.MapPost("/auth/change-password", async (ChangePasswordRequest request, IAccountProfileRepository repo, ChangePassword changePassword,
            ITokenService tokens, IRefreshTokenService refreshTokens, SessionGuard guard, ClaimsPrincipal user, HttpContext context, CancellationToken ct) =>
        {
            // The rules (length, confirmation, different from the current one, current one correct) are in the use case.
            if (!await changePassword.HandleAsync(UserId(user), request.CurrentPassword, request.NewPassword, request.ConfirmPassword, ct))
                return Results.Unauthorized();
            var refreshed = await repo.FindAccountByIdAsync(UserId(user), ct);
            if (refreshed is null) return Results.Unauthorized();
            // A new password signs out every other device; this one keeps its session (a new access token clears "must change").
            var current = SessionGuard.SessionOf(user);
            await refreshTokens.RevokeAllAsync(refreshed.UserId, SessionEndReasons.PasswordChanged, current, ct);
            guard.ForgetAll();
            return current is { } sid
                ? Results.Ok(TokenBody(refreshed, tokens.CreateAccessToken(refreshed, sid)))
                : await IssueTokensAsync(refreshed, tokens, refreshTokens, context, ct);
        }).RequireAuthorization();

        // Signed-in devices of the current account (profile page): list, sign out one, sign out all the others.
        api.MapGet("/auth/sessions", async (IRefreshTokenService refreshTokens, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await refreshTokens.ListSessionsAsync(UserId(user), SessionGuard.SessionOf(user), ct)))
            .RequireAuthorization();
        api.MapDelete("/auth/sessions/{sessionId:guid}", async (Guid sessionId, IRefreshTokenService refreshTokens, SessionGuard guard, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!await refreshTokens.RevokeSessionAsync(UserId(user), sessionId, ct))
                return Results.NotFound(new { message = "Phiên này đã kết thúc hoặc không thuộc tài khoản của bạn." });
            guard.Forget(sessionId);
            return Results.NoContent();
        }).RequireAuthorization();
        api.MapPost("/auth/sessions/revoke-others", async (IRefreshTokenService refreshTokens, SessionGuard guard, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (SessionGuard.SessionOf(user) is not { } current) return Results.BadRequest(new { message = "Hãy đăng nhập lại để quản lý thiết bị." });
            await refreshTokens.RevokeAllAsync(UserId(user), SessionEndReasons.RemoteSignOut, current, ct);
            guard.ForgetAll();
            return Results.NoContent();
        }).RequireAuthorization();

        return api;
    }

    private static async Task<IResult> IssueTokensAsync(AccountView account, ITokenService tokens, IRefreshTokenService refreshTokens, HttpContext context, CancellationToken ct)
    {
        var issued = await refreshTokens.IssueAsync(account.UserId, ClientIp(context), context.Request.Headers.UserAgent.ToString(), ct);
        SetRefreshCookie(context, issued);
        return Results.Ok(TokenBody(account, tokens.CreateAccessToken(account, issued.SessionId)));
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
