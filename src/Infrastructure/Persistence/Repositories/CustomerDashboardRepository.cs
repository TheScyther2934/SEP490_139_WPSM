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
            .Include(p => p.AddonContracts).ThenInclude(a => a.Items)
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

        // ── 3. Stages, money, addon contracts waiting for the customer ──────
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var stages = Domain.Services.EventStageRules.Timeline(project.Status, project.EventDate, DateOnly.FromDateTime(project.CreatedAt), today, policy);
        var currentStage = Domain.Services.EventStageRules.CurrentStage(project.Status, project.EventDate, today);
        var pendingAddons = project.AddonContracts.Count(a => a.Status == "PendingCustomer");
        var addonTotal = project.AddonContracts.Where(a => a.Status == "Confirmed").Sum(a => a.Items.Sum(i => i.Quantity * i.UnitPrice));
        var totalPaid = project.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount ?? 0m);
        var outstandingBalance = Math.Max(0m, project.CurrentTotal + addonTotal - totalPaid);

        // ── 7. Latest proof-of-execution photo ──────────────────────────────
        var latestPhoto = await db.ExecutionPhotos
            .AsNoTracking()
            .Include(ep => ep.DayOfTask)
            .Include(ep => ep.Uploader)
            .Where(ep => ep.DayOfTask.ProjectId == project.ProjectId && (ep.ContentType == null || ep.ContentType.StartsWith("image/")))
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
            stages,
            pendingAddons,
            outstandingBalance,
            project.BaseContractValue,
            totalPaid,
            currentStage,
            photoView)
        {
            Status = project.Status,
            StatusLabel = Domain.Services.EventStatus.Label(project.Status),
            CurrentTotal = project.CurrentTotal + addonTotal,
            Contacts = await EventFlowSupport.LoadContactsAsync(db, project.ProjectId, cancellationToken),
            Hotline = await EventFlowSupport.LoadHotlineAsync(db, cancellationToken)
        };
    }

    public Task<StoredFileRef?> GetProofPhotoAsync(int customerId, int photoId, CancellationToken cancellationToken = default) =>
        db.ExecutionPhotos.AsNoTracking()
            .Where(p => p.PhotoId == photoId && p.DayOfTask.Project.CustomerId == customerId && (p.ContentType == null || p.ContentType.StartsWith("image/")))
            .Select(p => new StoredFileRef(p.ImageFilePath, p.ContentType ?? "image/jpeg", p.FileName ?? "anh"))
            .FirstOrDefaultAsync(cancellationToken);
}
