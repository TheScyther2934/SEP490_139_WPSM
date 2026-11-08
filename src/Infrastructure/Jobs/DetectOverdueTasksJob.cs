using Hangfire;
using Microsoft.Extensions.Logging;
using Application.OverdueAlerts;

namespace Infrastructure.Jobs;

/// <summary>
/// JOB-05: Detect overdue day-of tasks every 1 minute (FE-09, Report 3 SRS &amp; Report 4 SDS).
/// Checks active wedding projects with Event Date = Today, queries pending day-of tasks exceeding deadline,
/// transitions status to Overdue via domain method, raises DayOfTaskOverdueEvent, creates in-app notifications
/// for the assigned Manager and active Admins, and queues email via EmailOutbox for JOB-07 dispatch.
/// </summary>
public sealed class DetectOverdueTasksJob(
    IDetectOverdueTasksService overdueService,
    ILogger<DetectOverdueTasksJob> logger)
{
    [DisableConcurrentExecution(60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Triggering JOB-05: Detect Overdue Day-of Tasks.");

        var result = await overdueService.DetectOverdueTasksAsync(
            new DetectOverdueTasksCommand(),
            cancellationToken);

        logger.LogInformation(
            "Finished JOB-05: ExecutionId={ExecutionId}, HasEventToday={HasEventToday}, Checked={Checked}, OverdueDetected={Overdue}, AlertsCreated={Alerts}, Notifications={Notifications}, Skipped={Skipped}, Failed={Failed}.",
            result.ExecutionId,
            result.HasEventToday,
            result.TotalTasksChecked,
            result.TotalOverdueDetected,
            result.TotalAlertsCreated,
            result.TotalNotificationsCreated,
            result.TotalSkipped,
            result.TotalFailed);
    }
}
