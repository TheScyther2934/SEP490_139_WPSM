using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Services;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Stage 3: once services are locked the operator books the outside suppliers and attaches evidence (transfer note,
/// invoice or contract). The checklist lists every locked service that needs a supplier; no purchase-order tracking.
/// Read only: attaching evidence is a use case (Application/UseCases/EventStages/AddSupplierEvidence).
/// </summary>
public sealed class SupplierEvidenceRepository(WpsmDbContext db, IFileService files) : ISupplierEvidenceRepository
{
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

    public async Task<(byte[] Content, string ContentType, string FileName)?> GetFileAsync(string eventCode, int evidenceId, CancellationToken cancellationToken = default)
    {
        var file = await db.SupplierEvidences.AsNoTracking().Where(e => e.EvidenceId == evidenceId && e.Project.ProjectCode == eventCode)
            .Select(e => e.File).FirstOrDefaultAsync(cancellationToken);
        if (file is null) return null;
        return await files.ReadAsync(file.StorageKey, cancellationToken) is { } content ? (content, file.ContentType, file.OriginalName) : null;
    }
}
