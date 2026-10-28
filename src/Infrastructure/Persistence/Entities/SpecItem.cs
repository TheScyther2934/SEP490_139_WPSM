using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class SpecItem
{
    public int SpecItemId { get; set; }

    public int ProjectId { get; set; }

    public int CatalogItemId { get; set; }

    public string Description { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? LineTotal { get; set; }

    public bool IsBaseline { get; set; }

    public int Version { get; set; }

    public virtual ServiceCatalogItem CatalogItem { get; set; } = null!;

    public virtual ICollection<Critem> Critems { get; set; } = new List<Critem>();

    public virtual WeddingProject Project { get; set; } = null!;
}
