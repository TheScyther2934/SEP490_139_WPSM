namespace WPSM.Application.Persistence;

public sealed record AddendumItemView(string ItemName, int OldQuantity, decimal OldUnitPrice, int NewQuantity, decimal NewUnitPrice, decimal DeltaAmount);
public sealed record AddendumView(string Code, string CrCode, string Status, decimal TotalVariance, DateTime? SignedAt, Guid? FileId, IReadOnlyList<AddendumItemView> Items);

public interface IAddendumRepository
{
    /// <summary>UC-11. Returns null when the user has no access to the project (GBR-12).</summary>
    Task<IReadOnlyList<AddendumView>?> GetAddendaAsync(string? projectCode, int userId, string? role, CancellationToken cancellationToken = default);
}
