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
/// Implements JOB-06: Calculate Final Settlement according to Report 3 (SRS) &amp; Report 4 (SDS).
/// Automatically queries Wedding Projects whose event was held yesterday,
/// transitions them to Event Completed, calculates final settlement balances via BalanceCalculator,
/// creates or updates the FINAL payment milestone with Status = Due,
/// and notifies the assigned Manager for settlement verification.
/// </summary>
public sealed class FinalSettlementService(
    WpsmDbContext db,
    INotificationRepository notificationRepository,
    IEmailOutbox emailOutbox,
    EmailOptions emailOptions,
    ILogger<FinalSettlementService> logger) : IFinalSettlementService
{
    private static readonly HashSet<string> InactiveProjectStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Closed", "Settled", "Cancelled"
    };

    public async Task<FinalSettlementResult> PrepareFinalSettlementsAsync(
        PrepareFinalSettlementCommand? command = null,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;
        var today = command?.TargetDate ?? PaymentCalculator.GetVietnamToday();
        var yesterday = today.AddDays(-1);

        logger.LogInformation(
            "JOB-06 [ExecutionId: {ExecutionId}] Checking projects with EventDate == {Yesterday:yyyy-MM-dd} (Today: {Today:yyyy-MM-dd}).",
            executionId, yesterday, today);

        // Query projects whose event was yesterday, baseline frozen, active
        var candidateProjects = await db.WeddingProjects
            .Include(p => p.Customer)
            .Include(p => p.Manager)
            .Include(p => p.Payments)
            .Include(p => p.ChangeRequests)
            .Where(p => p.EventDate == yesterday
                     && !InactiveProjectStatuses.Contains(p.Status)
                     && p.BaselineFrozenAt != null
                     && p.BaseContractValue > 0)
            .OrderBy(p => p.ProjectId)
            .ToListAsync(cancellationToken);

        int totalFound = candidateProjects.Count;
        int eventsCompleted = 0;
        int settlementsPrepared = 0;
        int finalMilestonesCreated = 0;
        int emailsQueued = 0;
        int notificationsCreated = 0;
        int skipped = 0;
        int failed = 0;
        var errors = new List<string>();

        var baseUrl = emailOptions.PublicBaseUrl?.TrimEnd('/') ?? "http://localhost:5173";

        foreach (var project in candidateProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 1. Validate Manager account
                var manager = project.Manager;
                if (manager == null ||
                    manager.UserId != project.ManagerId ||
                    !string.Equals(manager.Role, "Manager", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(manager.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Project {ProjectCode} has invalid assigned manager (ManagerId: {ManagerId}). Skipping.",
                        project.ProjectCode, project.ManagerId);
                    skipped++;
                    continue;
                }

                // 2. Mark Event Completed (SDS IV.5 / Report 4)
                // Note: Only mark Event Completed; NEVER mark Settled in JOB-06.
                if (!string.Equals(project.Status, "Event Completed", StringComparison.OrdinalIgnoreCase))
                {
                    project.Status = "Event Completed";
                    eventsCompleted++;
                }

                // 3. Final Settlement Calculation using BalanceCalculator
                var crTuples = project.ChangeRequests.Select(c => (c.Status, c.TotalVariance, c.CostImpact, c.PenaltyFee));
                var approvedCrVariance = PaymentCalculator.CalculateApprovedCrVariance(crTuples);
                var currentTotal = PaymentCalculator.CalculateCurrentTotal(project.BaseContractValue, approvedCrVariance);

                var paymentTuples = project.Payments.Select(p => (p.Status, p.Amount));
                var totalPaid = PaymentCalculator.CalculateTotalPaid(paymentTuples);
                var outstandingBalance = PaymentCalculator.CalculateOutstandingBalance(currentTotal, totalPaid);
                var finalAmountDue = Math.Round(outstandingBalance, 0, MidpointRounding.AwayFromZero);

                project.CurrentTotal = currentTotal;

                if (currentTotal - totalPaid < 0)
                {
                    logger.LogWarning(
                        "Project {ProjectCode} has negative balance / overpayment ({Balance} VND). Manager verification required.",
                        project.ProjectCode, currentTotal - totalPaid);
                }

                // 4. Create or Update FINAL Payment Milestone
                var finalPayment = project.Payments.FirstOrDefault(p =>
                    string.Equals(p.PaymentType, "FINAL", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.PaymentType, "Final", StringComparison.OrdinalIgnoreCase));

                if (finalAmountDue > 0)
                {
                    if (finalPayment == null)
                    {
                        finalPayment = new Payment
                        {
                            ProjectId = project.ProjectId,
                            PaymentType = "FINAL",
                            Status = "Due",
                            DueDate = today,
                            Amount = finalAmountDue
                        };
                        db.Payments.Add(finalPayment);
                        finalMilestonesCreated++;
                    }
                    else if (!string.Equals(finalPayment.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                    {
                        finalPayment.Amount = finalAmountDue;
                        finalPayment.Status = "Due";
                        finalPayment.DueDate = today;
                    }
                }
                else
                {
                    logger.LogInformation(
                        "Project {ProjectCode} final amount due is {Amount} VND. Not creating new non-positive payment record.",
                        project.ProjectCode, finalAmountDue);
                }

                await db.SaveChangesAsync(cancellationToken);
                settlementsPrepared++;

                // 5. Manager Notification (G-04)
                var encodedCode = Uri.EscapeDataString(project.ProjectCode);
                var customerName = project.Customer?.FullName ?? "Khách hàng";
                var settlementUrl = $"{baseUrl}/manager/projects/{encodedCode}/payments";
                var managerLink = $"/manager/projects/{encodedCode}/payments";

                var marker = $"[{project.ProjectCode}] Quyết toán sự kiện";
                var todayUtc = DateTime.UtcNow.Date;
                var alreadyNotified = await db.Notifications.AsNoTracking().AnyAsync(n =>
                    n.RecipientId == manager.UserId &&
                    n.Type == NotificationTypes.Payment &&
                    n.Message.Contains(marker) &&
                    n.CreatedAt >= todayUtc, cancellationToken);

                if (!alreadyNotified)
                {
                    var managerMsg = $"[{project.ProjectCode}] Quyết toán sự kiện ({customerName} - {project.EventDate:dd/MM/yyyy}): Tổng HĐ {currentTotal:N0} VND, đã thu {totalPaid:N0} VND, còn lại {finalAmountDue:N0} VND. Vui lòng kiểm tra và hoàn tất quyết toán.";

                    await notificationRepository.CreateNotificationAsync(
                        recipientId: manager.UserId,
                        type: NotificationTypes.Payment,
                        message: managerMsg,
                        linkUrl: managerLink,
                        relatedEntityType: "Payment",
                        relatedEntityId: finalPayment?.PaymentId.ToString(),
                        cancellationToken: cancellationToken);

                    notificationsCreated++;
                }

                // 6. Queue Email via JOB-07 (Isolated error handling)
                try
                {
                    var emailSubject = $"[{project.ProjectCode}] Chuẩn bị quyết toán sự kiện cưới (Final Settlement)";
                    var emailBody = EmailTemplates.FinalSettlementPreparation(
                        managerName: manager.FullName,
                        projectCode: project.ProjectCode,
                        customerName: customerName,
                        eventDate: project.EventDate,
                        baseContractValue: project.BaseContractValue,
                        approvedCrVariance: approvedCrVariance,
                        currentTotal: currentTotal,
                        totalPaid: totalPaid,
                        finalAmountDue: finalAmountDue,
                        settlementUrl: settlementUrl);

                    emailOutbox.Enqueue(
                        toEmail: manager.Email,
                        subject: emailSubject,
                        bodyHtml: emailBody,
                        kind: "PaymentRequest",
                        projectId: project.ProjectId);

                    emailsQueued++;
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to queue settlement preparation email for project {ProjectCode} to {Email}. Settlement preparation was successfully saved.",
                        project.ProjectCode, manager.Email);
                }
            }
            catch (DbUpdateException dbEx)
            {
                logger.LogWarning(dbEx, "Concurrency collision or DB constraint violation while preparing settlement for {ProjectCode}. Skipping.", project.ProjectCode);
                skipped++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                var errorMsg = $"Project {project.ProjectCode} error: {ex.Message}";
                errors.Add(errorMsg);
                logger.LogError(ex, "Unexpected error preparing settlement for project {ProjectCode}.", project.ProjectCode);
            }
        }

        var completedAt = DateTime.UtcNow;

        logger.LogInformation(
            "JOB-06 [ExecutionId: {ExecutionId}] Summary - StartedAt: {StartedAt:O}, CompletedAt: {CompletedAt:O}, TotalProjectsFound: {TotalProjectsFound}, TotalEventsCompleted: {TotalEventsCompleted}, TotalSettlementsPrepared: {TotalSettlementsPrepared}, TotalFinalMilestonesCreated: {TotalFinalMilestonesCreated}, TotalEmailsQueued: {TotalEmailsQueued}, TotalNotificationsCreated: {TotalNotificationsCreated}, TotalProjectsSkipped: {TotalProjectsSkipped}, TotalFailed: {TotalFailed}",
            executionId, startedAt, completedAt, totalFound, eventsCompleted, settlementsPrepared, finalMilestonesCreated, emailsQueued, notificationsCreated, skipped, failed);

        if (errors.Count > 0)
        {
            logger.LogWarning(
                "JOB-06 [ExecutionId: {ExecutionId}] Completed with {FailedCount} errors: {Errors}",
                executionId, failed, string.Join("; ", errors));
        }

        return new FinalSettlementResult(
            ExecutionId: executionId,
            StartedAt: startedAt,
            CompletedAt: completedAt,
            TotalProjectsFound: totalFound,
            TotalEventsCompleted: eventsCompleted,
            TotalSettlementsPrepared: settlementsPrepared,
            TotalFinalMilestonesCreated: finalMilestonesCreated,
            TotalEmailsQueued: emailsQueued,
            TotalNotificationsCreated: notificationsCreated,
            TotalProjectsSkipped: skipped,
            TotalFailed: failed,
            Errors: errors);
    }
}
