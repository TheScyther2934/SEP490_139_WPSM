using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

/// <summary>
/// Unit tests for C-03 — CustomerRoadmapRepository.
/// Covers 10 test cases required by Report 3 SRS Section 8.
/// Uses EF Core In-Memory database.
/// </summary>
public sealed class CustomerRoadmapRepositoryTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly Hall _hall;
    private readonly Slot _slot;
    private readonly User _customer;
    private readonly User _otherCustomer;
    private readonly User _manager;

    public CustomerRoadmapRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WpsmDbContext(options);

        _hall = new Hall { HallId = 1, Name = "Sảnh A", Status = "Active" };
        _slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Ca Tối" };
        _customer = new User { UserId = 10, Email = "c@test.vn", FullName = "Khách hàng A", Role = "Customer", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _otherCustomer = new User { UserId = 20, Email = "other@test.vn", FullName = "Khách hàng B", Role = "Customer", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _manager = new User { UserId = 99, Email = "mgr@test.vn", FullName = "Manager", Role = "Manager", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _db.Halls.Add(_hall);
        _db.Slots.Add(_slot);
        _db.Users.AddRange(_customer, _otherCustomer, _manager);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private WeddingProject CreateProject(int customerId, DateOnly eventDate, string code = "WPSM-C03-001")
    {
        var p = new WeddingProject
        {
            ProjectCode = code, CustomerId = customerId, ManagerId = _manager.UserId,
            HallId = _hall.HallId, SlotId = _slot.SlotId, EventDate = eventDate,
            BaseContractValue = 100_000_000m, Nrdamount = 30_000_000m,
            CurrentTotal = 100_000_000m, Status = "Active", CreatedAt = DateTime.UtcNow
        };
        _db.WeddingProjects.Add(p);
        _db.SaveChanges();
        return p;
    }

    private RoadmapTask AddTask(int projectId, string milestone, string title, string role, DateOnly dueDate, string status = "Pending", DateTime? completedAt = null)
    {
        var t = new RoadmapTask { ProjectId = projectId, Milestone = milestone, Title = title, AssigneeRole = role, DueDate = dueDate, Status = status, CompletedAt = completedAt };
        _db.RoadmapTasks.Add(t);
        _db.SaveChanges();
        return t;
    }

    private CustomerRoadmapRepository Repo() => new(_db);

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 1 — Customer xem roadmap của dự án mình
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetRoadmap_ReturnsData_ForOwnProject()
    {
        var project = CreateProject(_customer.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        AddTask(project.ProjectId, "T-90", "Xác nhận menu", "Customer", DateOnly.FromDateTime(DateTime.Today.AddDays(5)));

        var result = await Repo().GetRoadmapAsync(_customer.UserId, "WPSM-C03-001");

        Assert.NotNull(result);
        Assert.Equal("WPSM-C03-001", result.ProjectCode);
        Assert.True(result.TotalTasks > 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 2 — Customer KHÔNG xem được roadmap dự án người khác
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetRoadmap_ReturnsNull_ForOtherCustomerProject()
    {
        CreateProject(_otherCustomer.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)), "WPSM-C03-002");

        var result = await Repo().GetRoadmapAsync(_customer.UserId, "WPSM-C03-002");

        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 3 — Đúng 4 nhóm milestone
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetRoadmap_ReturnsFourMilestoneGroups()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(100));
        CreateProject(_customer.UserId, eventDate);

        var result = await Repo().GetRoadmapAsync(_customer.UserId, "WPSM-C03-001");

        Assert.NotNull(result);
        Assert.Equal(4, result.MilestoneGroups.Count);
        Assert.Contains(result.MilestoneGroups, g => g.Milestone == "T-90");
        Assert.Contains(result.MilestoneGroups, g => g.Milestone == "T-30");
        Assert.Contains(result.MilestoneGroups, g => g.Milestone == "T-7");
        Assert.Contains(result.MilestoneGroups, g => g.Milestone == "Event Day");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 4 — Task hiển thị đúng owner, deadline và status
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetRoadmap_TaskHasCorrectOwnerAndDueDateAndStatus()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(90));
        var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var project = CreateProject(_customer.UserId, eventDate);
        AddTask(project.ProjectId, "T-90", "Kiểm tra sảnh", "Customer", dueDate);

        var result = await Repo().GetRoadmapAsync(_customer.UserId, "WPSM-C03-001");

        var group = result!.MilestoneGroups.First(g => g.Milestone == "T-90");
        var task = group.Tasks.First(t => t.Title == "Kiểm tra sảnh");
        Assert.Equal("Customer", task.AssigneeRole);
        Assert.Equal(dueDate, task.DueDate);
        Assert.Equal("Pending", task.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 5 — Customer hoàn thành task của mình
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CompleteTask_Succeeds_ForCustomerOwnedTask()
    {
        var project = CreateProject(_customer.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        var task = AddTask(project.ProjectId, "T-90", "Gửi danh sách khách", "Customer",
            DateOnly.FromDateTime(DateTime.Today.AddDays(10)));

        var result = await Repo().CompleteTaskAsync(_customer.UserId, "WPSM-C03-001", task.TaskId);

        Assert.True(result.Success);
        Assert.NotNull(result.UpdatedTask);
        Assert.Equal("Done", result.UpdatedTask!.Status);
        Assert.NotNull(result.UpdatedTask.CompletedAt);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 6 — Customer KHÔNG thể hoàn thành task thuộc Manager
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CompleteTask_Fails_ForManagerOwnedTask()
    {
        var project = CreateProject(_customer.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        var task = AddTask(project.ProjectId, "T-30", "Kiểm tra âm thanh", "Manager",
            DateOnly.FromDateTime(DateTime.Today.AddDays(20)));

        var result = await Repo().CompleteTaskAsync(_customer.UserId, "WPSM-C03-001", task.TaskId);

        Assert.False(result.Success);
        Assert.Null(result.UpdatedTask);
        Assert.Contains("Ban điều phối", result.Message);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 7 — Hoàn thành task của dự án khác bị từ chối
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CompleteTask_Fails_ForTaskInOtherCustomerProject()
    {
        var otherProject = CreateProject(_otherCustomer.UserId,
            DateOnly.FromDateTime(DateTime.Today.AddDays(90)), "WPSM-C03-002");
        var task = AddTask(otherProject.ProjectId, "T-90", "Task của dự án khác", "Customer",
            DateOnly.FromDateTime(DateTime.Today.AddDays(5)));

        // Customer 10 tries to complete task belonging to Customer 20's project
        var result = await Repo().CompleteTaskAsync(_customer.UserId, "WPSM-C03-002", task.TaskId);

        Assert.False(result.Success);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 8 — Task đã Done không bị hoàn thành lặp gây lỗi dữ liệu (idempotent)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CompleteTask_IsIdempotent_WhenAlreadyDone()
    {
        var completedAt = DateTime.UtcNow.AddHours(-1);
        var project = CreateProject(_customer.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        var task = AddTask(project.ProjectId, "T-90", "Task đã xong", "Customer",
            DateOnly.FromDateTime(DateTime.Today.AddDays(5)), status: "Done", completedAt: completedAt);

        var result = await Repo().CompleteTaskAsync(_customer.UserId, "WPSM-C03-001", task.TaskId);

        Assert.True(result.Success);
        Assert.Equal("Done", result.UpdatedTask!.Status);
        // Completion timestamp must NOT be reset (original preserved)
        var dbTask = _db.RoadmapTasks.Find(task.TaskId)!;
        Assert.Equal(completedAt.Date, dbTask.CompletedAt!.Value.Date);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 9 — Task Overdue được hiển thị chính xác
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetRoadmap_OverdueTasks_DisplayedCorrectly()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(90));
        var project = CreateProject(_customer.UserId, eventDate);
        // Task with DueDate in the PAST and still Pending
        var pastDue = DateOnly.FromDateTime(DateTime.Today.AddDays(-3));
        AddTask(project.ProjectId, "T-90", "Nhiệm vụ quá hạn", "Customer", pastDue, status: "Pending");

        var result = await Repo().GetRoadmapAsync(_customer.UserId, "WPSM-C03-001");

        var group = result!.MilestoneGroups.First(g => g.Milestone == "T-90");
        var task = group.Tasks.First(t => t.Title == "Nhiệm vụ quá hạn");
        Assert.Equal("Overdue", task.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 10 — Milestone dates are calculated correctly from EventDate
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetRoadmap_MilestoneDates_DerivedFromEventDate()
    {
        var eventDate = new DateOnly(2027, 6, 15);
        CreateProject(_customer.UserId, eventDate);

        var result = await Repo().GetRoadmapAsync(_customer.UserId, "WPSM-C03-001");

        Assert.NotNull(result);
        Assert.Equal(eventDate.AddDays(-90), result.MilestoneGroups.First(g => g.Milestone == "T-90").MilestoneDate);
        Assert.Equal(eventDate.AddDays(-30), result.MilestoneGroups.First(g => g.Milestone == "T-30").MilestoneDate);
        Assert.Equal(eventDate.AddDays(-7),  result.MilestoneGroups.First(g => g.Milestone == "T-7").MilestoneDate);
        Assert.Equal(eventDate,              result.MilestoneGroups.First(g => g.Milestone == "Event Day").MilestoneDate);
    }
}
