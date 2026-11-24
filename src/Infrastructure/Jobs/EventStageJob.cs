using Application.UseCases.EventStages;
using Domain.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

/// <summary>
/// Daily stage job: locks every adjusting event on its lock date (customers can no longer change services) and reminds the
/// operators of locked events that passed the supplier deadline without evidence.
/// </summary>
public sealed class EventStageJob(LockDueEvents lockDueEvents, RemindSupplierDeadlines remindSupplierDeadlines, ILogger<EventStageJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var today = VietnamTime.GetVietnamToday();
        var locked = await lockDueEvents.HandleAsync(today, cancellationToken);
        var reminded = await remindSupplierDeadlines.HandleAsync(today, cancellationToken);
        logger.LogInformation("Event stage job: locked {Locked} event(s), sent {Reminded} supplier deadline reminder(s).", locked, reminded);
    }
}
