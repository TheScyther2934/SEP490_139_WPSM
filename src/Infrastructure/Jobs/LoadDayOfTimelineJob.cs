using System.Threading;
using System.Threading.Tasks;
using Application.DayOfTasks;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job for JOB-04: Load Day-of Timeline.
/// Runs daily at 06:00 Vietnam time (05:00/23:00 UTC depending on TZ offset).
/// Evaluates upcoming active projects with Frozen BEO and generates Day-of tasks.
/// </summary>
public sealed class LoadDayOfTimelineJob(
    ILoadDayOfTimelineService loadTimelineService,
    ILogger<LoadDayOfTimelineJob> logger)
{
    [DisableConcurrentExecution(300)]
    [AutomaticRetry(Attempts = 1, LogEvents = true)]
    public async Task ExecuteAsync()
    {
        logger.LogInformation("Hangfire JOB-04: Starting Load Day-of Timeline execution.");
        var result = await loadTimelineService.LoadTimelineAsync(null, CancellationToken.None);
        logger.LogInformation(
            "Hangfire JOB-04: Completed. Evaluated: {Evaluated}, Processed: {Processed}, TasksCreated: {Created}, Skipped: {Skipped}.",
            result.ProjectsEvaluated, result.ProjectsProcessed, result.TasksCreated, result.SkippedProjects);
    }
}
