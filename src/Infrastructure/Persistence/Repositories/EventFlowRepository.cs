using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Email;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads of the event stage flow: the operator workspace (stages, notes, addons, invoice), the customer's view of a booked event,
/// and the customer lookup used to create an addon contract.
/// </summary>
public sealed class EventFlowRepository(WpsmDbContext db) : IEventFlowRepository
{
    public async Task<EventWorkspaceView?> GetWorkspaceAsync(string eventCode, CancellationToken cancellationToken = default)
    {
        var p = await db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(x => x.Customer)
            .Include(x => x.StageNotes).ThenInclude(n => n.CreatedByNavigation)
            .Include(x => x.AddonContracts).ThenInclude(a => a.Items)
            .Include(x => x.AddonContracts).ThenInclude(a => a.CreatedByNavigation)
            .FirstOrDefaultAsync(x => x.ProjectCode == eventCode, cancellationToken);
        if (p is null) return null;
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var today = VietnamTime.GetVietnamToday();
        return new EventWorkspaceView(p.ProjectCode, p.Customer.FullName, p.Status, EventStatus.Label(p.Status),
            EventStageRules.CurrentStage(p.Status, p.EventDate, today),
            EventStageRules.Timeline(p.Status, p.EventDate, DateOnly.FromDateTime(p.CreatedAt), today, policy),
            p.BaseContractValue, p.CurrentTotal, EventStageRules.MinimumTotal(p.BaseContractValue, policy), p.DepositAmount,
            p.EventDate, EventStageRules.LockDate(p.EventDate, policy), EventStageRules.SupplierDeadline(p.EventDate, policy),
            EventStageRules.CanAdjust(p.Status, p.EventDate, today, policy), EventStageRules.CanAddAddon(p.Status),
            EventStatus.IsActive(p.Status) && today < p.EventDate,
            p.StageNotes.OrderByDescending(n => n.CreatedAt).Select(EventFlowSupport.MapNote).ToList(),
            p.AddonContracts.OrderByDescending(a => a.CreatedAt).Select(a => EventFlowSupport.MapAddon(a, p.ProjectCode)).ToList(),
            await EventFlowSupport.LoadContactsAsync(db, p.ProjectId, cancellationToken),
            await EventFlowSupport.LoadInvoiceViewAsync(db, p.ProjectId, p.ProjectCode, cancellationToken));
    }

