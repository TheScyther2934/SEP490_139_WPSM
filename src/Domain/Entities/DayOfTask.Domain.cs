using System;
using System.Collections.Generic;
using Domain.Events;
using Domain.Rules;

namespace Domain.Entities;

public partial class DayOfTask
{
    private readonly List<DayOfTaskOverdueEvent> _domainEvents = new();

    /// <summary>
    /// Pending domain events raised by entity lifecycle transitions.
    /// </summary>
    public IReadOnlyCollection<DayOfTaskOverdueEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Indicates whether this task was completed late (CompletedAt strictly after DeadlineTime).
    /// Preserved even after status is Done.
    /// </summary>
    public bool IsLate => CompletedAt.HasValue && CompletedAt.Value > DeadlineTime;

    /// <summary>
    /// Transitions task status from 'Pending' to 'Overdue' when current time exceeds deadline.
    /// Strictly adheres to GBR-07: Done tasks cannot be converted to Overdue, and Overdue tasks cannot be marked overdue twice.
    /// </summary>
    /// <param name="nowUtc">Current time in UTC</param>
    /// <returns>True if transition succeeded and event was raised; false otherwise.</returns>
    public bool MarkOverdue(DateTime nowUtc)
    {
        // 1. Never revert Done tasks
        if (string.Equals(Status, "Done", StringComparison.OrdinalIgnoreCase))
            return false;

        // 2. Do not mark already overdue task twice
        if (string.Equals(Status, "Overdue", StringComparison.OrdinalIgnoreCase))
            return false;

        // 3. Must be strictly Pending
        if (!string.Equals(Status, "Pending", StringComparison.OrdinalIgnoreCase))
            return false;

        // 4. Must strictly exceed deadline
        if (nowUtc <= DeadlineTime)
            return false;

        Status = "Overdue";
        OverdueFlaggedAt = nowUtc;

        _domainEvents.Add(new DayOfTaskOverdueEvent(
            TaskId: DayOfTaskId,
            ProjectId: ProjectId,
            ProjectCode: Project?.ProjectCode ?? "",
            TaskName: TaskName,
            DeadlineTime: DeadlineTime,
            OverdueAt: nowUtc,
            ManagerId: Project?.ManagerId ?? 0,
            HallName: Project?.Hall?.Name ?? "",
            EventDate: Project?.EventDate ?? default
        ));

        return true;
    }

    /// <summary>A task the operator added by hand (generated tasks carry the key of the service they come from).</summary>
    public bool IsCustom => SourceKey is null;

    public bool IsDone => string.Equals(Status, "Done", StringComparison.OrdinalIgnoreCase);

    /// <summary>A task added by hand by the operator (UC-31), waiting to be done.</summary>
    public static DayOfTask AddByHand(int projectId, int createdBy, CoordinationTaskDetails details)
    {
        var task = new DayOfTask { ProjectId = projectId, Status = "Pending", CreatedBy = createdBy };
        task.Apply(details);
        return task;
    }

    /// <summary>Changes name, department, deadline, proof requirement or note. A deadline moved later clears the overdue flag; a done task stays done.</summary>
    public void Edit(CoordinationTaskDetails details, DateTime nowUtc)
    {
        Apply(details);
        if (string.Equals(Status, "Overdue", StringComparison.OrdinalIgnoreCase) && DeadlineTime > nowUtc)
        {
            Status = "Pending";
            OverdueFlaggedAt = null;
        }
    }

    /// <summary>Only a task added by hand, not done and without proof, can be deleted.</summary>
    public void EnsureCanDelete()
    {
        if (!IsCustom)
            throw new BusinessRuleException("Chỉ xóa được nhiệm vụ do người điều hành thêm. Nhiệm vụ sinh từ dịch vụ có thể sửa hạn hoặc bỏ yêu cầu minh chứng.");
        if (IsDone || ExecutionPhotos.Count > 0)
            throw new BusinessRuleException("Nhiệm vụ đã hoàn thành hoặc đã có minh chứng, không thể xóa.");
    }

    /// <summary>
    /// Completes the task (UC-30/UC-31) with 0–3 new proof files (at least 1 when proof is required).
    /// GBR-07: an Overdue task is cleared only by completing it; completed after the deadline, IsLate evaluates to true.
    /// </summary>
    /// <param name="userId">Manager or coordinator completing the task</param>
    /// <param name="nowUtc">Completion timestamp</param>
    /// <param name="photoCount">Number of proof files uploaded with the completion</param>
    /// <param name="note">Optional note kept on the task</param>
    public void Complete(int userId, DateTime nowUtc, int photoCount, string? note = null)
    {
        if (IsDone)
            throw new BusinessRuleException("Nhiệm vụ đã được hoàn thành trước đó.");
        if (PhotoRequired && photoCount < 1)
            throw new BusinessRuleException("Nhiệm vụ này yêu cầu từ 1 đến 3 ảnh minh chứng (MSG10).");
        if (photoCount > CoordinationTaskPolicy.MaxProofPerUpload)
            throw new BusinessRuleException("Nhiệm vụ này cho phép tối đa 3 ảnh minh chứng mỗi lần (MSG10).");

        Status = "Done";
        CompletedAt = nowUtc;
        CompletedBy = userId;
        if (Text.Clip(note, 500) is { } kept) Note = kept;
    }

    /// <summary>
    /// Undoes a completion marked by mistake: the task waits again (the overdue job flags it if its deadline has passed).
    /// The proof already uploaded stays on the task.
    /// </summary>
    public void Reopen()
    {
        if (!IsDone) throw new BusinessRuleException("Nhiệm vụ chưa hoàn thành nên không cần mở lại.");
        Status = "Pending";
        CompletedAt = null;
        CompletedBy = null;
        OverdueFlaggedAt = null;
    }

    /// <summary>Proof added afterwards (done or not): 1–3 files at a time, up to <see cref="CoordinationTaskPolicy.MaxProofPerTask"/> per task.</summary>
    public void EnsureCanAddProof(int fileCount)
    {
        if (fileCount is < 1 or > CoordinationTaskPolicy.MaxProofPerUpload)
            throw new BusinessRuleException("Mỗi lần tải lên 1–3 tệp.");
        if (ExecutionPhotos.Count + fileCount > CoordinationTaskPolicy.MaxProofPerTask)
            throw new BusinessRuleException($"Mỗi nhiệm vụ giữ tối đa {CoordinationTaskPolicy.MaxProofPerTask} minh chứng (đang có {ExecutionPhotos.Count}).");
    }

    private void Apply(CoordinationTaskDetails details)
    {
        TaskName = details.TaskName;
        Department = details.Department;
        DeadlineTime = details.DeadlineUtc;
        PhotoRequired = details.PhotoRequired;
        Note = details.Note;
    }
}
