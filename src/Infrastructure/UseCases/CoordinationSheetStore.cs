using Microsoft.EntityFrameworkCore;
using Application.UseCases;
using Application.UseCases.CoordinationSheets;
using Domain.Rules;
using Domain.Services;
using Infrastructure.Persistence;
using Domain.Entities;
using Infrastructure.Persistence.Repositories;

namespace Infrastructure.UseCases;

/// <summary>Coordination sheet data for the use cases: the event and its sheet versions, staging new versions and tasks.</summary>
public sealed class CoordinationSheetStore(WpsmDbContext db) : ICoordinationSheetStore
{
    public async Task<CoordinationSheetState?> GetForFreezeAsync(string projectCode, int managerId, CancellationToken cancellationToken = default)
    {
        var row = await db.WeddingProjects.AsNoTracking()
            .Where(p => p.ProjectCode == projectCode.Trim()
                && (p.ManagerId == managerId || p.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)))
            .Select(p => new
            {
                Party = new EventParty(p.ProjectId, p.ProjectCode, p.Status, p.EventDate, p.CustomerId, p.Customer.FullName, p.Customer.Email),
                Latest = p.Beos.OrderByDescending(b => b.Version).Select(b => new { b.Version, b.Status }).FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new CoordinationSheetState(row.Party, row.Latest?.Version, row.Latest?.Status);
    }

    public async Task StageFrozenAsync(CoordinationSheetState state, CoordinationSheetDecision decision, int managerId, string notes, DateTime now,
        CancellationToken cancellationToken = default)
    {
        var sheet = decision.NewVersion
            ? Beo.NewVersion(state.Event.ProjectId, decision.Version)
            : await db.Beos.Include(b => b.BeodepartmentSheets)
                .SingleAsync(b => b.ProjectId == state.Event.ProjectId && b.Version == decision.Version, cancellationToken);
        if (decision.NewVersion) db.Beos.Add(sheet);
        sheet.Freeze(managerId, notes, now);
        sheet.RefreshDepartmentSheets(state.Event.ProjectCode, now);

        // Coordination tasks of the new lines (confirmed addon contracts); tasks already done stay as they are.
        await CoordinationPlan.EnsureTasksAsync(db, state.Event.ProjectId, cancellationToken);
    }

    public Task<WeddingProject?> GetEventWithSheetsAsync(string projectCode, int managerId, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.Include(p => p.Beos).ThenInclude(b => b.BeodepartmentSheets)
            .FirstOrDefaultAsync(p => p.ProjectCode == projectCode.Trim()
                && (p.ManagerId == managerId || p.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)), cancellationToken);

    public async Task<IReadOnlyList<WeddingProject>> GetLockedEventsWithoutSheetAsync(DateOnly todayVn, CancellationToken cancellationToken = default) =>
        await db.WeddingProjects.Where(p => p.Status == EventStatus.Locked && p.EventDate >= todayVn && !p.Beos.Any()).ToListAsync(cancellationToken);

    public void Add(Beo sheet) => db.Beos.Add(sheet);

    public Task StageCoordinationTasksAsync(int projectId, CancellationToken cancellationToken = default) =>
        CoordinationPlan.EnsureTasksAsync(db, projectId, cancellationToken);
}
