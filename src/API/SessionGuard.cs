using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Application.Auth;
using Infrastructure.Identity;

namespace API;

/// <summary>
/// Checks on every authenticated request that the device session of the access token is still open, so a device signed out
/// from another one (or by a password change) stops working at once instead of when its 15-minute token expires.
/// Answers are cached briefly to spare the database; this API's own revocations clear the cache entry immediately.
/// </summary>
public sealed class SessionGuard(IMemoryCache cache)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    public static Guid? SessionOf(ClaimsPrincipal user) =>
        Guid.TryParseExact(user.FindFirstValue(JwtTokenService.SessionClaim), "N", out var id) ? id : null;

    public async Task<bool> IsActiveAsync(Guid sessionId, IRefreshTokenService sessions, CancellationToken cancellationToken)
    {
        var key = Key(sessionId);
        if (cache.TryGetValue(key, out bool active)) return active;
        active = await sessions.IsSessionActiveAsync(sessionId, cancellationToken);
        cache.Set(key, active, CacheFor);
        return active;
    }

    public void Forget(Guid sessionId) => cache.Remove(Key(sessionId));

    /// <summary>After ending several sessions of a user (password change, account lock): drop every cached answer.</summary>
    public void ForgetAll()
    {
        if (cache is MemoryCache memory) memory.Compact(1.0);
    }

    private static string Key(Guid sessionId) => $"session:{sessionId:N}";
}
