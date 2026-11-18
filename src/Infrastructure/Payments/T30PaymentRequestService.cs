using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Application.Payments;
using Application.Persistence;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Payments;

/// <summary>
/// Implements JOB-02: Issue T-30 Payment Request according to Report 3 (SRS) & Report 4 (SDS).
/// Automatically queries Wedding Projects whose Event Date is exactly Today + 30 days,
/// calculates T-30 payment amount using the BalanceCalculator formula,
/// issues the T-30 payment milestone with Status = Due,
/// creates notifications for Customer & Manager, and queues emails via JOB-07.
/// </summary>
public sealed class T30PaymentRequestService(
    WpsmDbContext db,
    INotificationRepository notificationRepository,
    IEmailOutbox emailOutbox,
    EmailOptions emailOptions,
    ILogger<T30PaymentRequestService> logger) : IT30PaymentRequestService
{
    private static readonly HashSet<string> InactiveProjectStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Closed", "Settled", "Event Completed", "Cancelled"
    };

    public async Task<T30PaymentRequestResult> IssueT30PaymentRequestsAsync(
        IssueT30PaymentRequestCommand? command = null,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;
        var today = command?.TargetDate ?? PaymentCalculator.GetVietnamToday();
        var targetEventDate = today.AddDays(30);

        logger.LogInformation(
            "JOB-02 [ExecutionId: {ExecutionId}] Checking projects with EventDate == {TargetEventDate:yyyy-MM-dd} (Today: {Today:yyyy-MM-dd}).",
            executionId, targetEventDate, today);

        // Fetch dynamic T-30 ratio from PolicyConfig
        var policyRate = await db.PolicyConfigs
            .AsNoTracking()
            .Where(p => p.PolicyType == "T30_RATIO" && p.EffectiveFrom <= today)
            .OrderByDescending(p => p.EffectiveFrom)
            .Select(p => p.RatePercent)
            .FirstOrDefaultAsync(cancellationToken);

        var t30Ratio = policyRate > 0 ? policyRate : 70m;
        logger.LogInformation("Using T30_RATIO: {Ratio}% from PolicyConfig.", t30Ratio);

        // Query candidate projects: EventDate == Today + 30, active, baseline frozen
        var candidateProjects = await db.WeddingProjects
            .Include(p => p.Customer)
            .Include(p => p.Manager)
            .Include(p => p.Payments)
            .Include(p => p.ChangeRequests)
            .Where(p => p.EventDate == targetEventDate
                     && !InactiveProjectStatuses.Contains(p.Status)
                     && p.BaselineFrozenAt != null
                     && p.BaseContractValue > 0)
            .OrderBy(p => p.ProjectId)
            .ToListAsync(cancellationToken);

        int totalChecked = candidateProjects.Count;
        int requestsIssued = 0;
        int emailsQueued = 0;
        int skipped = 0;
        int failed = 0;
        var errors = new List<string>();

        var baseUrl = emailOptions.PublicBaseUrl?.TrimEnd('/') ?? "http://localhost:5173";

        foreach (var project in candidateProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 1. Idempotency check: project must not already have a T-30 payment
                var hasT30InProject = project.Payments.Any(p =>
                    string.Equals(p.PaymentType, "T30", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.PaymentType, "T-30", StringComparison.OrdinalIgnoreCase));

                if (hasT30InProject)
                {
                    logger.LogInformation("Project {ProjectCode} already has a T-30 payment milestone. Skipping.", project.ProjectCode);
                    skipped++;
                    continue;
                }

                var dbHasT30 = await db.Payments.AsNoTracking()
                    .AnyAsync(p => p.ProjectId == project.ProjectId &&
                                  (p.PaymentType == "T30" || p.PaymentType == "T-30"), cancellationToken);

                if (dbHasT30)
                {
                    logger.LogInformation("Project {ProjectCode} already has a T-30 payment milestone in DB. Skipping.", project.ProjectCode);
                    skipped++;
                    continue;
                }

                // 2. Validate Customer and Manager accounts
                var customer = project.Customer;
                if (customer == null ||
                    customer.UserId != project.CustomerId ||
                    !string.Equals(customer.Role, "Customer", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Project {ProjectCode} has invalid customer owner (CustomerId: {CustomerId}). Skipping.",
                        project.ProjectCode, project.CustomerId);
                    skipped++;
                    continue;
                }

                var manager = project.Manager;
                if (manager == null ||
                    manager.UserId != project.ManagerId ||
                    !string.Equals(manager.Role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(manager.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Project {ProjectCode} has invalid manager owner (ManagerId: {ManagerId}). Skipping.",
                        project.ProjectCode, project.ManagerId);
                    skipped++;
                    continue;
                }

                // 3. Verify NRD payment was initialized
                var nrdPayment = project.Payments.FirstOrDefault(p =>
                    string.Equals(p.PaymentType, "NRD", StringComparison.OrdinalIgnoreCase));

                if (nrdPayment == null)
                {
                    logger.LogWarning("Project {ProjectCode} has no NRD payment record initialized. Skipping.", project.ProjectCode);
                    skipped++;
                    continue;
                }

                var nrdAmount = nrdPayment.Amount ?? project.Nrdamount;

                // 4. Calculate financials using BalanceCalculator / PaymentCalculator
                // Only Approved / CustomerApproved CRs are included
                var crTuples = project.ChangeRequests.Select(c => (c.Status, c.TotalVariance, c.CostImpact, c.PenaltyFee));
                var approvedCrVariance = PaymentCalculator.CalculateApprovedCrVariance(crTuples);
                var currentTotal = PaymentCalculator.CalculateCurrentTotal(project.BaseContractValue, approvedCrVariance);

                var paymentTuples = project.Payments.Select(p => (p.Status, p.Amount));
                var totalPaid = PaymentCalculator.CalculateTotalPaid(paymentTuples);
                var outstandingBalance = PaymentCalculator.CalculateOutstandingBalance(currentTotal, totalPaid);

                // 5. Fully Paid check (Section 5)
                if (outstandingBalance <= 0)
                {
                    logger.LogInformation("Project {ProjectCode} is fully paid (OutstandingBalance <= 0). Skipping T-30 request.", project.ProjectCode);
                    skipped++;
                    continue;
                }

                // 6. Calculate T30 Required: Round(Current Project Total * T30 Ratio) - NRD
                var ratio = t30Ratio > 1m ? t30Ratio / 100m : t30Ratio;
                var cumulativeTarget = Math.Round(currentTotal * ratio, 0, MidpointRounding.AwayFromZero);
                var t30Required = Math.Max(0m, cumulativeTarget - nrdAmount);
                t30Required = Math.Round(t30Required, 0, MidpointRounding.AwayFromZero);
                t30Required = Math.Min(t30Required, outstandingBalance);

                if (t30Required <= 0)
                {
                    logger.LogInformation("Project {ProjectCode} calculated T-30 amount is <= 0 ({Amount}). Skipping T-30 request.",
                        project.ProjectCode, t30Required);
                    skipped++;
                    continue;
                }

                // Check for Negotiating CR (Section 6)
                var hasNegotiatingCr = project.ChangeRequests.Any(cr =>
                    string.Equals(cr.Status, "Negotiating", StringComparison.OrdinalIgnoreCase));

                // 7. Create T-30 Payment milestone in database
                var t30Payment = new Payment
                {
                    ProjectId = project.ProjectId,
                    PaymentType = "T30",
                    Status = "Due",
                    DueDate = today,
                    Amount = t30Required
                };

                db.Payments.Add(t30Payment);
                project.CurrentTotal = currentTotal;
                await db.SaveChangesAsync(cancellationToken);

                requestsIssued++;

                // 8. Create In-App Notifications for Customer and Manager (Section 7)
                var encodedCode = Uri.EscapeDataString(project.ProjectCode);
                var customerMsg = $"[{project.ProjectCode}] Yêu cầu thanh toán đợt hai (T-30 Second Payment): Số tiền {t30Required:N0} VND, hạn thanh toán {today:dd/MM/yyyy}. Dư nợ còn lại: {outstandingBalance:N0} VND.";
                var managerMsg = $"[{project.ProjectCode}] Đã phát hành yêu cầu thanh toán đợt hai (T-30): Số tiền {t30Required:N0} VND, hạn thanh toán {today:dd/MM/yyyy}. Dư nợ còn lại: {outstandingBalance:N0} VND.";

                await notificationRepository.CreateNotificationAsync(
                    recipientId: customer.UserId,
                    type: NotificationTypes.Payment,
                    message: customerMsg,
                    linkUrl: "/customer/payments",
                    relatedEntityType: "Payment",
                    relatedEntityId: t30Payment.PaymentId.ToString(),
                    cancellationToken: cancellationToken);

                await notificationRepository.CreateNotificationAsync(
                    recipientId: manager.UserId,
                    type: NotificationTypes.Payment,
                    message: managerMsg,
                    linkUrl: $"/manager/projects/{encodedCode}/payments",
                    relatedEntityType: "Payment",
                    relatedEntityId: t30Payment.PaymentId.ToString(),
                    cancellationToken: cancellationToken);

                if (hasNegotiatingCr)
                {
                    var crReminderMsg = $"[{project.ProjectCode}] Dự án có yêu cầu thay đổi (CR) đang trong quá trình đàm phán (Negotiating). Vui lòng xử lý để cập nhật quyết toán hợp đồng.";
                    await notificationRepository.CreateNotificationAsync(
                        recipientId: manager.UserId,
                        type: NotificationTypes.CrUpdate,
                        message: crReminderMsg,
                        linkUrl: $"/manager/projects/{encodedCode}/change-requests",
                        relatedEntityType: "Project",
                        relatedEntityId: project.ProjectCode,
                        cancellationToken: cancellationToken);
                }

                // 9. Queue Emails via JOB-07 (Section 8 - isolated error handling)
                try
                {
                    var customerEmailSubject = $"[{project.ProjectCode}] Yêu cầu thanh toán đợt 2 (Mốc T-30)";
                    var customerEmailBody = EmailTemplates.T30PaymentRequest(
                        fullName: customer.FullName,
                        role: "Customer",
                        projectCode: project.ProjectCode,
                        amount: t30Required,
                        dueDate: today,
                        currentTotal: currentTotal,
                        totalPaid: totalPaid,
                        outstandingBalance: outstandingBalance,
                        paymentUrl: $"{baseUrl}/customer/payments",
                        hasNegotiatingCr: false);

                    emailOutbox.Enqueue(
                        toEmail: customer.Email,
                        subject: customerEmailSubject,
                        bodyHtml: customerEmailBody,
                        kind: "PaymentRequest",
                        projectId: project.ProjectId);

                    var managerEmailSubject = $"[{project.ProjectCode}] Thông báo phát hành yêu cầu thanh toán T-30";
                    var managerEmailBody = EmailTemplates.T30PaymentRequest(
                        fullName: manager.FullName,
                        role: "Manager",
                        projectCode: project.ProjectCode,
                        amount: t30Required,
                        dueDate: today,
                        currentTotal: currentTotal,
                        totalPaid: totalPaid,
                        outstandingBalance: outstandingBalance,
                        paymentUrl: $"{baseUrl}/manager/projects/{encodedCode}/payments",
                        hasNegotiatingCr: hasNegotiatingCr);

                    emailOutbox.Enqueue(
                        toEmail: manager.Email,
                        subject: managerEmailSubject,
                        bodyHtml: managerEmailBody,
                        kind: "PaymentRequest",
                        projectId: project.ProjectId);

                    emailsQueued += 2;
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to queue T-30 payment emails for project {ProjectCode}. Payment milestone and in-app notifications were created successfully.",
                        project.ProjectCode);
                }
            }
            catch (DbUpdateException dbEx)
            {
                logger.LogWarning(dbEx, "Concurrency collision or DB constraint violation while creating T-30 payment for {ProjectCode}. Skipping.", project.ProjectCode);
                skipped++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                var errorMsg = $"Project {project.ProjectCode} error: {ex.Message}";
                errors.Add(errorMsg);
                logger.LogError(ex, "Unexpected error issuing T-30 payment for project {ProjectCode}.", project.ProjectCode);
            }
        }

        var completedAt = DateTime.UtcNow;

        logger.LogInformation(
            "JOB-02 [ExecutionId: {ExecutionId}] Summary - StartedAt: {StartedAt:O}, CompletedAt: {CompletedAt:O}, TotalProjectsChecked: {TotalProjectsChecked}, TotalRequestsIssued: {TotalRequestsIssued}, TotalEmailsQueued: {TotalEmailsQueued}, TotalSkipped: {TotalSkipped}, TotalFailed: {TotalFailed}",
            executionId, startedAt, completedAt, totalChecked, requestsIssued, emailsQueued, skipped, failed);

        if (errors.Count > 0)
        {
            logger.LogWarning(
                "JOB-02 [ExecutionId: {ExecutionId}] Completed with {FailedCount} errors: {Errors}",
                executionId, failed, string.Join("; ", errors));
        }

        return new T30PaymentRequestResult(
            ExecutionId: executionId,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            TotalProjectsChecked: totalChecked,
            TotalRequestsIssued: requestsIssued,
            TotalEmailsQueued: emailsQueued,
            TotalSkipped: skipped,
            TotalFailed: failed,
            Errors: errors);
    }
}
