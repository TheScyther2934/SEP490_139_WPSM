using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements <see cref="ICustomerDashboardRepository"/> for C-01.
/// All queries scope to a single Customer-owned project to prevent data leakage.
/// </summary>
public sealed class CustomerDashboardRepository(WpsmDbContext db) : ICustomerDashboardRepository
{
    // Vietnam timezone (UTC+7) — Windows: "SE Asia Standard Time", Linux: "Asia/Ho_Chi_Minh"
    private static readonly TimeZoneInfo VnTz = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz))
                return tz;
        }
        // Fallback: manual UTC+7 offset (never null)
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    public async Task<CustomerDashboardView?> GetDashboardAsync(
        int customerId,
        string projectCode,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Ownership check ───────────────────────────────────────────────
        var project = await db.WeddingProjects
            .AsNoTracking()
            .Include(p => p.Customer)
            .Include(p => p.Hall)
            .Include(p => p.Slot)
            .Include(p => p.Payments)
            .Include(p => p.ChangeRequests)
            .FirstOrDefaultAsync(
                p => p.ProjectCode == projectCode && p.CustomerId == customerId,
                cancellationToken);

        if (project is null) return null;

        // ── 2. Countdown (UTC+7) ─────────────────────────────────────────────
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));
        var rawDays = project.EventDate.DayNumber - today.DayNumber;
        var countdownDays = Math.Max(0, rawDays);
        var countdownLabel = countdownDays switch
        {
            0 => "Hôm nay — Ngày cưới!",
            1 => "Còn 1 ngày",
            _ => $"Còn {countdownDays} ngày"
        };

        // ── 3. Upcoming tasks (max 5, ordered by DueDate) ───────────────────
        var allTasks = await db.RoadmapTasks
            .AsNoTracking()
            .Where(t => t.ProjectId == project.ProjectId)
            .OrderBy(t => t.DueDate)
            .ToListAsync(cancellationToken);

        var taskViews = allTasks
            .Select(t => new DashboardTaskView(
                t.TaskId,
                t.Milestone,
                t.Title,
                t.AssigneeRole,
                t.DueDate,
                ResolveTaskStatus(t.Status, t.DueDate, today)))
            // Prioritise incomplete (Overdue first, then Pending), Done last
            .OrderBy(t => t.Status == "Done" ? 2 : t.Status == "Overdue" ? 0 : 1)
            .ThenBy(t => t.DueDate)
            .Take(5)
            .ToList();

        // ── 4. Open CRs (Draft or Negotiating — same rule as ProjectReadModel) ──
        var openCrCount = project.ChangeRequests
            .Count(cr => cr.Status is "Draft" or "Negotiating");

        // ── 5. Outstanding Balance ───────────────────────────────────────────
        // Formula: BaseContractValue + ΣApproved CR TotalVariance - ΣPaid Payments
        // "Approved" CR = CustomerApproved status (committed to contract)
        var approvedVariance = project.ChangeRequests
            .Where(cr => cr.Status == "CustomerApproved")
            .Sum(cr => cr.TotalVariance ?? 0m);

        var totalPaid = project.Payments
            .Where(p => p.Status == "Paid")
            .Sum(p => p.Amount ?? 0m);

        var outstandingBalance = Math.Max(
            0m,
            project.BaseContractValue + approvedVariance - totalPaid);

        // ── 6. Roadmap progress ──────────────────────────────────────────────
        var totalTaskCount = allTasks.Count;
        var doneCount = allTasks.Count(t => t.Status == "Done");
        var progress = totalTaskCount == 0
            ? 0
            : (int)Math.Round(doneCount * 100d / totalTaskCount);

        // ── 7. Latest proof-of-execution photo ──────────────────────────────
        var latestPhoto = await db.ExecutionPhotos
            .AsNoTracking()
            .Include(ep => ep.DayOfTask)
            .Include(ep => ep.Uploader)
            .Where(ep => ep.DayOfTask.ProjectId == project.ProjectId)
            .OrderByDescending(ep => ep.UploadedAt)
            .Select(ep => new
            {
                ep.PhotoId,
                ep.ImageFilePath,
                ep.UploadedAt,
                UploaderName = ep.Uploader.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);

        var photoView = latestPhoto is null
            ? null
            : new DashboardPhotoView(
                latestPhoto.PhotoId,
                latestPhoto.ImageFilePath,
                latestPhoto.UploadedAt,
                latestPhoto.UploaderName);

        return new CustomerDashboardView(
            project.ProjectCode,
            project.ProjectCode,
            project.Customer.FullName,
            project.Hall.Name,
            project.Slot.SlotName,
            project.EventDate,
            countdownDays,
            countdownLabel,
            taskViews,
            openCrCount,
            outstandingBalance,
            project.BaseContractValue,
            totalPaid,
            progress,
            photoView);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static string ResolveTaskStatus(string dbStatus, DateOnly dueDate, DateOnly today) =>
        dbStatus == "Done" ? "Done"
        : dueDate < today ? "Overdue"
        : "Pending";
}
