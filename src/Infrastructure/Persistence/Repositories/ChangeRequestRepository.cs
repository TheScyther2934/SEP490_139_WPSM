using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// FE-05 Change Request (C-05, C-06, M-07, M-08).
/// Owns the whole CR lifecycle: ticket creation, negotiation, mutual approval, addendum generation and
/// Admin penalty waivers. Business rules implemented here:
///   GBR-02 every new unit price must sit inside the PriceFrame in force today;
///   GBR-03 CR Price Delta = Σ item deltas, Penalty = |delta| × T-day rate, Total = delta + penalty;
///   GBR-04 a CR is Approved only after both the Customer and the Manager approve;
///   GBR-05 an approved CR that changes a Frozen BEO flags it Stale;
///   GBR-13 CR History is append-only — this class only ever adds rows.
/// </summary>
public sealed class ChangeRequestRepository(WpsmDbContext db) : IChangeRequestRepository
{
    private static readonly string[] CrTypes = ["Menu change", "Decor change", "Table count", "Script edit", "Other"];
    private static readonly string[] ClosedStatuses = ["Approved", "Rejected"];

    private static readonly TimeZoneInfo VnTz = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    // DATETIME2 holds UTC; the T-day arithmetic uses the Vietnamese calendar day.
    private static DateOnly TodayVn() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<CrListView?> GetListAsync(string? projectCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;

        var tickets = await db.ChangeRequests.AsNoTracking()
            .Include(c => c.CreatedByNavigation)
            .Include(c => c.Critems)
            .Where(c => c.ProjectId == project.ProjectId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var pending = await db.ExceptionRequests.AsNoTracking()
            .Where(e => e.ProjectId == project.ProjectId && e.Crid != null && e.Status == "Pending")
            .Select(e => e.Crid!.Value)
            .ToListAsync(cancellationToken);
        var pendingSet = pending.ToHashSet();

        var items = tickets.Select(c => new CrListItemView(
            c.Crcode, c.Type, c.Status, c.Description,
            c.CostImpact, c.PenaltyFee, c.TotalVariance ?? c.CostImpact + c.PenaltyFee,
            c.CreatedByNavigation.Role, c.CreatedByNavigation.FullName, AsUtc(c.CreatedAt),
            c.Critems.Count,
            c.CustomerApprovedAt is not null,
            c.ManagerApprovedAt is not null,
            pendingSet.Contains(c.Crid) ? "Pending" : null)).ToList();

        var summary = new CrSummaryView(
            items.Count,
            items.Count(i => i.Status == "Draft"),
            items.Count(i => i.Status == "Negotiating"),
            items.Count(i => i.Status == "Approved"),
            items.Count(i => i.Status == "Rejected"),
            items.Where(i => i.Status == "Approved").Sum(i => i.TotalImpact),
            items.Where(i => i.Status is "Draft" or "Negotiating").Sum(i => i.TotalImpact),
            items.Where(i => i.Status is "Draft" or "Negotiating").Sum(i => i.PenaltyFee));

        return new CrListView(project.ProjectCode, project.Customer.FullName, project.Hall.Name, project.Slot.SlotName,
            project.EventDate, project.BaseContractValue, project.CurrentTotal, summary, items);
    }

    public async Task<CrFormView?> GetFormAsync(string? projectCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken, withSpecItems: true);
        if (project is null) return null;

        var spec = BaselineSpecItems(project);
        var frames = await CurrentFramesAsync(spec.Select(s => s.CatalogItemId), cancellationToken);
        var today = TodayVn();
        var days = DaysToEvent(project.EventDate, today);
        var rules = await PenaltyRulesAsync(today, cancellationToken);
        var rate = rules.FirstOrDefault(r => days >= r.TDayMin && days <= r.TDayMax)?.RatePercent ?? 0m;

        var items = spec.OrderBy(s => s.CatalogItem?.Category).ThenBy(s => s.CatalogItem?.Name)
            .Select(s => new CrSpecOptionView(
                s.SpecItemId,
                s.CatalogItem?.Name ?? s.Description,
                WpsmCategoryNames.ToDisplay(s.CatalogItem?.Category ?? ""),
                s.CatalogItem?.Unit ?? "",
                s.Description,
                s.Quantity,
                s.UnitPrice,
                MinPrice(frames, s.CatalogItemId),
                MaxPrice(frames, s.CatalogItemId),
                frames.ContainsKey(s.CatalogItemId)))
            .ToList();

        return new CrFormView(project.ProjectCode, project.Customer.FullName, project.Hall.Name, project.Slot.SlotName,
            project.EventDate, days, project.BaseContractValue, project.CurrentTotal, items, rules, rate,
            PenaltyWindowLabel(rules), BlockedReason(project, today));
    }

