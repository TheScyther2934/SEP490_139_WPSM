using Hangfire;
using Microsoft.Extensions.Logging;
using Application.Roadmap;

namespace Infrastructure.Jobs;

/// <summary>
/// JOB-01: Sends Roadmap Milestone Reminders daily at 07:00 (FE-03, Report 3 SRS &amp; Report 4 SDS).
/// Checks pending tasks nearing their milestone due date (T-90, T-30, T-7, Event Day)
/// and notifies project owners (Customer / Manager) via in-app notifications and queued emails.
/// </summary>
public sealed class SendRoadmapMilestoneRemindersJob(
    IRoadmapReminderService reminderService,
    ILogger<SendRoadmapMilestoneRemindersJob> logger)
{
    [DisableConcurrentExecution(300)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Triggering JOB-01: Send Roadmap Milestone Reminders.");

        var result = await reminderService.SendMilestoneRemindersAsync(
            new SendRoadmapMilestoneRemindersCommand(),
            cancellationToken);

        logger.LogInformation(
            "Finished JOB-01: ExecutionId={ExecutionId}, Found={Found}, Reminders={Created}, Emails={Emails}, Skipped={Skipped}, Failed={Failed}.",
            result.ExecutionId,
            result.TotalTasksFound,
            result.TotalRemindersCreated,
            result.TotalEmailsQueued,
            result.TotalSkipped,
            result.TotalFailed);
    }
}
