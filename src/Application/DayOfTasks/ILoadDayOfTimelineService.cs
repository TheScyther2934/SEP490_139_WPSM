using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.DayOfTasks;

public sealed record LoadDayOfTimelineCommand(
    int? ProjectId = null,
    DateOnly? TargetDate = null);

public sealed record LoadDayOfTimelineResult(
    Guid ExecutionId,
    DateTime StartedAt,
    DateTime CompletedAt,
    int ProjectsEvaluated,
    int ProjectsProcessed,
    int TasksCreated,
    int SkippedProjects,
    IReadOnlyList<string> Errors);

/// <summary>
/// Service contract for JOB-04: Load Day-of Timeline.
/// Generates Day-of operational tasks from Frozen BEO / slot timeline for event day operations.
/// </summary>
public interface ILoadDayOfTimelineService
{
    Task<LoadDayOfTimelineResult> LoadTimelineAsync(
        LoadDayOfTimelineCommand? command = null,
        CancellationToken cancellationToken = default);
}
