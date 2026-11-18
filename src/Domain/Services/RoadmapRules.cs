namespace Domain.Services;

/// <summary>
/// Domain business rules for Roadmap tasks, milestones, and status calculation.
/// Consistent across Customer (C-03) and Manager (M-06) screens.
/// </summary>
public static class RoadmapRules
{
    private static readonly TimeZoneInfo VnTz = InitializeVietnamTimeZone();

    private static TimeZoneInfo InitializeVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    /// <summary>
    /// Gets the standard Vietnam TimeZone (UTC+7).
    /// </summary>
    public static TimeZoneInfo VietnamTimeZone => VnTz;

    /// <summary>
    /// Canonical milestone display order according to SRS FE-03.
    /// </summary>
    public static readonly IReadOnlyList<string> MilestoneOrder =
        new[] { "T-90", "T-30", "T-7", "Event Day" };

    /// <summary>
    /// Gets current date in Vietnam time.
    /// </summary>
    public static DateOnly GetVietnamToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));

    /// <summary>
    /// Gets current date and time in Vietnam time.
    /// </summary>
    public static DateTime GetVietnamNow() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz);

    /// <summary>
    /// Converts a date and time specified in Vietnam time to UTC.
    /// </summary>
    public static DateTime ConvertVietnamToUtc(DateOnly date, TimeOnly time)
    {
        var dt = date.ToDateTime(time);
        return TimeZoneInfo.ConvertTimeToUtc(dt, VnTz);
    }

    /// <summary>
    /// Converts a local Vietnam DateTime to UTC.
    /// </summary>
    public static DateTime ConvertVietnamToUtc(DateTime localVnDateTime)
    {
        var unspecified = DateTime.SpecifyKind(localVnDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, VnTz);
    }

    /// <summary>
    /// Converts a UTC DateTime to Vietnam time.
    /// </summary>
    public static DateTime ConvertUtcToVietnam(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, VnTz);
    }

    /// <summary>
    /// Ensures a DateTime is explicitly treated as UTC.
    /// </summary>
    public static DateTime NormalizeToUtc(DateTime dt) =>
        dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };

    /// <summary>
    /// Resolves milestone target date from event date.
    /// </summary>
    public static DateOnly ResolveMilestoneDate(string milestone, DateOnly eventDate) =>
        milestone switch
        {
            "T-90" => eventDate.AddDays(-90),
            "T-30" => eventDate.AddDays(-30),
            "T-7"  => eventDate.AddDays(-7),
            _      => eventDate   // "Event Day" and any unknown
        };

    /// <summary>
    /// Computes task status uniformly:
    /// - If status is "Done", it stays "Done" (completion time recorded separately).
    /// - If not "Done" and DueDate strictly before today (in VN time), it is "Overdue".
    /// - Otherwise "Pending".
    /// </summary>
    public static string CalculateStatus(string rawStatus, DateOnly dueDate, DateOnly today)
    {
        if (string.Equals(rawStatus, "Done", StringComparison.OrdinalIgnoreCase))
            return "Done";

        return dueDate < today ? "Overdue" : "Pending";
    }

    /// <summary>
    /// Computes remaining countdown days until event date (minimum 0).
    /// </summary>
    public static int CalculateCountdown(DateOnly eventDate, DateOnly today) =>
        Math.Max(0, eventDate.DayNumber - today.DayNumber);

    /// <summary>
    /// Determines whether a task requires Proof-of-Execution according to FE-08.
    /// Regular roadmap tasks do not require proof upload by default.
    /// </summary>
    public static bool RequiresProof(string milestone, string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        if (title.Contains("[Proof]", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("(Yêu cầu minh chứng)", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Minh chứng", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
