namespace Infrastructure.Persistence.Entities;

// Columns added for A-16/A-17; kept apart from the scaffolded class.
public partial class Complaint
{
    public DateTime? EscalatedAt { get; set; }

    public int? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
