namespace Application.Persistence;

/// <summary>
/// Provides read-only dashboard data for the authenticated Customer (C-01).
/// All methods validate project ownership server-side.
/// </summary>
public interface ICustomerDashboardRepository
{
    /// <summary>
    /// Returns the dashboard view for a specific project owned by <paramref name="customerId"/>.
    /// Returns <c>null</c> when the project does not exist or does not belong to the customer.
    /// </summary>
    Task<CustomerDashboardView?> GetDashboardAsync(
        int customerId,
        string projectCode,
        CancellationToken cancellationToken = default);
}
