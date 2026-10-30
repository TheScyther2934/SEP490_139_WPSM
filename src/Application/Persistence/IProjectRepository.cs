namespace WPSM.Application.Persistence;

public interface IProjectRepository
{
    Task<IReadOnlyList<WeddingProjectView>> GetProjectsAsync(int? userId = null, string? role = null, CancellationToken cancellationToken = default);
    Task<WeddingProjectView?> GetProjectAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<bool> ResendCredentialsAsync(string projectCode, int managerId, string customerPasswordHash, string temporaryPassword, CancellationToken cancellationToken = default);
    Task<CreatedProjectView> CreateProjectAsync(CreateProjectCommand command, int managerId, string customerPasswordHash, string temporaryPassword, CancellationToken cancellationToken = default);
    Task<bool> RecordNrdReceiptAsync(string projectCode, int managerId, string receiptPath, CancellationToken cancellationToken = default);
}
