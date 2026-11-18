using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Tests;

/// <summary>
/// Shared builders for the FE-02 / FE-05 / FE-07 / A-18 unit tests. Every test gets its own
/// in-memory WPSM database, so the fixtures below only create the rows a scenario needs.
/// </summary>
internal static class TestFixtures
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public static WpsmDbContext NewDb() =>
        new(new DbContextOptionsBuilder<WpsmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // The repositories open a transaction; the in-memory provider has none, and EF Core
            // raises TransactionIgnoredWarning as an error unless it is explicitly ignored here.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public static User NewUser(int id, string role, string? email = null, string? name = null) => new()
    {
        UserId = id,
        Email = email ?? $"{role.ToLowerInvariant()}{id}@test.local",
        FullName = name ?? $"{role} {id}",
        PasswordHash = "hash",
        Role = role,
        Status = "Active",
        CreatedAt = DateTime.UtcNow
    };

    public static Hall NewHall(int id = 1, string name = "Sảnh Hoàng Gia", string status = "Active") => new()
    {
        HallId = id,
        Name = name,
        MinCapacity = 100,
        MaxCapacity = 500,
        BasePrice = 50_000_000m,
        Status = status
    };

    public static Slot NewSlot(int id = 1, int hallId = 1, string name = "Evening") => new()
    {
        SlotId = id,
        HallId = hallId,
        SlotName = name,
        StartTime = new TimeOnly(17, 30),
        EndTime = new TimeOnly(21, 30)
    };

    public static ServiceCatalogItem NewCatalogItem(int id, string name, string category, string unit, decimal price, string status = "Active") => new()
    {
        ItemId = id,
        Name = name,
        Category = category,
        Unit = unit,
        DefaultPrice = price,
        Status = status
    };

    public static PriceFrame NewFrame(int itemId, decimal min, decimal max, int updatedBy = 1, DateOnly? from = null) => new()
    {
        ItemId = itemId,
        MinPrice = min,
        MaxPrice = max,
        EffectiveFrom = from ?? Today.AddDays(-30),
        UpdatedBy = updatedBy,
        UpdatedAt = DateTime.UtcNow
    };

    public static WeddingProject NewProject(
        int id,
        string code,
        int customerId,
        int managerId,
        int hallId = 1,
        int slotId = 1,
        DateOnly? eventDate = null,
        string status = "Active Baseline",
        decimal baseValue = 100_000_000m,
        bool frozen = false) => new()
    {
        ProjectId = id,
        ProjectCode = code,
        CustomerId = customerId,
        ManagerId = managerId,
        HallId = hallId,
        SlotId = slotId,
        EventDate = eventDate ?? Today.AddDays(30),
        BaseContractValue = baseValue,
        Nrdamount = baseValue * 0.3m,
        CurrentTotal = baseValue,
        Status = status,
        BaselineFrozenAt = frozen ? DateTime.UtcNow.AddDays(-5) : null,
        CreatedAt = DateTime.UtcNow.AddDays(-10)
    };

    public static SpecItem NewSpecItem(
        int id,
        int projectId,
        int catalogItemId,
        string description,
        int quantity,
        decimal unitPrice,
        bool baseline = true,
        int version = 1) => new()
    {
        SpecItemId = id,
        ProjectId = projectId,
        CatalogItemId = catalogItemId,
        Description = description,
        Quantity = quantity,
        UnitPrice = unitPrice,
        IsBaseline = baseline,
        Version = version
    };

    public static PolicyConfig NewPenaltyRule(int id, int tdayMin, int tdayMax, decimal rate, int updatedBy = 1) => new()
    {
        PolicyId = id,
        PolicyType = "CR_PENALTY",
        TdayMin = tdayMin,
        TdayMax = tdayMax,
        RatePercent = rate,
        EffectiveFrom = Today.AddDays(-30),
        UpdatedBy = updatedBy,
        UpdatedAt = DateTime.UtcNow
    };

    public static SpecificationRoadmapRepository NewSpecificationRepository(WpsmDbContext db, IEmailOutbox outbox) =>
        new(db, new ProjectReadModel(db), outbox, new EmailOptions { PublicBaseUrl = "http://test.local" });

    /// <summary>Captures the credential/roadmap e-mails the repositories enqueue.</summary>
    public sealed class RecordingEmailOutbox : IEmailOutbox
    {
        public List<(string To, string Subject, string Kind, int? ProjectId)> Messages { get; } = [];

        public void Enqueue(string toEmail, string subject, string bodyHtml, string kind, int? projectId) =>
            Messages.Add((toEmail, subject, kind, projectId));
    }
}
