using Domain.Services;

namespace Application.UseCases.EventStages;

/// <summary>Reads and writes event stages (no rules: those are in EventStageRules).</summary>
public interface IEventStageStore
{
    /// <summary>Events still adjusting whose day has not passed, each with the terms it was signed with.</summary>
    Task<IReadOnlyList<(EventParty Party, EventPolicy Terms)>> GetAdjustingEventsAsync(CancellationToken cancellationToken = default);
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
        // Lock date = event date - LockDays of the terms each event was signed with (a later policy does not move it).
        var candidates = await store.GetAdjustingEventsAsync(cancellationToken);
        var due = candidates.Where(e => EventStageRules.ShouldLock(e.Party.Status, e.Party.EventDate, today, e.Terms)).ToList();
        if (due.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var (party, terms) in due)
        {
            await store.StageLockedAsync(party.ProjectId, now, cancellationToken);
            await messages.ServicesLockedAsync(party with { Status = EventStatus.Locked }, terms, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return due.Count;
    }
}
