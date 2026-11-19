using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

public sealed class ManagerPaymentRepositoryTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly User _customer;
    private readonly User _manager;
    private readonly User _otherManager;
    private readonly WeddingProject _project;
    private readonly DummyNotificationRepository _notifications;

    public ManagerPaymentRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WpsmDbContext(options);
        _notifications = new DummyNotificationRepository();

        var hall = new Hall { HallId = 1, Name = "Sanh Diamond", Status = "Active" };
        var slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Toi" };
        _customer = new User { UserId = 11, Email = "cust11@wpsm.local", FullName = "Khach Hang 11", Role = "Customer", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _manager = new User { UserId = 22, Email = "mgr22@wpsm.local", FullName = "Manager 22", Role = "Manager", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _otherManager = new User { UserId = 33, Email = "mgr33@wpsm.local", FullName = "Manager 33", Role = "Manager", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };

        _project = new WeddingProject
        {
            ProjectId = 100,
            ProjectCode = "WPSM-M13-TEST",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = hall.HallId,
            SlotId = slot.SlotId,
            EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)),
            BaseContractValue = 200_000_000m,
            DepositAmount = 40_000_000m,
            CurrentTotal = 210_000_000m,
            Status = "Locked",
            CreatedAt = DateTime.UtcNow
        };

        _db.Halls.Add(hall);
        _db.Slots.Add(slot);
        _db.Users.AddRange(_customer, _manager, _otherManager);
        _db.WeddingProjects.Add(_project);

        // NRD Payment: 40M, Paid
        _db.Payments.Add(new Payment
        {
            PaymentId = 1,
            ProjectId = _project.ProjectId,
            PaymentType = "Deposit",
            Status = "Paid",
            Amount = 40_000_000m,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
            PaidDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
            PaymentMethod = "Cash",
            RecordedBy = _manager.UserId
        });

        // Confirmed addon contract: 10M.
        var addon = new AddonContract { AddonId = 1, AddonCode = "PS-1", ProjectId = _project.ProjectId, Title = "Thêm âm thanh ánh sáng", Status = "Confirmed",
            CreatedBy = _manager.UserId, CreatedAt = DateTime.UtcNow, ConfirmedAt = DateTime.UtcNow };
        addon.Items.Add(new AddonContractItem { Description = "Âm thanh bổ sung", Quantity = 1, UnitPrice = 10_000_000m });
        _db.AddonContracts.Add(addon);

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private ManagerPaymentRepository Repo() => new(_db, new DummyStorage(), _notifications);

    private void EventHeld()
    {
        _project.EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
        _project.Status = "Completed";
        _db.SaveChanges();
    }

    private static RecordPaymentCommand Settlement(decimal amount, string method = "Cash", int dayOffset = 0, byte[]? receipt = null) =>
        new("Settlement", amount, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(dayOffset)), method, receipt,
            receipt is null ? null : "receipt.pdf", receipt is null ? null : "application/pdf");

    [Fact]
    public async Task GetProjectPaymentsAsync_ShowsDepositAndSettlementOnly()
    {
        var overview = await Repo().GetProjectPaymentsAsync(_project.ProjectCode, _manager.UserId, "Manager");

        Assert.NotNull(overview);
        Assert.Equal(210_000_000m, overview.Summary.BaseContractValue);   // contract after adjustments
        Assert.Equal(10_000_000m, overview.Summary.ConfirmedAddonTotal);
        Assert.Equal(220_000_000m, overview.Summary.CurrentProjectTotal);
        Assert.Equal(40_000_000m, overview.Summary.TotalAmountPaid);
        Assert.Equal(180_000_000m, overview.Summary.OutstandingBalance);
        Assert.Equal(2, overview.Milestones.Count);
        Assert.Contains(overview.Milestones, m => m.PaymentType == "Deposit" && m.Status == "Paid");
        var settlement = overview.Milestones.Single(m => m.PaymentType == "Settlement");
        Assert.Equal(180_000_000m, settlement.Amount);
        Assert.Equal("Upcoming", settlement.Status);
    }

    [Fact]
    public async Task GetProjectPaymentsAsync_ForbiddenForUnassignedManager()
    {
        Assert.Null(await Repo().GetProjectPaymentsAsync(_project.ProjectCode, _otherManager.UserId, "Manager"));
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsTheDeposit()
    {
        var result = await Repo().RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager",
            new RecordPaymentCommand("Deposit", 10_000_000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash"));
        Assert.False(result.Success);
        Assert.Contains("đặt cọc", result.Message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsTheOldThirtyDayPayment()
    {
        var result = await Repo().RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager",
            new RecordPaymentCommand("T30", 10_000_000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash"));
        Assert.False(result.Success);
        Assert.Contains("tất toán", result.Message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsSettlementBeforeTheEvent()
    {
        var result = await Repo().RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", Settlement(180_000_000m));
        Assert.False(result.Success);
        Assert.Contains("sau ngày cưới", result.Message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsFutureDate()
    {
        EventHeld();
        var result = await Repo().RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", Settlement(180_000_000m, dayOffset: 5));
        Assert.False(result.Success);
        Assert.Contains("tương lai", result.Message);
    }

    [Fact]
    public async Task RecordPaymentAsync_SettlementIsPaidOnceForTheExactRemainder()
    {
        EventHeld();
        var result = await Repo().RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", Settlement(100_000_000m));
        Assert.False(result.Success);
        Assert.Contains("180.000.000", result.Message.Replace(",", "."));
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsBankTransferWithoutReceipt()
    {
        EventHeld();
        var result = await Repo().RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", Settlement(180_000_000m, "Bank"));
        Assert.False(result.Success);
        Assert.Contains("Chuyển khoản", result.Message);
    }

    [Fact]
    public async Task RecordPaymentAsync_SettlementClosesTheEvent()
    {
        EventHeld();

        var result = await Repo().RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", Settlement(180_000_000m, "Bank", receipt: [1, 2, 3, 4]));

        Assert.True(result.Success);
        Assert.Equal(0m, result.NewBalance);
        Assert.True(result.IsSettled);
        Assert.Equal("Settled", _project.Status);
        var invoice = _db.SettlementInvoices.Single(i => i.ProjectId == _project.ProjectId);
        Assert.Equal("Paid", invoice.Status);
        Assert.Equal(210_000_000m, invoice.ContractTotal);
        Assert.Equal(10_000_000m, invoice.AddonTotal);
        Assert.Equal(40_000_000m, invoice.DepositPaid);
        Assert.NotNull(_db.StoredFiles.FirstOrDefault(f => f.ProjectId == _project.ProjectId && f.Purpose == "SettlementReceipt"));
        Assert.True(_notifications.CreatedCount > 0);
    }

    [Fact]
    public void ReceiptPdfGenerator_GeneratesValidPdfBytes()
    {
        var model = new FinalSettlementReceiptPdfModel(
            ReceiptNumber: "BN-001", ProjectCode: "WPSM-TEST", CoupleName: "Nguyen Van A & Tran Thi B", CustomerEmail: "customer@test.vn",
            CustomerPhone: "0901234567", EventDate: new DateOnly(2026, 12, 1), HallName: "Sanh Diamond", BaseContractValue: 210_000_000m,
            ConfirmedAddonTotal: 10_000_000m, CurrentProjectTotal: 220_000_000m, TotalAmountPaid: 220_000_000m, FinalBalance: 0m,
            Status: "Settled", IssuingManagerName: "Manager Name", IssueDate: DateTime.UtcNow,
            Payments: new (string Type, DateOnly? Date, string? Method, decimal Amount, string? RecordedBy)[]
            {
                ("Tiền đặt cọc", (DateOnly?)new DateOnly(2026, 9, 1), "Cash", 40_000_000m, "Manager"),
                ("Tất toán", (DateOnly?)new DateOnly(2026, 12, 2), "Cash", 180_000_000m, "Manager")
            });

        var text = Encoding.ASCII.GetString(ReceiptPdfGenerator.GenerateFinalSettlementReceiptPdf(model));

        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("%%EOF", text);
        Assert.Contains("BN-001", text);
        Assert.Contains("Hop dong phat sinh da xac nhan", text);
    }

    private sealed class DummyStorage : IFileStorage
    {
        public Task UploadAsync(Stream content, string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<Stream?>(null);
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class DummyNotificationRepository : INotificationRepository
    {
        public int CreatedCount { get; private set; }

        public Task<NotificationView> CreateNotificationAsync(int recipientId, string type, string message, string? linkUrl = null, string? relatedEntityType = null, string? relatedEntityId = null, CancellationToken cancellationToken = default)
        {
            CreatedCount++;
            return Task.FromResult(new NotificationView(CreatedCount, type, message, linkUrl, false, DateTime.UtcNow, relatedEntityType, relatedEntityId));
        }

        public Task<NotificationListResponse> GetNotificationsAsync(int userId, string? filter = "All", int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> MarkAsReadAsync(int userId, int notificationId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
