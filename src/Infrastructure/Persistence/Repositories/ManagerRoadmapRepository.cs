using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements <see cref="IManagerRoadmapRepository"/> for M-06 Master Roadmap &amp; Tasks (FE-03, UC-21).
/// Enforces:
///   - Project authorization for Manager (or Admin) server-side
///   - Task owner filtering (All, Manager, Customer) preserving all 4 canonical milestones
///   - Strict role separation: Manager cannot mark Customer tasks Done
///   - Server-side UTC timestamp for completion
///   - Overdue calculation unified with Customer roadmap (Vietnam UTC+7)
///   - FE-08 Proof-of-Execution check for tasks requiring proof
/// </summary>
public sealed class ManagerRoadmapRepository(WpsmDbContext db) : IManagerRoadmapRepository
{
    // ── Authorization check ──────────────────────────────────────────────────
    public async Task<bool> CanManageProjectAsync(
        int managerId,
        string role,
        string projectIdentifier,
        CancellationToken cancellationToken = default)
    {
        var isNumeric = int.TryParse(projectIdentifier, out var intId);
        return await db.WeddingProjects
            .AsNoTracking()
            .AnyAsync(p =>
                (p.ProjectCode == projectIdentifier || (isNumeric && p.ProjectId == intId)) &&
                (role == "Admin" || p.ManagerId == managerId),
                cancellationToken);
    }

