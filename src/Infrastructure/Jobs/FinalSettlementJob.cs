using Hangfire;
using Microsoft.Extensions.Logging;
using Application.Payments;

namespace Infrastructure.Jobs;

/// <summary>
/// JOB-06: Calculate Final Settlement daily at 01:00 (FE-10, Report 3 SRS &amp; Report 4 SDS).
/// Checks Wedding Projects whose event was held yesterday, marks them Event Completed,
/// calculates final settlement amount, creates or updates the FINAL payment milestone with Status = Due,
/// and notifies the assigned Manager for settlement review and verification.
/// </summary>
public sealed class FinalSettlementJob(
    IFinalSettlementService settlementService,
    ILogger<FinalSettlementJob> logger)
{
    [DisableConcurrentExecution(300)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Triggering JOB-06: Calculate Final Settlement.");

        var result = await settlementService.PrepareFinalSettlementsAsync(
            new PrepareFinalSettlementCommand(),
            cancellationToken);

        logger.LogInformation(
            "Finished JOB-06: ExecutionId={ExecutionId}, Found={Found}, EventsCompleted={Events}, SettlementsPrepared={Prepared}, FinalMilestonesCreated={Milestones}, Emails={Emails}, Skipped={Skipped}, Failed={Failed}.",
            result.ExecutionId,
            result.TotalProjectsFound,
            result.TotalEventsCompleted,
            result.TotalSettlementsPrepared,
            result.TotalFinalMilestonesCreated,
            result.TotalEmailsQueued,
            result.TotalProjectsSkipped,
            result.TotalFailed);
    }
}