    public async Task<IReadOnlyList<CustomerEventRef>> GetCustomerEventsAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var rows = await db.WeddingProjects.AsNoTracking().Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.EventDate).Select(x => new { x.ProjectCode, x.EventDate, Hall = x.Hall.Name, x.Status }).ToListAsync(cancellationToken);
        return rows.Select(x => new CustomerEventRef(x.ProjectCode, x.EventDate, x.Hall, x.Status, EventStatus.Label(x.Status), EventStageRules.CanAddAddon(x.Status))).ToList();
    }

    public async Task<CustomerEventView?> GetCustomerEventAsync(int customerId, string? eventCode, CancellationToken cancellationToken = default)
    {
        var query = db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(x => x.Customer).Include(x => x.Hall).Include(x => x.Slot)
            .Include(x => x.SpecItems).ThenInclude(s => s.CatalogItem)
            .Include(x => x.StageNotes).ThenInclude(n => n.CreatedByNavigation)
            .Include(x => x.AddonContracts).ThenInclude(a => a.Items)
            .Include(x => x.AddonContracts).ThenInclude(a => a.CreatedByNavigation)
            .Where(x => x.CustomerId == customerId);
        // Without a code: the next active event, otherwise the latest one.
        var p = string.IsNullOrWhiteSpace(eventCode)
            ? await query.OrderBy(x => x.Status == EventStatus.Cancelled ? 2 : x.Status == EventStatus.Adjusting || x.Status == EventStatus.Locked ? 0 : 1).ThenBy(x => x.EventDate).FirstOrDefaultAsync(cancellationToken)
            : await query.FirstOrDefaultAsync(x => x.ProjectCode == eventCode, cancellationToken);
        if (p is null) return null;

        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var today = VietnamTime.GetVietnamToday();
        var services = p.SpecItems.OrderBy(s => s.CatalogItem.Category).ThenBy(s => s.CatalogItem.Name)
            .Select(s => new SpecificationLineView(s.CatalogItemId, s.CatalogItem.Name, WpsmCategoryNames.ToDisplay(s.CatalogItem.Category), s.Description,
                s.Quantity, s.CatalogItem.Unit, s.UnitPrice, s.LineTotal ?? s.Quantity * s.UnitPrice)).ToList();
        var addons = p.AddonContracts.OrderByDescending(a => a.CreatedAt).Select(a => EventFlowSupport.MapAddon(a, p.ProjectCode)).ToList();
        return new CustomerEventView(p.ProjectCode, p.Customer.FullName, p.Hall.Name, p.Slot.SlotName, $"{p.Slot.StartTime:HH:mm} - {p.Slot.EndTime:HH:mm}",
            p.EventDate, Math.Max(0, p.EventDate.DayNumber - today.DayNumber), p.Status, EventStatus.Label(p.Status),
            EventStageRules.CurrentStage(p.Status, p.EventDate, today),
            EventStageRules.Timeline(p.Status, p.EventDate, DateOnly.FromDateTime(p.CreatedAt), today, policy),
            p.BaseContractValue, p.CurrentTotal, p.DepositAmount, addons.Where(a => a.Status == "Confirmed").Sum(a => a.Total),
            EventStageRules.CanAdjust(p.Status, p.EventDate, today, policy), EventStageRules.LockDate(p.EventDate, policy),
            services, p.StageNotes.OrderByDescending(n => n.CreatedAt).Select(EventFlowSupport.MapNote).ToList(), addons,
            await EventFlowSupport.LoadContactsAsync(db, p.ProjectId, cancellationToken), await EventFlowSupport.LoadHotlineAsync(db, cancellationToken),
            await EventFlowSupport.LoadInvoiceViewAsync(db, p.ProjectId, p.ProjectCode, cancellationToken));
    }

    public async Task<IReadOnlyList<CustomerLookupView>> SearchCustomersAsync(string? query, int userId, string role, CancellationToken cancellationToken = default)
    {
        // Deactivated customers are found too: an old customer may come back for a new event.
        var customers = db.Users.AsNoTracking().Where(u => u.Role == "Customer");
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            var like = SqlLike.Contains(term);
            customers = customers.Where(u => EF.Functions.ILike(u.FullName, like) || EF.Functions.ILike(u.Email, like) || (u.Phone != null && EF.Functions.ILike(u.Phone, like)));
        }
        var rows = await customers.OrderBy(u => u.FullName).Take(20)
            .Select(u => new
            {
                u.UserId, u.FullName, u.Email, u.Phone,
                // A Manager only sees the events they operate; Admin sees all.
                Events = u.WeddingProjectCustomers
                    .Where(p => role == "Admin" || p.ManagerId == userId || p.ProjectManagers.Any(m => m.ManagerId == userId && m.RemovedAt == null))
                    .OrderByDescending(p => p.EventDate)
                    .Select(p => new { p.ProjectCode, p.EventDate, Hall = p.Hall.Name, p.Status }).ToList()
            }).ToListAsync(cancellationToken);
        return rows.Select(r => new CustomerLookupView(r.UserId, r.FullName, r.Email, r.Phone,
            r.Events.Select(e => new CustomerEventRef(e.ProjectCode, e.EventDate, e.Hall, e.Status, EventStatus.Label(e.Status), EventStageRules.CanAddAddon(e.Status))).ToList()))
            .ToList();
    }

    // Stage notes, cancelling, locking and the supplier-deadline reminder are use cases: Application/UseCases/EventStages.
}
