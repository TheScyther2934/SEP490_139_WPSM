namespace Application.Persistence;

public sealed record AddendumItemView(string ItemName, int OldQuantity, decimal OldUnitPrice, int NewQuantity, decimal NewUnitPrice, decimal DeltaAmount);
public sealed record AddendumView(string Code, string CrCode, string Status, decimal TotalVariance, DateTime? SignedAt, Guid? FileId, IReadOnlyList<AddendumItemView> Items);

public sealed record CrCostLineView(
    int CrItemId,
    int SpecItemId,
    string ItemName,
    string Category,
    string Unit,
    int OldQuantity,
    decimal OldUnitPrice,
    int NewQuantity,
    decimal NewUnitPrice,
    decimal DeltaAmount);

public sealed record CrHistoryEntryView(
    int HistoryId,
    string ActorName,
    string ActorRole,
    string Action,
    string? Note,
    DateTime CreatedAt);

public sealed record CrCostImpactView(
    int CrId,
    string CrCode,
    string ProjectCode,
    string CoupleName,
    string Type,
    string Description,
    string Status,
    decimal CostImpact,
    decimal PenaltyRate,
    decimal PenaltyFee,
    decimal TotalVariance,
    decimal BaseContractValue,
    decimal CurrentProjectTotal,
    decimal ProjectedProjectTotal,
    decimal PaidAmount,
    decimal OutstandingBalance,
    decimal ProjectedBalance,
    int DaysUntilEvent,
    bool ImpactsBeo,
    string BeoStatus,
    DateTime? CustomerApprovedAt,
    DateTime? ManagerApprovedAt,
    DateTime CreatedAt,
    AddendumView? Addendum,
    IReadOnlyList<CrCostLineView> Items,
    IReadOnlyList<CrHistoryEntryView> History);

public sealed record AddendumCrSpecOptionView(
    int SpecItemId,
    int CatalogItemId,
    string ItemName,
    string Category,
    string Unit,
    string Description,
    int CurrentQuantity,
    decimal CurrentUnitPrice,
    decimal CurrentLineTotal);

public sealed record CrCreationContextView(
    string ProjectCode,
    string CoupleName,
    string HallName,
    DateOnly EventDate,
    int DaysUntilEvent,
    decimal PenaltyRate,
    decimal BaseContractValue,
    decimal CurrentProjectTotal,
    IReadOnlyList<AddendumCrSpecOptionView> SpecItems);

public sealed record CreateCrItemInput(
    int TargetSpecItemId,
    int NewQuantity,
    decimal NewUnitPrice);

public interface IAddendumRepository
{
    /// <summary>UC-11 (C-09). Returns null when the user has no access to the project (GBR-12).</summary>
    Task<IReadOnlyList<AddendumView>?> GetAddendaAsync(string? projectCode, int userId, string? role, CancellationToken cancellationToken = default);

    /// <summary>Lists change requests with cost impact summary for a project (Customer or Manager).</summary>
    Task<IReadOnlyList<CrCostImpactView>?> GetProjectChangeRequestsAsync(string? projectCode, int userId, string? role, CancellationToken cancellationToken = default);

    /// <summary>UC-08 (C-06). Returns active specification items and T-day penalty rate for creating a new change request.</summary>
    Task<CrCreationContextView?> GetCrCreationContextAsync(string? projectCode, int customerId, CancellationToken cancellationToken = default);

    /// <summary>UC-08 (C-06). Customer creates a new change request against baseline/active specification items.</summary>
    Task<CrCostImpactView> CreateCustomerChangeRequestAsync(int customerId, string? projectCode, string type, string description, IReadOnlyList<CreateCrItemInput> items, CancellationToken cancellationToken = default);

    /// <summary>UC-09, UC-23 (C-07, M-09). Returns full cost impact and negotiation history for a change request.</summary>
    Task<CrCostImpactView?> GetCostImpactAsync(string? projectCode, string crCode, int userId, string? role, CancellationToken cancellationToken = default);

    /// <summary>UC-10 (C-08). Customer confirms cost impact quote, rejects, or requests adjustment on a change request.</summary>
    Task<CrCostImpactView?> DecideCustomerCrAsync(string crCode, int customerId, string action, string? note, CancellationToken cancellationToken = default);

    /// <summary>UC-22 (M-08). Manager reviews a Draft CR and sends the official cost impact quote to Customer for confirmation.</summary>
    Task<CrCostImpactView?> SendCostImpactQuoteAsync(string projectCode, string crCode, int managerId, string? note, CancellationToken cancellationToken = default);

    /// <summary>UC-23, UC-24 (M-09). Manager approves cost impact, generates contract addendum, updates project balance and flags Frozen BEO as Stale in a single transaction.</summary>
    Task<CrCostImpactView?> GenerateAddendumAsync(string projectCode, string crCode, int managerId, Guid? signedFileId, string? note, CancellationToken cancellationToken = default);

    /// <summary>UC-24 (M-09). Uploads/attaches the signed addendum file and marks the addendum as Signed.</summary>
    Task<CrCostImpactView?> AttachSignedAddendumAsync(string projectCode, string crCode, int managerId, Guid fileId, CancellationToken cancellationToken = default);
}
