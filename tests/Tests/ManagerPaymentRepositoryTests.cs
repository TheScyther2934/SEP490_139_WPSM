using Infrastructure.Email;
using Infrastructure.UseCases;
using Domain.Rules;
using Application.UseCases.Settlement;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Files;
using Infrastructure.Persistence;
using Domain.Entities;
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

    public ManagerPaymentRepositoryTests()
    {
        _db = TestFixtures.NewDb();

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

    private FileService Files() => new(_db, new TestFileStorage(), new FileStorageOptions { RootPath = ".", SigningKey = "test" });
    private ManagerPaymentRepository Repo() => new(_db, Files());
    private Task<RecordPaymentResult> Record(RecordPaymentCommand command) =>
        new RecordSettlement(new SettlementStore(_db, Files()), new SettlementDocuments(_db, Files()),
            new EventMessages(_db, new TestFixtures.RecordingEmailOutbox(), new EmailOptions()), Repo(), new UnitOfWork(_db))
        .HandleAsync(_project.ProjectCode, _manager.UserId, "Manager", command);
    private async Task<string> Refused(RecordPaymentCommand command) => (await Assert.ThrowsAsync<BusinessRuleException>(() => Record(command))).Message;

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
        var message = await Refused(new RecordPaymentCommand("Deposit", 10_000_000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash"));
        Assert.Contains("đặt cọc", message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsTheOldThirtyDayPayment()
    {
        var message = await Refused(new RecordPaymentCommand("T30", 10_000_000m, DateOnly.FromDateTime(DateTime.UtcNow), "Cash"));
        Assert.Contains("tất toán", message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsSettlementBeforeTheEvent()
    {
        var message = await Refused(Settlement(180_000_000m));
        Assert.Contains("sau ngày cưới", message);
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsFutureDate()
    {
        EventHeld();
        var message = await Refused(Settlement(180_000_000m, dayOffset: 5));
        Assert.Contains("tương lai", message);
    }

    [Fact]
    public async Task RecordPaymentAsync_SettlementIsPaidOnceForTheExactRemainder()
    {
        EventHeld();
        var message = await Refused(Settlement(100_000_000m));
        Assert.Contains("180.000.000", message.Replace(",", "."));
    }

    [Fact]
    public async Task RecordPaymentAsync_RejectsBankTransferWithoutReceipt()
    {
        EventHeld();
        var message = await Refused(Settlement(180_000_000m, "Bank"));
        Assert.Contains("Chuyển khoản", message);
    }

    [Fact]
    public async Task RecordPaymentAsync_SettlementClosesTheEvent()
    {
        EventHeld();

        var result = await Record(Settlement(180_000_000m, "Bank", receipt: Encoding.ASCII.GetBytes("%PDF-1.4 %%EOF")));

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
        Assert.Contains(_db.Notifications, n => n.RecipientId == _customer.UserId && n.Type == "Payment");
        Assert.Equal("Paid", _db.Payments.Single(p => p.PaymentType == "Settlement").Status);
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
}
