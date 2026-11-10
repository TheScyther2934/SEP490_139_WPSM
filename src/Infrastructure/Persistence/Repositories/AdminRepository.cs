using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

// DATETIME2 columns hold UTC (SYSUTCDATETIME defaults); views mark them Utc so the browser converts to UTC+7.
public sealed class AdminRepository(WpsmDbContext db) : IAdminRepository
{
    private static readonly TimeZoneInfo VnTz = GetVietnamTimeZone();
    private static readonly string[] CrStatuses = ["Approved", "Rejected", "Negotiating", "Draft"];

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    private static DateOnly TodayVn() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));
    private static DateTime StartUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), VnTz);
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public async Task<AdminDashboardView> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var today = TodayVn();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var halls = await GetHallUtilizationAsync(monthStart, monthEnd, cancellationToken);
        var revenue = await db.Payments.AsNoTracking()
            .Where(x => x.Status == "Paid" && x.PaidDate >= monthStart && x.PaidDate <= monthEnd)
            .SumAsync(x => x.Amount ?? 0, cancellationToken);
        var alerts = await GetOverdueAlertsAsync(cancellationToken);
        return new AdminDashboardView(
            await db.WeddingProjects.CountAsync(x => x.EventDate >= today && x.EventDate <= today.AddDays(30) && x.Status != "Closed", cancellationToken),
            Percent(halls.Sum(x => x.Booked), halls.Sum(x => x.Available)),
            revenue,
            await db.ChangeRequests.CountAsync(x => x.Status == "Negotiating", cancellationToken),
            alerts.Count,
            await db.Complaints.CountAsync(x => x.Status == "Escalated", cancellationToken),
            alerts.Take(5).ToList(),
            await GetPhotosAsync(new PhotoFeedFilter(null, null, null, null), 5, cancellationToken));
    }

    public async Task<AdminReportView> GetReportAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (from > to) throw new InvalidOperationException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if (to.DayNumber - from.DayNumber > 731) throw new InvalidOperationException("Khoảng thời gian báo cáo tối đa 2 năm.");
        var halls = await GetHallUtilizationAsync(from, to, cancellationToken);
        var revenue = await GetNetRevenueAsync(from, to, cancellationToken);
        var (byStatus, byType) = await GetCrStatisticsAsync(from, to, cancellationToken);
        return new AdminReportView(from, to, Percent(halls.Sum(x => x.Booked), halls.Sum(x => x.Available)), revenue.Sum(x => x.Received),
            byStatus.First(x => x.Status == "Approved").Count, halls, revenue, byStatus, byType);
    }

    private static decimal Percent(int part, int whole) => whole == 0 ? 0 : Math.Round(part * 100m / whole, 1);

    private async Task<IReadOnlyList<HallUtilizationRow>> GetHallUtilizationAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        var halls = await db.Halls.AsNoTracking().Where(x => x.Status == "Active").OrderBy(x => x.Name)
            .Select(x => new { x.HallId, x.Name, Slots = x.Slots.Count }).ToListAsync(cancellationToken);
        var booked = await db.WeddingProjects.AsNoTracking().Where(x => x.EventDate >= from && x.EventDate <= to)
            .GroupBy(x => x.HallId).Select(g => new { HallId = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.HallId, x => x.Count, cancellationToken);
        return halls.Select(h =>
        {
            var available = h.Slots * days;
            var count = booked.GetValueOrDefault(h.HallId);
            return new HallUtilizationRow(h.Name, available, count, Percent(count, available));
        }).ToList();
    }

    private async Task<IReadOnlyList<NetRevenueRow>> GetNetRevenueAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var projects = await db.WeddingProjects.AsNoTracking().Where(x => x.EventDate >= from && x.EventDate <= to)
            .Select(x => new
            {
                x.EventDate, x.BaseContractValue,
                Variance = x.ChangeRequests.Where(c => c.Status == "Approved").Sum(c => (decimal?)c.TotalVariance) ?? 0,
                Paid = x.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount) ?? 0
            }).ToListAsync(cancellationToken);
        var received = await db.Payments.AsNoTracking().Where(x => x.Status == "Paid" && x.PaidDate >= from && x.PaidDate <= to)
            .Select(x => new { x.PaidDate, x.Amount }).ToListAsync(cancellationToken);
        var rows = new List<NetRevenueRow>();
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            var inMonth = projects.Where(p => p.EventDate.Year == month.Year && p.EventDate.Month == month.Month).ToList();
            rows.Add(new NetRevenueRow(month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                inMonth.Sum(p => p.BaseContractValue), inMonth.Sum(p => p.Variance),
                received.Where(p => p.PaidDate!.Value.Year == month.Year && p.PaidDate.Value.Month == month.Month).Sum(p => p.Amount ?? 0),
                inMonth.Sum(p => Math.Max(0, p.BaseContractValue + p.Variance - p.Paid))));
        }
        return rows;
    }

    private async Task<(IReadOnlyList<CrStatusCount>, IReadOnlyList<CrTypeRow>)> GetCrStatisticsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = StartUtc(from);
        var end = StartUtc(to.AddDays(1));
        var crs = await db.ChangeRequests.AsNoTracking().Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .Select(x => new
            {
                x.Type, x.Status, x.CreatedAt, Amount = x.TotalVariance ?? 0, x.CustomerApprovedAt, x.ManagerApprovedAt,
                LastAction = x.Crhistories.Max(h => (DateTime?)h.CreatedAt)
            }).ToListAsync(cancellationToken);
        var byStatus = CrStatuses.Select(s => new CrStatusCount(s, crs.Count(x => x.Status == s))).ToList();
        // Negotiation time ends at the second approval, or at the last history entry of a rejected CR.
        var byType = crs.GroupBy(x => x.Type).OrderBy(g => g.Key).Select(g =>
        {
            var durations = g.Select(x => x.Status == "Approved" && x.CustomerApprovedAt is { } c && x.ManagerApprovedAt is { } m
                    ? (c > m ? c : m) - x.CreatedAt
                    : x.Status == "Rejected" && x.LastAction is { } last ? last - x.CreatedAt : (TimeSpan?)null)
                .Where(d => d is not null).Select(d => (decimal)d!.Value.TotalDays).ToList();
            return new CrTypeRow(g.Key, g.Count(), g.Sum(x => x.Amount), durations.Count == 0 ? null : Math.Round(durations.Average(), 1));
        }).ToList();
        return (byStatus, byType);
    }

    public Task<IReadOnlyList<ProofPhotoView>> GetPhotosAsync(PhotoFeedFilter filter, CancellationToken cancellationToken = default) =>
        GetPhotosAsync(filter, 60, cancellationToken);

    private async Task<IReadOnlyList<ProofPhotoView>> GetPhotosAsync(PhotoFeedFilter filter, int take, CancellationToken cancellationToken)
    {
        var query = db.ExecutionPhotos.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filter.ProjectCode)) query = query.Where(x => x.DayOfTask.Project.ProjectCode == filter.ProjectCode.Trim());
        if (filter.HallId is { } hallId) query = query.Where(x => x.DayOfTask.Project.HallId == hallId);
        if (!string.IsNullOrWhiteSpace(filter.TaskStatus)) query = query.Where(x => x.DayOfTask.Status == filter.TaskStatus);
        if (filter.Date is { } date)
        {
            var start = StartUtc(date);
            var end = StartUtc(date.AddDays(1));
            query = query.Where(x => x.UploadedAt >= start && x.UploadedAt < end);
        }
        var photos = await query.OrderByDescending(x => x.UploadedAt).Take(take)
            .Select(x => new { x.PhotoId, x.DayOfTask.Project.ProjectCode, Hall = x.DayOfTask.Project.Hall.Name, x.DayOfTask.TaskName, x.DayOfTask.Status, x.Uploader.FullName, x.UploadedAt })
            .ToListAsync(cancellationToken);
        return photos.Select(x => new ProofPhotoView(x.PhotoId, x.ProjectCode, x.Hall, x.TaskName, x.Status, x.FullName, Utc(x.UploadedAt))).ToList();
    }

    public async Task<PhotoAuditView?> GetPhotoAsync(int photoId, CancellationToken cancellationToken = default)
    {
        var x = await db.ExecutionPhotos.AsNoTracking().Where(p => p.PhotoId == photoId)
            .Select(p => new
            {
                p.PhotoId, p.DayOfTask.Project.ProjectCode, Hall = p.DayOfTask.Project.Hall.Name, Slot = p.DayOfTask.Project.Slot.SlotName,
                p.DayOfTask.TaskName, p.DayOfTask.DeadlineTime, p.UploadedAt, p.Uploader.FullName
            }).FirstOrDefaultAsync(cancellationToken);
        return x is null ? null : new PhotoAuditView(x.PhotoId, x.ProjectCode, x.Hall, x.Slot, x.TaskName, Utc(x.DeadlineTime), Utc(x.UploadedAt), x.UploadedAt > x.DeadlineTime, x.FullName);
    }

    public async Task<string?> GetPhotoPathAsync(int photoId, CancellationToken cancellationToken = default) =>
        await db.ExecutionPhotos.AsNoTracking().Where(x => x.PhotoId == photoId).Select(x => x.ImageFilePath).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<OverdueAlertView>> GetOverdueAlertsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var tasks = await db.DayOfTasks.AsNoTracking().Where(x => x.Status == "Overdue").OrderBy(x => x.DeadlineTime)
            .Select(x => new { x.DayOfTaskId, x.Project.ProjectCode, x.TaskName, x.DeadlineTime }).ToListAsync(cancellationToken);
        return tasks.Select(x => new OverdueAlertView(x.DayOfTaskId, x.ProjectCode, x.TaskName, Utc(x.DeadlineTime),
            Math.Max(0, (int)(now - x.DeadlineTime).TotalMinutes))).ToList();
    }

    public async Task<IReadOnlyList<EscalatedComplaintView>> GetEscalatedComplaintsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Complaints.AsNoTracking().Where(x => x.Status == "Escalated").OrderBy(x => x.EscalatedAt ?? x.CreatedAt)
            .Select(x => new
            {
                x.ComplaintId, x.ComplaintCode, x.Project.ProjectCode, x.Description, x.EscalationNote, x.EscalatedAt, x.Beo.Version,
                Photos = x.Project.DayOfTasks.SelectMany(t => t.ExecutionPhotos).Count()
            }).ToListAsync(cancellationToken);
        return rows.Select(x => new EscalatedComplaintView(x.ComplaintId, x.ComplaintCode, x.ProjectCode, x.Description, x.EscalationNote,
            x.EscalatedAt is { } at ? Utc(at) : null, x.Version, x.Photos)).ToList();
    }

    public async Task<bool> ResolveComplaintAsync(int complaintId, string decision, int adminId, CancellationToken cancellationToken = default)
    {
        decision = decision?.Trim() ?? "";
        if (decision.Length == 0) throw new InvalidOperationException("Trường Quyết định là bắt buộc.");
        if (decision.Length > 1000) throw new InvalidOperationException("Vượt quá độ dài tối đa 1000 ký tự.");
        var complaint = await db.Complaints.Include(x => x.Project).FirstOrDefaultAsync(x => x.ComplaintId == complaintId, cancellationToken);
        if (complaint is null) return false;
        if (complaint.Status != "Escalated") throw new InvalidOperationException("Khiếu nại không còn ở trạng thái Escalated.");
        complaint.Status = "Resolved";
        complaint.ResolutionNote = decision;
        complaint.ResolvedBy = adminId;
        complaint.ResolvedAt = DateTime.UtcNow;
        var message = $"Khiếu nại {complaint.ComplaintCode} đã được Admin giải quyết.";
        db.Notifications.Add(new Notification { RecipientId = complaint.Project.ManagerId, Type = "Complaint", Message = message, LinkUrl = OperationsSupport.Links.ManagerComplaint(complaint.Project.ProjectCode, complaint.ComplaintCode) });
        db.Notifications.Add(new Notification { RecipientId = complaint.Project.CustomerId, Type = "Complaint", Message = message, LinkUrl = OperationsSupport.Links.CustomerComplaint(complaint.ComplaintCode) });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ExceptionQueueView> GetExceptionRequestsAsync(CancellationToken cancellationToken = default)
    {
        var requests = await db.ExceptionRequests.AsNoTracking().AsSplitQuery()
            .Include(x => x.Project).ThenInclude(p => p.Customer)
            .Include(x => x.Project).ThenInclude(p => p.Hall)
            .Include(x => x.Cr)
            .Include(x => x.Allocation).ThenInclude(a => a!.Resource)
            .Include(x => x.Allocation).ThenInclude(a => a!.Hall)
            .Include(x => x.RequestedByNavigation)
            .Include(x => x.DecisionByNavigation)
            .OrderBy(x => x.Status == "Pending" ? 0 : 1)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        // Resource requests need the events that share the same resource in an overlapping window.
        var resourceIds = requests.Where(x => x.Allocation is not null).Select(x => x.Allocation!.ResourceId).Distinct().ToList();
        var related = resourceIds.Count == 0
            ? new List<ResourceAllocation>()
            : await db.ResourceAllocations.AsNoTracking()
                .Include(a => a.Project).ThenInclude(p => p.Customer)
                .Where(a => resourceIds.Contains(a.ResourceId) && a.Status != "Released")
                .ToListAsync(cancellationToken);

        var items = requests.Select(request =>
        {
            ExceptionCrContextView? cr = request.Cr is { } ticket
                ? new ExceptionCrContextView(ticket.Crcode, ticket.CostImpact, ticket.PenaltyRate ?? 0m, ticket.PenaltyFee,
                    ticket.TotalVariance ?? ticket.CostImpact + ticket.PenaltyFee)
                : null;

            ExceptionResourceContextView? resource = null;
            if (request.Allocation is { } allocation)
            {
                var clashing = related
                    .Where(other => other.AllocationId != allocation.AllocationId
                        && other.ResourceId == allocation.ResourceId
                        && other.StartTime < allocation.EndTime && allocation.StartTime < other.EndTime)
                    .OrderBy(other => other.StartTime)
                    .ToList();
                var otherQuantity = clashing.Where(ConsumesCapacity).Sum(other => other.AllocatedQuantity);
                var shortage = Math.Max(0, otherQuantity + allocation.AllocatedQuantity - allocation.Resource.TotalQuantity);
                resource = new ExceptionResourceContextView(
                    allocation.AllocationId, allocation.Resource.Name, ResourceCalendarRepository.TypeLabel(allocation.Resource.Type),
                    allocation.AllocatedQuantity, allocation.Resource.TotalQuantity, allocation.Hall.Name,
                    Utc(allocation.StartTime), Utc(allocation.EndTime), otherQuantity, shortage,
                    clashing.Select(other => new ExceptionClashingView(other.Project.ProjectCode, other.Project.Customer.FullName,
                        Utc(other.StartTime), Utc(other.EndTime), other.AllocatedQuantity, other.Status)).ToList());
            }

            return new ExceptionRequestView(
                request.ExceptionRequestId, request.RequestCode, request.SourceType, request.Status,
                request.Project.ProjectCode, request.Project.Customer.FullName, request.Project.Hall.Name,
                request.RequestedByNavigation.FullName, Utc(request.CreatedAt), request.RequestNote,
                request.DecisionNote, request.DecisionByNavigation?.FullName,
                request.DecidedAt is { } decidedAt ? Utc(decidedAt) : null,
                cr, resource);
        })
        .OrderBy(item => item.Status == "Pending" ? 0 : 1)
        .ThenBy(item => item.Status == "Pending" ? item.CreatedAt : DateTime.MaxValue)
        .ThenByDescending(item => item.DecidedAt ?? item.CreatedAt)
        .ToList();

        var pending = items.Where(item => item.Status == "Pending").ToList();
        int? oldestPendingHours = pending.Count == 0
            ? null
            : (int)Math.Max(0, Math.Floor((DateTime.UtcNow - pending.Min(item => item.CreatedAt)).TotalHours));
        var summary = new ExceptionQueueSummaryView(
            pending.Count,
            items.Count(item => item.Status == "Approved"),
            items.Count(item => item.Status == "Rejected"),
            oldestPendingHours);
        return new ExceptionQueueView(items, summary);
    }

    private static bool ConsumesCapacity(ResourceAllocation allocation) =>
        allocation.Status is "Reserved" or "Exception Approved";

    public async Task<bool> DecideExceptionAsync(int requestId, bool approve, string note, int adminId, CancellationToken cancellationToken = default)
    {
        note = note?.Trim() ?? "";
        if (note.Length == 0) throw new InvalidOperationException("Trường Ghi chú quyết định là bắt buộc.");
        if (note.Length > 300) throw new InvalidOperationException("Vượt quá độ dài tối đa 300 ký tự.");
        var request = await db.ExceptionRequests.Include(x => x.Cr).Include(x => x.Allocation).Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.ExceptionRequestId == requestId, cancellationToken);
        if (request is null) return false;
        if (request.Status != "Pending") throw new InvalidOperationException("Yêu cầu ngoại lệ này đã được quyết định.");
        var now = DateTime.UtcNow;
        request.Status = approve ? "Approved" : "Rejected";
        request.DecisionBy = adminId;
        request.DecisionNote = note;
        request.DecidedAt = now;
        if (request.Cr is { } cr)
        {
            if (cr.Status is "Approved" or "Rejected") throw new InvalidOperationException($"CR {cr.Crcode} đã đóng nên không thể quyết định ngoại lệ.");
            if (approve) cr.PenaltyFee = 0;
            db.Crhistories.Add(new Crhistory { Crid = cr.Crid, ActorId = adminId, Action = approve ? "ExceptionApproved" : "ExceptionRejected", Note = note, CreatedAt = now });
            db.Notifications.Add(new Notification
            {
                RecipientId = request.RequestedBy, Type = "CR update", LinkUrl = request.Cr is null ? OperationsSupport.Links.ResourceCalendar : OperationsSupport.Links.ManagerCr(request.Project.ProjectCode, request.Cr.Crcode),
                Message = $"Yêu cầu ngoại lệ {request.RequestCode} cho {cr.Crcode} đã được {(approve ? "chấp thuận" : "từ chối")}."
            });
        }
        if (!approve && request.Allocation is { } rejected) rejected.Status = "Released";
        if (approve && request.Allocation is { } allocation)
        {
            allocation.Status = "Exception Approved";
            allocation.IsExceptionApproved = true;
            allocation.ExceptionApprovedBy = adminId;
            allocation.ExceptionApprovedAt = now;
            allocation.ExceptionNote = note;
        }
        else if (!approve && request.Allocation is { Status: "PendingException" } rejected)
        {
            // A rejected resource exception never becomes valid, so the pending allocation is released.
            rejected.Status = "Released";
        }
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
