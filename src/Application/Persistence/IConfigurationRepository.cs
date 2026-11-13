namespace Application.Persistence;

public interface IConfigurationRepository
{
    Task<IReadOnlyList<CatalogItemView>> GetCatalogAsync(CancellationToken cancellationToken = default);
    Task<CatalogItemView> AddCatalogItemAsync(CatalogItemCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceFrameView>> GetPriceFramesAsync(CancellationToken cancellationToken = default);
    Task<PriceFrameView?> UpdatePriceFrameAsync(int priceFrameId, PriceFrameCommand command, int updatedBy, CancellationToken cancellationToken = default);
    Task<PolicyView> GetPoliciesAsync(CancellationToken cancellationToken = default);
    Task<PolicyView> UpdatePoliciesAsync(PolicyCommand command, int updatedBy, CancellationToken cancellationToken = default);
}

public sealed record PriceFrameHistoryView(int PriceFrameId, decimal MinPrice, decimal MaxPrice, DateOnly EffectiveFrom, string UpdatedBy, DateTime UpdatedAt);
public sealed record PenaltyRuleView(int TDayMin, int TDayMax, decimal RatePercent, DateOnly EffectiveFrom);
public sealed record PenaltyRuleCommand(int TDayMin, int TDayMax, decimal RatePercent);

// UC-37 edit/deactivate, UC-38 history, UC-40 T-day ranges (Report 4 IV.2 Admin catalog, PriceFrames, policies).
public interface IConfigurationAdminRepository
{
    Task<CatalogItemView?> UpdateCatalogItemAsync(int itemId, CatalogItemCommand command, CancellationToken cancellationToken = default);
    Task<CatalogItemView?> SetCatalogStatusAsync(int itemId, bool active, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceFrameHistoryView>> GetPriceFrameHistoryAsync(int itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PenaltyRuleView>> GetPenaltyRulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PenaltyRuleView>> UpdatePenaltyRulesAsync(IReadOnlyList<PenaltyRuleCommand> rules, DateOnly? effectiveFrom, int updatedBy, CancellationToken cancellationToken = default);
}
