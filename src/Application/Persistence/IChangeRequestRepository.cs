namespace Application.Persistence;

// ── FE-05 Change Request (C-05, C-06, M-07, M-08) ────────────────────────────
// Shared shapes for the Customer and Manager CR screens. The cost figures always come from the
// server so GBR-03 (price delta, T-day penalty, total CR impact) is calculated in one place.

public sealed record PenaltyRuleView(int TDayMin, int TDayMax, decimal RatePercent);

/// <summary>GBR-03 cost impact. <paramref name="PenaltyRate"/> is 0 when no T-day rule applies.</summary>
public sealed record CrCostView(
    decimal PriceDelta,
    decimal PenaltyRate,
    decimal PenaltyFee,
    decimal TotalImpact,
    decimal CurrentTotal,
    decimal ProjectedTotal,
    int DaysToEvent,
    bool InPenaltyWindow,
    string PenaltyWindow);

public sealed record CrSpecOptionView(
    int SpecItemId,
    string Name,
    string Category,
    string Unit,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal MinPrice,
    decimal MaxPrice,
    bool HasPriceFrame);

/// <summary>Data C-06 needs before a ticket exists: baseline items, PriceFrames and the active penalty rule.</summary>
public sealed record CrFormView(
    string ProjectCode,
    string CoupleName,
    string Hall,
    string Slot,
    DateOnly EventDate,
    int DaysToEvent,
    decimal BaseContractValue,
    decimal CurrentTotal,
    IReadOnlyList<CrSpecOptionView> SpecItems,
    IReadOnlyList<PenaltyRuleView> PenaltyRules,
    decimal ApplicablePenaltyRate,
    string PenaltyWindow,
    string? BlockedReason);

public sealed record CrItemView(
    int SpecItemId,
    string ItemName,
    string Category,
    string Unit,
    string Description,
    int OldQuantity,
    decimal OldUnitPrice,
    int NewQuantity,
    decimal NewUnitPrice,
    decimal DeltaAmount,
    decimal MinPrice,
    decimal MaxPrice,
    bool HasPriceFrame);

public sealed record CrHistoryView(DateTime At, string Action, string ActorName, string ActorRole, string? Note);

public sealed record CrSummaryView(
    int Total,
    int Draft,
    int Negotiating,
    int Approved,
    int Rejected,
    decimal ApprovedImpact,
    decimal PendingImpact,
    decimal PendingPenalty);

public sealed record CrListItemView(
    string Code,
    string Type,
    string Status,
    string Description,
    decimal PriceDelta,
    decimal PenaltyFee,
    decimal TotalImpact,
    string CreatedByRole,
    string CreatedByName,
    DateTime CreatedAt,
    int ItemCount,
    bool CustomerApproved,
    bool ManagerApproved,
    string? PendingException);

public sealed record CrListView(
    string ProjectCode,
    string CoupleName,
    string Hall,
    string Slot,
    DateOnly EventDate,
    decimal BaseContractValue,
    decimal CurrentTotal,
    CrSummaryView Summary,
    IReadOnlyList<CrListItemView> Items);

public sealed record CrDetailView(
    string Code,
    string ProjectCode,
    string CoupleName,
    string Hall,
    string Slot,
    DateOnly EventDate,
    string Type,
    string Description,
    string Status,
    string CreatedByRole,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? CustomerApprovedAt,
    DateTime? ManagerApprovedAt,
    CrCostView Cost,
    IReadOnlyList<CrItemView> Items,
    IReadOnlyList<CrHistoryView> History,
    IReadOnlyList<PenaltyRuleView> PenaltyRules,
    string? AddendumCode,
    string? BeoStatus,
    string? PendingException,
    bool CanEdit,
    bool CanApprove,
    bool CanReject,
    bool CanRequestException);

public sealed record CrLineCommand(int SpecItemId, int NewQuantity, decimal NewUnitPrice);

/// <summary>C-06 payload. <paramref name="Submit"/> false keeps the ticket Draft (UC-07 alternative flow).</summary>
public sealed record CrSaveCommand(string Type, string Description, IReadOnlyList<CrLineCommand> Items, bool Submit = false);

/// <summary>M-08 counter-proposal: the Manager re-states the new values and leaves a note for the Customer.</summary>
public sealed record CrCounterCommand(IReadOnlyList<CrLineCommand> Items, string? Note = null);

/// <summary>
/// CR tickets for FE-05. Every method re-checks project ownership server-side (GBR-12) and returns
/// <c>null</c> when the project/CR is outside the caller's scope; rule violations throw
/// <see cref="InvalidOperationException"/> and surface as HTTP 400 with the SRS message code.
/// A <c>null</c> project code resolves the Customer's own project.
/// </summary>
public interface IChangeRequestRepository
{
    Task<CrListView?> GetListAsync(string? projectCode, int userId, string role, CancellationToken cancellationToken = default);

    Task<CrFormView?> GetFormAsync(string? projectCode, int userId, string role, CancellationToken cancellationToken = default);

    Task<CrDetailView?> GetDetailAsync(string? projectCode, string crCode, int userId, string role, CancellationToken cancellationToken = default);

    Task<CrDetailView?> CreateAsync(string? projectCode, int userId, string role, CrSaveCommand command, CancellationToken cancellationToken = default);

    /// <summary>Re-saves a Draft (customer) or the Manager's own draft, optionally submitting it (UC-07).</summary>
    Task<CrDetailView?> UpdateDraftAsync(string? projectCode, string crCode, int userId, string role, CrSaveCommand command, CancellationToken cancellationToken = default);

    /// <summary>Draft → Negotiating and notifies the counterparty (MSG19).</summary>
    Task<CrDetailView?> SubmitAsync(string? projectCode, string crCode, int userId, string role, CancellationToken cancellationToken = default);

    /// <summary>M-08 counter-proposal; recalculates GBR-03 and resets both approvals.</summary>
    Task<CrDetailView?> CounterAsync(string? projectCode, string crCode, int userId, CrCounterCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records one party's approval. When both approved the CR becomes Approved, the addendum is
    /// generated, the project total is recalculated and the BEO is flagged Stale (GBR-04, UC-22).
    /// </summary>
    Task<CrDetailView?> ApproveAsync(string? projectCode, string crCode, int userId, string role, string? note, CancellationToken cancellationToken = default);

    /// <summary>Closes the CR with the mandatory reason; the audit trail is kept (GBR-13).</summary>
    Task<CrDetailView?> RejectAsync(string? projectCode, string crCode, int userId, string role, string note, CancellationToken cancellationToken = default);

    /// <summary>UC-47 / A-18: asks the Admin to waive the T-day penalty of this CR.</summary>
    Task<CrDetailView?> RequestExceptionAsync(string? projectCode, string crCode, int userId, string note, CancellationToken cancellationToken = default);
}
