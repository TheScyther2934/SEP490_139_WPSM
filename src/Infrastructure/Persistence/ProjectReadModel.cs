using Microsoft.EntityFrameworkCore;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.Infrastructure.Persistence;

public sealed class ProjectReadModel(WpsmDbContext db)
{
    internal IQueryable<WeddingProject> ProjectViewsQuery() => db.WeddingProjects.AsNoTracking().AsSplitQuery()
        .Include(x => x.Customer).Include(x => x.Hall).Include(x => x.Slot)
        .Include(x => x.SpecItems).ThenInclude(x => x.CatalogItem)
        .Include(x => x.ChangeRequests).Include(x => x.Beos).Include(x => x.Payments);

    internal async Task<WeddingProjectView> MapProjectAsync(WeddingProject x, CancellationToken cancellationToken)
    {
        var totalTasks = await db.RoadmapTasks.CountAsync(t => t.ProjectId == x.ProjectId, cancellationToken);
        var completedTasks = await db.RoadmapTasks.CountAsync(t => t.ProjectId == x.ProjectId && t.Status == "Done", cancellationToken);
        var paid = x.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount ?? 0m);
        var spec = x.SpecItems.OrderBy(s => s.CatalogItem.Category).ThenBy(s => s.CatalogItem.Name)
            .Select(s => new SpecificationLineView(s.CatalogItemId, s.CatalogItem.Name, WpsmCategoryNames.ToDisplay(s.CatalogItem.Category), s.Description,
                s.Quantity, s.CatalogItem.Unit, s.UnitPrice, s.LineTotal ?? s.Quantity * s.UnitPrice)).ToList();
        var summary = spec.Count == 0 ? "Chưa nhập đặc tả" : $"{spec.Count} hạng mục đã cấu hình";
        var guestCount = spec.FirstOrDefault(s => s.Category == "Ẩm thực")?.Quantity ?? 0;
        return new WeddingProjectView(x.ProjectCode, x.Customer.FullName, x.Hall.Name, x.Slot.SlotName, x.EventDate,
            x.BaseContractValue, x.Nrdamount, x.Status,
            totalTasks == 0 ? 0 : (int)Math.Round(completedTasks * 100d / totalTasks),
            x.ChangeRequests.Count(c => c.Status is "Draft" or "Negotiating"),
            x.Beos.OrderByDescending(b => b.Version).FirstOrDefault()?.Status ?? "Not started",
            Math.Max(0, x.CurrentTotal - paid), summary, guestCount, spec);
    }
}
