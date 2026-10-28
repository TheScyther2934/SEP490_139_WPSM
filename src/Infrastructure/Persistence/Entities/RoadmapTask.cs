using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class RoadmapTask
{
    public int TaskId { get; set; }

    public int ProjectId { get; set; }

    public string Milestone { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string AssigneeRole { get; set; } = null!;

    public DateOnly DueDate { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CompletedAt { get; set; }

    public DateTime? RemindedAt { get; set; }

    public virtual WeddingProject Project { get; set; } = null!;
}
