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
        // Every locked event whose day has passed (catches up if the job missed a day).
        var candidateProjects = await db.WeddingProjects
            .Include(p => p.Customer)
            .Include(p => p.Payments)
            .Where(p => p.EventDate <= yesterday && p.Status == EventStatus.Locked)
            .OrderBy(p => p.ProjectId)
            .ToListAsync(cancellationToken);

        int totalFound = candidateProjects.Count, eventsCompleted = 0, settlementsPrepared = 0, finalMilestonesCreated = 0;
        int emailsQueued = 0, notificationsCreated = 0, skipped = 0, failed = 0;
        var errors = new List<string>();
        var baseUrl = emailOptions.PublicBaseUrl?.TrimEnd('/') ?? "http://localhost:5173";
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);

        foreach (var project in candidateProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Stage 5: the event is held, the final invoice fixes what is left to pay.
                project.Status = EventStatus.Completed;
                eventsCompleted++;
                var hadSettlement = project.Payments.Any(p => p.PaymentType == PaymentKinds.Settlement);
                var invoice = await EventFlowSupport.IssueInvoiceAsync(db, project, null, policy, cancellationToken);
                if (!hadSettlement) finalMilestonesCreated++;
                await db.SaveChangesAsync(cancellationToken);
                settlementsPrepared++;

                var amountDue = invoice.ContractTotal + invoice.AddonTotal - invoice.DepositPaid;
                var code = Uri.EscapeDataString(project.ProjectCode);
                var message = $"[{project.ProjectCode}] Hóa đơn tất toán {invoice.InvoiceCode}: cần thu {EventStageRules.Vnd(amountDue)}, hạn {invoice.DueDate:dd/MM/yyyy}.";
                foreach (var operatorId in await EventFlowSupport.OperatorIdsAsync(db, project.ProjectId, cancellationToken))
                {
                    await notificationRepository.CreateNotificationAsync(operatorId, NotificationTypes.Payment, message,
                        $"/manager/projects/{code}/payments", "Payment", invoice.InvoiceCode, cancellationToken);
                    notificationsCreated++;
                }
                await notificationRepository.CreateNotificationAsync(project.CustomerId, NotificationTypes.Payment,
                    $"Sự kiện {project.ProjectCode} đã hoàn tất. Hóa đơn tất toán {EventStageRules.Vnd(amountDue)}, hạn {invoice.DueDate:dd/MM/yyyy}.",
                    "/customer/payments", "Payment", invoice.InvoiceCode, cancellationToken);
                notificationsCreated++;

                try
                {
                    emailOutbox.Enqueue(project.Customer.Email, $"Hóa đơn tất toán sự kiện {project.ProjectCode}",
                        EmailTemplates.SettlementInvoice(project.Customer.FullName, project.ProjectCode, invoice.ContractTotal, invoice.AddonTotal,
                            invoice.DepositPaid, amountDue, invoice.DueDate, $"{baseUrl}/customer/payments"), "PaymentRequest", project.ProjectId);
                    await db.SaveChangesAsync(cancellationToken);
                    emailsQueued++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Could not queue the settlement invoice email of {ProjectCode}; the invoice is saved.", project.ProjectCode);
                }
            }
            catch (DbUpdateException dbEx)
            {
                logger.LogWarning(dbEx, "Database conflict while preparing the settlement of {ProjectCode}. Skipping.", project.ProjectCode);
                skipped++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                errors.Add($"Project {project.ProjectCode} error: {ex.Message}");
                logger.LogError(ex, "Unexpected error preparing the settlement of {ProjectCode}.", project.ProjectCode);
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