    // ── Load Roadmap ─────────────────────────────────────────────────────────
    public async Task<ManagerRoadmapView?> GetRoadmapAsync(
        int managerId,
        string role,
        string projectIdentifier,
        TaskOwnerFilter filter = TaskOwnerFilter.All,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify project exists & manager is authorized
        var isNumeric = int.TryParse(projectIdentifier, out var intId);
        var project = await db.WeddingProjects
            .AsNoTracking()
            .Include(p => p.Customer)
            .Include(p => p.Hall)
            .Include(p => p.Slot)
            .FirstOrDefaultAsync(p =>
                p.ProjectCode == projectIdentifier ||
                (isNumeric && p.ProjectId == intId),
                cancellationToken);

        if (project is null) return null;

        // Manager authorization: must own project unless Admin
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) && project.ManagerId != managerId)
        {
            return null;
        }

        // 2. Load all tasks for this project
        var allTasks = await db.RoadmapTasks
            .AsNoTracking()
            .Where(t => t.ProjectId == project.ProjectId)
            .OrderBy(t => t.DueDate)
            .ToListAsync(cancellationToken);

        var today = RoadmapRules.GetVietnamToday();
        var countdown = RoadmapRules.CalculateCountdown(project.EventDate, today);

        // Pre-filter statistics for metadata cards
        var totalTasks = allTasks.Count;
        var doneTasks = allTasks.Count(t => string.Equals(t.Status, "Done", StringComparison.OrdinalIgnoreCase));
        var overdueTasks = allTasks.Count(t =>
            !string.Equals(t.Status, "Done", StringComparison.OrdinalIgnoreCase) && t.DueDate < today);
        var managerTasksCount = allTasks.Count(t =>
            string.Equals(t.AssigneeRole, "Manager", StringComparison.OrdinalIgnoreCase));
        var customerTasksCount = allTasks.Count(t =>
            string.Equals(t.AssigneeRole, "Customer", StringComparison.OrdinalIgnoreCase));
        var progressPct = totalTasks > 0 ? (int)Math.Round((double)doneTasks / totalTasks * 100) : 0;

        // 3. Apply Owner filter to displayed tasks
        var filteredTasks = (filter switch
        {
            TaskOwnerFilter.Manager => allTasks.Where(t =>
                string.Equals(t.AssigneeRole, "Manager", StringComparison.OrdinalIgnoreCase)),
            TaskOwnerFilter.Customer => allTasks.Where(t =>
                string.Equals(t.AssigneeRole, "Customer", StringComparison.OrdinalIgnoreCase)),
            _ => (IEnumerable<RoadmapTask>)allTasks
        }).ToList();

        var filteredTaskViews = filteredTasks.Select(t => MapTask(t, today)).ToList();

        // 4. Group tasks by milestone in canonical order (all 4 milestones always preserved)
        var tasksByMilestone = filteredTasks.ToLookup(t => t.Milestone);
        var groups = new List<ManagerRoadmapMilestoneGroup>();

        foreach (var milestone in RoadmapRules.MilestoneOrder)
        {
            var milestoneDate = RoadmapRules.ResolveMilestoneDate(milestone, project.EventDate);
            var groupTasks = tasksByMilestone[milestone]
                .Select(t => MapTask(t, today))
                .ToList();

            groups.Add(new ManagerRoadmapMilestoneGroup(
                milestone,
                milestoneDate,
                groupTasks,
                groupTasks.Count,
                groupTasks.Count(tv => tv.Status == "Done")));
        }

        // Any non-canonical milestone
        var knownMilestones = RoadmapRules.MilestoneOrder.ToHashSet();
        var unknownTasks = filteredTasks
            .Where(t => !knownMilestones.Contains(t.Milestone))
            .Select(t => MapTask(t, today))
            .ToList();

        if (unknownTasks.Count > 0)
        {
            groups.Add(new ManagerRoadmapMilestoneGroup(
                unknownTasks[0].Milestone,
                project.EventDate,
                unknownTasks,
                unknownTasks.Count,
                unknownTasks.Count(tv => tv.Status == "Done")));
        }

        return new ManagerRoadmapView(
            project.ProjectCode,
            project.Customer?.FullName ?? "Chưa rõ",
            project.Hall?.Name ?? "Chưa phân sảnh",
            project.Slot?.SlotName ?? "Chưa phân ca",
            project.EventDate,
            countdown,
            groups,
            filteredTaskViews,
            totalTasks,
            doneTasks,
            overdueTasks,
            managerTasksCount,
            customerTasksCount,
            progressPct);
    }

    // ── Load flat task list ──────────────────────────────────────────────────
    public async Task<IReadOnlyList<ManagerRoadmapTaskView>?> GetRoadmapTasksAsync(
        int managerId,
        string role,
        string projectIdentifier,
        TaskOwnerFilter filter = TaskOwnerFilter.All,
        CancellationToken cancellationToken = default)
    {
        var roadmap = await GetRoadmapAsync(managerId, role, projectIdentifier, filter, cancellationToken);
        return roadmap?.Tasks;
    }

    // ── Complete Manager-owned task ──────────────────────────────────────────
    public async Task<CompleteManagerTaskResult> CompleteTaskAsync(
        int managerId,
        string role,
        string projectIdentifier,
        int taskId,
        IReadOnlyList<string>? proofPhotoPaths = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify project exists & access rights
        var isNumeric = int.TryParse(projectIdentifier, out var intId);
        var project = await db.WeddingProjects
            .FirstOrDefaultAsync(p =>
                p.ProjectCode == projectIdentifier ||
                (isNumeric && p.ProjectId == intId),
                cancellationToken);

        if (project is null)
            return new CompleteManagerTaskResult(false, "Dự án không tồn tại.", null);

        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) && project.ManagerId != managerId)
            return new CompleteManagerTaskResult(false, "Bạn không có quyền quản lý dự án này.", null);

        // 2. Load the specific task and verify it belongs to this project
        var task = await db.RoadmapTasks
            .FirstOrDefaultAsync(t => t.TaskId == taskId && t.ProjectId == project.ProjectId, cancellationToken);

        if (task is null)
            return new CompleteManagerTaskResult(false, "Nhiệm vụ không tồn tại trong dự án này.", null);

        // 3. Security check: Manager can only complete tasks assigned to Manager role
        if (!string.Equals(task.AssigneeRole, "Manager", StringComparison.OrdinalIgnoreCase))
            return new CompleteManagerTaskResult(false,
                "Bạn không có quyền xác nhận hoàn thành nhiệm vụ này — nhiệm vụ thuộc trách nhiệm của Khách hàng.", null);

        var today = RoadmapRules.GetVietnamToday();

        // 4. Idempotency check: if already Done, return success without mutating
        if (string.Equals(task.Status, "Done", StringComparison.OrdinalIgnoreCase))
            return new CompleteManagerTaskResult(true, "Nhiệm vụ đã được hoàn thành trước đó.", MapTask(task, today));

        // 5. Proof-of-Execution check (FE-08)
        var requiresProof = RoadmapRules.RequiresProof(task.Milestone, task.Title);
        if (requiresProof)
        {
            var hasProvidedPhotos = proofPhotoPaths is { Count: > 0 };
            var hasExistingPhotos = await db.StoredFiles
                .AnyAsync(f => f.ProjectId == project.ProjectId && f.Purpose == "ProofPhoto", cancellationToken)
                || await db.ExecutionPhotos
                .AnyAsync(ep => ep.DayOfTask.ProjectId == project.ProjectId, cancellationToken);

            if (!hasProvidedPhotos && !hasExistingPhotos)
            {
                return new CompleteManagerTaskResult(false,
                    "Nhiệm vụ này yêu cầu tối thiểu 1 ảnh minh chứng thực hiện (1-3 ảnh) theo FE-08 trước khi xác nhận hoàn thành.",
                    null);
            }

            // If photos provided in request, save StoredFiles records
            if (proofPhotoPaths is not null)
            {
                foreach (var path in proofPhotoPaths)
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    db.StoredFiles.Add(new StoredFile
                    {
                        ProjectId = project.ProjectId,
                        Purpose = "ProofPhoto",
                        StorageKey = $"proof/{project.ProjectCode}/{Guid.NewGuid()}_{System.IO.Path.GetFileName(path)}",
                        OriginalName = System.IO.Path.GetFileName(path),
                        ContentType = "image/jpeg",
                        SizeBytes = 1024,
                        UploadedBy = managerId,
                        UploadedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // 6. Mark Done with server-side UTC timestamp (never client-provided)
        task.Status = "Done";
        task.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new CompleteManagerTaskResult(true, "Đã đánh dấu hoàn thành nhiệm vụ thành công.", MapTask(task, today));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static ManagerRoadmapTaskView MapTask(RoadmapTask task, DateOnly today)
    {
        var status = RoadmapRules.CalculateStatus(task.Status, task.DueDate, today);
        var requiresProof = RoadmapRules.RequiresProof(task.Milestone, task.Title);

        return new ManagerRoadmapTaskView(
            task.TaskId,
            task.Milestone,
            task.Title,
            task.AssigneeRole,
            task.DueDate,
            status,
            task.CompletedAt,
            requiresProof);
    }
}
