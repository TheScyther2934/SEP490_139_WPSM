using Microsoft.EntityFrameworkCore;
using Application.Dashboard;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Persistence;

namespace Infrastructure.Dashboard;

/// <summary>
/// Manager dashboard: KPIs, the next actions of each event derived from its stage (lock soon, supplier evidence missing,
/// addon contracts waiting for the customer, settlement to collect), overdue day-of tasks and stale coordination sheets.
/// Scoped to the events the manager operates (Primary or co-manager).
/// </summary>
public sealed class ManagerDashboardService(WpsmDbContext db) : IManagerDashboardService
{
    private static readonly string[] ActiveStatuses = [EventStatus.Adjusting, EventStatus.Locked, EventStatus.Completed];

    public async Task<ManagerDashboardResponse> GetDashboardAsync(int managerId, CancellationToken cancellationToken = default)
    {
        var today = VietnamTime.GetVietnamToday();
        var nowUtc = DateTime.UtcNow;
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);

        var projects = await db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(p => p.Customer).Include(p => p.Hall)
            .Include(p => p.Beos)
            .Include(p => p.AddonContracts)
            .Include(p => p.SpecItems).ThenInclude(s => s.CatalogItem)
            .Include(p => p.SupplierEvidences)
            .Include(p => p.Payments)
            .Where(p => p.ManagerId == managerId || p.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null))
            .ToListAsync(cancellationToken);
        var active = projects.Where(p => ActiveStatuses.Contains(p.Status)).ToList();
        var activeIds = active.Select(p => p.ProjectId).ToList();

        var staleBeos = new List<StaleBeoAlertView>();
        foreach (var p in active)
        {
            var latest = p.Beos.OrderByDescending(b => b.Version).FirstOrDefault();
            if (latest?.Status != "Stale") continue;
            var addon = p.AddonContracts.Where(a => a.Status == "Confirmed").OrderByDescending(a => a.ConfirmedAt).Select(a => a.AddonCode).FirstOrDefault();
            staleBeos.Add(new StaleBeoAlertView(latest.Beoid, p.ProjectCode, p.Hall?.Name ?? "", p.EventDate, latest.Version, "Stale", latest.Notes, addon));
        }

        var overdue = await db.DayOfTasks.AsNoTracking()
            .Where(t => activeIds.Contains(t.ProjectId) && t.Status == "Overdue" && t.CompletedAt == null)
            .OrderBy(t => t.DeadlineTime)
            .Select(t => new { t.DayOfTaskId, t.Project.ProjectCode, t.TaskName, t.DeadlineTime, Hall = t.Project.Hall.Name, t.Project.EventDate, Customer = t.Project.Customer.FullName })
            .ToListAsync(cancellationToken);
        var overdueViews = overdue.Select(t => new OverdueAlertView(t.DayOfTaskId, t.ProjectCode, t.TaskName, DateTime.SpecifyKind(t.DeadlineTime, DateTimeKind.Utc),
            Math.Max(0, (int)(nowUtc - t.DeadlineTime).TotalMinutes), "Overdue", t.Hall, t.EventDate, t.Customer, null)).ToList();

        // Next actions per event, derived from its stage (no stored task list).
        var tasks = new List<ManagerTaskItemView>();
        var upcoming = new List<UpcomingMilestoneView>();
        var taskId = 0;
        foreach (var p in active.OrderBy(p => p.EventDate))
        {
            var lockDate = EventStageRules.LockDate(p.EventDate, policy);
            var supplierDeadline = EventStageRules.SupplierDeadline(p.EventDate, policy);
            var missingSuppliers = p.SpecItems.Count(s => s.CatalogItem.RequiresSupplier && p.SupplierEvidences.All(e => e.SpecItemId != s.SpecItemId));
            var pendingAddons = p.AddonContracts.Where(a => a.Status == "PendingCustomer").ToList();
            void Add(string title, string stage, DateOnly due, string type) =>
                tasks.Add(new ManagerTaskItemView(++taskId, p.ProjectCode, title, stage, due, due < today ? "Overdue" : "Pending", false, type));

            if (p.Status == EventStatus.Adjusting && lockDate.DayNumber - today.DayNumber <= 7)
                Add($"Rà soát dịch vụ với khách trước khi khóa ({lockDate:dd/MM})", EventStageRules.StageTitle(EventStageRules.Adjusting), lockDate.AddDays(-1), "Lock");
            if (p.Status == EventStatus.Locked && missingSuppliers > 0)
                Add($"Đặt nhà cung cấp và tải minh chứng ({missingSuppliers} dịch vụ còn thiếu)", EventStageRules.StageTitle(EventStageRules.Locked), supplierDeadline, "Supplier");
            foreach (var addon in pendingAddons)
                Add($"Nhắc khách xác nhận hợp đồng phát sinh {addon.AddonCode}", "Hợp đồng phát sinh", today, "Addon");
            if (p.Status == EventStatus.Completed && p.Payments.All(x => x.PaymentType != PaymentKinds.Settlement || x.Status != "Paid"))
                Add("Thu tất toán theo hóa đơn cuối", EventStageRules.StageTitle(EventStageRules.Settlement), EventStageRules.SettlementDueDate(p.EventDate, policy), "Settlement");

            if (p.EventDate < today || upcoming.Count >= 8) continue;
            var stage = EventStageRules.CurrentStage(p.Status, p.EventDate, today);
            var (next, due) = stage switch
            {
                EventStageRules.Adjusting => ("Khóa dịch vụ", lockDate),
                EventStageRules.Locked when missingSuppliers > 0 && today <= supplierDeadline => ("Hạn đặt nhà cung cấp", supplierDeadline),
                _ => ("Ngày cưới", p.EventDate)
            };
            upcoming.Add(new UpcomingMilestoneView(p.ProjectCode, p.Customer?.FullName ?? "", p.Hall?.Name ?? "", p.EventDate, stage,
                EventStageRules.StageTitle(stage), next, due, VietnamTime.CalculateCountdown(due, today), missingSuppliers, pendingAddons.Count));
        }

        var dayOfToday = await db.DayOfTasks.AsNoTracking()
            .CountAsync(t => activeIds.Contains(t.ProjectId) && t.Project.EventDate == today && t.Status != "Done", cancellationToken);
        var kpis = new ManagerKpiSummary(
            ActiveProjects: active.Count,
            TasksDueToday: tasks.Count(t => t.DueDate <= today) + dayOfToday,
            PendingAddons: active.Sum(p => p.AddonContracts.Count(a => a.Status == "PendingCustomer")),
            StaleBeos: staleBeos.Count,
            OverdueTasksCount: overdueViews.Count,
            DraftingProjects: active.Count(p => p.Status == EventStatus.Adjusting),
            EventsIn7Days: active.Count(p => p.EventDate >= today && p.EventDate <= today.AddDays(7)));

        return new ManagerDashboardResponse(kpis, tasks.OrderBy(t => t.DueDate).Take(20).ToList(), upcoming, overdueViews, staleBeos);
    }
}
