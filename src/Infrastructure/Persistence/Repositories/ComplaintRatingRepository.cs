using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Entities;
using Domain.Rules;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads of the post-event portal, ratings and complaints (C-12..C-16, M-18, M-19). Rating, filing, resolving and escalating are use
/// cases (Application/UseCases/PostEvent); the rules are PostEventPolicy and Complaint.
/// </summary>
public sealed class ComplaintRatingRepository(WpsmDbContext db) : IComplaintRatingRepository
{
    private static readonly (string Code, string Label)[] RatingModules =
    [
        ("Hall", "Sảnh tiệc & Không gian"),
        ("Menu", "Thực đơn & Ẩm thực"),
        ("Decor", "Trang trí & Hoa tươi"),
        ("MC", "MC & Điều phối chương trình")
    ];

    public async Task<PostEventSummaryView?> GetPostEventSummaryAsync(int customerId, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var project = await ResolveCustomerProjectAsync(customerId, projectCode, cancellationToken);
        return project is null ? null : MapPostEventSummary(project);
    }

    public async Task<IReadOnlyList<ComplaintView>> GetCustomerComplaintsAsync(int customerId, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = LoadComplaintsQuery().Where(c => c.Project.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(c => c.Project.ProjectCode == projectCode.Trim());

        var rows = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
        return rows.Select(MapComplaintView).ToList();
    }

    public async Task<ComplaintView?> GetCustomerComplaintAsync(int customerId, string complaintCode, CancellationToken cancellationToken = default)
    {
        var code = (complaintCode ?? "").Trim();
        var row = await LoadComplaintsQuery()
            .FirstOrDefaultAsync(c => c.Project.CustomerId == customerId &&
                (c.ComplaintCode == code || c.ComplaintId.ToString() == code), cancellationToken);
        return row is null ? null : MapComplaintView(row);
    }

    public async Task<IReadOnlyList<BeoOptionView>> GetCustomerBeoOptionsAsync(int customerId, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = db.WeddingProjects.AsNoTracking().Where(p => p.CustomerId == customerId);
        var project = !string.IsNullOrWhiteSpace(projectCode)
            ? await query.FirstOrDefaultAsync(p => p.ProjectCode == projectCode.Trim(), cancellationToken)
            : await query.OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        if (project is null) return [];

        return await db.Beos.AsNoTracking()
            .Where(b => b.ProjectId == project.ProjectId)
            .OrderByDescending(b => b.Version)
            .Select(b => new BeoOptionView(b.Beoid, b.Version, b.Status, b.FrozenAt, b.Notes))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplaintView>> GetManagerComplaintsAsync(int managerId, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = LoadComplaintsQuery().Where(c => (c.Project.ManagerId == managerId || c.Project.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)));
        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(c => c.Project.ProjectCode == projectCode.Trim());

        var rows = await query
            .OrderBy(c => c.Status == "Open" ? 0 : c.Status == "Escalated" ? 1 : 2)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(MapComplaintView).ToList();
    }

    public async Task<ComplaintView?> GetManagerComplaintAsync(int managerId, string complaintCode, CancellationToken cancellationToken = default)
    {
        var code = (complaintCode ?? "").Trim();
        var row = await LoadComplaintsQuery()
            .FirstOrDefaultAsync(c => (c.Project.ManagerId == managerId || c.Project.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)) &&
                (c.ComplaintCode == code || c.ComplaintId.ToString() == code), cancellationToken);
        return row is null ? null : MapComplaintView(row);
    }

