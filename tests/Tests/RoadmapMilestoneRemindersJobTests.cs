using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Application.Persistence;
using Application.Roadmap;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Jobs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Roadmap;
using Xunit;

namespace Tests;

public sealed class RoadmapMilestoneRemindersJobTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly INotificationRepository _notificationRepo;
    private readonly IEmailOutbox _emailOutbox;
    private readonly EmailOptions _emailOptions;
    private readonly RoadmapReminderService _service;
    private readonly User _customer;
    private readonly User _manager;
    private readonly Hall _hall;
    private readonly Slot _slot;
    private readonly WeddingProject _project;

    public RoadmapMilestoneRemindersJobTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WpsmDbContext(options);
        _notificationRepo = new NotificationRepository(_db);
        _emailOutbox = new EmailOutbox(_db);
        _emailOptions = new EmailOptions { PublicBaseUrl = "http://localhost:5173" };

        _service = new RoadmapReminderService(
            _db,
            _notificationRepo,
            _emailOutbox,
            _emailOptions,
            NullLogger<RoadmapReminderService>.Instance);

        _customer = new User
        {
            UserId = 1,
            Email = "customer@example.com",
            FullName = "Khách Hàng A",
            Role = "Customer",
            Status = "Active",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        _manager = new User
        {
            UserId = 2,
            Email = "manager@example.com",
            FullName = "Quản Lý B",
            Role = "Manager",
            Status = "Active",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        _hall = new Hall { HallId = 1, Name = "Sảnh Vàng", Status = "Active" };
        _slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Lunch" };

        _db.Users.AddRange(_customer, _manager);
        _db.Halls.Add(_hall);
        _db.Slots.Add(_slot);
        _db.SaveChanges();

        _project = new WeddingProject
        {
            ProjectId = 1,
            ProjectCode = "PRJ-TEST-001",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = new DateOnly(2026, 12, 1),
            BaseContractValue = 100_000_000m,
            Nrdamount = 30_000_000m,
            CurrentTotal = 100_000_000m,
            Status = "Baseline Frozen",
            CreatedAt = DateTime.UtcNow
        };
        _db.WeddingProjects.Add(_project);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SendMilestoneReminders_CreatesNotificationsAndQueuesEmails_ForTasksWithinWindow()
    {
        // Arrange: Today is 2026-11-07. Target date set to today.
        var today = new DateOnly(2026, 11, 7);

        // Task 1: Customer-owned, due today (Milestone T-30)
        var task1 = new RoadmapTask
        {
            TaskId = 10,
            ProjectId = _project.ProjectId,
            Milestone = "T-30",
            Title = "Xác nhận số lượng khách",
            AssigneeRole = "Customer",
            DueDate = today,
            Status = "Pending"
        };

        // Task 2: Manager-owned, due in 2 days (Milestone T-7)
        var task2 = new RoadmapTask
        {
            TaskId = 20,
            ProjectId = _project.ProjectId,
            Milestone = "T-7",
            Title = "Duyệt và đóng băng BEO",
            AssigneeRole = "Manager",
            DueDate = today.AddDays(2),
            Status = "Pending"
        };

        _db.RoadmapTasks.AddRange(task1, task2);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.SendMilestoneRemindersAsync(new SendRoadmapMilestoneRemindersCommand(today));

        // Assert
        Assert.Equal(2, result.TotalTasksFound);
        Assert.Equal(2, result.TotalRemindersCreated);
        Assert.Equal(2, result.TotalEmailsQueued);
        Assert.Equal(0, result.TotalSkipped);
        Assert.Equal(0, result.TotalFailed);

        // Verify task1 marked reminded
        var dbTask1 = await _db.RoadmapTasks.FindAsync(10);
        Assert.NotNull(dbTask1!.RemindedAt);

        // Verify task2 marked reminded
        var dbTask2 = await _db.RoadmapTasks.FindAsync(20);
        Assert.NotNull(dbTask2!.RemindedAt);

        // Verify notifications
        var notifications = await _db.Notifications.ToListAsync();
        Assert.Equal(2, notifications.Count);

        var custNotification = notifications.Single(n => n.RecipientId == _customer.UserId);
        Assert.Equal(NotificationTypes.RoadmapReminder, custNotification.Type);
        Assert.Contains("PRJ-TEST-001", custNotification.Message);
        Assert.Contains("T-30", custNotification.Message);
        Assert.Contains("hôm nay", custNotification.Message);
        Assert.Contains("(Task #10)", custNotification.Message);
        Assert.Equal("/customer/roadmap", custNotification.LinkUrl);

        var mgrNotification = notifications.Single(n => n.RecipientId == _manager.UserId);
        Assert.Equal(NotificationTypes.RoadmapReminder, mgrNotification.Type);
        Assert.Contains("PRJ-TEST-001", mgrNotification.Message);
        Assert.Contains("T-7", mgrNotification.Message);
        Assert.Contains("còn 2 ngày", mgrNotification.Message);
        Assert.Contains("(Task #20)", mgrNotification.Message);
        Assert.Equal("/manager/projects/PRJ-TEST-001/roadmap", mgrNotification.LinkUrl);

        // Verify queued emails
        var emails = await _db.EmailOutbox.ToListAsync();
        Assert.Equal(2, emails.Count);
        Assert.All(emails, e => Assert.Equal("Reminder", e.Kind));
        Assert.All(emails, e => Assert.Equal("Queued", e.Status));

        var custEmail = emails.Single(e => e.ToEmail == _customer.Email);
        Assert.Contains("PRJ-TEST-001", custEmail.Subject);
        Assert.Contains("T-30", custEmail.Subject);
        Assert.Contains("/customer/roadmap", custEmail.BodyHtml);

        var mgrEmail = emails.Single(e => e.ToEmail == _manager.Email);
        Assert.Contains("PRJ-TEST-001", mgrEmail.Subject);
        Assert.Contains("T-7", mgrEmail.Subject);
        Assert.Contains("/manager/projects/PRJ-TEST-001/roadmap", mgrEmail.BodyHtml);
    }

    [Fact]
    public async Task SendMilestoneReminders_ExcludesTasksOutsideWindow()
    {
        var today = new DateOnly(2026, 11, 7);

        // Task due in 4 days (outside 3-day window)
        var futureTask = new RoadmapTask
        {
            TaskId = 30,
            ProjectId = _project.ProjectId,
            Milestone = "T-90",
            Title = "Chọn chủ đề và màu sắc tiệc",
            AssigneeRole = "Customer",
            DueDate = today.AddDays(4),
            Status = "Pending"
        };

        // Task due yesterday (overdue)
        var pastTask = new RoadmapTask
        {
            TaskId = 31,
            ProjectId = _project.ProjectId,
            Milestone = "T-90",
            Title = "Rà soát và ký xác nhận hợp đồng gốc",
            AssigneeRole = "Customer",
            DueDate = today.AddDays(-1),
            Status = "Pending"
        };

        _db.RoadmapTasks.AddRange(futureTask, pastTask);
        await _db.SaveChangesAsync();

        var result = await _service.SendMilestoneRemindersAsync(new SendRoadmapMilestoneRemindersCommand(today));

        Assert.Equal(0, result.TotalTasksFound);
        Assert.Equal(0, result.TotalRemindersCreated);
        Assert.Empty(_db.Notifications);
        Assert.Empty(_db.EmailOutbox);
    }

    [Fact]
    public async Task SendMilestoneReminders_ExcludesCompletedAndDoneTasks()
    {
        var today = new DateOnly(2026, 11, 7);

        var doneTask = new RoadmapTask
        {
            TaskId = 40,
            ProjectId = _project.ProjectId,
            Milestone = "T-30",
            Title = "Đã hoàn thành",
            AssigneeRole = "Customer",
            DueDate = today,
            Status = "Done",
            CompletedAt = DateTime.UtcNow
        };

        _db.RoadmapTasks.Add(doneTask);
        await _db.SaveChangesAsync();

        var result = await _service.SendMilestoneRemindersAsync(new SendRoadmapMilestoneRemindersCommand(today));

        Assert.Equal(0, result.TotalTasksFound);
        Assert.Equal(0, result.TotalRemindersCreated);
    }

    [Fact]
    public async Task SendMilestoneReminders_ExcludesAlreadyRemindedTasks()
    {
        var today = new DateOnly(2026, 11, 7);

        var remindedTask = new RoadmapTask
        {
            TaskId = 50,
            ProjectId = _project.ProjectId,
            Milestone = "T-30",
            Title = "Đã gửi reminder trước đó",
            AssigneeRole = "Customer",
            DueDate = today,
            Status = "Pending",
            RemindedAt = DateTime.UtcNow.AddDays(-1)
        };

        _db.RoadmapTasks.Add(remindedTask);
        await _db.SaveChangesAsync();

        var result = await _service.SendMilestoneRemindersAsync(new SendRoadmapMilestoneRemindersCommand(today));

        Assert.Equal(0, result.TotalTasksFound);
        Assert.Equal(0, result.TotalRemindersCreated);
    }

    [Fact]
    public async Task SendMilestoneReminders_ExcludesInactiveOrClosedProjects()
    {
        var today = new DateOnly(2026, 11, 7);

        var closedProject = new WeddingProject
        {
            ProjectId = 2,
            ProjectCode = "PRJ-CLOSED",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = new DateOnly(2026, 11, 8),
            BaseContractValue = 50_000_000m,
            Nrdamount = 15_000_000m,
            CurrentTotal = 50_000_000m,
            Status = "Closed",
            CreatedAt = DateTime.UtcNow
        };
        _db.WeddingProjects.Add(closedProject);

        var taskInClosedProject = new RoadmapTask
        {
            TaskId = 60,
            ProjectId = closedProject.ProjectId,
            Milestone = "Event Day",
            Title = "Chuẩn bị sảnh",
            AssigneeRole = "Manager",
            DueDate = today,
            Status = "Pending"
        };
        _db.RoadmapTasks.Add(taskInClosedProject);
        await _db.SaveChangesAsync();

        var result = await _service.SendMilestoneRemindersAsync(new SendRoadmapMilestoneRemindersCommand(today));

        Assert.Equal(0, result.TotalTasksFound);
        Assert.Equal(0, result.TotalRemindersCreated);
    }

    [Fact]
    public async Task SendMilestoneReminders_InvalidOwner_SkipsTaskAndLogsWarning()
    {
        var today = new DateOnly(2026, 11, 7);

        // Project with an inactive customer
        var inactiveCustomer = new User
        {
            UserId = 99,
            Email = "inactive@test.vn",
            FullName = "Khách Bị Khóa",
            Role = "Customer",
            Status = "Inactive",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(inactiveCustomer);

        var projectWithInactiveOwner = new WeddingProject
        {
            ProjectId = 3,
            ProjectCode = "PRJ-INACTIVE",
            CustomerId = inactiveCustomer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = new DateOnly(2026, 12, 1),
            BaseContractValue = 100_000_000m,
            Nrdamount = 30_000_000m,
            CurrentTotal = 100_000_000m,
            Status = "Baseline Frozen",
            CreatedAt = DateTime.UtcNow
        };
        _db.WeddingProjects.Add(projectWithInactiveOwner);

        var task = new RoadmapTask
        {
            TaskId = 70,
            ProjectId = projectWithInactiveOwner.ProjectId,
            Milestone = "T-7",
            Title = "Xác nhận lịch trình ngày cưới",
            AssigneeRole = "Customer",
            DueDate = today,
            Status = "Pending"
        };
        _db.RoadmapTasks.Add(task);
        await _db.SaveChangesAsync();

        var result = await _service.SendMilestoneRemindersAsync(new SendRoadmapMilestoneRemindersCommand(today));

        Assert.Equal(1, result.TotalTasksFound);
        Assert.Equal(0, result.TotalRemindersCreated);
        Assert.Equal(1, result.TotalSkipped);
        Assert.Equal(0, result.TotalFailed);
        Assert.Empty(_db.Notifications);
    }

    [Fact]
    public async Task SendMilestoneReminders_Idempotency_RunningTwiceDoesNotDuplicate()
    {
        var today = new DateOnly(2026, 11, 7);

        var task = new RoadmapTask
        {
            TaskId = 80,
            ProjectId = _project.ProjectId,
            Milestone = "Event Day",
            Title = "Chuẩn bị sảnh và kiểm tra âm thanh",
            AssigneeRole = "Manager",
            DueDate = today,
            Status = "Pending"
        };
        _db.RoadmapTasks.Add(task);
        await _db.SaveChangesAsync();

        // First run
        var run1 = await _service.SendMilestoneRemindersAsync(new SendRoadmapMilestoneRemindersCommand(today));
        Assert.Equal(1, run1.TotalTasksFound);
        Assert.Equal(1, run1.TotalRemindersCreated);
        Assert.Equal(1, run1.TotalEmailsQueued);
        Assert.Single(_db.Notifications);
        Assert.Single(_db.EmailOutbox);

        // Second run immediately after
        var run2 = await _service.SendMilestoneRemindersAsync(new SendRoadmapMilestoneRemindersCommand(today));
        Assert.Equal(0, run2.TotalTasksFound);
        Assert.Equal(0, run2.TotalRemindersCreated);
        Assert.Equal(0, run2.TotalEmailsQueued);

        // Database remains with exactly 1 notification and 1 email
        Assert.Single(_db.Notifications);
        Assert.Single(_db.EmailOutbox);
    }

    [Fact]
    public async Task SendMilestoneReminders_EmailFailure_DoesNotFailReminder()
    {
        var today = new DateOnly(2026, 11, 7);

        var task = new RoadmapTask
        {
            TaskId = 90,
            ProjectId = _project.ProjectId,
            Milestone = "T-30",
            Title = "Chốt buổi thử thực đơn",
            AssigneeRole = "Manager",
            DueDate = today.AddDays(1),
            Status = "Pending"
        };
        _db.RoadmapTasks.Add(task);
        await _db.SaveChangesAsync();

        // Create service with a failing email outbox
        var failingEmailOutbox = new FailingEmailOutbox();
        var customService = new RoadmapReminderService(
            _db,
            _notificationRepo,
            failingEmailOutbox,
            _emailOptions,
            NullLogger<RoadmapReminderService>.Instance);

        var result = await customService.SendMilestoneRemindersAsync(new SendRoadmapMilestoneRemindersCommand(today));

        // In-app notification succeeded even though email queue threw
        Assert.Equal(1, result.TotalTasksFound);
        Assert.Equal(1, result.TotalRemindersCreated);
        Assert.Equal(0, result.TotalEmailsQueued);
        Assert.Equal(0, result.TotalFailed);

        var savedTask = await _db.RoadmapTasks.FindAsync(90);
        Assert.NotNull(savedTask!.RemindedAt);
        Assert.Single(_db.Notifications);
    }

    [Fact]
    public async Task SendRoadmapMilestoneRemindersJob_ExecutesSuccessfully()
    {
        var job = new SendRoadmapMilestoneRemindersJob(_service, NullLogger<SendRoadmapMilestoneRemindersJob>.Instance);
        await job.RunAsync(CancellationToken.None);
        // Verify completes without unhandled exception
    }

    private sealed class FailingEmailOutbox : IEmailOutbox
    {
        public void Enqueue(string toEmail, string subject, string bodyHtml, string kind, int? projectId)
        {
            throw new InvalidOperationException("Email queue unavailable (simulated outage).");
        }
    }
}
