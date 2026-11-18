namespace Domain.Services;

/// <summary>
/// Pure domain calculations for contract finances, milestone payments, and balances (FE-10, UC-12).
/// Follows GBR-08 and SRS Section 2.
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

    /// <summary>
    /// Sums variance of Approved CRs only (status "Approved" or "CustomerApproved").
    /// Negative variance (deduction) reduces total; positive variance (surcharge) increases total.
    /// </summary>
    public static decimal CalculateApprovedCrVariance(IEnumerable<(string Status, decimal? TotalVariance, decimal CostImpact, decimal PenaltyFee)> changeRequests)
    {
        decimal sum = 0m;
        foreach (var cr in changeRequests)
        {
            if (string.Equals(cr.Status, "Approved", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cr.Status, "CustomerApproved", StringComparison.OrdinalIgnoreCase))
            {
                var variance = cr.TotalVariance ?? (cr.CostImpact + cr.PenaltyFee);
                sum += variance;
            }
        }
        return sum;
    }

    /// <summary>
    /// Current Project Total = Base Contract Value + Total Approved CR Variance.
    /// </summary>
    public static decimal CalculateCurrentTotal(decimal baseContractValue, decimal approvedCrVariance) =>
        Math.Max(0m, baseContractValue + approvedCrVariance);

    /// <summary>
    /// Total Amount Paid = sum of recorded payments with Status == "Paid".
    /// </summary>
    public static decimal CalculateTotalPaid(IEnumerable<(string Status, decimal? Amount)> payments) =>
        payments
            .Where(p => string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Amount ?? 0m);

    /// <summary>
    /// Outstanding Balance = Current Project Total - Total Amount Paid.
    /// Never returns negative according to domain rules.
    /// </summary>
    public static decimal CalculateOutstandingBalance(decimal currentProjectTotal, decimal totalAmountPaid) =>
        Math.Max(0m, currentProjectTotal - totalAmountPaid);

    /// <summary>
    /// Calculates T-30 second payment amount according to policy ratio:
    /// Typically Total * (T30_RATIO / 100) - NRD.
    /// </summary>
    public static decimal CalculateT30Amount(decimal baseValue, decimal nrdAmount, decimal t30Ratio = 70m)
    {
        var targetCumulative = decimal.Round(baseValue * (t30Ratio / 100m), 2);
        return Math.Max(0m, targetCumulative - nrdAmount);
    }

    /// <summary>
    /// Determines milestone display status: "Paid", "Due", or "Upcoming".
    /// </summary>
    public static string ResolveMilestoneStatus(string? recordedStatus, DateOnly? dueDate, DateOnly today, string projectStatus, string milestoneType)
    {
        if (string.Equals(recordedStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            return "Paid";

        if (string.Equals(milestoneType, "FINAL", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(projectStatus, "Settled", StringComparison.OrdinalIgnoreCase))
            return "Paid";

        if (dueDate.HasValue && today >= dueDate.Value)
            return "Due";

        return "Upcoming";
    }

    public static bool IsEventCompleted(DateOnly eventDate, string projectStatus, DateOnly today) =>
        today >= eventDate ||
        string.Equals(projectStatus, "Event Completed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(projectStatus, "Settled", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(projectStatus, "Closed", StringComparison.OrdinalIgnoreCase);

    public static string GetMilestoneName(string paymentType) =>
        paymentType.ToUpperInvariant() switch
        {
            "NRD" => "Đặt cọc hợp đồng (NRD)",
            "T30" or "T-30" => "Thanh toán đợt hai (Mốc T-30)",
            "FINAL" or "SETTLEMENT" => "Quyết toán hợp đồng (Final Settlement)",
            _ => paymentType
        };
}
