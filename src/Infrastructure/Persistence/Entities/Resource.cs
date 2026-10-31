using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class Resource
{
    public int ResourceId { get; set; }

    public string Name { get; set; } = null!;

    public string Type { get; set; } = null!;

    public int TotalQuantity { get; set; }

    public virtual ICollection<ResourceAllocation> ResourceAllocations { get; set; } = new List<ResourceAllocation>();
}
