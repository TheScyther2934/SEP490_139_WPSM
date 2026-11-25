using System.Text;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>Reads of the coordination sheet (M-13, M-14) and the PDF of a department sheet.</summary>
public sealed class BeoRepository(WpsmDbContext db) : IBeoRepository
{
    private static readonly string[] Departments = ["Kitchen", "Service", "Decor", "Tech-MC"];

    public async Task<ProjectBeoView?> GetBeoAsync(string projectCode, int managerId, CancellationToken cancellationToken = default)
    {
        var project = await LoadProjectForBeoAsync(projectCode, managerId, cancellationToken);
        return project is null ? null : MapBeoView(project);
    }

    // Saving the draft notes, freezing / re-approving, issuing the department sheets and the daily draft compilation are use cases:
    // Application/UseCases/CoordinationSheets (rules in Domain/Rules/CoordinationSheetPolicy and on Domain/Entities/Beo).

    public async Task<(byte[] Content, string ContentType, string FileName)?> ExportDepartmentSheetAsync(
        string projectCode,
        string department,
        int managerId,
        CancellationToken cancellationToken = default)
    {
        var normalizedDept = NormalizeDepartment(department);
        if (normalizedDept is null)
            throw new InvalidOperationException("Bộ phận không hợp lệ. Chỉ chấp nhận Kitchen, Service, Decor hoặc Tech-MC.");

        var project = await LoadProjectForBeoAsync(projectCode, managerId, cancellationToken);
        if (project is null) return null;

        var view = MapBeoView(project);
        var deptItems = view.Items.Where(i => i.Department == normalizedDept).ToList();
        // The department also gets its coordination tasks (preparation and event day), in deadline order.
        var deptTasks = project.DayOfTasks.Where(t => t.Department == normalizedDept).OrderBy(t => t.DeadlineTime).ToList();
        var pdfBytes = BuildSimplePdf(view, normalizedDept, DepartmentLabel(normalizedDept), deptItems, deptTasks);
        var fileName = $"BEO_{view.ProjectCode}_v{view.Version}_{normalizedDept}.pdf";
        return (pdfBytes, "application/pdf", fileName);
    }

