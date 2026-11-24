using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class PolicyConfig
{
    public int PolicyId { get; set; }

    public string PolicyType { get; set; } = null!;

    /// <summary>A percentage for *_RATIO policies, a number of days for the others.</summary>
    public decimal Value { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User UpdatedByNavigation { get; set; } = null!;
}
