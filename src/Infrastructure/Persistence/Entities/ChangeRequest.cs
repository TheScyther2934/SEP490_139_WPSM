using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class ChangeRequest
{
    public int Crid { get; set; }

    public string Crcode { get; set; } = null!;

    public int ProjectId { get; set; }

    public int CreatedBy { get; set; }

    public string Type { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal CostImpact { get; set; }

    public decimal? PenaltyRate { get; set; }

    public decimal PenaltyFee { get; set; }

    public decimal? TotalVariance { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CustomerApprovedAt { get; set; }

    public DateTime? ManagerApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ContractAddendum? ContractAddendum { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<Crhistory> Crhistories { get; set; } = new List<Crhistory>();

    public virtual ICollection<Critem> Critems { get; set; } = new List<Critem>();

    public virtual WeddingProject Project { get; set; } = null!;
}
