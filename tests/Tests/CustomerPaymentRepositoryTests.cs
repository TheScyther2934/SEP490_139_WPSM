using System;
using System.IO;
using System.Linq;
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

public sealed class CustomerPaymentRepositoryTests : IDisposable
{
    private readonly WpsmDbContext _db;
    private readonly TestFileStorage _storage = new();
    private readonly User _customer;
    private readonly User _otherCustomer;
    private readonly User _manager;
    private readonly WeddingProject _project;

    public CustomerPaymentRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WpsmDbContext(options);

        var hall = new Hall { HallId = 10, Name = "Sanh Bach Kim", Status = "Active" };
        var slot = new Slot { SlotId = 10, HallId = 10, SlotName = "Toi" };
        _customer = new User { UserId = 101, Email = "customer101@test.vn", FullName = "Le Van A", Role = "Customer", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _otherCustomer = new User { UserId = 102, Email = "customer102@test.vn", FullName = "Tran Thi B", Role = "Customer", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };
        _manager = new User { UserId = 201, Email = "mgr201@test.vn", FullName = "Pham Manager", Role = "Manager", Status = "Active", PasswordHash = "x", CreatedAt = DateTime.UtcNow };

        _project = new WeddingProject
        {
            ProjectId = 1,
            ProjectCode = "WPSM-2026-TEST",
            CustomerId = _customer.UserId,
            ManagerId = _manager.UserId,
            HallId = hall.HallId,
            SlotId = slot.SlotId,
            EventDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45)),
            BaseContractValue = 100_000_000m,
            DepositAmount = 20_000_000m,
            CurrentTotal = 105_000_000m,
            Status = "Locked",
            CreatedAt = DateTime.UtcNow
        };

        _db.Halls.Add(hall);
        _db.Slots.Add(slot);
        _db.Users.AddRange(_customer, _otherCustomer, _manager);
        _db.WeddingProjects.Add(_project);

        // Add NRD payment: 20M, Paid
        _db.Payments.Add(new Payment
        {
            PaymentId = 1,
            ProjectId = _project.ProjectId,
            PaymentType = "Deposit",
            Status = "Paid",
            Amount = 20_000_000m,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            PaidDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            PaymentMethod = "Chuyen khoan",
            ReceiptFilePath = "WPSM-TEST/receipts/deposit.pdf", ReceiptContentType = "application/pdf"
        });
        _storage.Files["WPSM-TEST/receipts/deposit.pdf"] = new byte[] { 1, 2, 3, 4, 5 };

        // Confirmed addon contract: 5M (counted); one waiting for the customer: 10M (not counted).
        _db.AddonContracts.Add(Addon(1, "PS-1", "Confirmed", 5_000_000m));
        _db.AddonContracts.Add(Addon(2, "PS-2", "PendingCustomer", 10_000_000m));

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private AddonContract Addon(int id, string code, string status, decimal amount)
    {
        var addon = new AddonContract { AddonId = id, AddonCode = code, ProjectId = _project.ProjectId, Title = code, Status = status,
            CreatedBy = _manager.UserId, CreatedAt = DateTime.UtcNow, ConfirmedAt = status == "Confirmed" ? DateTime.UtcNow : null };
        addon.Items.Add(new AddonContractItem { Description = code, Quantity = 1, UnitPrice = amount });
        return addon;
    }

    [Fact]
    public void PaymentCalculator_SettlementIsContractPlusConfirmedAddonsMinusDeposit()
    {
        Assert.Equal(115_000_000m, PaymentCalculator.CalculateGrandTotal(105_000_000m, 10_000_000m));
        Assert.Equal(85_000_000m, PaymentCalculator.CalculateSettlementAmount(105_000_000m, 10_000_000m, 30_000_000m));
        Assert.Equal(0m, PaymentCalculator.CalculateSettlementAmount(10_000_000m, 0m, 30_000_000m));
        var totalPaid = PaymentCalculator.CalculateTotalPaid(new (string Status, decimal? Amount)[] { ("Paid", 20_000_000m), ("Due", 50_000_000m) });
        Assert.Equal(20_000_000m, totalPaid);
        Assert.Equal(95_000_000m, PaymentCalculator.CalculateOutstandingBalance(115_000_000m, totalPaid));
    }

    [Fact]
    public async Task GetPaymentSummaryAsync_ReturnsAccurateSummaryForOwner()
    {
        var dummyStorage = _storage;
        var repo = new CustomerPaymentRepository(_db, dummyStorage);

        var summary = await repo.GetPaymentSummaryAsync(_customer.UserId);

        Assert.NotNull(summary);
        Assert.Equal(_project.ProjectCode, summary.ProjectCode);
        Assert.Equal(105_000_000m, summary.BaseContractValue);   // contract total after adjustments
        Assert.Equal(5_000_000m, summary.ConfirmedAddonTotal);
        Assert.Equal(110_000_000m, summary.CurrentProjectTotal);
        Assert.Equal(20_000_000m, summary.TotalAmountPaid);
        Assert.Equal(90_000_000m, summary.OutstandingBalance);
    }

    [Fact]
    public async Task GetPaymentSummaryAsync_ReturnsNullForUnrelatedUser()
    {
        var dummyStorage = _storage;
        var repo = new CustomerPaymentRepository(_db, dummyStorage);

        var summary = await repo.GetPaymentSummaryAsync(_otherCustomer.UserId);

        Assert.Null(summary);
    }

    [Fact]
    public async Task GetPaymentMilestonesAsync_HasOnlyDepositAndSettlement()
    {
        var dummyStorage = _storage;
        var repo = new CustomerPaymentRepository(_db, dummyStorage);

        var milestones = await repo.GetPaymentMilestonesAsync(_customer.UserId);

        Assert.NotNull(milestones);
        Assert.Equal(2, milestones.Count);

        var nrd = milestones.First(m => m.PaymentType == "Deposit");
        Assert.Equal("Paid", nrd.Status);
        Assert.Equal(20_000_000m, nrd.PaidAmount);
        Assert.True(nrd.ReceiptAvailable);

        var settlement = milestones.Single(m => m.PaymentType == "Settlement");
        Assert.Equal(90_000_000m, settlement.Amount);   // 105M + 5M confirmed addon - 20M deposit
        Assert.Equal("Upcoming", settlement.Status);
        Assert.Equal(_project.EventDate.AddDays(7), settlement.DueDate);
    }

    [Fact]
    public async Task GetPaymentReceiptAsync_ReturnsBytesWhenReceiptExists()
    {
        var dummyStorage = _storage;
        var repo = new CustomerPaymentRepository(_db, dummyStorage);

        var receipt = await repo.GetPaymentReceiptAsync(_customer.UserId, paymentType: "Deposit");

        Assert.NotNull(receipt);
        Assert.Equal(5, receipt.Content.Length);
        Assert.Equal("application/pdf", receipt.ContentType);
    }

    [Fact]
    public async Task FinalSettlementReceipt_DeniedWhenProjectNotSettled()
    {
        var dummyStorage = _storage;
        var repo = new CustomerPaymentRepository(_db, dummyStorage);

        // Project status is "Locked", not "Settled" -> throws InvalidOperationException
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.GetFinalSettlementReceiptAsync(_customer.UserId));
    }
}
