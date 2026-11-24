using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Application.Persistence;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Domain.Entities;
using Xunit;

namespace Tests;

/// <summary>
/// JWT access token, device sessions and rotating refresh tokens (SDS 3.2, III.1.1).
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
    public async Task Rotate_ReusedAfterGrace_EndsOnlyThatDevice()
    {
        var laptop = await _refresh.IssueAsync(7, null, "Mozilla/5.0 (Windows NT 10.0) Chrome/120.0 Safari/537.36");
        var phone = await _refresh.IssueAsync(7, null, "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0) Safari/604.1");
        var second = await _refresh.RotateAsync(laptop.Token, null);
        // The copied laptop token comes back after the grace window.
        _db.RefreshTokens.Single(x => x.TokenHash != null && x.SessionId == laptop.SessionId && x.ReplacedByTokenId != null).RevokedAt = DateTime.UtcNow.AddMinutes(-5);
        await _db.SaveChangesAsync();
        Assert.Null(await _refresh.RotateAsync(laptop.Token, null));
        Assert.Null(await _refresh.RotateAsync(second!.Token, null));
        Assert.Equal(Application.Auth.SessionEndReasons.TokenReuse, _db.UserSessions.Single(x => x.SessionId == laptop.SessionId).RevokedReason);
        // The phone is another session: still signed in.
        Assert.NotNull(await _refresh.RotateAsync(phone.Token, null));
    }

    [Fact]
    public async Task Rotate_ReusedWithinGrace_TwoTabsKeepTheSession()
    {
        var first = await _refresh.IssueAsync(7, null);
        var tabA = await _refresh.RotateAsync(first.Token, null);
        var tabB = await _refresh.RotateAsync(first.Token, null);
        Assert.NotNull(tabA);
        Assert.NotNull(tabB);
        Assert.Equal(first.SessionId, tabB!.SessionId);
        Assert.Null(_db.UserSessions.Single().RevokedAt);
        Assert.NotNull(await _refresh.RotateAsync(tabB.Token, null));
    }

    [Fact]
    public async Task SignIn_TwiceOpensTwoSessions_AndListsThem()
    {
        var laptop = await _refresh.IssueAsync(7, "10.0.0.1", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36 Edg/120.0");
        var phone = await _refresh.IssueAsync(7, "10.0.0.2", "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/120.0 Mobile Safari/537.36");
        Assert.NotEqual(laptop.SessionId, phone.SessionId);
        var list = await _refresh.ListSessionsAsync(7, phone.SessionId);
        Assert.Equal(2, list.Count);
        Assert.True(list[0].IsCurrent);
        Assert.Equal("Chrome · Điện thoại Android", list[0].DeviceName);
        Assert.Equal("Edge · Windows", list[1].DeviceName);
    }

    [Fact]
    public async Task RevokeSession_SignsOutThatDeviceOnly()
    {
        var laptop = await _refresh.IssueAsync(7, null);
        var phone = await _refresh.IssueAsync(7, null);
        Assert.True(await _refresh.RevokeSessionAsync(7, laptop.SessionId));
        Assert.False(await _refresh.IsSessionActiveAsync(laptop.SessionId));
        Assert.Null(await _refresh.RotateAsync(laptop.Token, null));
        Assert.True(await _refresh.IsSessionActiveAsync(phone.SessionId));
        Assert.NotNull(await _refresh.RotateAsync(phone.Token, null));
        // Another user cannot end it, and an ended session cannot be ended twice.
        Assert.False(await _refresh.RevokeSessionAsync(8, phone.SessionId));
        Assert.False(await _refresh.RevokeSessionAsync(7, laptop.SessionId));
    }

    [Fact]
    public async Task RevokeAll_KeepsTheCurrentDevice()
    {
        var current = await _refresh.IssueAsync(7, null);
        var other1 = await _refresh.IssueAsync(7, null);
        var other2 = await _refresh.IssueAsync(7, null);
        await _refresh.RevokeAllAsync(7, "PasswordChanged", current.SessionId);
        Assert.True(await _refresh.IsSessionActiveAsync(current.SessionId));
        Assert.False(await _refresh.IsSessionActiveAsync(other1.SessionId));
        Assert.False(await _refresh.IsSessionActiveAsync(other2.SessionId));
        Assert.NotNull(await _refresh.RotateAsync(current.Token, null));
        Assert.Null(await _refresh.RotateAsync(other1.Token, null));
    }

    [Fact]
    public async Task SignOut_EndsTheSession()
    {
        var issued = await _refresh.IssueAsync(7, null);
        await _refresh.RevokeAsync(issued.Token);
        var session = _db.UserSessions.Single();
        Assert.Equal("SignOut", session.RevokedReason);
        Assert.Null(await _refresh.RotateAsync(issued.Token, null));
    }

    [Fact]
    public async Task AccessToken_CarriesTheSession()
    {
        var account = new AccountView(7, "m@test.vn", "Manager", null, "Manager", "Active", "x", MustChangePassword: false);
        var sid = Guid.NewGuid();
        var token = new JwtTokenService(_options).CreateAccessToken(account, sid);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = JwtOptions.Issuer, ValidAudience = JwtOptions.Audience, IssuerSigningKey = JwtTokenService.SigningKey(_options.Secret)
        });
        Assert.Equal(sid.ToString("N"), result.Claims[JwtTokenService.SessionClaim]);
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
        _db.UserSessions.Single().AbsoluteExpiresAt = row.AbsoluteExpiresAt;
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
