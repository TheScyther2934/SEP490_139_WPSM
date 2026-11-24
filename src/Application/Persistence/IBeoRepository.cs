namespace Application.Persistence;

public sealed record BeoItemLineView(
    int SpecItemId,
    string Name,
    string Category,
    string Department,
    string DepartmentLabel,
    string Unit,
    int ContractQuantity,
    int EffectiveQuantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Description,
    bool ChangedByAddon,
    // Addon line added after the previous approved version: the departments have not received it yet (or just got it in this version).
    bool IsNew = false);

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

    /// <summary>UC-28 (M-11). Generates or refreshes the 4 department BEO sheets (Kitchen, Service, Decor, Tech-MC).</summary>

    /// <summary>UC-28 (M-11). Exports a printable department sheet document.</summary>
    Task<(byte[] Content, string ContentType, string FileName)?> ExportDepartmentSheetAsync(string projectCode, string department, int managerId, CancellationToken cancellationToken = default);

    /// <summary>Daily job: compiles the coordination sheet draft of every locked event that has none and notifies the Manager.</summary>
}
