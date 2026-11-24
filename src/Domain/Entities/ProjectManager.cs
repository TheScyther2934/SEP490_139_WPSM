namespace Domain.Entities;

/// <summary>
/// A Manager operating a project. Exactly one active Primary (the creator, mirrored in WeddingProjects.ManagerId) and any number
/// of CoManagers added by the Primary or an Admin. Removal keeps the row (RemovedAt) so the history of who operated stays visible.
/// </summary>
public class ProjectManager
{
    public int ProjectManagerId { get; set; }

    public int ProjectId { get; set; }

    public int ManagerId { get; set; }

    /// <summary>Primary or CoManager.</summary>
    public string Role { get; set; } = null!;

    public int? AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; }

    public string? Reason { get; set; }

    public int? RemovedBy { get; set; }

    public DateTime? RemovedAt { get; set; }

    public string? RemovalReason { get; set; }

    public virtual WeddingProject Project { get; set; } = null!;

    public virtual User Manager { get; set; } = null!;

    public virtual User? AssignedByNavigation { get; set; }

    public virtual User? RemovedByNavigation { get; set; }
}
