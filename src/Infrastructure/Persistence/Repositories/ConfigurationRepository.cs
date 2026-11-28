using Microsoft.EntityFrameworkCore;
using Domain.Services;
using Application.Persistence;
using Infrastructure.Persistence;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>Reads of the configuration screens. Changes are use cases: Application/UseCases/Configuration.</summary>
public sealed class ConfigurationRepository(WpsmDbContext db) : IConfigurationRepository
{
    public async Task<IReadOnlyList<CatalogItemView>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        await db.ServiceCatalogItems.AsNoTracking().OrderBy(x => x.Category).ThenBy(x => x.Name)
            .Select(x => new CatalogItemView(x.ItemId.ToString(), x.Name, WpsmCategoryNames.ToDisplay(x.Category), x.Unit, x.DefaultPrice, x.Status == "Active", x.RequiresSupplier))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PriceFrameView>> GetPriceFramesAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var frames = await db.PriceFrames.AsNoTracking().Include(x => x.Item)
            .Where(x => x.EffectiveFrom <= today).OrderByDescending(x => x.EffectiveFrom).ToListAsync(cancellationToken);
        return frames.GroupBy(x => x.ItemId).Select(g => g.First())
            .OrderBy(x => x.Item.Category).ThenBy(x => x.Item.Name)
            .Select(x => new PriceFrameView(x.PriceFrameId.ToString(), x.Item.Name, WpsmCategoryNames.ToDisplay(x.Item.Category), x.MinPrice, x.MaxPrice, "VND", x.ItemId))
            .ToList();
    }

    public async Task<PolicyView> GetPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var (p, effectiveFrom) = await EventFlowSupport.LoadPolicyVersionAsync(db, VietnamTime.GetVietnamToday(), cancellationToken);
        // Running events keep the terms they were signed with; count those whose terms differ from the policy in force.
        var runningTerms = await db.EventTerms.AsNoTracking()
            .Where(t => t.Project.Status == EventStatus.Adjusting || t.Project.Status == EventStatus.Locked || t.Project.Status == EventStatus.Completed)
            .ToListAsync(cancellationToken);
        var stamp = p.Stamp();
        return new PolicyView(p.DepositRatio, p.MaxDecreaseRatio, p.BookingMinDays, p.BookingMaxDays, p.LockDays, p.SupplierDeadlineDays,
            p.SettlementDueDays, await EventFlowSupport.LoadHotlineAsync(db, cancellationToken))
        {
            Stamp = stamp,
            EffectiveFrom = effectiveFrom,
            EventsOnOlderTerms = runningTerms.Count(t => t.ToPolicy().Stamp() != stamp)
        };
    }
}
