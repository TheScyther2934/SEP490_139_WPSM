using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Application.Auth;
using Application.Persistence;

namespace Infrastructure.Identity;

// Short claim names keep the token readable by the React client (checkRole reads "role").
public sealed class JwtTokenService(JwtOptions options) : ITokenService
{
    public const string MustChangePasswordClaim = "mcp";
    public const string SessionClaim = "sid";

    public static SymmetricSecurityKey SigningKey(string secret) => new(Encoding.UTF8.GetBytes(secret));

    public AccessToken CreateAccessToken(AccountView account, Guid? sessionId = null)
    {
        var expires = DateTime.UtcNow.Add(options.AccessLifetime);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, account.Email),
            new(JwtRegisteredClaimNames.Name, account.FullName),
            new("role", account.Role),
            new(MustChangePasswordClaim, account.MustChangePassword ? "true" : "false"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (sessionId is { } sid) claims.Add(new Claim(SessionClaim, sid.ToString("N")));
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtOptions.Issuer,
            Audience = JwtOptions.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(SigningKey(options.Secret), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(claims)
        });
        return new AccessToken(token, expires);
    }
}
