using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Application.Persistence;
using Infrastructure.Persistence;

namespace Infrastructure.Jobs;

/// <summary>
/// Weekly: removes files the storage holds but no table refers to (an upload whose database write failed, a test run).
/// Files referred to are never touched: proof photos and receipts are kept forever. Only files older than a day are
/// considered, so an upload in progress (stored, database row not yet saved) is never removed.
/// </summary>
public sealed class FileCleanupJob(WpsmDbContext db, IFileStorage storage, ILogger<FileCleanupJob> logger)
{
    private static readonly TimeSpan MinimumAge = TimeSpan.FromDays(1);

    [DisableConcurrentExecution(3600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        known.UnionWith(await db.StoredFiles.AsNoTracking().Select(x => x.StorageKey).ToListAsync(cancellationToken));
        known.UnionWith(await db.ExecutionPhotos.AsNoTracking().Select(x => x.ImageFilePath).ToListAsync(cancellationToken));
        known.UnionWith(await db.Payments.AsNoTracking().Where(x => x.ReceiptFilePath != null).Select(x => x.ReceiptFilePath!).ToListAsync(cancellationToken));

        var cutoff = DateTime.UtcNow - MinimumAge;
        var orphans = new List<string>();
        await foreach (var item in storage.ListAsync(cancellationToken))
            if (item.CreatedAt < cutoff && !known.Contains(item.Key)) orphans.Add(item.Key);

        var removed = 0;
        foreach (var key in orphans)
        {
            try
            {
                await storage.DeleteAsync(key, cancellationToken);
                removed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not remove orphan file {Key}.", key);
            }
        }
        logger.LogInformation("File cleanup: {Known} files referenced, {Removed} orphan files removed.", known.Count, removed);
        return removed;
    }
}
