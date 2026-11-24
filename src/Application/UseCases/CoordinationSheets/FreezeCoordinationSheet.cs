using Application.Persistence;
using Domain.Entities;
using Domain.Rules;

namespace Application.UseCases.CoordinationSheets;

/// <summary>The event and its latest coordination sheet, as needed to freeze it.</summary>
public sealed record CoordinationSheetState(EventParty Event, int? LatestVersion, string? LatestStatus);

/// <summary>Reads and writes coordination sheets (no rules: those are in CoordinationSheetPolicy).</summary>
public interface ICoordinationSheetStore
{
    /// <summary>Null when the event does not exist or the manager does not operate it.</summary>
    Task<CoordinationSheetState?> GetForFreezeAsync(string projectCode, int managerId, CancellationToken cancellationToken = default);
    /// <summary>Stages the frozen version, its 4 department sheets and the coordination tasks of its lines.</summary>
    Task StageFrozenAsync(CoordinationSheetState state, CoordinationSheetDecision decision, int managerId, string notes, DateTime now, CancellationToken cancellationToken = default);
    /// <summary>The event with its sheet versions and their department sheets (tracked), when the manager operates it; null otherwise.</summary>
    Task<WeddingProject?> GetEventWithSheetsAsync(string projectCode, int managerId, CancellationToken cancellationToken = default);
    /// <summary>Locked events from today on that have no coordination sheet yet (JOB-03).</summary>
    Task<IReadOnlyList<WeddingProject>> GetLockedEventsWithoutSheetAsync(DateOnly todayVn, CancellationToken cancellationToken = default);
    void Add(Beo sheet);
    /// <summary>Stages the coordination tasks still missing for the locked services and confirmed addon contracts.</summary>
    Task StageCoordinationTasksAsync(int projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case: the Manager freezes the coordination sheet (first time) or re-approves it after a confirmed addon contract.
/// The 4 departments get the new version, coordination tasks are added for the new lines, the customer is told.
/// </summary>
public sealed class FreezeCoordinationSheet(ICoordinationSheetStore store, IBeoRepository sheets, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<ProjectBeoView?> HandleAsync(string projectCode, int managerId, string? notes, CancellationToken cancellationToken = default)
    {
        var state = await store.GetForFreezeAsync(projectCode, managerId, cancellationToken);
        if (state is null) return null;

        var decision = CoordinationSheetPolicy.Freeze(state.Event.Status, state.LatestVersion, state.LatestStatus);
        var text = string.IsNullOrWhiteSpace(notes) ? decision.DefaultNotes : notes.Trim();

        await store.StageFrozenAsync(state, decision, managerId, text, DateTime.UtcNow, cancellationToken);
        await messages.CoordinationSheetApprovedAsync(state.Event, decision.Version, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await sheets.GetBeoAsync(projectCode, managerId, cancellationToken);
    }
}
