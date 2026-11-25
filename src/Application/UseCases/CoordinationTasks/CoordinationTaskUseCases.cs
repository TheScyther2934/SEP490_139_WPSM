using Application.Persistence;
using Domain.Entities;
using Domain.Rules;

namespace Application.UseCases.CoordinationTasks;

/// <summary>Data access for the coordination task use cases; implemented in Infrastructure. Entities come back tracked.</summary>
public interface ICoordinationTaskStore
{
    Task<WeddingProject?> GetEventAsync(string projectCode, CancellationToken cancellationToken = default);
    /// <summary>The task with its event and its proof files; null when it is not a task of this event.</summary>
    Task<DayOfTask?> GetTaskAsync(string projectCode, int taskId, CancellationToken cancellationToken = default);
    void Add(DayOfTask task);
    void Remove(DayOfTask task);
    /// <summary>Stages the tasks still missing for the locked services; returns how many were added.</summary>
    Task<int> StageMissingTasksAsync(int projectId, CancellationToken cancellationToken = default);
    /// <summary>Checks every file (type, size) before storing any, then stores them and attaches them to the task.</summary>
    Task StageProofAsync(DayOfTask task, int uploaderId, IReadOnlyList<ProofPhotoUpload> files, DateTime nowUtc, CancellationToken cancellationToken = default);
}

/// <summary>Use case (M-16): generates the missing tasks from the locked services, any time after the lock.</summary>
public sealed class GenerateCoordinationTasks(ICoordinationTaskStore store, IDayOfRepository board, IUnitOfWork unitOfWork)
{
    public async Task<DayOfBoardView?> HandleAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventAsync(projectCode, cancellationToken);
        if (project is null) return null;
        CoordinationTaskPolicy.EnsureCanGenerate(project.Status);
        await store.StageMissingTasksAsync(project.ProjectId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await board.GetBoardAsync(projectCode, cancellationToken);
    }
}

/// <summary>Use case (UC-31): the operator adds a task by hand.</summary>
public sealed class AddCoordinationTask(ICoordinationTaskStore store, IDayOfRepository board, IUnitOfWork unitOfWork)
{
    public async Task<DayOfBoardView?> HandleAsync(string projectCode, int managerId, CoordinationTaskCommand command, CancellationToken cancellationToken = default)
    {
        var project = await store.GetEventAsync(projectCode, cancellationToken);
        if (project is null) return null;
        CoordinationTaskPolicy.EnsureOpen(project.Status);
        var details = CoordinationTaskPolicy.ValidateDetails(command.TaskName, command.Department, command.Deadline, command.PhotoRequired, command.Note,
            project.EventDate, project.LockedAt);
        store.Add(DayOfTask.AddByHand(project.ProjectId, managerId, details));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await board.GetBoardAsync(projectCode, cancellationToken);
    }
}

/// <summary>Use case: the operator edits a task (deadline, department, proof required, note).</summary>
public sealed class EditCoordinationTask(ICoordinationTaskStore store, IDayOfRepository board, IUnitOfWork unitOfWork)
{
    public async Task<DayOfBoardView?> HandleAsync(string projectCode, int taskId, CoordinationTaskCommand command, CancellationToken cancellationToken = default)
    {
        var task = await store.GetTaskAsync(projectCode, taskId, cancellationToken);
        if (task is null) return null;
        CoordinationTaskPolicy.EnsureOpen(task.Project.Status);
        var details = CoordinationTaskPolicy.ValidateDetails(command.TaskName, command.Department, command.Deadline, command.PhotoRequired, command.Note,
            task.Project.EventDate, task.Project.LockedAt);
        task.Edit(details, DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await board.GetBoardAsync(projectCode, cancellationToken);
    }
}

/// <summary>Use case: the operator deletes a task added by hand that is not done and has no proof.</summary>
public sealed class DeleteCoordinationTask(ICoordinationTaskStore store, IDayOfRepository board, IUnitOfWork unitOfWork)
{
    public async Task<DayOfBoardView?> HandleAsync(string projectCode, int taskId, CancellationToken cancellationToken = default)
    {
        var task = await store.GetTaskAsync(projectCode, taskId, cancellationToken);
        if (task is null) return null;
        CoordinationTaskPolicy.EnsureOpen(task.Project.Status);
        task.EnsureCanDelete();
        store.Remove(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await board.GetBoardAsync(projectCode, cancellationToken);
    }
}

/// <summary>Use case (UC-30, M-17): the operator completes a task with its proof (MSG10 when proof is required and missing).</summary>
public sealed class CompleteCoordinationTask(ICoordinationTaskStore store, IDayOfRepository board, IUnitOfWork unitOfWork)
{
    public async Task<DayOfBoardView?> HandleAsync(string projectCode, int taskId, int managerId, IReadOnlyList<ProofPhotoUpload> files, string? note,
        CancellationToken cancellationToken = default)
    {
        var task = await store.GetTaskAsync(projectCode, taskId, cancellationToken);
        if (task is null) return null;
        CoordinationTaskPolicy.EnsureOpen(task.Project.Status);
        var now = DateTime.UtcNow;
        task.Complete(managerId, now, files.Count, note);
        await store.StageProofAsync(task, managerId, files, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await board.GetBoardAsync(projectCode, cancellationToken);
    }
}

/// <summary>Use case: the operator reopens a task completed by mistake, while the event is still open (before settlement).</summary>
public sealed class ReopenCoordinationTask(ICoordinationTaskStore store, IDayOfRepository board, IUnitOfWork unitOfWork)
{
    public async Task<DayOfBoardView?> HandleAsync(string projectCode, int taskId, CancellationToken cancellationToken = default)
    {
        var task = await store.GetTaskAsync(projectCode, taskId, cancellationToken);
        if (task is null) return null;
        CoordinationTaskPolicy.EnsureOpen(task.Project.Status);
        task.Reopen();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await board.GetBoardAsync(projectCode, cancellationToken);
    }
}

/// <summary>Use case: proof added to a task afterwards (a task completed without it, or more angles of the same work).</summary>
public sealed class AddCoordinationProof(ICoordinationTaskStore store, IDayOfRepository board, IUnitOfWork unitOfWork)
{
    public async Task<DayOfBoardView?> HandleAsync(string projectCode, int taskId, int managerId, IReadOnlyList<ProofPhotoUpload> files,
        CancellationToken cancellationToken = default)
    {
        var task = await store.GetTaskAsync(projectCode, taskId, cancellationToken);
        if (task is null) return null;
        CoordinationTaskPolicy.EnsureOpen(task.Project.Status);
        task.EnsureCanAddProof(files.Count);
        await store.StageProofAsync(task, managerId, files, DateTime.UtcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await board.GetBoardAsync(projectCode, cancellationToken);
    }
}
