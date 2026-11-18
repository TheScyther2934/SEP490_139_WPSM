using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>
/// FE-02 Specification — M-05 Specification Editor and Freeze Baseline (UC-20).
/// Covers the PriceFrame / Core-Combo guards (GBR-01/GBR-02/MSG06/MSG12) and the Master Roadmap
/// generation performed by <see cref="SpecificationRoadmapRepository"/>.
/// </summary>
public sealed class SpecificationRepositoryTests
{
    private const string ProjectCode = "WPSM-2026-014";
    private const int ManagerId = 2;
    private const int CustomerId = 1;

    private readonly WpsmDbContext _db;
    private readonly TestFixtures.RecordingEmailOutbox _outbox = new();
    private readonly SpecificationRoadmapRepository _repo;

    public SpecificationRepositoryTests()
    {
        _db = TestFixtures.NewDb();
        _repo = TestFixtures.NewSpecificationRepository(_db, _outbox);
    }

    // ── Seed + helpers ────────────────────────────────────────────────────────

    private async Task SeedAsync(
        DateOnly? eventDate = null,
        string status = "Active Baseline",
        bool frozen = false,
        bool withFrames = true,
        bool withAllCategories = true)
    {
        _db.Users.AddRange(
            TestFixtures.NewUser(CustomerId, "Customer", "customer@test.local", "Cô dâu Ánh"),
            TestFixtures.NewUser(ManagerId, "Manager", "manager@test.local", "Quản lý Minh"),
            TestFixtures.NewUser(3, "Manager", "manager3@test.local", "Quản lý Khác"),
            TestFixtures.NewUser(4, "Admin", "admin@test.local", "Admin"));

        _db.Halls.Add(TestFixtures.NewHall());
        _db.Slots.Add(TestFixtures.NewSlot());

        _db.ServiceCatalogItems.AddRange(
            TestFixtures.NewCatalogItem(10, "Sảnh Hoàng Gia", "Hall", "Sảnh", 50_000_000m),
            TestFixtures.NewCatalogItem(11, "Thực đơn tiệc 8 món", "Catering", "Bàn", 3_500_000m),
            TestFixtures.NewCatalogItem(12, "Trang trí sân khấu", "Decor", "Gói", 8_000_000m),
            TestFixtures.NewCatalogItem(13, "MC chuyên nghiệp", "MC", "Buổi", 5_000_000m),
            TestFixtures.NewCatalogItem(14, "Âm thanh & ánh sáng", "AV", "Gói", 6_000_000m),
            TestFixtures.NewCatalogItem(15, "Quay chụp ngày cưới", "Photo/Video", "Gói", 12_000_000m),
            TestFixtures.NewCatalogItem(16, "Trang trí cũ", "Decor", "Gói", 7_000_000m, "Inactive"));

        _db.WeddingProjects.Add(TestFixtures.NewProject(14, ProjectCode, CustomerId, ManagerId,
            eventDate: eventDate, status: status, baseValue: 100_000_000m, frozen: frozen));

        _db.SpecItems.Add(TestFixtures.NewSpecItem(100, 14, 10, "Sảnh Hoàng Gia - tiệc tối", 1, 50_000_000m));
        _db.SpecItems.Add(TestFixtures.NewSpecItem(101, 14, 11, "Thực đơn tiệc 8 món", 30, 3_500_000m));
        _db.SpecItems.Add(TestFixtures.NewSpecItem(102, 14, 12, "Trang trí sân khấu", 1, 8_000_000m));
        if (withAllCategories)
        {
            _db.SpecItems.Add(TestFixtures.NewSpecItem(103, 14, 13, "MC chuyên nghiệp", 1, 5_000_000m));
            _db.SpecItems.Add(TestFixtures.NewSpecItem(104, 14, 14, "Âm thanh & ánh sáng", 1, 6_000_000m));
        }

        if (withFrames)
        {
            _db.PriceFrames.AddRange(
                TestFixtures.NewFrame(10, 40_000_000m, 60_000_000m),
                TestFixtures.NewFrame(11, 3_000_000m, 4_000_000m),
                TestFixtures.NewFrame(12, 6_000_000m, 10_000_000m),
                TestFixtures.NewFrame(13, 4_000_000m, 7_000_000m),
                TestFixtures.NewFrame(14, 5_000_000m, 8_000_000m),
                TestFixtures.NewFrame(15, 10_000_000m, 15_000_000m),
                TestFixtures.NewFrame(16, 5_000_000m, 9_000_000m));
        }

        await _db.SaveChangesAsync();
    }

    private static List<SpecificationLineCommand> BaselineLines() =>
    [
        new(10, "Sảnh Hoàng Gia - tiệc tối", 1, 50_000_000m),
        new(11, "Thực đơn tiệc 8 món", 30, 3_500_000m),
        new(12, "Trang trí sân khấu", 1, 8_000_000m),
        new(13, "MC chuyên nghiệp", 1, 5_000_000m),
        new(14, "Âm thanh & ánh sáng", 1, 6_000_000m)
    ];

