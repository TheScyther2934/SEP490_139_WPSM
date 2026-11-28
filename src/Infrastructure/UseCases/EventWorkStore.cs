using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Application.UseCases.EventStages;
using Domain.Entities;
using Domain.Services;
using Infrastructure.Persistence;

namespace Infrastructure.UseCases;

/// <summary>Event data for the stage use cases: notes, cancelling, adjusting services, supplier evidence and its daily reminder.</summary>
public sealed class EventWorkStore(WpsmDbContext db, IFileService files) : IEventWorkStore
{
    public Task<EventPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) => EventFlowSupport.LoadPolicyAsync(db, cancellationToken);

    public Task<WeddingProject?> GetEventAsync(string projectCode, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);

    public Task<WeddingProject?> GetEventToCancelAsync(string projectCode, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.Include(x => x.AddonContracts).Include(x => x.ResourceAllocations).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);

    public Task<WeddingProject?> GetOperatedEventWithServicesAsync(string projectCode, int managerId, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.Include(x => x.SpecItems).Include(x => x.Terms)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode
                && (x.ManagerId == managerId || x.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)), cancellationToken);

    public Task<WeddingProject?> GetEventWithServicesAsync(string projectCode, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.Include(x => x.SpecItems).FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);

    public async Task<IReadOnlyDictionary<int, (string Name, bool Active)>> GetServicesAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        (await db.ServiceCatalogItems.AsNoTracking().Where(x => ids.Contains(x.ItemId)).Select(x => new { x.ItemId, x.Name, x.Status }).ToListAsync(cancellationToken))
            .ToDictionary(x => x.ItemId, x => (x.Name, x.Status == ServiceCatalogItem.Active));

    public async Task<IReadOnlyDictionary<int, (decimal Min, decimal Max)>> GetFramesInForceAsync(IReadOnlyCollection<int> ids, DateOnly todayVn,
        CancellationToken cancellationToken = default) =>
        (await db.PriceFrames.AsNoTracking().Where(x => ids.Contains(x.ItemId) && x.EffectiveFrom <= todayVn).ToListAsync(cancellationToken))
            .GroupBy(x => x.ItemId).Select(g => g.OrderByDescending(x => x.EffectiveFrom).First())
            .ToDictionary(x => x.ItemId, x => (x.MinPrice, x.MaxPrice));

    public void Remove(SpecItem line) => db.SpecItems.Remove(line);

    public void Add(StageNote note) => db.StageNotes.Add(note);

    public void Add(SupplierEvidence evidence) => db.SupplierEvidences.Add(evidence);

    public Task<SupplierEvidence?> GetSupplierEvidenceAsync(int projectId, int evidenceId, CancellationToken cancellationToken = default) =>
        db.SupplierEvidences.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.EvidenceId == evidenceId, cancellationToken);

    public void Remove(SupplierEvidence evidence) => db.SupplierEvidences.Remove(evidence);

    public async Task<Guid> StoreSupplierFileAsync(int projectId, string fileName, byte[] content, int uploaderId, CancellationToken cancellationToken = default) =>
        (await files.StoreAsync(projectId, "SupplierEvidence", fileName, content, uploaderId, cancellationToken)).Id;

    public async Task<IReadOnlyList<WeddingProject>> GetLockedUpcomingEventsAsync(DateOnly todayVn, CancellationToken cancellationToken = default) =>
        await db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(p => p.Terms)
            .Include(p => p.SpecItems).ThenInclude(s => s.CatalogItem)
            .Include(p => p.SupplierEvidences)
            .Where(p => p.Status == EventStatus.Locked && p.EventDate >= todayVn)
            .ToListAsync(cancellationToken);

    public Task<IReadOnlyList<int>> GetOperatorIdsAsync(int projectId, CancellationToken cancellationToken = default) =>
        EventFlowSupport.OperatorIdsAsync(db, projectId, cancellationToken);

    public Task<bool> SupplierReminderSentTodayAsync(int operatorId, string projectCode, CancellationToken cancellationToken = default)
    {
        var marker = EventMessages.SupplierReminderMarker(projectCode);
        var startOfDay = DateTime.UtcNow.Date;
        return db.Notifications.AnyAsync(n => n.RecipientId == operatorId && n.Message.StartsWith(marker) && n.CreatedAt >= startOfDay, cancellationToken);
    }
}
