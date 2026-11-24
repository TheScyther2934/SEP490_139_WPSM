using Application.Persistence;
using Domain.Entities;
using Domain.Services;

namespace Application.UseCases.CoordinationSheets;

/// <summary>Use case (M-13): the operator writes the notes of the coordination sheet; the first call creates the v1 draft.</summary>
public sealed class SaveCoordinationSheetDraft(ICoordinationSheetStore store, IBeoRepository sheets, IUnitOfWork unitOfWork)
{
    public async Task<ProjectBeoView?> HandleAsync(string projectCode, int managerId, string? notes, CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventWithSheetsAsync(projectCode, managerId, cancellationToken);
        if (project is null) return null;
        var latest = project.Beos.OrderByDescending(b => b.Version).FirstOrDefault();
        if (latest is null) store.Add(Beo.NewDraft(project.ProjectId, string.IsNullOrWhiteSpace(notes) ? Beo.DefaultDraftNotes : notes.Trim()));
        else latest.EditNotes(notes);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await sheets.GetBeoAsync(projectCode, managerId, cancellationToken);
    }
}

/// <summary>Use case (M-14): the 4 department sheets of the latest version are (re)issued; a v1 draft is created when none exists.</summary>
public sealed class GenerateDepartmentSheets(ICoordinationSheetStore store, IBeoRepository sheets, IUnitOfWork unitOfWork)
{
    public async Task<ProjectBeoView?> HandleAsync(string projectCode, int managerId, CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventWithSheetsAsync(projectCode, managerId, cancellationToken);
        if (project is null) return null;
        var latest = project.Beos.OrderByDescending(b => b.Version).FirstOrDefault();
        if (latest is null)
        {
            latest = Beo.NewDraft(project.ProjectId, "Bản thảo BEO tự động tổng hợp.");
            store.Add(latest);
        }
        latest.RefreshDepartmentSheets(project.ProjectCode, DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await sheets.GetBeoAsync(projectCode, managerId, cancellationToken);
    }
}

/// <summary>
/// Use case (JOB-03, daily; also POST /projects/beo/compile-t7): every locked event without a coordination sheet gets its v1 draft,
/// the department sheets and its coordination tasks; the operator is told the draft waits for approval. One commit for all.
/// </summary>
public sealed class CompileLockedDrafts(ICoordinationSheetStore store, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        var today = VietnamTime.GetVietnamToday();
        var events = await store.GetLockedEventsWithoutSheetAsync(today, cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var project in events)
        {
            var draft = Beo.NewDraft(project.ProjectId, $"Tự động tổng hợp bản thảo phiếu điều phối khi khóa dịch vụ ({today:dd/MM/yyyy}).");
            draft.RefreshDepartmentSheets(project.ProjectCode, now);
            store.Add(draft);
            // Coordination tasks open right after the lock: preparation work is done (with proof) before the event day.
            await store.StageCoordinationTasksAsync(project.ProjectId, cancellationToken);
            await messages.CoordinationDraftReadyAsync(project.ManagerId, project.ProjectCode, cancellationToken);
        }
        if (events.Count > 0) await unitOfWork.SaveChangesAsync(cancellationToken);
        return events.Count;
    }
}
