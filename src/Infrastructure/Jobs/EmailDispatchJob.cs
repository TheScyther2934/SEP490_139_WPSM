using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Persistence;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.Infrastructure.Jobs;

/// <summary>JOB-07: sends queued emails every minute; 3 retries at 5-minute intervals.</summary>
public sealed class EmailDispatchJob(WpsmDbContext db, IEmailSender sender, ILogger<EmailDispatchJob> logger)
{
    private const int MaxRetries = 3;
    private const int BatchSize = 50;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

    [DisableConcurrentExecution(55)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var due = await db.EmailOutbox.Where(x => x.Status == "Queued" && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt).Take(BatchSize).ToListAsync(cancellationToken);
        foreach (var email in due)
        {
            try
            {
                email.ProviderMessageId = await sender.SendAsync(email.ToEmail, email.Subject, email.BodyHtml, cancellationToken);
                email.Status = "Sent";
                email.SentAt = DateTime.UtcNow;
                email.LastError = null;
                Redact(email);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Email {EmailId} failed on attempt {Attempt}.", email.EmailId, email.RetryCount + 1);
                email.LastError = ex.Message is { Length: > 300 } message ? message[..300] : ex.Message;
                email.RetryCount++;
                if (email.RetryCount > MaxRetries)
                {
                    email.Status = "Failed";
                    Redact(email);
                }
                else
                {
                    email.NextAttemptAt = DateTime.UtcNow.Add(RetryInterval);
                }
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // A temporary password must not stay readable in the outbox once the email is finished.
    private static void Redact(EmailOutboxMessage email)
    {
        if (email.Kind == "Credentials") email.BodyHtml = "[Đã xóa nội dung chứa mật khẩu tạm]";
    }
}
