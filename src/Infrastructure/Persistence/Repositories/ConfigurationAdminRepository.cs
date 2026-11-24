using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Entities;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>UC-38 price frame history (read). Editing the catalog is a use case: Application/UseCases/Configuration.</summary>
public sealed class ConfigurationAdminRepository(WpsmDbContext db) : IConfigurationAdminRepository
{
    public async Task<IReadOnlyList<PriceFrameHistoryView>> GetPriceFrameHistoryAsync(int itemId, CancellationToken cancellationToken = default)
    {
        var rows = await db.PriceFrames.AsNoTracking().Where(x => x.ItemId == itemId).OrderByDescending(x => x.EffectiveFrom)
            .Select(x => new PriceFrameHistoryView(x.PriceFrameId, x.MinPrice, x.MaxPrice, x.EffectiveFrom, x.UpdatedByNavigation.FullName, x.UpdatedAt))
            .ToListAsync(cancellationToken);
        return rows.Select(x => x with { UpdatedAt = Utc(x.UpdatedAt) }).ToList();
    }
}
