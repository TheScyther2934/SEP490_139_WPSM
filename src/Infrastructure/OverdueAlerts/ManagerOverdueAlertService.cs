using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.OverdueAlerts;
using Application.Persistence;
using Infrastructure.Persistence;

namespace Infrastructure.OverdueAlerts;

/// <summary>
/// M-23: Fetches active overdue tasks for projects assigned to a Manager.
/// </summary>
public sealed class ManagerOverdueAlertService(WpsmDbContext db) : IManagerOverdueAlertService
{
    public async Task<IReadOnlyList<OverdueAlertView>> GetManagerOverdueAlertsAsync(
        int managerId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tasks = await db.DayOfTasks.AsNoTracking()
            .Where(x => x.Project.ManagerId == managerId && x.Status == "Overdue" && x.CompletedAt == null)
            .OrderBy(x => x.DeadlineTime)
            .Select(x => new
            {
                x.DayOfTaskId,
                x.Project.ProjectCode,
                x.TaskName,
                x.DeadlineTime,
                Hall = x.Project.Hall.Name,
                x.Project.EventDate,
                CustomerName = x.Project.Customer.FullName,
                ManagerName = x.Project.Manager.FullName
            })
            .ToListAsync(cancellationToken);

        return tasks.Select(x => new OverdueAlertView(
            TaskId: x.DayOfTaskId,
            ProjectCode: x.ProjectCode,
            TaskName: x.TaskName,
            Deadline: DateTime.SpecifyKind(x.DeadlineTime, DateTimeKind.Utc),
            OverdueMinutes: Math.Max(0, (int)(now - x.DeadlineTime).TotalMinutes),
            Status: "Overdue",
            Hall: x.Hall,
            EventDate: x.EventDate,
            CustomerName: x.CustomerName,
            ManagerName: x.ManagerName)).ToList();
    }
}
