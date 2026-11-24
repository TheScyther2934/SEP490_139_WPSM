using Domain.Rules;
using Domain.Services;

namespace Domain.Entities;

public partial class Payment
{
    /// <summary>The settlement payment row, when the invoice did not create one (nothing was due when it was issued).</summary>
    public static Payment NewSettlement(int projectId, DateOnly dueDate) =>
        new() { ProjectId = projectId, PaymentType = PaymentKinds.Settlement, Status = "Due", DueDate = dueDate };

    /// <summary>The settlement is paid once, for the exact remainder (SettlementPolicy); a transfer keeps its stored receipt.</summary>
    public void RecordSettlement(decimal amount, string method, DateOnly paidDate, int managerId, string? receiptKey, string? receiptName, string? receiptType)
    {
        if (Status == "Paid") throw new BusinessRuleException("Khoản tất toán đã được ghi nhận.");
        Status = "Paid";
        Amount = amount;
        PaymentMethod = method;
        PaidDate = paidDate;
        RecordedBy = managerId;
        if (receiptKey is null) return;
        ReceiptFilePath = receiptKey;
        ReceiptFileName = receiptName;
        ReceiptContentType = receiptType;
    }
}

public partial class SettlementInvoice
{
    public void MarkPaid(DateTime nowUtc)
    {
        Status = "Paid";
        PaidAt = nowUtc;
    }
}

public partial class WeddingProject
{
    /// <summary>The event day has passed: a locked event becomes held (stage 5), waiting for the settlement.</summary>
    public void MarkHeld()
    {
        if (Status == EventStatus.Locked) Status = EventStatus.Completed;
    }

    /// <summary>Nothing left to pay: the event is settled (ratings open, GBR-10).</summary>
    public void MarkSettled() => Status = EventStatus.Settled;
}
