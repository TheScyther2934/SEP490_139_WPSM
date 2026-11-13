using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Application.DayOfTasks;

public sealed record DayOfTaskPhotoDto(
    int PhotoId,
    string ImageUrl,
    DateTime UploadedAt,
    string UploaderName);

public sealed record DayOfTaskDto(
    int TaskId,
    int ProjectId,
    string ProjectCode,
    string TaskName,
    DateTime DeadlineTime,
    DateTime? CompletedAt,
    string Status,
    bool PhotoRequired,
    bool IsLate,
    int OverdueMinutes,
    IReadOnlyList<DayOfTaskPhotoDto> Photos);

public sealed record DayOfTaskUploadFile(
    Stream Stream,
    string FileName,
    string ContentType,
    long Length);

public sealed record CompleteDayOfTaskCommand(
    int TaskId,
    int UserId,
    string UserRole,
    IReadOnlyList<DayOfTaskUploadFile> Photos);

public sealed record CompleteDayOfTaskResult(
    bool Success,
    string Message,
    DayOfTaskDto? Task = null);

/// <summary>
/// Service contract for Day-of Operations &amp; Task Completion (M-16, M-17, UC-30, UC-31).
/// </summary>
public interface IDayOfTaskService
{
    Task<IReadOnlyList<DayOfTaskDto>?> GetDayOfTasksAsync(
        string projectCode,
        int userId,
        string userRole,
        CancellationToken cancellationToken = default);

    Task<CompleteDayOfTaskResult> CompleteDayOfTaskAsync(
        CompleteDayOfTaskCommand command,
        CancellationToken cancellationToken = default);
}
