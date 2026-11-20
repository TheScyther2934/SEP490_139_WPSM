namespace Application.Persistence;

public interface IProjectRepository
{
    Task<bool> CanAccessAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeddingProjectView>> GetProjectsAsync(int? userId = null, string? role = null, CancellationToken cancellationToken = default);
    Task<WeddingProjectView?> GetProjectAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<bool> ResendCredentialsAsync(string projectCode, int managerId, string customerPasswordHash, string temporaryPassword, CancellationToken cancellationToken = default);
    Task<CreatedProjectView> CreateProjectAsync(CreateProjectCommand command, int managerId, string customerPasswordHash, string temporaryPassword, CancellationToken cancellationToken = default);
    /// <summary>Marks the deposit paid by bank transfer with its receipt (already stored through IFileService).</summary>
    Task<bool> RecordDepositReceiptAsync(string projectCode, int managerId, StoredFileView receipt, CancellationToken cancellationToken = default);
}
