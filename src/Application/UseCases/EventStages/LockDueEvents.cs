using Domain.Services;

namespace Application.UseCases.EventStages;

/// <summary>Reads and writes event stages (no rules: those are in EventStageRules).</summary>
public interface IEventStageStore
{
    Task<EventPolicy> GetPolicyAsync(CancellationToken cancellationToken = default);
    /// <summary>Events still adjusting whose event date is on or before <paramref name="eventDateUntil"/>.</summary>
    Task<IReadOnlyList<EventParty>> GetAdjustingEventsAsync(DateOnly eventDateUntil, CancellationToken cancellationToken = default);
    Task StageLockedAsync(int projectId, DateTime now, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case (daily job): every adjusting event that reached its lock date is locked. Customers can no longer change services,
/// the operators book suppliers, the customer is told who to contact for changes.
/// </summary>
public sealed class LockDueEvents(IEventStageStore store, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<int> HandleAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var policy = await store.GetPolicyAsync(cancellationToken);
        // Candidates by date in the database, then the rule decides (lock date = event date - LockDays).
        var candidates = await store.GetAdjustingEventsAsync(today.AddDays(policy.LockDays), cancellationToken);
        var due = candidates.Where(e => EventStageRules.ShouldLock(e.Status, e.EventDate, today, policy)).ToList();
        if (due.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var party in due)
        {
            await store.StageLockedAsync(party.ProjectId, now, cancellationToken);
            await messages.ServicesLockedAsync(party with { Status = EventStatus.Locked }, policy, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}
