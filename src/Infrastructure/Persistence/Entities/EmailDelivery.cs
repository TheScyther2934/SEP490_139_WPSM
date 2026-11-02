namespace Infrastructure.Persistence.Entities;

public sealed class EmailDelivery
{
    public int EmailDeliveryId { get; set; }
    public int ProjectId { get; set; }
    public string Recipient { get; set; } = "";
    public string? ProtectedPayload { get; set; }
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
}
