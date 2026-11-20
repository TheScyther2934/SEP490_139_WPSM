using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Files;
using Infrastructure.Persistence.Entities;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Coordination tasks by department: preparation from the lock, the event-day timeline, tasks added by hand.
/// UC-30..UC-32, JOB-04, JOB-05 (SDS II.3, GBR-06, GBR-07).
/// </summary>
public sealed class DayOfRepository(WpsmDbContext db, IFileService files) : IDayOfRepository
{
    /// <summary>Proof files kept per task: 1–3 at completion, more can be added afterwards up to this limit.</summary>
    public const int MaxPhotosPerTask = 6;

    /// <summary>Preparation starts at the lock (the stage can be built days before), so tasks are open from then until settlement.</summary>
    private static bool CanAct(WeddingProject project) =>
        project.Status is Domain.Services.EventStatus.Locked or Domain.Services.EventStatus.Completed;

    private static string ClosedMessage(WeddingProject project) => project.Status == Domain.Services.EventStatus.Adjusting
        ? "Nhiệm vụ điều phối mở sau khi khóa dịch vụ."
        : "Sự kiện đã tất toán, đã đóng hoặc đã hủy, không thể cập nhật nhiệm vụ điều phối.";

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
            CanAddPhotos = CanAct(project),
            MaxPhotosPerTask = MaxPhotosPerTask,
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

    public async Task<DayOfBoardView?> LoadTimelineAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
        if (project is null) return null;
        if (project.Status != Domain.Services.EventStatus.Locked) throw new InvalidOperationException("Chỉ tạo nhiệm vụ điều phối khi sự kiện đã khóa dịch vụ và chưa diễn ra.");
        await CoordinationPlan.EnsureTasksAsync(db, project.ProjectId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBoardAsync(projectCode, cancellationToken);
    }

    public async Task<DayOfBoardView?> CreateTaskAsync(string projectCode, int managerId, CoordinationTaskCommand command, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
        if (project is null) return null;
        if (!CanAct(project)) throw new InvalidOperationException(ClosedMessage(project));
        var task = new DayOfTask { ProjectId = project.ProjectId, Status = "Pending", CreatedBy = managerId };
        Apply(task, project, command);
        db.DayOfTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBoardAsync(projectCode, cancellationToken);
    }

    public async Task<DayOfBoardView?> UpdateTaskAsync(string projectCode, int taskId, CoordinationTaskCommand command, CancellationToken cancellationToken = default)
    {
        var task = await FindTaskAsync(projectCode, taskId, cancellationToken);
        if (task is null) return null;
        if (!CanAct(task.Project)) throw new InvalidOperationException(ClosedMessage(task.Project));
        Apply(task, task.Project, command);
        // A deadline moved later clears the overdue flag; a done task stays done.
        if (task.Status == "Overdue" && task.DeadlineTime > DateTime.UtcNow)
        {
            task.Status = "Pending";
            task.OverdueFlaggedAt = null;
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetBoardAsync(projectCode, cancellationToken);
    }

    public async Task<DayOfBoardView?> DeleteTaskAsync(string projectCode, int taskId, CancellationToken cancellationToken = default)
    {
        var task = await FindTaskAsync(projectCode, taskId, cancellationToken);
        if (task is null) return null;
        if (!CanAct(task.Project)) throw new InvalidOperationException(ClosedMessage(task.Project));
        if (task.SourceKey is not null) throw new InvalidOperationException("Chỉ xóa được nhiệm vụ do người điều hành thêm. Nhiệm vụ sinh từ dịch vụ có thể sửa hạn hoặc bỏ yêu cầu minh chứng.");
        if (task.Status == "Done" || task.ExecutionPhotos.Count > 0) throw new InvalidOperationException("Nhiệm vụ đã hoàn thành hoặc đã có minh chứng, không thể xóa.");
        db.DayOfTasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBoardAsync(projectCode, cancellationToken);
    }

    public async Task<DayOfBoardView?> CompleteTaskAsync(string projectCode, int taskId, int managerId, IReadOnlyList<ProofPhotoUpload> photos, string? note, CancellationToken cancellationToken = default)
    {
        var task = await FindTaskAsync(projectCode, taskId, cancellationToken);
        if (task is null) return null;
        if (!CanAct(task.Project)) throw new InvalidOperationException(ClosedMessage(task.Project));
        if (task.Status == "Done") throw new InvalidOperationException("Nhiệm vụ đã hoàn thành.");
        if (photos.Count > 3 || (task.PhotoRequired && photos.Count == 0))
            throw new InvalidOperationException("Vui lòng tải lên 1–3 ảnh hoặc tài liệu minh chứng để hoàn thành nhiệm vụ này (MSG10).");

        var now = DateTime.UtcNow;
        await StoreProofAsync(task, managerId, photos, now, cancellationToken);
        // GBR-07: an Overdue task is cleared only by completing it; IsLate is derived from CompletedAt > DeadlineTime.
        task.Status = "Done";
        task.CompletedAt = now;
        task.CompletedBy = managerId;
        if (!string.IsNullOrWhiteSpace(note)) task.Note = Trim(note, 500);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBoardAsync(projectCode, cancellationToken);
    }

    public async Task<DayOfBoardView?> AddPhotosAsync(string projectCode, int taskId, int managerId, IReadOnlyList<ProofPhotoUpload> photos, CancellationToken cancellationToken = default)
    {
        var task = await FindTaskAsync(projectCode, taskId, cancellationToken);
        if (task is null) return null;
        if (!CanAct(task.Project)) throw new InvalidOperationException(ClosedMessage(task.Project));
        if (photos.Count is 0 or > 3) throw new InvalidOperationException("Mỗi lần tải lên 1–3 tệp.");
        if (task.ExecutionPhotos.Count + photos.Count > MaxPhotosPerTask)
            throw new InvalidOperationException($"Mỗi nhiệm vụ giữ tối đa {MaxPhotosPerTask} minh chứng (đang có {task.ExecutionPhotos.Count}).");
        await StoreProofAsync(task, managerId, photos, DateTime.UtcNow, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBoardAsync(projectCode, cancellationToken);
    }

    public async Task<int> LoadTodayAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var projectIds = await db.WeddingProjects.Where(x => x.EventDate == today && x.Status == "Locked").Select(x => x.ProjectId).ToListAsync(cancellationToken);
        var loaded = 0;
        foreach (var projectId in projectIds)
            if (await CoordinationPlan.EnsureTasksAsync(db, projectId, cancellationToken) > 0) loaded++;
        await db.SaveChangesAsync(cancellationToken);
        return loaded;
    }

    public async Task<int> DetectOverdueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var tasks = await db.DayOfTasks.Include(x => x.Project)
            .Where(x => x.Status == "Pending" && x.DeadlineTime < utcNow
                && (x.Project.Status == Domain.Services.EventStatus.Locked || x.Project.Status == Domain.Services.EventStatus.Completed))
            .ToListAsync(cancellationToken);
        foreach (var task in tasks)
        {
            task.Status = "Overdue";
            task.OverdueFlaggedAt = utcNow;
            var eventDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Utc(task.DeadlineTime), VnTz)) >= task.Project.EventDate;
            Notify(db, task.Project.ManagerId, "Overdue alert",
                $"Nhiệm vụ điều phối \"{task.TaskName}\" của {task.Project.ProjectCode} đã quá hạn từ {VnTime(task.DeadlineTime)} (MSG21).",
                eventDay ? Links.ManagerDayOf(task.Project.ProjectCode) : Links.ManagerDepartmentSheets(task.Project.ProjectCode));
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

