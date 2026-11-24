namespace Domain.Rules;

/// <summary>
/// Resource allocation rules (UC-25, UC-26, GBR-09). A resource is shared by every hall: allocations that overlap in time may not
/// add up to more than its total quantity, unless an Admin approved an exception (UC-47). Pure functions.
/// </summary>
public static class ResourceAllocationPolicy
{
    /// <summary>Counts against capacity.</summary>
    public const string Reserved = "Reserved";
    /// <summary>Waiting for the Admin; does not count against capacity.</summary>
    public const string PendingException = "PendingException";
    /// <summary>Allowed to exceed capacity by an Admin; counts against capacity.</summary>
    public const string ExceptionApproved = "Exception Approved";
    /// <summary>Freed; ignored everywhere.</summary>
    public const string Released = "Released";

    public const int MaxNoteLength = 300;

    public static bool CountsAgainstCapacity(string status) => status is Reserved or ExceptionApproved;

    /// <summary>How many units are missing when <paramref name="requested"/> is added to what overlapping allocations already hold; 0 when it fits.</summary>
    public static int Shortage(int totalQuantity, int otherQuantity, int requested) => Math.Max(0, otherQuantity + requested - totalQuantity);

    public static bool Overlaps(DateTime startA, DateTime endA, DateTime startB, DateTime endB) => startA < endB && startB < endA;

    /// <summary>Quantity and time range of an allocation request, checked before any database lookup but the resource.</summary>
    public static void ValidateRequest(int quantity, DateTime start, DateTime end)
    {
        if (quantity <= 0) throw new BusinessRuleException("Số lượng phải là số nguyên dương.");
        if (end <= start) throw new BusinessRuleException("Giờ kết thúc phải sau giờ bắt đầu.");
    }

    public static void EnsureWithinTotal(int quantity, int totalQuantity)
    {
        if (quantity > totalQuantity) throw new BusinessRuleException($"Số lượng vượt tổng số {totalQuantity} của tài nguyên.");
    }

    /// <summary>A settled or closed event no longer takes allocations.</summary>
    public static void EnsureEventOpen(string eventStatus)
    {
        if (eventStatus is "Settled" or "Closed") throw new BusinessRuleException("Sự kiện đã kết thúc.");
    }

    /// <summary>A note sent to or written by the Admin: required, at most 300 characters. Returns it trimmed.</summary>
    public static string ValidateNote(string? note, string fieldLabel)
    {
        var clean = (note ?? "").Trim();
        if (clean.Length == 0) throw new BusinessRuleException($"Trường {fieldLabel} là bắt buộc.");
        if (clean.Length > MaxNoteLength) throw new BusinessRuleException($"Vượt quá độ dài tối đa {MaxNoteLength} ký tự.");
        return clean;
    }
}
