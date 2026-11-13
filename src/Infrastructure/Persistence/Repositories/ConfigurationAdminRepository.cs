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

    public async Task<IReadOnlyList<PenaltyRuleView>> GetPenaltyRulesAsync(CancellationToken cancellationToken = default) =>
        (await PenaltyRulesAsync(db, TodayVn(), cancellationToken))
            .Select(x => new PenaltyRuleView(x.TdayMin ?? 0, x.TdayMax ?? 0, x.RatePercent, x.EffectiveFrom)).ToList();

    public async Task<IReadOnlyList<PenaltyRuleView>> UpdatePenaltyRulesAsync(IReadOnlyList<PenaltyRuleCommand> rules, DateOnly? effectiveFrom, int updatedBy, CancellationToken cancellationToken = default)
    {
        var effective = effectiveFrom ?? TodayVn();
        if (effective < TodayVn()) throw new InvalidOperationException("Ngày hiệu lực không được ở trong quá khứ.");
        if (rules.Count == 0) throw new InvalidOperationException("Cần ít nhất một khoảng T-day.");
        if (rules.Any(x => x.TDayMin < 0 || x.TDayMax < x.TDayMin)) throw new InvalidOperationException("T-day tối thiểu phải ≥ 0 và không lớn hơn T-day tối đa.");
        if (rules.Any(x => x.RatePercent is < 0 or > 100)) throw new InvalidOperationException("Tỷ lệ phạt phải từ 0 đến 100%.");
        var ordered = rules.OrderBy(x => x.TDayMin).ToList();
        for (var i = 1; i < ordered.Count; i++)
            if (ordered[i].TDayMin <= ordered[i - 1].TDayMax)
                throw new InvalidOperationException($"Các khoảng T-day không được chồng lấn ({ordered[i - 1].TDayMin}–{ordered[i - 1].TDayMax} và {ordered[i].TDayMin}–{ordered[i].TDayMax}) (MSG23).");

        // A rule set is versioned by its effective date (GBR-14): saving again on the same date replaces that set only.
        var sameDate = await db.PolicyConfigs.Where(x => x.PolicyType == "CR_PENALTY" && x.EffectiveFrom == effective).ToListAsync(cancellationToken);
        db.PolicyConfigs.RemoveRange(sameDate);
        foreach (var rule in ordered)
            db.PolicyConfigs.Add(new PolicyConfig
            {
                PolicyType = "CR_PENALTY", TdayMin = rule.TDayMin, TdayMax = rule.TDayMax, RatePercent = rule.RatePercent,
                EffectiveFrom = effective, UpdatedBy = updatedBy, UpdatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync(cancellationToken);
        return ordered.Select(x => new PenaltyRuleView(x.TDayMin, x.TDayMax, x.RatePercent, effective)).ToList();
    }

    private static CatalogItemView ToView(ServiceCatalogItem x) =>
        new(x.ItemId.ToString(), x.Name, WpsmCategoryNames.ToDisplay(x.Category), x.Unit, x.DefaultPrice, x.Status == "Active");
}
