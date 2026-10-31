using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class Beo
{
    public int Beoid { get; set; }

    public int ProjectId { get; set; }

    public int Version { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? FrozenAt { get; set; }

    public int? ApprovedBy { get; set; }

    public string? Notes { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual ICollection<BeodepartmentSheet> BeodepartmentSheets { get; set; } = new List<BeodepartmentSheet>();

    public virtual ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();

    public virtual WeddingProject Project { get; set; } = null!;
}
