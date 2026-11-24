using Application.Persistence;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;

namespace Application.UseCases.EventStages;

/// <summary>Data access for the stage use cases besides locking; implemented in Infrastructure. Entities come back tracked.</summary>
public interface IEventWorkStore
{
    Task<EventPolicy> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<WeddingProject?> GetEventAsync(string projectCode, CancellationToken cancellationToken = default);
    /// <summary>The event with its pending addon contracts and its resource allocations, to cancel it.</summary>
    Task<WeddingProject?> GetEventToCancelAsync(string projectCode, CancellationToken cancellationToken = default);
    /// <summary>The event with its service lines, when the manager operates it.</summary>
    Task<WeddingProject?> GetOperatedEventWithServicesAsync(string projectCode, int managerId, CancellationToken cancellationToken = default);
    /// <summary>The event with its service lines (the endpoint already checked who may open it).</summary>
    Task<WeddingProject?> GetEventWithServicesAsync(string projectCode, CancellationToken cancellationToken = default);
    /// <summary>Catalog services by id: name and whether still offered.</summary>
    Task<IReadOnlyDictionary<int, (string Name, bool Active)>> GetServicesAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);
    /// <summary>The price frame in force today for each service that has one.</summary>
    Task<IReadOnlyDictionary<int, (decimal Min, decimal Max)>> GetFramesInForceAsync(IReadOnlyCollection<int> ids, DateOnly todayVn, CancellationToken cancellationToken = default);
    void Remove(SpecItem line);
    void Add(StageNote note);
    void Add(SupplierEvidence evidence);
    /// <summary>Checks the file by its content and stores it. Saves its own row: call last, right before the use case saves.</summary>
    Task<Guid> StoreSupplierFileAsync(int projectId, string fileName, byte[] content, int uploaderId, CancellationToken cancellationToken = default);
    /// <summary>Locked events whose supplier deadline has come and whose day has not passed, with services and evidence (JOB-01).</summary>
    Task<IReadOnlyList<WeddingProject>> GetEventsPastSupplierDeadlineAsync(DateOnly todayVn, EventPolicy policy, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetOperatorIdsAsync(int projectId, CancellationToken cancellationToken = default);
    Task<bool> SupplierReminderSentTodayAsync(int operatorId, string projectCode, CancellationToken cancellationToken = default);
}

/// <summary>Use case (M-15): the operator writes a stage note (tasting, fitting, supplier, general).</summary>
public sealed class AddStageNote(IEventWorkStore store, IEventFlowRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<StageNoteView> HandleAsync(string projectCode, int userId, StageNoteCommand command, CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventAsync(projectCode, cancellationToken) ?? throw new KeyNotFoundException("Không tìm thấy sự kiện.");
        var (type, content) = EventStagePolicy.ValidateNote(project.Status, command.NoteType, command.Content);
        var stage = EventStageRules.CurrentStage(project.Status, project.EventDate, VietnamTime.GetVietnamToday());
        var note = StageNote.Write(project.ProjectId, stage, type, content, userId, DateTime.UtcNow);
        store.Add(note);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await reads.GetWorkspaceAsync(projectCode, cancellationToken))!.Notes.Single(n => n.Id == note.StageNoteId);
    }
}

