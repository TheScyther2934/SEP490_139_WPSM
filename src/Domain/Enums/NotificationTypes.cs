namespace Domain.Enums;

/// <summary>
/// Canonical notification types defined by the SRS and constrained by CK_Notifications_Type in the database.
/// </summary>
public static class NotificationTypes
{
    /// <summary>Stage deadlines: lock date reached, supplier evidence missing, settlement due.</summary>
    public const string StageReminder = "Stage reminder";
    public const string Payment = "Payment";
    /// <summary>An addon contract waits for / got the customer confirmation.</summary>
    public const string AddonContract = "Addon contract";
    public const string Beo = "BEO";
    public const string OverdueAlert = "Overdue alert";
    public const string Complaint = "Complaint";
    /// <summary>A Manager was added to / removed from the operators of a project.</summary>
    public const string Delegation = "Delegation";

    public static readonly string[] All =
    [
        StageReminder,
        Payment,
        AddonContract,
        Beo,
        OverdueAlert,
        Complaint,
        Delegation
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
