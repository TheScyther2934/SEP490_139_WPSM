using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>UC-37 edit/deactivate, UC-38 PriceFrame history, UC-40 T-day penalty ranges (MSG23, GBR-14).</summary>
public sealed class ConfigurationAdminRepository(WpsmDbContext db) : IConfigurationAdminRepository
{
    public async Task<CatalogItemView?> UpdateCatalogItemAsync(int itemId, CatalogItemCommand command, CancellationToken cancellationToken = default)
    {
        var item = await db.ServiceCatalogItems.FirstOrDefaultAsync(x => x.ItemId == itemId, cancellationToken);
        if (item is null) return null;
        var category = WpsmCategoryNames.Normalize(command.Category);
        var name = command.Name?.Trim() ?? "";
        if (name.Length is < 1 or > 100 || command.Unit?.Trim().Length is not (>= 1 and <= 20) || command.BasePrice <= 0)
            throw new InvalidOperationException("Tên, đơn vị hoặc đơn giá Catalog không hợp lệ.");
        if (await db.ServiceCatalogItems.AnyAsync(x => x.ItemId != itemId && x.Category == category && x.Name == name, cancellationToken))
            throw new InvalidOperationException("Tên dịch vụ đã tồn tại trong nhóm này.");
        // Existing Spec Items keep their own description and price, so editing the catalog does not change signed baselines.
        item.Name = name;
        item.Category = category;
        item.Unit = command.Unit!.Trim();
        item.DefaultPrice = command.BasePrice;
        item.Status = command.Active ? "Active" : "Inactive";
        await db.SaveChangesAsync(cancellationToken);
        return ToView(item);
    }

    public async Task<CatalogItemView?> SetCatalogStatusAsync(int itemId, bool active, CancellationToken cancellationToken = default)
    {
        var item = await db.ServiceCatalogItems.FirstOrDefaultAsync(x => x.ItemId == itemId, cancellationToken);
        if (item is null) return null;
        item.Status = active ? "Active" : "Inactive";
        await db.SaveChangesAsync(cancellationToken);
        return ToView(item);
    }

    public async Task<IReadOnlyList<PriceFrameHistoryView>> GetPriceFrameHistoryAsync(int itemId, CancellationToken cancellationToken = default)
    {
        var rows = await db.PriceFrames.AsNoTracking().Where(x => x.ItemId == itemId).OrderByDescending(x => x.EffectiveFrom)
            .Select(x => new PriceFrameHistoryView(x.PriceFrameId, x.MinPrice, x.MaxPrice, x.EffectiveFrom, x.UpdatedByNavigation.FullName, x.UpdatedAt))
            .ToListAsync(cancellationToken);
        return rows.Select(x => x with { UpdatedAt = Utc(x.UpdatedAt) }).ToList();
    }

    private static CatalogItemView ToView(ServiceCatalogItem x) =>
        new(x.ItemId.ToString(), x.Name, WpsmCategoryNames.ToDisplay(x.Category), x.Unit, x.DefaultPrice, x.Status == "Active");
}
