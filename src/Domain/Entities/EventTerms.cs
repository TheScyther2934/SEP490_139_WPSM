using Domain.Services;

namespace Domain.Entities;

/// <summary>
/// The policy values an event was signed with, copied from PolicyConfigs at signing and never changed afterwards.
/// Every later rule of the event (lock date, supplier deadline, decrease floor, settlement due date) reads these values,
/// so a new policy only applies to events signed from its effective date.
/// </summary>
public partial class EventTerms
{
    public int ProjectId { get; set; }
    public decimal DepositRatio { get; set; }
    public decimal MaxDecreaseRatio { get; set; }
    public int BookingMinDays { get; set; }
    public int BookingMaxDays { get; set; }
    public int LockDays { get; set; }
    public int SupplierDeadlineDays { get; set; }
    public int SettlementDueDays { get; set; }
    /// <summary>Effective date of the newest policy row used; null when every value was the built-in default.</summary>
    public DateOnly? PolicyEffectiveFrom { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual WeddingProject Project { get; set; } = null!;

    /// <summary>Copies the policy in force at signing.</summary>
    public static EventTerms Capture(EventPolicy policy, DateOnly? policyEffectiveFrom, DateTime nowUtc) => new()
    {
        DepositRatio = policy.DepositRatio,
        MaxDecreaseRatio = policy.MaxDecreaseRatio,
        BookingMinDays = policy.BookingMinDays,
        BookingMaxDays = policy.BookingMaxDays,
        LockDays = policy.LockDays,
        SupplierDeadlineDays = policy.SupplierDeadlineDays,
        SettlementDueDays = policy.SettlementDueDays,
        PolicyEffectiveFrom = policyEffectiveFrom,
        CreatedAt = nowUtc
    };

    public EventPolicy ToPolicy() =>
        new(DepositRatio, MaxDecreaseRatio, BookingMinDays, BookingMaxDays, LockDays, SupplierDeadlineDays, SettlementDueDays);

    /// <summary>The terms of an event; the policy in force only for an event without terms (should not happen after the migration).</summary>
    public static EventPolicy PolicyOf(EventTerms? terms, EventPolicy policyInForce) => terms?.ToPolicy() ?? policyInForce;
}
