using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Application.DayOfTasks;
using Application.Persistence;
using Infrastructure.Files;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.DayOfTasks;

/// <summary>
/// Day-of Operations service implementation (FE-08, FE-09, M-16, M-17, UC-30, UC-31).
/// </summary>
public sealed class DayOfTaskService(
    WpsmDbContext db,
    IFileStorage storage) : IDayOfTaskService
{
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];

    public async Task<IReadOnlyList<DayOfTaskDto>?> GetDayOfTasksAsync(
        string projectCode,
        int userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectCode == projectCode, cancellationToken);

        if (project is null) return null;

        // Authorization check: Manager must own the project, Admin has full system access
        if (string.Equals(userRole, "Manager", StringComparison.OrdinalIgnoreCase) && project.ManagerId != userId)
        {
            return null;
        }

        var nowUtc = DateTime.UtcNow;
        var tasks = await db.DayOfTasks
            .AsNoTracking()
            .Include(t => t.ExecutionPhotos)
                .ThenInclude(p => p.Uploader)
            .Where(t => t.ProjectId == project.ProjectId)
            .OrderBy(t => t.DeadlineTime)
            .ThenBy(t => t.DayOfTaskId)
            .ToListAsync(cancellationToken);

        return tasks.Select(t =>
        {
            var isLate = t.IsLate;
            var overdueMinutes = (t.Status == "Overdue" && t.CompletedAt == null)
                ? Math.Max(0, (int)(nowUtc - t.DeadlineTime).TotalMinutes)
                : 0;

            var photos = t.ExecutionPhotos.OrderBy(p => p.UploadedAt).Select(p => new DayOfTaskPhotoDto(
                PhotoId: p.PhotoId,
                ImageUrl: $"/api/wpsm/admin/photos/{p.PhotoId}/content",
                UploadedAt: DateTime.SpecifyKind(p.UploadedAt, DateTimeKind.Utc),
                UploaderName: p.Uploader?.FullName ?? "Unknown"
            )).ToList();

            return new DayOfTaskDto(
                TaskId: t.DayOfTaskId,
                ProjectId: t.ProjectId,
                ProjectCode: project.ProjectCode,
                TaskName: t.TaskName,
                DeadlineTime: DateTime.SpecifyKind(t.DeadlineTime, DateTimeKind.Utc),
                CompletedAt: t.CompletedAt.HasValue ? DateTime.SpecifyKind(t.CompletedAt.Value, DateTimeKind.Utc) : null,
                Status: t.Status,
                PhotoRequired: t.PhotoRequired,
                IsLate: isLate,
                OverdueMinutes: overdueMinutes,
                Photos: photos
            );
        }).ToList();
    }

    public async Task<CompleteDayOfTaskResult> CompleteDayOfTaskAsync(
        CompleteDayOfTaskCommand command,
        CancellationToken cancellationToken = default)
    {
        var task = await db.DayOfTasks
            .Include(t => t.Project)
            .Include(t => t.ExecutionPhotos)
            .FirstOrDefaultAsync(t => t.DayOfTaskId == command.TaskId, cancellationToken);

        if (task is null)
        {
            return new CompleteDayOfTaskResult(false, "Không tìm thấy nhiệm vụ ngày sự kiện.");
        }

        // Authorization check: GBR-12
        if (string.Equals(command.UserRole, "Manager", StringComparison.OrdinalIgnoreCase) &&
            task.Project.ManagerId != command.UserId)
        {
            return new CompleteDayOfTaskResult(false, "Bạn không có quyền thực hiện nhiệm vụ của dự án này.");
        }

        // Must not already be completed
        if (string.Equals(task.Status, "Done", StringComparison.OrdinalIgnoreCase))
        {
            return new CompleteDayOfTaskResult(false, "Nhiệm vụ đã được hoàn thành trước đó.");
        }

        var photos = command.Photos ?? Array.Empty<DayOfTaskUploadFile>();

        // Proof requirement validation
        if (task.PhotoRequired)
        {
            if (photos.Count < 1)
            {
                return new CompleteDayOfTaskResult(false, "Nhiệm vụ này yêu cầu từ 1 đến 3 ảnh minh chứng (MSG10).");
            }
            if (photos.Count > 3)
            {
                return new CompleteDayOfTaskResult(false, "Nhiệm vụ này chỉ cho phép tải lên tối đa 3 ảnh minh chứng.");
            }

            // Validate formats and size
            foreach (var photo in photos)
            {
                var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext) || photo.Length <= 0 || photo.Length > MaxFileBytes)
                {
                    return new CompleteDayOfTaskResult(false, "Ảnh không hợp lệ. Chỉ chấp nhận định dạng JPG/PNG dung lượng tối đa 10 MB mỗi ảnh (MSG17).");
                }
            }

            // Save uploaded photos
            foreach (var photoFile in photos)
            {
                var ext = Path.GetExtension(photoFile.FileName).ToLowerInvariant();
                var storageKey = $"proofs/{task.ProjectId}/{task.DayOfTaskId}_{Guid.NewGuid():N}{ext}";

                await storage.UploadAsync(photoFile.Stream, storageKey, cancellationToken);

                var executionPhoto = new ExecutionPhoto
                {
                    DayOfTaskId = task.DayOfTaskId,
                    UploaderId = command.UserId,
                    ImageFilePath = storageKey,
                    UploadedAt = DateTime.UtcNow
                };

                db.ExecutionPhotos.Add(executionPhoto);
                task.ExecutionPhotos.Add(executionPhoto);
            }
        }

        var nowUtc = DateTime.UtcNow;

        // Domain method Complete(userId, nowUtc, photoCount)
        task.Complete(command.UserId, nowUtc, photos.Count);

        await db.SaveChangesAsync(cancellationToken);

        // Map updated DTO
        var photosDto = task.ExecutionPhotos.OrderBy(p => p.UploadedAt).Select(p => new DayOfTaskPhotoDto(
            PhotoId: p.PhotoId,
            ImageUrl: $"/api/wpsm/admin/photos/{p.PhotoId}/content",
            UploadedAt: DateTime.SpecifyKind(p.UploadedAt, DateTimeKind.Utc),
            UploaderName: ""
        )).ToList();

        var dto = new DayOfTaskDto(
            TaskId: task.DayOfTaskId,
            ProjectId: task.ProjectId,
            ProjectCode: task.Project.ProjectCode,
            TaskName: task.TaskName,
            DeadlineTime: DateTime.SpecifyKind(task.DeadlineTime, DateTimeKind.Utc),
            CompletedAt: DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc),
            Status: "Done",
            PhotoRequired: task.PhotoRequired,
            IsLate: task.IsLate,
            OverdueMinutes: 0,
            Photos: photosDto
        );

        return new CompleteDayOfTaskResult(true, "Hoàn thành nhiệm vụ thành công.", dto);
    }
}