    private Task<DayOfTask?> FindTaskAsync(string projectCode, int taskId, CancellationToken cancellationToken) =>
        db.DayOfTasks.Include(x => x.Project).Include(x => x.ExecutionPhotos)
            .FirstOrDefaultAsync(x => x.DayOfTaskId == taskId && x.Project.ProjectCode == projectCode, cancellationToken);

    // Deadline between the lock (or now, for a project locked earlier) and the end of the event day.
    private static void Apply(DayOfTask task, WeddingProject project, CoordinationTaskCommand command)
    {
        var name = (command.TaskName ?? "").Trim();
        if (name.Length is < 3 or > 200) throw new InvalidOperationException("Tên nhiệm vụ cần từ 3 đến 200 ký tự.");
        if (!IsDepartment(command.Department)) throw new InvalidOperationException("Bộ phận không hợp lệ.");
        var deadline = Utc(command.Deadline.Kind == DateTimeKind.Local ? command.Deadline.ToUniversalTime() : command.Deadline);
        var lastMoment = VnToUtc(project.EventDate, new TimeOnly(23, 59));
        if (deadline > lastMoment) throw new InvalidOperationException("Hạn nhiệm vụ không được sau ngày cưới.");
        if (project.LockedAt is { } locked && deadline < Utc(locked).AddDays(-1))
            throw new InvalidOperationException("Hạn nhiệm vụ không được trước ngày khóa dịch vụ.");
        task.TaskName = name;
        task.Department = command.Department;
        task.DeadlineTime = deadline;
        task.PhotoRequired = command.PhotoRequired;
        task.Note = string.IsNullOrWhiteSpace(command.Note) ? null : Trim(command.Note, 500);
    }

    private async Task StoreProofAsync(DayOfTask task, int managerId, IReadOnlyList<ProofPhotoUpload> photos, DateTime now, CancellationToken cancellationToken)
    {
        // Checked for all files first, so nothing is stored when one of them is refused.
        if (photos.Any(x => x.Content.LongLength is 0 or > FileService.MaxBytes || FileTypes.Detect(x.Content) is null))
            throw new InvalidOperationException("Minh chứng chỉ nhận ảnh JPG, PNG, HEIC (iPhone), WebP hoặc tài liệu PDF, tối đa 10 MB mỗi tệp (MSG17).");
        foreach (var photo in photos)
        {
            var file = await files.StoreAsync(task.ProjectId, "ProofPhoto", photo.FileName, photo.Content, managerId, cancellationToken);
            task.ExecutionPhotos.Add(new ExecutionPhoto
            {
                UploaderId = managerId, UploadedAt = now, FileId = file.Id, ImageFilePath = file.StorageKey,
                FileName = Trim(file.OriginalName, 255), ContentType = file.ContentType
            });
        }
    }

    private static string Trim(string value, int max) { value = value.Trim(); return value.Length > max ? value[..max] : value; }

    private static bool IsImageType(string? contentType) => contentType is null || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}
