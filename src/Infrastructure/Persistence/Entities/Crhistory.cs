using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class Crhistory
{
    public int HistoryId { get; set; }

    public int Crid { get; set; }

    public int ActorId { get; set; }

    public string Action { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User Actor { get; set; } = null!;

    public virtual ChangeRequest Cr { get; set; } = null!;
}
