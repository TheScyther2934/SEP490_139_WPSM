using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DayOfTasks;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.DayOfTasks;

/// <summary>
/// Implements JOB-04: Load Day-of Timeline.
/// Generates operational tasks for wedding projects with a Frozen BEO.
/// Guarantees idempotency by skipping projects that already have DayOfTasks loaded.
/// </summary>
public sealed class LoadDayOfTimelineService(
    WpsmDbContext db,
    ILogger<LoadDayOfTimelineService> logger) : ILoadDayOfTimelineService
{
    private static readonly HashSet<string> InactiveProjectStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Closed", "Settled", "Cancelled"
    };

    public async Task<LoadDayOfTimelineResult> LoadTimelineAsync(
        LoadDayOfTimelineCommand? command = null,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;
        var errors = new List<string>();

        var query = db.WeddingProjects
            .Include(p => p.Slot)
            .Include(p => p.Beos)
            .Include(p => p.DayOfTasks)
            .Where(p => !InactiveProjectStatuses.Contains(p.Status));

        if (command?.ProjectId.HasValue == true)
        {
            query = query.Where(p => p.ProjectId == command.ProjectId.Value);
        }
        else if (command?.TargetDate.HasValue == true)
        {
            query = query.Where(p => p.EventDate == command.TargetDate.Value);
        }
        else
        {
            // Default JOB-04 scope: projects taking place today or within next 3 days
            var today = RoadmapRules.GetVietnamToday();
            var maxDate = today.AddDays(3);
            query = query.Where(p => p.EventDate >= today && p.EventDate <= maxDate);
        }

        var candidateProjects = await query.ToListAsync(cancellationToken);
        int evaluated = candidateProjects.Count;
        int processed = 0;
        int tasksCreated = 0;
        int skipped = 0;

        foreach (var project in candidateProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 1. Idempotency: skip if project already has DayOfTasks
                if (project.DayOfTasks.Any())
                {
                    skipped++;
                    continue;
                }

                // 2. Must have a Frozen BEO (or if single project requested, allow if baseline frozen)
                var hasFrozenBeo = project.Beos.Any(b => string.Equals(b.Status, "Frozen", StringComparison.OrdinalIgnoreCase));
                if (!hasFrozenBeo && command?.ProjectId == null)
                {
                    // For scheduled background job, strictly require Frozen BEO
                    skipped++;
                    continue;
                }

                // 3. Determine base slot times
                var slotStartTime = project.Slot?.StartTime ?? new TimeOnly(11, 0);
                var slotEndTime = project.Slot?.EndTime ?? slotStartTime.AddHours(4);

                var setupDeadlineUtc = RoadmapRules.ConvertVietnamToUtc(
                    project.EventDate,
                    slotStartTime.AddHours(-1));

                var welcomeDeadlineUtc = RoadmapRules.ConvertVietnamToUtc(
                    project.EventDate,
                    slotStartTime);

                var serviceDeadlineUtc = RoadmapRules.ConvertVietnamToUtc(
                    project.EventDate,
                    slotStartTime.AddHours(1));

                var settlementDeadlineUtc = RoadmapRules.ConvertVietnamToUtc(
                    project.EventDate,
                    slotEndTime.AddMinutes(-30));

                var tasks = new[]
                {
                    new DayOfTask
                    {
                        ProjectId = project.ProjectId,
                        TaskName = "Kiểm tra set-up bàn tiệc & âm thanh ánh sáng",
                        DeadlineTime = setupDeadlineUtc,
                        Status = "Pending",
                        PhotoRequired = true
                    },
                    new DayOfTask
                    {
                        ProjectId = project.ProjectId,
                        TaskName = "Đón tiếp khách mời & điều phối khai tiệc",
                        DeadlineTime = welcomeDeadlineUtc,
                        Status = "Pending",
                        PhotoRequired = false
                    },
                    new DayOfTask
                    {
                        ProjectId = project.ProjectId,
                        TaskName = "Giám sát quy trình phục vụ tiệc theo BEO",
                        DeadlineTime = serviceDeadlineUtc,
                        Status = "Pending",
                        PhotoRequired = true
                    },
                    new DayOfTask
                    {
                        ProjectId = project.ProjectId,
                        TaskName = "Đối soát phát sinh và nghiệm thu tiệc",
                        DeadlineTime = settlementDeadlineUtc,
                        Status = "Pending",
                        PhotoRequired = false
                    }
                };

                db.DayOfTasks.AddRange(tasks);
                processed++;
                tasksCreated += tasks.Length;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var msg = $"Project {project.ProjectCode} timeline load failed: {ex.Message}";
                logger.LogError(ex, "{Msg}", msg);
                errors.Add(msg);
            }
        }

        if (tasksCreated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var completedAt = DateTime.UtcNow;
        logger.LogInformation(
            "JOB-04 [ExecutionId: {ExecutionId}] Load day-of timeline complete. Evaluated: {Evaluated}, Processed: {Processed}, TasksCreated: {Created}, Skipped: {Skipped}.",
            executionId, evaluated, processed, tasksCreated, skipped);

        return new LoadDayOfTimelineResult(
            ExecutionId: executionId,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            ProjectsEvaluated: evaluated,
            ProjectsProcessed: processed,
            TasksCreated: tasksCreated,
            SkippedProjects: skipped,
            Errors: errors);
    }
}
