using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>UC-27..UC-29, JOB-03 (SDS IV.4.2 BeoCompiler, GBR-05).</summary>
public sealed class BeoRepository(WpsmDbContext db, IFileService files, IPdfGenerator pdf) : IBeoRepository
{
    private static readonly Dictionary<string, string> DepartmentNames = new()
    {
        ["Kitchen"] = "Bếp", ["Service"] = "Phục vụ", ["Decor"] = "Trang trí", ["Tech-MC"] = "Kỹ thuật / MC"
    };

    public async Task<BeoView?> GetAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await LoadProjectAsync(projectCode, cancellationToken);
        return project is null ? null : await MapAsync(project, cancellationToken);
    }

    public async Task<BeoView?> SaveNotesAsync(string projectCode, string? notes, CancellationToken cancellationToken = default)
    {
        var project = await LoadProjectAsync(projectCode, cancellationToken);
        if (project is null) return null;
        notes = notes?.Trim();
        if (notes?.Length > 500) throw new InvalidOperationException("Vượt quá độ dài tối đa 500 ký tự.");
        var beo = await LatestAsync(project.ProjectId, cancellationToken);
        if (beo is null)
        {
            EnsureProjectFrozen(project);
            beo = new Beo { ProjectId = project.ProjectId, Version = 1, Status = "Draft" };
            db.Beos.Add(beo);
        }
        else if (beo.Status != "Draft") throw new InvalidOperationException("BEO đã đóng băng, không thể sửa ghi chú.");
        beo.Notes = notes;
        await db.SaveChangesAsync(cancellationToken);
        return await MapAsync(project, cancellationToken);
    }

    public async Task<BeoView?> FreezeAsync(string projectCode, int managerId, CancellationToken cancellationToken = default)
    {
        var project = await LoadProjectAsync(projectCode, cancellationToken);
        if (project is null) return null;
        var beo = await LatestAsync(project.ProjectId, cancellationToken);
        if (beo?.Status is "Frozen") throw new InvalidOperationException("BEO đã được đóng băng.");
        if (beo?.Status is "Stale") throw new InvalidOperationException("BEO đang lỗi thời, hãy dùng chức năng Duyệt lại.");
        if (await FreezeBlockAsync(project, cancellationToken) is { } reason) throw new InvalidOperationException(reason);
        if (beo is null)
        {
            beo = new Beo { ProjectId = project.ProjectId, Version = 1, Status = "Draft" };
            db.Beos.Add(beo);
        }
        await FreezeVersionAsync(project, beo, managerId, cancellationToken);
        Notify(db, project.CustomerId, "BEO", $"BEO v{beo.Version} của tiệc cưới đã được chốt.", Links.CustomerOverview);
        await db.SaveChangesAsync(cancellationToken);
        return await MapAsync(project, cancellationToken);
    }

    public async Task<BeoView?> ReapproveAsync(string projectCode, int managerId, CancellationToken cancellationToken = default)
    {
        var project = await LoadProjectAsync(projectCode, cancellationToken);
        if (project is null) return null;
        var stale = await LatestAsync(project.ProjectId, cancellationToken);
        if (stale?.Status != "Stale") throw new InvalidOperationException("Chỉ duyệt lại được BEO đang lỗi thời (Stale).");
        if (await PendingCrCodesAsync(project.ProjectId, cancellationToken) is { Count: > 0 } pending)
            throw new InvalidOperationException($"Còn CR đang thương lượng ({string.Join(", ", pending)}), chưa thể duyệt lại BEO (GBR-05).");
        // The Stale version is kept unchanged for audit; the new version carries the recompiled content (UC-29).
        var next = new Beo { ProjectId = project.ProjectId, Version = stale.Version + 1, Status = "Draft", Notes = stale.Notes };
        db.Beos.Add(next);
        await FreezeVersionAsync(project, next, managerId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await MapAsync(project, cancellationToken);
    }

    public async Task<int> CreateDueDraftsAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var due = today.AddDays(7);
        var projects = await db.WeddingProjects.Where(x => x.Status == "Baseline Frozen" && x.EventDate <= due && x.EventDate >= today && !x.Beos.Any())
            .ToListAsync(cancellationToken);
        foreach (var project in projects)
        {
            db.Beos.Add(new Beo { ProjectId = project.ProjectId, Version = 1, Status = "Draft" });
            Notify(db, project.ManagerId, "BEO", $"BEO của {project.ProjectCode} đến hạn duyệt và đóng băng (T-7).", Links.ManagerBeo(project.ProjectCode));
        }
        await db.SaveChangesAsync(cancellationToken);
        return projects.Count;
    }

    private async Task FreezeVersionAsync(WeddingProject project, Beo beo, int managerId, CancellationToken cancellationToken)
    {
        var content = await CompileAsync(project, cancellationToken);
        // Render every PDF before storing any, so a rendering error leaves the BEO in Draft (UC-27 alternative flow).
        var documents = Departments.Select(department => (department, bytes: pdf.Render(SheetModel(project, beo.Version, department, content)))).ToList();
        var now = DateTime.UtcNow;
        foreach (var (department, bytes) in documents)
        {
            var stored = await files.StoreAsync(project.ProjectId, "BeoSheet", $"BEO-{project.ProjectCode}-v{beo.Version}-{department}.pdf", bytes, managerId, cancellationToken);
            beo.BeodepartmentSheets.Add(new BeodepartmentSheet { Department = department, PdfFilePath = stored.Id.ToString(), GeneratedAt = now });
        }
        beo.Status = "Frozen";
        beo.FrozenAt = now;
        beo.ApprovedBy = managerId;
    }

    private PdfDocumentModel SheetModel(WeddingProject project, int version, string department, Compiled content) =>
        new($"BEO v{version} – {DepartmentNames[department]}", $"Dự án {project.ProjectCode} · {project.Customer.FullName}",
            [("Ngày tiệc", project.EventDate.ToString("dd/MM/yyyy")), ("Sảnh / ca", $"{project.Hall.Name} – {project.Slot.SlotName} ({project.Slot.StartTime:HH\\:mm})"),
             ("Phụ lục áp dụng", content.Addenda.Count == 0 ? "Không có" : string.Join(", ", content.Addenda))],
            [
                new PdfTable("Lịch trình", ["Giờ", "Công việc"],
                    content.Timeline.Where(x => x.Department == department).Select(x => (IReadOnlyList<string>)[x.Time, x.Activity]).ToList()),
                new PdfTable("Hạng mục thực hiện", ["Hạng mục", "Mô tả", "Số lượng"],
                    content.Lines.Where(x => DepartmentOf(x.Category) == department)
                        .Select(x => (IReadOnlyList<string>)[x.Item, x.Description, $"{x.Quantity} {x.Unit}"]).ToList())
            ],
            ["Bản này thay thế mọi bản BEO trước. Không làm theo bản đã bị đánh dấu lỗi thời."]);

    private sealed record Compiled(List<BeoTimelineView> Timeline, List<BeoLineView> Lines, List<string> Addenda);

    private async Task<Compiled> CompileAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        var specs = await db.SpecItems.AsNoTracking().Include(x => x.CatalogItem).Where(x => x.ProjectId == project.ProjectId).ToListAsync(cancellationToken);
        var lines = specs.GroupBy(x => x.CatalogItemId).Select(g => g.OrderByDescending(x => x.Version).First())
            .OrderBy(x => x.CatalogItem.Category).ThenBy(x => x.CatalogItem.Name)
            .Select(x => new BeoLineView(x.CatalogItem.Category, x.CatalogItem.Name, x.Description, x.Quantity, x.CatalogItem.Unit)).ToList();
        var timeline = DayOfTemplate.Select(x => new BeoTimelineView(project.Slot.StartTime.AddMinutes(x.OffsetMinutes).ToString("HH:mm"), x.Task, x.Department)).ToList();
        var addenda = await db.ContractAddenda.AsNoTracking().Where(x => x.ProjectId == project.ProjectId).OrderBy(x => x.AddendumId)
            .Select(x => x.AddendumCode).ToListAsync(cancellationToken);
        return new Compiled(timeline, lines, addenda);
    }

    private async Task<BeoView> MapAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        var versions = await db.Beos.AsNoTracking().Include(x => x.ApprovedByNavigation).Include(x => x.BeodepartmentSheets)
            .Where(x => x.ProjectId == project.ProjectId).OrderByDescending(x => x.Version).ToListAsync(cancellationToken);
        var latest = versions.FirstOrDefault();
        var content = await CompileAsync(project, cancellationToken);
        var pending = await PendingCrCodesAsync(project.ProjectId, cancellationToken);
        var block = latest?.Status is "Frozen" or "Stale" ? null : await FreezeBlockAsync(project, cancellationToken);

        var changes = new List<BeoChangeView>();
        if (latest?.FrozenAt is { } frozenAt)
        {
            var approved = await db.ChangeRequests.AsNoTracking().Include(x => x.ContractAddendum)
                .Include(x => x.Critems).ThenInclude(x => x.TargetSpecItem).ThenInclude(x => x.CatalogItem)
                .Where(x => x.ProjectId == project.ProjectId && x.Status == "Approved" && x.ContractAddendum != null).ToListAsync(cancellationToken);
            changes = approved.Select(cr => (cr, at: (cr.CustomerApprovedAt ?? DateTime.MinValue) > (cr.ManagerApprovedAt ?? DateTime.MinValue) ? cr.CustomerApprovedAt!.Value : cr.ManagerApprovedAt ?? DateTime.MinValue))
                .Where(x => x.at > frozenAt).OrderBy(x => x.at)
                .Select(x => new BeoChangeView(x.cr.ContractAddendum!.AddendumCode, x.cr.Crcode, x.cr.ContractAddendum.TotalVariance, Utc(x.at),
                    x.cr.Critems.Select(i => new CrItemView(i.TargetSpecItemId, i.TargetSpecItem.CatalogItem.Name, i.TargetSpecItem.CatalogItem.Unit,
                        i.OldQuantity, i.OldUnitPrice, i.NewQuantity, i.NewUnitPrice, i.NewQuantity * i.NewUnitPrice - i.OldQuantity * i.OldUnitPrice)).ToList()))
                .ToList();
        }

        var displayLines = content.Lines.Select(x => x with { Category = WpsmCategoryNames.ToDisplay(x.Category) }).ToList();
        return new BeoView(project.ProjectCode, project.Customer.FullName, project.EventDate, project.EventDate.DayNumber - TodayVn().DayNumber,
            latest?.Version, latest?.Status ?? "None", latest?.FrozenAt is { } f ? Utc(f) : null, latest?.ApprovedByNavigation?.FullName, latest?.Notes,
            block is null && latest?.Status is null or "Draft", block, pending,
            content.Timeline, displayLines,
            Departments.Select(d => new BeoDepartmentView(d, content.Lines.Where(x => DepartmentOf(x.Category) == d)
                .Select(x => x with { Category = WpsmCategoryNames.ToDisplay(x.Category) }).ToList())).ToList(),
            content.Addenda,
            latest?.BeodepartmentSheets.OrderBy(x => Array.IndexOf(Departments, x.Department))
                .Select(x => new BeoSheetView(x.Department, Utc(x.GeneratedAt), Guid.TryParse(x.PdfFilePath, out var id) ? id : null)).ToList() ?? [],
            versions.Select(x => new BeoVersionView(x.Version, x.Status, x.FrozenAt is { } at ? Utc(at) : null, x.ApprovedByNavigation?.FullName)).ToList(),
            changes);
    }

    private async Task<string?> FreezeBlockAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        var today = TodayVn();
        if (project.Status != "Baseline Frozen") return "Baseline chưa được chốt hoặc tiệc đã diễn ra.";
        if (project.EventDate.AddDays(-7) > today) return $"Chỉ đóng băng BEO từ T-7 ({project.EventDate.AddDays(-7):dd/MM/yyyy}).";
        if (await PendingCrCodesAsync(project.ProjectId, cancellationToken) is { Count: > 0 } pending)
            return $"Còn CR đang thương lượng ({string.Join(", ", pending)}), chưa thể đóng băng BEO (GBR-05).";
        return null;
    }

    private static void EnsureProjectFrozen(WeddingProject project)
    {
        if (project.Status != "Baseline Frozen") throw new InvalidOperationException("Baseline chưa được chốt nên chưa có BEO.");
    }

    private Task<List<string>> PendingCrCodesAsync(int projectId, CancellationToken cancellationToken) =>
        db.ChangeRequests.AsNoTracking().Where(x => x.ProjectId == projectId && x.Status == "Negotiating").OrderBy(x => x.Crcode)
            .Select(x => x.Crcode).ToListAsync(cancellationToken);

    private Task<Beo?> LatestAsync(int projectId, CancellationToken cancellationToken) =>
        db.Beos.Where(x => x.ProjectId == projectId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken);

    private Task<WeddingProject?> LoadProjectAsync(string projectCode, CancellationToken cancellationToken) =>
        db.WeddingProjects.Include(x => x.Customer).Include(x => x.Hall).Include(x => x.Slot)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
}
