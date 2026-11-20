using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// The event stage flow: operator workspace (stages, stage notes, cancel), the customer's read-only view of a booked event,
/// the customer lookup used to create an addon contract, and the daily lock / supplier-deadline jobs.
/// </summary>
public sealed class EventFlowRepository(WpsmDbContext db, IEmailOutbox emailOutbox, EmailOptions emailOptions) : IEventFlowRepository
{
    public Task<EventPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) => EventFlowSupport.LoadPolicyAsync(db, cancellationToken);

    public Task<string> GetHotlineAsync(CancellationToken cancellationToken = default) => EventFlowSupport.LoadHotlineAsync(db, cancellationToken);

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

    public async Task<StageNoteView> AddNoteAsync(string eventCode, int userId, StageNoteCommand command, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == eventCode, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy sự kiện.");
        var type = command.NoteType?.Trim() ?? "";
        if (!EventFlowSupport.NoteTypes.Contains(type) || type == "Adjustment")
            throw new InvalidOperationException("Loại ghi chú không hợp lệ (điều chỉnh dịch vụ được ghi khi lưu thay đổi dịch vụ).");
        var content = command.Content?.Trim() ?? "";
        if (content.Length is < 3 or > 1000) throw new InvalidOperationException("Nội dung ghi chú cần từ 3 đến 1000 ký tự.");
        if (project.Status == EventStatus.Cancelled) throw new InvalidOperationException("Sự kiện đã hủy.");

        var stage = EventStageRules.CurrentStage(project.Status, project.EventDate, VietnamTime.GetVietnamToday());
        var note = new StageNote { ProjectId = project.ProjectId, Stage = (byte)stage, NoteType = type, Content = content, CreatedBy = userId, CreatedAt = DateTime.UtcNow };
        db.StageNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(note).Reference(n => n.CreatedByNavigation).LoadAsync(cancellationToken);
        return EventFlowSupport.MapNote(note);
    }

    /// <summary>The customer cancels: the event frees its hall and slot, the deposit is kept (never refunded).</summary>
    public async Task<EventWorkspaceView?> CancelEventAsync(string eventCode, int userId, string reason, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == eventCode, cancellationToken);
        if (project is null) return null;
        reason = reason?.Trim() ?? "";
        if (reason.Length is < 5 or > 500) throw new InvalidOperationException("Hãy ghi lý do hủy (5 đến 500 ký tự).");
        if (!EventStatus.IsActive(project.Status) || VietnamTime.GetVietnamToday() >= project.EventDate)
            throw new InvalidOperationException("Chỉ hủy được sự kiện đang chuẩn bị (trước ngày cưới).");

        project.Status = EventStatus.Cancelled;
        project.CancelledAt = DateTime.UtcNow;
        project.CancelledBy = userId;
        project.CancelReason = reason;
        foreach (var addon in await db.AddonContracts.Where(a => a.ProjectId == project.ProjectId && a.Status == "PendingCustomer").ToListAsync(cancellationToken))
        {
            addon.Status = "Cancelled";
            addon.CancelledAt = DateTime.UtcNow;
            addon.CancelReason = "Sự kiện đã hủy";
        }
        foreach (var allocation in await db.ResourceAllocations.Where(a => a.ProjectId == project.ProjectId && a.Status != "Released").ToListAsync(cancellationToken))
            allocation.Status = "Released";
        db.StageNotes.Add(new StageNote
        {
            ProjectId = project.ProjectId, Stage = (byte)EventStageRules.CurrentStage(EventStatus.Adjusting, project.EventDate, VietnamTime.GetVietnamToday()),
            NoteType = "General", Content = $"Khách hủy sự kiện, không hoàn cọc. Lý do: {reason}", CreatedBy = userId, CreatedAt = DateTime.UtcNow
        });
        OperationsSupport.Notify(db, project.CustomerId, NotificationTypes.Payment,
            $"Sự kiện {project.ProjectCode} đã được hủy theo yêu cầu. Tiền đặt cọc không được hoàn lại theo hợp đồng.", OperationsSupport.Links.CustomerEvent);
        await db.SaveChangesAsync(cancellationToken);
        return await GetWorkspaceAsync(eventCode, cancellationToken);
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

    public async Task<int> LockDueEventsAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var lockBefore = today.AddDays(policy.LockDays);   // lock date = event date - LockDays <= today
        var due = await db.WeddingProjects.Include(p => p.Customer)
            .Where(p => p.Status == EventStatus.Adjusting && p.EventDate <= lockBefore).ToListAsync(cancellationToken);
        if (due.Count == 0) return 0;
        var hotline = await EventFlowSupport.LoadHotlineAsync(db, cancellationToken);
        var baseUrl = emailOptions.PublicBaseUrl?.TrimEnd('/') ?? "http://localhost:5173";
        foreach (var project in due)
        {
            project.Status = EventStatus.Locked;
            project.LockedAt = DateTime.UtcNow;
            var contacts = await EventFlowSupport.LoadContactsAsync(db, project.ProjectId, cancellationToken);
            var contactText = string.Join("<br>", contacts.Select(c => System.Net.WebUtility.HtmlEncode($"{c.FullName} – {c.Phone ?? c.Email}")));
            OperationsSupport.Notify(db, project.CustomerId, NotificationTypes.StageReminder,
                $"Dịch vụ của sự kiện {project.ProjectCode} đã được chốt. Cần thay đổi, hãy liên hệ người phụ trách hoặc hotline {hotline}.",
                OperationsSupport.Links.CustomerEvent);
            foreach (var operatorId in await EventFlowSupport.OperatorIdsAsync(db, project.ProjectId, cancellationToken))
                OperationsSupport.Notify(db, operatorId, NotificationTypes.StageReminder,
                    $"[{project.ProjectCode}] Đã khóa dịch vụ. Đặt nhà cung cấp và tải minh chứng trước {EventStageRules.SupplierDeadline(project.EventDate, policy):dd/MM/yyyy}.",
                    OperationsSupport.Links.ManagerSuppliers(project.ProjectCode));
            emailOutbox.Enqueue(project.Customer.Email, $"Sự kiện {project.ProjectCode} đã chốt dịch vụ",
                EmailTemplates.EventLocked(project.Customer.FullName, project.ProjectCode, project.EventDate, contactText, hotline, baseUrl), "Reminder", project.ProjectId);
        }
        await db.SaveChangesAsync(cancellationToken);
        return due.Count;
    }

    public async Task<int> RemindSupplierDeadlinesAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var deadlineBefore = today.AddDays(policy.SupplierDeadlineDays);   // supplier deadline <= today
        var events = await db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(p => p.SpecItems).ThenInclude(s => s.CatalogItem)
            .Include(p => p.SupplierEvidences)
            .Where(p => p.Status == EventStatus.Locked && p.EventDate <= deadlineBefore && p.EventDate >= today)
            .ToListAsync(cancellationToken);
        var startOfDay = DateTime.UtcNow.Date;
        var reminded = 0;
        foreach (var p in events)
        {
            var missing = p.SpecItems.Where(s => s.CatalogItem.RequiresSupplier && p.SupplierEvidences.All(e => e.SpecItemId != s.SpecItemId))
                .Select(s => s.CatalogItem.Name).ToList();
            if (missing.Count == 0) continue;
            var marker = $"[{p.ProjectCode}] Quá hạn đặt nhà cung cấp";
            foreach (var operatorId in await EventFlowSupport.OperatorIdsAsync(db, p.ProjectId, cancellationToken))
            {
                // One reminder per operator per day.
                if (await db.Notifications.AnyAsync(n => n.RecipientId == operatorId && n.Message.StartsWith(marker) && n.CreatedAt >= startOfDay, cancellationToken)) continue;
                OperationsSupport.Notify(db, operatorId, NotificationTypes.StageReminder,
                    $"{marker}: còn thiếu minh chứng cho {string.Join(", ", missing.Take(4))}{(missing.Count > 4 ? "…" : "")}.",
                    OperationsSupport.Links.ManagerSuppliers(p.ProjectCode));
                reminded++;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return reminded;
    }
}
