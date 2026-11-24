using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Entities;
using Domain.Rules;

namespace Infrastructure.Persistence.Repositories;

/// <summary>Operators of an event (read). Adding and removing co-operators are use cases: Application/UseCases/CoOperators.</summary>
public sealed class ProjectManagerRepository(WpsmDbContext db) : IProjectManagerRepository
{
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // Primary = the creator (WeddingProjects.ManagerId). Only the Primary or an Admin may change the operator list.
    private static bool CanManage(WeddingProject project, int userId, string role) => CoOperatorPolicy.CanManage(project.ManagerId, userId, role);

    private Task<WeddingProject?> LoadAsync(string projectCode, CancellationToken cancellationToken) =>
        db.WeddingProjects.Include(x => x.Manager)
            .Include(x => x.ProjectManagers).ThenInclude(x => x.Manager)
            .Include(x => x.ProjectManagers).ThenInclude(x => x.AssignedByNavigation)
            .Include(x => x.ProjectManagers).ThenInclude(x => x.RemovedByNavigation)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);

    private static ProjectManagersView Map(WeddingProject project, int userId, string role)
    {
        var rows = project.ProjectManagers
            .OrderBy(x => x.RemovedAt is not null).ThenBy(x => x.Role == "Primary" ? 0 : 1).ThenByDescending(x => x.AssignedAt)
            .Select(x => new ProjectManagerView(x.ProjectManagerId, x.ManagerId, x.Manager.FullName, x.Manager.Email, x.Role, x.AssignedByNavigation?.FullName,
                Utc(x.AssignedAt), x.Reason, x.RemovedByNavigation?.FullName, x.RemovedAt is { } r ? Utc(r) : null, x.RemovalReason, x.RemovedAt is null))
            .ToList();
        // Projects older than the table still show their Primary.
        if (!rows.Any(x => x.Role == "Primary" && x.Active))
            rows.Insert(0, new ProjectManagerView(0, project.ManagerId, project.Manager.FullName, project.Manager.Email, "Primary", null, Utc(project.CreatedAt),
                "Người tạo sự kiện", null, null, null, true));
        return new ProjectManagersView(project.ProjectCode, CanManage(project, userId, role), rows);
    }

    public async Task<ProjectManagersView?> GetAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default) =>
        await LoadAsync(projectCode, cancellationToken) is { } project ? Map(project, userId, role) : null;

    public async Task<IReadOnlyList<OptionItem>> GetCandidatesAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.AsNoTracking().Where(x => x.ProjectCode == projectCode)
            .Select(x => new { x.ProjectId, x.ManagerId }).FirstOrDefaultAsync(cancellationToken);
        if (project is null) return [];
        return await db.Users.AsNoTracking()
            .Where(u => u.Role == "Manager" && u.Status == "Active" && u.UserId != project.ManagerId
                && !db.ProjectManagers.Any(m => m.ProjectId == project.ProjectId && m.ManagerId == u.UserId && m.RemovedAt == null))
            .OrderBy(u => u.FullName).Select(u => new OptionItem(u.UserId, u.FullName + " (" + u.Email + ")")).ToListAsync(cancellationToken);
    }
}
