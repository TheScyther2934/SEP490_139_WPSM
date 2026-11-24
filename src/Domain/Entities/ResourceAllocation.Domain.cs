using Domain.Rules;

namespace Domain.Entities;

/// <summary>Behaviour of an allocation; the capacity check (GBR-09) needs the other allocations and is done by the use case.</summary>
public partial class ResourceAllocation
{
    /// <summary>A new allocation in the hall of the event; times are UTC.</summary>
    public static ResourceAllocation Reserve(int resourceId, WeddingProject project, DateTime startUtc, DateTime endUtc, int quantity) => new()
    {
        ResourceId = resourceId,
        HallId = project.HallId,
        ProjectId = project.ProjectId,
        StartTime = startUtc,
        EndTime = endUtc,
        AllocatedQuantity = quantity,
        Status = ResourceAllocationPolicy.Reserved
    };

    /// <summary>Reschedule or substitute (UC-26) is refused once released or while the Admin is deciding an exception.</summary>
    public void EnsureCanEdit(bool hasPendingException)
    {
        if (Status == ResourceAllocationPolicy.Released) throw new BusinessRuleException("Phân bổ đã được giải phóng.");
        if (hasPendingException) throw new BusinessRuleException("Phân bổ đang chờ Admin quyết định ngoại lệ.");
    }

    public void Reschedule(int resourceId, DateTime startUtc, DateTime endUtc, int quantity)
    {
        ResourceId = resourceId;
        StartTime = startUtc;
        EndTime = endUtc;
        AllocatedQuantity = quantity;
    }

    public void Release() => Status = ResourceAllocationPolicy.Released;

    public void ApproveException(int adminId, DateTime nowUtc, string note)
    {
        Status = ResourceAllocationPolicy.ExceptionApproved;
        IsExceptionApproved = true;
        ExceptionApprovedBy = adminId;
        ExceptionApprovedAt = nowUtc;
        ExceptionNote = note;
    }

    /// <summary>A rejected exception never becomes valid, so the allocation is freed.</summary>
    public void RejectException() => Release();
}
