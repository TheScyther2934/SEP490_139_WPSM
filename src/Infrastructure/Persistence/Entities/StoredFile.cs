namespace WPSM.Infrastructure.Persistence.Entities;

public partial class StoredFile
{
    public Guid FileId { get; set; }

    public int? ProjectId { get; set; }

    public string Purpose { get; set; } = null!;

    public string StorageKey { get; set; } = null!;

    public string OriginalName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public int? UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; }

    public virtual WeddingProject? Project { get; set; }
}