    private Task<WeddingProject?> LoadProjectForBeoAsync(string projectCode, int managerId, CancellationToken cancellationToken) =>
        db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(p => p.Customer)
            .Include(p => p.Hall)
            .Include(p => p.Slot)
            .Include(p => p.SpecItems).ThenInclude(s => s.CatalogItem)
            .Include(p => p.AddonContracts).ThenInclude(a => a.Items).ThenInclude(i => i.CatalogItem)
            .Include(p => p.Beos).ThenInclude(b => b.ApprovedByNavigation)
            .Include(p => p.Beos).ThenInclude(b => b.BeodepartmentSheets)
            .Include(p => p.DayOfTasks)
            .FirstOrDefaultAsync(p => p.ProjectCode == projectCode.Trim() && ((p.ManagerId == managerId || p.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)) || db.Users.Any(u => u.UserId == managerId && u.Role == "Admin")), cancellationToken);

    private static ProjectBeoView MapBeoView(WeddingProject project)
    {
        var latestBeo = project.Beos.OrderByDescending(b => b.Version).FirstOrDefault();
        // Lines new to the departments: confirmed after the last approval (stale sheet), or added by the latest version (since the one before).
        var newSince = latestBeo?.Status == "Stale" ? latestBeo.FrozenAt
            : project.Beos.Where(b => b.FrozenAt != null).OrderByDescending(b => b.Version).Skip(1).FirstOrDefault()?.FrozenAt;
        // Service lines as locked, then the lines of the confirmed addon contracts (marked as changed by an addon).
        var items = project.SpecItems
            .OrderBy(s => s.SpecItemId)
            .Select(s =>
            {
                var dept = MapCategoryToDepartment(s.CatalogItem.Category);
                return new BeoItemLineView(s.SpecItemId, s.CatalogItem.Name, s.CatalogItem.Category, dept, DepartmentLabel(dept), s.CatalogItem.Unit,
                    s.Quantity, s.Quantity, s.UnitPrice, s.Quantity * s.UnitPrice, s.Description ?? "", false);
            })
            .Concat(project.AddonContracts.Where(a => a.Status == "Confirmed").OrderBy(a => a.ConfirmedAt).SelectMany(a => a.Items.Select(i => (Addon: a, Item: i))).Select(x =>
            {
                var category = x.Item.CatalogItem?.Category ?? "Other";
                var dept = MapCategoryToDepartment(category);
                var i = x.Item;
                return new BeoItemLineView(-i.AddonItemId, i.CatalogItem?.Name ?? i.Description, category, dept, DepartmentLabel(dept), i.CatalogItem?.Unit ?? "",
                    0, i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice, i.Description, true, newSince is not null && x.Addon.ConfirmedAt > newSince);
            }))
            .ToList();

        var sheets = Departments.Select((dept, index) =>
        {
            var dbSheet = latestBeo?.BeodepartmentSheets.FirstOrDefault(s => s.Department == dept);
            var deptItems = items.Where(i => i.Department == dept).ToList();
            return new BeoDepartmentSheetView(
                dbSheet?.SheetId ?? (index + 1),
                dept,
                DepartmentLabel(dept),
                dbSheet?.GeneratedAt ?? latestBeo?.FrozenAt ?? project.CreatedAt,
                dbSheet?.PdfFilePath ?? $"beo/{project.ProjectCode}_v{latestBeo?.Version ?? 1}_{dept}.pdf",
                deptItems);
        }).ToList();

        var history = project.Beos
            .OrderByDescending(b => b.Version)
            .Select(b => new BeoVersionSummaryView(
                b.Beoid,
                b.Version,
                b.Status,
                b.FrozenAt,
                b.ApprovedByNavigation?.FullName,
                b.Notes))
            .ToList();

        var daysUntil = project.EventDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;

        return new ProjectBeoView(
            project.ProjectCode,
            project.Customer.FullName,
            project.Hall.Name,
            project.Slot.SlotName,
            project.EventDate,
            daysUntil,
            latestBeo?.Beoid,
            latestBeo?.Version ?? 1,
            latestBeo?.Status ?? "Not started",
            latestBeo?.FrozenAt,
            latestBeo?.ApprovedByNavigation?.FullName,
            latestBeo?.Notes,
            items,
            sheets,
            history);
    }

    public static string MapCategoryToDepartment(string? category) => OperationsSupport.DepartmentOf(category);

    public static string DepartmentLabel(string department) => department switch
    {
        "Kitchen" => "Bộ phận Bếp (Kitchen)",
        "Service" => "Bộ phận Phục vụ & Bar (Service)",
        "Decor" => "Bộ phận Trang trí (Decor)",
        "Tech-MC" => "Bộ phận Âm thanh, Ánh sáng & MC (Tech-MC)",
        _ => department
    };

    private static string? NormalizeDepartment(string? dept) => (dept ?? "").Trim() switch
    {
        var d when string.Equals(d, "Kitchen", StringComparison.OrdinalIgnoreCase) => "Kitchen",
        var d when string.Equals(d, "Service", StringComparison.OrdinalIgnoreCase) => "Service",
        var d when string.Equals(d, "Decor", StringComparison.OrdinalIgnoreCase) => "Decor",
        var d when string.Equals(d, "Tech-MC", StringComparison.OrdinalIgnoreCase) || string.Equals(d, "TechMC", StringComparison.OrdinalIgnoreCase) => "Tech-MC",
        _ => null
    };

    private static byte[] BuildSimplePdf(ProjectBeoView beo, string dept, string deptLabel, IReadOnlyList<BeoItemLineView> items,
        IReadOnlyList<DayOfTask> tasks)
    {
        static string AsciiSafe(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    var mapped = c switch
                    {
                        'đ' => 'd',
                        'Đ' => 'D',
                        '(' or ')' or '\\' => ' ',
                        _ when c is >= ' ' and <= '~' => c,
                        _ => ' '
                    };
                    sb.Append(mapped);
                }
            }
            return sb.ToString();
        }

        var lines = new List<string>
        {
            $"WPSM - BAN DIEU HANH SU KIEN BEO (v{beo.Version} - {beo.Status})",
            $"Bo phan: {AsciiSafe(deptLabel)} [{dept}]",
            $"Du an: {beo.ProjectCode} - Khach hang: {AsciiSafe(beo.CoupleName)}",
            $"Sanh: {AsciiSafe(beo.Hall)} | Ca: {beo.Slot} | Ngay: {beo.EventDate:dd/MM/yyyy}",
            "------------------------------------------------------------------------",
            "STT | Hang muc                       | SL  | Don vi | Ghi chu",
            "------------------------------------------------------------------------"
        };

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var name = AsciiSafe(item.Name);
            if (name.Length > 30) name = name[..30];
            var note = AsciiSafe(item.Description);
            if (note.Length > 28) note = note[..28];
            lines.Add($"{i + 1,3} | {name,-30} | {item.EffectiveQuantity,3} | {AsciiSafe(item.Unit),-6} | {note}");
        }

        if (items.Count == 0) lines.Add("    (Bo phan khong co hang muc dich vu nao)");

        lines.Add("");
        lines.Add("NHIEM VU CAN HOAN THANH");
        lines.Add("------------------------------------------------------------------------");
        lines.Add("STT | Han              | Nhiem vu                         | Trang thai");
        lines.Add("------------------------------------------------------------------------");
        if (tasks.Count == 0) lines.Add("    (Chua tao nhiem vu dieu phoi cho bo phan nay)");
        for (var i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var deadline = Domain.Services.VietnamTime.ConvertUtcToVietnam(DateTime.SpecifyKind(task.DeadlineTime, DateTimeKind.Utc));
            var name = AsciiSafe(task.TaskName) + (task.PhotoRequired ? " *" : "");
            if (name.Length > 32) name = name[..32];
            var status = task.Status switch { "Done" => "Da xong", "Overdue" => "Qua han", _ => "Chua xong" };
            lines.Add($"{i + 1,3} | {deadline:HH:mm dd/MM/yyyy} | {name,-32} | {status}");
        }
        if (tasks.Any(t => t.PhotoRequired)) lines.Add("    (*) Nhiem vu can anh hoac tai lieu minh chung khi hoan thanh.");

        if (!string.IsNullOrWhiteSpace(beo.Notes))
        {
            lines.Add("------------------------------------------------------------------------");
            lines.Add($"Ghi chu van hanh: {AsciiSafe(beo.Notes)}");
        }

        return SimplePdf(lines);
    }

    /// <summary>A plain A4 PDF (Courier 10 pt), 45 lines per page, as many pages as needed.</summary>
    private static byte[] SimplePdf(IReadOnlyList<string> lines)
    {
        const int perPage = 45;
        var pages = lines.Chunk(perPage).ToList();
        if (pages.Count == 0) pages.Add([]);
        // Objects: 1 catalog, 2 pages, 3 font, then a page and its content stream for each page.
        var pdf = new StringBuilder();
        var offsets = new List<int>();
        void Obj(string body) { offsets.Add(pdf.Length); pdf.AppendLine($"{offsets.Count} 0 obj {body} endobj"); }

        pdf.AppendLine("%PDF-1.4");
        Obj("<< /Type /Catalog /Pages 2 0 R >>");
        var kids = string.Join(" ", pages.Select((_, i) => $"{4 + i * 2} 0 R"));
        Obj($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        Obj("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");
        for (var p = 0; p < pages.Count; p++)
        {
            Obj($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents {5 + p * 2} 0 R >>");
            var content = new StringBuilder("BT\n/F1 10 Tf\n");
            var y = 780;
            foreach (var line in pages[p]) { content.AppendLine($"1 0 0 1 40 {y} Tm ({line}) Tj"); y -= 16; }
            if (pages.Count > 1) content.AppendLine($"1 0 0 1 480 40 Tm (Trang {p + 1}/{pages.Count}) Tj");
            content.AppendLine("ET");
            var stream = content.ToString();
            Obj($"<< /Length {Encoding.ASCII.GetByteCount(stream)} >> stream\n{stream}endstream");
        }

        var xrefOffset = pdf.Length;
        pdf.AppendLine("xref");
        pdf.AppendLine($"0 {offsets.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets) pdf.AppendLine($"{offset:D10} 00000 n ");
        pdf.AppendLine($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString());
        pdf.AppendLine("%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
