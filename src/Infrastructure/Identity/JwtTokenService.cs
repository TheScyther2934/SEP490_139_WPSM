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

    public static SymmetricSecurityKey SigningKey(string secret) => new(Encoding.UTF8.GetBytes(secret));

    public AccessToken CreateAccessToken(AccountView account)
    {
        var expires = DateTime.UtcNow.Add(options.AccessLifetime);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtOptions.Issuer,
            Audience = JwtOptions.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(SigningKey(options.Secret), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, account.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, account.Email),
                new Claim(JwtRegisteredClaimNames.Name, account.FullName),
                new Claim("role", account.Role),
                new Claim(MustChangePasswordClaim, account.MustChangePassword ? "true" : "false"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ])
        });
        return new AccessToken(token, expires);
    }
}
