using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence;

public sealed class ProjectReadModel(WpsmDbContext db)
{
    internal IQueryable<WeddingProject> ProjectViewsQuery() => db.WeddingProjects.AsNoTracking().AsSplitQuery()
        .Include(x => x.Customer).Include(x => x.Hall).Include(x => x.Slot)
        .Include(x => x.SpecItems).ThenInclude(x => x.CatalogItem)
        .Include(x => x.AddonContracts).ThenInclude(a => a.Items)
        .Include(x => x.Beos)
        .Include(x => x.Complaints).Include(x => x.DayOfTasks)
        .Include(x => x.Payments);

    internal async Task<WeddingProjectView> MapProjectAsync(WeddingProject x, CancellationToken cancellationToken)
    {
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var today = VietnamTime.GetVietnamToday();
        var payments = x.Payments.OrderBy(p => p.PaymentType == PaymentKinds.Deposit ? 0 : 1)
            .Select(p => new PaymentView(p.PaymentType, p.Status, p.Amount ?? 0m, p.DueDate, p.PaidDate, p.ReceiptContent != null || p.ReceiptFilePath != null))
            .ToList();
        var paid = payments.Where(p => p.Status == "Paid").Sum(p => p.Amount);
        var depositStatus = payments.FirstOrDefault(p => p.Type == PaymentKinds.Deposit)?.Status ?? "Unknown";

        var spec = x.SpecItems.OrderBy(s => s.CatalogItem?.Category).ThenBy(s => s.CatalogItem?.Name)
            .Select(s => new SpecificationLineView(s.CatalogItemId, s.CatalogItem?.Name ?? "", WpsmCategoryNames.ToDisplay(s.CatalogItem?.Category ?? ""), s.Description,
                s.Quantity, s.CatalogItem?.Unit ?? "", s.UnitPrice, s.LineTotal ?? s.Quantity * s.UnitPrice)).ToList();
        var summary = spec.Count == 0 ? "Chưa có dịch vụ" : $"{spec.Count} dịch vụ";
        var guestCount = spec.FirstOrDefault(s => s.Category == "Ẩm thực")?.Quantity ?? 0;

        var confirmedAddons = x.AddonContracts.Where(a => a.Status == "Confirmed").Sum(a => a.Items.Sum(i => i.Quantity * i.UnitPrice));
        var pendingAddons = x.AddonContracts.Count(a => a.Status == "PendingCustomer");
        var slotTime = $"{x.Slot.StartTime:HH:mm} - {x.Slot.EndTime:HH:mm}";
        // Credentials go through the outbox only (JOB-07); Queued is shown as Pending.
        var emailOutboxStatus = await db.EmailOutbox.Where(e => e.RelatedProjectId == x.ProjectId && e.Kind == "Credentials")
            .OrderByDescending(e => e.EmailId).Select(e => e.Status).FirstOrDefaultAsync(cancellationToken);
        var credentialStatus = emailOutboxStatus switch { null => "ExistingAccount", "Queued" => "Pending", var status => status };

        return new WeddingProjectView(x.ProjectCode, x.Customer.FullName, x.Hall.Name, x.Slot.SlotName, x.EventDate,
            x.BaseContractValue, x.DepositAmount, x.Status,
            EventStageRules.CurrentStage(x.Status, x.EventDate, today), pendingAddons,
            x.Beos.OrderByDescending(b => b.Version).FirstOrDefault()?.Status ?? "Not started",
            x.Status == EventStatus.Cancelled ? 0 : Math.Max(0, x.CurrentTotal + confirmedAddons - paid),
            summary, guestCount, spec, x.LockedAt, depositStatus, slotTime, x.AddonContracts.Count)
        {
            StatusLabel = EventStatus.Label(x.Status),
            LockDate = EventStageRules.LockDate(x.EventDate, policy),
            SupplierDeadline = EventStageRules.SupplierDeadline(x.EventDate, policy),
            MinimumTotal = EventStageRules.MinimumTotal(x.BaseContractValue, policy),
            CanAdjust = EventStageRules.CanAdjust(x.Status, x.EventDate, today, policy),
            CanAddAddon = EventStageRules.CanAddAddon(x.Status),
            ConfirmedAddonTotal = confirmedAddons,
            Stages = EventStageRules.Timeline(x.Status, x.EventDate, DateOnly.FromDateTime(x.CreatedAt), today, policy),
            CustomerEmail = x.Customer.Email,
            CustomerPhone = x.Customer.Phone ?? "",
            CurrentTotal = x.CurrentTotal,
            OpenComplaints = x.Complaints.Count(c => c.Status != "Resolved"),
            DayOfTotal = x.DayOfTasks.Count,
            DayOfCompleted = x.DayOfTasks.Count(t => t.Status == "Done"),
            DayOfOverdue = x.DayOfTasks.Count(t => t.Status == "Overdue"),
            DayOfMissingProof = x.DayOfTasks.Count(t => t.Status == "Done" && t.PhotoRequired && !t.ExecutionPhotos.Any()),
            Payments = payments,
            Addons = x.AddonContracts.OrderByDescending(a => a.CreatedAt)
                .Select(a => new ProjectModuleItem(a.AddonCode, a.Status, a.Title)).ToList(),
            Beos = x.Beos.OrderByDescending(b => b.Version).Select(b => new ProjectModuleItem($"Phiếu điều phối v{b.Version}", b.Status, b.Notes)).ToList(),
            Complaints = x.Complaints.Select(c => new ProjectModuleItem(c.ComplaintCode, c.Status, c.Description)).ToList(),
            DayOfTasks = x.DayOfTasks.OrderBy(t => t.DeadlineTime).Select(t => new ProjectModuleItem(t.TaskName, t.Status, t.DeadlineTime.ToString("dd/MM/yyyy HH:mm"))).ToList(),
            CredentialEmailStatus = credentialStatus
        };
    }
}
