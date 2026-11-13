namespace Application.Auth;

/// <summary>The raw token goes to the client once; only its hash is stored.</summary>
public sealed record IssuedRefreshToken(string Token, int UserId, DateTime ExpiresAt);

/// <summary>Rotating refresh tokens (SDS 3.2): 30-minute idle window, 8-hour absolute limit, reuse revokes the user's tokens.</summary>
public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(int userId, string? ipAddress, CancellationToken cancellationToken = default);
    /// <summary>Returns null when the token is unknown, expired or already used.</summary>
    Task<IssuedRefreshToken?> RotateAsync(string token, string? ipAddress, CancellationToken cancellationToken = default);
    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
    Task RevokeAllAsync(int userId, CancellationToken cancellationToken = default);
}
