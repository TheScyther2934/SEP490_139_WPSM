using Microsoft.EntityFrameworkCore;
using Application.UseCases;
using Application.UseCases.EventStages;
using Domain.Entities;
using Domain.Services;
using Infrastructure.Persistence;

namespace Infrastructure.UseCases;

/// <summary>Event stage data for the lock job: adjusting events with their signed terms, stages the lock.</summary>
public sealed class EventStageStore(WpsmDbContext db) : IEventStageStore
{
    public async Task<IReadOnlyList<(EventParty Party, EventPolicy Terms)>> GetAdjustingEventsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.WeddingProjects.AsNoTracking().Include(p => p.Customer).Include(p => p.Terms)
            .Where(p => p.Status == EventStatus.Adjusting)
            .ToListAsync(cancellationToken);
        var inForce = rows.All(p => p.Terms is not null) ? EventPolicy.Default : await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        return rows.Select(p => (new EventParty(p.ProjectId, p.ProjectCode, p.Status, p.EventDate, p.CustomerId, p.Customer.FullName, p.Customer.Email),
            EventTerms.PolicyOf(p.Terms, inForce))).ToList();
    }

    public async Task StageLockedAsync(int projectId, DateTime now, CancellationToken cancellationToken = default)
    {
        // Loaded (tracked) before changing it, so the audit log records the real previous values.
        var project = await db.WeddingProjects.FindAsync([projectId], cancellationToken) ?? throw new KeyNotFoundException("Không tìm thấy sự kiện.");
        project.Status = EventStatus.Locked;
        project.LockedAt = now;
    }
}
