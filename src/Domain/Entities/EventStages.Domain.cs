using Domain.Rules;
using Domain.Services;

namespace Domain.Entities;

public partial class StageNote
{
    /// <summary>A note of the stage the event is in; an adjustment note also keeps the total before and after.</summary>
    public static StageNote Write(int projectId, int stage, string noteType, string content, int userId, DateTime nowUtc,
        decimal? valueBefore = null, decimal? valueAfter = null) => new()
    {
        ProjectId = projectId, Stage = (byte)stage, NoteType = noteType, Content = content,
        ValueBefore = valueBefore, ValueAfter = valueAfter, CreatedBy = userId, CreatedAt = nowUtc
    };
}

public partial class SupplierEvidence
{
    public static SupplierEvidence Attach(int projectId, int? specItemId, string supplier, string evidenceType, Guid fileId, string? note, int userId, DateTime nowUtc) => new()
    {
        ProjectId = projectId, SpecItemId = specItemId, SupplierName = supplier, EvidenceType = evidenceType,
        FileId = fileId, Note = note, UploadedBy = userId, UploadedAt = nowUtc
    };
}

public partial class WeddingProject
{
    /// <summary>
    /// The customer cancels (reason checked by EventStagePolicy.ValidateCancel): the hall and slot are freed, addon contracts still
    /// waiting for the customer are cancelled, resource allocations are released. The deposit is kept. Needs AddonContracts and
    /// ResourceAllocations loaded.
    /// </summary>
    public void Cancel(int userId, string reason, DateTime nowUtc)
    {
        Status = EventStatus.Cancelled;
        CancelledAt = nowUtc;
        CancelledBy = userId;
        CancelReason = reason;
        foreach (var addon in AddonContracts.Where(a => a.Status == AddonContractPolicy.PendingCustomer))
        {
            addon.Status = AddonContractPolicy.Cancelled;
            addon.CancelledAt = nowUtc;
            addon.CancelReason = "Sự kiện đã hủy";
        }
        foreach (var allocation in ResourceAllocations.Where(a => a.Status != ResourceAllocationPolicy.Released)) allocation.Release();
    }

    /// <summary>Locked services booked from an outside supplier that still have no evidence. Needs SpecItems (with CatalogItem) and SupplierEvidences.</summary>
    public IReadOnlyList<string> ServicesMissingSupplierEvidence() =>
        SpecItems.Where(s => s.CatalogItem.RequiresSupplier && SupplierEvidences.All(e => e.SpecItemId != s.SpecItemId)).Select(s => s.CatalogItem.Name).ToList();

    /// <summary>
    /// Stage 2: the service lines become exactly the adjusted ones (checked by EventStagePolicy) and the total follows. Needs SpecItems
    /// loaded. Returns the lines dropped from the contract, for the store to delete.
    /// </summary>
    public IReadOnlyList<SpecItem> AdjustServices(IReadOnlyList<AdjustedLine> lines)
    {
        var wanted = lines.Select(x => x.CatalogItemId).ToHashSet();
        var dropped = SpecItems.Where(x => !wanted.Contains(x.CatalogItemId)).ToList();
        foreach (var line in dropped) SpecItems.Remove(line);
        foreach (var item in lines)
        {
            var line = SpecItems.FirstOrDefault(x => x.CatalogItemId == item.CatalogItemId);
            if (line is null)
                SpecItems.Add(new SpecItem { ProjectId = ProjectId, CatalogItemId = item.CatalogItemId, Description = item.Description.Trim(),
                    Quantity = item.Quantity, UnitPrice = item.UnitPrice });
            else
            {
                line.Description = item.Description.Trim();
                line.Quantity = item.Quantity;
                line.UnitPrice = item.UnitPrice;
            }
        }
        CurrentTotal = lines.Sum(i => i.Quantity * i.UnitPrice);
        return dropped;
    }
}
