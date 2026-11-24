namespace Application.Persistence;

public interface IProjectRepository
{
    Task<bool> CanAccessAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeddingProjectView>> GetProjectsAsync(int? userId = null, string? role = null, CancellationToken cancellationToken = default);
    Task<WeddingProjectView?> GetProjectAsync(string projectCode, CancellationToken cancellationToken = default);
    /// <summary>Active halls with their slots; a slot is unavailable on <paramref name="date"/> when an event (not cancelled) holds it.</summary>
    Task<IReadOnlyList<HallView>> GetHallsAsync(DateOnly? date, CancellationToken cancellationToken = default);
    /// <summary>Where the deposit receipt of the event is stored; null when there is none.</summary>
    Task<StoredFileRef?> GetDepositReceiptAsync(string projectCode, CancellationToken cancellationToken = default);
}
