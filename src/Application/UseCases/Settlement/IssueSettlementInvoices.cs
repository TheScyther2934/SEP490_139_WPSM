using Application.Payments;
using Domain.Services;

namespace Application.UseCases.Settlement;

/// <summary>
/// Use case (JOB-06, daily): every locked event whose day has passed is held (stage 5) and gets its settlement invoice: contract after
/// adjustments + confirmed addon contracts - deposit, due SettlementDueDays after the event. Operators and customer are told; the customer
/// also gets the invoice by email. Each event commits on its own, so one failure does not block the others; an email outage never undoes
/// an invoice.
/// </summary>
public sealed class IssueSettlementInvoices(ISettlementStore store, IEventMessages messages, IUnitOfWork unitOfWork) : IFinalSettlementService
{
    public async Task<FinalSettlementResult> PrepareFinalSettlementsAsync(PrepareFinalSettlementCommand? command = null, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var today = command?.TargetDate ?? VietnamTime.GetVietnamToday();
        var events = await store.GetLockedEventsHeldUntilAsync(today.AddDays(-1), cancellationToken);
        int completed = 0, prepared = 0, milestones = 0, emails = 0, notifications = 0, failed = 0;
        var errors = new List<string>();

        foreach (var project in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                project.MarkHeld();
                completed++;
                var hadSettlement = project.Payments.Any(p => p.PaymentType == PaymentKinds.Settlement);
                var invoice = await store.StageInvoiceAsync(project, null, cancellationToken);
                if (!hadSettlement) milestones++;
                var amountDue = invoice.ContractTotal + invoice.AddonTotal - invoice.DepositPaid;
                var operators = await store.GetOperatorIdsAsync(project.ProjectId, cancellationToken);
                await messages.SettlementInvoiceIssuedAsync(operators, project.CustomerId, project.ProjectCode, invoice.InvoiceCode, amountDue, invoice.DueDate,
                    cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                prepared++;
                notifications += operators.Count + 1;

                try
                {
                    await messages.SettlementInvoiceEmailAsync(project, invoice, amountDue, cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    emails++;
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested) { /* the invoice is saved; JOB-07 is not involved */ }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                errors.Add($"Project {project.ProjectCode} error: {ex.Message}");
            }
        }

        return new FinalSettlementResult(Guid.NewGuid(), startedAt, DateTime.UtcNow, events.Count, completed, prepared, milestones, emails, notifications,
            0, failed, errors);
    }
}
