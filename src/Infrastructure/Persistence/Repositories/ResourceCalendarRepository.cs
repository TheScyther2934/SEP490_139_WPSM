using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// FE-07 Resource Calendar (M-21, M-22).
/// Builds the hall/slot grid from Wedding Projects and the resource lanes from Resource Allocations,
/// and enforces GBR-09: the same resource may not be allocated beyond its TotalQuantity in overlapping
/// time ranges unless an Admin approved a special exception (UC-47).
/// Allocation states: Reserved (counts against capacity) → PendingException (waiting for the Admin, does
/// not count) → Exception Approved (counts, allowed to exceed) or Released (freed). A rejected exception
/// is released by the Admin decision (see AdminRepository.DecideExceptionAsync).
/// </summary>
public sealed class ResourceCalendarRepository(WpsmDbContext db) : IResourceCalendarRepository
{
    private static readonly string[] CapacityStatuses = ["Reserved", "Exception Approved"];
    private static readonly string[] TypeOrder = ["Audio", "Follow Light", "Decor Frame", "Technician", "Kitchen"];

    private static readonly TimeZoneInfo VnTz = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    private static DateOnly TodayVn() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));
    private static DateTime StartUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), VnTz);
    private static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), VnTz);
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => value
    };

    /// <summary>SYSUTCDATETIME values come back Unspecified; the client needs the instant.</summary>
    private static DateTime UtcValue(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static string TypeLabel(string type) => type switch
    {
        "Audio" => "Âm thanh",
        "Follow Light" => "Đèn follow",
        "Decor Frame" => "Khung trang trí",
        "Technician" => "Kỹ thuật viên",
        "Kitchen" => "Bếp & nhân sự",
        _ => type
    };

    private static bool ConsumesCapacity(ResourceAllocation allocation) => CapacityStatuses.Contains(allocation.Status);

    private static int TypeRank(string type)
    {
        var index = Array.IndexOf(TypeOrder, type);
        return index >= 0 ? index : int.MaxValue;
    }

    // ── M-21: calendar grid ───────────────────────────────────────────────────

    public async Task<ResourceCalendarView?> GetCalendarAsync(DateOnly from, DateOnly to, int? hallId, int managerId, CancellationToken cancellationToken = default)
    {
        if (to < from) throw new InvalidOperationException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if (to.DayNumber - from.DayNumber > 62) throw new InvalidOperationException("Khoảng thời gian xem lịch tối đa 2 tháng.");

        var halls = await db.Halls.AsNoTracking().Include(h => h.Slots)
            .Where(h => h.Status == "Active" && (hallId == null || h.HallId == hallId))
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken);

        var bookings = await db.WeddingProjects.AsNoTracking()
            .Include(p => p.Customer).Include(p => p.Hall).Include(p => p.Slot)
            .Where(p => p.ManagerId == managerId && p.EventDate >= from && p.EventDate <= to && (hallId == null || p.HallId == hallId))
            .OrderBy(p => p.EventDate).ThenBy(p => p.Hall.Name)
            .ToListAsync(cancellationToken);

        // The project picker of the allocation dialog: the Manager's upcoming, still-open projects.
        var projects = await db.WeddingProjects.AsNoTracking()
            .Include(p => p.Customer).Include(p => p.Hall).Include(p => p.Slot)
            .Where(p => p.ManagerId == managerId && p.EventDate >= from && p.Status != "Closed" && p.Status != "Settled")
            .OrderBy(p => p.EventDate).ThenBy(p => p.Hall.Name)
            .Take(200)
            .ToListAsync(cancellationToken);

        // Resources and their allocations are shared across halls, so they are never hidden by the hall
        // filter: the availability shown in the dialog must stay correct for every hall.
        var allResources = await db.Resources.AsNoTracking().ToListAsync(cancellationToken);
        var types = allResources.GroupBy(r => r.Type)
            .OrderBy(g => TypeRank(g.Key))
            .Select(g => new ResourceTypeOptionView(g.Key, TypeLabel(g.Key), g.Count(), g.Sum(r => r.TotalQuantity)))
            .ToList();

        var rangeStart = StartUtc(from);
        var rangeEnd = StartUtc(to.AddDays(1));
        var allocations = await db.ResourceAllocations.AsNoTracking()
            .Include(a => a.Resource)
            .Include(a => a.Project).ThenInclude(p => p.Customer)
            .Include(a => a.Hall)
            .Where(a => a.Project.ManagerId == managerId && a.Status != "Released"
                && a.StartTime < rangeEnd && a.EndTime > rangeStart)
            .OrderBy(a => a.StartTime)
            .ToListAsync(cancellationToken);

        var allocationIds = allocations.Select(a => a.AllocationId).ToList();
        var pendingIds = allocationIds.Count == 0
            ? new HashSet<int>()
            : (await db.ExceptionRequests.AsNoTracking()
                .Where(e => e.AllocationId != null && allocationIds.Contains(e.AllocationId.Value) && e.Status == "Pending")
                .Select(e => e.AllocationId!.Value)
                .ToListAsync(cancellationToken)).ToHashSet();
        var pendingRequests = pendingIds.Count;

        var conflicts = ComputeConflicts(allocations);
        var lanes = allResources.Select(resource =>
        {
            var mine = allocations.Where(a => a.ResourceId == resource.ResourceId).ToList();
            var usages = new Dictionary<DateOnly, (int Reserved, int Approved, int Pending, bool Conflict, List<AllocationBriefView> Items)>();
            foreach (var allocation in mine)
            {
                foreach (var date in DatesOf(allocation, from, to))
                {
                    usages.TryGetValue(date, out var bucket);
                    bucket.Items ??= [];
                    switch (allocation.Status)
                    {
                        case "Reserved": bucket.Reserved += allocation.AllocatedQuantity; break;
                        case "Exception Approved": bucket.Approved += allocation.AllocatedQuantity; break;
                        case "PendingException": bucket.Pending += allocation.AllocatedQuantity; break;
                    }
                    bucket.Conflict |= conflicts.ContainsKey(allocation.AllocationId);
                    bucket.Items.Add(MapBrief(allocation, pendingIds.Contains(allocation.AllocationId)));
                    usages[date] = bucket;
                }
            }
            return new ResourceLaneView(resource.ResourceId, resource.Name, resource.Type, TypeLabel(resource.Type), resource.TotalQuantity,
                usages.OrderBy(u => u.Key)
                    .Select(u => new ResourceUsageView(resource.ResourceId, u.Key, u.Value.Reserved, u.Value.Approved, u.Value.Pending, u.Value.Conflict, u.Value.Items))
                    .ToList());
        }).ToList();

        var bookedSlots = bookings.Count;
        var availableSlots = halls.Sum(h => h.Slots.Count) * (to.DayNumber - from.DayNumber + 1);
        var summary = new CalendarSummaryView(
            bookedSlots,
            allocations.Count,
            conflicts.Count,
            pendingRequests,
            bookedSlots,
            availableSlots,
            availableSlots == 0 ? 0 : Math.Round(bookedSlots * 100m / availableSlots, 1));

        return new ResourceCalendarView(from, to,
            halls.Select(h => new CalendarHallView(h.HallId, h.Name, h.MinCapacity, h.MaxCapacity,
                h.Slots.OrderBy(s => s.StartTime).Select(s => new CalendarSlotView(s.SlotId, s.SlotName, s.StartTime, s.EndTime)).ToList())).ToList(),
            projects.Select(p => new CalendarProjectView(p.ProjectId, p.ProjectCode, p.Customer.FullName, p.HallId, p.Hall.Name, p.SlotId, p.Slot.SlotName, p.EventDate)).ToList(),
            bookings.Select(p => new CalendarBookingView(p.ProjectId, p.ProjectCode, p.Customer.FullName, p.Hall.Name, p.HallId, p.SlotId, p.EventDate, p.Status)).ToList(),
            allResources.Select(r => new ResourceOptionView(r.ResourceId, r.Name, r.Type, TypeLabel(r.Type), r.TotalQuantity)).ToList(),
            types, lanes, summary);
    }

    public async Task<IReadOnlyList<ResourceOptionView>> GetResourcesAsync(CancellationToken cancellationToken = default) =>
        await db.Resources.AsNoTracking()
            .OrderBy(r => r.Type).ThenBy(r => r.Name)
            .Select(r => new ResourceOptionView(r.ResourceId, r.Name, r.Type, TypeLabel(r.Type), r.TotalQuantity))
            .ToListAsync(cancellationToken);

    // ── M-22: one allocation with its clashing events ─────────────────────────

    public async Task<ResourceAllocationDetailView?> GetAllocationAsync(int allocationId, int managerId, CancellationToken cancellationToken = default)
    {
        var allocation = await db.ResourceAllocations.AsNoTracking()
            .Include(a => a.Resource).Include(a => a.Hall)
            .Include(a => a.Project).ThenInclude(p => p.Customer)
            .FirstOrDefaultAsync(a => a.AllocationId == allocationId && a.Project.ManagerId == managerId, cancellationToken);
        if (allocation is null) return null;

        var clashing = await db.ResourceAllocations.AsNoTracking()
            .Include(a => a.Project).ThenInclude(p => p.Customer).Include(a => a.Hall)
            .Where(a => a.ResourceId == allocation.ResourceId && a.AllocationId != allocation.AllocationId
                && a.Status != "Released"
                && a.StartTime < allocation.EndTime && allocation.StartTime < a.EndTime)
            .OrderBy(a => a.StartTime)
            .ToListAsync(cancellationToken);
        var clashingIds = clashing.Select(a => a.AllocationId).ToList();
        var clashingPending = clashingIds.Count == 0
            ? new HashSet<int>()
            : (await db.ExceptionRequests.AsNoTracking()
                .Where(e => e.AllocationId != null && clashingIds.Contains(e.AllocationId.Value) && e.Status == "Pending")
                .Select(e => e.AllocationId!.Value)
                .ToListAsync(cancellationToken)).ToHashSet();

        var request = await db.ExceptionRequests.AsNoTracking()
            .Where(e => e.AllocationId == allocation.AllocationId)
            .OrderByDescending(e => e.ExceptionRequestId)
            .FirstOrDefaultAsync(cancellationToken);

        var otherQuantity = clashing.Where(ConsumesCapacity).Sum(a => a.AllocatedQuantity);
        var shortage = Math.Max(0, otherQuantity + allocation.AllocatedQuantity - allocation.Resource.TotalQuantity);
        var hasConflict = ConsumesCapacity(allocation) && shortage > 0;
        var pending = request is { Status: "Pending" };

        return new ResourceAllocationDetailView(
            allocation.AllocationId,
            allocation.ResourceId, allocation.Resource.Name, allocation.Resource.Type, TypeLabel(allocation.Resource.Type), allocation.Resource.TotalQuantity,
            allocation.ProjectId, allocation.Project.ProjectCode, allocation.Project.Customer.FullName,
            allocation.HallId, allocation.Hall.Name, allocation.Project.EventDate,
            UtcValue(allocation.StartTime), UtcValue(allocation.EndTime), allocation.AllocatedQuantity, allocation.Status,
            allocation.IsExceptionApproved,
            allocation.ExceptionApprovedAt is { } approvedAt ? UtcValue(approvedAt) : null,
            allocation.ExceptionNote,
            pending ? request!.RequestCode : null,
            pending ? request!.RequestNote : null,
            otherQuantity, shortage, hasConflict,
            allocation.Status == "Reserved" && hasConflict && !pending,
            clashing.Select(a => MapBrief(a, clashingPending.Contains(a.AllocationId))).ToList());
    }

    // ── M-21: create / reschedule / substitute ────────────────────────────────

    public async Task<SaveAllocationResult?> SaveAsync(int? allocationId, ResourceAllocationCommand command, int managerId, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.Include(p => p.Slot)
            .FirstOrDefaultAsync(p => p.ProjectId == command.ProjectId && p.ManagerId == managerId, cancellationToken);
        if (project is null) return null;
        var resource = await db.Resources.FirstOrDefaultAsync(r => r.ResourceId == command.ResourceId, cancellationToken);
        if (resource is null) return null;

        if (project.Status is "Settled" or "Closed")
            throw new InvalidOperationException("Dự án đã quyết toán hoặc đã đóng nên không thể phân bổ tài nguyên.");
        if (command.Quantity <= 0)
            throw new InvalidOperationException("Số lượng phân bổ phải lớn hơn 0.");
        if (command.Quantity > resource.TotalQuantity)
            throw new InvalidOperationException($"Số lượng yêu cầu vượt quá tổng {resource.Name} hiện có ({resource.TotalQuantity}).");

        var start = AsUtc(command.StartTime);
        var end = AsUtc(command.EndTime);
        if (end <= start) throw new InvalidOperationException("Giờ kết thúc phải sau giờ bắt đầu.");
        if (DateOnly.FromDateTime(Local(start)) != project.EventDate || DateOnly.FromDateTime(Local(end)) != project.EventDate)
            throw new InvalidOperationException("Thời gian phân bổ phải nằm trong ngày diễn ra tiệc.");

        ResourceAllocation? allocation = null;
        if (allocationId is { } id)
        {
            allocation = await db.ResourceAllocations.Include(a => a.Project)
                .FirstOrDefaultAsync(a => a.AllocationId == id && a.Project.ManagerId == managerId, cancellationToken);
            if (allocation is null) return null;
            if (allocation.Status == "Released")
                throw new InvalidOperationException("Phân bổ này đã được thu hồi nên không thể chỉnh sửa.");
            if (allocation.Status == "PendingException")
                throw new InvalidOperationException("Phân bổ đang chờ Admin quyết định ngoại lệ nên không thể chỉnh sửa.");
            if (allocation.Status == "Exception Approved")
                throw new InvalidOperationException("Phân bổ đã được Admin chấp thuận ngoại lệ. Hãy thu hồi và tạo phân bổ mới nếu cần đổi.");
        }

        var conflict = await BuildConflictAsync(resource, allocationId, start, end, command.Quantity, cancellationToken);
        string? note = null;
        if (conflict is not null)
        {
            if (!command.RequestException) return new SaveAllocationResult(null, conflict);
            note = (command.ExceptionNote ?? "").Trim();
            if (note.Length == 0) throw new InvalidOperationException("Trường Ghi chú gửi Admin là bắt buộc.");
            if (note.Length > 300) throw new InvalidOperationException("Vượt quá độ dài tối đa 300 ký tự (MSG16).");
            if (allocationId is { } existingAllocation && await db.ExceptionRequests.AnyAsync(e => e.AllocationId == existingAllocation && e.Status == "Pending", cancellationToken))
                throw new InvalidOperationException("Đã có yêu cầu ngoại lệ đang chờ Admin quyết định cho phân bổ này.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (allocation is null)
        {
            allocation = new ResourceAllocation
            {
                ResourceId = resource.ResourceId,
                HallId = project.HallId,
                ProjectId = project.ProjectId,
                StartTime = start,
                EndTime = end,
                AllocatedQuantity = command.Quantity,
                Status = "Reserved"
            };
            db.ResourceAllocations.Add(allocation);
        }
        else
        {
            allocation.ResourceId = resource.ResourceId;
            allocation.HallId = project.HallId;
            allocation.ProjectId = project.ProjectId;
            allocation.StartTime = start;
            allocation.EndTime = end;
            allocation.AllocatedQuantity = command.Quantity;
            allocation.Status = "Reserved";
        }

        if (conflict is not null)
        {
            allocation.Status = "PendingException";
            await db.SaveChangesAsync(cancellationToken);   // the exception request needs the allocation id
            db.ExceptionRequests.Add(new ExceptionRequest
            {
                RequestCode = await NextExceptionCodeAsync(cancellationToken),
                SourceType = "ResourceConflict",
                ProjectId = project.ProjectId,
                AllocationId = allocation.AllocationId,
                RequestedBy = managerId,
                RequestNote = note!,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SaveAllocationResult(await GetAllocationAsync(allocation.AllocationId, managerId, cancellationToken), null);
    }

    public async Task<ResourceAllocationDetailView?> ReleaseAsync(int allocationId, int managerId, CancellationToken cancellationToken = default)
    {
        var allocation = await db.ResourceAllocations.Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.AllocationId == allocationId && a.Project.ManagerId == managerId, cancellationToken);
        if (allocation is null) return null;
        if (allocation.Status == "Released") throw new InvalidOperationException("Phân bổ này đã được thu hồi.");
        if (allocation.Status == "PendingException")
            throw new InvalidOperationException("Phân bổ đang chờ Admin quyết định ngoại lệ nên chưa thể thu hồi.");

        allocation.Status = "Released";
        await db.SaveChangesAsync(cancellationToken);
        return await GetAllocationAsync(allocationId, managerId, cancellationToken);
    }

    public async Task<ResourceAllocationDetailView?> RequestExceptionAsync(int allocationId, string note, int managerId, CancellationToken cancellationToken = default)
    {
        var allocation = await db.ResourceAllocations.Include(a => a.Resource).Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.AllocationId == allocationId && a.Project.ManagerId == managerId, cancellationToken);
        if (allocation is null) return null;
        if (allocation.Status != "Reserved")
            throw new InvalidOperationException("Chỉ phân bổ đang hiệu lực mới có thể đề nghị Admin chấp thuận ngoại lệ.");

        var cleanNote = (note ?? "").Trim();
        if (cleanNote.Length == 0) throw new InvalidOperationException("Trường Ghi chú gửi Admin là bắt buộc.");
        if (cleanNote.Length > 300) throw new InvalidOperationException("Vượt quá độ dài tối đa 300 ký tự (MSG16).");

        var conflict = await BuildConflictAsync(allocation.Resource, allocation.AllocationId, allocation.StartTime, allocation.EndTime, allocation.AllocatedQuantity, cancellationToken);
        if (conflict is null)
            throw new InvalidOperationException("Phân bổ này không còn xung đột nên không cần ngoại lệ.");
        if (await db.ExceptionRequests.AnyAsync(e => e.AllocationId == allocationId && e.Status == "Pending", cancellationToken))
            throw new InvalidOperationException("Đã có yêu cầu ngoại lệ đang chờ Admin quyết định cho phân bổ này.");

        allocation.Status = "PendingException";
        db.ExceptionRequests.Add(new ExceptionRequest
        {
            RequestCode = await NextExceptionCodeAsync(cancellationToken),
            SourceType = "ResourceConflict",
            ProjectId = allocation.ProjectId,
            AllocationId = allocation.AllocationId,
            RequestedBy = managerId,
            RequestNote = cleanNote,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return await GetAllocationAsync(allocationId, managerId, cancellationToken);
    }

    // ── Conflict detection (GBR-09) ───────────────────────────────────────────

    /// <summary>Allocation id → (quantity allocated by others, shortage) for every over-capacity allocation.</summary>
    private static Dictionary<int, (int Other, int Shortage)> ComputeConflicts(IReadOnlyList<ResourceAllocation> allocations)
    {
        var result = new Dictionary<int, (int, int)>();
        foreach (var group in allocations.GroupBy(a => a.ResourceId))
        {
            var list = group.ToList();
            var total = list[0].Resource.TotalQuantity;
            foreach (var allocation in list)
            {
                if (!ConsumesCapacity(allocation)) continue;
                var other = list
                    .Where(b => b.AllocationId != allocation.AllocationId && ConsumesCapacity(b)
                        && b.StartTime < allocation.EndTime && allocation.StartTime < b.EndTime)
                    .Sum(b => b.AllocatedQuantity);
                var over = other + allocation.AllocatedQuantity - total;
                if (over > 0) result[allocation.AllocationId] = (other, over);
            }
        }
        return result;
    }

    private async Task<ResourceConflictView?> BuildConflictAsync(Resource resource, int? excludeAllocationId, DateTime start, DateTime end, int quantity, CancellationToken cancellationToken)
    {
        var clashing = await db.ResourceAllocations.AsNoTracking()
            .Include(a => a.Project).ThenInclude(p => p.Customer).Include(a => a.Hall)
            .Where(a => a.ResourceId == resource.ResourceId
                && a.Status != "Released" && a.Status != "PendingException"
                && a.StartTime < end && start < a.EndTime
                && (excludeAllocationId == null || a.AllocationId != excludeAllocationId))
            .OrderBy(a => a.StartTime)
            .ToListAsync(cancellationToken);

        var other = clashing.Sum(a => a.AllocatedQuantity);
        if (other + quantity <= resource.TotalQuantity) return null;

        var clashingIds = clashing.Select(a => a.AllocationId).ToList();
        var pending = clashingIds.Count == 0
            ? new HashSet<int>()
            : (await db.ExceptionRequests.AsNoTracking()
                .Where(e => e.AllocationId != null && clashingIds.Contains(e.AllocationId.Value) && e.Status == "Pending")
                .Select(e => e.AllocationId!.Value)
                .ToListAsync(cancellationToken)).ToHashSet();

        return new ResourceConflictView(resource.ResourceId, resource.Name, TypeLabel(resource.Type), resource.TotalQuantity,
            quantity, other, other + quantity - resource.TotalQuantity,
            clashing.Select(a => MapBrief(a, pending.Contains(a.AllocationId))).ToList());
    }

    // ── Small helpers ─────────────────────────────────────────────────────────

    private static AllocationBriefView MapBrief(ResourceAllocation allocation, bool hasPendingException) =>
        new(allocation.AllocationId, allocation.ProjectId, allocation.Project.ProjectCode,
            allocation.Project.Customer.FullName, allocation.Hall.Name,
            UtcValue(allocation.StartTime), UtcValue(allocation.EndTime),
            allocation.AllocatedQuantity, allocation.Status, hasPendingException);

    /// <summary>Every Vietnamese calendar day the allocation touches, clamped to the requested range.</summary>
    private static IEnumerable<DateOnly> DatesOf(ResourceAllocation allocation, DateOnly from, DateOnly to)
    {
        var first = DateOnly.FromDateTime(Local(allocation.StartTime));
        var last = DateOnly.FromDateTime(Local(allocation.EndTime));
        if (last < first) last = first;
        for (var date = first; date <= last; date = date.AddDays(1))
            if (date >= from && date <= to) yield return date;
    }

    private async Task<string> NextExceptionCodeAsync(CancellationToken cancellationToken)
    {
        var head = $"EXC-{TodayVn().Year}-";
        var codes = await db.ExceptionRequests.AsNoTracking()
            .Where(e => e.RequestCode.StartsWith(head))
            .Select(e => e.RequestCode)
            .ToListAsync(cancellationToken);
        var next = codes
            .Select(code => int.TryParse(code[(code.LastIndexOf('-') + 1)..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{head}{next:000}";
    }
}
