using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class DayOfTask
{
    public int DayOfTaskId { get; set; }

    public int ProjectId { get; set; }

    public string TaskName { get; set; } = null!;

    public DateTime DeadlineTime { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string Status { get; set; } = null!;

    public bool PhotoRequired { get; set; }

    public virtual ICollection<ExecutionPhoto> ExecutionPhotos { get; set; } = new List<ExecutionPhoto>();

    public virtual WeddingProject Project { get; set; } = null!;
}
