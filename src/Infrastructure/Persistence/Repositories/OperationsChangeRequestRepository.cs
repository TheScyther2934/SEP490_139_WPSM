using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Application.Persistence;
using Infrastructure.Persistence.Entities;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>UC-07..UC-11, UC-22..UC-24 (SDS II.2). The /projects/{id} filter has already checked GBR-12.</summary>
public sealed class OperationsChangeRequestRepository(WpsmDbContext db, IFileService files, IPdfGenerator pdf, ILogger<OperationsChangeRequestRepository> logger) : IOperationsChangeRequestRepository
{
    private static readonly string[] Types = ["Menu change", "Decor change", "Table count", "Script edit", "Other"];
    private static readonly string[] OpenStatuses = ["Draft", "Negotiating"];

    public async Task<IReadOnlyList<OpCrSummaryView>> ListAsync(string projectCode, string? status, CancellationToken cancellationToken = default)
    {
        var query = db.ChangeRequests.AsNoTracking().Where(x => x.Project.ProjectCode == projectCode);
        if (!string.IsNullOrWhiteSpace(status) && status != "All") query = query.Where(x => x.Status == status);
        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new OpCrSummaryView(x.Crcode, x.Type, x.Description, x.CreatedByNavigation.Role, x.Status,
                x.Status == "Approved" ? x.CostImpact + x.PenaltyFee : x.Critems.Sum(i => i.NewQuantity * i.NewUnitPrice - i.OldQuantity * i.OldUnitPrice),
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CrSpecItemView>> GetSpecItemsAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
        if (project is null) return [];
        var current = await CurrentSpecItemsAsync(project.ProjectId, cancellationToken);
        var frames = await FramesAsync(current.Select(x => x.CatalogItemId), TodayVn(), cancellationToken);
        return current.Select(x => new CrSpecItemView(x.SpecItemId, x.CatalogItem.Name, WpsmCategoryNames.ToDisplay(x.CatalogItem.Category), x.CatalogItem.Unit,
                x.Description, x.Quantity, x.UnitPrice, frames.GetValueOrDefault(x.CatalogItemId)?.MinPrice, frames.GetValueOrDefault(x.CatalogItemId)?.MaxPrice))
            .ToList();
    }

    public async Task<OpCrCostView> PreviewAsync(string projectCode, IReadOnlyList<CrItemCommand> items, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");
        var lines = await ValidateItemsAsync(project, items, cancellationToken);
        var today = TodayVn();
        return Cost(lines.Select(x => (x.Spec.Quantity, x.Spec.UnitPrice, x.Command.NewQuantity, x.Command.NewUnitPrice)), project.EventDate, today,
            await PenaltyRulesAsync(db, today, cancellationToken), false, project.CurrentTotal);
    }

    public async Task<OpCrDetailView> CreateAsync(string projectCode, int userId, string role, CreateCrCommand command, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");
        EnsureCanChange(project);
        if (!Types.Contains(command.Type)) throw new InvalidOperationException("Trường Loại thay đổi là bắt buộc.");
        var reason = command.Reason?.Trim() ?? "";
        if (reason.Length == 0) throw new InvalidOperationException("Trường Lý do là bắt buộc.");
        if (reason.Length > 500) throw new InvalidOperationException("Vượt quá độ dài tối đa 500 ký tự.");
        var lines = await ValidateItemsAsync(project, command.Items, cancellationToken);

        var now = DateTime.UtcNow;
        var cr = new ChangeRequest
        {
            Crcode = await NextCodeAsync(db.ChangeRequests.Select(x => x.Crcode), "CR", cancellationToken),
            ProjectId = project.ProjectId, CreatedBy = userId, Type = command.Type, Description = reason,
            Status = command.Submit ? "Negotiating" : "Draft", CreatedAt = now,
            CostImpact = lines.Sum(x => x.Command.NewQuantity * x.Command.NewUnitPrice - x.Spec.Quantity * x.Spec.UnitPrice)
        };
        foreach (var (spec, item) in lines)
            cr.Critems.Add(new Critem { TargetSpecItemId = spec.SpecItemId, OldQuantity = spec.Quantity, OldUnitPrice = spec.UnitPrice, NewQuantity = item.NewQuantity, NewUnitPrice = item.NewUnitPrice });
        cr.Crhistories.Add(new Crhistory { ActorId = userId, Action = "Created", Note = reason, CreatedAt = now });
        if (command.Submit)
        {
            cr.Crhistories.Add(new Crhistory { ActorId = userId, Action = "Submitted", CreatedAt = now });
            NotifyCounterparty(project, cr.Crcode, role, $"{cr.Crcode} mới được gửi để thương lượng.");
        }
        db.ChangeRequests.Add(cr);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(projectCode, cr.Crcode, cancellationToken))!;
    }

