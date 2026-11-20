namespace Application.Persistence;

// Co-management: a project is operated by its Primary Manager (the creator) plus any co-managers the Primary or an Admin adds.

public sealed record ProjectManagerView(int ProjectManagerId, int ManagerId, string Name, string Email, string Role, string? AssignedByName,
    DateTime AssignedAt, string? Reason, string? RemovedByName, DateTime? RemovedAt, string? RemovalReason, bool Active);

/// <summary>Everyone who operates (Active) or operated the project, plus whether the caller may add or remove co-managers.</summary>
public sealed record ProjectManagersView(string ProjectCode, bool CanManage, IReadOnlyList<ProjectManagerView> Managers);

/// <summary>Outcome of an add/remove: Forbidden when the caller is neither the Primary nor an Admin, Error for a rule violation.</summary>
public sealed record ProjectManagerResult(ProjectManagersView? View, string? Error = null, bool Forbidden = false, bool NotFound = false);

public interface IProjectManagerRepository
{
    Task<ProjectManagersView?> GetAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default);
    /// <summary>Active Managers who could be added (not already operating the project).</summary>
    Task<IReadOnlyList<OptionItem>> GetCandidatesAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<ProjectManagerResult> AddAsync(string projectCode, int managerId, string? reason, int actorId, string actorRole, CancellationToken cancellationToken = default);
    Task<ProjectManagerResult> RemoveAsync(string projectCode, int projectManagerId, string? reason, int actorId, string actorRole, CancellationToken cancellationToken = default);
}
