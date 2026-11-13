using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Persistence;

public sealed record ManagerPaymentSummaryView(
    int ProjectId,
    string ProjectCode,
    string CoupleName,
    string HallName,
    string SlotName,
    DateOnly EventDate,
    string ProjectStatus,
    decimal BaseContractValue,
    decimal TotalApprovedCrVariance,
    decimal CurrentProjectTotal,
    decimal TotalAmountPaid,
    decimal OutstandingBalance,
    bool IsEventCompleted,
    bool IsSettled,
    string Currency = "VND");

public sealed record ManagerPaymentMilestoneView(
    int? PaymentId,
    string PaymentType,
    string MilestoneName,
    DateOnly? DueDate,
    decimal Amount,
    decimal PaidAmount,
    string Status,
    string? PaymentMethod,
    DateOnly? PaidDate,
    string? RecordedByName,
    bool ReceiptAvailable,
    string? ReceiptUrl = null);

public sealed record ManagerPaymentHistoryItemView(
    int PaymentId,
    string PaymentType,
    decimal Amount,
    DateOnly? PaymentDate,
    string? Method,
    string Status,
    string? RecordedByName,
    bool ReceiptAvailable,
    string? ReceiptUrl,
    DateTime? CreatedAt);

public sealed record SettlementCalculationView(
    decimal BaseContractValue,
    decimal ApprovedCrVariance,
    decimal CurrentProjectTotal,
    decimal NrdPaid,
    decimal T30Paid,
    decimal OtherRecordedPayments,
    decimal TotalRecordedPayments,
    decimal FinalAmountDue,
    bool IsEventCompleted,
    bool CanSettle,
    bool IsSettled,
    DateTime CalculatedAt);

public sealed record SettlementPaymentItemView(
    string PaymentType,
    DateOnly? PaidDate,
    string? Method,
    decimal Amount,
    string? RecordedBy);

public sealed record FinalSettlementReceiptView(
    string ReceiptNumber,
    string ProjectCode,
    string CoupleName,
    string CustomerEmail,
    string CustomerPhone,
    DateOnly EventDate,
    string HallName,
    decimal BaseContractValue,
    decimal TotalApprovedCrVariance,
    decimal CurrentProjectTotal,
    decimal TotalAmountPaid,
    decimal FinalBalance,
    string Status,
    string IssuingManagerName,
    DateTime IssueDate,
    IReadOnlyList<SettlementPaymentItemView> PaymentHistory,
    string? DownloadUrl);

public sealed record ManagerPaymentOverviewView(
    ManagerPaymentSummaryView Summary,
    IReadOnlyList<ManagerPaymentMilestoneView> Milestones,
    IReadOnlyList<ManagerPaymentHistoryItemView> History,
    SettlementCalculationView Settlement);

public sealed record RecordPaymentCommand(
    string PaymentType,
    decimal Amount,
    DateOnly PaymentDate,
    string Method,
    byte[]? ReceiptBytes = null,
    string? ReceiptFileName = null,
    string? ReceiptContentType = null);

public sealed record RecordPaymentResult(
    bool Success,
    string Message,
    int? PaymentId = null,
    decimal NewBalance = 0m,
    bool IsSettled = false,
    ManagerPaymentOverviewView? Overview = null);

public interface IManagerPaymentRepository
{
    Task<ManagerPaymentOverviewView?> GetProjectPaymentsAsync(
        string projectCode,
        int userId,
        string role,
        CancellationToken cancellationToken = default);

    Task<RecordPaymentResult> RecordPaymentAsync(
        string projectCode,
        int managerId,
        string role,
        RecordPaymentCommand command,
        CancellationToken cancellationToken = default);

    Task<SettlementCalculationView?> GetSettlementCalculationAsync(
        string projectCode,
        int userId,
        string role,
        CancellationToken cancellationToken = default);

    Task<FinalSettlementReceiptView?> GenerateSettlementReceiptAsync(
        string projectCode,
        int managerId,
        string role,
        CancellationToken cancellationToken = default);

    Task<FileContentResultModel?> GetSettlementReceiptFileAsync(
        string projectCode,
        int userId,
        string role,
        CancellationToken cancellationToken = default);

    Task<FileContentResultModel?> GetPaymentReceiptFileAsync(
        string projectCode,
        string paymentType,
        int userId,
        string role,
        CancellationToken cancellationToken = default);
}
