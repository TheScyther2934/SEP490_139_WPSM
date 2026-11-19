using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>UC-30..UC-32, JOB-04, JOB-05 (SDS II.3, GBR-06, GBR-07).</summary>
public sealed class DayOfRepository(WpsmDbContext db, IFileService files) : IDayOfRepository
{
    /// <summary>Proof photos kept per task: 1–3 at completion, more can be added afterwards up to this limit.</summary>
    public const int MaxPhotosPerTask = 6;

    /// <summary>Photos are taken from the event day; the board stays open for them until the event is settled.</summary>
    private static bool CanAddPhotos(WeddingProject project) =>
        project.Status is Domain.Services.EventStatus.Locked or Domain.Services.EventStatus.Completed && TodayVn() >= project.EventDate;

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
                x.ExecutionPhotos.OrderBy(p => p.UploadedAt).Select(p => new DayOfPhotoView(p.PhotoId, Utc(p.UploadedAt)) { UploaderName = p.Uploader?.FullName }).ToList())
                { CompletedBy = x.CompletedByUser?.FullName }).ToList())
        {
            ProjectStatus = project.Status,
            CanAddPhotos = CanAddPhotos(project),
            MaxPhotosPerTask = MaxPhotosPerTask
        };
    }

    public async Task<IReadOnlyList<ProjectPhotoView>> GetPhotosAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var rows = await db.ExecutionPhotos.AsNoTracking().Where(x => x.DayOfTask.Project.ProjectCode == projectCode)
            .OrderByDescending(x => x.UploadedAt).ThenByDescending(x => x.PhotoId)
            .Select(x => new ProjectPhotoView(x.PhotoId, x.DayOfTask.TaskName, x.UploadedAt, x.Uploader.FullName)).ToListAsync(cancellationToken);
        return rows.Select(x => x with { UploadedAt = Utc(x.UploadedAt) }).ToList();
    }

    public async Task<DayOfBoardView?> LoadTimelineAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.Include(x => x.Slot).FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
        if (project is null) return null;
        if (project.Status != "Locked") throw new InvalidOperationException("Baseline chưa được chốt hoặc tiệc đã kết thúc.");
        if (project.EventDate.AddDays(-1) > TodayVn()) throw new InvalidOperationException("Chỉ tải lịch trình ngày cưới từ một ngày trước tiệc.");
        await AddTimelineAsync(project, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBoardAsync(projectCode, cancellationToken);
    }

    public async Task<DayOfBoardView?> CompleteTaskAsync(string projectCode, int taskId, int managerId, IReadOnlyList<ProofPhotoUpload> photos, CancellationToken cancellationToken = default)
    {
        var task = await db.DayOfTasks.Include(x => x.Project).Include(x => x.ExecutionPhotos)
            .FirstOrDefaultAsync(x => x.DayOfTaskId == taskId && x.Project.ProjectCode == projectCode, cancellationToken);
        if (task is null) return null;
        if (task.Status == "Done") throw new InvalidOperationException("Công việc đã hoàn thành.");
        if (photos.Count > 3 || (task.PhotoRequired && photos.Count == 0))
            throw new InvalidOperationException("Vui lòng tải lên 1–3 ảnh để hoàn thành công việc này (MSG10).");

        var now = DateTime.UtcNow;
        await StorePhotosAsync(task, managerId, photos, now, cancellationToken);
        // GBR-07: an Overdue task is cleared only by completing it; IsLate is derived from CompletedAt > DeadlineTime.
        task.Status = "Done";
        task.CompletedAt = now;
        task.CompletedBy = managerId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetBoardAsync(projectCode, cancellationToken);
    }

    public async Task<DayOfBoardView?> AddPhotosAsync(string projectCode, int taskId, int managerId, IReadOnlyList<ProofPhotoUpload> photos, CancellationToken cancellationToken = default)
    {
        var task = await db.DayOfTasks.Include(x => x.Project).Include(x => x.ExecutionPhotos)
            .FirstOrDefaultAsync(x => x.DayOfTaskId == taskId && x.Project.ProjectCode == projectCode, cancellationToken);
        if (task is null) return null;
        if (!CanAddPhotos(task.Project))
            throw new InvalidOperationException(TodayVn() < task.Project.EventDate
                ? "Chỉ tải ảnh minh chứng từ ngày cưới."
                : "Sự kiện đã tất toán hoặc đã đóng, không thể bổ sung ảnh minh chứng.");
        if (photos.Count is 0 or > 3) throw new InvalidOperationException("Mỗi lần tải lên 1–3 ảnh.");
        if (task.ExecutionPhotos.Count + photos.Count > MaxPhotosPerTask)
            throw new InvalidOperationException($"Mỗi công việc giữ tối đa {MaxPhotosPerTask} ảnh (đang có {task.ExecutionPhotos.Count} ảnh).");
        await StorePhotosAsync(task, managerId, photos, DateTime.UtcNow, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBoardAsync(projectCode, cancellationToken);
    }

    private async Task StorePhotosAsync(DayOfTask task, int managerId, IReadOnlyList<ProofPhotoUpload> photos, DateTime now, CancellationToken cancellationToken)
    {
        if (photos.Any(x => x.Content.Length is 0 or > 10 * 1024 * 1024 || !IsImage(x.Content)))
            throw new InvalidOperationException("Ảnh minh chứng chỉ nhận JPG hoặc PNG, tối đa 10 MB (MSG17).");
        foreach (var photo in photos)
        {
            var file = await files.StoreAsync(task.ProjectId, "ProofPhoto", photo.FileName, photo.Content, managerId, cancellationToken);
            task.ExecutionPhotos.Add(new ExecutionPhoto { UploaderId = managerId, UploadedAt = now, ImageFilePath = (await files.GetLocalPathAsync(file.Id, cancellationToken))! });
        }
    }

    public async Task<int> LoadTodayAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var projects = await db.WeddingProjects.Include(x => x.Slot).Where(x => x.EventDate == today && x.Status == "Locked")
            .ToListAsync(cancellationToken);
        var loaded = 0;
        foreach (var project in projects)
            if (await AddTimelineAsync(project, cancellationToken)) loaded++;
        await db.SaveChangesAsync(cancellationToken);
        return loaded;
    }

    public async Task<int> DetectOverdueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var tasks = await db.DayOfTasks.Include(x => x.Project).Where(x => x.Status == "Pending" && x.DeadlineTime < utcNow).ToListAsync(cancellationToken);
        foreach (var task in tasks)
        {
            task.Status = "Overdue";
            Notify(db, task.Project.ManagerId, "Overdue alert", $"Công việc \"{task.TaskName}\" đã quá hạn từ {VnTime(task.DeadlineTime)} (MSG21).",
                Links.ManagerDayOf(task.Project.ProjectCode));
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // The Manager completed a task while the job ran; the completion wins (SDS III.2.3).
            return 0;
        }
        return tasks.Count;
    }

    private static bool IsImage(byte[] content) =>
        content.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) || content.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

    // Template from SDS IV.4.3 relative to the slot start; skipped when the board already has tasks.
    private async Task<bool> AddTimelineAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        if (await db.DayOfTasks.AnyAsync(x => x.ProjectId == project.ProjectId, cancellationToken)) return false;
        var start = VnToUtc(project.EventDate, project.Slot.StartTime);
        foreach (var (offset, name, _, photoRequired) in DayOfTemplate)
            db.DayOfTasks.Add(new DayOfTask { ProjectId = project.ProjectId, TaskName = name, DeadlineTime = start.AddMinutes(offset), Status = "Pending", PhotoRequired = photoRequired });
        return true;
    }
}
