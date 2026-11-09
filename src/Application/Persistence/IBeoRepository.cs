namespace Application.Persistence;

public sealed record BeoItemLineView(
    int SpecItemId,
    string Name,
    string Category,
    string Department,
    string DepartmentLabel,
    string Unit,
    int BaselineQuantity,
    int EffectiveQuantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Description,
    bool ChangedByAddendum);

public sealed record BeoDepartmentSheetView(
    int SheetId,
    string Department,
    string DepartmentLabel,
    DateTime GeneratedAt,
    string PdfFilePath,
    IReadOnlyList<BeoItemLineView> Items);

public sealed record BeoVersionSummaryView(
    int BeoId,
    int Version,
    string Status,
    DateTime? FrozenAt,
    string? ApprovedByName,
    string? Notes);

public sealed record ProjectBeoView(
    string ProjectCode,
    string CoupleName,
    string Hall,
    string Slot,
    DateOnly EventDate,
    int DaysUntilEvent,
    int? BeoId,
    int Version,
    string Status,
    DateTime? FrozenAt,
    string? ApprovedByName,
    string? Notes,
    IReadOnlyList<BeoItemLineView> Items,
    IReadOnlyList<BeoDepartmentSheetView> DepartmentSheets,
    IReadOnlyList<BeoVersionSummaryView> History);

public interface IBeoRepository
{
    /// <summary>UC-27..29 (M-10..M-12). Loads the active BEO, department breakdown, and version history for a project.</summary>
    Task<ProjectBeoView?> GetBeoAsync(string projectCode, int managerId, CancellationToken cancellationToken = default);

    /// <summary>UC-27 (M-10). Creates or updates draft BEO operational notes.</summary>
    Task<ProjectBeoView?> SaveDraftBeoAsync(string projectCode, int managerId, string? notes, CancellationToken cancellationToken = default);

    /// <summary>UC-27, UC-29 (M-10, M-12). Freezes a Draft BEO or re-approves a Stale BEO as a new Frozen version and generates the 4 department sheets.</summary>
    Task<ProjectBeoView?> FreezeOrReapproveBeoAsync(string projectCode, int managerId, string? notes, CancellationToken cancellationToken = default);

    /// <summary>UC-28 (M-11). Generates or refreshes the 4 department BEO sheets (Kitchen, Service, Decor, Tech-MC).</summary>
    Task<ProjectBeoView?> GenerateDepartmentSheetsAsync(string projectCode, int managerId, CancellationToken cancellationToken = default);

    /// <summary>UC-28 (M-11). Exports a printable department sheet document.</summary>
    Task<(byte[] Content, string ContentType, string FileName)?> ExportDepartmentSheetAsync(string projectCode, string department, int managerId, CancellationToken cancellationToken = default);

    /// <summary>JOB-03 (BF-04). Daily 07:00 job that compiles baseline + addenda into a BEO draft at T-7 and notifies the Manager.</summary>
    Task<int> CompileT7DraftsAsync(CancellationToken cancellationToken = default);
}
