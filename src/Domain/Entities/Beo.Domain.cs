using Domain.Rules;

namespace Domain.Entities;

/// <summary>
/// Behaviour of a coordination sheet version (phiếu điều phối). Whether a version may be frozen is CoordinationSheetPolicy.Freeze;
/// these methods carry out what was decided.
/// </summary>
public partial class Beo
{
    public const string DefaultDraftNotes = "Bản thảo BEO khởi tạo từ hợp đồng đặc tả và phụ lục.";

    /// <summary>A first draft (v1). The department sheets are added through the navigation, so one save stores everything.</summary>
    public static Beo NewDraft(int projectId, string notes) => new()
    {
        ProjectId = projectId,
        Version = 1,
        Status = CoordinationSheetPolicy.Draft,
        Notes = notes
    };

    /// <summary>A new version issued when an approved sheet is re-approved after a confirmed addon contract.</summary>
    public static Beo NewVersion(int projectId, int version) => new() { ProjectId = projectId, Version = version, Status = CoordinationSheetPolicy.Draft };

    /// <summary>
    /// The operator's notes. A draft takes them as written (empty clears them); an approved or stale version keeps its notes when
    /// nothing is written.
    /// </summary>
    public void EditNotes(string? notes)
    {
        var text = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Notes = Status == CoordinationSheetPolicy.Draft ? text : text ?? Notes;
    }

    public void Freeze(int managerId, string notes, DateTime nowUtc)
    {
        Status = CoordinationSheetPolicy.Frozen;
        FrozenAt = nowUtc;
        ApprovedBy = managerId;
        Notes = notes;
    }

    /// <summary>One sheet per department (Kitchen, Service, Decor, Tech-MC) for this version; the PDF is generated when downloaded.</summary>
    public void RefreshDepartmentSheets(string projectCode, DateTime nowUtc)
    {
        foreach (var department in CoordinationTaskPolicy.Departments)
        {
            var path = $"beo/{projectCode}_v{Version}_{department}.pdf";
            if (BeodepartmentSheets.FirstOrDefault(s => s.Department == department) is { } existing)
            {
                existing.PdfFilePath = path;
                existing.GeneratedAt = nowUtc;
            }
            else BeodepartmentSheets.Add(new BeodepartmentSheet { Department = department, PdfFilePath = path, GeneratedAt = nowUtc });
        }
    }
}
