using Application.Persistence;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace Infrastructure.Email;

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

/// <summary>
/// API-03 through MailKit (System.Net.Mail.SmtpClient skipped AUTH against Brevo and is not recommended for new code).
/// With Smtp:Host it sends over SMTP (STARTTLS on 587, implicit TLS on 465); without it, Development writes .eml files.
/// </summary>
public sealed class SmtpEmailSender(EmailOptions options) : IEmailSender
{
    public async Task<string> SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default)
    {
        var messageId = $"{Guid.NewGuid():N}@wpsm";
        var message = new MimeMessage { Subject = subject, MessageId = messageId, Body = new TextPart(TextFormat.Html) { Text = bodyHtml } };
        message.From.Add(MailboxAddress.Parse(options.From));
        message.To.Add(MailboxAddress.Parse(toEmail));

        if (!string.IsNullOrWhiteSpace(options.Host))
        {
            using var client = new SmtpClient { Timeout = 30_000 };
            var security = !options.EnableSsl ? SecureSocketOptions.None
                : options.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(options.Host, options.Port, security, cancellationToken);
            if (!string.IsNullOrWhiteSpace(options.User))
                await client.AuthenticateAsync(options.User, options.Password ?? "", cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(options.PickupDirectory))
        {
            Directory.CreateDirectory(options.PickupDirectory);
            await message.WriteToAsync(Path.Combine(options.PickupDirectory, $"{Guid.NewGuid()}.eml"), cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("Chưa cấu hình Smtp:Host.");
        }
        return messageId;
    }
}
