namespace Application.Payments;

/// <summary>
/// Command to trigger JOB-02: Issue T-30 Payment Request.
/// Allows specifying an optional TargetDate for testing or on-demand execution.
/// </summary>
public sealed record IssueT30PaymentRequestCommand(DateOnly? TargetDate = null);

/// <summary>
/// Execution metrics and audit summary for JOB-02.
/// </summary>
public sealed record T30PaymentRequestResult(
    Guid ExecutionId,
    DateTime StartedAt,
    DateTime CompletedAt,
    int TotalProjectsChecked,
    int TotalRequestsIssued,
    int TotalEmailsQueued,
    int TotalSkipped,
    int TotalFailed,
    IReadOnlyList<string> Errors);

/// <summary>
/// Service responsible for identifying projects at Event Date - 30 days,
/// evaluating contract finances and change requests, issuing T-30 payment milestone,
/// creating notifications, and queuing outbox emails.
/// </summary>
public interface IT30PaymentRequestService
{
    Task<T30PaymentRequestResult> IssueT30PaymentRequestsAsync(
        IssueT30PaymentRequestCommand? command = null,
        CancellationToken cancellationToken = default);
}
