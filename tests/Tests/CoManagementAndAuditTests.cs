using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Auditing;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

/// <summary>
/// Co-management (Primary + co-managers, only the Primary or an Admin may change the list) and the audit log interceptor
/// (one row per saved change, the acting user, old → new values, secrets masked).
/// </summary>
public sealed class CoManagementAndAuditTests : IDisposable
{
    private const int CustomerId = 10, PrimaryId = 20, OtherManagerId = 21, ThirdManagerId = 22, AdminId = 1;
    private readonly WpsmDbContext _db;
    private readonly TestActor _actor = new();

    private sealed class TestActor : IAuditActor
    {
        public int? UserId { get; set; } = PrimaryId;
        public string? Name { get; set; } = "Primary";
        public string? Email { get; set; } = "primary@test.vn";
        public string? Role { get; set; } = "Manager";
        public string? IpAddress => "127.0.0.1";
    }

    public CoManagementAndAuditTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditSaveChangesInterceptor(_actor)).Options;
        _db = new WpsmDbContext(options);
        _db.Halls.Add(new Hall { HallId = 1, Name = "Sảnh A", Status = "Active" });
        _db.Slots.Add(new Slot { SlotId = 1, HallId = 1, SlotName = "Evening", StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(22, 0) });
        _db.Users.AddRange(
            User(AdminId, "Admin", "admin@test.vn"), User(CustomerId, "Customer", "c@test.vn"), User(PrimaryId, "Manager", "primary@test.vn"),
            User(OtherManagerId, "Manager", "other@test.vn"), User(ThirdManagerId, "Manager", "third@test.vn"));
        _db.WeddingProjects.Add(new WeddingProject
        {
            ProjectId = 5, ProjectCode = "WPSM-T-001", CustomerId = CustomerId, ManagerId = PrimaryId, CreatedBy = PrimaryId, HallId = 1, SlotId = 1,
            EventDate = new DateOnly(2027, 1, 16), BaseContractValue = 100_000_000m, DepositAmount = 30_000_000m, CurrentTotal = 100_000_000m,
            Status = "Locked", LockedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    public void Dispose() => _db.Dispose();

    private static User User(int id, string role, string email) =>
        new() { UserId = id, Email = email, FullName = $"{role} {id}", Role = role, Status = "Active", PasswordHash = "secret-hash", CreatedAt = DateTime.UtcNow };

    private ProjectManagerRepository Managers() => new(_db);

    private Task<bool> CanAccess(int userId) => new ProjectRepository(_db, null!, null!, null!).CanAccessAsync("WPSM-T-001", userId, "Manager");

    [Fact]
    public async Task Primary_adds_a_co_manager_who_then_gets_access()
    {
        Assert.False(await CanAccess(OtherManagerId));
        var result = await Managers().AddAsync("WPSM-T-001", OtherManagerId, "Nghỉ phép một tuần", PrimaryId, "Manager");
        Assert.Null(result.Error);
        Assert.Equal(2, result.View!.Managers.Count(m => m.Active));
        Assert.True(await CanAccess(OtherManagerId));
        Assert.True(await CanAccess(PrimaryId));
        Assert.Contains(_db.Notifications, n => n.RecipientId == OtherManagerId && n.Type == "Delegation");
        // The audit row keeps the Vietnamese reason as text, so Admin can search for it.
        Assert.Contains(_db.AuditLogs, a => a.EntityType == "ProjectManager" && a.Action == "Create" && a.Changes!.Contains("Nghỉ phép một tuần"));
    }

    [Fact]
    public async Task Co_manager_cannot_delegate_further_but_admin_can()
    {
        await Managers().AddAsync("WPSM-T-001", OtherManagerId, "Hỗ trợ ngày cưới", PrimaryId, "Manager");
        var byCoManager = await Managers().AddAsync("WPSM-T-001", ThirdManagerId, "Ủy quyền tiếp", OtherManagerId, "Manager");
        Assert.True(byCoManager.Forbidden);
        var byAdmin = await Managers().AddAsync("WPSM-T-001", ThirdManagerId, "Admin điều phối", AdminId, "Admin");
        Assert.Null(byAdmin.Error);
        Assert.Equal(3, byAdmin.View!.Managers.Count(m => m.Active));
    }

    [Fact]
    public async Task Rules_reason_required_no_duplicates_primary_cannot_be_removed()
    {
        Assert.NotNull((await Managers().AddAsync("WPSM-T-001", OtherManagerId, "", PrimaryId, "Manager")).Error);
        Assert.NotNull((await Managers().AddAsync("WPSM-T-001", PrimaryId, "Thêm chính mình", PrimaryId, "Manager")).Error);
        Assert.NotNull((await Managers().AddAsync("WPSM-T-001", CustomerId, "Không phải Manager", PrimaryId, "Manager")).Error);
        var added = await Managers().AddAsync("WPSM-T-001", OtherManagerId, "Hỗ trợ ngày cưới", PrimaryId, "Manager");
        Assert.NotNull((await Managers().AddAsync("WPSM-T-001", OtherManagerId, "Thêm lần hai", PrimaryId, "Manager")).Error);
        var primaryRow = added.View!.Managers.Single(m => m.Role == "Primary");
        Assert.NotNull((await Managers().RemoveAsync("WPSM-T-001", primaryRow.ProjectManagerId, "Gỡ người tạo", AdminId, "Admin")).Error);
    }

    [Fact]
    public async Task Removed_co_manager_loses_access_but_stays_in_history()
    {
        var added = await Managers().AddAsync("WPSM-T-001", OtherManagerId, "Hỗ trợ ngày cưới", PrimaryId, "Manager");
        var row = added.View!.Managers.Single(m => m.ManagerId == OtherManagerId);
        var removed = await Managers().RemoveAsync("WPSM-T-001", row.ProjectManagerId, "Đã hết thời gian hỗ trợ", PrimaryId, "Manager");
        Assert.Null(removed.Error);
        Assert.False(await CanAccess(OtherManagerId));
        var history = removed.View!.Managers.Single(m => m.ManagerId == OtherManagerId);
        Assert.False(history.Active);
        Assert.Equal("Đã hết thời gian hỗ trợ", history.RemovalReason);
    }

    [Fact]
    public async Task Audit_log_records_actor_old_and_new_values_and_masks_secrets()
    {
        _actor.UserId = AdminId; _actor.Name = "Admin"; _actor.Email = "admin@test.vn"; _actor.Role = "Admin";
        var project = await _db.WeddingProjects.SingleAsync(x => x.ProjectId == 5);
        project.Status = "Completed";
        var user = await _db.Users.SingleAsync(x => x.UserId == OtherManagerId);
        user.PasswordHash = "new-secret";
        await _db.SaveChangesAsync();

        var projectLog = await _db.AuditLogs.SingleAsync(x => x.EntityType == "WeddingProject" && x.Action == "Update");
        Assert.Equal(AdminId, projectLog.UserId);
        Assert.Equal("Admin", projectLog.Role);
        Assert.Equal("Project", projectLog.Impact);
        Assert.Equal("WPSM-T-001", projectLog.ProjectCode);
        var status = JsonDocument.Parse(projectLog.Changes!).RootElement.GetProperty("Status");
        Assert.Equal("Locked", status.GetProperty("old").GetString());
        Assert.Equal("Completed", status.GetProperty("new").GetString());

        var accountLog = await _db.AuditLogs.SingleAsync(x => x.EntityType == "User" && x.Action == "Update");
        Assert.Equal("Account", accountLog.Impact);
        Assert.DoesNotContain("new-secret", accountLog.Changes);
        Assert.DoesNotContain("secret-hash", accountLog.Changes);
    }

    [Fact]
    public async Task Beo_freeze_waits_for_the_lock_but_not_for_pending_addons()
    {
        // Still adjusting (stage 2): the coordination sheet is frozen only from the locked services.
        var project = await _db.WeddingProjects.SingleAsync(x => x.ProjectId == 5);
        project.Status = "Adjusting";
        project.LockedAt = null;
        await _db.SaveChangesAsync();
        var early = await Assert.ThrowsAsync<InvalidOperationException>(() => new BeoRepository(_db).FreezeOrReapproveBeoAsync("WPSM-T-001", PrimaryId, null));
        Assert.Contains("khóa dịch vụ", early.Message);

        // Locked with an addon contract still waiting for the customer: it is not in the sheet, so it does not block (20/11/2026).
        project.Status = "Locked";
        project.LockedAt = DateTime.UtcNow;
        var addon = new AddonContract { AddonCode = "PS-T-1", ProjectId = 5, Title = "Thêm bàn", Status = "PendingCustomer", CreatedBy = PrimaryId, CreatedAt = DateTime.UtcNow };
        addon.Items.Add(new AddonContractItem { Description = "Bàn", Quantity = 1, UnitPrice = 3_500_000m });
        _db.AddonContracts.Add(addon);
        await _db.SaveChangesAsync();
        var frozen = await new BeoRepository(_db).FreezeOrReapproveBeoAsync("WPSM-T-001", PrimaryId, null);
        Assert.Equal("Frozen", frozen!.Status);
        Assert.DoesNotContain(frozen.Items, i => i.ChangedByAddon);
    }

    [Fact]
    public async Task Audit_log_skips_notifications_and_unchanged_saves()
    {
        var before = await _db.AuditLogs.CountAsync();
        _db.Notifications.Add(new Notification { RecipientId = PrimaryId, Type = "Payment", Message = "x" });
        await _db.SaveChangesAsync();
        await _db.SaveChangesAsync();
        Assert.Equal(before, await _db.AuditLogs.CountAsync());
    }
}
