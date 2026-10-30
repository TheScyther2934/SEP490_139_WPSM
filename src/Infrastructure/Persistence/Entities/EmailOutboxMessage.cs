namespace WPSM.Infrastructure.Persistence.Entities;

public partial class EmailOutboxMessage
{
    public long EmailId { get; set; }

    public string ToEmail { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string BodyHtml { get; set; } = null!;

    public string Kind { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int RetryCount { get; set; }

    public DateTime NextAttemptAt { get; set; }

    public string? ProviderMessageId { get; set; }

    public string? LastError { get; set; }

    public int? RelatedProjectId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? DeliveredAt { get; set; }
}
