using Hangfire;
using Microsoft.Extensions.Logging;
using Application.Persistence;

namespace Infrastructure.Jobs;

/// <summary>JOB-03 (BF-04): Compiles draft BEO at T-7 from frozen baseline and approved addenda.</summary>
public sealed class BeoCompilationJob(IBeoRepository beos, ILogger<BeoCompilationJob> logger)
{
    [DisableConcurrentExecution(300)]
    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var count = await beos.CompileT7DraftsAsync(cancellationToken);
        if (count > 0)
            logger.LogInformation("JOB-03 compiled {Count} T-7 BEO draft(s).", count);
    }
}
