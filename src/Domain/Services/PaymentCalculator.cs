namespace Domain.Services;

/// <summary>
/// Money of an event: two payments only, the deposit at signing and the settlement after the event.
/// Settlement = contract total after adjustments + confirmed addon contracts - deposit paid.
/// </summary>
public static class PaymentCalculator
{
    private static readonly TimeZoneInfo VnTz = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    public static DateOnly GetVietnamToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));

    /// <summary>Sum of the payments recorded as Paid.</summary>
    public static decimal CalculateTotalPaid(IEnumerable<(string Status, decimal? Amount)> payments) =>
        payments.Where(p => string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount ?? 0m);

    /// <summary>What the customer owes in total: the contract after adjustments plus the confirmed addon contracts.</summary>
    public static decimal CalculateGrandTotal(decimal contractTotal, decimal confirmedAddonTotal) =>
        Math.Max(0m, contractTotal) + Math.Max(0m, confirmedAddonTotal);

    /// <summary>Amount of the settlement payment (never negative: the 70% floor keeps the contract above the 30% deposit).</summary>
    public static decimal CalculateSettlementAmount(decimal contractTotal, decimal confirmedAddonTotal, decimal depositPaid) =>
        Math.Max(0m, CalculateGrandTotal(contractTotal, confirmedAddonTotal) - depositPaid);

    public static decimal CalculateOutstandingBalance(decimal grandTotal, decimal totalAmountPaid) =>
        Math.Max(0m, grandTotal - totalAmountPaid);

    /// <summary>Display status of a payment: Paid, Due (due date reached) or Upcoming.</summary>
    public static string ResolvePaymentStatus(string? recordedStatus, DateOnly? dueDate, DateOnly today)
    {
        if (string.Equals(recordedStatus, "Paid", StringComparison.OrdinalIgnoreCase)) return "Paid";
        return dueDate.HasValue && today >= dueDate.Value ? "Due" : "Upcoming";
    }

    public static bool IsEventCompleted(DateOnly eventDate, string eventStatus, DateOnly today) =>
        today >= eventDate || eventStatus is EventStatus.Completed or EventStatus.Settled or EventStatus.Closed;
}