    private static SpecificationCommand Command(IReadOnlyList<SpecificationLineCommand>? items, int guestCount = 300) =>
        new("Đặc tả tiệc cưới", guestCount, items);

    // ── UpdateSpecificationAsync ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateSpecification_PersistsNewAndEditedItems()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines[1] = lines[1] with { Quantity = 35, Description = "Thực đơn tiệc 8 món - tăng 5 bàn" };
        lines.Add(new SpecificationLineCommand(15, "Quay chụp ngày cưới", 1, 12_000_000m));

        var view = await _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines));

        Assert.NotNull(view);
        Assert.Equal(6, view!.Specification!.Count);
        Assert.Equal(35, view.GuestCount);
        var catering = await _db.SpecItems.SingleAsync(x => x.SpecItemId == 101);
        Assert.Equal(35, catering.Quantity);
        Assert.Contains("tăng 5 bàn", catering.Description);
        Assert.True(await _db.SpecItems.AnyAsync(x => x.CatalogItemId == 15 && x.Version == 1));
    }

    [Fact]
    public async Task UpdateSpecification_RemovesItemsOmittedFromPayload()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines.RemoveAll(x => x.CatalogItemId == 14); // bỏ hạng mục AV

        var view = await _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines));

        Assert.NotNull(view);
        Assert.Equal(4, view!.Specification!.Count);
        Assert.False(await _db.SpecItems.AnyAsync(x => x.CatalogItemId == 14));
    }

    [Fact]
    public async Task UpdateSpecification_KeepsExistingSpecWhenItemsAreNull()
    {
        await SeedAsync();

        var view = await _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(null, guestCount: 250));

        Assert.NotNull(view);
        Assert.Equal(5, view!.Specification!.Count);
        Assert.Equal(5, await _db.SpecItems.CountAsync());
    }

    [Fact]
    public async Task UpdateSpecification_RejectsDuplicateCatalogItem()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines.Add(lines[1]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines)));

        Assert.Contains("một lần", ex.Message);
    }

    [Fact]
    public async Task UpdateSpecification_RejectsNonPositiveQuantity()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines[1] = lines[1] with { Quantity = 0 };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines)));

        Assert.Contains("số lượng dương", ex.Message);
    }

    [Fact]
    public async Task UpdateSpecification_RejectsBlankDescription()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines[2] = lines[2] with { Description = "   " };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines)));

        Assert.Contains("mô tả", ex.Message);
    }

    [Fact]
    public async Task UpdateSpecification_RejectsDescriptionLongerThan500Characters()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines[2] = lines[2] with { Description = new string('a', 501) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines)));

        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task UpdateSpecification_AcceptsDescriptionOfExactly500Characters()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines[2] = lines[2] with { Description = new string('a', 500) };

        var view = await _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines));

        Assert.NotNull(view);
    }

    [Fact]
    public async Task UpdateSpecification_RejectsInactiveCatalogItem()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines[4] = lines[4] with { CatalogItemId = 16, UnitPrice = 7_000_000m };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines)));

        Assert.Contains("không còn hoạt động", ex.Message);
    }

    [Fact]
    public async Task UpdateSpecification_RejectsPriceBelowPriceFrame()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines[1] = lines[1] with { UnitPrice = 2_500_000m };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines)));

        Assert.Contains("MSG06", ex.Message);
    }

    [Fact]
    public async Task UpdateSpecification_RejectsPriceAbovePriceFrame()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines[1] = lines[1] with { UnitPrice = 4_500_000m };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines)));

        Assert.Contains("MSG06", ex.Message);
    }

    [Fact]
    public async Task UpdateSpecification_AcceptsPricesAtFrameBoundaries()
    {
        await SeedAsync();
        var lines = BaselineLines();
        lines[1] = lines[1] with { UnitPrice = 3_000_000m }; // đúng MinPrice
        lines[2] = lines[2] with { UnitPrice = 10_000_000m }; // đúng MaxPrice

        var view = await _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(lines));

        Assert.NotNull(view);
        Assert.Equal(3_000_000m, (await _db.SpecItems.SingleAsync(x => x.SpecItemId == 101)).UnitPrice);
        Assert.Equal(10_000_000m, (await _db.SpecItems.SingleAsync(x => x.SpecItemId == 102)).UnitPrice);
    }

    [Fact]
    public async Task UpdateSpecification_RejectsEditAfterBaselineFrozen()
    {
        await SeedAsync(status: "Baseline Frozen", frozen: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(BaselineLines())));

        Assert.Contains("MSG09", ex.Message);
    }

    [Fact]
    public async Task UpdateSpecification_RejectsNegativeGuestCount()
    {
        await SeedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateSpecificationAsync(ProjectCode, ManagerId, Command(BaselineLines(), guestCount: -1)));

        Assert.Contains("Số lượng khách", ex.Message);
    }

    [Fact]
    public async Task UpdateSpecification_ReturnsNullForAnotherManagersProject()
    {
        await SeedAsync();

        var view = await _repo.UpdateSpecificationAsync(ProjectCode, managerId: 3, Command(BaselineLines()));

        Assert.Null(view);
    }

    // ── FreezeBaselineAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task FreezeBaseline_CreatesMasterRoadmapAndNotifiesCustomer()
    {
        await SeedAsync(eventDate: TestFixtures.Today.AddDays(30));

        var view = await _repo.FreezeBaselineAsync(ProjectCode, ManagerId);

        Assert.NotNull(view);
        Assert.Equal("Baseline Frozen", view!.Status);
        Assert.NotNull(view.BaselineFrozenAt);
        Assert.Equal(11, await _db.RoadmapTasks.CountAsync());
        Assert.Equal(11, (await _repo.GetRoadmapAsync(ProjectCode)).Count);
        Assert.True(await _db.Notifications.AnyAsync(n => n.RecipientId == CustomerId && n.Type == "Roadmap reminder"));
        var mail = Assert.Single(_outbox.Messages);
        Assert.Equal("Reminder", mail.Kind);
        Assert.Equal(14, mail.ProjectId);
    }

    [Fact]
    public async Task FreezeBaseline_RejectsMissingRequiredCategory()
    {
        await SeedAsync(withAllCategories: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.FreezeBaselineAsync(ProjectCode, ManagerId));

        Assert.Contains("MSG12", ex.Message);
        Assert.Contains("MC", ex.Message);
    }

    [Fact]
    public async Task FreezeBaseline_RejectsItemWithoutCurrentPriceFrame()
    {
        await SeedAsync();
        var frame = await _db.PriceFrames.SingleAsync(x => x.ItemId == 13);
        _db.PriceFrames.Remove(frame);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.FreezeBaselineAsync(ProjectCode, ManagerId));

        Assert.Contains("PriceFrame", ex.Message);
    }

    [Fact]
    public async Task FreezeBaseline_RejectsInactiveCatalogItem()
    {
        await SeedAsync();
        var mc = await _db.ServiceCatalogItems.SingleAsync(x => x.ItemId == 13);
        mc.Status = "Inactive";
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.FreezeBaselineAsync(ProjectCode, ManagerId));

        Assert.Contains("ngừng hoạt động", ex.Message);
    }

    [Fact]
    public async Task FreezeBaseline_RejectsSecondFreeze()
    {
        await SeedAsync();
        await _repo.FreezeBaselineAsync(ProjectCode, ManagerId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.FreezeBaselineAsync(ProjectCode, ManagerId));

        Assert.Contains("không còn ở trạng thái", ex.Message);
    }

    [Fact]
    public async Task FreezeBaseline_ClampsOnlyTheT90MilestonesToToday()
    {
        var eventDate = TestFixtures.Today.AddDays(10);
        await SeedAsync(eventDate: eventDate);

        var view = await _repo.FreezeBaselineAsync(ProjectCode, ManagerId);

        Assert.NotNull(view);
        var tasks = await _db.RoadmapTasks.ToListAsync();
        Assert.All(tasks.Where(t => t.Milestone == "T-90"), task => Assert.Equal(TestFixtures.Today, task.DueDate));
        Assert.All(tasks.Where(t => t.Milestone == "T-7"), task => Assert.Equal(eventDate.AddDays(-7), task.DueDate));
        Assert.All(tasks.Where(t => t.Milestone == "Event Day"), task => Assert.Equal(eventDate, task.DueDate));
    }

    [Fact]
    public async Task FreezeBaseline_ReturnsNullForUnknownProject()
    {
        await SeedAsync();

        var view = await _repo.FreezeBaselineAsync("WPSM-2026-999", ManagerId);

        Assert.Null(view);
    }

    // ── GetRoadmapAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoadmap_ReturnsTasksOrderedByDueDate()
    {
        await SeedAsync();
        _db.RoadmapTasks.AddRange(
            new RoadmapTask { ProjectId = 14, Milestone = "T-7", Title = "Duyệt BEO", AssigneeRole = "Manager", DueDate = TestFixtures.Today.AddDays(20), Status = "Pending" },
            new RoadmapTask { ProjectId = 14, Milestone = "T-30", Title = "Chốt thực đơn", AssigneeRole = "Manager", DueDate = TestFixtures.Today.AddDays(5), Status = "Pending" },
            new RoadmapTask { ProjectId = 14, Milestone = "T-90", Title = "Ký hợp đồng", AssigneeRole = "Customer", DueDate = TestFixtures.Today.AddDays(-5), Status = "Done" });
        await _db.SaveChangesAsync();

        var roadmap = await _repo.GetRoadmapAsync(ProjectCode);

        Assert.Equal(3, roadmap.Count);
        Assert.Equal("Ký hợp đồng", roadmap[0].Title);
        Assert.Equal("Chốt thực đơn", roadmap[1].Title);
        Assert.Equal("Duyệt BEO", roadmap[2].Title);
    }

    [Fact]
    public async Task GetRoadmap_ReturnsEmptyForUnknownProject()
    {
        await SeedAsync();

        var roadmap = await _repo.GetRoadmapAsync("WPSM-2026-999");

        Assert.Empty(roadmap);
    }
}
