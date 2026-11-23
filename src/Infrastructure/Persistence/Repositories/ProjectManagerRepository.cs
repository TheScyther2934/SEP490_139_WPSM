using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class ProjectManagerRepository(WpsmDbContext db) : IProjectManagerRepository
{
    private static readonly string[] Finished = ["Settled", "Closed"];

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // Primary = the creator (WeddingProjects.ManagerId). Only the Primary or an Admin may change the operator list.
    private static bool CanManage(WeddingProject project, int userId, string role) => role == "Admin" || role == "Manager" && project.ManagerId == userId;

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

    public async Task<ProjectManagerResult> AddAsync(string projectCode, int managerId, string? reason, int actorId, string actorRole, CancellationToken cancellationToken = default)
    {
        var project = await LoadAsync(projectCode, cancellationToken);
        if (project is null) return new ProjectManagerResult(null, NotFound: true);
        if (!CanManage(project, actorId, actorRole)) return new ProjectManagerResult(null, "Chỉ Manager tạo sự kiện hoặc Admin mới được thêm người điều hành.", Forbidden: true);
        if (Finished.Contains(project.Status)) return new ProjectManagerResult(null, "Sự kiện đã tất toán hoặc đã đóng, không thể thêm người điều hành.");
        var note = reason?.Trim() ?? "";
        if (note.Length is < 5 or > 500) return new ProjectManagerResult(null, "Vui lòng nhập lý do ủy quyền (5–500 ký tự).");

        var manager = await db.Users.FirstOrDefaultAsync(u => u.UserId == managerId, cancellationToken);
        if (manager is null || manager.Role != "Manager" || manager.Status != "Active")
            return new ProjectManagerResult(null, "Người được ủy quyền phải là tài khoản Manager đang hoạt động.");
        if (managerId == project.ManagerId || project.ProjectManagers.Any(x => x.ManagerId == managerId && x.RemovedAt is null))
            return new ProjectManagerResult(null, $"{manager.FullName} đang điều hành sự kiện này.");

        // Older projects get their Primary row on first change, so the history is complete.
        if (!project.ProjectManagers.Any(x => x.Role == "Primary" && x.RemovedAt is null))
            project.ProjectManagers.Add(new ProjectManager { ManagerId = project.ManagerId, Manager = project.Manager, Role = "Primary", AssignedBy = project.CreatedBy, AssignedAt = project.CreatedAt, Reason = "Người tạo sự kiện" });
        project.ProjectManagers.Add(new ProjectManager { ManagerId = managerId, Manager = manager, Role = "CoManager", AssignedBy = actorId, AssignedAt = DateTime.UtcNow, Reason = note });
        db.Notifications.Add(new Notification
        {
            RecipientId = managerId, Type = "Delegation",
            Message = $"Bạn được ủy quyền đồng điều hành sự kiện {project.ProjectCode}. Lý do: {note}",
            LinkUrl = $"/manager/projects/{Uri.EscapeDataString(project.ProjectCode)}"
        });
        await db.SaveChangesAsync(cancellationToken);
        return new ProjectManagerResult(Map((await LoadAsync(projectCode, cancellationToken))!, actorId, actorRole));
    }

    public async Task<ProjectManagerResult> RemoveAsync(string projectCode, int projectManagerId, string? reason, int actorId, string actorRole, CancellationToken cancellationToken = default)
    {
        var project = await LoadAsync(projectCode, cancellationToken);
        if (project is null) return new ProjectManagerResult(null, NotFound: true);
        if (!CanManage(project, actorId, actorRole)) return new ProjectManagerResult(null, "Chỉ Manager tạo sự kiện hoặc Admin mới được gỡ người điều hành.", Forbidden: true);
        var row = project.ProjectManagers.FirstOrDefault(x => x.ProjectManagerId == projectManagerId && x.RemovedAt is null);
        if (row is null) return new ProjectManagerResult(null, NotFound: true);
        if (row.Role == "Primary") return new ProjectManagerResult(null, "Không thể gỡ Manager tạo sự kiện.");
        var note = reason?.Trim() ?? "";
        if (note.Length is < 5 or > 500) return new ProjectManagerResult(null, "Vui lòng nhập lý do gỡ quyền (5–500 ký tự).");

        row.RemovedAt = DateTime.UtcNow;
        row.RemovedBy = actorId;
        row.RemovalReason = note;
        db.Notifications.Add(new Notification
        {
            RecipientId = row.ManagerId, Type = "Delegation",
            Message = $"Quyền đồng điều hành sự kiện {project.ProjectCode} của bạn đã kết thúc. Lý do: {note}",
            LinkUrl = "/manager/projects"
        });
        await db.SaveChangesAsync(cancellationToken);
        return new ProjectManagerResult(Map((await LoadAsync(projectCode, cancellationToken))!, actorId, actorRole));
    }
}
