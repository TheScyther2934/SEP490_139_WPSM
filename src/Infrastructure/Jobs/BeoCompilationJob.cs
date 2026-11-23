using Hangfire;
using Microsoft.Extensions.Logging;
using Application.Persistence;

namespace Infrastructure.Jobs;

/// <summary>Daily job: compiles the coordination sheet draft of events locked in stage 3.</summary>
public sealed class BeoCompilationJob(IBeoRepository beos, ILogger<BeoCompilationJob> logger)
{
    [DisableConcurrentExecution(300)]
    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var count = await beos.CompileLockedDraftsAsync(cancellationToken);
        if (count > 0)
            logger.LogInformation("Compiled {Count} coordination sheet draft(s) for locked events.", count);
    }
}
