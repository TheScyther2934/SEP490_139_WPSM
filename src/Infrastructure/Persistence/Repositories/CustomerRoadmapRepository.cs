using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements <see cref="ICustomerRoadmapRepository"/> for C-03 (UC-05, UC-06).
/// Responsible for:
///   - Verifying Customer->Project ownership before any data access.
///   - Resolving Overdue status in Vietnam UTC+7 time.
///   - Grouping tasks by canonical milestone order.
///   - Completing Customer-owned tasks with server-side timestamp.
/// </summary>
public sealed class CustomerRoadmapRepository(WpsmDbContext db) : ICustomerRoadmapRepository
{
    private static readonly TimeZoneInfo VnTz = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    // Canonical milestone display order
    private static readonly IReadOnlyList<string> MilestoneOrder =
        new[] { "T-90", "T-30", "T-7", "Event Day" };

    // ── UC-05: View roadmap ──────────────────────────────────────────────────
    public async Task<CustomerRoadmapView?> GetRoadmapAsync(
        int customerId,
        string projectCode,
        CancellationToken cancellationToken = default)
    {
        // Ownership verification
        var project = await db.WeddingProjects
            .AsNoTracking()
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(
                p => p.ProjectCode == projectCode && p.CustomerId == customerId,
                cancellationToken);

        if (project is null) return null;

        // All tasks for this project, ordered by DueDate
        var tasks = await db.RoadmapTasks
            .AsNoTracking()
            .Where(t => t.ProjectId == project.ProjectId)
            .OrderBy(t => t.DueDate)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));

        // Countdown (min 0)
        var countdown = Math.Max(0, project.EventDate.DayNumber - today.DayNumber);

        // Group tasks by milestone in canonical order
        var tasksByMilestone = tasks.ToLookup(t => t.Milestone);

        var groups = new List<CustomerRoadmapMilestoneGroup>();
        foreach (var milestone in MilestoneOrder)
        {
            var milestoneDate = ResolveMilestoneDate(milestone, project.EventDate);
            var milestoneTaskViews = tasksByMilestone[milestone]
                .Select(t => MapTask(t, today))
                .ToList();

            // Include group even if empty (SRS requires all 4 milestones visible)
            groups.Add(new CustomerRoadmapMilestoneGroup(
                milestone,
                milestoneDate,
                milestoneTaskViews,
                milestoneTaskViews.Count,
                milestoneTaskViews.Count(tv => tv.Status == "Done")));
        }

        // Any tasks with unknown milestone — append at end
        var knownMilestones = MilestoneOrder.ToHashSet();
        var unknownTasks = tasks
            .Where(t => !knownMilestones.Contains(t.Milestone))
            .Select(t => MapTask(t, today))
            .ToList();
        if (unknownTasks.Count > 0)
        {
            var unknownMilestone = unknownTasks[0].Milestone;
            groups.Add(new CustomerRoadmapMilestoneGroup(
                unknownMilestone,
                project.EventDate,
                unknownTasks,
                unknownTasks.Count,
                unknownTasks.Count(tv => tv.Status == "Done")));
        }

        return new CustomerRoadmapView(
            project.ProjectCode,
            project.Customer.FullName,
            project.EventDate,
            countdown,
            groups,
            tasks.Count,
            tasks.Count(t => t.Status == "Done"));
    }

    // ── UC-06: Complete Customer task ────────────────────────────────────────
    public async Task<CompleteTaskResult> CompleteTaskAsync(
        int customerId,
        string projectCode,
        int taskId,
        CancellationToken cancellationToken = default)
    {
        // Verify project ownership
        var project = await db.WeddingProjects
            .FirstOrDefaultAsync(
                p => p.ProjectCode == projectCode && p.CustomerId == customerId,
                cancellationToken);

        if (project is null)
            return new CompleteTaskResult(false, "Dự án không tồn tại hoặc bạn không có quyền truy cập.", null);

        // Load the specific task and verify it belongs to this project
        var task = await db.RoadmapTasks
            .FirstOrDefaultAsync(
                t => t.TaskId == taskId && t.ProjectId == project.ProjectId,
                cancellationToken);

        if (task is null)
            return new CompleteTaskResult(false, "Nhiệm vụ không tồn tại trong dự án này.", null);

        // Security: Customer may only complete tasks assigned to Customer role
        if (!string.Equals(task.AssigneeRole, "Customer", StringComparison.OrdinalIgnoreCase))
            return new CompleteTaskResult(false, "Bạn không có quyền xác nhận hoàn thành nhiệm vụ này — nhiệm vụ thuộc trách nhiệm của Ban điều phối.", null);

        // Idempotency: if already Done, return success without mutating
        if (task.Status == "Done")
        {
            var today0 = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));
            return new CompleteTaskResult(true, "Nhiệm vụ đã được hoàn thành trước đó.", MapTask(task, today0));
        }

        // Mark done — server-side timestamp, not client-provided
        task.Status = "Done";
        task.CompletedAt = DateTime.UtcNow;

        // G-04: Notify the manager that the customer completed this task
        db.Notifications.Add(new Notification
        {
            RecipientId = project.ManagerId,
            Type = "Roadmap reminder",
            Message = $"Khách hàng đã hoàn thành nhiệm vụ '{task.Title}' thuộc dự án {project.ProjectCode}.",
            LinkUrl = $"/manager/projects/{project.ProjectCode}/roadmap",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);

        var todayVn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));
        return new CompleteTaskResult(true, "Đã đánh dấu hoàn thành nhiệm vụ.", MapTask(task, todayVn));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CustomerRoadmapTaskView MapTask(
        RoadmapTask task,
        DateOnly today)
    {
        var status = task.Status == "Done" ? "Done"
            : task.DueDate < today ? "Overdue"
            : "Pending";

        return new CustomerRoadmapTaskView(
            task.TaskId,
            task.Milestone,
            task.Title,
            task.AssigneeRole,
            task.DueDate,
            status,
            task.CompletedAt);
    }

    private static DateOnly ResolveMilestoneDate(string milestone, DateOnly eventDate) =>
        milestone switch
        {
            "T-90" => eventDate.AddDays(-90),
            "T-30" => eventDate.AddDays(-30),
            "T-7"  => eventDate.AddDays(-7),
            _      => eventDate   // "Event Day" and any unknown
        };
}
