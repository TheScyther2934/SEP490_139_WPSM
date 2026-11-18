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

public sealed class IssueT30PaymentRequestJobTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly INotificationRepository _notificationRepo;
    private readonly IEmailOutbox _emailOutbox;
    private readonly EmailOptions _emailOptions;
    private readonly T30PaymentRequestService _service;
    private readonly User _customer;
    private readonly User _manager;
    private readonly Hall _hall;
    private readonly Slot _slot;

    public IssueT30PaymentRequestJobTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new WpsmDbContext(options);
        _notificationRepo = new NotificationRepository(_db);
        _emailOutbox = new EmailOutbox(_db);
        _emailOptions = new EmailOptions { PublicBaseUrl = "http://localhost:5173" };

        _service = new T30PaymentRequestService(
            _db,
            _notificationRepo,
            _emailOutbox,
            _emailOptions,
            NullLogger<T30PaymentRequestService>.Instance);

        _customer = new User
        {
            UserId = 1,
            Email = "customer@test.vn",
            FullName = "Nguyễn Văn Khách",
            Role = "Customer",
            Status = "Active",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        _manager = new User
        {
            UserId = 2,
            Email = "manager@test.vn",
            FullName = "Trần Văn Quản Lý",
            Role = "Manager",
            Status = "Active",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        _hall = new Hall { HallId = 1, Name = "Sảnh Kim Cương", Status = "Active" };
        _slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Evening" };

        _db.Users.AddRange(_customer, _manager);
        _db.Halls.Add(_hall);
        _db.Slots.Add(_slot);

        // Seed default T30_RATIO = 70% in PolicyConfig
        _db.PolicyConfigs.Add(new PolicyConfig
        {
            PolicyId = 1,
            PolicyType = "T30_RATIO",
            RatePercent = 70m,
            EffectiveFrom = new DateOnly(2020, 1, 1),
            UpdatedBy = _manager.UserId,
            UpdatedAt = DateTime.UtcNow
        });

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
            BaselineFrozenAt = hasBaseline ? DateTime.UtcNow.AddDays(-60) : null,
            CreatedAt = DateTime.UtcNow.AddDays(-60)
        };
        _db.WeddingProjects.Add(p);
        _db.SaveChanges();

        // Add initial NRD payment
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
    public async Task IssueT30_EventDateExactlyTodayPlus30_CreatesDuePayment_AndNotifiesCustomerAndManager()
    {
        var today = new DateOnly(2026, 11, 7);
        var targetEventDate = today.AddDays(30); // 2026-12-07

        var project = CreateProject("PRJ-T30-001", targetEventDate, baseValue: 100_000_000m, nrd: 30_000_000m);

        // Act
        var result = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        // Assert
        Assert.Equal(1, result.TotalProjectsChecked);
        Assert.Equal(1, result.TotalRequestsIssued);
        Assert.Equal(2, result.TotalEmailsQueued);
        Assert.Equal(0, result.TotalSkipped);
        Assert.Equal(0, result.TotalFailed);

        // Verify T30 payment in DB: 70% of 100M = 70M. 70M - 30M (NRD) = 40M.
        var t30Payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == "T30");

        Assert.NotNull(t30Payment);
        Assert.Equal("Due", t30Payment!.Status);
        Assert.Equal(today, t30Payment.DueDate);
        Assert.Equal(40_000_000m, t30Payment.Amount);

        // Verify Notifications
        var notifications = await _db.Notifications.ToListAsync();
        Assert.Equal(2, notifications.Count);

        var custNotification = notifications.Single(n => n.RecipientId == _customer.UserId);
        Assert.Equal(NotificationTypes.Payment, custNotification.Type);
        Assert.Contains("PRJ-T30-001", custNotification.Message);
        Assert.Contains("40,000,000", custNotification.Message);
        Assert.Contains("T-30", custNotification.Message);
        Assert.Equal("/customer/payments", custNotification.LinkUrl);

        var mgrNotification = notifications.Single(n => n.RecipientId == _manager.UserId);
        Assert.Equal(NotificationTypes.Payment, mgrNotification.Type);
        Assert.Contains("PRJ-T30-001", mgrNotification.Message);
        Assert.Contains("40,000,000", mgrNotification.Message);
        Assert.Equal("/manager/projects/PRJ-T30-001/payments", mgrNotification.LinkUrl);

        // Verify Emails in outbox
        var emails = await _db.EmailOutbox.ToListAsync();
        Assert.Equal(2, emails.Count);
        Assert.All(emails, e => Assert.Equal("PaymentRequest", e.Kind));
        Assert.All(emails, e => Assert.Equal("Queued", e.Status));

        var custEmail = emails.Single(e => e.ToEmail == _customer.Email);
        Assert.Contains("PRJ-T30-001", custEmail.Subject);
        Assert.Contains("/customer/payments", custEmail.BodyHtml);
        Assert.Contains("40,000,000", custEmail.BodyHtml);

        var mgrEmail = emails.Single(e => e.ToEmail == _manager.Email);
        Assert.Contains("PRJ-T30-001", mgrEmail.Subject);
        Assert.Contains("/manager/projects/PRJ-T30-001/payments", mgrEmail.BodyHtml);
    }

    [Fact]
    public async Task IssueT30_EventDateNotExactly30Days_Ignored()
    {
        var today = new DateOnly(2026, 11, 7);

        // 29 days ahead (not 30)
        CreateProject("PRJ-T29", today.AddDays(29));
        // 31 days ahead (not 30)
        CreateProject("PRJ-T31", today.AddDays(31));

        var result = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        Assert.Equal(0, result.TotalProjectsChecked);
        Assert.Equal(0, result.TotalRequestsIssued);
        Assert.Empty(_db.Notifications);
        Assert.Empty(_db.EmailOutbox);
    }

    [Fact]
    public async Task IssueT30_ProjectWithoutContractBaseline_Ignored()
    {
        var today = new DateOnly(2026, 11, 7);
        // Project has not frozen baseline
        CreateProject("PRJ-NO-BASELINE", today.AddDays(30), hasBaseline: false);

        var result = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        Assert.Equal(0, result.TotalProjectsChecked);
        Assert.Equal(0, result.TotalRequestsIssued);
    }

    [Fact]
    public async Task IssueT30_ProjectAlreadyHasT30Payment_Skipped()
    {
        var today = new DateOnly(2026, 11, 7);
        var project = CreateProject("PRJ-EXISTING-T30", today.AddDays(30));

        // Add already existing T30 payment
        _db.Payments.Add(new Payment
        {
            ProjectId = project.ProjectId,
            PaymentType = "T30",
            Status = "Due",
            DueDate = today,
            Amount = 40_000_000m
        });
        await _db.SaveChangesAsync();

        var result = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        Assert.Equal(1, result.TotalProjectsChecked);
        Assert.Equal(0, result.TotalRequestsIssued);
        Assert.Equal(1, result.TotalSkipped);
        Assert.Empty(_db.Notifications);
    }

    [Fact]
    public async Task IssueT30_FullyPaidProject_SkippedWithoutPaymentRequest()
    {
        var today = new DateOnly(2026, 11, 7);
        var project = CreateProject("PRJ-FULLY-PAID", today.AddDays(30), baseValue: 100_000_000m, nrd: 30_000_000m);

        // Add an additional payment covering the remaining balance
        _db.Payments.Add(new Payment
        {
            ProjectId = project.ProjectId,
            PaymentType = "FINAL",
            Status = "Paid",
            Amount = 70_000_000m,
            PaymentMethod = "Bank",
            PaidDate = today.AddDays(-10),
            RecordedBy = _manager.UserId
        });
        await _db.SaveChangesAsync();

        var result = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        Assert.Equal(1, result.TotalProjectsChecked);
        Assert.Equal(0, result.TotalRequestsIssued);
        Assert.Equal(1, result.TotalSkipped);
        Assert.Empty(_db.Notifications);
        Assert.Empty(_db.EmailOutbox);
    }

    [Fact]
    public async Task IssueT30_WithApprovedChangeRequests_CalculatesAmountCorrectly()
    {
        var today = new DateOnly(2026, 11, 7);
        // Base = 100M, NRD = 30M
        var project = CreateProject("PRJ-WITH-CR", today.AddDays(30), baseValue: 100_000_000m, nrd: 30_000_000m);

        // Add an Approved CR with +20M variance
        var cr = new ChangeRequest
        {
            Crcode = "CR-001",
            ProjectId = project.ProjectId,
            CreatedBy = _customer.UserId,
            Type = "ScopeAddition",
            Description = "Thêm gói hoa tươi bàn tiệc",
            CostImpact = 20_000_000m,
            PenaltyFee = 0m,
            TotalVariance = 20_000_000m,
            Status = "CustomerApproved",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        _db.ChangeRequests.Add(cr);
        await _db.SaveChangesAsync();

        // Current Total = 100M + 20M = 120M. Target 70% of 120M = 84M. T-30 Required = 84M - 30M = 54M.
        var result = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        Assert.Equal(1, result.TotalRequestsIssued);

        var payment = await _db.Payments.SingleAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == "T30");
        Assert.Equal(54_000_000m, payment.Amount);
    }

    [Fact]
    public async Task IssueT30_WithNegotiatingChangeRequest_ExcludesFromAmount_AndNotifiesManager()
    {
        var today = new DateOnly(2026, 11, 7);
        var project = CreateProject("PRJ-NEGOTIATING", today.AddDays(30), baseValue: 100_000_000m, nrd: 30_000_000m);

        // Add Negotiating CR (should NOT affect Current Total)
        var cr = new ChangeRequest
        {
            Crcode = "CR-NEGOTIATE",
            ProjectId = project.ProjectId,
            CreatedBy = _customer.UserId,
            Type = "ScopeAddition",
            Description = "Đang đàm phán thêm bàn tiệc",
            CostImpact = 15_000_000m,
            PenaltyFee = 0m,
            TotalVariance = 15_000_000m,
            Status = "Negotiating",
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        _db.ChangeRequests.Add(cr);
        await _db.SaveChangesAsync();

        var result = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        Assert.Equal(1, result.TotalRequestsIssued);

        // Payment amount stays 40M (based only on BaseContractValue, ignoring Negotiating CR)
        var payment = await _db.Payments.SingleAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == "T30");
        Assert.Equal(40_000_000m, payment.Amount);

        // Verify Manager received CR Update reminder notification
        var crReminder = await _db.Notifications.FirstOrDefaultAsync(n =>
            n.RecipientId == _manager.UserId && n.Type == NotificationTypes.CrUpdate);

        Assert.NotNull(crReminder);
        Assert.Contains("PRJ-NEGOTIATING", crReminder!.Message);
        Assert.Contains("Negotiating", crReminder.Message);
        Assert.Equal("/manager/projects/PRJ-NEGOTIATING/change-requests", crReminder.LinkUrl);
    }

    [Fact]
    public async Task IssueT30_UsesDynamicPolicyConfigRatio()
    {
        var today = new DateOnly(2026, 11, 7);

        // Update PolicyConfig to 80%
        var policy = await _db.PolicyConfigs.SingleAsync(p => p.PolicyType == "T30_RATIO");
        policy.RatePercent = 80m;
        await _db.SaveChangesAsync();

        // Base = 100M, NRD = 30M. 80% of 100M = 80M. Required = 80M - 30M = 50M.
        var project = CreateProject("PRJ-CUSTOM-RATIO", today.AddDays(30), baseValue: 100_000_000m, nrd: 30_000_000m);

        var result = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        Assert.Equal(1, result.TotalRequestsIssued);
        var payment = await _db.Payments.SingleAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == "T30");
        Assert.Equal(50_000_000m, payment.Amount);
    }

    [Fact]
    public async Task IssueT30_InactiveOrClosedProject_Ignored()
    {
        var today = new DateOnly(2026, 11, 7);
        CreateProject("PRJ-CLOSED", today.AddDays(30), status: "Closed");
        CreateProject("PRJ-SETTLED", today.AddDays(30), status: "Settled");

        var result = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        Assert.Equal(0, result.TotalProjectsChecked);
        Assert.Equal(0, result.TotalRequestsIssued);
    }

    [Fact]
    public async Task IssueT30_MissingNrdPayment_Skipped()
    {
        var today = new DateOnly(2026, 11, 7);
        var p = new WeddingProject
        {
            ProjectCode = "PRJ-NO-NRD",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = _hall.HallId,
            SlotId = _slot.SlotId,
            EventDate = today.AddDays(30),
            BaseContractValue = 100_000_000m,
            Nrdamount = 30_000_000m,
            CurrentTotal = 100_000_000m,
            Status = "Baseline Frozen",
            BaselineFrozenAt = DateTime.UtcNow.AddDays(-60),
            CreatedAt = DateTime.UtcNow.AddDays(-60)
        };
        _db.WeddingProjects.Add(p);
        await _db.SaveChangesAsync();
        // Do not add NRD payment

        var result = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        Assert.Equal(1, result.TotalProjectsChecked);
        Assert.Equal(0, result.TotalRequestsIssued);
        Assert.Equal(1, result.TotalSkipped);
    }

    [Fact]
    public async Task IssueT30_EmailOutage_DoesNotRollbackPayment()
    {
        var today = new DateOnly(2026, 11, 7);
        var project = CreateProject("PRJ-EMAIL-FAIL", today.AddDays(30));

        var failingEmailOutbox = new FailingEmailOutbox();
        var customService = new T30PaymentRequestService(
            _db,
            _notificationRepo,
            failingEmailOutbox,
            _emailOptions,
            NullLogger<T30PaymentRequestService>.Instance);

        var result = await customService.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));

        // Payment and notifications still created
        Assert.Equal(1, result.TotalRequestsIssued);
        Assert.Equal(0, result.TotalEmailsQueued);
        Assert.Equal(0, result.TotalFailed);

        var payment = await _db.Payments.SingleAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == "T30");
        Assert.NotNull(payment);
        Assert.Equal(2, await _db.Notifications.CountAsync());
    }

    [Fact]
    public async Task IssueT30_Idempotency_RunningTwiceDoesNotDuplicate()
    {
        var today = new DateOnly(2026, 11, 7);
        var project = CreateProject("PRJ-IDEMPOTENT", today.AddDays(30));

        // First run
        var run1 = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));
        Assert.Equal(1, run1.TotalRequestsIssued);
        Assert.Equal(2, run1.TotalEmailsQueued);

        // Second run
        var run2 = await _service.IssueT30PaymentRequestsAsync(new IssueT30PaymentRequestCommand(today));
        Assert.Equal(0, run2.TotalRequestsIssued);
        Assert.Equal(0, run2.TotalEmailsQueued);
        Assert.Equal(1, run2.TotalSkipped);

        // Payments table has only 1 T30 payment
        Assert.Equal(1, await _db.Payments.CountAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == "T30"));
        Assert.Equal(2, await _db.Notifications.CountAsync());
        Assert.Equal(2, await _db.EmailOutbox.CountAsync());
    }

    [Fact]
    public async Task IssueT30PaymentRequestJob_ExecutesSuccessfully()
    {
        var job = new IssueT30PaymentRequestJob(_service, NullLogger<IssueT30PaymentRequestJob>.Instance);
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
