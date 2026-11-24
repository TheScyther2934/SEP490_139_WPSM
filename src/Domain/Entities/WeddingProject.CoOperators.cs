using Domain.Rules;

namespace Domain.Entities;

/// <summary>Co-operators of an event. Who may change the list is checked first by the use case (CoOperatorPolicy.CanManage).</summary>
public partial class WeddingProject
{
    /// <summary>Adds an active Manager as co-operator. Events older than the operator table get their primary row first, so the history is complete.</summary>
    public ProjectManager AddCoOperator(User manager, int actorId, string reason, DateTime nowUtc)
    {
        if (Status is "Settled" or "Closed") throw new BusinessRuleException("Sự kiện đã tất toán hoặc đã đóng, không thể thêm người điều hành.");
        if (manager.Role != AccountPolicy.Manager || manager.Status != "Active")
            throw new BusinessRuleException("Người được ủy quyền phải là tài khoản Manager đang hoạt động.");
        if (manager.UserId == ManagerId || ProjectManagers.Any(x => x.ManagerId == manager.UserId && x.RemovedAt is null))
            throw new BusinessRuleException($"{manager.FullName} đang điều hành sự kiện này.");

        if (!ProjectManagers.Any(x => x.Role == CoOperatorPolicy.Primary && x.RemovedAt is null))
            ProjectManagers.Add(new ProjectManager
            {
                ManagerId = ManagerId, Manager = Manager, Role = CoOperatorPolicy.Primary, AssignedBy = CreatedBy, AssignedAt = CreatedAt, Reason = "Người tạo sự kiện"
            });
        var row = new ProjectManager { ManagerId = manager.UserId, Manager = manager, Role = CoOperatorPolicy.CoManager, AssignedBy = actorId, AssignedAt = nowUtc, Reason = reason };
        ProjectManagers.Add(row);
        return row;
    }

    /// <summary>Ends a co-operator's access (the row stays as history). The primary operator is never removed. Null when not an active operator.</summary>
    public ProjectManager? RemoveCoOperator(int projectManagerId, int actorId, string? reason, DateTime nowUtc)
    {
        var row = ProjectManagers.FirstOrDefault(x => x.ProjectManagerId == projectManagerId && x.RemovedAt is null);
        if (row is null) return null;
        if (row.Role == CoOperatorPolicy.Primary) throw new BusinessRuleException("Không thể gỡ Manager tạo sự kiện.");
        row.RemovalReason = CoOperatorPolicy.ValidateReason(reason, removal: true);
        row.RemovedAt = nowUtc;
        row.RemovedBy = actorId;
        return row;
    }
}
