using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.Dashboard;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Persistence;

namespace Infrastructure.Dashboard;

/// <summary>
/// M-01 Manager Dashboard Service implementation (FE-03, FE-09, SRS §3.3).
/// Returns real operational metrics, my tasks, alert panels (Overdue day-of tasks &amp; Stale BEOs),
/// and upcoming project milestones strictly scoped to the authenticated manager.
/// </summary>
public sealed class ManagerDashboardService(WpsmDbContext db) : IManagerDashboardService
{
    private static readonly string[] ActiveStatuses = ["Active Baseline", "Baseline Frozen", "Event Completed"];

    public async Task<ManagerDashboardResponse> GetDashboardAsync(
        int managerId,
        CancellationToken cancellationToken = default)
    {
        var today = RoadmapRules.GetVietnamToday();
        var nowUtc = DateTime.UtcNow;

        // 1. Projects scoped strictly to the manager
        var projects = await db.WeddingProjects
            .AsNoTracking()
            .Include(p => p.Customer)
            .Include(p => p.Hall)
            .Include(p => p.Slot)
            .Include(p => p.ChangeRequests)
            .Include(p => p.Beos)
            .Where(p => p.ManagerId == managerId)
            .ToListAsync(cancellationToken);

        var activeProjects = projects.Where(p => ActiveStatuses.Contains(p.Status)).ToList();
        var activeProjectIds = activeProjects.Select(p => p.ProjectId).ToList();

        // Stale BEOs check: latest BEO version per project is Stale
        var staleBeoAlerts = new List<StaleBeoAlertView>();
        int staleBeoCount = 0;
        foreach (var p in activeProjects)
        {
            var latestBeo = p.Beos.OrderByDescending(b => b.Version).FirstOrDefault();
            if (latestBeo != null && string.Equals(latestBeo.Status, "Stale", StringComparison.OrdinalIgnoreCase))
            {
                staleBeoCount++;
                var crCode = p.ChangeRequests
                    .Where(c => c.Status == "Approved")
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => c.Crcode)
                    .FirstOrDefault();

                staleBeoAlerts.Add(new StaleBeoAlertView(
                    BeoId: latestBeo.Beoid,
                    ProjectCode: p.ProjectCode,
                    HallName: p.Hall?.Name ?? "",
                    EventDate: p.EventDate,
                    Version: latestBeo.Version,
                    Status: "Stale",
                    Notes: latestBeo.Notes,
                    ApprovedCrCode: crCode));
            }
        }

        // Active overdue tasks for this manager
        var overdueTasks = await db.DayOfTasks
            .AsNoTracking()
            .Where(t => activeProjectIds.Contains(t.ProjectId) && t.Status == "Overdue" && t.CompletedAt == null)
            .OrderBy(t => t.DeadlineTime)
            .Select(t => new
            {
                t.DayOfTaskId,
                t.Project.ProjectCode,
                t.TaskName,
                t.DeadlineTime,
                Hall = t.Project.Hall.Name,
                t.Project.EventDate,
                CustomerName = t.Project.Customer.FullName
            })
            .ToListAsync(cancellationToken);

        var overdueAlertViews = overdueTasks.Select(t => new OverdueAlertView(
            TaskId: t.DayOfTaskId,
            ProjectCode: t.ProjectCode,
            TaskName: t.TaskName,
            Deadline: DateTime.SpecifyKind(t.DeadlineTime, DateTimeKind.Utc),
            OverdueMinutes: Math.Max(0, (int)(nowUtc - t.DeadlineTime).TotalMinutes),
            Status: "Overdue",
            Hall: t.Hall,
            EventDate: t.EventDate,
            CustomerName: t.CustomerName,
            ManagerName: null)).ToList();

        // Tasks due today count (Roadmap tasks for manager + DayOfTasks on today's events)
        var roadmapTasksDueToday = await db.RoadmapTasks
            .AsNoTracking()
            .CountAsync(t => activeProjectIds.Contains(t.ProjectId)
                          && t.AssigneeRole == "Manager"
                          && t.Status != "Done"
                          && t.DueDate == today, cancellationToken);

