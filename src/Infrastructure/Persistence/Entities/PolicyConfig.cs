using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class PolicyConfig
{
    public int PolicyId { get; set; }

    public string PolicyType { get; set; } = null!;

    public int? TdayMin { get; set; }

    public int? TdayMax { get; set; }

    public decimal RatePercent { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User UpdatedByNavigation { get; set; } = null!;
}
