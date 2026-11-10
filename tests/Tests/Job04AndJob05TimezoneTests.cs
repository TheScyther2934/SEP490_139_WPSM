using System;
using System.Linq;
using System.Threading.Tasks;
using Application.DayOfTasks;
using Application.Events;
using Application.OverdueAlerts;
using Application.Persistence;
using Domain.Enums;
using Domain.Events;
using Domain.Services;
using Infrastructure.DayOfTasks;
using Infrastructure.Email;
using Infrastructure.Events;
using Infrastructure.OverdueAlerts;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Tests;

public sealed class Job04AndJob05TimezoneTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly DetectOverdueTasksService _job05Service;
    private readonly LoadDayOfTimelineService _job04Service;
    private readonly User _manager;
    private readonly User _admin;
    private readonly User _customer;
    private readonly Hall _hall;
    private readonly Slot _slot;

    public Job04AndJob05TimezoneTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WpsmDbContext(options);

        var notifRepo = new NotificationRepository(_db);
        var emailOutbox = new EmailOutbox(_db);
        var emailOptions = new EmailOptions { PublicBaseUrl = "http://localhost:5173" };

        var handler = new DayOfTaskOverdueEventHandler(
            _db,
            notifRepo,
            emailOutbox,
            emailOptions,
            NullLogger<DayOfTaskOverdueEventHandler>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<DayOfTaskOverdueEvent>>(handler);
        var sp = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(sp, NullLogger<DomainEventDispatcher>.Instance);

        _job05Service = new DetectOverdueTasksService(
            _db,
            dispatcher,
            NullLogger<DetectOverdueTasksService>.Instance);

        _job04Service = new LoadDayOfTimelineService(
            _db,
            NullLogger<LoadDayOfTimelineService>.Instance);

        _manager = new User { UserId = 1, Email = "mgr@test.com", FullName = "Manager 1", Role = "Manager", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };
        _admin = new User { UserId = 2, Email = "admin@test.com", FullName = "Admin 1", Role = "Admin", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };
        _customer = new User { UserId = 3, Email = "cust@test.com", FullName = "Customer 1", Role = "Customer", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };

        _hall = new Hall { HallId = 1, Name = "Sảnh Thảo Ngọc", MinCapacity = 200, MaxCapacity = 400, BasePrice = 40_000_000, Status = "Active" };
        _slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Trưa", StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(14, 0) };

        _db.Users.AddRange(_manager, _admin, _customer);
        _db.Halls.Add(_hall);
        _db.Slots.Add(_slot);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void RoadmapRules_VietnamTimezoneConversions_AreAccurateAndPreserveExactMoments()
    {
        // 11:00 AM on 2026-11-10 in Vietnam (UTC+7) is 04:00 AM UTC on 2026-11-10
        var vnDate = new DateOnly(2026, 11, 10);
        var vnTime = new TimeOnly(11, 0);

        var utcTime = RoadmapRules.ConvertVietnamToUtc(vnDate, vnTime);
        Assert.Equal(DateTimeKind.Utc, utcTime.Kind);
        Assert.Equal(new DateTime(2026, 11, 10, 4, 0, 0, DateTimeKind.Utc), utcTime);

        // Convert back to Vietnam time
        var backToVn = RoadmapRules.ConvertUtcToVietnam(utcTime);
        Assert.Equal(new DateTime(2026, 11, 10, 11, 0, 0), backToVn);
    }

    [Fact]
    public void RoadmapRules_MidnightRollover_ConvertsCorrectly()
    {
        // 02:00 AM on 2026-11-10 in Vietnam (UTC+7) is 19:00 PM UTC on the PREVIOUS day (2026-11-09)
        var vnDate = new DateOnly(2026, 11, 10);
        var vnTime = new TimeOnly(2, 0);

        var utcTime = RoadmapRules.ConvertVietnamToUtc(vnDate, vnTime);
        Assert.Equal(new DateTime(2026, 11, 9, 19, 0, 0, DateTimeKind.Utc), utcTime);

        var backToVn = RoadmapRules.ConvertUtcToVietnam(utcTime);
        Assert.Equal(new DateTime(2026, 11, 10, 2, 0, 0), backToVn);
    }

    [Fact]
    public async Task Job05_DetectOverdue_UsesNormalizedUtcComparisonWithoutSevenHourLag()
    {
        var todayVn = new DateOnly(2026, 11, 10);

        // Project taking place today
        var project = new WeddingProject
        {
            ProjectId = 10,
            ProjectCode = "WPSM-TZ-TEST",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = todayVn,
            BaseContractValue = 100_000_000,
            Nrdamount = 30_000_000,
            CurrentTotal = 100_000_000,
            Status = "Baseline Frozen",
            CreatedAt = DateTime.UtcNow
        };
        _db.WeddingProjects.Add(project);

        // Task deadline: 10:00 AM Vietnam time = 03:00 UTC
        var deadlineUtc = RoadmapRules.ConvertVietnamToUtc(todayVn, new TimeOnly(10, 0));

        var task = new DayOfTask
        {
            DayOfTaskId = 501,
            ProjectId = project.ProjectId,
            TaskName = "Set up âm thanh ánh sáng",
            DeadlineTime = deadlineUtc,
            Status = "Pending",
            PhotoRequired = true
        };
        _db.DayOfTasks.Add(task);
        await _db.SaveChangesAsync();

        // 1. Current time is 09:55 AM Vietnam time (02:55 UTC) -> NOT overdue
        var beforeDeadlineUtc = RoadmapRules.ConvertVietnamToUtc(todayVn, new TimeOnly(9, 55));
        var resultBefore = await _job05Service.DetectOverdueTasksAsync(
            new DetectOverdueTasksCommand(TargetDate: todayVn, CurrentUtc: beforeDeadlineUtc));

        Assert.Equal(0, resultBefore.TotalOverdueDetected);
        var taskCheck1 = await _db.DayOfTasks.FindAsync(501);
        Assert.Equal("Pending", taskCheck1!.Status);

        // 2. Current time is 10:02 AM Vietnam time (03:02 UTC) -> IMMEDIATELY Overdue (no 7h lag!)
        var afterDeadlineUtc = RoadmapRules.ConvertVietnamToUtc(todayVn, new TimeOnly(10, 2));
        var resultAfter = await _job05Service.DetectOverdueTasksAsync(
            new DetectOverdueTasksCommand(TargetDate: todayVn, CurrentUtc: afterDeadlineUtc));

        Assert.Equal(1, resultAfter.TotalOverdueDetected);
        var taskCheck2 = await _db.DayOfTasks.FindAsync(501);
        Assert.Equal("Overdue", taskCheck2!.Status);
        Assert.NotNull(taskCheck2.OverdueFlaggedAt);
    }

    [Fact]
    public async Task Job04_LoadDayOfTimeline_CreatesTasksFromFrozenBeo_WithIdempotency()
    {
        var targetDate = new DateOnly(2026, 11, 15);

        var project = new WeddingProject
        {
            ProjectId = 20,
            ProjectCode = "WPSM-JOB04-TEST",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = targetDate,
            BaseContractValue = 120_000_000,
            Nrdamount = 36_000_000,
            CurrentTotal = 120_000_000,
            Status = "Baseline Frozen",
            CreatedAt = DateTime.UtcNow,
            Beos =
            {
                new Beo
                {
                    Beoid = 201,
                    Version = 1,
                    Status = "Frozen",
                    FrozenAt = DateTime.UtcNow.AddDays(-2)
                }
            }
        };
        _db.WeddingProjects.Add(project);
        await _db.SaveChangesAsync();

        // First execution: loads 4 operational tasks
        var loadResult = await _job04Service.LoadTimelineAsync(
            new LoadDayOfTimelineCommand(ProjectId: 20));

        Assert.Equal(1, loadResult.ProjectsProcessed);
        Assert.Equal(4, loadResult.TasksCreated);

        var tasks = await _db.DayOfTasks.Where(t => t.ProjectId == 20).OrderBy(t => t.DeadlineTime).ToListAsync();
        Assert.Equal(4, tasks.Count);
        Assert.Equal("Pending", tasks[0].Status);
        Assert.True(tasks[0].PhotoRequired);

        // Deadlines must be in UTC
        Assert.All(tasks, t => Assert.Equal(DateTimeKind.Utc, t.DeadlineTime.Kind));

        // Second execution: idempotency test (must skip, no duplicate tasks)
        var secondRun = await _job04Service.LoadTimelineAsync(
            new LoadDayOfTimelineCommand(ProjectId: 20));

        Assert.Equal(0, secondRun.TasksCreated);
        Assert.Equal(1, secondRun.SkippedProjects);

        var tasksAfter = await _db.DayOfTasks.Where(t => t.ProjectId == 20).CountAsync();
        Assert.Equal(4, tasksAfter);
    }
}
