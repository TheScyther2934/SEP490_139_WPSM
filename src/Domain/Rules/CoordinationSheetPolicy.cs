using Domain.Services;

namespace Domain.Rules;

/// <summary>What freezing the coordination sheet does: freeze the current draft in place, or issue a new frozen version.</summary>
public sealed record CoordinationSheetDecision(bool NewVersion, int Version, string DefaultNotes);

/// <summary>
/// Coordination sheet (phiếu điều phối) rules. The sheet lists every service of the event for the 4 departments: the locked
/// services plus the addon contracts the customer confirmed. Pure functions: no database, testable on their own.
/// </summary>
public static class CoordinationSheetPolicy
{
    public const string Draft = "Draft";
    public const string Frozen = "Frozen";
    public const string Stale = "Stale";

    /// <summary>
    /// Freeze (first time) or re-approve (after a confirmed addon contract). Only while the event is locked: before, services still
    /// change (stage 2); after the event, the sheet is history. An addon contract still waiting for the customer is not in the sheet,
    /// so it does not block: if the customer confirms it later, the sheet turns stale and is re-approved once more.
    /// </summary>
    public static CoordinationSheetDecision Freeze(string eventStatus, int? latestVersion, string? latestStatus)
    {
        if (eventStatus != EventStatus.Locked)
            throw new BusinessRuleException("Phiếu điều phối chỉ chốt khi sự kiện đã khóa dịch vụ (giai đoạn 3).");
        if (latestVersion is null) return new(true, 1, "Chốt phiếu điều phối phiên bản v1.");
        if (latestStatus == Draft) return new(false, latestVersion.Value, "Chốt phiếu điều phối phiên bản v1.");
        var next = latestVersion.Value + 1;
        return new(true, next, $"Duyệt lại phiếu điều phối v{next} sau hợp đồng phát sinh.");
    }

    /// <summary>A confirmed addon contract changes what the departments deliver: a frozen sheet must be re-approved.</summary>
    public static bool TurnsStaleOnConfirmedAddon(string? latestStatus) => latestStatus == Frozen;
}
