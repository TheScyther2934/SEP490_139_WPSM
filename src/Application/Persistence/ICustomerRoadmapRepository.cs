namespace Application.Persistence;

/// <summary>
/// Read / write operations on Customer Roadmap for C-03 (UC-05, UC-06).
/// All methods verify project ownership server-side.
/// </summary>
public interface ICustomerRoadmapRepository
{
    /// <summary>
    /// Returns the full roadmap grouped by milestone for a Customer-owned project.
    /// Returns <c>null</c> when project does not exist or does not belong to <paramref name="customerId"/>.
    /// </summary>
    Task<CustomerRoadmapView?> GetRoadmapAsync(
        int customerId,
        string projectCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a Customer-owned task as Done (UC-06).
    /// Returns failure result if task belongs to Manager, is already Done, or project ownership fails.
    /// </summary>
    Task<CompleteTaskResult> CompleteTaskAsync(
        int customerId,
        string projectCode,
        int taskId,
        CancellationToken cancellationToken = default);
}
