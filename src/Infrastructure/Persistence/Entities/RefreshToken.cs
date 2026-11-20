namespace Infrastructure.Persistence.Entities;

public class RefreshToken
{
    public int RefreshTokenId { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime AbsoluteExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public int? ReplacedByTokenId { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedByIp { get; set; }

    /// <summary>The device session this token belongs to (null only for tokens issued before sessions existed).</summary>
    public Guid? SessionId { get; set; }

    public virtual UserSession? Session { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual RefreshToken? ReplacedByToken { get; set; }
}
