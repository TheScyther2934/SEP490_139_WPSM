using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Enums;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

/// <summary>
/// Unit tests for M-06 — Master Roadmap &amp; Tasks (Manager) according to Report 3 SRS Section 8 &amp; 9.
/// Covers all 10 mandatory test cases.
/// Uses EF Core In-Memory database.
/// </summary>
public sealed class ManagerRoadmapRepositoryTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly Hall _hall;
    private readonly Slot _slot;
    private readonly User _manager;
    private readonly User _otherManager;
    private readonly User _customer;

    public ManagerRoadmapRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WpsmDbContext(options);

        _hall = new Hall { HallId = 1, Name = "Sảnh Kim Cương", Status = "Active" };
        _slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Ca Tối (17:30 - 21:30)" };
        _manager = new User { UserId = 100, Email = "mgr1@test.vn", FullName = "Quản lý Nguyễn Văn A", Role = "Manager", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _otherManager = new User { UserId = 101, Email = "mgr2@test.vn", FullName = "Quản lý Trần Thị B", Role = "Manager", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _customer = new User { UserId = 200, Email = "customer@test.vn", FullName = "Khách hàng Lê Văn C", Role = "Customer", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };

        _db.Halls.Add(_hall);
        _db.Slots.Add(_slot);
        _db.Users.AddRange(_manager, _otherManager, _customer);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private WeddingProject CreateProject(int managerId, DateOnly eventDate, string code = "PRJ-M06-001")
    {
        var p = new WeddingProject
        {
            ProjectCode = code,
            CustomerId = _customer.UserId,
            ManagerId = managerId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = eventDate,
            BaseContractValue = 150_000_000m,
            Nrdamount = 45_000_000m,
            CurrentTotal = 150_000_000m,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        _db.WeddingProjects.Add(p);
        _db.SaveChanges();
        return p;
    }

    private RoadmapTask AddTask(int projectId, string milestone, string title, string role, DateOnly dueDate, string status = "Pending", DateTime? completedAt = null)
    {
        var t = new RoadmapTask
        {
            ProjectId = projectId,
            Milestone = milestone,
            Title = title,
            AssigneeRole = role,
            DueDate = dueDate,
            Status = status,
            CompletedAt = completedAt
        };
        _db.RoadmapTasks.Add(t);
        _db.SaveChanges();
        return t;
    }

    private ManagerRoadmapRepository Repo() => new(_db);

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 1 — Manager xem roadmap của dự án được phép quản lý
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test01_Manager_CanViewRoadmap_ForAuthorizedProject()
    {
        var eventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(90));
        var project = CreateProject(_manager.UserId, eventDate, "PRJ-M06-001");
        AddTask(project.ProjectId, "T-90", "Xác nhận phối hợp NCC", "Manager", DateOnly.FromDateTime(DateTime.Today.AddDays(10)));
        AddTask(project.ProjectId, "T-90", "Ký xác nhận hợp đồng", "Customer", DateOnly.FromDateTime(DateTime.Today.AddDays(5)));

        var result = await Repo().GetRoadmapAsync(_manager.UserId, "Manager", "PRJ-M06-001");

        Assert.NotNull(result);
        Assert.Equal("PRJ-M06-001", result.ProjectCode);
        Assert.Equal(_customer.FullName, result.CustomerName);
        Assert.Equal(_hall.Name, result.Hall);
        Assert.Equal(_slot.SlotName, result.Slot);
        Assert.Equal(eventDate, result.EventDate);
        Assert.True(result.CountdownDays > 0);
        Assert.Equal(2, result.TotalTasks);
        Assert.Equal(4, result.MilestoneGroups.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 2 — Manager không thể thao tác trên dự án không được phép
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test02_Manager_CannotAccess_UnauthorizedProject()
    {
        // Project belongs to _otherManager
        var project = CreateProject(_otherManager.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(60)), "PRJ-M06-OTHER");
        var task = AddTask(project.ProjectId, "T-30", "Chốt buổi thử thực đơn", "Manager", DateOnly.FromDateTime(DateTime.Today.AddDays(15)));

        // _manager tries to view roadmap
        var roadmap = await Repo().GetRoadmapAsync(_manager.UserId, "Manager", "PRJ-M06-OTHER");
        Assert.Null(roadmap);

        // _manager tries to complete task
        var completeResult = await Repo().CompleteTaskAsync(_manager.UserId, "Manager", "PRJ-M06-OTHER", task.TaskId);
        Assert.False(completeResult.Success);
        Assert.Contains("không có quyền", completeResult.Message);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 3 — Filter All trả về mọi task của dự án
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test03_FilterAll_ReturnsAllTasks()
    {
        var project = CreateProject(_manager.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        AddTask(project.ProjectId, "T-90", "Task Manager 1", "Manager", DateOnly.FromDateTime(DateTime.Today.AddDays(5)));
        AddTask(project.ProjectId, "T-90", "Task Customer 1", "Customer", DateOnly.FromDateTime(DateTime.Today.AddDays(10)));
        AddTask(project.ProjectId, "T-30", "Task Manager 2", "Manager", DateOnly.FromDateTime(DateTime.Today.AddDays(20)));

        var result = await Repo().GetRoadmapAsync(_manager.UserId, "Manager", project.ProjectCode, TaskOwnerFilter.All);

        Assert.NotNull(result);
        Assert.Equal(3, result.Tasks.Count);
        Assert.Contains(result.Tasks, t => t.Title == "Task Manager 1");
        Assert.Contains(result.Tasks, t => t.Title == "Task Customer 1");
        Assert.Contains(result.Tasks, t => t.Title == "Task Manager 2");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 4 — Filter Manager Tasks chỉ trả về task thuộc Manager
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test04_FilterManager_ReturnsOnlyManagerTasks()
    {
        var project = CreateProject(_manager.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        AddTask(project.ProjectId, "T-90", "Task Manager 1", "Manager", DateOnly.FromDateTime(DateTime.Today.AddDays(5)));
        AddTask(project.ProjectId, "T-90", "Task Customer 1", "Customer", DateOnly.FromDateTime(DateTime.Today.AddDays(10)));
        AddTask(project.ProjectId, "T-30", "Task Manager 2", "Manager", DateOnly.FromDateTime(DateTime.Today.AddDays(20)));

        var result = await Repo().GetRoadmapAsync(_manager.UserId, "Manager", project.ProjectCode, TaskOwnerFilter.Manager);

        Assert.NotNull(result);
        Assert.Equal(2, result.Tasks.Count);
        Assert.All(result.Tasks, t => Assert.Equal("Manager", t.AssigneeRole));
        Assert.DoesNotContain(result.Tasks, t => t.Title == "Task Customer 1");
        // Canonical milestone groups are still preserved (none lost)
        Assert.Equal(4, result.MilestoneGroups.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 5 — Filter Customer Tasks chỉ trả về task thuộc Customer
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test05_FilterCustomer_ReturnsOnlyCustomerTasks()
    {
        var project = CreateProject(_manager.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        AddTask(project.ProjectId, "T-90", "Task Manager 1", "Manager", DateOnly.FromDateTime(DateTime.Today.AddDays(5)));
        AddTask(project.ProjectId, "T-90", "Task Customer 1", "Customer", DateOnly.FromDateTime(DateTime.Today.AddDays(10)));
        AddTask(project.ProjectId, "T-30", "Task Customer 2", "Customer", DateOnly.FromDateTime(DateTime.Today.AddDays(25)));

        var result = await Repo().GetRoadmapAsync(_manager.UserId, "Manager", project.ProjectCode, TaskOwnerFilter.Customer);

        Assert.NotNull(result);
        Assert.Equal(2, result.Tasks.Count);
        Assert.All(result.Tasks, t => Assert.Equal("Customer", t.AssigneeRole));
        Assert.DoesNotContain(result.Tasks, t => t.Title == "Task Manager 1");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 6 — Manager hoàn thành Manager-owned task thành công
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test06_Manager_CompleteTask_Succeeds_ForManagerOwnedTask()
    {
        var project = CreateProject(_manager.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        var task = AddTask(project.ProjectId, "T-30", "Chốt buổi thử thực đơn", "Manager", DateOnly.FromDateTime(DateTime.Today.AddDays(15)));

        var result = await Repo().CompleteTaskAsync(_manager.UserId, "Manager", project.ProjectCode, task.TaskId);

        Assert.True(result.Success);
        Assert.NotNull(result.UpdatedTask);
        Assert.Equal("Done", result.UpdatedTask!.Status);
        Assert.NotNull(result.UpdatedTask.CompletedAt);

        // Verification in database
        var dbTask = _db.RoadmapTasks.Find(task.TaskId)!;
        Assert.Equal("Done", dbTask.Status);
        Assert.NotNull(dbTask.CompletedAt);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 7 — Manager không thể hoàn thành Customer-owned task
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test07_Manager_CannotComplete_CustomerOwnedTask()
    {
        var project = CreateProject(_manager.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        var customerTask = AddTask(project.ProjectId, "T-30", "Duyệt sơ đồ bàn", "Customer", DateOnly.FromDateTime(DateTime.Today.AddDays(15)));

        var result = await Repo().CompleteTaskAsync(_manager.UserId, "Manager", project.ProjectCode, customerTask.TaskId);

        Assert.False(result.Success);
        Assert.Null(result.UpdatedTask);
        Assert.Contains("Khách hàng", result.Message);

        // Verify task status in database was NOT changed
        var dbTask = _db.RoadmapTasks.Find(customerTask.TaskId)!;
        Assert.Equal("Pending", dbTask.Status);
        Assert.Null(dbTask.CompletedAt);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 8 — Task Overdue được hiển thị đúng
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test08_OverdueTasks_DisplayedCorrectly()
    {
        var project = CreateProject(_manager.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        // Task DueDate is 5 days ago, still Pending
        var pastDue = DateOnly.FromDateTime(DateTime.Today.AddDays(-5));
        var overdueTask = AddTask(project.ProjectId, "T-90", "Đặt hàng nguyên vật liệu", "Manager", pastDue, status: "Pending");

        var result = await Repo().GetRoadmapAsync(_manager.UserId, "Manager", project.ProjectCode);

        Assert.NotNull(result);
        var taskView = result.Tasks.First(t => t.TaskId == overdueTask.TaskId);
        Assert.Equal("Overdue", taskView.Status);
        Assert.True(result.OverdueTasks > 0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 9 — Task Done không bị chuyển lại Pending khi tải lại
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test09_TaskDone_PersistsDoneStatus_OnReload()
    {
        var pastDue = DateOnly.FromDateTime(DateTime.Today.AddDays(-10));
        var completedAt = DateTime.UtcNow.AddDays(-8);
        var project = CreateProject(_manager.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(90)));
        // Task is already Done even though DueDate was in the past
        var doneTask = AddTask(project.ProjectId, "T-90", "Duyệt hợp đồng", "Manager", pastDue, status: "Done", completedAt: completedAt);

        // Reload roadmap multiple times
        var result1 = await Repo().GetRoadmapAsync(_manager.UserId, "Manager", project.ProjectCode);
        var result2 = await Repo().GetRoadmapAsync(_manager.UserId, "Manager", project.ProjectCode);

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        var taskView1 = result1.Tasks.First(t => t.TaskId == doneTask.TaskId);
        var taskView2 = result2.Tasks.First(t => t.TaskId == doneTask.TaskId);

        // Must remain Done and not turn to Overdue or Pending
        Assert.Equal("Done", taskView1.Status);
        Assert.Equal("Done", taskView2.Status);
        Assert.Equal(completedAt.Date, taskView1.CompletedAt!.Value.Date);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 10 — Các task yêu cầu proof không thể bỏ qua quy trình minh chứng của FE-08
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test10_TaskRequiringProof_CannotBypassProofVerification()
    {
        var project = CreateProject(_manager.UserId, DateOnly.FromDateTime(DateTime.Today.AddDays(5)));
        // Task explicitly marked for proof-of-execution
        var proofTask = AddTask(project.ProjectId, "Event Day", "Chuẩn bị sảnh và kiểm tra âm thanh (Yêu cầu minh chứng)", "Manager",
            DateOnly.FromDateTime(DateTime.Today));

        // 1. Attempt complete WITHOUT providing proof photos -> Must fail
        var resultWithoutProof = await Repo().CompleteTaskAsync(_manager.UserId, "Manager", project.ProjectCode, proofTask.TaskId, proofPhotoPaths: null);

        Assert.False(resultWithoutProof.Success);
        Assert.Contains("minh chứng", resultWithoutProof.Message);
        Assert.Null(resultWithoutProof.UpdatedTask);

        // Database status must remain unchanged
        var dbTaskAfterFail = _db.RoadmapTasks.Find(proofTask.TaskId)!;
        Assert.Equal("Pending", dbTaskAfterFail.Status);

        // 2. Complete WITH 1-3 proof photos -> Must succeed
        var proofPhotos = new[] { "photos/sound_check_1.jpg", "photos/hall_check_2.jpg" };
        var resultWithProof = await Repo().CompleteTaskAsync(_manager.UserId, "Manager", project.ProjectCode, proofTask.TaskId, proofPhotoPaths: proofPhotos);

        Assert.True(resultWithProof.Success);
        Assert.NotNull(resultWithProof.UpdatedTask);
        Assert.Equal("Done", resultWithProof.UpdatedTask!.Status);

        // Database status must now be Done
        var dbTaskAfterSuccess = _db.RoadmapTasks.Find(proofTask.TaskId)!;
        Assert.Equal("Done", dbTaskAfterSuccess.Status);
        Assert.NotNull(dbTaskAfterSuccess.CompletedAt);
    }
}
