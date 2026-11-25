using Application.Persistence;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;

namespace Application.UseCases.Configuration;

/// <summary>Data access for the Admin configuration use cases; implemented in Infrastructure. Entities come back tracked.</summary>
public interface IConfigurationStore
{
    Task<bool> CatalogNameTakenAsync(string category, string name, int? exceptItemId, CancellationToken cancellationToken = default);
    Task<ServiceCatalogItem?> GetCatalogItemAsync(int itemId, CancellationToken cancellationToken = default);
    void Add(ServiceCatalogItem item);
    /// <summary>The price frame with its service.</summary>
    Task<PriceFrame?> GetPriceFrameAsync(int priceFrameId, CancellationToken cancellationToken = default);
    Task<PriceFrame?> GetPriceFrameAsync(int itemId, DateOnly effectiveFrom, CancellationToken cancellationToken = default);
    void Add(PriceFrame frame);
    /// <summary>The policy in force today (latest value of each key; defaults for missing keys).</summary>
    Task<EventPolicy> GetPolicyInForceAsync(CancellationToken cancellationToken = default);
    Task<PolicyConfig?> GetPolicyRowAsync(string policyType, DateOnly effectiveFrom, CancellationToken cancellationToken = default);
    void Add(PolicyConfig row);
    Task<SystemSetting?> GetSettingAsync(string key, CancellationToken cancellationToken = default);
    void Add(SystemSetting setting);
}

internal static class CatalogViews
{
    public static CatalogItemView View(ServiceCatalogItem x) =>
        new(x.ItemId.ToString(), x.Name, ServiceCategories.Display(x.Category), x.Unit, x.DefaultPrice, x.Status == ServiceCatalogItem.Active, x.RequiresSupplier);
}

/// <summary>Use case (UC-37, A-04): the Admin adds a service to the catalog.</summary>
public sealed class AddCatalogItem(IConfigurationStore store, IUnitOfWork unitOfWork)
{
    public async Task<CatalogItemView> HandleAsync(CatalogItemCommand command, CancellationToken cancellationToken = default)
    {
        var category = ServiceCategories.Normalize(command.Category);
        var (name, unit) = ConfigurationPolicy.ValidateCatalogItem(command.Name, command.Unit, command.BasePrice);
        ConfigurationPolicy.EnsureNameFree(await store.CatalogNameTakenAsync(category, name, null, cancellationToken));
        var item = ServiceCatalogItem.Create(name, category, unit, command.BasePrice, command.Active, command.RequiresSupplier ?? false);
        store.Add(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CatalogViews.View(item);
    }
}

/// <summary>Use case (UC-37): the Admin edits a catalog service; signed service lines are not touched.</summary>
public sealed class UpdateCatalogItem(IConfigurationStore store, IUnitOfWork unitOfWork)
{
    public async Task<CatalogItemView?> HandleAsync(int itemId, CatalogItemCommand command, CancellationToken cancellationToken = default)
    {
        var item = await store.GetCatalogItemAsync(itemId, cancellationToken);
        if (item is null) return null;
        var category = ServiceCategories.Normalize(command.Category);
        var (name, unit) = ConfigurationPolicy.ValidateCatalogItem(command.Name, command.Unit, command.BasePrice);
        ConfigurationPolicy.EnsureNameFree(await store.CatalogNameTakenAsync(category, name, itemId, cancellationToken));
        item.Edit(name, category, unit, command.BasePrice, command.Active, command.RequiresSupplier);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CatalogViews.View(item);
    }
}

/// <summary>Use case (UC-37): the Admin turns a catalog service on or off (off: no longer offered for new events).</summary>
public sealed class SetCatalogItemStatus(IConfigurationStore store, IUnitOfWork unitOfWork)
{
    public async Task<CatalogItemView?> HandleAsync(int itemId, bool active, CancellationToken cancellationToken = default)
    {
        var item = await store.GetCatalogItemAsync(itemId, cancellationToken);
        if (item is null) return null;
        item.SetActive(active);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CatalogViews.View(item);
    }
}

/// <summary>
/// Use case (UC-38, A-05): the Admin sets the price frame of a service from a date. A new date adds a version; the same date changes
/// that version. Earlier versions stay for the contracts signed under them.
/// </summary>
public sealed class UpdatePriceFrame(IConfigurationStore store, IUnitOfWork unitOfWork)
{
    public async Task<PriceFrameView?> HandleAsync(int priceFrameId, PriceFrameCommand command, int adminId, CancellationToken cancellationToken = default)
    {
        var effective = ConfigurationPolicy.ValidatePriceFrame(command.MinPrice, command.MaxPrice, command.EffectiveFrom, VietnamTime.GetVietnamToday());
        var current = await store.GetPriceFrameAsync(priceFrameId, cancellationToken);
        if (current is null) return null;
        var now = DateTime.UtcNow;
        var target = await store.GetPriceFrameAsync(current.ItemId, effective, cancellationToken);
        if (target is null)
        {
            target = PriceFrame.Create(current.ItemId, effective, command.MinPrice, command.MaxPrice, adminId, now);
            store.Add(target);
        }
        else target.Change(command.MinPrice, command.MaxPrice, adminId, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PriceFrameView(target.PriceFrameId.ToString(), current.Item.Name, ServiceCategories.Display(current.Item.Category),
            target.MinPrice, target.MaxPrice, "VND", current.ItemId);
    }
}

/// <summary>
/// Use case (UC-39, A-06): the Admin saves a new version of the event policy from a date, and the hotline. Values unchanged today add
/// no version. Events keep the stages they were planned with until the new version takes effect.
/// </summary>
public sealed class UpdateEventPolicies(IConfigurationStore store, IConfigurationRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<PolicyView> HandleAsync(PolicyCommand command, int adminId, CancellationToken cancellationToken = default)
    {
        var today = VietnamTime.GetVietnamToday();
        var wanted = new EventPolicy(command.DepositRatio, command.MaxDecreaseRatio, command.BookingMinDays, command.BookingMaxDays,
            command.LockDays, command.SupplierDeadlineDays, command.SettlementDueDays);
        var effective = ConfigurationPolicy.ValidateEventPolicy(wanted, command.EffectiveFrom, today);
        var hotline = ConfigurationPolicy.ValidateHotline(command.Hotline);

        var current = ConfigurationPolicy.Values(await store.GetPolicyInForceAsync(cancellationToken));
        var now = DateTime.UtcNow;
        foreach (var (type, value) in ConfigurationPolicy.Values(wanted))
        {
            if (current[type] == value && effective == today) continue;   // unchanged: no new version
            var row = await store.GetPolicyRowAsync(type, effective, cancellationToken);
            if (row is null) store.Add(PolicyConfig.Create(type, value, effective, adminId, now));
            else row.Change(value, adminId, now);
        }

        if (hotline is not null)
        {
            var setting = await store.GetSettingAsync(SystemSetting.HotlineKey, cancellationToken);
            if (setting is null) store.Add(SystemSetting.Create(SystemSetting.HotlineKey, hotline, adminId, now));
            else setting.Change(hotline, adminId, now);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await reads.GetPoliciesAsync(cancellationToken);
    }
}
