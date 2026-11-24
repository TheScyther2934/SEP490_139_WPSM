using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Application.UseCases.CoordinationTasks;
using Domain.Entities;
using Infrastructure.Files;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;

namespace Infrastructure.UseCases;

/// <summary>Coordination task data for the use cases: tracked entities, task generation from the locked services, proof storage.</summary>
public sealed class CoordinationTaskStore(WpsmDbContext db, IFileService files) : ICoordinationTaskStore
{
    public Task<WeddingProject?> GetEventAsync(string projectCode, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);

    public Task<DayOfTask?> GetTaskAsync(string projectCode, int taskId, CancellationToken cancellationToken = default) =>
        db.DayOfTasks.Include(x => x.Project).Include(x => x.ExecutionPhotos)
            .FirstOrDefaultAsync(x => x.DayOfTaskId == taskId && x.Project.ProjectCode == projectCode, cancellationToken);

    public void Add(DayOfTask task) => db.DayOfTasks.Add(task);

    public void Remove(DayOfTask task) => db.DayOfTasks.Remove(task);

    public Task<int> StageMissingTasksAsync(int projectId, CancellationToken cancellationToken = default) =>
        CoordinationPlan.EnsureTasksAsync(db, projectId, cancellationToken);

    public async Task StageProofAsync(DayOfTask task, int uploaderId, IReadOnlyList<ProofPhotoUpload> uploads, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // Checked for all files first, so nothing is stored when one of them is refused.
        if (uploads.Any(x => x.Content.LongLength is 0 or > FileService.MaxBytes || FileTypes.Detect(x.Content) is null))
            throw new InvalidOperationException("Minh chứng chỉ nhận ảnh JPG, PNG, HEIC (iPhone), WebP hoặc tài liệu PDF, tối đa 10 MB mỗi tệp (MSG17).");
        foreach (var upload in uploads)
        {
            var file = await files.StoreAsync(task.ProjectId, "ProofPhoto", upload.FileName, upload.Content, uploaderId, cancellationToken);
            task.ExecutionPhotos.Add(new ExecutionPhoto
            {
                UploaderId = uploaderId, UploadedAt = nowUtc, FileId = file.Id, ImageFilePath = file.StorageKey,
                FileName = Clip(file.OriginalName, 255), ContentType = file.ContentType
            });
        }
    }

    private static string Clip(string value, int max) { value = value.Trim(); return value.Length > max ? value[..max] : value; }
}
