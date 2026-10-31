using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class Rating
{
    public int RatingId { get; set; }

    public int ProjectId { get; set; }

    public string Module { get; set; } = null!;

    public byte Stars { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual WeddingProject Project { get; set; } = null!;
}