    public async Task<CrDetailView?> GetDetailAsync(string? projectCode, string crCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;

        var cr = await db.ChangeRequests.AsNoTracking()
            .Include(c => c.CreatedByNavigation)
            .Include(c => c.Critems).ThenInclude(i => i.TargetSpecItem).ThenInclude(s => s.CatalogItem)
            .Include(c => c.Crhistories).ThenInclude(h => h.Actor)
            .Include(c => c.ContractAddendum)
            .FirstOrDefaultAsync(c => c.ProjectId == project.ProjectId && c.Crcode == crCode, cancellationToken);
        if (cr is null) return null;

        var frames = await CurrentFramesAsync(cr.Critems.Select(i => i.TargetSpecItem.CatalogItemId), cancellationToken);
        var rules = await PenaltyRulesAsync(TodayVn(), cancellationToken);
        var exception = await db.ExceptionRequests.AsNoTracking()
            .Where(e => e.Crid == cr.Crid)
            .OrderByDescending(e => e.ExceptionRequestId)
            .Select(e => e.Status)
            .FirstOrDefaultAsync(cancellationToken);
        var beoStatus = await db.Beos.AsNoTracking()
            .Where(b => b.ProjectId == project.ProjectId)
            .OrderByDescending(b => b.Version)
            .Select(b => b.Status)
            .FirstOrDefaultAsync(cancellationToken);

        var items = cr.Critems
            .OrderBy(i => i.TargetSpecItem.CatalogItem?.Category).ThenBy(i => i.TargetSpecItem.CatalogItem?.Name)
            .Select(i => new CrItemView(
                i.TargetSpecItemId,
                i.TargetSpecItem.CatalogItem?.Name ?? i.TargetSpecItem.Description,
                WpsmCategoryNames.ToDisplay(i.TargetSpecItem.CatalogItem?.Category ?? ""),
                i.TargetSpecItem.CatalogItem?.Unit ?? "",
                i.TargetSpecItem.Description,
                i.OldQuantity, i.OldUnitPrice, i.NewQuantity, i.NewUnitPrice,
                i.DeltaAmount ?? i.NewUnitPrice * i.NewQuantity - i.OldUnitPrice * i.OldQuantity,
                MinPrice(frames, i.TargetSpecItem.CatalogItemId),
                MaxPrice(frames, i.TargetSpecItem.CatalogItemId),
                frames.ContainsKey(i.TargetSpecItem.CatalogItemId)))
            .ToList();

        var history = cr.Crhistories.OrderBy(h => h.CreatedAt)
            .Select(h => new CrHistoryView(AsUtc(h.CreatedAt), h.Action, h.Actor.FullName, h.Actor.Role, h.Note))
            .ToList();

        var priceDelta = cr.CostImpact;
        var penaltyRate = cr.PenaltyRate ?? 0m;
        var penaltyFee = cr.PenaltyFee;
        var today = TodayVn();
        var days = DaysToEvent(project.EventDate, today);
        // An approved CR is already part of CurrentTotal (UC-22 step 5), so its projection is the current total.
        var projectedTotal = cr.Status == "Approved" ? project.CurrentTotal : project.CurrentTotal + priceDelta + penaltyFee;
        var cost = new CrCostView(priceDelta, penaltyRate, penaltyFee, priceDelta + penaltyFee,
            project.CurrentTotal, projectedTotal, days, penaltyRate > 0, PenaltyWindowLabel(rules));

        var negotiating = cr.Status == "Negotiating";
        var isDraft = cr.Status == "Draft";
        var mine = cr.CreatedBy == userId;
        var canEdit = isDraft && mine || negotiating && role == "Manager";
        var canApprove = negotiating && (role == "Manager" ? cr.ManagerApprovedAt is null : cr.CustomerApprovedAt is null);
        var canRequestException = negotiating && role == "Manager" && penaltyFee > 0 && exception != "Pending";

        return new CrDetailView(
            cr.Crcode, project.ProjectCode, project.Customer.FullName, project.Hall.Name, project.Slot.SlotName, project.EventDate,
            cr.Type, cr.Description, cr.Status, cr.CreatedByNavigation.Role, cr.CreatedByNavigation.FullName, AsUtc(cr.CreatedAt),
            AsUtc(cr.CustomerApprovedAt), AsUtc(cr.ManagerApprovedAt), cost, items, history, rules,
            cr.ContractAddendum?.AddendumCode, beoStatus, exception,
            canEdit, canApprove, negotiating, canRequestException);
    }

