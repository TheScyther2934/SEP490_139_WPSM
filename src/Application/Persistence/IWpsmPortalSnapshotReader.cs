namespace WPSM.Application.Persistence;

public interface IWpsmPortalSnapshotReader
{
    Task<WpsmPortalSnapshot> GetSnapshotAsync(int? userId, string? projectCode = null, CancellationToken cancellationToken = default);
}
