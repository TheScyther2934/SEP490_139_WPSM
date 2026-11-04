namespace Infrastructure.Identity;

// Lifetimes follow SDS III.1.1. Secret comes from User Secrets or the environment (Jwt:Secret).
public sealed class JwtOptions
{
    public const string Issuer = "wpsm-api";
    public const string Audience = "wpsm-client";

    public required string Secret { get; init; }
    public TimeSpan AccessLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshIdleLifetime { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan RefreshAbsoluteLifetime { get; init; } = TimeSpan.FromHours(8);
}
