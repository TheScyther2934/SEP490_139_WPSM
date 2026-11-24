using Microsoft.EntityFrameworkCore;
using Application.UseCases.Configuration;
using Domain.Entities;
using Domain.Services;
using Infrastructure.Persistence;

namespace Infrastructure.UseCases;

/// <summary>Configuration data for the Admin use cases: catalog, price frames, policy versions and settings.</summary>
public sealed class ConfigurationStore(WpsmDbContext db) : IConfigurationStore
{
    public Task<bool> CatalogNameTakenAsync(string category, string name, int? exceptItemId, CancellationToken cancellationToken = default) =>
        db.ServiceCatalogItems.AnyAsync(x => x.ItemId != exceptItemId && x.Category == category && x.Name == name, cancellationToken);

    public Task<ServiceCatalogItem?> GetCatalogItemAsync(int itemId, CancellationToken cancellationToken = default) =>
        db.ServiceCatalogItems.FirstOrDefaultAsync(x => x.ItemId == itemId, cancellationToken);

    public void Add(ServiceCatalogItem item) => db.ServiceCatalogItems.Add(item);

    public Task<PriceFrame?> GetPriceFrameAsync(int priceFrameId, CancellationToken cancellationToken = default) =>
        db.PriceFrames.Include(x => x.Item).FirstOrDefaultAsync(x => x.PriceFrameId == priceFrameId, cancellationToken);

    public Task<PriceFrame?> GetPriceFrameAsync(int itemId, DateOnly effectiveFrom, CancellationToken cancellationToken = default) =>
        db.PriceFrames.FirstOrDefaultAsync(x => x.ItemId == itemId && x.EffectiveFrom == effectiveFrom, cancellationToken);

    public void Add(PriceFrame frame) => db.PriceFrames.Add(frame);

    public Task<EventPolicy> GetPolicyInForceAsync(CancellationToken cancellationToken = default) => EventFlowSupport.LoadPolicyAsync(db, cancellationToken);

    public Task<PolicyConfig?> GetPolicyRowAsync(string policyType, DateOnly effectiveFrom, CancellationToken cancellationToken = default) =>
        db.PolicyConfigs.FirstOrDefaultAsync(x => x.PolicyType == policyType && x.EffectiveFrom == effectiveFrom, cancellationToken);

    public void Add(PolicyConfig row) => db.PolicyConfigs.Add(row);

    public Task<SystemSetting?> GetSettingAsync(string key, CancellationToken cancellationToken = default) =>
        db.SystemSettings.FirstOrDefaultAsync(x => x.SettingKey == key, cancellationToken);

    public void Add(SystemSetting setting) => db.SystemSettings.Add(setting);
}
