using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class PriceFrame
{
    public int PriceFrameId { get; set; }

    public int ItemId { get; set; }

    public decimal MinPrice { get; set; }

    public decimal MaxPrice { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public int UpdatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ServiceCatalogItem Item { get; set; } = null!;

    public virtual User UpdatedByNavigation { get; set; } = null!;
}
