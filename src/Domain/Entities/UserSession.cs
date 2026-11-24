namespace Domain.Entities;

/// <summary>
/// One signed-in device (browser) of a user. Created at sign-in; its refresh tokens rotate inside it.
/// Revoking it signs that device out (refresh fails, access tokens carrying its id are refused) without touching other devices.
/// </summary>
public class UserSession
{
    public Guid SessionId { get; set; }

    public int UserId { get; set; }

    /// <summary>Readable device label from the User-Agent, e.g. "Chrome · Windows".</summary>
    public string? DeviceName { get; set; }

    public string? UserAgent { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Last sign-in or token refresh of this device.</summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>Hard end of the session (sign-in + absolute refresh lifetime).</summary>
    public DateTime AbsoluteExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>SignOut, RemoteSignOut, PasswordChanged, PasswordReset, AccountLocked, TokenReuse.</summary>
    public string? RevokedReason { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
