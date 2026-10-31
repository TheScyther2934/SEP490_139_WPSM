using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class ServiceCatalogItem
{
    public int ItemId { get; set; }

    public string Name { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public decimal DefaultPrice { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<PriceFrame> PriceFrames { get; set; } = new List<PriceFrame>();

    public virtual ICollection<SpecItem> SpecItems { get; set; } = new List<SpecItem>();
}
