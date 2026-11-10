using System;
using System.Collections.Generic;

namespace Infrastructure.Persistence.Entities;

public partial class DayOfTask
{
    public int DayOfTaskId { get; set; }

    public int ProjectId { get; set; }

    public string TaskName { get; set; } = null!;

    public DateTime DeadlineTime { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int? CompletedBy { get; set; }

    public DateTime? OverdueFlaggedAt { get; set; }

    public string Status { get; set; } = null!;

    public bool PhotoRequired { get; set; }

    public byte[]? RowVersion { get; set; }

    public virtual ICollection<ExecutionPhoto> ExecutionPhotos { get; set; } = new List<ExecutionPhoto>();

    public virtual WeddingProject Project { get; set; } = null!;

    public virtual User? CompletedByUser { get; set; }
}
