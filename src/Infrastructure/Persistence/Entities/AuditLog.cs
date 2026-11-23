namespace Infrastructure.Persistence.Entities;

/// <summary>One audited action (append-only, see TR_AuditLogs_AppendOnly). UserId null means a background job.</summary>
public class AuditLog
{
    public long AuditId { get; set; }

    public DateTime OccurredAt { get; set; }

    public int? UserId { get; set; }

    public string? AccountName { get; set; }

    public string? Email { get; set; }

    public string? Role { get; set; }

    /// <summary>Create, Update, Delete or Login.</summary>
    public string Action { get; set; } = null!;

    /// <summary>Service, Upload, Account, Project, Payment, ChangeRequest, Policy, Complaint, Exception or Other.</summary>
    public string Impact { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public string? EntityId { get; set; }

    public int? ProjectId { get; set; }

    public string? ProjectCode { get; set; }

    public string Summary { get; set; } = null!;

    /// <summary>JSON object: field → { old, new }; secrets and file contents are masked.</summary>
    public string? Changes { get; set; }

    public string? IpAddress { get; set; }
}
