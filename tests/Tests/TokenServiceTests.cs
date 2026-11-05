using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Application.Persistence;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Xunit;

namespace Tests;

/// <summary>
/// JWT access token and rotating refresh token (SDS 3.2, III.1.1).
/// Uses EF Core In-Memory database.
/// </summary>
public sealed class TokenServiceTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly JwtOptions _options = new() { Secret = new string('k', 64) };
    private readonly RefreshTokenService _refresh;

    public TokenServiceTests()
    {
        _db = new WpsmDbContext(new DbContextOptionsBuilder<WpsmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Users.Add(new User { UserId = 7, Email = "m@test.vn", FullName = "Manager", Role = "Manager", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow });
        _db.SaveChanges();
        _refresh = new RefreshTokenService(_db, _options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Issue_StoresOnlyHash()
    {
        var issued = await _refresh.IssueAsync(7, "127.0.0.1");
        var row = Assert.Single(_db.RefreshTokens);
        Assert.NotEqual(issued.Token, row.TokenHash);
        Assert.Equal(64, row.TokenHash.Length);
        Assert.True(row.ExpiresAt <= row.AbsoluteExpiresAt);
    }

    [Fact]
    public async Task Rotate_ReturnsNewToken_AndRevokesOld()
    {
        var first = await _refresh.IssueAsync(7, null);
        var second = await _refresh.RotateAsync(first.Token, null);
        Assert.NotNull(second);
        Assert.NotEqual(first.Token, second!.Token);
        var old = _db.RefreshTokens.OrderBy(x => x.RefreshTokenId).First();
        Assert.NotNull(old.RevokedAt);
        Assert.NotNull(old.ReplacedByTokenId);
    }

    [Fact]
    public async Task Rotate_ReusedToken_RevokesEverySession()
    {
        var first = await _refresh.IssueAsync(7, null);
        var second = await _refresh.RotateAsync(first.Token, null);
        Assert.Null(await _refresh.RotateAsync(first.Token, null));
        Assert.All(_db.RefreshTokens, x => Assert.NotNull(x.RevokedAt));
        Assert.Null(await _refresh.RotateAsync(second!.Token, null));
    }

    [Fact]
    public async Task Rotate_IdleExpired_ReturnsNull()
    {
        var issued = await _refresh.IssueAsync(7, null);
        var row = _db.RefreshTokens.Single();
        row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();
        Assert.Null(await _refresh.RotateAsync(issued.Token, null));
    }

    [Fact]
    public async Task Rotate_KeepsAbsoluteLimit()
    {
        var issued = await _refresh.IssueAsync(7, null);
        var row = _db.RefreshTokens.Single();
        row.AbsoluteExpiresAt = DateTime.UtcNow.AddMinutes(5);
        row.ExpiresAt = row.AbsoluteExpiresAt;
        await _db.SaveChangesAsync();
        var next = await _refresh.RotateAsync(issued.Token, null);
        Assert.NotNull(next);
        Assert.True(next!.ExpiresAt <= row.AbsoluteExpiresAt);
    }

    [Fact]
    public async Task Revoke_UnknownOrEmptyToken_DoesNothing()
    {
        await _refresh.RevokeAsync("");
        await _refresh.RevokeAsync("not-a-token");
        Assert.Empty(_db.RefreshTokens);
    }

    [Fact]
    public async Task AccessToken_CarriesRoleAndValidatesWithKey()
    {
        var account = new AccountView(7, "m@test.vn", "Manager", null, "Manager", "Active", "x", MustChangePassword: true);
        var token = new JwtTokenService(_options).CreateAccessToken(account);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = JwtOptions.Issuer,
            ValidAudience = JwtOptions.Audience,
            IssuerSigningKey = JwtTokenService.SigningKey(_options.Secret)
        });
        Assert.True(result.IsValid);
        Assert.Equal("Manager", result.Claims["role"]);
        Assert.Equal("7", result.Claims["sub"]);
        Assert.Equal("true", result.Claims[JwtTokenService.MustChangePasswordClaim]);
        Assert.True(token.ExpiresAt <= DateTime.UtcNow.AddMinutes(15).AddSeconds(5));
    }
}
