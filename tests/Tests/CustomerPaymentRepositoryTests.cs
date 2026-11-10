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
            Nrdamount = 20_000_000m,
            CurrentTotal = 105_000_000m,
            Status = "Baseline Frozen",
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
            PaymentType = "NRD",
            Status = "Paid",
            Amount = 20_000_000m,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            PaidDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            PaymentMethod = "Chuyen khoan",
            ReceiptContent = new byte[] { 1, 2, 3, 4, 5 }
        });

        // Add Approved CR: 5M
        _db.ChangeRequests.Add(new ChangeRequest
        {
            Crid = 1,
            Crcode = "CR-001",
            ProjectId = _project.ProjectId,
            CreatedBy = _customer.UserId,
            Type = "Scope",
            Description = "Them mon an",
            CostImpact = 5_000_000m,
            PenaltyFee = 0m,
            TotalVariance = 5_000_000m,
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        });

        // Add Draft CR: 10M (should NOT be counted in variance)
        _db.ChangeRequests.Add(new ChangeRequest
        {
            Crid = 2,
            Crcode = "CR-002",
            ProjectId = _project.ProjectId,
            CreatedBy = _customer.UserId,
            Type = "Scope",
            Description = "Ban go tuy chon",
            CostImpact = 10_000_000m,
            PenaltyFee = 0m,
            TotalVariance = 10_000_000m,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        });

        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void PaymentCalculator_CalculatesCorrectFinancialMetrics()
    {
        var baseValue = 100_000_000m;
        var crList = new (string Status, decimal? TotalVariance, decimal CostImpact, decimal PenaltyFee)[]
        {
            ("Approved", 5_000_000m, 5_000_000m, 0m),
            ("CustomerApproved", 3_000_000m, 3_000_000m, 0m),
            ("Draft", 10_000_000m, 10_000_000m, 0m),
            ("Rejected", 2_000_000m, 2_000_000m, 0m)
        };

        var crVariance = PaymentCalculator.CalculateApprovedCrVariance(crList);
        Assert.Equal(8_000_000m, crVariance);

        var currentTotal = PaymentCalculator.CalculateCurrentTotal(baseValue, crVariance);
        Assert.Equal(108_000_000m, currentTotal);

        var payments = new (string Status, decimal? Amount)[]
        {
            ("Paid", 20_000_000m),
            ("Due", 50_000_000m)
        };
        var totalPaid = PaymentCalculator.CalculateTotalPaid(payments);
        Assert.Equal(20_000_000m, totalPaid);

        var balance = PaymentCalculator.CalculateOutstandingBalance(currentTotal, totalPaid);
        Assert.Equal(88_000_000m, balance);
    }

    [Fact]
    public async Task GetPaymentSummaryAsync_ReturnsAccurateSummaryForOwner()
    {
        var dummyStorage = new DummyFileStorage();
        var repo = new CustomerPaymentRepository(_db, dummyStorage);

        var summary = await repo.GetPaymentSummaryAsync(_customer.UserId);

        Assert.NotNull(summary);
        Assert.Equal(_project.ProjectCode, summary.ProjectCode);
        Assert.Equal(100_000_000m, summary.BaseContractValue);
        Assert.Equal(5_000_000m, summary.TotalApprovedCrVariance);
        Assert.Equal(105_000_000m, summary.CurrentProjectTotal);
        Assert.Equal(20_000_000m, summary.TotalAmountPaid);
        Assert.Equal(85_000_000m, summary.OutstandingBalance);
    }

    [Fact]
    public async Task GetPaymentSummaryAsync_ReturnsNullForUnrelatedUser()
    {
        var dummyStorage = new DummyFileStorage();
        var repo = new CustomerPaymentRepository(_db, dummyStorage);

        var summary = await repo.GetPaymentSummaryAsync(_otherCustomer.UserId);

        Assert.Null(summary);
    }

    [Fact]
    public async Task GetPaymentMilestonesAsync_GeneratesThreeMilestones()
    {
        var dummyStorage = new DummyFileStorage();
        var repo = new CustomerPaymentRepository(_db, dummyStorage);

        var milestones = await repo.GetPaymentMilestonesAsync(_customer.UserId);

        Assert.NotNull(milestones);
        Assert.Equal(3, milestones.Count);

        var nrd = milestones.First(m => m.PaymentType == "NRD");
        Assert.Equal("Paid", nrd.Status);
        Assert.Equal(20_000_000m, nrd.PaidAmount);
        Assert.True(nrd.ReceiptAvailable);

        var t30 = milestones.First(m => m.PaymentType == "T30");
        Assert.NotNull(t30);

        var final = milestones.First(m => m.PaymentType == "FINAL");
        Assert.NotNull(final);
    }

    [Fact]
    public async Task GetPaymentReceiptAsync_ReturnsBytesWhenReceiptExists()
    {
        var dummyStorage = new DummyFileStorage();
        var repo = new CustomerPaymentRepository(_db, dummyStorage);

        var receipt = await repo.GetPaymentReceiptAsync(_customer.UserId, paymentType: "NRD");

        Assert.NotNull(receipt);
        Assert.Equal(5, receipt.Content.Length);
        Assert.Equal("application/pdf", receipt.ContentType);
    }

    [Fact]
    public async Task FinalSettlementReceipt_DeniedWhenProjectNotSettled()
    {
        var dummyStorage = new DummyFileStorage();
        var repo = new CustomerPaymentRepository(_db, dummyStorage);

        // Project status is "Baseline Frozen", not "Settled" -> throws InvalidOperationException
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.GetFinalSettlementReceiptAsync(_customer.UserId));
    }

    private sealed class DummyFileStorage : IFileStorage
    {
        public Task UploadAsync(Stream content, string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<Stream?>(null);
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
