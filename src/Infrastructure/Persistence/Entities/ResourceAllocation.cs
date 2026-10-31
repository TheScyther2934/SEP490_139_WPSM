using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class ResourceAllocation
{
    public int AllocationId { get; set; }

    public int ResourceId { get; set; }

    public int HallId { get; set; }

    public int ProjectId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int AllocatedQuantity { get; set; }

    public string Status { get; set; } = null!;

    public bool IsExceptionApproved { get; set; }

    public int? ExceptionApprovedBy { get; set; }

    public DateTime? ExceptionApprovedAt { get; set; }

    public string? ExceptionNote { get; set; }

    public virtual User? ExceptionApprovedByNavigation { get; set; }

    public virtual Hall Hall { get; set; } = null!;

    public virtual WeddingProject Project { get; set; } = null!;

    public virtual Resource Resource { get; set; } = null!;
}