    // ── C-06: create / save draft ─────────────────────────────────────────────

    public async Task<CrDetailView?> CreateAsync(string? projectCode, int userId, string role, CrSaveCommand command, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken, withSpecItems: true);
        if (project is null) return null;
        var blocked = BlockedReason(project, TodayVn());
        if (blocked is not null) throw new InvalidOperationException(blocked);

        var (type, description) = ValidateTicket(command.Type, command.Description);
        var lines = ValidateLines(command.Items);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var cr = new ChangeRequest
        {
            Crcode = await NextCodeAsync("CR", cancellationToken),
            ProjectId = project.ProjectId,
            CreatedBy = userId,
            Type = type,
            Description = description,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        };
        db.ChangeRequests.Add(cr);
        await db.SaveChangesAsync(cancellationToken);

        var items = await ApplyLinesAsync(cr, project, lines, cancellationToken);
        await RecalculateAsync(cr, items, project.EventDate, TodayVn(), cancellationToken);

        if (command.Submit) cr.Status = "Negotiating";
        AddHistory(cr, userId, command.Submit ? "Submitted" : "Created", null);
        if (command.Submit) NotifyCounterparty(project, userId, cr, "vừa được gửi tới bạn để thương lượng");

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetDetailAsync(project.ProjectCode, cr.Crcode, userId, role, cancellationToken);
    }

    public async Task<CrDetailView?> UpdateDraftAsync(string? projectCode, string crCode, int userId, string role, CrSaveCommand command, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken, withSpecItems: true);
        if (project is null) return null;
        var cr = await LoadTicketAsync(project.ProjectId, crCode, cancellationToken);
        if (cr is null) return null;
        if (cr.Status != "Draft")
            throw new InvalidOperationException("Chỉ bản nháp mới có thể chỉnh sửa. Yêu cầu đã gửi được đàm phán bằng đề xuất đối ứng.");
        if (cr.CreatedBy != userId)
            throw new InvalidOperationException("Chỉ người tạo mới có thể chỉnh sửa bản nháp này.");

        var (type, description) = ValidateTicket(command.Type, command.Description);
        var lines = ValidateLines(command.Items);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        cr.Type = type;
        cr.Description = description;
        var items = await ApplyLinesAsync(cr, project, lines, cancellationToken);
        await RecalculateAsync(cr, items, project.EventDate, TodayVn(), cancellationToken);
        AddHistory(cr, userId, "Updated", null);
        if (command.Submit)
        {
            cr.Status = "Negotiating";
            AddHistory(cr, userId, "Submitted", null);
            NotifyCounterparty(project, userId, cr, "vừa được gửi tới bạn để thương lượng");
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetDetailAsync(project.ProjectCode, cr.Crcode, userId, role, cancellationToken);
    }

    public async Task<CrDetailView?> SubmitAsync(string? projectCode, string crCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;
        var cr = await LoadTicketAsync(project.ProjectId, crCode, cancellationToken);
        if (cr is null) return null;
        if (cr.Status != "Draft") throw new InvalidOperationException("Chỉ bản nháp mới có thể gửi để thương lượng.");
        if (cr.CreatedBy != userId) throw new InvalidOperationException("Chỉ người tạo mới có thể gửi bản nháp này.");
        if (cr.Critems.Count == 0) throw new InvalidOperationException("Yêu cầu thay đổi cần ít nhất một hạng mục.");

        cr.Status = "Negotiating";
        AddHistory(cr, userId, "Submitted", null);
        NotifyCounterparty(project, userId, cr, "vừa được gửi tới bạn để thương lượng");
        await db.SaveChangesAsync(cancellationToken);
        return await GetDetailAsync(project.ProjectCode, cr.Crcode, userId, role, cancellationToken);
    }

    // ── M-08: counter-proposal, decisions and exceptions ──────────────────────

    public async Task<CrDetailView?> CounterAsync(string? projectCode, string crCode, int userId, CrCounterCommand command, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, "Manager", cancellationToken, withSpecItems: true);
        if (project is null) return null;
        var cr = await LoadTicketAsync(project.ProjectId, crCode, cancellationToken);
        if (cr is null) return null;
        if (cr.Status != "Negotiating")
            throw new InvalidOperationException("Chỉ yêu cầu đang thương lượng mới có thể nhận đề xuất đối ứng.");

        var lines = ValidateLines(command.Items);
        var note = Trim(command.Note, 500);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // New terms void any earlier consent: both parties must approve the counter-proposal (GBR-04).
        cr.CustomerApprovedAt = null;
        cr.ManagerApprovedAt = null;
        var items = await ApplyLinesAsync(cr, project, lines, cancellationToken);
        await RecalculateAsync(cr, items, project.EventDate, TodayVn(), cancellationToken);
        AddHistory(cr, userId, "CounterProposal", note);
        Notify(project.CustomerId, "CR update", $"Quản lý đã gửi đề xuất đối ứng cho yêu cầu {cr.Crcode}. Vui lòng xem chi tiết.", CustomerLink());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetDetailAsync(project.ProjectCode, cr.Crcode, userId, "Manager", cancellationToken);
    }

    public async Task<CrDetailView?> ApproveAsync(string? projectCode, string crCode, int userId, string role, string? note, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;
        var cr = await LoadTicketAsync(project.ProjectId, crCode, cancellationToken);
        if (cr is null) return null;
        if (cr.Status != "Negotiating")
            throw new InvalidOperationException("Chỉ yêu cầu đang thương lượng mới có thể được phê duyệt.");
        var cleanNote = Trim(note, 500);
        var now = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (role == "Manager")
        {
            if (cr.ManagerApprovedAt is not null) throw new InvalidOperationException("Bạn đã phê duyệt yêu cầu này.");
            cr.ManagerApprovedAt = now;
            AddHistory(cr, userId, "ManagerApproved", cleanNote);
        }
        else
        {
            if (cr.CustomerApprovedAt is not null) throw new InvalidOperationException("Bạn đã phê duyệt yêu cầu này.");
            cr.CustomerApprovedAt = now;
            AddHistory(cr, userId, "CustomerApproved", cleanNote);
        }

        if (cr.CustomerApprovedAt is not null && cr.ManagerApprovedAt is not null)
            await ApproveAndGenerateAddendumAsync(cr, project, userId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetDetailAsync(project.ProjectCode, cr.Crcode, userId, role, cancellationToken);
    }

    public async Task<CrDetailView?> RejectAsync(string? projectCode, string crCode, int userId, string role, string note, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;
        var cr = await LoadTicketAsync(project.ProjectId, crCode, cancellationToken);
        if (cr is null) return null;
        if (ClosedStatuses.Contains(cr.Status))
            throw new InvalidOperationException("Yêu cầu này đã đóng nên không thể từ chối.");
        var cleanNote = (note ?? "").Trim();
        if (cleanNote.Length == 0) throw new InvalidOperationException("Trường Lý do từ chối là bắt buộc.");
        if (cleanNote.Length > 500) throw new InvalidOperationException("Vượt quá độ dài tối đa 500 ký tự (MSG16).");
        if (cr.Status == "Draft" && cr.CreatedBy != userId)
            throw new InvalidOperationException("Chỉ người tạo mới có thể hủy bản nháp này.");

        cr.Status = "Rejected";
        AddHistory(cr, userId, "Rejected", cleanNote);
        NotifyCounterparty(project, userId, cr, $"đã bị từ chối: {cleanNote}");
        await db.SaveChangesAsync(cancellationToken);
        return await GetDetailAsync(project.ProjectCode, cr.Crcode, userId, role, cancellationToken);
    }

    public async Task<CrDetailView?> RequestExceptionAsync(string? projectCode, string crCode, int userId, string note, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, "Manager", cancellationToken);
        if (project is null) return null;
        var cr = await LoadTicketAsync(project.ProjectId, crCode, cancellationToken);
        if (cr is null) return null;
        if (cr.Status != "Negotiating")
            throw new InvalidOperationException("Chỉ có thể đề nghị ngoại lệ cho yêu cầu đang thương lượng.");
        if (cr.PenaltyFee <= 0)
            throw new InvalidOperationException("Yêu cầu này không có phí phạt nên không cần ngoại lệ.");
        var cleanNote = (note ?? "").Trim();
        if (cleanNote.Length == 0) throw new InvalidOperationException("Trường Ghi chú gửi Admin là bắt buộc.");
        if (cleanNote.Length > 300) throw new InvalidOperationException("Vượt quá độ dài tối đa 300 ký tự (MSG16).");
        if (await db.ExceptionRequests.AnyAsync(e => e.Crid == cr.Crid && e.Status == "Pending", cancellationToken))
            throw new InvalidOperationException("Đã có yêu cầu ngoại lệ đang chờ Admin quyết định cho yêu cầu này.");

        db.ExceptionRequests.Add(new ExceptionRequest
        {
            RequestCode = await NextCodeAsync("EXC", cancellationToken),
            SourceType = "CrPenaltyWaiver",
            ProjectId = project.ProjectId,
            Crid = cr.Crid,
            RequestedBy = userId,
            RequestNote = cleanNote,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });
        AddHistory(cr, userId, "ExceptionRequested", cleanNote);
        var adminIds = await db.Users.AsNoTracking()
            .Where(u => u.Role == "Admin" && u.Status == "Active")
            .Select(u => u.UserId)
            .ToListAsync(cancellationToken);
        foreach (var adminId in adminIds)
            Notify(adminId, "CR update", $"Quản lý đề nghị miễn phí phạt cho yêu cầu {cr.Crcode}.", "/admin/exception-requests");

        await db.SaveChangesAsync(cancellationToken);
        return await GetDetailAsync(project.ProjectCode, cr.Crcode, userId, "Manager", cancellationToken);
    }

    // ── Approval side effects (UC-22 steps 5-6) ───────────────────────────────

    private async Task ApproveAndGenerateAddendumAsync(ChangeRequest cr, WeddingProject project, int actorId, CancellationToken cancellationToken)
    {
        // GBR-03: the T-day rate follows the approval date, so the penalty is finalised here — unless the
        // Admin already granted a waiver (UC-47), in which case the waiver must survive the approval.
        var waived = await db.ExceptionRequests.AsNoTracking()
            .AnyAsync(e => e.Crid == cr.Crid && e.SourceType == "CrPenaltyWaiver" && e.Status == "Approved", cancellationToken);
        if (waived) cr.PenaltyFee = 0;
        else
        {
            var items = await db.Critems.Where(i => i.Crid == cr.Crid).ToListAsync(cancellationToken);
            await RecalculateAsync(cr, items, project.EventDate, TodayVn(), cancellationToken);
        }
        cr.Status = "Approved";

        if (!await db.ContractAddenda.AnyAsync(a => a.Crid == cr.Crid, cancellationToken))
        {
            var addendum = new ContractAddendum
            {
                AddendumCode = await NextCodeAsync("PL", cancellationToken),
                Crid = cr.Crid,
                ProjectId = project.ProjectId,
                TotalVariance = cr.CostImpact + cr.PenaltyFee,
                Status = "Generated"
            };
            db.ContractAddenda.Add(addendum);
            AddHistory(cr, actorId, "AddendumGenerated", addendum.AddendumCode);
        }

        // CurrentTotal = Base + Σ approved CR impact (M-09). The current CR is not committed yet.
        var otherImpact = await db.ChangeRequests.AsNoTracking()
            .Where(c => c.ProjectId == project.ProjectId && c.Status == "Approved" && c.Crid != cr.Crid)
            .SumAsync(c => (decimal?)c.TotalVariance, cancellationToken) ?? 0m;
        project.CurrentTotal = project.BaseContractValue + otherImpact + cr.CostImpact + cr.PenaltyFee;

        // GBR-05: an approved scope change makes a Frozen BEO Stale; a Draft BEO is recompiled by JOB-03.
        var beo = await db.Beos.Where(b => b.ProjectId == project.ProjectId)
            .OrderByDescending(b => b.Version).FirstOrDefaultAsync(cancellationToken);
        if (beo is { Status: "Frozen" }) beo.Status = "Stale";

        AddHistory(cr, actorId, "Approved", null);
        Notify(project.CustomerId, "CR update",
            $"Yêu cầu thay đổi {cr.Crcode} đã được cả hai bên phê duyệt. Phụ lục hợp đồng đã được tạo.", CustomerLink());
        Notify(project.ManagerId, "CR update",
            $"Yêu cầu thay đổi {cr.Crcode} đã được phê duyệt. Tổng hợp đồng đã được cập nhật.", ManagerLink(project));
    }

    // ── Ticket persistence helpers ────────────────────────────────────────────

    private async Task<List<Critem>> ApplyLinesAsync(ChangeRequest cr, WeddingProject project, IReadOnlyList<(int SpecItemId, int NewQuantity, decimal NewUnitPrice)> lines, CancellationToken cancellationToken)
    {
        var spec = BaselineSpecItems(project);
        var frames = await CurrentFramesAsync(spec.Select(s => s.CatalogItemId), cancellationToken);
        var existing = await db.Critems.Where(i => i.Crid == cr.Crid).ToListAsync(cancellationToken);
        var result = new List<Critem>(lines.Count);

        foreach (var line in lines)
        {
            var specItem = spec.FirstOrDefault(s => s.SpecItemId == line.SpecItemId)
                ?? throw new InvalidOperationException("Hạng mục đặc tả không thuộc baseline hiện tại của dự án.");
            var name = specItem.CatalogItem?.Name ?? specItem.Description;
            if (!frames.TryGetValue(specItem.CatalogItemId, out var frame))
                throw new InvalidOperationException($"'{name}' chưa có PriceFrame hiện hành nên chưa thể đổi giá (GBR-02).");
            if (line.NewUnitPrice < frame.Min || line.NewUnitPrice > frame.Max)
                throw new InvalidOperationException($"Đơn giá của '{name}' phải nằm trong khoảng {frame.Min:N0} - {frame.Max:N0} VND (MSG06).");

            var item = existing.FirstOrDefault(i => i.TargetSpecItemId == specItem.SpecItemId);
            if (item is null)
            {
                item = new Critem
                {
                    Crid = cr.Crid,
                    TargetSpecItemId = specItem.SpecItemId,
                    OldQuantity = specItem.Quantity,
                    OldUnitPrice = specItem.UnitPrice,
                    NewQuantity = line.NewQuantity,
                    NewUnitPrice = line.NewUnitPrice
                };
                db.Critems.Add(item);
            }
            else
            {
                item.OldQuantity = specItem.Quantity;
                item.OldUnitPrice = specItem.UnitPrice;
                item.NewQuantity = line.NewQuantity;
                item.NewUnitPrice = line.NewUnitPrice;
            }
            result.Add(item);
        }

        // A counter-proposal replaces the whole set: lines the Manager removed are deleted (GBR-13 keeps history).
        var kept = result.Select(i => i.TargetSpecItemId).ToHashSet();
        db.Critems.RemoveRange(existing.Where(i => !kept.Contains(i.TargetSpecItemId)));
        return result;
    }

    private async Task RecalculateAsync(ChangeRequest cr, IReadOnlyList<Critem> items, DateOnly eventDate, DateOnly on, CancellationToken cancellationToken)
    {
        var delta = items.Sum(i => i.NewUnitPrice * i.NewQuantity - i.OldUnitPrice * i.OldQuantity);
        var rules = await PenaltyRulesAsync(on, cancellationToken);
        var days = DaysToEvent(eventDate, on);
        var rate = rules.FirstOrDefault(r => days >= r.TDayMin && days <= r.TDayMax)?.RatePercent ?? 0m;
        cr.CostImpact = delta;
        cr.PenaltyRate = rate;
        cr.PenaltyFee = rate <= 0 ? 0m : decimal.Round(Math.Abs(delta) * rate / 100m, 2);
    }

    private async Task<ChangeRequest?> LoadTicketAsync(int projectId, string crCode, CancellationToken cancellationToken) =>
        await db.ChangeRequests
            .Include(c => c.Critems)
            .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Crcode == crCode, cancellationToken);

    private async Task<WeddingProject?> ResolveProjectAsync(string? projectCode, int userId, string role, CancellationToken cancellationToken, bool withSpecItems = false)
    {
        var query = db.WeddingProjects.Include(x => x.Customer).Include(x => x.Hall).Include(x => x.Slot).AsQueryable();
        if (withSpecItems) query = query.Include(x => x.SpecItems).ThenInclude(x => x.CatalogItem);
        query = role switch
        {
            "Customer" => query.Where(x => x.CustomerId == userId),
            "Manager" => query.Where(x => x.ManagerId == userId),
            _ => throw new InvalidOperationException("Vai trò không được hỗ trợ cho yêu cầu thay đổi.")
        };
        if (!string.IsNullOrWhiteSpace(projectCode)) query = query.Where(x => x.ProjectCode == projectCode.Trim());
        return await query.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    // ── Reads of reference data ───────────────────────────────────────────────

    private async Task<Dictionary<int, (decimal Min, decimal Max)>> CurrentFramesAsync(IEnumerable<int> catalogItemIds, CancellationToken cancellationToken)
    {
        var ids = catalogItemIds.Distinct().ToArray();
        var frames = new Dictionary<int, (decimal Min, decimal Max)>();
        if (ids.Length == 0) return frames;
        var today = TodayVn();
        var rows = await db.PriceFrames.AsNoTracking()
            .Where(x => ids.Contains(x.ItemId) && x.EffectiveFrom <= today)
            .Select(x => new { x.ItemId, x.MinPrice, x.MaxPrice, x.EffectiveFrom })
            .ToListAsync(cancellationToken);
        foreach (var group in rows.GroupBy(x => x.ItemId))
        {
            var latest = group.OrderByDescending(x => x.EffectiveFrom).First();
            frames[group.Key] = (latest.MinPrice, latest.MaxPrice);
        }
        return frames;
    }

    private static decimal MinPrice(Dictionary<int, (decimal Min, decimal Max)> frames, int itemId) =>
        frames.TryGetValue(itemId, out var frame) ? frame.Min : 0m;

    private static decimal MaxPrice(Dictionary<int, (decimal Min, decimal Max)> frames, int itemId) =>
        frames.TryGetValue(itemId, out var frame) ? frame.Max : 0m;

    private async Task<IReadOnlyList<PenaltyRuleView>> PenaltyRulesAsync(DateOnly on, CancellationToken cancellationToken)
    {
        var rows = await db.PolicyConfigs.AsNoTracking()
            .Where(x => x.PolicyType == "CR_PENALTY" && x.EffectiveFrom <= on && x.TdayMin != null && x.TdayMax != null)
            .Select(x => new { TDayMin = x.TdayMin!.Value, TDayMax = x.TdayMax!.Value, x.RatePercent, x.EffectiveFrom })
            .ToListAsync(cancellationToken);
        return rows.GroupBy(x => (x.TDayMin, x.TDayMax))
            .Select(g => g.OrderByDescending(x => x.EffectiveFrom).First())
            .OrderBy(x => x.TDayMin)
            .Select(x => new PenaltyRuleView(x.TDayMin, x.TDayMax, x.RatePercent))
            .ToList();
    }

    private static List<SpecItem> BaselineSpecItems(WeddingProject project)
    {
        var baseline = project.SpecItems.Where(s => s.IsBaseline && s.Version == 1).ToList();
        return baseline.Count > 0 ? baseline : project.SpecItems.ToList();
    }

    // ── Codes ─────────────────────────────────────────────────────────────────

    /// <summary>Codes read CR-2026-001 / PL-2026-001 / EXC-2026-001 (max suffix + 1 for the current year).</summary>
    private async Task<string> NextCodeAsync(string prefix, CancellationToken cancellationToken)
    {
        var year = TodayVn().Year;
        var head = $"{prefix}-{year}-";
        var codes = prefix switch
        {
            "CR" => await db.ChangeRequests.AsNoTracking().Where(c => c.Crcode.StartsWith(head)).Select(c => c.Crcode).ToListAsync(cancellationToken),
            "PL" => await db.ContractAddenda.AsNoTracking().Where(a => a.AddendumCode.StartsWith(head)).Select(a => a.AddendumCode).ToListAsync(cancellationToken),
            _ => await db.ExceptionRequests.AsNoTracking().Where(e => e.RequestCode.StartsWith(head)).Select(e => e.RequestCode).ToListAsync(cancellationToken)
        };
        var next = codes
            .Select(code => int.TryParse(code[(code.LastIndexOf('-') + 1)..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return $"{head}{next:000}";
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private static (string Type, string Description) ValidateTicket(string? type, string? description)
    {
        var cleanType = (type ?? "").Trim();
        if (!CrTypes.Contains(cleanType)) throw new InvalidOperationException("Loại yêu cầu thay đổi không hợp lệ.");
        var cleanDescription = (description ?? "").Trim();
        if (cleanDescription.Length == 0) throw new InvalidOperationException("Trường Lý do thay đổi là bắt buộc.");
        if (cleanDescription.Length > 500) throw new InvalidOperationException("Vượt quá độ dài tối đa 500 ký tự (MSG16).");
        return (cleanType, cleanDescription);
    }

    private static List<(int SpecItemId, int NewQuantity, decimal NewUnitPrice)> ValidateLines(IReadOnlyList<CrLineCommand>? items)
    {
        var lines = items?.ToList() ?? [];
        if (lines.Count == 0) throw new InvalidOperationException("Yêu cầu thay đổi cần ít nhất một hạng mục.");
        if (lines.Count > 20) throw new InvalidOperationException("Mỗi yêu cầu thay đổi tối đa 20 hạng mục.");
        if (lines.Any(l => l.NewQuantity < 0)) throw new InvalidOperationException("Số lượng mới không được âm.");
        if (lines.Any(l => l.NewUnitPrice < 0)) throw new InvalidOperationException("Đơn giá mới không được âm.");
        if (lines.GroupBy(l => l.SpecItemId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Mỗi hạng mục đặc tả chỉ được xuất hiện một lần trong yêu cầu thay đổi.");
        return lines.Select(l => (l.SpecItemId, l.NewQuantity, l.NewUnitPrice)).ToList();
    }

    private static string? BlockedReason(WeddingProject project, DateOnly today)
    {
        if (project.Status is "Settled" or "Closed")
            return "Dự án đã quyết toán hoặc đã đóng nên không thể tạo yêu cầu thay đổi.";
        if (project.BaselineFrozenAt is null)
            return "Baseline chưa được đóng băng nên chưa thể tạo yêu cầu thay đổi.";
        if (project.EventDate < today)
            return "Sự kiện đã diễn ra nên không thể tạo yêu cầu thay đổi.";
        return null;
    }

    // ── Small helpers ─────────────────────────────────────────────────────────

    private static int DaysToEvent(DateOnly eventDate, DateOnly today) => eventDate.DayNumber - today.DayNumber;

    private static string PenaltyWindowLabel(IReadOnlyList<PenaltyRuleView> rules)
    {
        if (rules.Count == 0) return "Chưa cấu hình khung phạt T-day";
        return string.Join(" · ", rules.Select(r => r.TDayMin switch
        {
            0 => $"T-{r.TDayMax} đến ngày diễn ra: {r.RatePercent:0.##}%",
            _ when r.TDayMin == r.TDayMax => $"T-{r.TDayMax}: {r.RatePercent:0.##}%",
            _ => $"T-{r.TDayMax} đến T-{r.TDayMin}: {r.RatePercent:0.##}%"
        }));
    }

    private static string? Trim(string? value, int max)
    {
        var clean = value?.Trim();
        if (string.IsNullOrEmpty(clean)) return null;
        return clean.Length > max ? clean[..max] : clean;
    }

    // SYSUTCDATETIME values are read as Unspecified by SQL Server; mark them Utc so JSON carries the offset.
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value) => value is { } v ? AsUtc(v) : null;

    private void AddHistory(ChangeRequest cr, int actorId, string action, string? note) =>
        db.Crhistories.Add(new Crhistory { Crid = cr.Crid, ActorId = actorId, Action = action, Note = note, CreatedAt = DateTime.UtcNow });

    private void Notify(int recipientId, string type, string message, string link) =>
        db.Notifications.Add(new Notification
        {
            RecipientId = recipientId,
            Type = type,
            Message = message.Length > 300 ? message[..300] : message,
            LinkUrl = link,
            CreatedAt = DateTime.UtcNow
        });

    private void NotifyCounterparty(WeddingProject project, int actorId, ChangeRequest cr, string what)
    {
        if (actorId == project.CustomerId)
            Notify(project.ManagerId, "CR update", $"Yêu cầu thay đổi {cr.Crcode} {what}.", ManagerLink(project));
        else
            Notify(project.CustomerId, "CR update", $"Yêu cầu thay đổi {cr.Crcode} {what}.", CustomerLink());
    }

    private static string ManagerLink(WeddingProject project) => $"/manager/projects/{project.ProjectCode}/change-requests";
    private static string CustomerLink() => "/customer/change-requests";
}
