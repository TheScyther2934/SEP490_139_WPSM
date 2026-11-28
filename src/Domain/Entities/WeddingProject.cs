using System;
using System.Collections.Generic;

namespace Domain.Entities;

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

    /// <summary>Deposit paid at signing (stage 1); never refunded when the event is cancelled.</summary>
    public decimal DepositAmount { get; set; }

    public decimal CurrentTotal { get; set; }

    public string Status { get; set; } = null!;

    /// <summary>When the services were locked (10 days before the event, by the system or an operator).</summary>
    public DateTime? LockedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public int? CancelledBy { get; set; }

    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>User who created the project (accountability for Admin); null only for rows older than the column.</summary>
    public int? CreatedBy { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    /// <summary>Everyone who operates or operated the project (Primary + CoManagers, removed rows kept for history).</summary>
    public virtual ICollection<ProjectManager> ProjectManagers { get; set; } = new List<ProjectManager>();

    public virtual ICollection<Beo> Beos { get; set; } = new List<Beo>();

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<DayOfTask> DayOfTasks { get; set; } = new List<DayOfTask>();

    public virtual Hall Hall { get; set; } = null!;

    public virtual User Manager { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual ICollection<ResourceAllocation> ResourceAllocations { get; set; } = new List<ResourceAllocation>();

    public virtual Slot Slot { get; set; } = null!;

    public virtual ICollection<SpecItem> SpecItems { get; set; } = new List<SpecItem>();

    public virtual ICollection<StageNote> StageNotes { get; set; } = new List<StageNote>();

    public virtual ICollection<AddonContract> AddonContracts { get; set; } = new List<AddonContract>();

    public virtual ICollection<SupplierEvidence> SupplierEvidences { get; set; } = new List<SupplierEvidence>();

    public virtual SettlementInvoice? SettlementInvoice { get; set; }

    public virtual EventTerms? Terms { get; set; }
}
