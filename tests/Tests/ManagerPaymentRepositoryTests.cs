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
            Nrdamount = 40_000_000m,
            CurrentTotal = 210_000_000m,
            Status = "Baseline Frozen",
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
            PaymentType = "NRD",
            Status = "Paid",
            Amount = 40_000_000m,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
            PaidDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)),
            PaymentMethod = "Cash",
            RecordedBy = _manager.UserId
        });

        // Approved CR: 10M
        _db.ChangeRequests.Add(new ChangeRequest
        {
            Crid = 101,
            Crcode = "CR-101",
            ProjectId = _project.ProjectId,
            CreatedBy = _customer.UserId,
            Type = "Scope",
            Description = "Them am thanh anh sang",
            CostImpact = 10_000_000m,
            PenaltyFee = 0m,
            TotalVariance = 10_000_000m,
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        });

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetProjectPaymentsAsync_ReturnsCorrectBreakdownForManager()
    {
        var storage = new DummyStorage();
        var repo = new ManagerPaymentRepository(_db, storage, _notifications);

        var overview = await repo.GetProjectPaymentsAsync(_project.ProjectCode, _manager.UserId, "Manager");

        Assert.NotNull(overview);
        Assert.Equal(200_000_000m, overview.Summary.BaseContractValue);
        Assert.Equal(10_000_000m, overview.Summary.TotalApprovedCrVariance);
        Assert.Equal(210_000_000m, overview.Summary.CurrentProjectTotal);
        Assert.Equal(40_000_000m, overview.Summary.TotalAmountPaid);
        Assert.Equal(170_000_000m, overview.Summary.OutstandingBalance);

        // 3 milestones: NRD, T30, FINAL
        Assert.Equal(3, overview.Milestones.Count);
        Assert.Contains(overview.Milestones, m => m.PaymentType == "NRD" && m.Status == "Paid");
        Assert.Contains(overview.Milestones, m => m.PaymentType == "T30");
        Assert.Contains(overview.Milestones, m => m.PaymentType == "FINAL");
    }

    [Fact]
    public async Task GetProjectPaymentsAsync_ForbiddenForUnassignedManager()
    {
        var storage = new DummyStorage();
        var repo = new ManagerPaymentRepository(_db, storage, _notifications);

        var overview = await repo.GetProjectPaymentsAsync(_project.ProjectCode, _otherManager.UserId, "Manager");

        Assert.Null(overview);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsNrd()
    {
        var storage = new DummyStorage();
        var repo = new ManagerPaymentRepository(_db, storage, _notifications);

        var command = new RecordPaymentCommand(
            PaymentType: "NRD",
            Amount: 10_000_000m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Method: "Cash");

        var result = await repo.RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", command);

        Assert.False(result.Success);
        Assert.Contains("NRD", result.Message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsAmountExceedingBalance()
    {
        var storage = new DummyStorage();
        var repo = new ManagerPaymentRepository(_db, storage, _notifications);

        // Outstanding balance is 170M. Attempt to record 180M.
        var command = new RecordPaymentCommand(
            PaymentType: "T30",
            Amount: 180_000_000m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Method: "Bank",
            ReceiptBytes: new byte[] { 1, 2, 3 },
            ReceiptFileName: "receipt.png",
            ReceiptContentType: "image/png");

        var result = await repo.RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", command);

        Assert.False(result.Success);
        Assert.Contains("MSG11", result.Message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsFutureDate()
    {
        var storage = new DummyStorage();
        var repo = new ManagerPaymentRepository(_db, storage, _notifications);

        var command = new RecordPaymentCommand(
            PaymentType: "T30",
            Amount: 50_000_000m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            Method: "Cash");

        var result = await repo.RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", command);

        Assert.False(result.Success);
        Assert.Contains("tương lai", result.Message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsBankTransferWithoutReceipt()
    {
        var storage = new DummyStorage();
        var repo = new ManagerPaymentRepository(_db, storage, _notifications);

        var command = new RecordPaymentCommand(
            PaymentType: "T30",
            Amount: 50_000_000m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Method: "Bank",
            ReceiptBytes: null);

        var result = await repo.RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", command);

        Assert.False(result.Success);
        Assert.Contains("chuyển khoản", result.Message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsFinalPaymentBeforeEventCompleted()
    {
        var storage = new DummyStorage();
        var repo = new ManagerPaymentRepository(_db, storage, _notifications);

        // EventDate is in +40 days, project status is "Baseline Frozen"
        var command = new RecordPaymentCommand(
            PaymentType: "FINAL",
            Amount: 100_000_000m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Method: "Cash");

        var result = await repo.RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", command);

        Assert.False(result.Success);
        Assert.Contains("hoàn tất", result.Message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RecordsValidT30Payment()
    {
        var storage = new DummyStorage();
        var repo = new ManagerPaymentRepository(_db, storage, _notifications);

        var command = new RecordPaymentCommand(
            PaymentType: "T30",
            Amount: 100_000_000m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Method: "Bank",
            ReceiptBytes: new byte[] { 1, 2, 3, 4 },
            ReceiptFileName: "evidence.pdf",
            ReceiptContentType: "application/pdf");

        var result = await repo.RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", command);

        Assert.True(result.Success);
        Assert.Equal(70_000_000m, result.NewBalance);
        Assert.False(result.IsSettled);

        // Check DB payment record
        var t30 = _db.Payments.FirstOrDefault(p => p.ProjectId == _project.ProjectId && p.PaymentType == "T30");
        Assert.NotNull(t30);
        Assert.Equal("Paid", t30.Status);
        Assert.Equal(100_000_000m, t30.Amount);
        Assert.Equal("Bank", t30.PaymentMethod);

        // Check notification was sent to customer
        Assert.True(_notifications.CreatedCount > 0);
    }

    [Fact]
    public async Task RecordPaymentAsync_FinalPaymentReachingZero_TransitionsToSettled()
    {
        var storage = new DummyStorage();
        var repo = new ManagerPaymentRepository(_db, storage, _notifications);

        // Simulate event completed by setting event date to past
        _project.EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
        _project.Status = "Event Completed";
        _db.SaveChanges();

        // Remaining balance is 170M. Record full remaining 170M as FINAL.
        var command = new RecordPaymentCommand(
            PaymentType: "FINAL",
            Amount: 170_000_000m,
            PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
            Method: "Cash");

        var result = await repo.RecordPaymentAsync(_project.ProjectCode, _manager.UserId, "Manager", command);

        Assert.True(result.Success);
        Assert.Equal(0m, result.NewBalance);
        Assert.True(result.IsSettled);
        Assert.Equal("Settled", _project.Status);

        // Verify receipt PDF stored
        var stored = _db.StoredFiles.FirstOrDefault(f => f.ProjectId == _project.ProjectId && f.Purpose == "SettlementReceipt");
        Assert.NotNull(stored);
    }

    [Fact]
    public void ReceiptPdfGenerator_GeneratesValidPdfBytes()
    {
        var model = new FinalSettlementReceiptPdfModel(
            ReceiptNumber: "FSR-001",
            ProjectCode: "WPSM-TEST",
            CoupleName: "Nguyen Van A & Tran Thi B",
            CustomerEmail: "customer@test.vn",
            CustomerPhone: "0901234567",
            EventDate: new DateOnly(2026, 12, 1),
            HallName: "Sanh Diamond",
            BaseContractValue: 200_000_000m,
            TotalApprovedCrVariance: 10_000_000m,
            CurrentProjectTotal: 210_000_000m,
            TotalAmountPaid: 210_000_000m,
            FinalBalance: 0m,
            Status: "Settled",
            IssuingManagerName: "Manager Name",
            IssueDate: DateTime.UtcNow,
            Payments: new (string Type, DateOnly? Date, string? Method, decimal Amount, string? RecordedBy)[]
            {
                ("NRD", (DateOnly?)new DateOnly(2026, 9, 1), "Cash", 40_000_000m, "Manager"),
                ("T-30", (DateOnly?)new DateOnly(2026, 11, 1), "Bank", 100_000_000m, "Manager"),
                ("FINAL", (DateOnly?)new DateOnly(2026, 12, 2), "Cash", 70_000_000m, "Manager")
            }
        );

        var pdfBytes = ReceiptPdfGenerator.GenerateFinalSettlementReceiptPdf(model);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 200);

        var text = Encoding.ASCII.GetString(pdfBytes);
        Assert.StartsWith("%PDF-1.4", text);
        Assert.Contains("%%EOF", text);
        Assert.Contains("WPSM - WEDDING PLANNING AND SERVICE MANAGEMENT", text);
        Assert.Contains("BIEN NHAN QUYET TOAN HOP DONG", text);
        Assert.Contains("FSR-001", text);
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
