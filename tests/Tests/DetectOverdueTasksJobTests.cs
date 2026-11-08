using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Application.OverdueAlerts;
using Application.Persistence;
using Domain.Enums;
using Infrastructure.Email;
using Infrastructure.OverdueAlerts;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

public sealed class DetectOverdueTasksJobTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly INotificationRepository _notificationRepo;
    private readonly IEmailOutbox _emailOutbox;
    private readonly EmailOptions _emailOptions;
    private readonly DetectOverdueTasksService _service;

    private readonly User _customer;
    private readonly User _manager;
    private readonly User _admin;
    private readonly Hall _hall;
    private readonly Slot _slot;

    public DetectOverdueTasksJobTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WpsmDbContext(options);
        _notificationRepo = new NotificationRepository(_db);
        _emailOutbox = new EmailOutbox(_db);
        _emailOptions = new EmailOptions { PublicBaseUrl = "http://localhost:5173" };

        _service = new DetectOverdueTasksService(
            _db,
            _notificationRepo,
            _emailOutbox,
            _emailOptions,
            NullLogger<DetectOverdueTasksService>.Instance);

        _customer = new User
        {
            UserId = 10,
            Email = "customer@example.com",
            FullName = "Khách Hàng Test",
            Role = "Customer",
            Status = "Active",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };
        _manager = new User
        {
            UserId = 20,
            Email = "manager@example.com",
            FullName = "Quản Lý Test",
            Role = "Manager",
            Status = "Active",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };
        _admin = new User
        {
            UserId = 30,
            Email = "admin@example.com",
            FullName = "Admin Test",
            Role = "Admin",
            Status = "Active",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };
        _hall = new Hall { HallId = 1, Name = "Sảnh Kim Cương", MinCapacity = 200, MaxCapacity = 400, BasePrice = 50_000_000, Status = "Active" };
        _slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Lunch", StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(14, 0) };

        _db.Users.AddRange(_customer, _manager, _admin);
        _db.Halls.Add(_hall);
        _db.Slots.Add(_slot);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task FastSkip_WhenNoWeddingEventsToday_SkipsImmediately()
    {
        var targetDate = new DateOnly(2026, 11, 9);

        // Project event date is next month
        var project = new WeddingProject
        {
            ProjectId = 1,
            ProjectCode = "WPSM-2026-FUTURE",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = targetDate.AddDays(30),
            BaseContractValue = 100_000_000,
            Nrdamount = 30_000_000,
            CurrentTotal = 100_000_000,
            Status = "Baseline Frozen",
            CreatedAt = DateTime.UtcNow
        };
        _db.WeddingProjects.Add(project);
        await _db.SaveChangesAsync();

        var result = await _service.DetectOverdueTasksAsync(
            new DetectOverdueTasksCommand(TargetDate: targetDate, CurrentUtc: DateTime.UtcNow));

        Assert.False(result.HasEventToday);
        Assert.Equal(0, result.TotalTasksChecked);
        Assert.Equal(0, result.TotalOverdueDetected);
        Assert.Equal(0, result.TotalNotificationsCreated);
    }

    [Fact]
    public async Task DetectOverdueTasks_WhenTaskOverdue_MarksOverdueAndNotifiesManagerAndAdmins()
    {
        var targetDate = new DateOnly(2026, 11, 9);
        var nowUtc = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

        var project = new WeddingProject
        {
            ProjectId = 2,
            ProjectCode = "WPSM-2026-TODAY",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = targetDate,
            BaseContractValue = 120_000_000,
            Nrdamount = 36_000_000,
            CurrentTotal = 120_000_000,
            Status = "Baseline Frozen",
            CreatedAt = DateTime.UtcNow
        };
        _db.WeddingProjects.Add(project);

        var taskOverdue = new DayOfTask
        {
            DayOfTaskId = 101,
            ProjectId = 2,
            TaskName = "Kiểm tra set-up âm thanh",
            DeadlineTime = nowUtc.AddMinutes(-30), // Passed deadline
            Status = "Pending",
            PhotoRequired = true
        };
        var taskPendingFuture = new DayOfTask
        {
            DayOfTaskId = 102,
            ProjectId = 2,
            TaskName = "Khai tiệc",
            DeadlineTime = nowUtc.AddMinutes(45), // Future deadline
            Status = "Pending",
            PhotoRequired = false
        };

        _db.DayOfTasks.AddRange(taskOverdue, taskPendingFuture);
        await _db.SaveChangesAsync();

        var result = await _service.DetectOverdueTasksAsync(
            new DetectOverdueTasksCommand(TargetDate: targetDate, CurrentUtc: nowUtc));

        Assert.True(result.HasEventToday);
        Assert.Equal(1, result.TotalTasksChecked); // Only taskOverdue had deadline < now
        Assert.Equal(1, result.TotalOverdueDetected);
        Assert.Equal(2, result.TotalNotificationsCreated); // 1 for Manager, 1 for Admin
        Assert.Equal(2, result.TotalEmailsQueued);

        // Verify task state in database
        var updatedTask = await _db.DayOfTasks.FindAsync(101);
        Assert.NotNull(updatedTask);
        Assert.Equal("Overdue", updatedTask.Status);

        // Verify future task untouched
        var futureTask = await _db.DayOfTasks.FindAsync(102);
        Assert.NotNull(futureTask);
        Assert.Equal("Pending", futureTask.Status);

        // Verify notifications in DB
        var notifications = await _db.Notifications.ToListAsync();
        Assert.Contains(notifications, n => n.RecipientId == _manager.UserId && n.Type == NotificationTypes.OverdueAlert);
        Assert.Contains(notifications, n => n.RecipientId == _admin.UserId && n.Type == NotificationTypes.OverdueAlert);
        // Verify Customer was NEVER notified
        Assert.DoesNotContain(notifications, n => n.RecipientId == _customer.UserId);
    }

    [Fact]
    public async Task DetectOverdueTasks_Idempotency_RunningTwiceDoesNotDuplicateAlertOrNotification()
    {
        var targetDate = new DateOnly(2026, 11, 9);
        var nowUtc = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

        var project = new WeddingProject
        {
            ProjectId = 3,
            ProjectCode = "WPSM-2026-IDEM",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = targetDate,
            BaseContractValue = 100_000_000,
            Nrdamount = 30_000_000,
            CurrentTotal = 100_000_000,
            Status = "Baseline Frozen",
            CreatedAt = DateTime.UtcNow
        };
        _db.WeddingProjects.Add(project);

        var task = new DayOfTask
        {
            DayOfTaskId = 201,
            ProjectId = 3,
            TaskName = "Nhiệm vụ kiểm tra lặp",
            DeadlineTime = nowUtc.AddMinutes(-20),
            Status = "Pending"
        };
        _db.DayOfTasks.Add(task);
        await _db.SaveChangesAsync();

        // First run
        var firstRun = await _service.DetectOverdueTasksAsync(
            new DetectOverdueTasksCommand(TargetDate: targetDate, CurrentUtc: nowUtc));
        Assert.Equal(1, firstRun.TotalOverdueDetected);
        Assert.Equal(2, firstRun.TotalNotificationsCreated);

        // Second run
        var secondRun = await _service.DetectOverdueTasksAsync(
            new DetectOverdueTasksCommand(TargetDate: targetDate, CurrentUtc: nowUtc.AddMinutes(5)));
        Assert.Equal(0, secondRun.TotalOverdueDetected);
        Assert.Equal(0, secondRun.TotalNotificationsCreated);

        // Total notifications in DB should still be 2 (1 manager + 1 admin)
        var totalNotifications = await _db.Notifications.CountAsync(n => n.Type == NotificationTypes.OverdueAlert);
        Assert.Equal(2, totalNotifications);
    }
}
