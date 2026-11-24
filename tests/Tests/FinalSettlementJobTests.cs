using Infrastructure.UseCases;
using Application.UseCases.Settlement;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Jobs;
using Infrastructure.Persistence;
using Domain.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

/// <summary>
/// Stage 5: the day after the event the job marks every locked event Completed and issues its final invoice
/// (contract after adjustments + confirmed addon contracts - deposit, due 7 days after the event).
/// </summary>
public sealed class FinalSettlementJobTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly IssueSettlementInvoices _service;
    private readonly User _customer;
    private readonly User _manager;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public FinalSettlementJobTests()
    {
        _db = new WpsmDbContext(new DbContextOptionsBuilder<WpsmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _service = NewService(new EmailOutbox(_db));
        _customer = new User { UserId = 1, Email = "customer@wedding.vn", FullName = "Nguyễn Văn Chú Rể", Role = "Customer", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };
        _manager = new User { UserId = 2, Email = "manager@wedding.vn", FullName = "Lê Thị Quản Lý", Role = "Manager", Status = "Active", PasswordHash = "h", CreatedAt = DateTime.UtcNow };
        _db.Users.AddRange(_customer, _manager);
        _db.Halls.Add(new Hall { HallId = 1, Name = "Sảnh Hoàng Kim", Status = "Active" });
        _db.Slots.Add(new Slot { SlotId = 1, HallId = 1, SlotName = "Evening" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private IssueSettlementInvoices NewService(IEmailOutbox outbox) => new(new SettlementStore(_db, null!),
        new EventMessages(_db, outbox, new EmailOptions { PublicBaseUrl = "http://localhost:5173" }), new UnitOfWork(_db));

    private WeddingProject CreateEvent(string code, DateOnly eventDate, decimal total = 100_000_000m, decimal deposit = 30_000_000m, string status = "Locked")
    {
        var p = new WeddingProject
        {
            ProjectCode = code, CustomerId = _customer.UserId, ManagerId = _manager.UserId, HallId = 1, SlotId = 1, EventDate = eventDate,
            BaseContractValue = total, DepositAmount = deposit, CurrentTotal = total, Status = status,
            LockedAt = status == "Adjusting" ? null : DateTime.UtcNow.AddDays(-12), CreatedAt = DateTime.UtcNow.AddDays(-60)
        };
        _db.WeddingProjects.Add(p);
        _db.SaveChanges();
        _db.ProjectManagers.Add(new ProjectManager { ProjectId = p.ProjectId, ManagerId = _manager.UserId, Role = "Primary", AssignedAt = DateTime.UtcNow });
        _db.Payments.Add(new Payment { ProjectId = p.ProjectId, PaymentType = "Deposit", Status = "Paid", Amount = deposit, DueDate = eventDate.AddDays(-60),
            PaymentMethod = "Bank", PaidDate = eventDate.AddDays(-60), RecordedBy = _manager.UserId });
        _db.SaveChanges();
        return p;
    }

    private void AddAddon(WeddingProject p, string code, string status, decimal amount)
    {
        var addon = new AddonContract { AddonCode = code, ProjectId = p.ProjectId, Title = code, Status = status, CreatedBy = _manager.UserId,
            CreatedAt = DateTime.UtcNow, ConfirmedAt = status == "Confirmed" ? DateTime.UtcNow : null };
        addon.Items.Add(new AddonContractItem { Description = code, Quantity = 1, UnitPrice = amount });
        _db.AddonContracts.Add(addon);
        _db.SaveChanges();
    }

    [Fact]
    public async Task EventYesterday_IsCompleted_AndGetsTheInvoice()
    {
        var p = CreateEvent("WPSM-T-1", Today.AddDays(-1));
        AddAddon(p, "PS-1", "Confirmed", 8_000_000m);
        AddAddon(p, "PS-2", "PendingCustomer", 5_000_000m);   // not confirmed: not billed

        var result = await _service.PrepareFinalSettlementsAsync();

        Assert.Equal(1, result.TotalEventsCompleted);
        var loaded = await _db.WeddingProjects.SingleAsync(x => x.ProjectId == p.ProjectId);
        Assert.Equal("Completed", loaded.Status);
        var invoice = await _db.SettlementInvoices.Include(i => i.Lines).SingleAsync(i => i.ProjectId == p.ProjectId);
        Assert.Equal(100_000_000m, invoice.ContractTotal);
        Assert.Equal(8_000_000m, invoice.AddonTotal);
        Assert.Equal(30_000_000m, invoice.DepositPaid);
        Assert.Equal(Today.AddDays(6), invoice.DueDate);   // event + 7 days
        Assert.Equal(3, invoice.Lines.Count);
        Assert.Equal(-30_000_000m, invoice.Lines.Single(l => l.LineType == "Deposit").Amount);
        var settlement = await _db.Payments.SingleAsync(x => x.ProjectId == p.ProjectId && x.PaymentType == "Settlement");
        Assert.Equal("Due", settlement.Status);
        Assert.Equal(78_000_000m, settlement.Amount);
        Assert.True(await _db.Notifications.AnyAsync(n => n.RecipientId == _manager.UserId && n.Type == "Payment"));
        Assert.True(await _db.Notifications.AnyAsync(n => n.RecipientId == _customer.UserId && n.Type == "Payment"));
        Assert.Contains(await _db.EmailOutbox.ToListAsync(), e => e.ToEmail == _customer.Email && e.Kind == "PaymentRequest");
    }

    [Fact]
    public async Task MissedDays_AreCaughtUp()
    {
        CreateEvent("WPSM-T-2", Today.AddDays(-4));
        var result = await _service.PrepareFinalSettlementsAsync();
        Assert.Equal(1, result.TotalEventsCompleted);
    }

    [Fact]
    public async Task EventTodayOrLater_IsIgnored()
    {
        CreateEvent("WPSM-T-3", Today);
        CreateEvent("WPSM-T-4", Today.AddDays(3));
        var result = await _service.PrepareFinalSettlementsAsync();
        Assert.Equal(0, result.TotalProjectsFound);
        Assert.False(await _db.SettlementInvoices.AnyAsync());
    }

    [Theory]
    [InlineData("Adjusting")]
    [InlineData("Settled")]
    [InlineData("Closed")]
    [InlineData("Cancelled")]
    public async Task OnlyLockedEvents_AreSettled(string status)
    {
        var p = CreateEvent("WPSM-T-5", Today.AddDays(-1), status: status);
        if (status == "Cancelled") { p.CancelledAt = DateTime.UtcNow; p.CancelReason = "Khách hủy"; await _db.SaveChangesAsync(); }
        var result = await _service.PrepareFinalSettlementsAsync();
        Assert.Equal(0, result.TotalProjectsFound);
    }

    [Fact]
    public async Task NothingLeftToPay_IssuesTheInvoiceWithoutASettlementPayment()
    {
        var p = CreateEvent("WPSM-T-6", Today.AddDays(-1), total: 30_000_000m, deposit: 30_000_000m);
        await _service.PrepareFinalSettlementsAsync();
        var invoice = await _db.SettlementInvoices.SingleAsync(i => i.ProjectId == p.ProjectId);
        Assert.Equal(0m, invoice.ContractTotal + invoice.AddonTotal - invoice.DepositPaid);
        Assert.False(await _db.Payments.AnyAsync(x => x.ProjectId == p.ProjectId && x.PaymentType == "Settlement"));
    }

    [Fact]
    public async Task EmailOutage_DoesNotUndoTheInvoice()
    {
        var p = CreateEvent("WPSM-T-7", Today.AddDays(-1));
        var result = await NewService(new FailingEmailOutbox()).PrepareFinalSettlementsAsync();
        Assert.Equal(1, result.TotalSettlementsPrepared);
        Assert.Equal(0, result.TotalEmailsQueued);
        Assert.True(await _db.SettlementInvoices.AnyAsync(i => i.ProjectId == p.ProjectId));
    }

    [Fact]
    public async Task RunningTwice_DoesNotDuplicate()
    {
        var p = CreateEvent("WPSM-T-8", Today.AddDays(-1));
        await _service.PrepareFinalSettlementsAsync();
        var second = await _service.PrepareFinalSettlementsAsync();
        Assert.Equal(0, second.TotalProjectsFound);   // already Completed
        Assert.Equal(1, await _db.SettlementInvoices.CountAsync(i => i.ProjectId == p.ProjectId));
        Assert.Equal(1, await _db.Payments.CountAsync(x => x.ProjectId == p.ProjectId && x.PaymentType == "Settlement"));
    }

    [Fact]
    public async Task FinalSettlementJob_ExecutesSuccessfully()
    {
        var job = new FinalSettlementJob(_service, NullLogger<FinalSettlementJob>.Instance);
        await job.RunAsync(CancellationToken.None);
    }

    private sealed class FailingEmailOutbox : IEmailOutbox
    {
        public void Enqueue(string toEmail, string subject, string bodyHtml, string kind, int? projectId) =>
            throw new InvalidOperationException("Email queue unavailable.");
    }
}
