namespace Domain.Enums;

/// <summary>
/// Canonical notification types defined by the SRS and constrained by CK_Notifications_Type in the database.
/// </summary>
public static class NotificationTypes
{
    public const string RoadmapReminder = "Roadmap reminder";
    public const string Payment = "Payment";
    public const string CrUpdate = "CR update";
    public const string Beo = "BEO";
    public const string OverdueAlert = "Overdue alert";
    public const string Complaint = "Complaint";

    public static readonly string[] All =
    [
        RoadmapReminder,
        Payment,
        CrUpdate,
        Beo,
        OverdueAlert,
        Complaint
    ];

    public static bool IsValid(string? type) =>
        !string.IsNullOrWhiteSpace(type) && All.Contains(type, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Normalizes type string to the exact case required by SQL check constraint CK_Notifications_Type.
    /// </summary>
    public static string Normalize(string type)
    {
        var matched = All.FirstOrDefault(t => string.Equals(t, type, StringComparison.OrdinalIgnoreCase));
        return matched ?? type;
    }
}
