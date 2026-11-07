using Hangfire;
using Microsoft.Extensions.Logging;
using Application.Payments;

namespace Infrastructure.Jobs;

/// <summary>
/// JOB-02: Issue T-30 Payment Request daily at 07:00 (FE-10, Report 3 SRS &amp; Report 4 SDS).
/// Checks Wedding Projects where Event Date = Today + 30 days, calculates T-30 payment amount,
/// issues the T-30 payment milestone with Status = Due, notifies Customer &amp; Manager, and queues emails.
/// </summary>
public sealed class IssueT30PaymentRequestJob(
    IT30PaymentRequestService paymentRequestService,
    ILogger<IssueT30PaymentRequestJob> logger)
{
    [DisableConcurrentExecution(300)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Triggering JOB-02: Issue T-30 Payment Request.");

        var result = await paymentRequestService.IssueT30PaymentRequestsAsync(
            new IssueT30PaymentRequestCommand(),
            cancellationToken);

        logger.LogInformation(
            "Finished JOB-02: ExecutionId={ExecutionId}, Checked={Checked}, Issued={Issued}, Emails={Emails}, Skipped={Skipped}, Failed={Failed}.",
            result.ExecutionId,
            result.TotalProjectsChecked,
            result.TotalRequestsIssued,
            result.TotalEmailsQueued,
            result.TotalSkipped,
            result.TotalFailed);
    }
}
