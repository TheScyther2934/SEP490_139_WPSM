using System;
using System.Collections.Generic;

namespace Domain.Entities;

public partial class ExecutionPhoto
{
    public int PhotoId { get; set; }

    public int DayOfTaskId { get; set; }

    public int UploaderId { get; set; }

    /// <summary>Storage key of the file (same as StoredFiles.StorageKey), never a URL or a disk path.</summary>
    public string ImageFilePath { get; set; } = null!;

    /// <summary>The StoredFiles row of this proof (type, size, original name).</summary>
    public Guid? FileId { get; set; }

    public virtual StoredFile? File { get; set; }

    public DateTime UploadedAt { get; set; }

    public string? FileName { get; set; }

    /// <summary>image/jpeg, image/png or application/pdf; null on legacy rows (images).</summary>
    public string? ContentType { get; set; }

    public virtual DayOfTask DayOfTask { get; set; } = null!;

    public virtual User Uploader { get; set; } = null!;
}
