using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

public sealed class AddendumRepositoryTests
{
    private readonly WpsmDbContext _db;
    private readonly AddendumRepository _repo;

    public AddendumRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WpsmDbContext(options);
        _repo = new AddendumRepository(_db);
    }

    private static User CreateUser(int id, string email, string role) =>
        new() { UserId = id, Email = email, FullName = email, Role = role, PasswordHash = "hash", Status = "Active" };

    [Fact]
    public async Task CreateCustomerChangeRequestAsync_CalculatesCostImpactAndPenaltyFee()
    {
        // Arrange
        var customer = CreateUser(1, "customer@test.com", "Customer");
        var manager = CreateUser(2, "manager@test.com", "Manager");
        var hall = new Hall { HallId = 1, Name = "Sảnh Hoàng Gia", MinCapacity = 100, MaxCapacity = 500, BasePrice = 50_000_000m, Status = "Active" };
        var slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Evening", StartTime = new TimeOnly(17, 30), EndTime = new TimeOnly(21, 30) };
        var catalogItem = new ServiceCatalogItem
        {
            ItemId = 10,
            Name = "Thực đơn tiệc cưới",
            Category = "Catering",
            Unit = "Bàn",
            DefaultPrice = 3_500_000m,
            Status = "Active"
        };
        var project = new WeddingProject
        {
            ProjectId = 14,
            ProjectCode = "WPSM-2026-014",
            CustomerId = 1,
            ManagerId = 2,
            HallId = 1,
            SlotId = 1,
            EventDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)), // 10 days until event -> hits T-14 penalty
            BaseContractValue = 185_000_000m,
            Nrdamount = 55_500_000m,
            CurrentTotal = 185_000_000m,
            Status = "Baseline Frozen",
            CreatedAt = DateTime.UtcNow
        };
        var specItem = new SpecItem
        {
            SpecItemId = 100,
            ProjectId = 14,
            CatalogItemId = 10,
            Description = "Thực đơn tiêu chuẩn",
            Quantity = 30,
            UnitPrice = 3_500_000m,
            IsBaseline = true,
            Version = 1
        };
        var policy = new PolicyConfig
        {
            PolicyId = 1,
            PolicyType = "CR_PENALTY",
            TdayMin = 0,
            TdayMax = 14,
            RatePercent = 5m,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)),
            UpdatedBy = 2,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.AddRange(customer, manager);
        _db.Halls.Add(hall);
        _db.Slots.Add(slot);
        _db.ServiceCatalogItems.Add(catalogItem);
        _db.WeddingProjects.Add(project);
        _db.SpecItems.Add(specItem);
        _db.PolicyConfigs.Add(policy);
        await _db.SaveChangesAsync();

        // Act 1: Get creation context (C-06)
        var ctx = await _repo.GetCrCreationContextAsync("WPSM-2026-014", 1);
        Assert.NotNull(ctx);
        Assert.Single(ctx.SpecItems);
        Assert.Equal(5m, ctx.PenaltyRate);

        // Act 2: Create CR adding 5 tables (30 -> 35 tables @ 3,500,000 = +17,500,000 VND, 5% penalty = 875,000 VND)
        var created = await _repo.CreateCustomerChangeRequestAsync(
            1,
            "WPSM-2026-014",
            "Table count",
            "Bổ sung 5 bàn tiệc cho khách gia đình.",
            [new CreateCrItemInput(100, 35, 3_500_000m)]);

        // Assert
        Assert.NotNull(created);
        Assert.Equal("CR-014-01", created.CrCode);
        Assert.Equal("Negotiating", created.Status);
        Assert.Equal(17_500_000m, created.CostImpact);
        Assert.Equal(5m, created.PenaltyRate);
        Assert.Equal(875_000m, created.PenaltyFee);
        Assert.Equal(18_375_000m, created.TotalVariance);
        Assert.Single(created.Items);
        Assert.Equal(17_500_000m, created.Items[0].DeltaAmount);
    }
}
