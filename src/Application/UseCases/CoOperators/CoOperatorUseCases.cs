using Application.Persistence;
using Domain.Entities;
using Domain.Rules;

namespace Application.UseCases.CoOperators;

/// <summary>Data access for the co-operator use cases; implemented in Infrastructure. Entities come back tracked.</summary>
public interface ICoOperatorStore
{
    /// <summary>The event with its primary operator and its operator rows.</summary>
    Task<WeddingProject?> GetEventAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>Use case: the primary operator or an Admin adds a co-operator with a reason; the new operator is told.</summary>
public sealed class AddCoOperator(ICoOperatorStore store, IProjectManagerRepository reads, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<ProjectManagerResult> HandleAsync(string projectCode, int managerId, string? reason, int actorId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventAsync(projectCode, cancellationToken);
        if (project is null) return new ProjectManagerResult(null, NotFound: true);
        if (!CoOperatorPolicy.CanManage(project.ManagerId, actorId, actorRole))
            return new ProjectManagerResult(null, "Chỉ Manager tạo sự kiện hoặc Admin mới được thêm người điều hành.", Forbidden: true);
        var note = CoOperatorPolicy.ValidateReason(reason, removal: false);
        var manager = await store.GetUserAsync(managerId, cancellationToken)
            ?? throw new BusinessRuleException("Người được ủy quyền phải là tài khoản Manager đang hoạt động.");

        project.AddCoOperator(manager, actorId, note, DateTime.UtcNow);
        await messages.CoOperatorAddedAsync(managerId, project.ProjectCode, note, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ProjectManagerResult(await reads.GetAsync(projectCode, actorId, actorRole, cancellationToken));
    }
}

/// <summary>Use case: the primary operator or an Admin removes a co-operator with a reason; the removed operator is told.</summary>
public sealed class RemoveCoOperator(ICoOperatorStore store, IProjectManagerRepository reads, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<ProjectManagerResult> HandleAsync(string projectCode, int projectManagerId, string? reason, int actorId, string actorRole,
        CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventAsync(projectCode, cancellationToken);
        if (project is null) return new ProjectManagerResult(null, NotFound: true);
        if (!CoOperatorPolicy.CanManage(project.ManagerId, actorId, actorRole))
            return new ProjectManagerResult(null, "Chỉ Manager tạo sự kiện hoặc Admin mới được gỡ người điều hành.", Forbidden: true);

        var row = project.RemoveCoOperator(projectManagerId, actorId, reason, DateTime.UtcNow);
        if (row is null) return new ProjectManagerResult(null, NotFound: true);
        await messages.CoOperatorRemovedAsync(row.ManagerId, project.ProjectCode, row.RemovalReason!, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ProjectManagerResult(await reads.GetAsync(projectCode, actorId, actorRole, cancellationToken));
    }
}
