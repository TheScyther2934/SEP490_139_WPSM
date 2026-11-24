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
        var pdfBytes = BuildSimplePdf(view, normalizedDept, DepartmentLabel(normalizedDept), deptItems);
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

    private static byte[] BuildSimplePdf(ProjectBeoView beo, string dept, string deptLabel, IReadOnlyList<BeoItemLineView> items)
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

        if (!string.IsNullOrWhiteSpace(beo.Notes))
        {
            lines.Add("------------------------------------------------------------------------");
            lines.Add($"Ghi chu van hanh: {AsciiSafe(beo.Notes)}");
        }

        var contentSb = new StringBuilder();
        contentSb.AppendLine("BT");
        contentSb.AppendLine("/F1 10 Tf");
        var y = 780;
        foreach (var line in lines)
        {
            contentSb.AppendLine($"1 0 0 1 40 {y} Tm ({line}) Tj");
            y -= 16;
            if (y < 60) break;
        }
        contentSb.AppendLine("ET");
        var streamBytes = Encoding.ASCII.GetBytes(contentSb.ToString());

        var pdf = new StringBuilder();
        pdf.AppendLine("%PDF-1.4");
        var obj1Offset = pdf.Length;
        pdf.AppendLine("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj");
        var obj2Offset = pdf.Length;
        pdf.AppendLine("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj");
        var obj3Offset = pdf.Length;
        pdf.AppendLine("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj");
        var obj4Offset = pdf.Length;
        pdf.AppendLine("4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Courier >> endobj");
        var obj5Offset = pdf.Length;
        pdf.AppendLine($"5 0 obj << /Length {streamBytes.Length} >> stream");
        pdf.Append(Encoding.ASCII.GetString(streamBytes));
        pdf.AppendLine("endstream endobj");
        var xrefOffset = pdf.Length;
        pdf.AppendLine("xref");
        pdf.AppendLine("0 6");
        pdf.AppendLine("0000000000 65535 f ");
        pdf.AppendLine($"{obj1Offset:D10} 00000 n ");
        pdf.AppendLine($"{obj2Offset:D10} 00000 n ");
        pdf.AppendLine($"{obj3Offset:D10} 00000 n ");
        pdf.AppendLine($"{obj4Offset:D10} 00000 n ");
        pdf.AppendLine($"{obj5Offset:D10} 00000 n ");
        pdf.AppendLine("trailer << /Size 6 /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefOffset.ToString());
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
