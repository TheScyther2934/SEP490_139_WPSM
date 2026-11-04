namespace Infrastructure.Persistence.Entities;

public class ExceptionRequest
{
    public int ExceptionRequestId { get; set; }

    public string RequestCode { get; set; } = null!;

    public string SourceType { get; set; } = null!;

    public int ProjectId { get; set; }

    public int? Crid { get; set; }

    public int? AllocationId { get; set; }

    public int RequestedBy { get; set; }

    public string RequestNote { get; set; } = null!;

    public string Status { get; set; } = "Pending";

    public int? DecisionBy { get; set; }

    public string? DecisionNote { get; set; }

    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual WeddingProject Project { get; set; } = null!;

    public virtual ChangeRequest? Cr { get; set; }

    public virtual ResourceAllocation? Allocation { get; set; }

    public virtual User RequestedByNavigation { get; set; } = null!;
}