    public async Task<OpCrDetailView?> GetAsync(string projectCode, string crCode, CancellationToken cancellationToken = default)
    {
        var cr = await LoadAsync(projectCode, crCode, cancellationToken, tracking: false);
        return cr is null ? null : await MapAsync(cr, cancellationToken);
    }

    public async Task<OpCrDetailView?> SubmitAsync(string projectCode, string crCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var cr = await LoadAsync(projectCode, crCode, cancellationToken);
        if (cr is null) return null;
        if (cr.Status != "Draft") throw new InvalidOperationException("Chỉ gửi được CR đang ở trạng thái Nháp.");
        if (cr.CreatedBy != userId) throw new InvalidOperationException("Chỉ người tạo CR mới gửi được bản nháp.");
        EnsureCanChange(cr.Project);
        cr.Status = "Negotiating";
        cr.Crhistories.Add(new Crhistory { ActorId = userId, Action = "Submitted", CreatedAt = DateTime.UtcNow });
        NotifyCounterparty(cr.Project, cr.Crcode, role, $"{cr.Crcode} mới được gửi để thương lượng.");
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectCode, crCode, cancellationToken);
    }

    public async Task<OpCrDetailView?> CounterProposeAsync(string projectCode, string crCode, int userId, string role, IReadOnlyList<CrItemCommand> items, string? note, CancellationToken cancellationToken = default)
    {
        var cr = await LoadAsync(projectCode, crCode, cancellationToken);
        if (cr is null) return null;
        var editingDraft = cr.Status == "Draft" && cr.CreatedBy == userId;
        if (!editingDraft && !(cr.Status == "Negotiating" && role == "Manager"))
            throw new InvalidOperationException("Chỉ Manager được đề xuất lại khi CR đang thương lượng.");
        if (note?.Trim().Length > 500) throw new InvalidOperationException("Vượt quá độ dài tối đa 500 ký tự.");
        EnsureCanChange(cr.Project);
        var lines = await ValidateItemsAsync(cr.Project, items, cancellationToken);

        db.Critems.RemoveRange(cr.Critems);
        cr.Critems.Clear();
        foreach (var (spec, item) in lines)
            cr.Critems.Add(new Critem { TargetSpecItemId = spec.SpecItemId, OldQuantity = spec.Quantity, OldUnitPrice = spec.UnitPrice, NewQuantity = item.NewQuantity, NewUnitPrice = item.NewUnitPrice });
        cr.CostImpact = lines.Sum(x => x.Command.NewQuantity * x.Command.NewUnitPrice - x.Spec.Quantity * x.Spec.UnitPrice);
        // A counter-proposal changes the terms, so earlier approvals no longer apply (SDS II.2 CounterProposeCommand).
        cr.CustomerApprovedAt = null;
        cr.ManagerApprovedAt = null;
        cr.Crhistories.Add(new Crhistory { ActorId = userId, Action = editingDraft ? "DraftEdited" : "CounterProposed", Note = note?.Trim(), CreatedAt = DateTime.UtcNow });
        if (!editingDraft) NotifyCounterparty(cr.Project, cr.Crcode, role, $"Manager đã đề xuất lại {cr.Crcode}. Vui lòng xem chi phí mới.");
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectCode, crCode, cancellationToken);
    }

    public async Task<OpCrDetailView?> ApproveAsync(string projectCode, string crCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var cr = await LoadAsync(projectCode, crCode, cancellationToken);
        if (cr is null) return null;
        if (cr.Status != "Negotiating") throw new InvalidOperationException("CR không ở trạng thái đang thương lượng.");
        EnsureCanChange(cr.Project);
        if (await db.ExceptionRequests.AnyAsync(x => x.Crid == cr.Crid && x.Status == "Pending", cancellationToken))
            throw new InvalidOperationException("CR đang chờ Admin quyết định yêu cầu ngoại lệ, chưa thể duyệt.");

        var now = DateTime.UtcNow;
        if (role == "Customer")
        {
            if (cr.CustomerApprovedAt is not null) throw new InvalidOperationException("Bạn đã duyệt CR này.");
            cr.CustomerApprovedAt = now;
        }
        else
        {
            if (cr.ManagerApprovedAt is not null) throw new InvalidOperationException("Bạn đã duyệt CR này.");
            cr.ManagerApprovedAt = now;
        }
        cr.Crhistories.Add(new Crhistory { ActorId = userId, Action = role == "Customer" ? "CustomerApproved" : "ManagerApproved", CreatedAt = now });

        ContractAddendum? addendum = null;
        if (cr.CustomerApprovedAt is not null && cr.ManagerApprovedAt is not null) addendum = await FinalizeAsync(cr, userId, now, cancellationToken);
        else NotifyCounterparty(cr.Project, cr.Crcode, role, $"{cr.Crcode} đã được {(role == "Customer" ? "khách hàng" : "Manager")} duyệt, đang chờ bạn.");
        await db.SaveChangesAsync(cancellationToken);
        if (addendum is not null) await RenderAddendumAsync(addendum, cr, cancellationToken);
        return await GetAsync(projectCode, crCode, cancellationToken);
    }

    public async Task<OpCrDetailView?> RejectAsync(string projectCode, string crCode, int userId, string role, string? reason, CancellationToken cancellationToken = default)
    {
        var cr = await LoadAsync(projectCode, crCode, cancellationToken);
        if (cr is null) return null;
        if (!OpenStatuses.Contains(cr.Status)) throw new InvalidOperationException("CR đã đóng.");
        reason = reason?.Trim();
        if (role == "Manager" && string.IsNullOrEmpty(reason)) throw new InvalidOperationException("Trường Lý do từ chối là bắt buộc.");
        if (reason?.Length > 300) throw new InvalidOperationException("Vượt quá độ dài tối đa 300 ký tự.");
        cr.Status = "Rejected";
        cr.Crhistories.Add(new Crhistory { ActorId = userId, Action = "Rejected", Note = reason, CreatedAt = DateTime.UtcNow });
        NotifyCounterparty(cr.Project, cr.Crcode, role, $"{cr.Crcode} đã bị từ chối và đóng lại.");
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectCode, crCode, cancellationToken);
    }

    public async Task<OpCrDetailView?> CommentAsync(string projectCode, string crCode, int userId, string role, string note, CancellationToken cancellationToken = default)
    {
        var cr = await LoadAsync(projectCode, crCode, cancellationToken);
        if (cr is null) return null;
        if (cr.Status != "Negotiating") throw new InvalidOperationException("Chỉ góp ý được khi CR đang thương lượng.");
        note = note?.Trim() ?? "";
        if (note.Length == 0) throw new InvalidOperationException("Trường Nội dung góp ý là bắt buộc.");
        if (note.Length > 500) throw new InvalidOperationException("Vượt quá độ dài tối đa 500 ký tự.");
        cr.Crhistories.Add(new Crhistory { ActorId = userId, Action = "Commented", Note = note, CreatedAt = DateTime.UtcNow });
        NotifyCounterparty(cr.Project, cr.Crcode, role, $"Có góp ý mới trên {cr.Crcode}.");
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectCode, crCode, cancellationToken);
    }

    public async Task<OpCrDetailView?> RequestExceptionAsync(string projectCode, string crCode, int managerId, string note, CancellationToken cancellationToken = default)
    {
        var cr = await LoadAsync(projectCode, crCode, cancellationToken);
        if (cr is null) return null;
        if (cr.Status != "Negotiating") throw new InvalidOperationException("Chỉ yêu cầu ngoại lệ khi CR đang thương lượng.");
        note = note?.Trim() ?? "";
        if (note.Length == 0) throw new InvalidOperationException("Trường Ghi chú là bắt buộc.");
        if (note.Length > 300) throw new InvalidOperationException("Vượt quá độ dài tối đa 300 ký tự.");
        if (await db.ExceptionRequests.AnyAsync(x => x.Crid == cr.Crid && x.Status == "Pending", cancellationToken))
            throw new InvalidOperationException("CR này đã có yêu cầu ngoại lệ đang chờ Admin.");
        var now = DateTime.UtcNow;
        var code = await NextCodeAsync(db.ExceptionRequests.Select(x => x.RequestCode), "EXC", cancellationToken);
        db.ExceptionRequests.Add(new ExceptionRequest
        {
            RequestCode = code, SourceType = "CrPenaltyWaiver", ProjectId = cr.ProjectId, Crid = cr.Crid,
            RequestedBy = managerId, RequestNote = note, Status = "Pending", CreatedAt = now
        });
        cr.Crhistories.Add(new Crhistory { ActorId = managerId, Action = "ExceptionRequested", Note = $"{code}: {note}", CreatedAt = now });
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(projectCode, crCode, cancellationToken);
    }

    public async Task<OpCrAddendumView?> GetAddendumAsync(string projectCode, string crCode, CancellationToken cancellationToken = default)
    {
        var cr = await LoadAsync(projectCode, crCode, cancellationToken);
        if (cr?.ContractAddendum is not { } addendum) return null;
        // PDF rendering runs after the approval commit; a failed render is retried on the next read (SDS II.2 GenerateAddendumPdfJob).
        if (!Guid.TryParse(addendum.FilePath, out _)) await RenderAddendumAsync(addendum, cr, cancellationToken);
        return await MapAddendumAsync(cr, cancellationToken);
    }

    public async Task<OpCrAddendumView?> AttachSignedAsync(string projectCode, string crCode, int managerId, byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        var cr = await LoadAsync(projectCode, crCode, cancellationToken);
        if (cr?.ContractAddendum is not { } addendum) return null;
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        await files.StoreAsync(cr.ProjectId, "SignedAddendum", $"{addendum.AddendumCode}_signed{extension}", content, managerId, cancellationToken);
        addendum.Status = "Signed";
        addendum.SignedAt = DateTime.UtcNow;
        Notify(db, cr.Project.CustomerId, "CR update", $"Bản ký phụ lục {addendum.AddendumCode} đã được lưu.", Links.CustomerContract + "/addenda");
        await db.SaveChangesAsync(cancellationToken);
        return await MapAddendumAsync(cr, cancellationToken);
    }

    private async Task<ContractAddendum> FinalizeAsync(ChangeRequest cr, int userId, DateTime now, CancellationToken cancellationToken)
    {
        var project = cr.Project;
        var current = (await CurrentSpecItemsAsync(project.ProjectId, cancellationToken)).Select(x => x.SpecItemId).ToHashSet();
        if (cr.Critems.Any(x => !current.Contains(x.TargetSpecItemId)))
            throw new InvalidOperationException("Hạng mục đã được CR khác thay đổi. Manager cần đề xuất lại giá trị mới.");

        var today = TodayVn();
        var waived = await db.ExceptionRequests.AnyAsync(x => x.Crid == cr.Crid && x.Status == "Approved", cancellationToken);
        var cost = Cost(cr.Critems.Select(x => (x.OldQuantity, x.OldUnitPrice, x.NewQuantity, x.NewUnitPrice)), project.EventDate, today,
            await PenaltyRulesAsync(db, today, cancellationToken), waived, project.CurrentTotal);
        cr.Status = "Approved";
        cr.CostImpact = cost.PriceDelta;
        cr.PenaltyRate = cost.PenaltyRate;
        cr.PenaltyFee = cost.PenaltyFee;
        project.CurrentTotal += cost.TotalImpact;
        cr.Crhistories.Add(new Crhistory { ActorId = userId, Action = "Approved", Note = $"Tổng ảnh hưởng {Money(cost.TotalImpact)} (phạt {cost.PenaltyRate:0.##}%)", CreatedAt = now });

        // Approved items become a revised Spec Item version; the frozen baseline (version 1) stays untouched (GBR-01).
        var versions = await db.SpecItems.Where(x => x.ProjectId == project.ProjectId).GroupBy(x => x.CatalogItemId)
            .Select(g => new { g.Key, Max = g.Max(x => x.Version) }).ToDictionaryAsync(x => x.Key, x => x.Max, cancellationToken);
        foreach (var item in cr.Critems)
        {
            db.SpecItems.Add(new SpecItem
            {
                ProjectId = project.ProjectId, CatalogItemId = item.TargetSpecItem.CatalogItemId, Description = item.TargetSpecItem.Description,
                Quantity = item.NewQuantity, UnitPrice = item.NewUnitPrice, IsBaseline = false,
                Version = versions.GetValueOrDefault(item.TargetSpecItem.CatalogItemId) + 1
            });
        }

        var addendum = new ContractAddendum
        {
            AddendumCode = await NextCodeAsync(db.ContractAddenda.Select(x => x.AddendumCode), "ADD", cancellationToken),
            Crid = cr.Crid, ProjectId = project.ProjectId, TotalVariance = cost.TotalImpact, Status = "Generated"
        };
        db.ContractAddenda.Add(addendum);
        cr.ContractAddendum = addendum;

        // GBR-05: a Draft BEO is recompiled from the new Spec Items; a Frozen BEO becomes Stale.
        var beo = await db.Beos.Where(x => x.ProjectId == project.ProjectId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken);
        if (beo?.Status == "Frozen")
        {
            beo.Status = "Stale";
            cr.Crhistories.Add(new Crhistory { ActorId = userId, Action = "BeoMarkedStale", Note = $"BEO v{beo.Version}", CreatedAt = now });
            Notify(db, project.ManagerId, "BEO", $"BEO của {project.ProjectCode} đã lỗi thời do {addendum.AddendumCode}. Vui lòng duyệt lại.", Links.ManagerBeo(project.ProjectCode));
        }
        else if (beo?.Status == "Draft")
            cr.Crhistories.Add(new Crhistory { ActorId = userId, Action = "BeoDraftUpdated", Note = $"BEO v{beo.Version}", CreatedAt = now });

        Notify(db, project.CustomerId, "CR update", $"{cr.Crcode} đã được duyệt. Phụ lục {addendum.AddendumCode} đã được tạo.", Links.CustomerCr(cr.Crcode));
        Notify(db, project.ManagerId, "CR update", $"{cr.Crcode} đã được duyệt. Phụ lục {addendum.AddendumCode} đã được tạo.", Links.ManagerCr(project.ProjectCode, cr.Crcode));
        return addendum;
    }

    private async Task RenderAddendumAsync(ContractAddendum addendum, ChangeRequest cr, CancellationToken cancellationToken)
    {
        try
        {
            var project = cr.Project;
            var bytes = pdf.Render(new PdfDocumentModel($"Phụ lục hợp đồng {addendum.AddendumCode}", $"Theo Change Request {cr.Crcode}",
                [
                    ("Dự án", project.ProjectCode), ("Khách hàng", project.Customer.FullName), ("Ngày tiệc", project.EventDate.ToString("dd/MM/yyyy")),
                    ("Loại thay đổi", cr.Type), ("Lý do", cr.Description), ("Chênh lệch giá", Money(cr.CostImpact)),
                    ("Phí phạt", $"{Money(cr.PenaltyFee)} ({cr.PenaltyRate ?? 0:0.##}%)"), ("Tổng phát sinh", Money(cr.CostImpact + cr.PenaltyFee)),
                    ("Tổng hợp đồng mới", Money(project.CurrentTotal))
                ],
                [new PdfTable("Hạng mục thay đổi", ["Hạng mục", "Cũ", "Mới", "Chênh lệch"],
                    cr.Critems.Select(i => (IReadOnlyList<string>)[i.TargetSpecItem.CatalogItem.Name,
                        $"{i.OldQuantity} × {Money(i.OldUnitPrice)}", $"{i.NewQuantity} × {Money(i.NewUnitPrice)}",
                        Money(i.NewQuantity * i.NewUnitPrice - i.OldQuantity * i.OldUnitPrice)]).ToList())],
                ["Phụ lục được ký trực tiếp giữa hai bên; bản scan có chữ ký được Manager lưu vào hệ thống (LI-05)."]));
            var stored = await files.StoreAsync(cr.ProjectId, "AddendumPdf", $"{addendum.AddendumCode}.pdf", bytes, null, cancellationToken);
            addendum.FilePath = stored.Id.ToString();
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Addendum PDF {Code} failed; it is retried on the next read.", addendum.AddendumCode);
        }
    }

    private async Task<OpCrAddendumView> MapAddendumAsync(ChangeRequest cr, CancellationToken cancellationToken)
    {
        var addendum = cr.ContractAddendum!;
        var project = cr.Project;
        var paid = await db.Payments.Where(x => x.ProjectId == project.ProjectId && x.Status == "Paid").SumAsync(x => x.Amount ?? 0, cancellationToken);
        var signed = await db.StoredFiles.AsNoTracking()
            .Where(x => x.ProjectId == project.ProjectId && x.Purpose == "SignedAddendum" && x.OriginalName.StartsWith(addendum.AddendumCode + "_"))
            .OrderByDescending(x => x.UploadedAt).Select(x => (Guid?)x.FileId).FirstOrDefaultAsync(cancellationToken);
        var detail = await MapAsync(cr, cancellationToken);
        return new OpCrAddendumView(addendum.AddendumCode, cr.Crcode, addendum.Status, addendum.TotalVariance, addendum.SignedAt,
            Guid.TryParse(addendum.FilePath, out var pdfId) ? pdfId : null, signed, project.BaseContractValue, project.CurrentTotal, paid,
            project.CurrentTotal - paid, detail.BeoEffect, detail.Items, detail.Cost);
    }

    private async Task<OpCrDetailView> MapAsync(ChangeRequest cr, CancellationToken cancellationToken)
    {
        var history = await db.Crhistories.AsNoTracking().Where(x => x.Crid == cr.Crid).OrderBy(x => x.CreatedAt).ThenBy(x => x.HistoryId)
            .Select(x => new OpCrHistoryView(x.Actor.FullName, x.Actor.Role, x.Action, x.Note, x.CreatedAt)).ToListAsync(cancellationToken);
        var exceptions = await db.ExceptionRequests.AsNoTracking().Where(x => x.Crid == cr.Crid).OrderByDescending(x => x.CreatedAt)
            .Select(x => new CrExceptionView(x.RequestCode, x.Status, x.RequestNote, x.DecisionNote)).ToListAsync(cancellationToken);
        var creator = await db.Users.AsNoTracking().Where(x => x.UserId == cr.CreatedBy).Select(x => new { x.FullName, x.Role }).FirstAsync(cancellationToken);
        var items = cr.Critems.Select(i => new OpCrItemView(i.TargetSpecItemId, i.TargetSpecItem.CatalogItem.Name, i.TargetSpecItem.CatalogItem.Unit,
            i.OldQuantity, i.OldUnitPrice, i.NewQuantity, i.NewUnitPrice, i.NewQuantity * i.NewUnitPrice - i.OldQuantity * i.OldUnitPrice)).ToList();

        OpCrCostView cost;
        if (cr.Status == "Approved")
        {
            var approvedOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Utc(Max(cr.CustomerApprovedAt, cr.ManagerApprovedAt)), VnTz));
            var total = cr.CostImpact + cr.PenaltyFee;
            cost = new OpCrCostView(cr.CostImpact, cr.Project.EventDate.DayNumber - approvedOn.DayNumber, cr.PenaltyRate ?? 0, cr.PenaltyFee,
                exceptions.Any(x => x.Status == "Approved"), total, cr.Project.CurrentTotal - total, cr.Project.CurrentTotal);
        }
        else
        {
            var today = TodayVn();
            cost = Cost(cr.Critems.Select(x => (x.OldQuantity, x.OldUnitPrice, x.NewQuantity, x.NewUnitPrice)), cr.Project.EventDate, today,
                await PenaltyRulesAsync(db, today, cancellationToken), exceptions.Any(x => x.Status == "Approved"), cr.Project.CurrentTotal);
        }
        var beoEffect = history.LastOrDefault(x => x.Action is "BeoMarkedStale" or "BeoDraftUpdated") is { } effect
            ? effect.Action == "BeoMarkedStale" ? "BEO bị đánh dấu lỗi thời (Stale), cần duyệt lại (MSG22)." : "Bản nháp BEO đã được cập nhật."
            : cr.Status == "Approved" ? "Chưa có BEO, nội dung mới sẽ vào bản nháp khi BEO được tạo." : null;
        return new OpCrDetailView(cr.Crcode, cr.Project.ProjectCode, cr.Type, cr.Description, creator.Role, creator.FullName, cr.Status, Utc(cr.CreatedAt),
            cr.CustomerApprovedAt is { } c ? Utc(c) : null, cr.ManagerApprovedAt is { } m ? Utc(m) : null, items, cost,
            history.Select(x => x with { CreatedAt = Utc(x.CreatedAt) }).ToList(), exceptions, cr.ContractAddendum?.AddendumCode, beoEffect);
    }

    private static DateTime Max(DateTime? a, DateTime? b) => (a ?? DateTime.MinValue) > (b ?? DateTime.MinValue) ? a!.Value : b ?? DateTime.UtcNow;

    private async Task<ChangeRequest?> LoadAsync(string projectCode, string crCode, CancellationToken cancellationToken, bool tracking = true)
    {
        var query = db.ChangeRequests.Include(x => x.Project).ThenInclude(x => x.Customer)
            .Include(x => x.Critems).ThenInclude(x => x.TargetSpecItem).ThenInclude(x => x.CatalogItem)
            .Include(x => x.ContractAddendum).AsSplitQuery();
        if (!tracking) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(x => x.Crcode == crCode && x.Project.ProjectCode == projectCode, cancellationToken);
    }

    // Latest version of each Spec Item: the baseline, or the newest revision from approved CRs.
    private async Task<List<SpecItem>> CurrentSpecItemsAsync(int projectId, CancellationToken cancellationToken)
    {
        var items = await db.SpecItems.AsNoTracking().Include(x => x.CatalogItem).Where(x => x.ProjectId == projectId).ToListAsync(cancellationToken);
        return items.GroupBy(x => x.CatalogItemId).Select(g => g.OrderByDescending(x => x.Version).First())
            .OrderBy(x => x.CatalogItem.Category).ThenBy(x => x.CatalogItem.Name).ToList();
    }

    private async Task<Dictionary<int, PriceFrame>> FramesAsync(IEnumerable<int> itemIds, DateOnly onDate, CancellationToken cancellationToken)
    {
        var ids = itemIds.Distinct().ToArray();
        var frames = await db.PriceFrames.AsNoTracking().Where(x => ids.Contains(x.ItemId) && x.EffectiveFrom <= onDate).ToListAsync(cancellationToken);
        return frames.GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EffectiveFrom).First());
    }

    private async Task<List<(SpecItem Spec, CrItemCommand Command)>> ValidateItemsAsync(WeddingProject project, IReadOnlyList<CrItemCommand>? items, CancellationToken cancellationToken)
    {
        if (items is null || items.Count == 0) throw new InvalidOperationException("Trường Hạng mục là bắt buộc.");
        if (items.GroupBy(x => x.SpecItemId).Any(g => g.Count() > 1)) throw new InvalidOperationException("Mỗi hạng mục chỉ được chọn một lần trong CR.");
        var current = (await CurrentSpecItemsAsync(project.ProjectId, cancellationToken)).ToDictionary(x => x.SpecItemId);
        var frames = await FramesAsync(current.Values.Select(x => x.CatalogItemId), TodayVn(), cancellationToken);
        var result = new List<(SpecItem, CrItemCommand)>();
        foreach (var item in items)
        {
            if (!current.TryGetValue(item.SpecItemId, out var spec)) throw new InvalidOperationException("Hạng mục không thuộc đặc tả hiện hành của dự án.");
            if (item.NewQuantity <= 0) throw new InvalidOperationException($"Số lượng mới của '{spec.CatalogItem.Name}' phải lớn hơn 0.");
            if (frames.TryGetValue(spec.CatalogItemId, out var frame) && (item.NewUnitPrice < frame.MinPrice || item.NewUnitPrice > frame.MaxPrice))
                throw new InvalidOperationException($"Đơn giá của '{spec.CatalogItem.Name}' phải nằm trong khoảng {frame.MinPrice:N0} - {frame.MaxPrice:N0} VND (MSG06).");
            if (item.NewUnitPrice < 0) throw new InvalidOperationException("Đơn giá không được âm.");
            if (item.NewQuantity == spec.Quantity && item.NewUnitPrice == spec.UnitPrice)
                throw new InvalidOperationException($"'{spec.CatalogItem.Name}' chưa có thay đổi so với giá trị hiện tại.");
            result.Add((spec, item));
        }
        return result;
    }

    private static void EnsureCanChange(WeddingProject project)
    {
        if (project.Status == "Active Baseline") throw new InvalidOperationException("Baseline chưa được chốt nên chưa thể tạo Change Request.");
        if (project.Status != "Baseline Frozen" || project.EventDate < TodayVn())
            throw new InvalidOperationException("Dự án đã diễn ra hoặc đã quyết toán nên không thể thay đổi.");
    }

    private void NotifyCounterparty(WeddingProject project, string crCode, string actorRole, string message)
    {
        if (actorRole == "Customer") Notify(db, project.ManagerId, "CR update", message, Links.ManagerCr(project.ProjectCode, crCode));
        else Notify(db, project.CustomerId, "CR update", message, Links.CustomerCr(crCode));
    }
}
