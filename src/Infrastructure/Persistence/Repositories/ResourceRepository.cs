using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>UC-25, UC-26 and the resource branch of UC-47 (GBR-09, SDS III.2.2 overlap query).</summary>
public sealed class ResourceRepository(WpsmDbContext db) : IResourceRepository
{
    public async Task<ResourceCalendarView> GetCalendarAsync(int managerId, DateOnly from, DateOnly to, int? hallId, string? type, CancellationToken cancellationToken = default)
    {
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 62) to = from.AddDays(62);
        var resources = await db.Resources.AsNoTracking().Where(x => type == null || type == "" || x.Type == type).OrderBy(x => x.Type).ThenBy(x => x.Name)
            .Select(x => new ResourceView(x.ResourceId, x.Name, x.Type, x.TotalQuantity)).ToListAsync(cancellationToken);
        var bookings = await db.WeddingProjects.AsNoTracking().Where(x => x.EventDate >= from && x.EventDate <= to && x.Status != "Closed" && (hallId == null || x.HallId == hallId))
            .OrderBy(x => x.EventDate).ThenBy(x => x.Slot.StartTime)
            .Select(x => new HallBookingView(x.ProjectCode, x.Hall.Name, x.Slot.SlotName, x.EventDate, x.Status, x.ManagerId == managerId))
            .ToListAsync(cancellationToken);
        var start = VnToUtc(from, TimeOnly.MinValue);
        var end = VnToUtc(to.AddDays(1), TimeOnly.MinValue);
        var resourceIds = resources.Select(x => x.ResourceId).ToArray();
        var allocations = await Project(db.ResourceAllocations.AsNoTracking()
            .Where(x => x.StartTime < end && x.EndTime > start && x.Status != "Released" && resourceIds.Contains(x.ResourceId) && (hallId == null || x.HallId == hallId))
            .OrderBy(x => x.StartTime), managerId).ToListAsync(cancellationToken);
        return new ResourceCalendarView(resources, bookings, allocations);
    }

    public async Task<AllocationResult?> AllocateAsync(int managerId, AllocationCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var (project, resource) = await ValidateAsync(managerId, command, cancellationToken);
        if (project is null) return null;
        await LockAsync(resource.ResourceId, cancellationToken);
        var conflicts = await ConflictsAsync(resource, command, null, cancellationToken);
        if (conflicts.Count > 0) return Conflict(resource, conflicts);
        var allocation = new ResourceAllocation
        {
            ResourceId = resource.ResourceId, HallId = project.HallId, ProjectId = project.ProjectId, StartTime = command.StartTime.ToUniversalTime(),
            EndTime = command.EndTime.ToUniversalTime(), AllocatedQuantity = command.Quantity, Status = "Reserved"
        };
        db.ResourceAllocations.Add(allocation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AllocationResult(await FindAsync(managerId, allocation.AllocationId, cancellationToken), "Đã phân bổ tài nguyên.", null, []);
    }

    public async Task<AllocationResult?> UpdateAsync(int managerId, int allocationId, AllocationCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var allocation = await db.ResourceAllocations.Include(x => x.Project).FirstOrDefaultAsync(x => x.AllocationId == allocationId && x.Project.ManagerId == managerId, cancellationToken);
        if (allocation is null) return null;
        if (allocation.Status == "Released") throw new InvalidOperationException("Phân bổ đã được giải phóng.");
        if (await HasOpenExceptionAsync(allocationId, cancellationToken)) throw new InvalidOperationException("Phân bổ đang chờ Admin quyết định ngoại lệ.");
        var (project, resource) = await ValidateAsync(managerId, command with { ProjectCode = allocation.Project.ProjectCode }, cancellationToken);
        if (project is null) return null;
        await LockAsync(resource.ResourceId, cancellationToken);
        // Reschedule or substitute (UC-26): the system re-checks before saving.
        var conflicts = await ConflictsAsync(resource, command, allocationId, cancellationToken);
        if (conflicts.Count > 0) return Conflict(resource, conflicts);
        allocation.ResourceId = resource.ResourceId;
        allocation.StartTime = command.StartTime.ToUniversalTime();
        allocation.EndTime = command.EndTime.ToUniversalTime();
        allocation.AllocatedQuantity = command.Quantity;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AllocationResult(await FindAsync(managerId, allocationId, cancellationToken), "Đã cập nhật phân bổ.", null, []);
    }

    public async Task<bool> ReleaseAsync(int managerId, int allocationId, CancellationToken cancellationToken = default)
    {
        var allocation = await db.ResourceAllocations.FirstOrDefaultAsync(x => x.AllocationId == allocationId && x.Project.ManagerId == managerId, cancellationToken);
        if (allocation is null) return false;
        allocation.Status = "Released";
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AllocationResult?> GetConflictAsync(int managerId, int allocationId, CancellationToken cancellationToken = default)
    {
        var view = await FindAsync(managerId, allocationId, cancellationToken);
        if (view is null) return null;
        var resource = await db.Resources.AsNoTracking().FirstAsync(x => x.ResourceId == view.ResourceId, cancellationToken);
        var conflicts = await ConflictsAsync(resource, new AllocationCommand(view.ResourceId, view.ProjectCode, view.StartTime, view.EndTime, view.Quantity), allocationId, cancellationToken);
        return new AllocationResult(view, conflicts.Count == 0 ? "Không còn xung đột." : null, ToView(resource), conflicts);
    }

    public async Task<AllocationResult?> RequestExceptionAsync(int managerId, AllocationCommand command, string note, CancellationToken cancellationToken = default)
    {
        note = note?.Trim() ?? "";
        if (note.Length == 0) throw new InvalidOperationException("Trường Ghi chú là bắt buộc.");
        if (note.Length > 300) throw new InvalidOperationException("Vượt quá độ dài tối đa 300 ký tự.");
        var (project, resource) = await ValidateAsync(managerId, command, cancellationToken);
        if (project is null) return null;
        var conflicts = await ConflictsAsync(resource, command, null, cancellationToken);
        if (conflicts.Count == 0) throw new InvalidOperationException("Không có xung đột, hãy phân bổ trực tiếp.");
        // The row stays out of the conflict query while the request is Pending (SDS IV.1: PendingException ignored).
        var allocation = new ResourceAllocation
        {
            ResourceId = resource.ResourceId, HallId = project.HallId, ProjectId = project.ProjectId, StartTime = command.StartTime.ToUniversalTime(),
            EndTime = command.EndTime.ToUniversalTime(), AllocatedQuantity = command.Quantity, Status = "Reserved"
        };
        db.ResourceAllocations.Add(allocation);
        await db.SaveChangesAsync(cancellationToken);
        db.ExceptionRequests.Add(new ExceptionRequest
        {
            RequestCode = await NextCodeAsync(db.ExceptionRequests.Select(x => x.RequestCode), "EXC", cancellationToken),
            SourceType = "ResourceConflict", ProjectId = project.ProjectId, AllocationId = allocation.AllocationId, RequestedBy = managerId,
            RequestNote = note, Status = "Pending", CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return new AllocationResult(await FindAsync(managerId, allocation.AllocationId, cancellationToken), "Đã gửi yêu cầu ngoại lệ tới Admin.", ToView(resource), conflicts);
    }

    private async Task<(WeddingProject? Project, Resource Resource)> ValidateAsync(int managerId, AllocationCommand command, CancellationToken cancellationToken)
    {
        if (command.Quantity <= 0) throw new InvalidOperationException("Số lượng phải là số nguyên dương.");
        if (command.EndTime <= command.StartTime) throw new InvalidOperationException("Giờ kết thúc phải sau giờ bắt đầu.");
        var resource = await db.Resources.AsNoTracking().FirstOrDefaultAsync(x => x.ResourceId == command.ResourceId, cancellationToken)
            ?? throw new InvalidOperationException("Trường Tài nguyên là bắt buộc.");
        if (command.Quantity > resource.TotalQuantity) throw new InvalidOperationException($"Số lượng vượt tổng số {resource.TotalQuantity} của tài nguyên.");
        var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == command.ProjectCode && x.ManagerId == managerId, cancellationToken);
        if (project is not null && project.Status is "Settled" or "Closed") throw new InvalidOperationException("Dự án đã kết thúc.");
        return (project, resource);
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
        return overlapping.Sum(x => x.Quantity) + command.Quantity > resource.TotalQuantity
            ? overlapping.Select(x => x with { StartTime = Utc(x.StartTime), EndTime = Utc(x.EndTime) }).ToList()
            : [];
    }

    // sp_getapplock keeps two Managers from both passing the capacity check for the same resource (SDS III.2.2).
    private Task LockAsync(int resourceId, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"EXEC sp_getapplock @Resource = {"resource:" + resourceId}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000", cancellationToken);

    private Task<bool> HasOpenExceptionAsync(int allocationId, CancellationToken cancellationToken) =>
        db.ExceptionRequests.AnyAsync(x => x.AllocationId == allocationId && x.Status == "Pending", cancellationToken);

    private static AllocationResult Conflict(Resource resource, List<ConflictView> conflicts) =>
        new(null, $"Phát hiện xung đột với {conflicts[0].ProjectCode} lúc {VnTime(conflicts[0].StartTime)} (MSG08).", ToView(resource), conflicts);

    private static ResourceView ToView(Resource x) => new(x.ResourceId, x.Name, x.Type, x.TotalQuantity);

    private Task<AllocationView?> FindAsync(int managerId, int allocationId, CancellationToken cancellationToken) =>
        Project(db.ResourceAllocations.AsNoTracking().Where(x => x.AllocationId == allocationId && x.Project.ManagerId == managerId), managerId)
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<AllocationView> Project(IQueryable<ResourceAllocation> query, int managerId) =>
        query.Select(x => new AllocationView(x.AllocationId, x.ResourceId, x.Resource.Name, x.Resource.Type,
            x.Project.ProjectCode, x.Hall.Name, DateTime.SpecifyKind(x.StartTime, DateTimeKind.Utc), DateTime.SpecifyKind(x.EndTime, DateTimeKind.Utc),
            x.AllocatedQuantity, x.Status,
            db.ExceptionRequests.Where(e => e.AllocationId == x.AllocationId).OrderByDescending(e => e.CreatedAt).Select(e => e.Status).FirstOrDefault(),
            x.Project.ManagerId == managerId));
}
