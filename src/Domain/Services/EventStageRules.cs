namespace Domain.Services;

/// <summary>Event statuses stored in WeddingProjects.Status.</summary>
public static class EventStatus
{
    public const string Adjusting = "Adjusting";   // stage 2: the operator may still change services
    public const string Locked = "Locked";         // stage 3 and 4: services locked, suppliers booked
    public const string Completed = "Completed";   // stage 5: event held, waiting for the settlement
    public const string Settled = "Settled";       // settlement paid
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";   // cancelled by the customer, deposit kept

    public static readonly IReadOnlyList<string> All = [Adjusting, Locked, Completed, Settled, Closed, Cancelled];

    public static string Label(string status) => status switch
    {
        Adjusting => "Đang điều chỉnh",
        Locked => "Đã chốt",
        Completed => "Đã tổ chức",
        Settled => "Đã tất toán",
        Closed => "Đã đóng",
        Cancelled => "Đã hủy",
        _ => status
    };

    public static bool IsActive(string status) => status is Adjusting or Locked;
}

/// <summary>Payment types stored in Payments.PaymentType: two payments per event.</summary>
public static class PaymentKinds
{
    public const string Deposit = "Deposit";
    public const string Settlement = "Settlement";

    public static string Label(string type) => type switch
    {
        Deposit => "Tiền đặt cọc",
        Settlement => "Tất toán",
        _ => type
    };
}

/// <summary>Values from PolicyConfigs; the defaults are the agreed business rules.</summary>
public sealed record EventPolicy(
    decimal DepositRatio = 30m,
    decimal MaxDecreaseRatio = 30m,
    int BookingMinDays = 15,
    int BookingMaxDays = 90,
    int LockDays = 10,
    int SupplierDeadlineDays = 8,
    int SettlementDueDays = 7)
{
    public static readonly EventPolicy Default = new();

    public static class Keys
    {
        public const string DepositRatio = "DEPOSIT_RATIO";
        public const string MaxDecreaseRatio = "MAX_DECREASE_RATIO";
        public const string BookingMinDays = "BOOKING_MIN_DAYS";
        public const string BookingMaxDays = "BOOKING_MAX_DAYS";
        public const string LockDays = "LOCK_DAYS";
        public const string SupplierDeadlineDays = "SUPPLIER_DEADLINE_DAYS";
        public const string SettlementDueDays = "SETTLEMENT_DUE_DAYS";

        public static readonly IReadOnlyList<string> All =
            [DepositRatio, MaxDecreaseRatio, BookingMinDays, BookingMaxDays, LockDays, SupplierDeadlineDays, SettlementDueDays];
    }

    /// <summary>Builds the policy from the latest value of each key; missing keys keep the default.</summary>
    public static EventPolicy From(IReadOnlyDictionary<string, decimal> values)
    {
        decimal Get(string key, decimal fallback) => values.TryGetValue(key, out var v) ? v : fallback;
        var d = Default;
        return new EventPolicy(
            Get(Keys.DepositRatio, d.DepositRatio),
            Get(Keys.MaxDecreaseRatio, d.MaxDecreaseRatio),
            (int)Get(Keys.BookingMinDays, d.BookingMinDays),
            (int)Get(Keys.BookingMaxDays, d.BookingMaxDays),
            (int)Get(Keys.LockDays, d.LockDays),
            (int)Get(Keys.SupplierDeadlineDays, d.SupplierDeadlineDays),
            (int)Get(Keys.SettlementDueDays, d.SettlementDueDays));
    }
}

public sealed record EventStageView(int Number, string Title, string Description, DateOnly? StartDate, DateOnly? EndDate, string State);

/// <summary>
/// The five stages of an event:
/// 1 sign and deposit, 2 adjusting (until LockDays before), 3 locked and suppliers booked, 4 event day, 5 settlement.
/// Stages are derived from the event date, the policy and the status, so they never need to be stored.
/// </summary>
public static class EventStageRules
{
    private static readonly System.Globalization.CultureInfo Vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Money as Vietnamese users read it: 52.500.000 đ.</summary>
    public static string Vnd(decimal value) => value.ToString("N0", Vi) + " đ";

    public const int SignAndDeposit = 1, Adjusting = 2, Locked = 3, EventDay = 4, Settlement = 5;

    public static string StageTitle(int stage) => stage switch
    {
        SignAndDeposit => "Ký hợp đồng và đặt cọc",
        Adjusting => "Điều chỉnh dịch vụ",
        Locked => "Chốt và đặt nhà cung cấp",
        EventDay => "Ngày cưới",
        Settlement => "Tất toán",
        _ => $"Giai đoạn {stage}"
    };

    public static DateOnly LockDate(DateOnly eventDate, EventPolicy policy) => eventDate.AddDays(-policy.LockDays);

    public static DateOnly SupplierDeadline(DateOnly eventDate, EventPolicy policy) => eventDate.AddDays(-policy.SupplierDeadlineDays);

