using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Application.UseCases.Resources;
using Domain.Entities;
using Domain.Rules;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;

namespace Infrastructure.UseCases;

/// <summary>Resource allocation data for the use cases: scoped lookups, the per-resource lock and the overlap query (SDS III.2.2).</summary>
public sealed class ResourceAllocationStore(WpsmDbContext db) : IResourceAllocationStore
{
    public Task<WeddingProject?> GetEventAsync(int managerId, string projectCode, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == projectCode
            && (x.ManagerId == managerId || x.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)), cancellationToken);

    public Task<Resource?> GetResourceAsync(int resourceId, CancellationToken cancellationToken = default) =>
        db.Resources.AsNoTracking().FirstOrDefaultAsync(x => x.ResourceId == resourceId, cancellationToken);

    public Task<ResourceAllocation?> GetAllocationAsync(int managerId, int allocationId, CancellationToken cancellationToken = default) =>
        db.ResourceAllocations.Include(x => x.Project).Include(x => x.Resource)
            .FirstOrDefaultAsync(x => x.AllocationId == allocationId
                && (x.Project.ManagerId == managerId || x.Project.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)), cancellationToken);

    public Task<bool> HasPendingExceptionAsync(int allocationId, CancellationToken cancellationToken = default) =>
        db.ExceptionRequests.AnyAsync(x => x.AllocationId == allocationId && x.Status == ExceptionRequest.Pending, cancellationToken);

    // A transaction-scoped advisory lock. Key (4201, resourceId): 4201 marks resource locks; waits at most 10 s.
    // The in-memory test database has no SQL, so there is nothing to lock there.
    public async Task LockResourceAsync(int resourceId, CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsRelational()) return;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('lock_timeout', '10s', true), pg_advisory_xact_lock(4201, {resourceId})", cancellationToken);
    }

    public async Task<IReadOnlyList<ConflictView>> GetOverlappingAsync(int resourceId, DateTime startUtc, DateTime endUtc, int? excludeAllocationId,
        CancellationToken cancellationToken = default)
    {
        var overlapping = await db.ResourceAllocations.AsNoTracking()
            .Where(x => x.ResourceId == resourceId && x.AllocationId != excludeAllocationId && x.StartTime < endUtc && x.EndTime > startUtc
                && (x.Status == ResourceAllocationPolicy.Reserved || x.Status == ResourceAllocationPolicy.ExceptionApproved)
                && !db.ExceptionRequests.Any(e => e.AllocationId == x.AllocationId && e.Status != ExceptionRequest.Approved))
            .OrderBy(x => x.StartTime)
            .Select(x => new ConflictView(x.Project.ProjectCode, x.StartTime, x.EndTime, x.AllocatedQuantity))
            .ToListAsync(cancellationToken);
        return overlapping.Select(x => x with { StartTime = OperationsSupport.Utc(x.StartTime), EndTime = OperationsSupport.Utc(x.EndTime) }).ToList();
    }

    public Task<string> NextExceptionCodeAsync(CancellationToken cancellationToken = default) =>
        OperationsSupport.NextCodeAsync(db.ExceptionRequests.Select(x => x.RequestCode), "EXC", cancellationToken);

    public Task<ExceptionRequest?> GetExceptionRequestAsync(int requestId, CancellationToken cancellationToken = default) =>
        db.ExceptionRequests.Include(x => x.Allocation).Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.ExceptionRequestId == requestId, cancellationToken);

    public void Add(ResourceAllocation allocation) => db.ResourceAllocations.Add(allocation);

    public void Add(ExceptionRequest request) => db.ExceptionRequests.Add(request);
}
