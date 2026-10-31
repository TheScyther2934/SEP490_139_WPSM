using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class Slot
{
    public int SlotId { get; set; }

    public int HallId { get; set; }

    public string SlotName { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public virtual Hall Hall { get; set; } = null!;

    public virtual ICollection<WeddingProject> WeddingProjects { get; set; } = new List<WeddingProject>();
}
