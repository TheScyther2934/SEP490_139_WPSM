using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using WPSM.Infrastructure.Persistence;

namespace WPSM.API;

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
                    catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or ArgumentException)
                    {
                        delivery.Status = "Failed";
                        await db.SaveChangesAsync(stoppingToken);
                        continue;
                    }
                    if (status == "NotConfigured")
                    {
                        delivery.NextAttemptAt = now.AddMinutes(1);
                    }
                    else
                    {
                        delivery.Attempts++;
                        delivery.NextAttemptAt = now.AddMinutes(5);
                        if (status == "Sent")
                        {
                            delivery.Status = "Sent";
                            delivery.SentAt = DateTime.UtcNow;
                            delivery.ProtectedPayload = null;
                        }
                        else if (delivery.Attempts >= 4) delivery.Status = "Failed";
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Email dispatch deferred ({ErrorType}).", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
