using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.Dashboard;
using Infrastructure.Dashboard;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Xunit;

namespace Tests;

public sealed class ManagerDashboardTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly ManagerDashboardService _service;

    private readonly User _managerA;
    private readonly User _managerB;
    private readonly User _customer;
    private readonly Hall _hall;
    private readonly Slot _slot;

    public ManagerDashboardTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WpsmDbContext(options);
        _service = new ManagerDashboardService(_db);

        _managerA = new User { UserId = 1, Email = "mgrA@example.com", FullName = "Quản Lý A", Role = "Manager", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };
        _managerB = new User { UserId = 2, Email = "mgrB@example.com", FullName = "Quản Lý B", Role = "Manager", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };
        _customer = new User { UserId = 3, Email = "cust@example.com", FullName = "Khách Hàng", Role = "Customer", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };

        _hall = new Hall { HallId = 1, Name = "Sảnh Kim Cương", MinCapacity = 200, MaxCapacity = 400, BasePrice = 50_000_000, Status = "Active" };
        _slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Lunch", StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(14, 0) };

        _db.Users.AddRange(_managerA, _managerB, _customer);
        _db.Halls.Add(_hall);
        _db.Slots.Add(_slot);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task GetDashboard_StrictlyScopesToManagerProjects_ExcludesOtherManagers()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Project belonging to Manager A
        var projA = new WeddingProject
        {
            ProjectId = 1,
            ProjectCode = "WPSM-PROJ-A",
            CustomerId = _customer.UserId,
            ManagerId = _managerA.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = today.AddDays(5),
            BaseContractValue = 100_000_000,
            Nrdamount = 30_000_000,
            CurrentTotal = 100_000_000,
            Status = "Baseline Frozen",
            CreatedAt = DateTime.UtcNow
        };

        // Project belonging to Manager B
        var projB = new WeddingProject
        {
            ProjectId = 2,
            ProjectCode = "WPSM-PROJ-B",
            CustomerId = _customer.UserId,
            ManagerId = _managerB.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = today.AddDays(2),
            BaseContractValue = 150_000_000,
            Nrdamount = 45_000_000,
            CurrentTotal = 150_000_000,
            Status = "Active Baseline",
            CreatedAt = DateTime.UtcNow
        };

        _db.WeddingProjects.AddRange(projA, projB);

        // Overdue task on Manager A project
        _db.DayOfTasks.Add(new DayOfTask
        {
            DayOfTaskId = 1,
            ProjectId = projA.ProjectId,
            TaskName = "Task Overdue A",
            DeadlineTime = DateTime.UtcNow.AddMinutes(-30),
            Status = "Overdue"
        });

        // Overdue task on Manager B project
        _db.DayOfTasks.Add(new DayOfTask
        {
            DayOfTaskId = 2,
            ProjectId = projB.ProjectId,
            TaskName = "Task Overdue B",
            DeadlineTime = DateTime.UtcNow.AddMinutes(-40),
            Status = "Overdue"
        });

        // Stale BEO on Manager A project
        _db.Beos.Add(new Beo
        {
            Beoid = 10,
            ProjectId = projA.ProjectId,
            Version = 2,
            Status = "Stale",
            Notes = "Cần cập nhật món ăn"
        });

        // Stale BEO on Manager B project
        _db.Beos.Add(new Beo
        {
            Beoid = 20,
            ProjectId = projB.ProjectId,
            Version = 1,
            Status = "Stale"
        });

        // Roadmap task for Manager A
        _db.RoadmapTasks.Add(new RoadmapTask
        {
            TaskId = 101,
            ProjectId = projA.ProjectId,
            Milestone = "T-30",
            Title = "Soạn BEO Manager A",
            AssigneeRole = "Manager",
            DueDate = today.AddDays(10),
            Status = "Pending"
        });

        // Customer task on Project A (must be excluded from My Tasks)
        _db.RoadmapTasks.Add(new RoadmapTask
        {
            TaskId = 102,
            ProjectId = projA.ProjectId,
            Milestone = "T-30",
            Title = "Khách chọn menu",
            AssigneeRole = "Customer",
            DueDate = today.AddDays(10),
            Status = "Pending"
        });

        await _db.SaveChangesAsync();

        // Query dashboard for Manager A
        var dashboardA = await _service.GetDashboardAsync(_managerA.UserId);

        // Verification of Manager A
        Assert.Equal(1, dashboardA.Kpis.ActiveProjects);
        Assert.Equal(0, dashboardA.Kpis.DraftingProjects);
        Assert.Equal(1, dashboardA.Kpis.EventsIn7Days);
        Assert.Equal(1, dashboardA.Kpis.OverdueTasksCount);
        Assert.Equal(1, dashboardA.Kpis.StaleBeos);

        // AlertPanel
        Assert.Single(dashboardA.OverdueTasks);
        Assert.Equal("WPSM-PROJ-A", dashboardA.OverdueTasks[0].ProjectCode);
        Assert.Single(dashboardA.StaleBeos);
        Assert.Equal("WPSM-PROJ-A", dashboardA.StaleBeos[0].ProjectCode);

        // MyTasks must only contain Manager A's tasks, never Customer or Manager B tasks
        Assert.Single(dashboardA.MyTasks);
        Assert.Equal("Soạn BEO Manager A", dashboardA.MyTasks[0].Title);
        Assert.Equal("WPSM-PROJ-A", dashboardA.MyTasks[0].ProjectCode);
    }
}
