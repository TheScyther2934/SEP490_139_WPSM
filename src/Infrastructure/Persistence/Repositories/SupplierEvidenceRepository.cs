using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Stage 3: once services are locked the operator books the outside suppliers and attaches evidence (transfer note,
/// invoice or contract). The checklist lists every locked service that needs a supplier; no purchase-order tracking.
/// </summary>
public sealed class SupplierEvidenceRepository(WpsmDbContext db, IFileStorage storage) : ISupplierEvidenceRepository
{
    private static readonly HashSet<string> Types = ["Transfer", "Invoice", "Contract", "Other"];
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png"
    };

    public async Task<SupplierBoardView?> GetBoardAsync(string eventCode, CancellationToken cancellationToken = default)
    {
        var p = await db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(x => x.SpecItems).ThenInclude(s => s.CatalogItem)
            .Include(x => x.SupplierEvidences).ThenInclude(e => e.File)
            .Include(x => x.SupplierEvidences).ThenInclude(e => e.UploadedByNavigation)
            .FirstOrDefaultAsync(x => x.ProjectCode == eventCode, cancellationToken);
        if (p is null) return null;
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var today = VietnamTime.GetVietnamToday();
        var deadline = EventStageRules.SupplierDeadline(p.EventDate, policy);
        var lines = p.SpecItems.Where(s => s.CatalogItem.RequiresSupplier).OrderBy(s => s.CatalogItem.Category).ThenBy(s => s.CatalogItem.Name)
            .Select(s => new SupplierChecklistLine(s.SpecItemId, s.CatalogItem.Name, WpsmCategoryNames.ToDisplay(s.CatalogItem.Category),
                p.SupplierEvidences.Count(e => e.SpecItemId == s.SpecItemId))).ToList();
        var names = p.SpecItems.ToDictionary(s => s.SpecItemId, s => s.CatalogItem.Name);
        var evidences = p.SupplierEvidences.OrderByDescending(e => e.UploadedAt)
            .Select(e => new SupplierEvidenceView(e.EvidenceId, e.SupplierName, e.EvidenceType, e.SpecItemId,
                e.SpecItemId is int id && names.TryGetValue(id, out var n) ? n : null, e.Note, e.FileId, e.File.OriginalName,
                e.UploadedByNavigation.FullName, DateTime.SpecifyKind(e.UploadedAt, DateTimeKind.Utc))).ToList();
        return new SupplierBoardView(p.ProjectCode, p.Status, EventStageRules.LockDate(p.EventDate, policy), deadline,
            p.Status is EventStatus.Locked or EventStatus.Completed, p.Status == EventStatus.Locked && today > deadline && lines.Any(l => l.EvidenceCount == 0),
            lines, evidences);
    }

    public async Task<SupplierEvidenceView> AddAsync(string eventCode, int userId, SupplierEvidenceCommand command, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.Include(p => p.SpecItems).FirstOrDefaultAsync(p => p.ProjectCode == eventCode, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy sự kiện.");
        if (project.Status is not (EventStatus.Locked or EventStatus.Completed))
            throw new InvalidOperationException("Minh chứng nhà cung cấp được tải lên sau khi sự kiện đã khóa dịch vụ (giai đoạn 3).");
        var supplier = command.SupplierName?.Trim() ?? "";
        if (supplier.Length is < 2 or > 150) throw new InvalidOperationException("Tên nhà cung cấp cần từ 2 đến 150 ký tự.");
        if (!Types.Contains(command.EvidenceType)) throw new InvalidOperationException("Loại minh chứng phải là chuyển khoản, hóa đơn, hợp đồng hoặc khác.");
        if (command.SpecItemId is int specId && project.SpecItems.All(s => s.SpecItemId != specId))
            throw new InvalidOperationException("Dịch vụ không thuộc sự kiện này.");
        if (command.Note is { Length: > 500 }) throw new InvalidOperationException("Ghi chú tối đa 500 ký tự.");
        if (command.Content is not { Length: > 0 }) throw new InvalidOperationException("Hãy chọn tệp minh chứng.");
        if (command.Content.Length > 10 * 1024 * 1024) throw new InvalidOperationException("Tệp tối đa 10 MB.");
        var ext = Path.GetExtension(command.FileName ?? "").ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(ext, out var contentType)) throw new InvalidOperationException("Chỉ nhận tệp PDF, JPG hoặc PNG.");

        var key = $"suppliers/{project.ProjectCode}/{Guid.NewGuid():N}{ext}";
        using (var stream = new MemoryStream(command.Content)) await storage.UploadAsync(stream, key, cancellationToken);
        var file = new StoredFile
        {
            FileId = Guid.NewGuid(), ProjectId = project.ProjectId, Purpose = "SupplierEvidence", StorageKey = key,
            OriginalName = Path.GetFileName(command.FileName ?? $"minh-chung{ext}"), ContentType = contentType, SizeBytes = command.Content.Length,
            UploadedBy = userId, UploadedAt = DateTime.UtcNow
        };
        db.StoredFiles.Add(file);
        var evidence = new SupplierEvidence
        {
            ProjectId = project.ProjectId, SpecItemId = command.SpecItemId, SupplierName = supplier, EvidenceType = command.EvidenceType,
            FileId = file.FileId, Note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim(), UploadedBy = userId, UploadedAt = DateTime.UtcNow
        };
        db.SupplierEvidences.Add(evidence);
        await db.SaveChangesAsync(cancellationToken);

        var board = await GetBoardAsync(eventCode, cancellationToken);
        return board!.Evidences.Single(e => e.Id == evidence.EvidenceId);
    }

    public async Task<(byte[] Content, string ContentType, string FileName)?> GetFileAsync(string eventCode, int evidenceId, CancellationToken cancellationToken = default)
    {
        var file = await db.SupplierEvidences.AsNoTracking().Where(e => e.EvidenceId == evidenceId && e.Project.ProjectCode == eventCode)
            .Select(e => e.File).FirstOrDefaultAsync(cancellationToken);
        if (file is null) return null;
        await using var stream = await storage.OpenReadAsync(file.StorageKey, cancellationToken);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return (ms.ToArray(), file.ContentType, file.OriginalName);
    }
}
