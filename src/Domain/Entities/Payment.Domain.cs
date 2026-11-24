using Domain.Rules;

namespace Domain.Entities;

public partial class Payment
{
    /// <summary>A deposit paid by bank transfer: recorded once, with the receipt the Manager uploaded.</summary>
    public void RecordBankReceipt(string storageKey, string fileName, string contentType, int managerId, DateOnly todayVn)
    {
        if (Status == "Paid") throw new BusinessRuleException("Tiền đặt cọc đã được ghi nhận.");
        Status = "Paid";
        PaymentMethod = EventBookingPolicy.Bank;
        ReceiptFilePath = storageKey;
        ReceiptFileName = fileName;
        ReceiptContentType = contentType;
        PaidDate = todayVn;
        RecordedBy = managerId;
    }
}
