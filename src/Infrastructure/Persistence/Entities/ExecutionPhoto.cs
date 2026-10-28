using System;
using System.Collections.Generic;

namespace WPSM.Infrastructure.Persistence.Entities;

public partial class ExecutionPhoto
{
    public int PhotoId { get; set; }

    public int DayOfTaskId { get; set; }

    public int UploaderId { get; set; }

    public string ImageFilePath { get; set; } = null!;

    public DateTime UploadedAt { get; set; }

    public virtual DayOfTask DayOfTask { get; set; } = null!;

    public virtual User Uploader { get; set; } = null!;
}
