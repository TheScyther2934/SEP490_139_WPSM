using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Entities;
using Domain.Rules;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Coordination tasks by department, read side: the board (preparation from the lock, the event-day timeline, tasks added by hand)
/// and the proof photos. UC-30..UC-32 (SDS II.3). Changes go through the use cases in Application/UseCases/CoordinationTasks.
/// </summary>
public sealed class DayOfRepository(WpsmDbContext db) : IDayOfRepository
{
    public async Task<DayOfBoardView?> GetBoardAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.AsNoTracking().Include(x => x.Customer).Include(x => x.Slot)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
        if (project is null) return null;
        var tasks = await db.DayOfTasks.AsNoTracking().AsSplitQuery()
            .Include(x => x.ExecutionPhotos).ThenInclude(p => p.Uploader)
            .Include(x => x.CompletedByUser)
            .Where(x => x.ProjectId == project.ProjectId)
            .OrderBy(x => x.DeadlineTime).ThenBy(x => x.DayOfTaskId).ToListAsync(cancellationToken);
        var beoStatus = await db.Beos.AsNoTracking().Where(x => x.ProjectId == project.ProjectId).OrderByDescending(x => x.Version)
            .Select(x => x.Status).FirstOrDefaultAsync(cancellationToken) ?? "None";
        return new DayOfBoardView(project.ProjectCode, project.Customer.FullName, project.EventDate,
            $"{project.Slot.StartTime:HH\\:mm} – {project.Slot.EndTime:HH\\:mm}", beoStatus,
            tasks.Select(x => new DayOfTaskView(x.DayOfTaskId, x.TaskName, Utc(x.DeadlineTime), x.Status, x.PhotoRequired,
                x.CompletedAt is { } done ? Utc(done) : null, x.CompletedAt > x.DeadlineTime,
                x.ExecutionPhotos.OrderBy(p => p.UploadedAt).Select(p => new DayOfPhotoView(p.PhotoId, Utc(p.UploadedAt))
                {
                    UploaderName = p.Uploader?.FullName, FileName = p.FileName, IsImage = IsImageType(p.ContentType)
                }).ToList())
            {
                CompletedBy = x.CompletedByUser?.FullName,
                Department = x.Department,
                Note = x.Note,
                IsCustom = x.SourceKey is null,
                IsEventDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Utc(x.DeadlineTime), VnTz)) >= project.EventDate
            }).ToList())
        {
            ProjectStatus = project.Status,
            CanAddPhotos = CoordinationTaskPolicy.IsOpen(project.Status),
            MaxPhotosPerTask = CoordinationTaskPolicy.MaxProofPerTask,
            LockedAt = project.LockedAt is { } locked ? Utc(locked) : null
        };
    }

    public async Task<IReadOnlyList<ProjectPhotoView>> GetPhotosAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var rows = await db.ExecutionPhotos.AsNoTracking()
            .Where(x => x.DayOfTask.Project.ProjectCode == projectCode && (x.ContentType == null || x.ContentType.StartsWith("image/")))
            .OrderByDescending(x => x.UploadedAt).ThenByDescending(x => x.PhotoId)
            .Select(x => new ProjectPhotoView(x.PhotoId, x.DayOfTask.TaskName, x.UploadedAt, x.Uploader.FullName)).ToListAsync(cancellationToken);
        return rows.Select(x => x with { UploadedAt = Utc(x.UploadedAt) }).ToList();
    }

    private static bool IsImageType(string? contentType) => contentType is null || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}
