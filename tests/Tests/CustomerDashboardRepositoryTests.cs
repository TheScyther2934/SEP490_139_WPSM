using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;
using Domain.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

/// <summary>
/// Unit tests for C-01 Customer Dashboard — CustomerDashboardRepository.
/// Uses EF Core In-Memory database to avoid SQL Server dependency.
/// Covers the 8 cases required by the SRS implementation checklist.
/// </summary>
public sealed class CustomerDashboardRepositoryTests : IDisposable
{
    // ── In-Memory DB factory ─────────────────────────────────────────────────
    private readonly WpsmDbContext _db;

    public CustomerDashboardRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WpsmDbContext(options);
        SeedBaseData();
    }

    public void Dispose() => _db.Dispose();

    // ── Shared seed helpers ──────────────────────────────────────────────────
    private Hall _hall = null!;
    private Slot _slot = null!;
    private User _customer = null!;
    private User _otherCustomer = null!;
    private User _manager = null!;

    private void SeedBaseData()
    {
        _hall = new Hall { HallId = 1, Name = "Sảnh Hoa Hồng", Status = "Active" };
        _slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Ca Tối" };
        _customer = new User { UserId = 10, Email = "a@test.vn", FullName = "Minh Anh & Thu Hà", Role = "Customer", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _otherCustomer = new User { UserId = 20, Email = "b@test.vn", FullName = "Khác", Role = "Customer", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _manager = new User { UserId = 99, Email = "mgr@test.vn", FullName = "Manager", Role = "Manager", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };

        _db.Halls.Add(_hall);
        _db.Slots.Add(_slot);
        _db.Users.AddRange(_customer, _otherCustomer, _manager);
        _db.SaveChanges();
    }

    private WeddingProject CreateProject(int customerId, DateOnly eventDate, decimal baseValue = 100_000_000m, string code = "WPSM-TEST-001")
    {
        var project = new WeddingProject
        {
            ProjectCode = code,
            CustomerId = customerId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = eventDate,
            BaseContractValue = baseValue,
            DepositAmount = 30_000_000m,
            CurrentTotal = baseValue,
            Status = "Locked",
            CreatedAt = DateTime.UtcNow
        };
        _db.WeddingProjects.Add(project);
        _db.SaveChanges();
        return project;
    }

    private CustomerDashboardRepository Repo() => new(_db);

    // =========================================================================
    // TEST 1 — Customer truy cập đúng dự án của mình
    // =========================================================================
    [Fact]
    public async Task GetDashboard_ReturnsData_WhenProjectBelongsToCustomer()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        CreateProject(_customer.UserId, futureDate);

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        Assert.Equal("WPSM-TEST-001", result.ProjectCode);
        Assert.Equal(_customer.FullName, result.CoupleName);
    }

    // =========================================================================
    // TEST 2 — Customer KHÔNG truy cập được dự án của người khác
    // =========================================================================
    [Fact]
    public async Task GetDashboard_ReturnsNull_WhenProjectBelongsToAnotherCustomer()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        CreateProject(_otherCustomer.UserId, futureDate, code: "WPSM-TEST-002");

        // Customer 10 tries to access project belonging to customer 20
        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-002");

        Assert.Null(result);
    }

    // =========================================================================
    // TEST 3a — Countdown đúng TRƯỚC ngày cưới
    // =========================================================================
    [Fact]
    public async Task GetDashboard_CountdownDays_CorrectBeforeWedding()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(45));
        CreateProject(_customer.UserId, eventDate);

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        // Countdown may differ ±1 due to timezone, accept range 44-45
        Assert.InRange(result.CountdownDays, 44, 45);
        Assert.Contains("ngày", result.CountdownLabel);
    }

    // =========================================================================
    // TEST 3b — Countdown = 0 VÀO NGÀY cưới (không âm)
    // =========================================================================
    [Fact]
    public async Task GetDashboard_CountdownDays_ZeroOnWeddingDay()
    {
        // EventDate = today (local Vietnam time ~ UTC+7)
        var eventDate = DateOnly.FromDateTime(DateTime.Today);
        CreateProject(_customer.UserId, eventDate);

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        Assert.Equal(0, result.CountdownDays);
        Assert.Contains("Hôm nay", result.CountdownLabel);
    }

    // =========================================================================
    // TEST 4 — The five stages of the event, the current one highlighted
    // =========================================================================
    [Fact]
    public async Task GetDashboard_ShowsTheFiveStagesAndTheCurrentStage()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(40));
        var project = CreateProject(_customer.UserId, eventDate);
        project.Status = "Adjusting";
        project.LockedAt = null;
        await _db.SaveChangesAsync();

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        Assert.Equal(5, result.Stages.Count);
        Assert.Equal(2, result.CurrentStage);
        Assert.Equal("Current", result.Stages.Single(s => s.Number == 2).State);
        Assert.Equal("Done", result.Stages.Single(s => s.Number == 1).State);
        Assert.Equal("Đang điều chỉnh", result.StatusLabel);
    }

    // =========================================================================
    // TEST 5 — Only confirmed addon contracts count in the balance
    // =========================================================================
    [Fact]
    public async Task GetDashboard_OutstandingBalance_CountsOnlyConfirmedAddons()
    {
        const decimal baseValue = 100_000_000m;
        const decimal paid = 30_000_000m;
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var project = CreateProject(_customer.UserId, eventDate, baseValue);
        _db.Payments.Add(new Payment { ProjectId = project.ProjectId, PaymentType = "Deposit", Status = "Paid", Amount = paid, DueDate = DateOnly.FromDateTime(DateTime.Today) });
        AddAddon(project, "PS-1", "Confirmed", 4_000_000m);
        AddAddon(project, "PS-2", "PendingCustomer", 9_000_000m);
        AddAddon(project, "PS-3", "Cancelled", 5_000_000m);
        await _db.SaveChangesAsync();

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        Assert.Equal(baseValue + 4_000_000m - paid, result.OutstandingBalance);
        Assert.Equal(baseValue + 4_000_000m, result.CurrentTotal);
    }

    // =========================================================================
    // TEST 6 — Addon contracts waiting for the customer's confirmation
    // =========================================================================
    [Fact]
    public async Task GetDashboard_PendingAddonCount_CountsOnlyWaitingAddons()
    {
        var project = CreateProject(_customer.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(5)));
        AddAddon(project, "PS-1", "PendingCustomer", 1_000_000m);
        AddAddon(project, "PS-2", "PendingCustomer", 2_000_000m);
        AddAddon(project, "PS-3", "Confirmed", 3_000_000m);
        await _db.SaveChangesAsync();

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        Assert.Equal(2, result.PendingAddonCount);
    }

    // =========================================================================
    // TEST 7a — Who to contact: every operator of the event and the hotline
    // =========================================================================
    [Fact]
    public async Task GetDashboard_ShowsOperatorsAndHotline()
    {
        var project = CreateProject(_customer.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(60)));
        _db.ProjectManagers.Add(new ProjectManager { ProjectId = project.ProjectId, ManagerId = _manager.UserId, Role = "Primary", AssignedAt = DateTime.UtcNow });
        _db.SystemSettings.Add(new SystemSetting { SettingKey = "Hotline", SettingValue = "0976586668", UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        var contact = Assert.Single(result.Contacts);
        Assert.Equal("Manager", contact.FullName);
        Assert.Equal("Primary", contact.Role);
        Assert.Equal("0976586668", result.Hotline);
    }

    private void AddAddon(WeddingProject project, string code, string status, decimal amount)
    {
        var addon = new AddonContract
        {
            AddonCode = code, ProjectId = project.ProjectId, Title = code, Status = status, CreatedBy = _manager.UserId, CreatedAt = DateTime.UtcNow,
            ConfirmedAt = status == "Confirmed" ? DateTime.UtcNow : null, CancelledAt = status == "Cancelled" ? DateTime.UtcNow : null,
            CancelReason = status == "Cancelled" ? "Không cần nữa" : null
        };
        addon.Items.Add(new AddonContractItem { Description = code, Quantity = 1, UnitPrice = amount });
        _db.AddonContracts.Add(addon);
    }

    // =========================================================================
    // TEST 7b — Không có ảnh → LatestProofPhoto = null (không exception)
    // =========================================================================
    [Fact]
    public async Task GetDashboard_ReturnsNullPhoto_WhenNoExecutionPhotos()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(60));
        CreateProject(_customer.UserId, eventDate);

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        Assert.Null(result.LatestProofPhoto);
    }

    // =========================================================================
    // TEST 8 — Project không tồn tại → trả về null (không exception)
    // =========================================================================
    [Fact]
    public async Task GetDashboard_ReturnsNull_WhenProjectDoesNotExist()
    {
        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-NOT-EXIST");

        Assert.Null(result);
    }
}
