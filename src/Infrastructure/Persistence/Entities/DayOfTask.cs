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

    /// <summary>Concurrency token: the PostgreSQL system column xmin (changes on every update).</summary>
    public uint RowVersion { get; set; }

    /// <summary>Kitchen, Service, Decor or Tech-MC.</summary>
    public string Department { get; set; } = "Service";

    /// <summary>What generated the task: spec:&lt;id&gt;, addon:&lt;id&gt;, dayof:&lt;n&gt;; null for a task added by hand.</summary>
    public string? SourceKey { get; set; }

    public string? Note { get; set; }

    public int? CreatedBy { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<ExecutionPhoto> ExecutionPhotos { get; set; } = new List<ExecutionPhoto>();

    public virtual WeddingProject Project { get; set; } = null!;

    public virtual User? CompletedByUser { get; set; }
}
