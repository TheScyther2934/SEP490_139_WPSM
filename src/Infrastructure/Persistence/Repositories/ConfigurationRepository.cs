using Microsoft.EntityFrameworkCore;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Persistence;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.Infrastructure.Persistence.Repositories;

public sealed class ConfigurationRepository(WpsmDbContext db) : IConfigurationRepository
{
    public async Task<IReadOnlyList<CatalogItemView>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        await db.ServiceCatalogItems.AsNoTracking().OrderBy(x => x.Category).ThenBy(x => x.Name)
            .Select(x => new CatalogItemView(x.ItemId.ToString(), x.Name, WpsmCategoryNames.ToDisplay(x.Category), x.Unit, x.DefaultPrice, x.Status == "Active"))
            .ToListAsync(cancellationToken);

    public async Task<CatalogItemView> AddCatalogItemAsync(CatalogItemCommand command, CancellationToken cancellationToken = default)
    {
        var category = WpsmCategoryNames.Normalize(command.Category);
        if (command.Name.Trim().Length is < 1 or > 100 || command.Unit.Trim().Length is < 1 or > 20 || command.BasePrice <= 0)
            throw new InvalidOperationException("Tên, đơn vị hoặc đơn giá Catalog không hợp lệ.");
        var entity = new ServiceCatalogItem
        {
            Name = command.Name.Trim(), Category = category, Unit = command.Unit.Trim(),
            DefaultPrice = command.BasePrice, Status = command.Active ? "Active" : "Inactive"
        };
        db.ServiceCatalogItems.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new CatalogItemView(entity.ItemId.ToString(), entity.Name, WpsmCategoryNames.ToDisplay(entity.Category), entity.Unit, entity.DefaultPrice, entity.Status == "Active");
    }

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

    public async Task<PriceFrameView?> UpdatePriceFrameAsync(int priceFrameId, PriceFrameCommand command, int updatedBy, CancellationToken cancellationToken = default)
    {
        if (command.MinPrice < 0 || command.MaxPrice < command.MinPrice) throw new InvalidOperationException("Giá tối thiểu phải không âm và không vượt giá tối đa.");
        var current = await db.PriceFrames.Include(x => x.Item).FirstOrDefaultAsync(x => x.PriceFrameId == priceFrameId, cancellationToken);
        if (current is null) return null;
        var effectiveFrom = command.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.Today);
        if (effectiveFrom < DateOnly.FromDateTime(DateTime.Today)) throw new InvalidOperationException("Ngày hiệu lực không được ở trong quá khứ.");
        var target = await db.PriceFrames.FirstOrDefaultAsync(x => x.ItemId == current.ItemId && x.EffectiveFrom == effectiveFrom, cancellationToken);
        if (target is null)
        {
            target = new PriceFrame { ItemId = current.ItemId, EffectiveFrom = effectiveFrom, UpdatedBy = updatedBy, MinPrice = command.MinPrice, MaxPrice = command.MaxPrice, UpdatedAt = DateTime.UtcNow };
            db.PriceFrames.Add(target);
        }
        else
        {
            target.MinPrice = command.MinPrice;
            target.MaxPrice = command.MaxPrice;
            target.UpdatedBy = updatedBy;
            target.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return new PriceFrameView(target.PriceFrameId.ToString(), current.Item.Name, WpsmCategoryNames.ToDisplay(current.Item.Category), target.MinPrice, target.MaxPrice, "VND", current.ItemId);
    }

    public async Task<PolicyView> GetPoliciesAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var active = await db.PolicyConfigs.AsNoTracking().Where(x => x.EffectiveFrom <= today)
            .OrderByDescending(x => x.EffectiveFrom).ToListAsync(cancellationToken);
        var nrd = active.FirstOrDefault(x => x.PolicyType == "NRD_RATIO")?.RatePercent ?? 30m;
        var t30 = active.FirstOrDefault(x => x.PolicyType == "T30_RATIO")?.RatePercent ?? 70m;
        var cr = active.Where(x => x.PolicyType == "CR_PENALTY").OrderBy(x => x.TdayMin).FirstOrDefault();
        return new PolicyView(nrd, t30, Math.Max(0, 100m - t30), cr?.TdayMax ?? 14, cr?.RatePercent ?? 5m);
    }

    public async Task<PolicyView> UpdatePoliciesAsync(PolicyCommand command, int updatedBy, CancellationToken cancellationToken = default)
    {
        var effective = command.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.Today);
        if (effective < DateOnly.FromDateTime(DateTime.Today)) throw new InvalidOperationException("Ngày hiệu lực không được ở trong quá khứ.");
        if (command.NrdRatio < 0 || command.T30Ratio < command.NrdRatio || command.T30Ratio > 100 || command.CrPenaltyDays < 0 || command.CrPenaltyPercent is < 0 or > 100)
            throw new InvalidOperationException("Tỷ lệ hoặc ngưỡng phạt chính sách không hợp lệ.");
        await UpsertPolicyAsync("NRD_RATIO", null, null, command.NrdRatio, effective, updatedBy, cancellationToken);
        await UpsertPolicyAsync("T30_RATIO", null, null, command.T30Ratio, effective, updatedBy, cancellationToken);
        await UpsertPolicyAsync("CR_PENALTY", 0, command.CrPenaltyDays, command.CrPenaltyPercent, effective, updatedBy, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetPoliciesAsync(cancellationToken);
    }

    private async Task UpsertPolicyAsync(string type, int? min, int? max, decimal rate, DateOnly effective, int updatedBy, CancellationToken cancellationToken)
    {
        var policy = await db.PolicyConfigs.FirstOrDefaultAsync(x => x.PolicyType == type && x.TdayMin == min && x.TdayMax == max && x.EffectiveFrom == effective, cancellationToken);
        if (policy is null)
        {
            db.PolicyConfigs.Add(new PolicyConfig
            {
                PolicyType = type, TdayMin = min, TdayMax = max, RatePercent = rate,
                EffectiveFrom = effective, UpdatedBy = updatedBy, UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            policy.RatePercent = rate;
            policy.UpdatedBy = updatedBy;
            policy.UpdatedAt = DateTime.UtcNow;
        }
    }
}
