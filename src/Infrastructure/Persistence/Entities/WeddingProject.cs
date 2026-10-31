using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class WeddingProject
{
    public int ProjectId { get; set; }

    public string ProjectCode { get; set; } = null!;

    public int CustomerId { get; set; }

    public int ManagerId { get; set; }

    public int HallId { get; set; }

    public int SlotId { get; set; }

    public DateOnly EventDate { get; set; }

    public decimal BaseContractValue { get; set; }

    public decimal Nrdamount { get; set; }

    public decimal CurrentTotal { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? BaselineFrozenAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Beo> Beos { get; set; } = new List<Beo>();

    public virtual ICollection<ChangeRequest> ChangeRequests { get; set; } = new List<ChangeRequest>();

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual ICollection<ContractAddendum> ContractAddenda { get; set; } = new List<ContractAddendum>();

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<DayOfTask> DayOfTasks { get; set; } = new List<DayOfTask>();

    public virtual Hall Hall { get; set; } = null!;

    public virtual User Manager { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual ICollection<ResourceAllocation> ResourceAllocations { get; set; } = new List<ResourceAllocation>();

    public virtual ICollection<RoadmapTask> RoadmapTasks { get; set; } = new List<RoadmapTask>();

    public virtual Slot Slot { get; set; } = null!;

    public virtual ICollection<SpecItem> SpecItems { get; set; } = new List<SpecItem>();
}
