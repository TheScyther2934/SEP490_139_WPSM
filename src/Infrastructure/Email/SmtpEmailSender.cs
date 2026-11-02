using System.Net;
using System.Net.Mail;
using System.Text;
using WPSM.Application.Persistence;

namespace WPSM.Infrastructure.Email;

public sealed class EmailOptions
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? User { get; init; }
    public string? Password { get; init; }
    public bool EnableSsl { get; init; } = true;
    public string From { get; init; } = "no-reply@wpsm.local";
    public string? PickupDirectory { get; init; }
    public string? WebhookSecret { get; init; }
    public string PublicBaseUrl { get; init; } = "http://localhost:5231";
}

public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public async Task<string> SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient();
        if (!string.IsNullOrWhiteSpace(options.Host))
        {
            client.Host = options.Host;
            client.Port = options.Port;
            client.EnableSsl = options.EnableSsl;
            if (!string.IsNullOrWhiteSpace(options.User)) client.Credentials = new NetworkCredential(options.User, options.Password);
        }
        else if (!string.IsNullOrWhiteSpace(options.PickupDirectory))
        {
            Directory.CreateDirectory(options.PickupDirectory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = options.PickupDirectory;
        }
        else
        {
            throw new InvalidOperationException("Chưa cấu hình Smtp:Host.");
        }

        var messageId = $"{Guid.NewGuid():N}@wpsm";
        using var message = new MailMessage(options.From, toEmail)
        {
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = bodyHtml,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        message.Headers.Add("Message-ID", $"<{messageId}>");
        await client.SendMailAsync(message, cancellationToken);
        return messageId;
    }
}
