using System.Globalization;
using Application.Persistence;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;

namespace Application.UseCases.Resources;

/// <summary>Data access for the resource allocation use cases; implemented in Infrastructure. Entities come back tracked.</summary>
public interface IResourceAllocationStore
{
    /// <summary>The event when the caller operates it (primary or co-operator); null otherwise (GBR-12).</summary>
    Task<WeddingProject?> GetEventAsync(int managerId, string projectCode, CancellationToken cancellationToken = default);
    Task<Resource?> GetResourceAsync(int resourceId, CancellationToken cancellationToken = default);
    /// <summary>The allocation with its event and resource, when the caller operates the event; null otherwise.</summary>
    Task<ResourceAllocation?> GetAllocationAsync(int managerId, int allocationId, CancellationToken cancellationToken = default);
    Task<bool> HasPendingExceptionAsync(int allocationId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Held until the transaction ends, so two operators cannot both pass the capacity check for the same resource (SDS III.2.2).
    /// </summary>
    Task LockResourceAsync(int resourceId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Allocations of the resource that overlap the range and count against capacity (Reserved or Exception Approved, without an
    /// undecided or rejected exception request).
    /// </summary>
    Task<IReadOnlyList<ConflictView>> GetOverlappingAsync(int resourceId, DateTime startUtc, DateTime endUtc, int? excludeAllocationId,
        CancellationToken cancellationToken = default);
    Task<string> NextExceptionCodeAsync(CancellationToken cancellationToken = default);
    /// <summary>The request with its allocation and event; null when it does not exist.</summary>
    Task<ExceptionRequest?> GetExceptionRequestAsync(int requestId, CancellationToken cancellationToken = default);
    void Add(ResourceAllocation allocation);
    void Add(ExceptionRequest request);
}

/// <summary>Steps shared by the allocation use cases.</summary>
internal static class AllocationSteps
{
    /// <summary>Checks the request (same order as before the move) and returns the event and the resource; event null when not the caller's.</summary>
    public static async Task<(WeddingProject? Project, Resource Resource)> LoadAsync(IResourceAllocationStore store, int managerId, AllocationCommand command,
        CancellationToken cancellationToken)
    {
        ResourceAllocationPolicy.ValidateRequest(command.Quantity, command.StartTime, command.EndTime);
        var resource = await store.GetResourceAsync(command.ResourceId, cancellationToken)
            ?? throw new BusinessRuleException("Trường Tài nguyên là bắt buộc.");
        ResourceAllocationPolicy.EnsureWithinTotal(command.Quantity, resource.TotalQuantity);
        var project = await store.GetEventAsync(managerId, command.ProjectCode, cancellationToken);
        if (project is not null) ResourceAllocationPolicy.EnsureEventOpen(project.Status);
        return (project, resource);
    }

    /// <summary>The overlapping allocations when the request does not fit (GBR-09); empty when it fits.</summary>
    public static async Task<IReadOnlyList<ConflictView>> ConflictsAsync(IResourceAllocationStore store, Resource resource, DateTime startUtc, DateTime endUtc,
        int quantity, int? excludeAllocationId, CancellationToken cancellationToken)
    {
        var overlapping = await store.GetOverlappingAsync(resource.ResourceId, startUtc, endUtc, excludeAllocationId, cancellationToken);
        return ResourceAllocationPolicy.Shortage(resource.TotalQuantity, overlapping.Sum(x => x.Quantity), quantity) > 0 ? overlapping : [];
    }

    public static ResourceView View(Resource resource) => new(resource.ResourceId, resource.Name, resource.Type, resource.TotalQuantity);

    /// <summary>MSG08: nothing is saved; the screen shows the clashing events.</summary>
    public static AllocationResult Conflict(Resource resource, IReadOnlyList<ConflictView> conflicts) =>
        new(null, $"Phát hiện xung đột với {conflicts[0].ProjectCode} lúc {VietnamTime.ConvertUtcToVietnam(conflicts[0].StartTime).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} (MSG08).",
            View(resource), conflicts);
}

/// <summary>Use case (UC-25, M-21): the operator allocates a resource to one of their events.</summary>
public sealed class AllocateResource(IResourceAllocationStore store, IResourceRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<AllocationResult?> HandleAsync(int managerId, AllocationCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var (project, resource) = await AllocationSteps.LoadAsync(store, managerId, command, cancellationToken);
        if (project is null) return null;
        await store.LockResourceAsync(resource.ResourceId, cancellationToken);
        var start = command.StartTime.ToUniversalTime();
        var end = command.EndTime.ToUniversalTime();
        var conflicts = await AllocationSteps.ConflictsAsync(store, resource, start, end, command.Quantity, null, cancellationToken);
        if (conflicts.Count > 0) return AllocationSteps.Conflict(resource, conflicts);

        var allocation = ResourceAllocation.Reserve(resource.ResourceId, project, start, end, command.Quantity);
        store.Add(allocation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AllocationResult(await reads.GetAllocationAsync(managerId, allocation.AllocationId, cancellationToken), "Đã phân bổ tài nguyên.", null, []);
    }
}

/// <summary>Use case (UC-26, M-22): reschedule or substitute an allocation; the capacity is checked again before saving.</summary>
public sealed class UpdateResourceAllocation(IResourceAllocationStore store, IResourceRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<AllocationResult?> HandleAsync(int managerId, int allocationId, AllocationCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var allocation = await store.GetAllocationAsync(managerId, allocationId, cancellationToken);
        if (allocation is null) return null;
        allocation.EnsureCanEdit(await store.HasPendingExceptionAsync(allocationId, cancellationToken));
        // The event of an allocation never changes, whatever the request says.
        var (project, resource) = await AllocationSteps.LoadAsync(store, managerId, command with { ProjectCode = allocation.Project.ProjectCode }, cancellationToken);
        if (project is null) return null;
        await store.LockResourceAsync(resource.ResourceId, cancellationToken);
        var start = command.StartTime.ToUniversalTime();
        var end = command.EndTime.ToUniversalTime();
        var conflicts = await AllocationSteps.ConflictsAsync(store, resource, start, end, command.Quantity, allocationId, cancellationToken);
        if (conflicts.Count > 0) return AllocationSteps.Conflict(resource, conflicts);

        allocation.Reschedule(resource.ResourceId, start, end, command.Quantity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AllocationResult(await reads.GetAllocationAsync(managerId, allocationId, cancellationToken), "Đã cập nhật phân bổ.", null, []);
    }
}

/// <summary>Use case: the operator frees an allocation. False when it is not one of the caller's.</summary>
public sealed class ReleaseResourceAllocation(IResourceAllocationStore store, IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(int managerId, int allocationId, CancellationToken cancellationToken = default)
    {
        var allocation = await store.GetAllocationAsync(managerId, allocationId, cancellationToken);
        if (allocation is null) return false;
        allocation.Release();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

/// <summary>
/// Use case (UC-47 from UC-25, M-21): the allocation does not fit, so the operator asks the Admin. The allocation is saved with the
/// request and stays out of the capacity check until the Admin decides.
/// </summary>
public sealed class RequestResourceException(IResourceAllocationStore store, IResourceRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<AllocationResult?> HandleAsync(int managerId, AllocationCommand command, string? note, CancellationToken cancellationToken = default)
    {
        var cleanNote = ResourceAllocationPolicy.ValidateNote(note, "Ghi chú");
        var (project, resource) = await AllocationSteps.LoadAsync(store, managerId, command, cancellationToken);
        if (project is null) return null;
        var start = command.StartTime.ToUniversalTime();
        var end = command.EndTime.ToUniversalTime();
        var conflicts = await AllocationSteps.ConflictsAsync(store, resource, start, end, command.Quantity, null, cancellationToken);
        if (conflicts.Count == 0) throw new BusinessRuleException("Không có xung đột, hãy phân bổ trực tiếp.");

        var allocation = ResourceAllocation.Reserve(resource.ResourceId, project, start, end, command.Quantity);
        store.Add(allocation);
        store.Add(ExceptionRequest.ForResourceConflict(await store.NextExceptionCodeAsync(cancellationToken), allocation, managerId, cleanNote, DateTime.UtcNow));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new AllocationResult(await reads.GetAllocationAsync(managerId, allocation.AllocationId, cancellationToken),
            "Đã gửi yêu cầu ngoại lệ tới Admin.", AllocationSteps.View(resource), conflicts);
    }
}

/// <summary>Use case (UC-47, A-18): the Admin approves or rejects a request with a mandatory note; the operator is told.</summary>
public sealed class DecideExceptionRequest(IResourceAllocationStore store, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(int requestId, bool approve, string? note, int adminId, CancellationToken cancellationToken = default)
    {
        var cleanNote = ResourceAllocationPolicy.ValidateNote(note, "Ghi chú quyết định");
        var request = await store.GetExceptionRequestAsync(requestId, cancellationToken);
        if (request is null) return false;
        request.Decide(approve, adminId, cleanNote, DateTime.UtcNow);
        await messages.ExceptionDecidedAsync(request.RequestedBy, request.RequestCode, request.Project.ProjectCode, approve, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