        var dayOfTasksDueToday = await db.DayOfTasks
            .AsNoTracking()
            .CountAsync(t => activeProjectIds.Contains(t.ProjectId)
                          && t.Project.EventDate == today
                          && t.Status != "Done", cancellationToken);

        int tasksDueToday = roadmapTasksDueToday + dayOfTasksDueToday;

        // 2. KPI calculation: 5 SRS §3.3 KPIs
        var kpis = new ManagerKpiSummary(
            ActiveProjects: activeProjects.Count,
            TasksDueToday: tasksDueToday,
            OpenChangeRequests: activeProjects.Sum(p => p.ChangeRequests.Count(c => c.Status is "Draft" or "Negotiating" or "Submitted")),
            StaleBeos: staleBeoCount,
            OverdueTasksCount: overdueAlertViews.Count,
            DraftingProjects: activeProjects.Count(p => p.Status == "Active Baseline"),
            EventsIn7Days: activeProjects.Count(p => p.EventDate >= today && p.EventDate <= today.AddDays(7)));

        // 3. My Tasks: Manager-assigned roadmap tasks that are Pending or Overdue
        var managerRoadmapTasks = await db.RoadmapTasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Where(t => activeProjectIds.Contains(t.ProjectId)
                     && t.AssigneeRole == "Manager"
                     && t.Status != "Done")
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.TaskId)
            .Take(20)
            .ToListAsync(cancellationToken);

        var myTasks = managerRoadmapTasks.Select(t =>
        {
            var calculatedStatus = RoadmapRules.CalculateStatus(t.Status, t.DueDate, today);
            var reqProof = RoadmapRules.RequiresProof(t.Milestone, t.Title);
            return new ManagerTaskItemView(
                TaskId: t.TaskId,
                ProjectCode: t.Project.ProjectCode,
                Title: t.Title,
                Milestone: t.Milestone,
                DueDate: t.DueDate,
                Status: calculatedStatus,
                RequiresProof: reqProof,
                TaskType: "Roadmap");
        }).ToList();

        // 4. Upcoming Milestones & Events
        var upcomingMilestones = new List<UpcomingMilestoneView>();
        var upcomingProjects = activeProjects
            .Where(p => p.EventDate >= today)
            .OrderBy(p => p.EventDate)
            .Take(8)
            .ToList();

        foreach (var p in upcomingProjects)
        {
            var allTasks = await db.RoadmapTasks
                .AsNoTracking()
                .Where(t => t.ProjectId == p.ProjectId)
                .ToListAsync(cancellationToken);

            var totalCount = allTasks.Count;
            var doneCount = allTasks.Count(t => t.Status == "Done");

            // Find current active milestone
            var pendingTask = allTasks.Where(t => t.Status != "Done").OrderBy(t => t.DueDate).FirstOrDefault();
            var activeMilestone = pendingTask?.Milestone ?? "Event Day";
            var milestoneDueDate = pendingTask?.DueDate ?? p.EventDate;
            var daysRemaining = RoadmapRules.CalculateCountdown(p.EventDate, today);

            upcomingMilestones.Add(new UpcomingMilestoneView(
                ProjectCode: p.ProjectCode,
                CoupleName: p.Customer?.FullName ?? "",
                HallName: p.Hall?.Name ?? "",
                EventDate: p.EventDate,
                Milestone: activeMilestone,
                DueDate: milestoneDueDate,
                DaysRemaining: daysRemaining,
                TotalTasks: totalCount,
                CompletedTasks: doneCount));
        }

        return new ManagerDashboardResponse(
            Kpis: kpis,
            MyTasks: myTasks,
            UpcomingMilestones: upcomingMilestones,
            OverdueTasks: overdueAlertViews,
            StaleBeos: staleBeoAlerts);
    }
}
