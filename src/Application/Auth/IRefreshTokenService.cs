namespace Application.Auth;

/// <summary>The raw token goes to the client once; only its hash is stored. SessionId names the signed-in device.</summary>
public sealed record IssuedRefreshToken(string Token, int UserId, DateTime ExpiresAt, Guid SessionId);

/// <summary>One signed-in device as shown on the profile page.</summary>
public sealed record UserSessionView(Guid Id, string DeviceName, string? IpAddress, DateTime CreatedAt, DateTime LastSeenAt, DateTime ExpiresAt, bool IsCurrent);

/// <summary>Why a session ended (stored in UserSessions.RevokedReason).</summary>
public static class SessionEndReasons
{
    public const string SignOut = "SignOut";
    public const string RemoteSignOut = "RemoteSignOut";
    public const string PasswordChanged = "PasswordChanged";
    public const string PasswordReset = "PasswordReset";
    public const string AccountLocked = "AccountLocked";
    public const string TokenReuse = "TokenReuse";
}

/// <summary>
/// Device sessions with rotating refresh tokens (SDS 3.2): 30-minute idle window, 8-hour absolute limit per session.
/// Every sign-in opens a session, so one account can be used on several devices; each device is signed out on its own.
/// A refresh token used twice ends only its own session, except within a short grace window (two tabs refreshing together).
/// </summary>
public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(int userId, string? ipAddress, string? userAgent = null, CancellationToken cancellationToken = default);
    /// <summary>Returns null when the token is unknown, expired, already used or its session has ended.</summary>
    Task<IssuedRefreshToken?> RotateAsync(string token, string? ipAddress, CancellationToken cancellationToken = default);
    /// <summary>Sign-out: ends the session of this token.</summary>
    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
    /// <summary>Ends every session of the user, except <paramref name="keepSessionId"/> when given.</summary>
    Task RevokeAllAsync(int userId, string reason, Guid? keepSessionId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserSessionView>> ListSessionsAsync(int userId, Guid? currentSessionId, CancellationToken cancellationToken = default);
    /// <summary>Ends one session of the user; false when it is not theirs or already ended.</summary>
    Task<bool> RevokeSessionAsync(int userId, Guid sessionId, CancellationToken cancellationToken = default);
    /// <summary>True while the session exists, is not revoked and has not reached its absolute end.</summary>
    Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
