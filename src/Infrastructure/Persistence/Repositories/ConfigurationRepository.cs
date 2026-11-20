using Microsoft.EntityFrameworkCore;
using Domain.Services;
using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

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
        if (await db.ServiceCatalogItems.AnyAsync(x => x.Category == category && x.Name == command.Name.Trim(), cancellationToken))
            throw new InvalidOperationException("Tên dịch vụ đã tồn tại trong nhóm này.");
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
        var p = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        return new PolicyView(p.DepositRatio, p.MaxDecreaseRatio, p.BookingMinDays, p.BookingMaxDays, p.LockDays, p.SupplierDeadlineDays,
            p.SettlementDueDays, await EventFlowSupport.LoadHotlineAsync(db, cancellationToken));
    }

    /// <summary>Saves a new version of the event rules (effective from a date, never in the past) and the hotline.</summary>
    public async Task<PolicyView> UpdatePoliciesAsync(PolicyCommand command, int updatedBy, CancellationToken cancellationToken = default)
    {
        var today = VietnamTime.GetVietnamToday();
        var effective = command.EffectiveFrom ?? today;
        if (effective < today) throw new InvalidOperationException("Ngày hiệu lực không được ở trong quá khứ.");
        if (command.DepositRatio is <= 0 or > 100 || command.MaxDecreaseRatio is < 0 or >= 100)
            throw new InvalidOperationException("Tỷ lệ đặt cọc phải trong khoảng 1–100%, mức giảm tối đa trong khoảng 0–99%.");
        if (100 - command.MaxDecreaseRatio < command.DepositRatio)
            throw new InvalidOperationException("Mức giảm tối đa quá lớn: giá trị sau khi giảm phải luôn lớn hơn tiền đặt cọc.");
        if (command.BookingMinDays < 1 || command.BookingMaxDays < command.BookingMinDays || command.BookingMaxDays > 365)
            throw new InvalidOperationException("Khoảng ngày ký hợp đồng không hợp lệ (tối thiểu ≥ 1, tối đa ≥ tối thiểu và ≤ 365).");
        if (command.SupplierDeadlineDays < 1 || command.LockDays < command.SupplierDeadlineDays || command.LockDays >= command.BookingMinDays)
            throw new InvalidOperationException("Mốc khóa phải ≥ hạn đặt nhà cung cấp và nhỏ hơn số ngày ký tối thiểu.");
        if (command.SettlementDueDays is < 0 or > 90)
            throw new InvalidOperationException("Hạn tất toán phải từ 0 đến 90 ngày sau ngày cưới.");

        var values = new Dictionary<string, decimal>
        {
            [EventPolicy.Keys.DepositRatio] = command.DepositRatio,
            [EventPolicy.Keys.MaxDecreaseRatio] = command.MaxDecreaseRatio,
            [EventPolicy.Keys.BookingMinDays] = command.BookingMinDays,
            [EventPolicy.Keys.BookingMaxDays] = command.BookingMaxDays,
            [EventPolicy.Keys.LockDays] = command.LockDays,
            [EventPolicy.Keys.SupplierDeadlineDays] = command.SupplierDeadlineDays,
            [EventPolicy.Keys.SettlementDueDays] = command.SettlementDueDays
        };
        var current = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var currentValues = new Dictionary<string, decimal>
        {
            [EventPolicy.Keys.DepositRatio] = current.DepositRatio, [EventPolicy.Keys.MaxDecreaseRatio] = current.MaxDecreaseRatio,
            [EventPolicy.Keys.BookingMinDays] = current.BookingMinDays, [EventPolicy.Keys.BookingMaxDays] = current.BookingMaxDays,
            [EventPolicy.Keys.LockDays] = current.LockDays, [EventPolicy.Keys.SupplierDeadlineDays] = current.SupplierDeadlineDays,
            [EventPolicy.Keys.SettlementDueDays] = current.SettlementDueDays
        };
        foreach (var (type, value) in values)
        {
            if (currentValues[type] == value && effective == today) continue;   // unchanged: no new version
            var row = await db.PolicyConfigs.FirstOrDefaultAsync(x => x.PolicyType == type && x.EffectiveFrom == effective, cancellationToken);
            if (row is null)
                db.PolicyConfigs.Add(new PolicyConfig { PolicyType = type, Value = value, EffectiveFrom = effective, UpdatedBy = updatedBy, UpdatedAt = DateTime.UtcNow });
            else
            {
                row.Value = value;
                row.UpdatedBy = updatedBy;
                row.UpdatedAt = DateTime.UtcNow;
            }
        }

        var hotline = command.Hotline?.Trim();
        if (!string.IsNullOrEmpty(hotline))
        {
            if (hotline.Length > 20 || hotline.Any(c => !char.IsDigit(c) && c is not (' ' or '.' or '+')))
                throw new InvalidOperationException("Số hotline chỉ gồm chữ số (tối đa 20 ký tự).");
            var setting = await db.SystemSettings.FirstOrDefaultAsync(x => x.SettingKey == EventFlowSupport.HotlineKey, cancellationToken);
            if (setting is null) db.SystemSettings.Add(new SystemSetting { SettingKey = EventFlowSupport.HotlineKey, SettingValue = hotline, UpdatedBy = updatedBy, UpdatedAt = DateTime.UtcNow });
            else if (setting.SettingValue != hotline) { setting.SettingValue = hotline; setting.UpdatedBy = updatedBy; setting.UpdatedAt = DateTime.UtcNow; }
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetPoliciesAsync(cancellationToken);
    }
}
