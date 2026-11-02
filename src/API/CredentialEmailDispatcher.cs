using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;

namespace API;

public sealed record CredentialEmailPayload(string Name, string Password);

public sealed class CredentialEmailDispatcher(IServiceScopeFactory scopes, ILogger<CredentialEmailDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<WpsmDbContext>();
                var sender = scope.ServiceProvider.GetRequiredService<CredentialEmailSender>();
                var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("WPSM.CredentialEmail.v1");
                var now = DateTime.UtcNow;
                var pending = await db.EmailDeliveries.Where(e => e.Status == "Pending" && e.NextAttemptAt <= now)
                    .OrderBy(e => e.CreatedAt).Take(20).ToListAsync(stoppingToken);
                foreach (var delivery in pending)
                {
                    string status;
                    try
                    {
                        var payload = JsonSerializer.Deserialize<CredentialEmailPayload>(protector.Unprotect(delivery.ProtectedPayload!))!;
                        status = await sender.SendAsync(delivery.Recipient, payload.Name, payload.Password);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to decrypt or send credential email for delivery {Id}.", delivery.EmailDeliveryId);
                        status = "Failed";
                    }

                    delivery.Attempts++;
                    delivery.Status = status == "Sent" ? "Sent" : delivery.Attempts >= 5 ? "Failed" : "Pending";
                    if (status == "Sent") delivery.SentAt = DateTime.UtcNow;
                    else delivery.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, delivery.Attempts));
                }

                if (pending.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing credential email queue.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
