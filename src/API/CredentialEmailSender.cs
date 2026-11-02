using System.Net;
using System.Net.Mail;
using System.Text;

namespace API;

public sealed class CredentialEmailSender(IConfiguration configuration, ILogger<CredentialEmailSender> logger)
{
    public async Task<string> SendAsync(string email, string name, string password)
    {
        var host = configuration["Wpsm:Email:Host"];
        if (string.IsNullOrWhiteSpace(host)) return "NotConfigured";
        try
        {
            using var message = new MailMessage(configuration["Wpsm:Email:From"] ?? "noreply@wpsm.local", email)
            {
                Subject = "Thông tin tài khoản WPSM",
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
                Body = $"Xin chào {name},\n\nTài khoản WPSM của bạn đã được tạo.\n" +
                    $"Đăng nhập: {configuration["Wpsm:PortalUrl"] ?? "http://localhost:5231/G01"}\n" +
                    $"Email: {email}\nMật khẩu tạm: {password}\n\nVui lòng đổi mật khẩu ở lần đăng nhập đầu tiên."
            };
            using var client = new SmtpClient();
            client.Host = host;
            client.Port = configuration.GetValue("Wpsm:Email:Port", 587);
            client.EnableSsl = configuration.GetValue("Wpsm:Email:EnableSsl", true);
            if (!string.IsNullOrWhiteSpace(configuration["Wpsm:Email:Username"]))
                client.Credentials = new NetworkCredential(configuration["Wpsm:Email:Username"], configuration["Wpsm:Email:Password"]);
            await client.SendMailAsync(message);
            return "Sent";
        }
        catch (Exception ex) when (ex is SmtpException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            logger.LogWarning("Credential email could not be delivered ({ErrorType}).", ex.GetType().Name);
            return "Failed";
        }
    }
}
