using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class AdminProjectRepository(WpsmDbContext db) : IAdminProjectRepository
{
    private static readonly IReadOnlyList<string> Statuses = EventStatus.All;
    private static readonly string[] HeldStatuses = ["Completed", "Settled", "Closed"];

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value is { } v ? Utc(v) : null;
    private static PersonRef? Person(User? user) => user is null ? null : new PersonRef(user.UserId, user.FullName, user.Email, user.Role);
    // Primary first, then co-managers in the order they were added.
    private static IReadOnlyList<PersonRef> Operators(User primary, IEnumerable<User> coManagers) =>
        [Person(primary)!, .. coManagers.Where(u => u.UserId != primary.UserId).Select(u => Person(u)!)];

    public async Task<PagedResult<AdminProjectRow>> SearchAsync(AdminProjectQuery query, CancellationToken cancellationToken = default)
    {
        var projects = db.WeddingProjects.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            projects = projects.Where(x => x.ProjectCode.Contains(term) || x.Customer.FullName.Contains(term)
                || x.Customer.Email.Contains(term) || (x.Customer.Phone != null && x.Customer.Phone.Contains(term))
                || x.Manager.FullName.Contains(term) || x.ProjectManagers.Any(m => m.RemovedAt == null && m.Manager.FullName.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(query.Status)) projects = projects.Where(x => x.Status == query.Status);
        if (query.HallId is { } hallId) projects = projects.Where(x => x.HallId == hallId);
        if (query.ManagerId is { } managerId)
            projects = projects.Where(x => x.ManagerId == managerId || x.ProjectManagers.Any(m => m.ManagerId == managerId && m.RemovedAt == null));
        if (query.From is { } from) projects = projects.Where(x => x.EventDate >= from);
        if (query.To is { } to) projects = projects.Where(x => x.EventDate <= to);

        projects = query.Sort switch
        {
            "eventDate" => projects.OrderBy(x => x.EventDate).ThenBy(x => x.ProjectCode),
            "createdAt" => projects.OrderByDescending(x => x.CreatedAt),
            "code" => projects.OrderBy(x => x.ProjectCode),
            _ => projects.OrderByDescending(x => x.EventDate).ThenBy(x => x.ProjectCode)
        };

        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var total = await projects.CountAsync(cancellationToken);
        var page = Math.Clamp(query.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var rows = await projects.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.ProjectCode, Couple = x.Customer.FullName, x.Customer.Email, x.Customer.Phone, x.EventDate, Hall = x.Hall.Name, Slot = x.Slot.SlotName,
                x.Status, x.CurrentTotal, x.CreatedAt,
                Paid = x.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount ?? 0),
                Addons = x.AddonContracts.Where(a => a.Status == "Confirmed").SelectMany(a => a.Items).Sum(i => (decimal?)(i.Quantity * i.UnitPrice)) ?? 0,
                Creator = x.CreatedByNavigation, x.Manager,
                CoManagers = x.ProjectManagers.Where(m => m.RemovedAt == null && m.Role == "CoManager").OrderBy(m => m.AssignedAt).Select(m => m.Manager).ToList()
            }).ToListAsync(cancellationToken);

        var items = rows.Select(x => new AdminProjectRow(x.ProjectCode, x.Couple, x.Email, x.Phone, x.EventDate, x.Hall, x.Slot, x.Status,
            x.CurrentTotal + x.Addons, x.Paid, x.Status == EventStatus.Cancelled ? 0 : Math.Max(0, x.CurrentTotal + x.Addons - x.Paid), Person(x.Creator), Operators(x.Manager, x.CoManagers), Utc(x.CreatedAt))).ToList();
        return new PagedResult<AdminProjectRow>(items, total, page, pageSize);
    }

    public async Task<AdminProjectFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var halls = await db.Halls.AsNoTracking().OrderBy(x => x.Name).Select(x => new OptionItem(x.HallId, x.Name)).ToListAsync(cancellationToken);
        var managers = await db.Users.AsNoTracking().Where(x => x.Role == "Manager").OrderBy(x => x.FullName)
            .Select(x => new OptionItem(x.UserId, x.FullName)).ToListAsync(cancellationToken);
        return new AdminProjectFilterOptions(halls, managers, Statuses);
    }

    public async Task<AdminProjectDetail?> GetDetailAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var p = await db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(x => x.Customer).Include(x => x.Hall).Include(x => x.Slot).Include(x => x.Manager).Include(x => x.CreatedByNavigation)
            .Include(x => x.ProjectManagers).ThenInclude(x => x.Manager)
            .Include(x => x.SpecItems).ThenInclude(x => x.CatalogItem)
            .Include(x => x.AddonContracts).ThenInclude(x => x.Items)
            .Include(x => x.AddonContracts).ThenInclude(x => x.CreatedByNavigation)
            .Include(x => x.StageNotes).ThenInclude(x => x.CreatedByNavigation)
            .Include(x => x.SupplierEvidences).ThenInclude(x => x.File)
            .Include(x => x.SupplierEvidences).ThenInclude(x => x.SpecItem!).ThenInclude(x => x.CatalogItem)
            .Include(x => x.SupplierEvidences).ThenInclude(x => x.UploadedByNavigation)
            .Include(x => x.Payments)
            .Include(x => x.DayOfTasks).ThenInclude(x => x.ExecutionPhotos).ThenInclude(x => x.Uploader)
            .Include(x => x.DayOfTasks).ThenInclude(x => x.CompletedByUser)
            .Include(x => x.Complaints)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
        if (p is null) return null;

        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var paid = p.Payments.Where(x => x.Status == "Paid").Sum(x => x.Amount ?? 0);
        var addonTotal = p.AddonContracts.Where(a => a.Status == "Confirmed").Sum(a => a.Items.Sum(i => i.Quantity * i.UnitPrice));
        var grand = p.CurrentTotal + addonTotal;
        var held = HeldStatuses.Contains(p.Status);
        var settledOn = p.Payments.Where(x => x.PaymentType == PaymentKinds.Settlement && x.Status == "Paid").Select(x => x.PaidDate).FirstOrDefault();
        var timeline = new List<AdminTimelineStep>
        {
            new("signed", "Ký hợp đồng và đặt cọc", Utc(p.CreatedAt), true, p.CreatedByNavigation?.FullName),
            new("locked", $"Khóa dịch vụ (hạn {EventStageRules.LockDate(p.EventDate, policy):dd/MM/yyyy})", Utc(p.LockedAt), p.LockedAt is not null, null),
            new("held", "Tổ chức tiệc cưới", p.EventDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), held, null),
            new("settled", "Tất toán", settledOn?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), p.Status is EventStatus.Settled or EventStatus.Closed, null),
            p.Status == EventStatus.Cancelled
                ? new("cancelled", "Đã hủy (không hoàn cọc)", Utc(p.CancelledAt), true, null)
                : new("closed", "Đóng sự kiện", null, p.Status == EventStatus.Closed, null)
        };

        var specification = p.SpecItems.OrderBy(x => x.CatalogItem.Category).ThenBy(x => x.CatalogItem.Name)
            .Select(x => new AdminSpecLine(WpsmCategoryNames.ToDisplay(x.CatalogItem.Category), x.CatalogItem.Name, x.Description, x.Quantity, x.CatalogItem.Unit,
                x.UnitPrice, x.Quantity * x.UnitPrice)).ToList();
        var addons = p.AddonContracts.OrderByDescending(x => x.CreatedAt).Select(a => EventFlowSupport.MapAddon(a, p.ProjectCode)).ToList();
        var notes = p.StageNotes.OrderByDescending(x => x.CreatedAt).Select(EventFlowSupport.MapNote).ToList();
        var suppliers = p.SupplierEvidences.OrderByDescending(x => x.UploadedAt)
            .Select(e => new SupplierEvidenceView(e.EvidenceId, e.SupplierName, e.EvidenceType, e.SpecItemId, e.SpecItem?.CatalogItem.Name, e.Note,
                e.FileId, e.File.OriginalName, e.UploadedByNavigation.FullName, Utc(e.UploadedAt))).ToList();
        var evidence = p.DayOfTasks.OrderBy(x => x.DeadlineTime)
            .Select(t => new AdminEvidenceTask(t.DayOfTaskId, t.TaskName, Utc(t.DeadlineTime), t.Status, t.PhotoRequired, Utc(t.CompletedAt),
                t.CompletedByUser?.FullName,
                t.ExecutionPhotos.Where(x => x.ContentType == null || x.ContentType.StartsWith("image/")).OrderBy(x => x.UploadedAt)
                    .Select(x => new AdminEvidencePhoto(x.PhotoId, Utc(x.UploadedAt), x.Uploader.FullName, x.UploadedAt > t.DeadlineTime)).ToList()))
            .ToList();
        var complaints = p.Complaints.OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminComplaintRow(x.ComplaintCode, x.Status, x.Description, Utc(x.CreatedAt), x.ResolutionNote)).ToList();

        return new AdminProjectDetail(p.ProjectCode, p.Customer.FullName, p.Customer.Email, p.Customer.Phone, p.Hall.Name, p.Slot.SlotName,
            $"{p.Slot.StartTime:HH:mm} - {p.Slot.EndTime:HH:mm}", p.EventDate, p.Status, Utc(p.CreatedAt), Utc(p.LockedAt),
            Person(p.CreatedByNavigation),
            Operators(p.Manager, p.ProjectManagers.Where(m => m.RemovedAt == null && m.Role == "CoManager").OrderBy(m => m.AssignedAt).Select(m => m.Manager)), timeline,
            new AdminMoneySummary(p.BaseContractValue, p.DepositAmount, p.CurrentTotal, EventStageRules.MinimumTotal(p.BaseContractValue, policy), addonTotal,
                grand, paid, p.Status == EventStatus.Cancelled ? 0 : Math.Max(0, grand - paid)),
            specification, addons, notes, suppliers, await EventFlowSupport.LoadInvoiceViewAsync(db, p.ProjectId, p.ProjectCode, cancellationToken),
            evidence, complaints)
        {
            StatusLabel = EventStatus.Label(p.Status),
            CancelReason = p.CancelReason
        };
    }
}
