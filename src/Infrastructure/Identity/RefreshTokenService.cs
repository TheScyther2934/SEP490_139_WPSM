using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Application.Auth;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Identity;

public sealed class RefreshTokenService(WpsmDbContext db, JwtOptions options) : IRefreshTokenService
{
    public async Task<IssuedRefreshToken> IssueAsync(int userId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var (token, entity) = NewToken(userId, ipAddress, now, now.Add(options.RefreshAbsoluteLifetime));
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(token, userId, entity.ExpiresAt);
    }

    public async Task<IssuedRefreshToken?> RotateAsync(string token, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = Hash(token);
        var current = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (current is null) return null;
        var now = DateTime.UtcNow;
        if (current.RevokedAt is not null)
        {
            // A used token coming back means it was copied; end every session of that user.
            await RevokeAllAsync(current.UserId, cancellationToken);
            return null;
        }
        if (current.ExpiresAt <= now || current.AbsoluteExpiresAt <= now) return null;

        var (next, entity) = NewToken(current.UserId, ipAddress, now, current.AbsoluteExpiresAt);
        current.RevokedAt = now;
        current.ReplacedByToken = entity;
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(next, current.UserId, entity.ExpiresAt);
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var hash = Hash(token);
        var current = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAt == null, cancellationToken);
        if (current is null) return;
        current.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllAsync(int userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var active = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var item in active) item.RevokedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private (string Token, RefreshToken Entity) NewToken(int userId, string? ipAddress, DateTime now, DateTime absoluteExpiresAt)
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var idle = now.Add(options.RefreshIdleLifetime);
        return (token, new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(token),
            ExpiresAt = idle < absoluteExpiresAt ? idle : absoluteExpiresAt,
            AbsoluteExpiresAt = absoluteExpiresAt,
            CreatedAt = now,
            CreatedByIp = ipAddress?.Length > 45 ? ipAddress[..45] : ipAddress
        });
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
