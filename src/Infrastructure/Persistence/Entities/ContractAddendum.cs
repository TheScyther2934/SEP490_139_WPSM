using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class ContractAddendum
{
    public int AddendumId { get; set; }

    public string AddendumCode { get; set; } = null!;

    public int Crid { get; set; }

    public int ProjectId { get; set; }

    public decimal TotalVariance { get; set; }

    public string? FilePath { get; set; }

    public DateTime? SignedAt { get; set; }

    public string Status { get; set; } = null!;

    public virtual ChangeRequest Cr { get; set; } = null!;

    public virtual WeddingProject Project { get; set; } = null!;
}
