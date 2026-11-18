using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Application.Payments;
using Application.Persistence;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Jobs;
using Infrastructure.Payments;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

public sealed class FinalSettlementJobTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly INotificationRepository _notificationRepo;
    private readonly IEmailOutbox _emailOutbox;
    private readonly EmailOptions _emailOptions;
    private readonly FinalSettlementService _service;
    private readonly User _customer;
    private readonly User _manager;
    private readonly Hall _hall;
    private readonly Slot _slot;

    public FinalSettlementJobTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WpsmDbContext(options);
        _notificationRepo = new NotificationRepository(_db);
        _emailOutbox = new EmailOutbox(_db);
        _emailOptions = new EmailOptions { PublicBaseUrl = "http://localhost:5173" };

        _service = new FinalSettlementService(
            _db,
            _notificationRepo,
            _emailOutbox,
            _emailOptions,
            NullLogger<FinalSettlementService>.Instance);

        _customer = new User
        {
            UserId = 1,
            Email = "customer@wedding.vn",
            FullName = "Nguyễn Văn Chú Rể",
            Role = "Customer",
            Status = "Active",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        _manager = new User
        {
            UserId = 2,
            Email = "manager@wedding.vn",
            FullName = "Lê Thị Quản Lý",
            Role = "Manager",
            Status = "Active",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        _hall = new Hall { HallId = 1, Name = "Sảnh Hoàng Kim", Status = "Active" };
        _slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Evening" };

        _db.Users.AddRange(_customer, _manager);
        _db.Halls.Add(_hall);
        _db.Slots.Add(_slot);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private WeddingProject CreateProject(
        string code,
        DateOnly eventDate,
        decimal baseValue = 100_000_000m,
        decimal nrd = 30_000_000m,
        string status = "Baseline Frozen",
        bool hasBaseline = true)
    {
        var p = new WeddingProject
        {
            ProjectCode = code,
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = eventDate,
            BaseContractValue = baseValue,
            Nrdamount = nrd,
            CurrentTotal = baseValue,
            Status = status,
            BaselineFrozenAt = hasBaseline ? DateTime.UtcNow.AddDays(-100) : null,
            CreatedAt = DateTime.UtcNow.AddDays(-100)
        };
        _db.WeddingProjects.Add(p);
        _db.SaveChanges();

        // Recorded NRD payment
        var nrdPayment = new Payment
        {
            ProjectId = p.ProjectId,
            PaymentType = "NRD",
            Status = "Paid",
            DueDate = eventDate.AddDays(-90),
            Amount = nrd,
            PaymentMethod = "Bank",
            PaidDate = eventDate.AddDays(-90),
            RecordedBy = _manager.UserId
        };
        _db.Payments.Add(nrdPayment);
        _db.SaveChanges();

        return p;
    }

    [Fact]
    public async Task PrepareFinalSettlement_EventYesterday_SetsEventCompleted_CreatesFinalMilestone_NotifiesManager()
    {
        // Arrange: Today is 2026-11-07 -> Yesterday was 2026-11-06
        var today = new DateOnly(2026, 11, 7);
        var yesterday = today.AddDays(-1);

        var project = CreateProject("PRJ-SETTLE-001", yesterday, baseValue: 100_000_000m, nrd: 30_000_000m);

        // Add T30 payment paid = 40M
        _db.Payments.Add(new Payment
        {
            ProjectId = project.ProjectId,
            PaymentType = "T30",
            Status = "Paid",
            DueDate = yesterday.AddDays(-30),
            Amount = 40_000_000m,
            PaymentMethod = "Bank",
            PaidDate = yesterday.AddDays(-30),
            RecordedBy = _manager.UserId
        });

        // Add Approved CR = +10M
        _db.ChangeRequests.Add(new ChangeRequest
        {
            Crcode = "CR-01",
            ProjectId = project.ProjectId,
            CreatedBy = _customer.UserId,
            Type = "ScopeAddition",
            Description = "Thêm trang trí bàn gallery",
            CostImpact = 10_000_000m,
            PenaltyFee = 0m,
            TotalVariance = 10_000_000m,
            Status = "CustomerApproved",
            CreatedAt = DateTime.UtcNow.AddDays(-15)
        });
        await _db.SaveChangesAsync();

        // Total = 100M + 10M = 110M. Paid = 30M + 40M = 70M. Final Due = 40M.

        // Act
        var result = await _service.PrepareFinalSettlementsAsync(new PrepareFinalSettlementCommand(today));

        // Assert
        Assert.Equal(1, result.TotalProjectsFound);
        Assert.Equal(1, result.TotalEventsCompleted);
        Assert.Equal(1, result.TotalSettlementsPrepared);
        Assert.Equal(1, result.TotalFinalMilestonesCreated);
        Assert.Equal(1, result.TotalNotificationsCreated);
        Assert.Equal(1, result.TotalEmailsQueued);
        Assert.Equal(0, result.TotalProjectsSkipped);
        Assert.Equal(0, result.TotalFailed);

        // Verify Project status transitioned to "Event Completed" and NOT "Settled"
        var dbProject = await _db.WeddingProjects.FindAsync(project.ProjectId);
        Assert.Equal("Event Completed", dbProject!.Status);
        Assert.Equal(110_000_000m, dbProject.CurrentTotal);

        // Verify FINAL Payment created
        var finalPayment = await _db.Payments
            .FirstOrDefaultAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == "FINAL");

        Assert.NotNull(finalPayment);
        Assert.Equal("Due", finalPayment!.Status);
        Assert.Equal(today, finalPayment.DueDate);
        Assert.Equal(40_000_000m, finalPayment.Amount);

        // Verify Manager Notification
        var notification = await _db.Notifications.SingleAsync();
        Assert.Equal(_manager.UserId, notification.RecipientId);
        Assert.Equal(NotificationTypes.Payment, notification.Type);
        Assert.Contains("PRJ-SETTLE-001", notification.Message);
        Assert.Contains("Nguyễn Văn Chú Rể", notification.Message);
        Assert.Contains("110,000,000", notification.Message);
        Assert.Contains("70,000,000", notification.Message);
        Assert.Contains("40,000,000", notification.Message);
        Assert.Equal("/manager/projects/PRJ-SETTLE-001/payments", notification.LinkUrl);

        // Verify Email in outbox
        var email = await _db.EmailOutbox.SingleAsync();
        Assert.Equal(_manager.Email, email.ToEmail);
        Assert.Equal("PaymentRequest", email.Kind);
        Assert.Contains("PRJ-SETTLE-001", email.Subject);
        Assert.Contains("40,000,000", email.BodyHtml);
        Assert.Contains("/manager/projects/PRJ-SETTLE-001/payments", email.BodyHtml);
    }

    [Fact]
    public async Task PrepareFinalSettlement_EventNotYesterday_Ignored()
    {
        var today = new DateOnly(2026, 11, 7);
        // Event was 2 days ago or is today
        CreateProject("PRJ-PAST", today.AddDays(-2));
        CreateProject("PRJ-TODAY", today);

        var result = await _service.PrepareFinalSettlementsAsync(new PrepareFinalSettlementCommand(today));

        Assert.Equal(0, result.TotalProjectsFound);
        Assert.Equal(0, result.TotalSettlementsPrepared);
        Assert.Empty(_db.Notifications);
        Assert.Empty(_db.EmailOutbox);
    }

    [Fact]
    public async Task PrepareFinalSettlement_ProjectWithoutBaseline_Ignored()
    {
        var today = new DateOnly(2026, 11, 7);
        CreateProject("PRJ-NO-BASELINE", today.AddDays(-1), hasBaseline: false);

        var result = await _service.PrepareFinalSettlementsAsync(new PrepareFinalSettlementCommand(today));

        Assert.Equal(0, result.TotalProjectsFound);
        Assert.Equal(0, result.TotalSettlementsPrepared);
    }

    [Fact]
    public async Task PrepareFinalSettlement_InactiveOrSettledProjects_Ignored()
    {
        var today = new DateOnly(2026, 11, 7);
        CreateProject("PRJ-SETTLED", today.AddDays(-1), status: "Settled");
        CreateProject("PRJ-CLOSED", today.AddDays(-1), status: "Closed");

        var result = await _service.PrepareFinalSettlementsAsync(new PrepareFinalSettlementCommand(today));

        Assert.Equal(0, result.TotalProjectsFound);
    }

    [Fact]
    public async Task PrepareFinalSettlement_ExistingFinalMilestone_UpdatedWithoutDuplication()
    {
        var today = new DateOnly(2026, 11, 7);
        var yesterday = today.AddDays(-1);
        var project = CreateProject("PRJ-UPDATE-FINAL", yesterday, baseValue: 100_000_000m, nrd: 30_000_000m);

        // Pre-existing FINAL milestone with estimated amount or Upcoming
        _db.Payments.Add(new Payment
        {
            ProjectId = project.ProjectId,
            PaymentType = "FINAL",
            Status = "Upcoming",
            Amount = 10_000_000m,
            DueDate = yesterday
        });
        await _db.SaveChangesAsync();

        var result = await _service.PrepareFinalSettlementsAsync(new PrepareFinalSettlementCommand(today));

        Assert.Equal(1, result.TotalProjectsFound);
        Assert.Equal(0, result.TotalFinalMilestonesCreated); // Already existed, so 0 created
        Assert.Equal(1, result.TotalSettlementsPrepared);

        // Total payments with PaymentType == "FINAL" should still be 1
        var finalPayments = await _db.Payments
            .Where(p => p.ProjectId == project.ProjectId && p.PaymentType == "FINAL")
            .ToListAsync();

        Assert.Single(finalPayments);
        Assert.Equal("Due", finalPayments[0].Status);
        Assert.Equal(70_000_000m, finalPayments[0].Amount); // 100M - 30M = 70M
    }

    [Fact]
    public async Task PrepareFinalSettlement_ZeroBalance_DoesNotCreatePaymentRecord()
    {
        var today = new DateOnly(2026, 11, 7);
        var yesterday = today.AddDays(-1);
        // Base = 100M, NRD = 100M (Fully paid)
        var project = CreateProject("PRJ-ZERO-BALANCE", yesterday, baseValue: 100_000_000m, nrd: 100_000_000m);

        var result = await _service.PrepareFinalSettlementsAsync(new PrepareFinalSettlementCommand(today));

        Assert.Equal(1, result.TotalProjectsFound);
        Assert.Equal(1, result.TotalEventsCompleted);
        Assert.Equal(1, result.TotalSettlementsPrepared);
        Assert.Equal(0, result.TotalFinalMilestonesCreated); // 0 payment not created in DB

        // Project must remain Event Completed, NOT Settled (Manager must perform settlement action)
        var dbProject = await _db.WeddingProjects.FindAsync(project.ProjectId);
        Assert.Equal("Event Completed", dbProject!.Status);

        // Manager still notified that final due is 0 and needs verification
        var notification = await _db.Notifications.SingleAsync();
        Assert.Contains("0 VND", notification.Message);
    }

    [Fact]
    public async Task PrepareFinalSettlement_IgnoresDraftAndNegotiatingCRs()
    {
        var today = new DateOnly(2026, 11, 7);
        var yesterday = today.AddDays(-1);
        var project = CreateProject("PRJ-CR-FILTER", yesterday, baseValue: 100_000_000m, nrd: 30_000_000m);

        // Add Draft CR (+15M) and Negotiating CR (+25M)
        _db.ChangeRequests.AddRange(
            new ChangeRequest
            {
                Crcode = "CR-DRAFT",
                ProjectId = project.ProjectId,
                CreatedBy = _customer.UserId,
                Type = "ScopeAddition",
                Description = "Bản nháp",
                CostImpact = 15_000_000m,
                Status = "Draft",
                CreatedAt = DateTime.UtcNow
            },
            new ChangeRequest
            {
                Crcode = "CR-NEGOTIATE",
                ProjectId = project.ProjectId,
                CreatedBy = _customer.UserId,
                Type = "ScopeAddition",
                Description = "Đang đàm phán",
                CostImpact = 25_000_000m,
                Status = "Negotiating",
                CreatedAt = DateTime.UtcNow
            }
        );
        await _db.SaveChangesAsync();

        var result = await _service.PrepareFinalSettlementsAsync(new PrepareFinalSettlementCommand(today));

        Assert.Equal(1, result.TotalSettlementsPrepared);

        // Final Due must be 100M - 30M = 70M (Draft and Negotiating CRs ignored)
        var finalPayment = await _db.Payments.SingleAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == "FINAL");
        Assert.Equal(70_000_000m, finalPayment.Amount);
    }

    [Fact]
    public async Task PrepareFinalSettlement_EmailOutage_DoesNotRollbackSettlement()
    {
        var today = new DateOnly(2026, 11, 7);
        var yesterday = today.AddDays(-1);
        var project = CreateProject("PRJ-EMAIL-ERR", yesterday);

        var failingEmailOutbox = new FailingEmailOutbox();
        var customService = new FinalSettlementService(
            _db,
            _notificationRepo,
            failingEmailOutbox,
            _emailOptions,
            NullLogger<FinalSettlementService>.Instance);

        var result = await customService.PrepareFinalSettlementsAsync(new PrepareFinalSettlementCommand(today));

        Assert.Equal(1, result.TotalProjectsFound);
        Assert.Equal(1, result.TotalSettlementsPrepared);
        Assert.Equal(0, result.TotalEmailsQueued);
        Assert.Equal(0, result.TotalFailed);

        var dbProject = await _db.WeddingProjects.FindAsync(project.ProjectId);
        Assert.Equal("Event Completed", dbProject!.Status);
        Assert.Single(_db.Notifications);
    }

    [Fact]
    public async Task PrepareFinalSettlement_Idempotency_RunningTwiceDoesNotDuplicate()
    {
        var today = new DateOnly(2026, 11, 7);
        var yesterday = today.AddDays(-1);
        var project = CreateProject("PRJ-IDEMPOTENT-SETTLE", yesterday);

        // First run
        var run1 = await _service.PrepareFinalSettlementsAsync(new PrepareFinalSettlementCommand(today));
        Assert.Equal(1, run1.TotalProjectsFound);
        Assert.Equal(1, run1.TotalSettlementsPrepared);
        Assert.Equal(1, run1.TotalFinalMilestonesCreated);
        Assert.Equal(1, run1.TotalNotificationsCreated);

        // Second run
        var run2 = await _service.PrepareFinalSettlementsAsync(new PrepareFinalSettlementCommand(today));
        Assert.Equal(1, run2.TotalProjectsFound);
        Assert.Equal(0, run2.TotalEventsCompleted); // Already completed
        Assert.Equal(0, run2.TotalFinalMilestonesCreated); // Not duplicated
        Assert.Equal(0, run2.TotalNotificationsCreated); // Not duplicated

        // Assert counts in DB
        Assert.Equal(1, await _db.Payments.CountAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == "FINAL"));
        Assert.Equal(1, await _db.Notifications.CountAsync());
    }

    [Fact]
    public async Task FinalSettlementJob_ExecutesSuccessfully()
    {
        var job = new FinalSettlementJob(_service, NullLogger<FinalSettlementJob>.Instance);
        await job.RunAsync(CancellationToken.None);
    }

    private sealed class FailingEmailOutbox : IEmailOutbox
    {
        public void Enqueue(string toEmail, string subject, string bodyHtml, string kind, int? projectId)
        {
            throw new InvalidOperationException("Email queue unavailable.");
        }
    }
}
