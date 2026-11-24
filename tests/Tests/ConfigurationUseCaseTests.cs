using Application.Persistence;
using Application.UseCases.Configuration;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.UseCases;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Tests;

/// <summary>Admin configuration (UC-37..UC-39): rules alone, then the use cases on an in-memory database.</summary>
public sealed class ConfigurationUseCaseTests
{
    private const int AdminId = 3;
    private readonly WpsmDbContext _db = TestFixtures.NewDb();
    private static DateOnly Today => VietnamTime.GetVietnamToday();

    [Fact]
    public void Categories_accept_vietnamese_or_english_names_and_unknown_ones_are_refused()
    {
        Assert.Equal("Catering", ServiceCategories.Normalize(" Ẩm thực "));
        Assert.Equal("Photo/Video", ServiceCategories.Normalize("media"));
        Assert.Equal("Quay chụp", ServiceCategories.Display("Photo/Video"));
        Assert.Throws<BusinessRuleException>(() => ServiceCategories.Normalize("Pháo hoa"));
    }

    [Fact]
    public void Price_frames_and_policies_take_effect_from_today_and_stay_coherent()
    {
        Assert.Throws<BusinessRuleException>(() => ConfigurationPolicy.ValidatePriceFrame(5, 4, null, Today));
        Assert.Throws<BusinessRuleException>(() => ConfigurationPolicy.ValidatePriceFrame(1, 4, Today.AddDays(-1), Today));
        Assert.Equal(Today, ConfigurationPolicy.ValidatePriceFrame(1, 4, null, Today));

        Assert.Equal(Today, ConfigurationPolicy.ValidateEventPolicy(EventPolicy.Default, null, Today));
        Assert.Throws<BusinessRuleException>(() => ConfigurationPolicy.ValidateEventPolicy(EventPolicy.Default with { MaxDecreaseRatio = 80 }, null, Today));
        Assert.Throws<BusinessRuleException>(() => ConfigurationPolicy.ValidateEventPolicy(EventPolicy.Default with { LockDays = 5 }, null, Today));
        Assert.Throws<BusinessRuleException>(() => ConfigurationPolicy.ValidateEventPolicy(EventPolicy.Default with { LockDays = 20 }, null, Today));
        Assert.Null(ConfigurationPolicy.ValidateHotline("  "));
        Assert.Throws<BusinessRuleException>(() => ConfigurationPolicy.ValidateHotline("0976-ABC"));
    }

    private ConfigurationStore Store() => new(_db);

    private async Task SeedAsync()
    {
        _db.Users.Add(TestFixtures.NewUser(AdminId, "Admin", "admin@test.local", "Admin"));
        _db.ServiceCatalogItems.Add(TestFixtures.NewCatalogItem(11, "Thực đơn tiệc 8 món", "Catering", "Bàn", 3_500_000m));
        _db.PriceFrames.Add(new PriceFrame { PriceFrameId = 7, ItemId = 11, MinPrice = 3_000_000m, MaxPrice = 4_000_000m, EffectiveFrom = Today.AddDays(-30), UpdatedBy = AdminId, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Catalog_names_are_unique_within_a_category()
    {
        await SeedAsync();

        var added = await new AddCatalogItem(Store(), new UnitOfWork(_db)).HandleAsync(new CatalogItemCommand(" Thực đơn chay ", "ẩm thực", " Bàn ", 2_800_000m, true));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new AddCatalogItem(Store(), new UnitOfWork(_db)).HandleAsync(new CatalogItemCommand("Thực đơn chay", "Catering", "Bàn", 1m, true)));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            new UpdateCatalogItem(Store(), new UnitOfWork(_db)).HandleAsync(11, new CatalogItemCommand("Thực đơn chay", "Catering", "Bàn", 1m, true)));
        var off = await new SetCatalogItemStatus(Store(), new UnitOfWork(_db)).HandleAsync(11, false);

        Assert.Equal(("Thực đơn chay", "Ẩm thực", "Bàn"), (added.Name, added.Category, added.Unit));
        Assert.False(off!.Active);
        Assert.Null(await new SetCatalogItemStatus(Store(), new UnitOfWork(_db)).HandleAsync(999, true));
    }

    [Fact]
    public async Task A_price_frame_from_a_new_date_adds_a_version_and_the_same_date_changes_it()
    {
        await SeedAsync();
        var update = new UpdatePriceFrame(Store(), new UnitOfWork(_db));

        await update.HandleAsync(7, new PriceFrameCommand(3_200_000m, 4_200_000m, Today.AddDays(5)), AdminId);
        await update.HandleAsync(7, new PriceFrameCommand(3_300_000m, 4_300_000m, Today.AddDays(5)), AdminId);

        var frames = await _db.PriceFrames.AsNoTracking().OrderBy(f => f.EffectiveFrom).ToListAsync();
        Assert.Equal(2, frames.Count);
        Assert.Equal((3_000_000m, 3_300_000m), (frames[0].MinPrice, frames[1].MinPrice));
    }

    [Fact]
    public async Task Policies_add_versions_only_for_changed_values_and_save_the_hotline()
    {
        await SeedAsync();
        var update = new UpdateEventPolicies(Store(), new ConfigurationRepository(_db), new UnitOfWork(_db));
        var d = EventPolicy.Default;

        var view = await update.HandleAsync(new PolicyCommand(40, d.MaxDecreaseRatio, d.BookingMinDays, d.BookingMaxDays, d.LockDays, d.SupplierDeadlineDays,
            d.SettlementDueDays, " 0901 234 567 "), AdminId);

        Assert.Equal((40m, "0901 234 567"), (view.DepositRatio, view.Hotline));
        Assert.Equal(EventPolicy.Keys.DepositRatio, (await _db.PolicyConfigs.SingleAsync()).PolicyType);
        await Assert.ThrowsAsync<BusinessRuleException>(() => update.HandleAsync(new PolicyCommand(80, 30, 15, 90, 10, 8, 7), AdminId));
    }
}
