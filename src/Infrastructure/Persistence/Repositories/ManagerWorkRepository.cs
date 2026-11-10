using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>M-01 KPI, my tasks and alerts; M-06 roadmap with Manager completion (UC-21); M-23 overdue list (UC-32).</summary>
public sealed class ManagerWorkRepository(WpsmDbContext db) : IManagerWorkRepository
{
    private static readonly string[] Active = ["Active Baseline", "Baseline Frozen", "Event Completed"];

    public async Task<ManagerWorkView> GetWorkAsync(int managerId, CancellationToken cancellationToken = default)
    {
        var today = TodayVn();
        var activeProjects = await db.WeddingProjects.CountAsync(x => x.ManagerId == managerId && Active.Contains(x.Status), cancellationToken);
        var tasks = await Tasks(db.RoadmapTasks.Where(x => x.Project.ManagerId == managerId && x.AssigneeRole == "Manager" && x.Status != "Done" && Active.Contains(x.Project.Status))
            .OrderBy(x => x.DueDate).Take(50)).ToListAsync(cancellationToken);
        var openCrs = await db.ChangeRequests.CountAsync(x => x.Project.ManagerId == managerId && x.Status == "Negotiating", cancellationToken);
        var latestBeos = await db.Beos.AsNoTracking().Where(x => x.Project.ManagerId == managerId)
            .Select(x => new { x.ProjectId, x.Version, x.Status, x.Project.ProjectCode, x.Project.Customer.FullName, x.Project.EventDate }).ToListAsync(cancellationToken);
        var stale = latestBeos.GroupBy(x => x.ProjectId).Select(g => g.MaxBy(x => x.Version)!).Where(x => x.Status == "Stale")
            .OrderBy(x => x.EventDate).Select(x => new StaleBeoView(x.ProjectCode, x.FullName, x.Version, x.EventDate)).ToList();
        return new ManagerWorkView(activeProjects, tasks.Count(x => x.DueDate <= today), openCrs, stale.Count, tasks,
            await GetOverdueAsync(managerId, cancellationToken), stale);
    }

    public async Task<IReadOnlyList<ManagerTaskView>> GetRoadmapAsync(string projectCode, CancellationToken cancellationToken = default) =>
        await Tasks(db.RoadmapTasks.Where(x => x.Project.ProjectCode == projectCode).OrderBy(x => x.DueDate).ThenBy(x => x.TaskId)).ToListAsync(cancellationToken);

    public async Task<bool> CompleteTaskAsync(string projectCode, int taskId, int managerId, CancellationToken cancellationToken = default)
    {
        var task = await db.RoadmapTasks.Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.TaskId == taskId && x.Project.ProjectCode == projectCode && x.Project.ManagerId == managerId, cancellationToken);
        if (task is null) return false;
        if (task.AssigneeRole != "Manager") throw new InvalidOperationException("Không thể đánh dấu công việc của khách hàng.");
        if (task.Status == "Done") throw new InvalidOperationException("Công việc đã hoàn thành.");
        task.Status = "Done";
        task.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<OverdueTaskView>> GetOverdueAsync(int managerId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var rows = await db.DayOfTasks.AsNoTracking().Where(x => x.Project.ManagerId == managerId && x.Status == "Overdue").OrderBy(x => x.DeadlineTime)
            .Select(x => new { x.DayOfTaskId, x.Project.ProjectCode, x.Project.Customer.FullName, x.TaskName, x.DeadlineTime, x.Status })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new OverdueTaskView(x.DayOfTaskId, x.ProjectCode, x.FullName, x.TaskName, Utc(x.DeadlineTime),
            Math.Max(0, (int)(now - x.DeadlineTime).TotalMinutes), x.Status)).ToList();
    }

    private static IQueryable<ManagerTaskView> Tasks(IQueryable<Entities.RoadmapTask> query) =>
        query.AsNoTracking().Select(x => new ManagerTaskView(x.TaskId, x.Project.ProjectCode, x.Project.Customer.FullName, x.Milestone, x.Title,
            x.AssigneeRole, x.DueDate, x.Status, x.CompletedAt));
}
