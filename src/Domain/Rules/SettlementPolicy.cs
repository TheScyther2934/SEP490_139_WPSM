using Domain.Services;

namespace Domain.Rules;

/// <summary>
/// Stage 5 settlement (tất toán): after the event, the customer pays once the exact remainder = contract total after adjustments +
/// confirmed addon contracts - what was paid. Cash or bank transfer (a transfer needs its receipt). Pure functions.
/// </summary>
public static class SettlementPolicy
{
    public const string Cash = "Cash";
    public const string Bank = "Bank";

    /// <summary>What may be recorded here and when: only the settlement, only after the event, never dated in the future.</summary>
    public static void EnsureCanRecord(string eventStatus, string? paymentType, DateOnly eventDate, DateOnly paymentDate, DateOnly todayVn)
    {
        if (eventStatus is EventStatus.Settled or EventStatus.Closed)
            throw new BusinessRuleException("Sự kiện đã tất toán hoặc đã đóng, không thể ghi nhận thêm thanh toán.");
        if (eventStatus == EventStatus.Cancelled) throw new BusinessRuleException("Sự kiện đã hủy, không ghi nhận thanh toán.");
        var type = (paymentType ?? "").Trim();
        if (string.Equals(type, PaymentKinds.Deposit, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Tiền đặt cọc được ghi nhận khi ký hợp đồng, không ghi nhận lại ở đây.");
        if (!string.Equals(type, PaymentKinds.Settlement, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Chỉ ghi nhận được khoản tất toán.");
        if (!PaymentCalculator.IsEventCompleted(eventDate, eventStatus, todayVn)) throw new BusinessRuleException("Chỉ ghi nhận tất toán sau ngày cưới.");
        if (paymentDate > todayVn) throw new BusinessRuleException("Ngày thanh toán không được ở tương lai (giờ Việt Nam).");
    }

    /// <summary>The remainder still to pay: contract total + confirmed addons - every paid payment.</summary>
    public static decimal Outstanding(decimal contractTotal, decimal confirmedAddonTotal, IEnumerable<(string Status, decimal? Amount)> payments) =>
        PaymentCalculator.CalculateOutstandingBalance(PaymentCalculator.CalculateGrandTotal(contractTotal, confirmedAddonTotal),
            PaymentCalculator.CalculateTotalPaid(payments));

    /// <summary>One payment for the exact remainder.</summary>
    public static void EnsureExactAmount(decimal amount, decimal outstanding)
    {
        if (amount <= 0) throw new BusinessRuleException("Số tiền thanh toán phải lớn hơn 0.");
        if (amount != outstanding)
            throw new BusinessRuleException($"Tất toán thanh toán một lần đúng số còn lại {EventStageRules.Vnd(outstanding)} (đã nhập {EventStageRules.Vnd(amount)}).");
    }

    /// <summary>"cash" / "tien mat" is cash, anything else a bank transfer, which requires its receipt.</summary>
    public static string Method(string? method, bool hasReceipt)
    {
        var value = (method ?? "").Trim().ToLowerInvariant() is "cash" or "tien mat" ? Cash : Bank;
        if (value == Bank && !hasReceipt) throw new BusinessRuleException("Chuyển khoản bắt buộc tải lên chứng từ (JPG, PNG hoặc PDF).");
        return value;
    }

    /// <summary>The settlement receipt is issued only after the event and once nothing is left to pay.</summary>
    public static void EnsureCanIssueReceipt(bool eventCompleted, decimal outstanding)
    {
        if (!eventCompleted) throw new BusinessRuleException("Chỉ phát hành biên nhận tất toán sau ngày cưới.");
        if (outstanding > 0) throw new BusinessRuleException($"Còn {EventStageRules.Vnd(outstanding)} chưa thanh toán.");
    }
}
