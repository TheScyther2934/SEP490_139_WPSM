namespace Application.Persistence;

public interface IConfigurationRepository
{
    Task<IReadOnlyList<CatalogItemView>> GetCatalogAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceFrameView>> GetPriceFramesAsync(CancellationToken cancellationToken = default);
    Task<PolicyView> GetPoliciesAsync(CancellationToken cancellationToken = default);
}

public sealed record PriceFrameHistoryView(int PriceFrameId, decimal MinPrice, decimal MaxPrice, DateOnly EffectiveFrom, string UpdatedBy, DateTime UpdatedAt);

// UC-37 edit/deactivate, UC-38 history (Admin catalog and PriceFrames).
public interface IConfigurationAdminRepository
{
    Task<IReadOnlyList<PriceFrameHistoryView>> GetPriceFrameHistoryAsync(int itemId, CancellationToken cancellationToken = default);
}
