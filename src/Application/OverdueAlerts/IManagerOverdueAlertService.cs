using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Persistence;

namespace Application.OverdueAlerts;

/// <summary>
/// M-23: Overdue Alerts for Manager (UC-32, GBR-12).
/// Scoped strictly to projects assigned to the authenticated manager.
/// </summary>
public interface IManagerOverdueAlertService
{
    Task<IReadOnlyList<OverdueAlertView>> GetManagerOverdueAlertsAsync(
        int managerId,
        CancellationToken cancellationToken = default);
}
