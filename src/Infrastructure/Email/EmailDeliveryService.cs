using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Persistence;

namespace WPSM.Infrastructure.Email;

public sealed class EmailDeliveryService(WpsmDbContext db, EmailOptions options) : IEmailDeliveryService
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.WebhookSecret);

    public bool VerifySignature(byte[] body, string? signature)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(signature)) return false;
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.WebhookSecret!), body));
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(signature.Trim().ToUpperInvariant()));
    }

    public async Task<bool> ApplyStatusAsync(string messageId, string status, string? error, CancellationToken cancellationToken = default)
    {
        if (status is not ("Delivered" or "Failed")) throw new InvalidOperationException("Trạng thái gửi email không hợp lệ.");
        var email = await db.EmailOutbox.FirstOrDefaultAsync(x => x.ProviderMessageId == messageId, cancellationToken);
        if (email is null) return false;
        if (status == "Delivered")
        {
            email.Status = "Sent";
            email.DeliveredAt ??= DateTime.UtcNow;
        }
        else
        {
            email.Status = "Failed";
            email.LastError = error is { Length: > 300 } ? error[..300] : error;
        }
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
