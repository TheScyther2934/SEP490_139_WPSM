namespace Application.Payments;

/// <summary>
/// Command to trigger JOB-06: Calculate Final Settlement.
/// Allows specifying an optional TargetDate for testing or on-demand execution.
/// </summary>
public sealed record PrepareFinalSettlementCommand(DateOnly? TargetDate = null);

/// <summary>
/// Execution metrics and audit log for JOB-06.
/// </summary>
public sealed record FinalSettlementResult(
    Guid ExecutionId,
    DateTime StartedAt,
    DateTime CompletedAt,
    int TotalProjectsFound,
    int TotalEventsCompleted,
    int TotalSettlementsPrepared,
    int TotalFinalMilestonesCreated,
    int TotalEmailsQueued,
    int TotalNotificationsCreated,
    int TotalProjectsSkipped,
    int TotalFailed,
    IReadOnlyList<string> Errors);

/// <summary>
/// Service responsible for identifying Wedding Projects held yesterday,
/// marking Event Completed, calculating final settlement amount,
/// creating or updating the FINAL payment milestone with Status = Due,
/// and notifying the assigned Manager for verification.
/// </summary>
public interface IFinalSettlementService
{
    Task<FinalSettlementResult> PrepareFinalSettlementsAsync(
        PrepareFinalSettlementCommand? command = null,
        CancellationToken cancellationToken = default);
}
