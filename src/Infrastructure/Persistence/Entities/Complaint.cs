using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class Complaint
{
    public int ComplaintId { get; set; }

    public string ComplaintCode { get; set; } = null!;

    public int ProjectId { get; set; }

    public int Beoid { get; set; }

    public string Description { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? EscalationNote { get; set; }

    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Beo Beo { get; set; } = null!;

    public virtual WeddingProject Project { get; set; } = null!;
}
