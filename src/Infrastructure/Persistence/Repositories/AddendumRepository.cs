using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class AddendumRepository(WpsmDbContext db) : IAddendumRepository
{
    public async Task<IReadOnlyList<AddendumView>?> GetAddendaAsync(string? projectCode, int userId, string? role, CancellationToken cancellationToken = default)
    {
        var projectId = await ResolveAccessibleProjectIdAsync(projectCode, userId, role, cancellationToken);
        if (projectId is null) return null;

        var addenda = await db.ContractAddenda.AsNoTracking().AsSplitQuery()
            .Include(x => x.Cr).ThenInclude(x => x.Critems).ThenInclude(x => x.TargetSpecItem).ThenInclude(x => x.CatalogItem)
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.AddendumId)
            .ToListAsync(cancellationToken);

        return addenda.Select(MapAddendum).ToList();
    }

    public async Task<IReadOnlyList<CrCostImpactView>?> GetProjectChangeRequestsAsync(string? projectCode, int userId, string? role, CancellationToken cancellationToken = default)
    {
        var projectId = await ResolveAccessibleProjectIdAsync(projectCode, userId, role, cancellationToken);
        if (projectId is null) return null;

        var crs = await LoadChangeRequestsQuery()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<CrCostImpactView>(crs.Count);
        foreach (var cr in crs)
            result.Add(await BuildCostImpactViewAsync(cr, cancellationToken));
        return result;
    }

    public async Task<CrCreationContextView?> GetCrCreationContextAsync(string? projectCode, int customerId, CancellationToken cancellationToken = default)
    {
        var projectId = await ResolveAccessibleProjectIdAsync(projectCode, customerId, "Customer", cancellationToken);
        if (projectId is null) return null;

        var project = await db.WeddingProjects.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Hall)
            .Include(x => x.SpecItems).ThenInclude(s => s.CatalogItem)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId.Value, cancellationToken);
        if (project is null) return null;

        var daysUntil = project.EventDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
        var penaltyRate = await ResolvePenaltyRateAsync(daysUntil, cancellationToken);

        var activeSpecs = project.SpecItems
            .GroupBy(s => s.CatalogItemId)
            .Select(g => g.OrderByDescending(s => s.Version).First())
            .OrderBy(s => s.CatalogItem.Category)
            .ThenBy(s => s.CatalogItem.Name)
            .Select(s => new CrSpecOptionView(
                s.SpecItemId,
                s.CatalogItemId,
                s.CatalogItem.Name,
                s.CatalogItem.Category,
                s.CatalogItem.Unit,
                s.Description,
                s.Quantity,
                s.UnitPrice,
                s.Quantity * s.UnitPrice))
            .ToList();

        return new CrCreationContextView(
            project.ProjectCode,
            project.Customer.FullName,
            project.Hall.Name,
            project.EventDate,
            daysUntil,
            penaltyRate,
            project.BaseContractValue,
            project.CurrentTotal,
            activeSpecs);
    }

    public async Task<CrCostImpactView> CreateCustomerChangeRequestAsync(
        int customerId,
        string? projectCode,
        string type,
        string description,
        IReadOnlyList<CreateCrItemInput> items,
        CancellationToken cancellationToken = default)
    {
        var trimmedDesc = (description ?? "").Trim();
        if (trimmedDesc.Length == 0)
            throw new InvalidOperationException("Vui lòng nhập lý do và mô tả chi tiết yêu cầu thay đổi.");
        if (trimmedDesc.Length > 500)
            throw new InvalidOperationException("Mô tả yêu cầu thay đổi tối đa 500 ký tự.");

        if (items is null || items.Count == 0)
            throw new InvalidOperationException("Vui lòng chọn ít nhất 1 hạng mục hợp đồng cần thay đổi.");

        var projectId = await ResolveAccessibleProjectIdAsync(projectCode, customerId, "Customer", cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy dự án của khách hàng.");

        var project = await db.WeddingProjects
            .Include(x => x.SpecItems).ThenInclude(s => s.CatalogItem)
            .FirstAsync(x => x.ProjectId == projectId, cancellationToken);

        var specMap = project.SpecItems.ToDictionary(s => s.SpecItemId);
        var normalizedLines = new List<(SpecItem Spec, int NewQuantity, decimal NewUnitPrice, decimal Delta)>();

        foreach (var line in items)
        {
            if (!specMap.TryGetValue(line.TargetSpecItemId, out var spec))
                throw new InvalidOperationException($"Không tìm thấy hạng mục đặc tả #{line.TargetSpecItemId} trong hợp đồng.");
            if (line.NewQuantity < 0)
                throw new InvalidOperationException("Số lượng mới không được âm.");
            if (line.NewUnitPrice < 0m)
                throw new InvalidOperationException("Đơn giá mới không được âm.");
            if (normalizedLines.Any(x => x.Spec.SpecItemId == spec.SpecItemId))
                throw new InvalidOperationException($"Hạng mục '{spec.CatalogItem.Name}' bị trùng lặp trong yêu cầu.");

            var delta = line.NewQuantity * line.NewUnitPrice - spec.Quantity * spec.UnitPrice;
            normalizedLines.Add((spec, line.NewQuantity, decimal.Round(line.NewUnitPrice, 2), decimal.Round(delta, 2)));
        }

        if (normalizedLines.All(x => x.NewQuantity == x.Spec.Quantity && x.NewUnitPrice == x.Spec.UnitPrice))
            throw new InvalidOperationException("Số lượng hoặc đơn giá mới phải khác với đặc tả hiện tại.");

        var daysUntil = project.EventDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
        var penaltyRate = await ResolvePenaltyRateAsync(daysUntil, cancellationToken);
        var costImpact = normalizedLines.Sum(x => x.Delta);
        var penaltyFee = decimal.Round(Math.Abs(costImpact) * penaltyRate / 100m, 0);

        var seq = await db.ChangeRequests.CountAsync(x => x.ProjectId == project.ProjectId, cancellationToken) + 1;
        var suffix = project.ProjectCode.Split('-').LastOrDefault() ?? project.ProjectId.ToString();
        var code = $"CR-{suffix}-{seq:D2}";
        while (await db.ChangeRequests.AnyAsync(x => x.Crcode == code, cancellationToken))
        {
            seq++;
            code = $"CR-{suffix}-{seq:D2}";
        }

        var normalizedType = string.IsNullOrWhiteSpace(type) ? "Other" : type.Trim();
        if (normalizedType.Length > 30) normalizedType = normalizedType[..30];

        var now = DateTime.UtcNow;
        var cr = new ChangeRequest
        {
            Crcode = code,
            ProjectId = project.ProjectId,
            CreatedBy = customerId,
            Type = normalizedType,
            Description = trimmedDesc,
            CostImpact = costImpact,
            PenaltyRate = penaltyRate,
            PenaltyFee = penaltyFee,
            Status = "Negotiating",
            CustomerApprovedAt = now,
            ManagerApprovedAt = null,
            CreatedAt = now
        };
        db.ChangeRequests.Add(cr);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var (spec, newQty, newPrice, _) in normalizedLines)
        {
            db.Critems.Add(new Critem
            {
                Crid = cr.Crid,
                CatalogItemId = spec.CatalogItemId,
                TargetSpecItemId = spec.SpecItemId,
                OldQuantity = spec.Quantity,
                OldUnitPrice = spec.UnitPrice,
                NewQuantity = newQty,
                NewUnitPrice = newPrice
            });
        }

        db.Crhistories.Add(new Crhistory
        {
            Crid = cr.Crid,
            ActorId = customerId,
            Action = "Khởi tạo yêu cầu thay đổi",
            Note = trimmedDesc,
            CreatedAt = now
        });

        db.Notifications.Add(new Notification
        {
            RecipientId = project.ManagerId,
            Type = "CR update",
            Message = $"Khách hàng vừa gửi yêu cầu thay đổi mới {code} cho dự án {project.ProjectCode}.",
            LinkUrl = $"/M09?id={project.ProjectCode}&cr={code}",
            IsRead = false,
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        var reloaded = await LoadChangeRequestsQuery().FirstAsync(x => x.Crid == cr.Crid, cancellationToken);
        return await BuildCostImpactViewAsync(reloaded, cancellationToken);
    }

    public async Task<CrCostImpactView?> GetCostImpactAsync(string? projectCode, string crCode, int userId, string? role, CancellationToken cancellationToken = default)
    {
        var normalizedCode = (crCode ?? "").Trim();
        var query = LoadChangeRequestsQuery().Where(x => x.Crcode == normalizedCode);
        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(x => x.Project.ProjectCode == projectCode.Trim());

        var cr = await query.FirstOrDefaultAsync(cancellationToken);
        if (cr is null || !CanAccess(cr.Project, userId, role)) return null;
        return await BuildCostImpactViewAsync(cr, cancellationToken);
    }

    public async Task<CrCostImpactView?> DecideCustomerCrAsync(string crCode, int customerId, string action, string? note, CancellationToken cancellationToken = default)
    {
        var cr = await db.ChangeRequests
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Crcode == crCode.Trim() && x.Project.CustomerId == customerId, cancellationToken);
        if (cr is null) return null;

        if (cr.Status is "Approved" or "Rejected")
            throw new InvalidOperationException("Yêu cầu thay đổi này đã chốt trạng thái, không thể thao tác thêm.");
        if (cr.Status == "Draft")
            throw new InvalidOperationException("Yêu cầu thay đổi đang chờ Quản lý dự án thẩm định và gửi báo giá chi phí.");

        var normalizedAction = (action ?? "").Trim();
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var now = DateTime.UtcNow;

        switch (normalizedAction)
        {
            case "Approve":
                cr.CustomerApprovedAt = now;
                if (cr.ManagerApprovedAt is not null)
                    cr.Status = "Approved";
                else
                    cr.Status = "Negotiating";
                break;

            case "Reject":
                if (string.IsNullOrWhiteSpace(trimmedNote))
                    throw new InvalidOperationException("Vui lòng nhập lý do từ chối yêu cầu thay đổi.");
                cr.Status = "Rejected";
                break;

            case "RequestAdjustment":
                if (string.IsNullOrWhiteSpace(trimmedNote))
                    throw new InvalidOperationException("Vui lòng nhập nội dung cần điều chỉnh.");
                cr.CustomerApprovedAt = null;
                cr.Status = "Negotiating";
                break;

            default:
                throw new InvalidOperationException("Thao tác phản hồi không hợp lệ.");
        }

        db.Crhistories.Add(new Crhistory
        {
            Crid = cr.Crid,
            ActorId = customerId,
            Action = normalizedAction switch
            {
                "Approve" => "Khách hàng phê duyệt tác động chi phí",
                "Reject" => "Khách hàng từ chối yêu cầu thay đổi",
                _ => "Khách hàng yêu cầu điều chỉnh phương án"
            },
            Note = trimmedNote,
            CreatedAt = now
        });

        db.Notifications.Add(new Notification
        {
            RecipientId = cr.Project.ManagerId,
            Type = "CR update",
            Message = $"Khách hàng đã phản hồi ({normalizedAction}) đối với yêu cầu {cr.Crcode} của dự án {cr.Project.ProjectCode}.",
            LinkUrl = $"/M09?id={cr.Project.ProjectCode}&cr={cr.Crcode}",
            IsRead = false,
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        var reloaded = await LoadChangeRequestsQuery().FirstAsync(x => x.Crid == cr.Crid, cancellationToken);
        return await BuildCostImpactViewAsync(reloaded, cancellationToken);
    }

    public async Task<CrCostImpactView?> SendCostImpactQuoteAsync(string projectCode, string crCode, int managerId, string? note, CancellationToken cancellationToken = default)
    {
        var cr = await db.ChangeRequests
            .Include(x => x.Project)
            .FirstOrDefaultAsync(x => x.Crcode == crCode.Trim() && x.Project.ProjectCode == projectCode.Trim() && x.Project.ManagerId == managerId, cancellationToken);
        if (cr is null) return null;

        if (cr.Status is "Approved" or "Rejected")
            throw new InvalidOperationException("Yêu cầu thay đổi đã chốt trạng thái, không thể gửi lại báo giá.");

        var now = DateTime.UtcNow;
        cr.ManagerApprovedAt = now;
        cr.CustomerApprovedAt = null;
        cr.Status = "Negotiating";

        db.Crhistories.Add(new Crhistory
        {
            Crid = cr.Crid,
            ActorId = managerId,
            Action = "Quản lý thẩm định & gửi báo giá tác động chi phí",
            Note = string.IsNullOrWhiteSpace(note) ? "Đã kiểm tra năng lực đáp ứng và gửi bảng tính tác động chi phí cho khách hàng xác nhận." : note.Trim(),
            CreatedAt = now
        });

        db.Notifications.Add(new Notification
        {
            RecipientId = cr.Project.CustomerId,
            Type = "CR update",
            Message = $"Quản lý dự án đã thẩm định và gửi báo giá chi phí cho yêu cầu {cr.Crcode}. Vui lòng kiểm tra và xác nhận.",
            LinkUrl = $"/C07?cr={cr.Crcode}",
            IsRead = false,
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        var reloaded = await LoadChangeRequestsQuery().FirstAsync(x => x.Crid == cr.Crid, cancellationToken);
        return await BuildCostImpactViewAsync(reloaded, cancellationToken);
    }

    public async Task<CrCostImpactView?> GenerateAddendumAsync(string projectCode, string crCode, int managerId, Guid? signedFileId, string? note, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var cr = await db.ChangeRequests
            .Include(x => x.Project)
            .Include(x => x.ContractAddendum)
            .Include(x => x.Critems).ThenInclude(i => i.TargetSpecItem)
            .FirstOrDefaultAsync(x => x.Crcode == crCode.Trim() && x.Project.ProjectCode == projectCode.Trim() && x.Project.ManagerId == managerId, cancellationToken);
        if (cr is null) return null;

        if (cr.Status == "Rejected")
            throw new InvalidOperationException("Không thể lập phụ lục cho yêu cầu thay đổi đã bị từ chối.");

        var now = DateTime.UtcNow;
        cr.ManagerApprovedAt ??= now;
        cr.CustomerApprovedAt ??= now;
        cr.Status = "Approved";

        var totalVariance = cr.CostImpact + cr.PenaltyFee;
        var addendum = cr.ContractAddendum;
        if (addendum is null)
        {
            var seq = await db.ContractAddenda.CountAsync(x => x.ProjectId == cr.ProjectId, cancellationToken) + 1;
            addendum = new ContractAddendum
            {
                AddendumCode = $"ADD-{cr.Project.ProjectCode.Split('-').LastOrDefault() ?? cr.ProjectId.ToString()}-{seq:D2}",
                Crid = cr.Crid,
                ProjectId = cr.ProjectId,
                TotalVariance = totalVariance,
                Status = signedFileId.HasValue ? "Signed" : "Generated",
                SignedAt = signedFileId.HasValue ? now : null,
                FilePath = signedFileId?.ToString()
            };
            db.ContractAddenda.Add(addendum);
        }
        else
        {
            addendum.TotalVariance = totalVariance;
            if (signedFileId.HasValue)
            {
                addendum.FilePath = signedFileId.Value.ToString();
                addendum.Status = "Signed";
                addendum.SignedAt = now;
            }
        }

        // Apply line item changes to revised specification items without mutating frozen baseline rows (GBR-01 trigger)
        var projectSpecs = await db.SpecItems.Where(x => x.ProjectId == cr.ProjectId).ToListAsync(cancellationToken);
        foreach (var line in cr.Critems)
        {
            if (line.NewQuantity > 0)
            {
                var catalogItemId = line.TargetSpecItem.CatalogItemId;
                var latestRevised = projectSpecs
                    .Where(x => x.CatalogItemId == catalogItemId && !x.IsBaseline)
                    .OrderByDescending(x => x.Version)
                    .FirstOrDefault();
                if (latestRevised is not null)
                {
                    latestRevised.Quantity = line.NewQuantity;
                    latestRevised.UnitPrice = line.NewUnitPrice;
                }
                else
                {
                    var maxVersion = projectSpecs
                        .Where(x => x.CatalogItemId == catalogItemId)
                        .Select(x => x.Version)
                        .DefaultIfEmpty(1)
                        .Max();
                    var revised = new SpecItem
                    {
                        ProjectId = cr.ProjectId,
                        CatalogItemId = catalogItemId,
                        Description = line.TargetSpecItem.Description,
                        Quantity = line.NewQuantity,
                        UnitPrice = line.NewUnitPrice,
                        IsBaseline = false,
                        Version = maxVersion + 1
                    };
                    db.SpecItems.Add(revised);
                    projectSpecs.Add(revised);
                }
            }
        }

        // Update project CurrentTotal from base contract + all addenda
        var existingVariance = await db.ContractAddenda
            .Where(x => x.ProjectId == cr.ProjectId && x.Crid != cr.Crid)
            .SumAsync(x => (decimal?)x.TotalVariance, cancellationToken) ?? 0m;
        cr.Project.CurrentTotal = Math.Max(0m, cr.Project.BaseContractValue + existingVariance + totalVariance);

        // GBR-05: Any Frozen BEO becomes Stale when an approved CR generates an addendum
        var frozenBeos = await db.Beos
            .Where(x => x.ProjectId == cr.ProjectId && x.Status == "Frozen")
            .ToListAsync(cancellationToken);
        foreach (var beo in frozenBeos)
        {
            beo.Status = "Stale";
            beo.Notes = string.IsNullOrWhiteSpace(beo.Notes)
                ? $"BEO chuyển sang Stale do phụ lục {addendum.AddendumCode} ({cr.Crcode})."
                : $"{beo.Notes} | Stale bởi {addendum.AddendumCode}";
        }

        db.Crhistories.Add(new Crhistory
        {
            Crid = cr.Crid,
            ActorId = managerId,
            Action = signedFileId.HasValue ? "Phê duyệt & tải lên phụ lục đã ký" : "Phê duyệt tác động chi phí & phát hành phụ lục",
            Note = string.IsNullOrWhiteSpace(note) ? $"Phụ lục {addendum.AddendumCode} · Chênh lệch: {totalVariance:N0} VND" : note.Trim(),
            CreatedAt = now
        });

        db.Notifications.Add(new Notification
        {
            RecipientId = cr.Project.CustomerId,
            Type = "CR update",
            Message = $"Phụ lục hợp đồng {addendum.AddendumCode} từ yêu cầu {cr.Crcode} đã được phát hành.",
            LinkUrl = "/C09",
            IsRead = false,
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var reloaded = await LoadChangeRequestsQuery().FirstAsync(x => x.Crid == cr.Crid, cancellationToken);
        return await BuildCostImpactViewAsync(reloaded, cancellationToken);
    }

    public async Task<CrCostImpactView?> AttachSignedAddendumAsync(string projectCode, string crCode, int managerId, Guid fileId, CancellationToken cancellationToken = default)
    {
        return await GenerateAddendumAsync(projectCode, crCode, managerId, fileId, "Đã tải lên bản phụ lục có chữ ký xác nhận.", cancellationToken);
    }

    private IQueryable<ChangeRequest> LoadChangeRequestsQuery() =>
        db.ChangeRequests.AsNoTracking().AsSplitQuery()
            .Include(x => x.Project).ThenInclude(p => p.Customer)
            .Include(x => x.Project).ThenInclude(p => p.Payments)
            .Include(x => x.Project).ThenInclude(p => p.Beos)
            .Include(x => x.ContractAddendum)
            .Include(x => x.Critems).ThenInclude(i => i.TargetSpecItem).ThenInclude(s => s.CatalogItem)
            .Include(x => x.Crhistories).ThenInclude(h => h.Actor);

    private async Task<CrCostImpactView> BuildCostImpactViewAsync(ChangeRequest cr, CancellationToken cancellationToken)
    {
        var project = cr.Project;
        var daysUntil = project.EventDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;

        var penaltyRate = cr.PenaltyRate ?? await ResolvePenaltyRateAsync(daysUntil, cancellationToken);
        var costImpact = cr.Critems.Count > 0
            ? cr.Critems.Sum(i => i.NewQuantity * i.NewUnitPrice - i.OldQuantity * i.OldUnitPrice)
            : cr.CostImpact;
        var penaltyFee = cr.PenaltyFee > 0 ? cr.PenaltyFee : decimal.Round(Math.Abs(costImpact) * penaltyRate / 100m, 0);
        var totalVariance = costImpact + penaltyFee;

        var paidAmount = project.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount ?? 0m);
        var outstandingBalance = Math.Max(0m, project.CurrentTotal - paidAmount);
        var isAlreadyApplied = cr.ContractAddendum is not null;
        var projectedProjectTotal = isAlreadyApplied ? project.CurrentTotal : Math.Max(0m, project.CurrentTotal + totalVariance);
        var projectedBalance = Math.Max(0m, projectedProjectTotal - paidAmount);

        var latestBeo = project.Beos.OrderByDescending(b => b.Version).FirstOrDefault();
        var beoStatus = latestBeo?.Status ?? "Not started";
        var impactsBeo = latestBeo is not null && latestBeo.Status is "Frozen" or "Stale";

        var items = cr.Critems.Select(i => new CrCostLineView(
            i.CritemId,
            i.TargetSpecItemId,
            i.TargetSpecItem.CatalogItem.Name,
            i.TargetSpecItem.CatalogItem.Category,
            i.TargetSpecItem.CatalogItem.Unit,
            i.OldQuantity,
            i.OldUnitPrice,
            i.NewQuantity,
            i.NewUnitPrice,
            i.NewQuantity * i.NewUnitPrice - i.OldQuantity * i.OldUnitPrice
        )).ToList();

        var history = cr.Crhistories
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new CrHistoryEntryView(h.HistoryId, h.Actor.FullName, h.Actor.Role, h.Action, h.Note, h.CreatedAt))
            .ToList();

        var addendum = cr.ContractAddendum is not null ? MapAddendum(cr.ContractAddendum, cr) : null;

        return new CrCostImpactView(
            cr.Crid,
            cr.Crcode,
            project.ProjectCode,
            project.Customer.FullName,
            cr.Type,
            cr.Description,
            cr.Status,
            costImpact,
            penaltyRate,
            penaltyFee,
            totalVariance,
            project.BaseContractValue,
            project.CurrentTotal,
            projectedProjectTotal,
            paidAmount,
            outstandingBalance,
            projectedBalance,
            daysUntil,
            impactsBeo,
            beoStatus,
            cr.CustomerApprovedAt,
            cr.ManagerApprovedAt,
            cr.CreatedAt,
            addendum,
            items,
            history);
    }

    private async Task<decimal> ResolvePenaltyRateAsync(int daysUntilEvent, CancellationToken cancellationToken)
    {
        var d = Math.Max(0, daysUntilEvent);
        var rule = await db.PolicyConfigs.AsNoTracking()
            .Where(x => x.PolicyType == "CR_PENALTY" && x.TdayMin <= d && x.TdayMax >= d)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
        return rule?.RatePercent ?? 0m;
    }

    private async Task<int?> ResolveAccessibleProjectIdAsync(string? projectCode, int userId, string? role, CancellationToken cancellationToken)
    {
        var projects = db.WeddingProjects.AsNoTracking()
            .Where(x => (role == "Customer" && x.CustomerId == userId) || (role == "Manager" && x.ManagerId == userId) || role == "Admin");
        if (!string.IsNullOrWhiteSpace(projectCode))
            projects = projects.Where(x => x.ProjectCode == projectCode.Trim());
        return await projects.OrderByDescending(x => x.CreatedAt).Select(x => (int?)x.ProjectId).FirstOrDefaultAsync(cancellationToken);
    }

    private static bool CanAccess(WeddingProject project, int userId, string? role) =>
        (role == "Customer" && project.CustomerId == userId) ||
        (role == "Manager" && project.ManagerId == userId) ||
        role == "Admin";

    private static AddendumView MapAddendum(ContractAddendum x) => MapAddendum(x, x.Cr);

    private static AddendumView MapAddendum(ContractAddendum x, ChangeRequest cr) =>
        new(x.AddendumCode, cr.Crcode, x.Status, x.TotalVariance, x.SignedAt,
            Guid.TryParse(x.FilePath, out var fileId) ? fileId : null,
            cr.Critems.Select(i => new AddendumItemView(
                i.TargetSpecItem.CatalogItem.Name,
                i.OldQuantity,
                i.OldUnitPrice,
                i.NewQuantity,
                i.NewUnitPrice,
                i.NewQuantity * i.NewUnitPrice - i.OldQuantity * i.OldUnitPrice
            )).ToList());
}
