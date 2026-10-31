namespace WPSM.Application.Persistence;

public interface IConfigurationRepository
{
    Task<IReadOnlyList<CatalogItemView>> GetCatalogAsync(CancellationToken cancellationToken = default);
    Task<CatalogItemView> AddCatalogItemAsync(CatalogItemCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceFrameView>> GetPriceFramesAsync(CancellationToken cancellationToken = default);
    Task<PriceFrameView?> UpdatePriceFrameAsync(int priceFrameId, PriceFrameCommand command, int updatedBy, CancellationToken cancellationToken = default);
    Task<PolicyView> GetPoliciesAsync(CancellationToken cancellationToken = default);
    Task<PolicyView> UpdatePoliciesAsync(PolicyCommand command, int updatedBy, CancellationToken cancellationToken = default);
}
