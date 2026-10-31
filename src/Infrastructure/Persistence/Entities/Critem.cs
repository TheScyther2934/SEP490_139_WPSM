using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class Critem
{
    public int CritemId { get; set; }

    public int Crid { get; set; }

    public int TargetSpecItemId { get; set; }

    public int OldQuantity { get; set; }

    public decimal OldUnitPrice { get; set; }

    public int NewQuantity { get; set; }

    public decimal NewUnitPrice { get; set; }

    public decimal? DeltaAmount { get; set; }

    public virtual ChangeRequest Cr { get; set; } = null!;

    public virtual SpecItem TargetSpecItem { get; set; } = null!;
}
