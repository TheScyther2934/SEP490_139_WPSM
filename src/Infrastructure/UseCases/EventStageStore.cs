using Microsoft.EntityFrameworkCore;
using Application.UseCases;
using Application.UseCases.EventStages;
using Domain.Services;
using Infrastructure.Persistence;

namespace Infrastructure.UseCases;

/// <summary>Event stage data for the lock job: policy, candidate events, stages the lock.</summary>
public sealed class EventStageStore(WpsmDbContext db) : IEventStageStore
{
    public Task<EventPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) => EventFlowSupport.LoadPolicyAsync(db, cancellationToken);

    public async Task<IReadOnlyList<EventParty>> GetAdjustingEventsAsync(DateOnly eventDateUntil, CancellationToken cancellationToken = default) =>
        await db.WeddingProjects.AsNoTracking().Where(p => p.Status == EventStatus.Adjusting && p.EventDate <= eventDateUntil)
            .Select(p => new EventParty(p.ProjectId, p.ProjectCode, p.Status, p.EventDate, p.CustomerId, p.Customer.FullName, p.Customer.Email))
            .ToListAsync(cancellationToken);

    public async Task StageLockedAsync(int projectId, DateTime now, CancellationToken cancellationToken = default)
    {
        // Loaded (tracked) before changing it, so the audit log records the real previous values.
        var project = await db.WeddingProjects.FindAsync([projectId], cancellationToken) ?? throw new KeyNotFoundException("Không tìm thấy sự kiện.");
        project.Status = EventStatus.Locked;
        project.LockedAt = now;
    }
}
