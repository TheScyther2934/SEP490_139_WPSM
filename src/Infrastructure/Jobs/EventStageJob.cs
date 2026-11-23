using Application.Persistence;
using Domain.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

/// <summary>
/// Daily stage job: locks every adjusting event on its lock date (customers can no longer change services) and reminds the
/// operators of locked events that passed the supplier deadline without evidence.
/// </summary>
public sealed class EventStageJob(IEventFlowRepository events, ILogger<EventStageJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var today = VietnamTime.GetVietnamToday();
        var locked = await events.LockDueEventsAsync(today, cancellationToken);
        var reminded = await events.RemindSupplierDeadlinesAsync(today, cancellationToken);
        logger.LogInformation("Event stage job: locked {Locked} event(s), sent {Reminded} supplier deadline reminder(s).", locked, reminded);
    }
}