    private async Task<WeddingProject?> ResolveCustomerProjectAsync(int customerId, string? projectCode, CancellationToken cancellationToken)
    {
        var query = db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(p => p.Customer)
            .Include(p => p.Hall)
            .Include(p => p.Slot)
            .Include(p => p.Ratings)
            .Include(p => p.Payments)
            .Include(p => p.DayOfTasks)
            .Include(p => p.Complaints)
            .Include(p => p.Beos)
            .Where(p => p.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(projectCode))
            return await query.FirstOrDefaultAsync(p => p.ProjectCode == projectCode.Trim(), cancellationToken);

        return await query.OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<Complaint> LoadComplaintsQuery() =>
        db.Complaints.AsNoTracking().AsSplitQuery()
            .Include(c => c.Beo)
            .Include(c => c.Project).ThenInclude(p => p.Customer)
            .Include(c => c.Project).ThenInclude(p => p.Hall)
            .Include(c => c.Project).ThenInclude(p => p.DayOfTasks).ThenInclude(t => t.ExecutionPhotos)
            .Include(c => c.Project).ThenInclude(p => p.SpecItems).ThenInclude(s => s.CatalogItem)
            .Include(c => c.Project).ThenInclude(p => p.AddonContracts).ThenInclude(a => a.Items).ThenInclude(i => i.CatalogItem);

    private static PostEventSummaryView MapPostEventSummary(WeddingProject project)
    {
        var existingByModule = project.Ratings.ToDictionary(r => r.Module, StringComparer.OrdinalIgnoreCase);
        var ratings = RatingModules.Select(m =>
        {
            existingByModule.TryGetValue(m.Code, out var r);
            return new ModuleRatingView(
                m.Code,
                m.Label,
                r is null ? null : r.Stars,
                r?.Comment,
                r is null ? null : DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc));
        }).ToList();

        var isLocked = project.Ratings.Count > 0;
        var paidTotal = project.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount ?? 0m);
        var completedTasks = project.DayOfTasks.Count(t => t.Status == "Done");
        var openComplaints = project.Complaints.Count(c => c.Status != "Resolved");

        return new PostEventSummaryView(
            project.ProjectCode,
            project.Customer.FullName,
            project.Hall.Name,
            project.Slot.SlotName,
            project.EventDate,
            project.Status,
            !isLocked && PostEventPolicy.CanRate(project.Status),
            isLocked,
            project.CurrentTotal,
            paidTotal,
            completedTasks,
            project.DayOfTasks.Count,
            openComplaints,
            ratings);
    }

    private static ComplaintView MapComplaintView(Complaint c)
    {
        var project = c.Project;
        static (string Dept, string Label) Department(string category)
        {
            var dept = category switch
            {
                "Catering" => "Kitchen",
                "Hall" or "Transport" or "Furniture" or "Ceremony" => "Service",
                "Decor" or "Attire" or "Makeup" => "Decor",
                _ => "Tech-MC"
            };
            return (dept, dept switch
            {
                "Kitchen" => "Bộ phận Bếp",
                "Service" => "Bộ phận Phục vụ bàn và sảnh",
                "Decor" => "Bộ phận Trang trí và hoa",
                _ => "Bộ phận Kỹ thuật âm thanh, ánh sáng và MC"
            });
        }

        // What was committed: the locked service lines plus the confirmed addon contracts.
        var beoItems = project.SpecItems.OrderBy(s => s.SpecItemId).Select(s =>
            {
                var (dept, label) = Department(s.CatalogItem.Category);
                return new BeoItemLineView(s.SpecItemId, s.CatalogItem.Name, s.CatalogItem.Category, dept, label, s.CatalogItem.Unit,
                    s.Quantity, s.Quantity, s.UnitPrice, s.Quantity * s.UnitPrice, s.Description ?? "", false);
            })
            .Concat(project.AddonContracts.Where(x => x.Status == "Confirmed").SelectMany(x => x.Items).Select(i =>
            {
                var (dept, label) = Department(i.CatalogItem?.Category ?? "Other");
                return new BeoItemLineView(-i.AddonItemId, i.CatalogItem?.Name ?? i.Description, i.CatalogItem?.Category ?? "Other", dept, label,
                    i.CatalogItem?.Unit ?? "", 0, i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice, i.Description, true);
            }))
            .ToList();

        var photosCount = project.DayOfTasks.SelectMany(t => t.ExecutionPhotos).Count();

        return new ComplaintView(
            c.ComplaintId,
            c.ComplaintCode,
            project.ProjectCode,
            project.Customer.FullName,
            project.Hall.Name,
            project.EventDate,
            c.Beoid,
            c.Beo.Version,
            c.Beo.Status,
            c.Beo.Notes,
            c.Description,
            c.Status,
            c.EscalationNote,
            c.EscalatedAt is { } esc ? DateTime.SpecifyKind(esc, DateTimeKind.Utc) : null,
            c.ResolutionNote,
            c.ResolvedAt is { } res ? DateTime.SpecifyKind(res, DateTimeKind.Utc) : null,
            c.ResolvedBy.HasValue ? (c.EscalatedAt.HasValue ? "Admin" : "Manager") : null,
            DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc),
            photosCount,
            beoItems);
    }
}
