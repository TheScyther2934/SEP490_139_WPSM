using Application.Persistence;

namespace Application.Auth;

public sealed record AccessToken(string Token, DateTime ExpiresAt);

/// <summary>Issues the short-lived JWT access token (SDS III.1.1: 15 minutes, HS256).</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(AccountView account);
}
