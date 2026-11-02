using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
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
            Nrdamount = 30_000_000m,
            CurrentTotal = baseValue,
            Status = "Baseline Frozen",
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
    // TEST 4 — Chỉ hiển thị tối đa 5 upcoming tasks
    // =========================================================================
    [Fact]
    public async Task GetDashboard_UpcomingTasks_MaxFive()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(90));
        var project = CreateProject(_customer.UserId, eventDate);

        // Add 8 tasks
        for (int i = 1; i <= 8; i++)
        {
            _db.RoadmapTasks.Add(new RoadmapTask
            {
                ProjectId = project.ProjectId,
                Milestone = "T-90",
                Title = $"Task {i}",
                AssigneeRole = "Customer",
                DueDate = eventDate.AddDays(-90 + i),
                Status = "Pending"
            });
        }
        await _db.SaveChangesAsync();

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        Assert.True(result.UpcomingTasks.Count <= 5, $"Expected ≤5 tasks but got {result.UpcomingTasks.Count}");
    }

    // =========================================================================
    // TEST 5 — Không tính CR chưa được phê duyệt (Draft/Negotiating) vào balance
    // =========================================================================
    [Fact]
    public async Task GetDashboard_OutstandingBalance_ExcludesUnapprovedCRVariance()
    {
        const decimal baseValue = 100_000_000m;
        const decimal paid = 30_000_000m;
        const decimal unapprovedVariance = 10_000_000m;

        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(60));
        var project = CreateProject(_customer.UserId, eventDate, baseValue);

        // NRD paid
        _db.Payments.Add(new Payment { ProjectId = project.ProjectId, PaymentType = "NRD", Status = "Paid", Amount = paid, DueDate = DateOnly.FromDateTime(DateTime.Today) });
        // Draft CR (should NOT count)
        _db.ChangeRequests.Add(new ChangeRequest { ProjectId = project.ProjectId, Crcode = "CR-001", CreatedBy = _customer.UserId, Type = "Scope", Description = "Draft CR", Status = "Draft", CostImpact = unapprovedVariance, TotalVariance = unapprovedVariance, CreatedAt = DateTime.UtcNow });
        // Negotiating CR (should NOT count)
        _db.ChangeRequests.Add(new ChangeRequest { ProjectId = project.ProjectId, Crcode = "CR-002", CreatedBy = _customer.UserId, Type = "Scope", Description = "Negotiating CR", Status = "Negotiating", CostImpact = unapprovedVariance, TotalVariance = unapprovedVariance, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        // Balance = Base - Paid (unapproved CRs excluded)
        var expected = Math.Max(0m, baseValue - paid);
        Assert.Equal(expected, result.OutstandingBalance);
    }

    // =========================================================================
    // TEST 6 — Không tính CR Rejected vào số lượng CR đang mở
    // =========================================================================
    [Fact]
    public async Task GetDashboard_OpenCrCount_ExcludesRejectedCRs()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(60));
        var project = CreateProject(_customer.UserId, eventDate);

        _db.ChangeRequests.AddRange(
            new ChangeRequest { ProjectId = project.ProjectId, Crcode = "CR-A", CreatedBy = _customer.UserId, Type = "Scope", Description = "Open Draft", Status = "Draft", CostImpact = 0, CreatedAt = DateTime.UtcNow },
            new ChangeRequest { ProjectId = project.ProjectId, Crcode = "CR-B", CreatedBy = _customer.UserId, Type = "Scope", Description = "Negotiating", Status = "Negotiating", CostImpact = 0, CreatedAt = DateTime.UtcNow },
            new ChangeRequest { ProjectId = project.ProjectId, Crcode = "CR-C", CreatedBy = _customer.UserId, Type = "Scope", Description = "Rejected", Status = "Rejected", CostImpact = 0, CreatedAt = DateTime.UtcNow },
            new ChangeRequest { ProjectId = project.ProjectId, Crcode = "CR-D", CreatedBy = _customer.UserId, Type = "Scope", Description = "CustomerApproved", Status = "CustomerApproved", CostImpact = 0, CreatedAt = DateTime.UtcNow }
        );
        await _db.SaveChangesAsync();

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        // Only Draft and Negotiating count as "open" (per domain rule)
        Assert.Equal(2, result.OpenCrCount);
    }

    // =========================================================================
    // TEST 7a — Không có task → trả về danh sách rỗng hợp lệ (không exception)
    // =========================================================================
    [Fact]
    public async Task GetDashboard_ReturnsEmptyTasks_WhenNoRoadmapExists()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(60));
        CreateProject(_customer.UserId, eventDate);

        var result = await Repo().GetDashboardAsync(_customer.UserId, "WPSM-TEST-001");

        Assert.NotNull(result);
        Assert.Empty(result.UpcomingTasks);
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
