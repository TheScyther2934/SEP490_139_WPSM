using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

// Shared by the Report 4 repositories: Vietnam time, GBR-03 cost formula, record codes and client links.
public static class OperationsSupport
{
    public static readonly TimeZoneInfo VnTz = FindVietnamTimeZone();
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    private static TimeZoneInfo FindVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    public static DateOnly TodayVn() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));
    public static DateTime VnToUtc(DateOnly date, TimeOnly time) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time), VnTz);
    public static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    public static string VnTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), VnTz).ToString("dd/MM/yyyy HH:mm", Vi);
    public static string Money(decimal value) => value.ToString("N0", Vi) + " VND";

    /// <summary>Next code such as CR-0007; codes are unique across projects.</summary>
    public static async Task<string> NextCodeAsync(IQueryable<string> codes, string prefix, CancellationToken cancellationToken)
    {
        var existing = await codes.Where(x => x.StartsWith(prefix + "-")).ToListAsync(cancellationToken);
        var max = existing.Select(x => int.TryParse(x[(prefix.Length + 1)..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}-{max + 1:0000}";
    }

    /// <summary>CR_PENALTY rules of the latest rule set effective on the date (SDS II.2 step 4).</summary>
    public static async Task<IReadOnlyList<PolicyConfig>> PenaltyRulesAsync(WpsmDbContext db, DateOnly onDate, CancellationToken cancellationToken)
    {
        var effective = await db.PolicyConfigs.AsNoTracking().Where(x => x.PolicyType == "CR_PENALTY" && x.EffectiveFrom <= onDate)
            .MaxAsync(x => (DateOnly?)x.EffectiveFrom, cancellationToken);
        if (effective is null) return [];
        return await db.PolicyConfigs.AsNoTracking().Where(x => x.PolicyType == "CR_PENALTY" && x.EffectiveFrom == effective)
            .OrderBy(x => x.TdayMin).ToListAsync(cancellationToken);
    }

    public static async Task<decimal> RatioAsync(WpsmDbContext db, string type, DateOnly onDate, decimal fallback, CancellationToken cancellationToken) =>
        await db.PolicyConfigs.AsNoTracking().Where(x => x.PolicyType == type && x.EffectiveFrom <= onDate)
            .OrderByDescending(x => x.EffectiveFrom).Select(x => (decimal?)x.RatePercent).FirstOrDefaultAsync(cancellationToken) ?? fallback;

    /// <summary>GBR-03: price delta, T-day penalty on |delta|, total impact. Deterministic; rounding midpoint away from zero.</summary>
    public static CrCostView Cost(IEnumerable<(int OldQuantity, decimal OldUnitPrice, int NewQuantity, decimal NewUnitPrice)> items, DateOnly eventDate,
        DateOnly onDate, IReadOnlyList<PolicyConfig> rules, bool waived, decimal currentTotal)
    {
        var delta = items.Sum(x => x.NewQuantity * x.NewUnitPrice - x.OldQuantity * x.OldUnitPrice);
        var tDay = eventDate.DayNumber - onDate.DayNumber;
        var rate = rules.FirstOrDefault(x => (x.TdayMin ?? 0) <= tDay && tDay <= (x.TdayMax ?? int.MaxValue))?.RatePercent ?? 0m;
        var fee = waived ? 0m : Math.Round(Math.Abs(delta) * rate / 100m, 0, MidpointRounding.AwayFromZero);
        return new CrCostView(delta, tDay, rate, fee, waived, delta + fee, currentTotal, currentTotal + delta + fee);
    }

    /// <summary>SDS IV.4.3: day-of deadlines as minutes before the slot start; used by the BEO timeline and JOB-04.</summary>
    public static readonly (int OffsetMinutes, string Task, string Department, bool PhotoRequired)[] DayOfTemplate =
    [
        (-120, "Dọn sảnh và sắp xếp bàn tiệc", "Service", false),
        (-90, "Hoàn thiện trang trí sân khấu", "Decor", true),
        (-60, "Kiểm tra âm thanh, ánh sáng", "Tech-MC", true),
        (-30, "Bàn đón khách và đồ uống chào mừng", "Service", false),
        (0, "Bắt đầu nghi lễ, tín hiệu cho MC", "Tech-MC", false)
    ];

    /// <summary>SDS IV.4.2: which department executes each catalog category.</summary>
    public static string DepartmentOf(string category) => category switch
    {
        "Catering" => "Kitchen",
        "Decor" => "Decor",
        "MC" or "AV" or "Photo/Video" => "Tech-MC",
        _ => "Service"
    };

    public static readonly string[] Departments = ["Kitchen", "Service", "Decor", "Tech-MC"];

    public static decimal Outstanding(WeddingProject project, IEnumerable<Payment> payments) =>
        project.CurrentTotal - payments.Where(x => x.Status == "Paid").Sum(x => x.Amount ?? 0);

    public static void Notify(WpsmDbContext db, int recipientId, string type, string message, string? link) =>
        db.Notifications.Add(new Notification
        {
            RecipientId = recipientId, Type = type, LinkUrl = link, CreatedAt = DateTime.UtcNow,
            Message = message.Length > 300 ? message[..300] : message
        });

    // React routes (src/Client/src/routes/paths.js); notifications open them on click.
    public static class Links
    {
        private static string Seg(string value) => Uri.EscapeDataString(value);
        public static string ManagerProject(string code) => $"/manager/projects/{Seg(code)}";
        public static string ManagerCr(string code, string cr) => $"{ManagerProject(code)}/change-requests/{Seg(cr)}";
        public static string ManagerBeo(string code) => $"{ManagerProject(code)}/beo";
        public static string ManagerPayments(string code) => $"{ManagerProject(code)}/payments";
        public static string ManagerDayOf(string code) => $"{ManagerProject(code)}/day-of";
        public static string ManagerComplaint(string code, string complaint) => $"{ManagerProject(code)}/complaints/{Seg(complaint)}";
        public static string ManagerRoadmap(string code) => $"{ManagerProject(code)}/roadmap";
        public const string ResourceCalendar = "/manager/resource-calendar";
        public static string CustomerCr(string cr) => $"/customer/change-requests/{Seg(cr)}";
        public static string CustomerComplaint(string complaint) => $"/customer/complaints/{Seg(complaint)}";
        public const string CustomerRoadmap = "/customer/roadmap";
        public const string CustomerPayments = "/customer/payments";
        public const string CustomerOverview = "/customer/overview";
        public const string CustomerContract = "/customer/contract";
    }
}
