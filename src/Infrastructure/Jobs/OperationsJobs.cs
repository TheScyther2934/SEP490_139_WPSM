using System.Net;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Jobs;

/// <summary>JOB-01..JOB-06 of Report 4 IV.3 (time zone Asia/Ho_Chi_Minh). Each job selects only rows still in the state it changes, so a rerun is harmless.</summary>
public sealed class OperationsJobs(WpsmDbContext db, IOperationsBeoRepository beos, IDayOfRepository dayOf, IPaymentRepository payments, IEmailOutbox emailOutbox,
    ILogger<OperationsJobs> logger)
{
    /// <summary>JOB-01: Pending tasks due within 3 days, not reminded in the last 24 hours.</summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task RoadmapRemindersAsync(CancellationToken cancellationToken)
    {
        var today = TodayVn();
        var limit = today.AddDays(3);
        var since = DateTime.UtcNow.AddHours(-24);
        var tasks = await db.RoadmapTasks.Include(x => x.Project).ThenInclude(x => x.Customer)
            .Where(x => x.Status != "Done" && x.DueDate <= limit && (x.RemindedAt == null || x.RemindedAt < since)
                && (x.Project.Status == "Baseline Frozen" || x.Project.Status == "Active Baseline"))
            .ToListAsync(cancellationToken);
        foreach (var task in tasks)
        {
            var project = task.Project;
            var message = $"Nhắc việc {task.Milestone}: \"{task.Title}\" hạn {task.DueDate:dd/MM/yyyy}.";
            if (task.AssigneeRole == "Customer")
            {
                Notify(db, project.CustomerId, "Roadmap reminder", message, Links.CustomerRoadmap);
                emailOutbox.Enqueue(project.Customer.Email, $"Nhắc việc chuẩn bị tiệc cưới {project.ProjectCode}",
                    $"<p>Xin chào {WebUtility.HtmlEncode(project.Customer.FullName)},</p><p>{WebUtility.HtmlEncode(message)}</p>", "Reminder", project.ProjectId);
            }
            else Notify(db, project.ManagerId, "Roadmap reminder", $"{project.ProjectCode} – {message}", Links.ManagerRoadmap(project.ProjectCode));
            task.RemindedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("JOB-01 sent {Count} roadmap reminders.", tasks.Count);
    }

    /// <summary>JOB-02</summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task T30PaymentsAsync(CancellationToken cancellationToken) =>
        logger.LogInformation("JOB-02 issued {Count} T-30 payments.", await payments.IssueT30PaymentsAsync(TodayVn(), cancellationToken));

    /// <summary>JOB-03</summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task BeoDraftsAsync(CancellationToken cancellationToken) =>
        logger.LogInformation("JOB-03 created {Count} BEO drafts.", await beos.CreateDueDraftsAsync(TodayVn(), cancellationToken));

    /// <summary>JOB-04</summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task LoadDayOfTimelineAsync(CancellationToken cancellationToken) =>
        logger.LogInformation("JOB-04 loaded {Count} day-of timelines.", await dayOf.LoadTodayAsync(TodayVn(), cancellationToken));

    /// <summary>JOB-05: every minute, but returns at once when no event is held today.</summary>
    [DisableConcurrentExecution(55)]
    [AutomaticRetry(Attempts = 0)]
    public async Task OverdueDetectionAsync(CancellationToken cancellationToken)
    {
        var today = TodayVn();
        if (!await db.WeddingProjects.AnyAsync(x => x.EventDate == today || x.EventDate == today.AddDays(-1), cancellationToken)) return;
        var flagged = await dayOf.DetectOverdueAsync(DateTime.UtcNow, cancellationToken);
        if (flagged > 0) logger.LogInformation("JOB-05 flagged {Count} overdue tasks.", flagged);
    }

    /// <summary>JOB-06</summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task FinalSettlementAsync(CancellationToken cancellationToken) =>
        logger.LogInformation("JOB-06 prepared {Count} final settlements.", await payments.PrepareFinalSettlementsAsync(TodayVn(), cancellationToken));
}
