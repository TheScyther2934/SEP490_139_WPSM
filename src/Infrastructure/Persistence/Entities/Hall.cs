using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class Hall
{
    public int HallId { get; set; }

    public string Name { get; set; } = null!;

    public int MinCapacity { get; set; }

    public int MaxCapacity { get; set; }

    public decimal BasePrice { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<ResourceAllocation> ResourceAllocations { get; set; } = new List<ResourceAllocation>();

    public virtual ICollection<Slot> Slots { get; set; } = new List<Slot>();

    public virtual ICollection<WeddingProject> WeddingProjects { get; set; } = new List<WeddingProject>();
}
