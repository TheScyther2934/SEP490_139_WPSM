using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Application.Auth;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Identity;

public sealed class RefreshTokenService(WpsmDbContext db, JwtOptions options) : IRefreshTokenService
{
    /// <summary>
    /// A token rotated this recently and presented again is a second tab (or a retried request) refreshing at the same moment,
    /// not a stolen copy: it gets a fresh token of the same session instead of ending it.
    /// </summary>
    public static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(30);

    public async Task<IssuedRefreshToken> IssueAsync(int userId, string? ipAddress, string? userAgent = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            UserId = userId,
            DeviceName = DeviceLabel.From(userAgent),
            UserAgent = Cut(userAgent, 300),
            IpAddress = Cut(ipAddress, 45),
            CreatedAt = now,
            LastSeenAt = now,
            AbsoluteExpiresAt = now.Add(options.RefreshAbsoluteLifetime)
        };
        db.UserSessions.Add(session);
        var (token, entity) = NewToken(session, ipAddress, now);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(token, userId, entity.ExpiresAt, session.SessionId);
    }

    public async Task<IssuedRefreshToken?> RotateAsync(string token, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = Hash(token);
        var current = await db.RefreshTokens.Include(x => x.Session).SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        // Tokens from before device sessions have no session: they are no longer accepted (sign in again).
        if (current?.Session is not { } session) return null;
        var now = DateTime.UtcNow;
        if (session.RevokedAt is not null || session.AbsoluteExpiresAt <= now) return null;
        if (current.RevokedAt is not null)
        {
            if (now - current.RevokedAt.Value > ReuseGrace)
            {
                // A used token coming back later means it was copied: end this device's session only.
                End(session, SessionEndReasons.TokenReuse, now);
                await RevokeTokensAsync([session.SessionId], now, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                return null;
            }
        }
        else if (current.ExpiresAt <= now) return null;

        var (next, entity) = NewToken(session, ipAddress, now);
        if (current.RevokedAt is null)
        {
            current.RevokedAt = now;
            current.ReplacedByToken = entity;
        }
        session.LastSeenAt = now;
        if (ipAddress is not null) session.IpAddress = Cut(ipAddress, 45);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(next, session.UserId, entity.ExpiresAt, session.SessionId);
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var hash = Hash(token);
        var current = await db.RefreshTokens.Include(x => x.Session).SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (current is null) return;
        var now = DateTime.UtcNow;
        if (current.Session is { } session)
        {
            End(session, SessionEndReasons.SignOut, now);
            await RevokeTokensAsync([session.SessionId], now, cancellationToken);
        }
        else current.RevokedAt ??= now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(int userId, string reason, Guid? keepSessionId = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var sessions = await db.UserSessions.Where(x => x.UserId == userId && x.RevokedAt == null && x.SessionId != keepSessionId).ToListAsync(cancellationToken);
        foreach (var session in sessions) End(session, reason, now);
        var tokens = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null && (x.SessionId == null || x.SessionId != keepSessionId))
            .ToListAsync(cancellationToken);
        foreach (var item in tokens) item.RevokedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSessionView>> ListSessionsAsync(int userId, Guid? currentSessionId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var idle = options.RefreshIdleLifetime;
        var sessions = await db.UserSessions.AsNoTracking()
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.AbsoluteExpiresAt > now)
            .OrderByDescending(x => x.LastSeenAt).ToListAsync(cancellationToken);
        // A device idle past the refresh window cannot come back without signing in: not listed as signed in.
        return sessions.Where(x => x.LastSeenAt.Add(idle) > now || x.SessionId == currentSessionId)
            .Select(x => new UserSessionView(x.SessionId, x.DeviceName ?? "Thiết bị không rõ", x.IpAddress,
                Utc(x.CreatedAt), Utc(x.LastSeenAt), Utc(x.AbsoluteExpiresAt), x.SessionId == currentSessionId))
            .OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.LastSeenAt).ToList();
    }

    public async Task<bool> RevokeSessionAsync(int userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await db.UserSessions.SingleOrDefaultAsync(x => x.SessionId == sessionId && x.UserId == userId && x.RevokedAt == null, cancellationToken);
        if (session is null) return false;
        var now = DateTime.UtcNow;
        End(session, SessionEndReasons.RemoteSignOut, now);
        await RevokeTokensAsync([sessionId], now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return db.UserSessions.AsNoTracking().AnyAsync(x => x.SessionId == sessionId && x.RevokedAt == null && x.AbsoluteExpiresAt > now, cancellationToken);
    }

    private async Task RevokeTokensAsync(IReadOnlyCollection<Guid> sessionIds, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var item in await db.RefreshTokens.Where(x => x.SessionId != null && sessionIds.Contains(x.SessionId.Value) && x.RevokedAt == null).ToListAsync(cancellationToken))
            item.RevokedAt = now;
    }

    private static void End(UserSession session, string reason, DateTime now)
    {
        if (session.RevokedAt is not null) return;
        session.RevokedAt = now;
        session.RevokedReason = reason;
    }

    private (string Token, RefreshToken Entity) NewToken(UserSession session, string? ipAddress, DateTime now)
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var idle = now.Add(options.RefreshIdleLifetime);
        return (token, new RefreshToken
        {
            UserId = session.UserId,
            Session = session,
            TokenHash = Hash(token),
            ExpiresAt = idle < session.AbsoluteExpiresAt ? idle : session.AbsoluteExpiresAt,
            AbsoluteExpiresAt = session.AbsoluteExpiresAt,
            CreatedAt = now,
            CreatedByIp = Cut(ipAddress, 45)
        });
    }

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string? Cut(string? value, int max) => value is null ? null : value.Length > max ? value[..max] : value;

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
