using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence;
using Domain.Entities;
using Infrastructure.Persistence.Repositories;
using Xunit;

namespace Tests;

/// <summary>Read side of the complaint screens. Rating and complaint changes are covered by PostEventTests.</summary>
public sealed class ComplaintRatingRepositoryTests
{
    private readonly WpsmDbContext _db;
    private readonly ComplaintRatingRepository _repo;

    public ComplaintRatingRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WpsmDbContext(options);
        _repo = new ComplaintRatingRepository(_db);
    }

    private static User CreateUser(int id, string email, string role) =>
        new() { UserId = id, Email = email, FullName = email, Role = role, PasswordHash = "hash", Status = "Active" };

    [Fact]
    public async Task GetCustomerBeoOptionsAsync_ReturnsOrderedBeoVersions()
    {
        // Arrange
        var customer = CreateUser(1, "c@test.com", "Customer");
        var manager = CreateUser(2, "m@test.com", "Manager");
        var hall = new Hall { HallId = 1, Name = "Hall A", MinCapacity = 100, MaxCapacity = 200, BasePrice = 10m, Status = "Active" };
        var slot = new Slot { SlotId = 1, HallId = 1, SlotName = "Evening", StartTime = new TimeOnly(17, 30), EndTime = new TimeOnly(21, 30) };
        var project = new WeddingProject
        {
            ProjectId = 10,
            ProjectCode = "WPSM-2025-001",
            CustomerId = 1,
            ManagerId = 2,
            HallId = 1,
            SlotId = 1,
            EventDate = new DateOnly(2025, 10, 25),
            BaseContractValue = 100m,
            DepositAmount = 30m,
            CurrentTotal = 100m,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };
        var beo1 = new Beo { Beoid = 101, ProjectId = 10, Version = 1, Status = "Frozen", FrozenAt = DateTime.UtcNow.AddDays(-5), Notes = "v1" };
        var beo2 = new Beo { Beoid = 102, ProjectId = 10, Version = 2, Status = "Frozen", FrozenAt = DateTime.UtcNow.AddDays(-2), Notes = "v2" };

        _db.Users.AddRange(customer, manager);
        _db.Halls.Add(hall);
        _db.Slots.Add(slot);
        _db.WeddingProjects.Add(project);
        _db.Beos.AddRange(beo1, beo2);
        await _db.SaveChangesAsync();

        // Act
        var options = await _repo.GetCustomerBeoOptionsAsync(1, "WPSM-2025-001");

        // Assert
        Assert.NotNull(options);
        Assert.Equal(2, options.Count);
        Assert.Equal(2, options[0].Version);
        Assert.Equal(1, options[1].Version);
    }
}
