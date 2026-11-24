using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads addon contracts (extra services after the lock). Creating, reminding, cancelling and confirming are use cases
/// (Application/UseCases/AddonContracts) with their rules in Domain (AddonContractPolicy).
/// </summary>
public sealed class AddonContractRepository(WpsmDbContext db) : IAddonContractRepository
{
    public async Task<IReadOnlyList<AddonContractView>> ListAsync(string eventCode, CancellationToken cancellationToken = default)
    {
        var addons = await Query().Where(a => a.Project.ProjectCode == eventCode).OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
        return addons.Select(a => EventFlowSupport.MapAddon(a, eventCode)).ToList();
    }

    public async Task<AddonContractView?> GetAsync(string eventCode, string addonCode, CancellationToken cancellationToken = default) =>
        await Query().FirstOrDefaultAsync(a => a.AddonCode == addonCode && a.Project.ProjectCode == eventCode, cancellationToken) is { } addon
            ? EventFlowSupport.MapAddon(addon, eventCode) : null;

    private IQueryable<AddonContract> Query() => db.AddonContracts.AsNoTracking().Include(a => a.Items).Include(a => a.CreatedByNavigation);
}