/// <summary>Use case: the customer cancels before the event (done by the operator). The deposit is kept; hall, slot and resources are freed.</summary>
public sealed class CancelEvent(IEventWorkStore store, IEventFlowRepository reads, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<EventWorkspaceView?> HandleAsync(string projectCode, int userId, string? reason, CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventToCancelAsync(projectCode, cancellationToken);
        if (project is null) return null;
        var today = VietnamTime.GetVietnamToday();
        var text = EventStagePolicy.ValidateCancel(project.Status, project.EventDate, today, reason);
        var now = DateTime.UtcNow;
        project.Cancel(userId, text, now);
        store.Add(StageNote.Write(project.ProjectId, EventStageRules.CurrentStage(EventStatus.Adjusting, project.EventDate, today), "General",
            $"Khách hủy sự kiện, không hoàn cọc. Lý do: {text}", userId, now));
        await messages.EventCancelledAsync(project.CustomerId, project.ProjectCode, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await reads.GetWorkspaceAsync(projectCode, cancellationToken);
    }
}

/// <summary>
/// Use case (stage 2, M-06): the customer asks for changes and the operator edits the service lines before the lock date, writing why.
/// Prices stay in their frames and the total stays above the floor of the signed value.
/// </summary>
public sealed class AdjustServices(IEventWorkStore store, IProjectRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<WeddingProjectView?> HandleAsync(string projectCode, int managerId, SpecificationCommand command, CancellationToken cancellationToken = default)
    {
        var project = await store.GetOperatedEventWithServicesAsync(projectCode, managerId, cancellationToken);
        if (project is null) return null;
        var policy = await store.GetPolicyAsync(cancellationToken);
        var today = VietnamTime.GetVietnamToday();
        EventStagePolicy.EnsureCanAdjust(project.Status, project.EventDate, today, policy);
        var lines = command.Items?.Select(i => new AdjustedLine(i.CatalogItemId, i.Description, i.Quantity, i.UnitPrice)).ToList();
        var (note, noteType) = EventStagePolicy.ValidateAdjustment(lines, command.Note, command.NoteType);

        var ids = lines!.Select(x => x.CatalogItemId).Distinct().ToArray();
        var services = await store.GetServicesAsync(ids, cancellationToken);
        EventStagePolicy.EnsureServicesAvailable(ids, project.SpecItems.Select(s => s.CatalogItemId).ToHashSet(), services.ToDictionary(x => x.Key, x => x.Value.Active));
        var frames = await store.GetFramesInForceAsync(ids, today, cancellationToken);
        foreach (var line in lines!)
            EventStagePolicy.EnsureWithinFrame(services[line.CatalogItemId].Name, line.UnitPrice,
                frames.TryGetValue(line.CatalogItemId, out var f) ? f.Min : null, frames.TryGetValue(line.CatalogItemId, out var g) ? g.Max : null);
        var before = project.CurrentTotal;
        EventStagePolicy.EnsureAboveFloor(project.BaseContractValue, lines.Sum(i => i.Quantity * i.UnitPrice), policy);

        foreach (var dropped in project.AdjustServices(lines)) store.Remove(dropped);
        store.Add(StageNote.Write(project.ProjectId, EventStageRules.Adjusting, noteType, note, managerId, DateTime.UtcNow, before, project.CurrentTotal));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await reads.GetProjectAsync(projectCode, cancellationToken);
    }
}

/// <summary>Use case (stage 3, M-08): the operator attaches evidence that a supplier was booked (transfer, invoice, contract).</summary>
public sealed class AddSupplierEvidence(IEventWorkStore store, ISupplierEvidenceRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<SupplierEvidenceView> HandleAsync(string projectCode, int userId, SupplierEvidenceCommand command, CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventWithServicesAsync(projectCode, cancellationToken) ?? throw new KeyNotFoundException("Không tìm thấy sự kiện.");
        var (supplier, note) = EventStagePolicy.ValidateEvidence(project.Status, command.SupplierName, command.EvidenceType,
            command.SpecItemId is not int specId || project.SpecItems.Any(s => s.SpecItemId == specId), command.Note, command.Content is { Length: > 0 });
        var fileId = await store.StoreSupplierFileAsync(project.ProjectId, command.FileName ?? "minh-chung", command.Content, userId, cancellationToken);
        var evidence = SupplierEvidence.Attach(project.ProjectId, command.SpecItemId, supplier, command.EvidenceType, fileId, note, userId, DateTime.UtcNow);
        store.Add(evidence);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await reads.GetBoardAsync(projectCode, cancellationToken))!.Evidences.Single(e => e.Id == evidence.EvidenceId);
    }
}

/// <summary>Use case (JOB-01, daily): operators of locked events past the supplier deadline without evidence are reminded, once a day each.</summary>
public sealed class RemindSupplierDeadlines(IEventWorkStore store, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<int> HandleAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var policy = await store.GetPolicyAsync(cancellationToken);
        var reminded = 0;
        foreach (var project in await store.GetEventsPastSupplierDeadlineAsync(today, policy, cancellationToken))
        {
            var missing = project.ServicesMissingSupplierEvidence();
            if (missing.Count == 0) continue;
            foreach (var operatorId in await store.GetOperatorIdsAsync(project.ProjectId, cancellationToken))
            {
                if (await store.SupplierReminderSentTodayAsync(operatorId, project.ProjectCode, cancellationToken)) continue;
                await messages.SupplierEvidenceMissingAsync(operatorId, project.ProjectCode, missing, cancellationToken);
                reminded++;
            }
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return reminded;
    }
}
