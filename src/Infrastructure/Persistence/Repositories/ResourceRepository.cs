using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Entities;
using Domain.Rules;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads of UC-25, UC-26 (calendar, one allocation, its conflicts). Allocating, rescheduling, releasing and asking for an exception
/// are use cases (Application/UseCases/Resources); the capacity rule is ResourceAllocationPolicy (GBR-09).
/// </summary>
public sealed class ResourceRepository(WpsmDbContext db) : IResourceRepository
{
    public async Task<OpResourceCalendarView> GetCalendarAsync(int managerId, DateOnly from, DateOnly to, int? hallId, string? type, CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 62) to = from.AddDays(62);
        var resources = await db.Resources.AsNoTracking().Where(x => type == null || type == "" || x.Type == type).OrderBy(x => x.Type).ThenBy(x => x.Name)
            .Select(x => new ResourceView(x.ResourceId, x.Name, x.Type, x.TotalQuantity)).ToListAsync(cancellationToken);
        var bookings = await db.WeddingProjects.AsNoTracking().Where(x => x.EventDate >= from && x.EventDate <= to && x.Status != "Closed" && (hallId == null || x.HallId == hallId))
            .OrderBy(x => x.EventDate).ThenBy(x => x.Slot.StartTime)
            .Select(x => new HallBookingView(x.ProjectCode, x.Hall.Name, x.Slot.SlotName, x.EventDate, x.Status, (x.ManagerId == managerId || x.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null))))
            .ToListAsync(cancellationToken);
        var start = VnToUtc(from, TimeOnly.MinValue);
        var end = VnToUtc(to.AddDays(1), TimeOnly.MinValue);
        var resourceIds = resources.Select(x => x.ResourceId).ToArray();
        var allocations = await Project(db.ResourceAllocations.AsNoTracking()
            .Where(x => x.StartTime < end && x.EndTime > start && x.Status != "Released" && resourceIds.Contains(x.ResourceId) && (hallId == null || x.HallId == hallId))
            .OrderBy(x => x.StartTime), managerId).ToListAsync(cancellationToken);
        return new OpResourceCalendarView(resources, bookings, allocations);
    }

    public async Task<AllocationResult?> GetConflictAsync(int managerId, int allocationId, CancellationToken cancellationToken = default)
    {
        var view = await GetAllocationAsync(managerId, allocationId, cancellationToken);
        if (view is null) return null;
        var resource = await db.Resources.AsNoTracking().FirstAsync(x => x.ResourceId == view.ResourceId, cancellationToken);
        var conflicts = await ConflictsAsync(resource, new AllocationCommand(view.ResourceId, view.ProjectCode, view.StartTime, view.EndTime, view.Quantity), allocationId, cancellationToken);
        return new AllocationResult(view, conflicts.Count == 0 ? "Không còn xung đột." : null, ToView(resource), conflicts);
    }

    private async Task<List<ConflictView>> ConflictsAsync(Resource resource, AllocationCommand command, int? excludeId, CancellationToken cancellationToken)
    {
        var start = command.StartTime.ToUniversalTime();
        var end = command.EndTime.ToUniversalTime();
        var overlapping = await db.ResourceAllocations.AsNoTracking()
            .Where(x => x.ResourceId == resource.ResourceId && x.AllocationId != excludeId && x.StartTime < end && x.EndTime > start
                && (x.Status == "Reserved" || x.Status == "Exception Approved")
                && !db.ExceptionRequests.Any(e => e.AllocationId == x.AllocationId && e.Status != "Approved"))
            .Select(x => new ConflictView(x.Project.ProjectCode, x.StartTime, x.EndTime, x.AllocatedQuantity)).ToListAsync(cancellationToken);
        return ResourceAllocationPolicy.Shortage(resource.TotalQuantity, overlapping.Sum(x => x.Quantity), command.Quantity) > 0
            ? overlapping.Select(x => x with { StartTime = Utc(x.StartTime), EndTime = Utc(x.EndTime) }).ToList()
            : [];
    }

    private static ResourceView ToView(Resource x) => new(x.ResourceId, x.Name, x.Type, x.TotalQuantity);

    public Task<AllocationView?> GetAllocationAsync(int managerId, int allocationId, CancellationToken cancellationToken = default) =>
        Project(db.ResourceAllocations.AsNoTracking().Where(x => x.AllocationId == allocationId && (x.Project.ManagerId == managerId || x.Project.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null))), managerId)
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<AllocationView> Project(IQueryable<ResourceAllocation> query, int managerId) =>
        query.Select(x => new AllocationView(x.AllocationId, x.ResourceId, x.Resource.Name, x.Resource.Type,
            x.Project.ProjectCode, x.Hall.Name, DateTime.SpecifyKind(x.StartTime, DateTimeKind.Utc), DateTime.SpecifyKind(x.EndTime, DateTimeKind.Utc),
            x.AllocatedQuantity, x.Status,
            db.ExceptionRequests.Where(e => e.AllocationId == x.AllocationId).OrderByDescending(e => e.CreatedAt).Select(e => e.Status).FirstOrDefault(),
            (x.Project.ManagerId == managerId || x.Project.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null))));
}
