namespace Application.Persistence;

public sealed record CustomerPaymentSummaryView(
    int ProjectId,
    string ProjectCode,
    string ProjectStatus,
    decimal BaseContractValue,
    decimal TotalApprovedCrVariance,
    decimal CurrentProjectTotal,
    decimal TotalAmountPaid,
    decimal OutstandingBalance,
    string Currency = "VND");

public sealed record CustomerPaymentMilestoneView(
    int? PaymentId,
    string PaymentType,
    string MilestoneName,
    DateOnly? DueDate,
    decimal Amount,
    decimal PaidAmount,
    string Status,
    string? PaymentMethod,
    DateOnly? PaidDate,
    bool ReceiptAvailable,
    string? ReceiptDownloadUrl = null);

public sealed record FinalSettlementReceiptInfo(
    bool IsAvailable,
    string ProjectStatus,
    string? FileName = null,
    string? DownloadUrl = null,
    string Message = "");

public sealed record CustomerPaymentsOverviewView(
    CustomerPaymentSummaryView Summary,
    IReadOnlyList<CustomerPaymentMilestoneView> Milestones,
    FinalSettlementReceiptInfo FinalReceipt);

public sealed record FileContentResultModel(
    byte[] Content,
    string ContentType,
    string FileName);

public interface ICustomerPaymentRepository
{
    Task<CustomerPaymentsOverviewView?> GetPaymentsOverviewAsync(
        int customerId,
        string? projectCode = null,
        CancellationToken cancellationToken = default);

    Task<CustomerPaymentSummaryView?> GetPaymentSummaryAsync(
        int customerId,
        string? projectCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerPaymentMilestoneView>?> GetPaymentMilestonesAsync(
        int customerId,
        string? projectCode = null,
        CancellationToken cancellationToken = default);

    Task<FileContentResultModel?> GetPaymentReceiptAsync(
        int customerId,
        int? paymentId = null,
        string? paymentType = null,
        string? projectCode = null,
        CancellationToken cancellationToken = default);

    Task<FileContentResultModel?> GetFinalSettlementReceiptAsync(
        int customerId,
        string? projectCode = null,
        CancellationToken cancellationToken = default);
}