    public static DateOnly SettlementDueDate(DateOnly eventDate, EventPolicy policy) => eventDate.AddDays(policy.SettlementDueDays);

    /// <summary>Stage 1 rule: the event date must be between BookingMinDays and BookingMaxDays after today. Returns an error or null.</summary>
    public static string? ValidateBookingDate(DateOnly eventDate, DateOnly today, EventPolicy policy)
    {
        var days = eventDate.DayNumber - today.DayNumber;
        if (days < policy.BookingMinDays)
            return $"Ngày cưới phải cách hôm nay ít nhất {policy.BookingMinDays} ngày (hiện chỉ còn {Math.Max(days, 0)} ngày).";
        if (days > policy.BookingMaxDays)
            return $"Chỉ nhận ký hợp đồng trước ngày cưới tối đa {policy.BookingMaxDays} ngày (ngày đã chọn cách {days} ngày).";
        return null;
    }

    public static decimal DepositFor(decimal contractValue, EventPolicy policy) =>
        Math.Round(contractValue * policy.DepositRatio / 100m, 0, MidpointRounding.AwayFromZero);

    /// <summary>Stage 2 floor: the total may not go below (100 - MaxDecreaseRatio)% of the value signed in stage 1.</summary>
    public static decimal MinimumTotal(decimal signedValue, EventPolicy policy) =>
        Math.Round(signedValue * (100m - policy.MaxDecreaseRatio) / 100m, 0, MidpointRounding.AwayFromZero);

    /// <summary>Returns an error when the new total breaks the floor, otherwise null. Increases are never limited.</summary>
    public static string? ValidateAdjustedTotal(decimal signedValue, decimal newTotal, EventPolicy policy)
    {
        var floor = MinimumTotal(signedValue, policy);
        return newTotal < floor
            ? $"Tổng giá trị sau điều chỉnh ({Vnd(newTotal)}) thấp hơn mức tối thiểu {Vnd(floor)} (không được giảm quá {policy.MaxDecreaseRatio:G29}% giá trị lúc ký)."
            : null;
    }

    /// <summary>The system locks an adjusting event on the lock date.</summary>
    public static bool ShouldLock(string status, DateOnly eventDate, DateOnly today, EventPolicy policy) =>
        status == EventStatus.Adjusting && today >= LockDate(eventDate, policy);

    /// <summary>Services can be changed only while adjusting and before the lock date.</summary>
    public static bool CanAdjust(string status, DateOnly eventDate, DateOnly today, EventPolicy policy) =>
        status == EventStatus.Adjusting && today < LockDate(eventDate, policy);

    /// <summary>Addon contracts exist only for a locked event, until it is settled.</summary>
    public static bool CanAddAddon(string status) => status is EventStatus.Locked or EventStatus.Completed;

    public static int CurrentStage(string status, DateOnly eventDate, DateOnly today) => status switch
    {
        EventStatus.Adjusting => Adjusting,
        EventStatus.Locked => today >= eventDate ? EventDay : Locked,
        EventStatus.Completed or EventStatus.Settled or EventStatus.Closed => Settlement,
        _ => SignAndDeposit
    };

    /// <summary>The five stages with their dates and state (Done / Current / Upcoming / Cancelled) for the timeline.</summary>
    public static IReadOnlyList<EventStageView> Timeline(string status, DateOnly eventDate, DateOnly signedOn, DateOnly today, EventPolicy policy)
    {
        var current = CurrentStage(status, eventDate, today);
        var finished = status is EventStatus.Settled or EventStatus.Closed;
        string State(int n) => status == EventStatus.Cancelled ? (n == SignAndDeposit ? "Done" : "Cancelled")
            : finished || n < current ? "Done" : n == current ? "Current" : "Upcoming";
        var lockDate = LockDate(eventDate, policy);
        return
        [
            new(SignAndDeposit, StageTitle(SignAndDeposit), $"Ký hợp đồng, đặt cọc {policy.DepositRatio:G29}% giá trị.", signedOn, signedOn, State(SignAndDeposit)),
            new(Adjusting, StageTitle(Adjusting), $"Thử món, thử đồ, thay đổi dịch vụ qua người phụ trách. Không giảm quá {policy.MaxDecreaseRatio:G29}% giá trị hợp đồng.",
                signedOn, lockDate.AddDays(-1), State(Adjusting)),
            new(Locked, StageTitle(Locked), $"Hệ thống khóa dịch vụ. Người phụ trách đặt nhà cung cấp trước {policy.SupplierDeadlineDays} ngày.",
                lockDate, eventDate.AddDays(-1), State(Locked)),
            new(EventDay, StageTitle(EventDay), "Tổ chức tiệc cưới và ghi nhận ảnh minh chứng.", eventDate, eventDate, State(EventDay)),
            new(Settlement, StageTitle(Settlement), "Thanh toán phần còn lại cùng các hợp đồng phát sinh đã xác nhận.",
                eventDate.AddDays(1), SettlementDueDate(eventDate, policy), State(Settlement))
        ];
    }
}
