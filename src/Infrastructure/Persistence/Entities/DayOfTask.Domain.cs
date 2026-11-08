using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Events;

namespace Infrastructure.Persistence.Entities;

public partial class DayOfTask
{
    private readonly List<DayOfTaskOverdueEvent> _domainEvents = new();

    /// <summary>
    /// Pending domain events raised by entity lifecycle transitions.
    /// </summary>
    [NotMapped]
    public IReadOnlyCollection<DayOfTaskOverdueEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Indicates whether this task was completed late (CompletedAt strictly after DeadlineTime).
    /// Preserved even after status is Done.
    /// </summary>
    [NotMapped]
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

    /// <summary>
    /// Completes the task (UC-30/UC-31).
    /// If completed after deadline, IsLate will evaluate to true.
    /// </summary>
    /// <param name="userId">Manager or coordinator completing the task</param>
    /// <param name="nowUtc">Completion timestamp</param>
    /// <param name="photoCount">Number of uploaded proof photos</param>
    public void Complete(int userId, DateTime nowUtc, int photoCount)
    {
        if (string.Equals(Status, "Done", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Nhiệm vụ đã được hoàn thành trước đó.");

        if (PhotoRequired && photoCount < 1)
            throw new InvalidOperationException("Nhiệm vụ này yêu cầu từ 1 đến 3 ảnh minh chứng.");

        if (PhotoRequired && photoCount > 3)
            throw new InvalidOperationException("Nhiệm vụ này cho phép tối đa 3 ảnh minh chứng.");

        Status = "Done";
        CompletedAt = nowUtc;
    }
}
