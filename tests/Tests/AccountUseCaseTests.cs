using Application.Persistence;
using Application.UseCases.Accounts;
using Domain.Entities;
using Domain.Rules;
using Infrastructure.Email;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.UseCases;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>Accounts (A-19, profile, password): rules alone, then the use cases on an in-memory database. No email is sent.</summary>
public sealed class AccountUseCaseTests
{
    private const int AdminId = 3;
    private const int ManagerId = 2;
    private const int CustomerId = 1;
    private readonly WpsmDbContext _db = TestFixtures.NewDb();
    private readonly TestFixtures.RecordingEmailOutbox _outbox = new();
    private readonly PasswordService _passwords = new(new PasswordHasher<User>());

    [Fact]
    public void Details_roles_and_locks_follow_the_account_rules()
    {
        Assert.Equal(new AccountDetails("Minh", "minh@test.vn", "0912345678"), AccountPolicy.ValidateDetails(" Minh ", " minh@test.vn ", "0912 345 678"));
        Assert.Throws<BusinessRuleException>(() => AccountPolicy.ValidateDetails("Minh", "minh@localhost", null));
        Assert.Throws<BusinessRuleException>(() => AccountPolicy.EnsureStaffRole("Customer"));
        Assert.Throws<BusinessRuleException>(() => AccountPolicy.EnsureCanChangeRole("Manager", "Admin", isSelf: false, openEvents: 2));
        Assert.Throws<BusinessRuleException>(() => AccountPolicy.EnsureCanChangeRole("Admin", "Manager", isSelf: true, openEvents: 0));
        AccountPolicy.EnsureCanChangeRole("Manager", "Admin", isSelf: false, openEvents: 0);
        Assert.Throws<BusinessRuleException>(() => AccountPolicy.EnsureCanLock("Admin", isSelf: true, openEvents: 0));
        Assert.Throws<BusinessRuleException>(() => AccountPolicy.ValidateNewPassword("old-pass", "new-pass1", "new-pass2"));
        Assert.Throws<BusinessRuleException>(() => AccountPolicy.ValidateNewPassword("same-pass", "same-pass", "same-pass"));
        Assert.Throws<BusinessRuleException>(() => new User { Status = "Inactive" }.IssueTemporaryPassword("hash"));
    }

    private AccountStore Store() => new(_db);
    private UserAccountRepository Reads() => new(_db);
    private AccountMessages Messages() => new(_outbox, new EmailOptions { PublicBaseUrl = "http://test.local" });

    private async Task SeedAsync()
    {
        _db.Users.AddRange(
            TestFixtures.NewUser(CustomerId, "Customer", "customer@test.local", "Cô dâu Ánh"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"),
            TestFixtures.NewUser(AdminId, "Admin", "admin@test.local", "Admin"));
        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());
        _db.WeddingProjects.Add(TestFixtures.NewProject(14, "WPSM-2026-014", CustomerId, ManagerId, status: "Locked"));
        _db.UserSessions.Add(new UserSession { SessionId = Guid.NewGuid(), UserId = ManagerId, CreatedAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow, AbsoluteExpiresAt = DateTime.UtcNow.AddHours(8) });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Creating_a_staff_account_sends_its_temporary_password_and_refuses_a_used_email()
    {
        await SeedAsync();
        var create = new CreateStaffAccount(Store(), _passwords, Messages(), Reads(), new UnitOfWork(_db));

        var created = await create.HandleAsync(new UserAccountCommand("Lan Anh", "lananh@test.vn", null, "Manager"));

        Assert.True(created.Account.MustChangePassword);
        Assert.True(_passwords.Verify(_db.Users.Single(u => u.Email == "lananh@test.vn").PasswordHash, created.TemporaryPassword));
        Assert.Contains(_outbox.Messages, m => m.To == "lananh@test.vn" && m.Kind == "Credentials");
        await Assert.ThrowsAsync<BusinessRuleException>(() => create.HandleAsync(new UserAccountCommand("Khác", "manager@test.local", null, "Admin")));
        await Assert.ThrowsAsync<BusinessRuleException>(() => create.HandleAsync(new UserAccountCommand("Khách", "khach@test.vn", null, "Customer")));
    }

    [Fact]
    public async Task A_manager_with_open_events_is_neither_locked_nor_made_admin_and_locking_signs_out_every_device()
    {
        await SeedAsync();
        var status = new SetAccountStatus(Store(), Reads(), new UnitOfWork(_db));
        var update = new UpdateAccount(Store(), Reads(), new UnitOfWork(_db));

        await Assert.ThrowsAsync<BusinessRuleException>(() => status.HandleAsync(ManagerId, false, AdminId));
        await Assert.ThrowsAsync<BusinessRuleException>(() => update.HandleAsync(ManagerId, new UserAccountCommand("Quản lý Minh", "manager@test.local", null, "Admin"), AdminId));
        await Assert.ThrowsAsync<BusinessRuleException>(() => status.HandleAsync(AdminId, false, AdminId));

        _db.WeddingProjects.Single().Status = "Closed";
        await _db.SaveChangesAsync();
        var locked = await status.HandleAsync(ManagerId, false, AdminId);

        Assert.False(locked!.Active);
        Assert.NotNull((await _db.UserSessions.AsNoTracking().SingleAsync()).RevokedAt);
    }

    [Fact]
    public async Task Reissuing_a_password_requires_an_unlocked_account_and_ends_its_sessions()
    {
        await SeedAsync();
        var reset = new ResetAccountPassword(Store(), _passwords, Messages(), Reads(), new UnitOfWork(_db));

        var issued = await reset.HandleAsync(ManagerId);

        Assert.True(issued!.Account.MustChangePassword);
        Assert.Equal("PasswordReset", (await _db.UserSessions.AsNoTracking().SingleAsync()).RevokedReason);
        Assert.Contains(_outbox.Messages, m => m.To == "manager@test.local");
        Assert.Null(await reset.HandleAsync(999));
    }

    [Fact]
    public async Task Changing_the_password_checks_the_current_one_and_clears_must_change()
    {
        await SeedAsync();
        var user = _db.Users.Single(u => u.UserId == CustomerId);
        user.PasswordHash = _passwords.Hash("temporary-1");
        user.MustChangePassword = true;
        await _db.SaveChangesAsync();
        var change = new ChangePassword(Store(), _passwords, new UnitOfWork(_db));

        await Assert.ThrowsAsync<BusinessRuleException>(() => change.HandleAsync(CustomerId, "wrong-pass", "brand-new-1", "brand-new-1"));
        Assert.True(await change.HandleAsync(CustomerId, "temporary-1", "brand-new-1", "brand-new-1"));

        var saved = await _db.Users.AsNoTracking().SingleAsync(u => u.UserId == CustomerId);
        Assert.False(saved.MustChangePassword);
        Assert.True(_passwords.Verify(saved.PasswordHash, "brand-new-1"));
        var profile = await new UpdateProfile(Store(), new UnitOfWork(_db)).HandleAsync(CustomerId, new ProfileCommand(" Ánh Nguyễn ", "091-234-5678"));
        Assert.Equal(("Ánh Nguyễn", "0912345678"), (profile!.FullName, profile.Phone));
    }
}
