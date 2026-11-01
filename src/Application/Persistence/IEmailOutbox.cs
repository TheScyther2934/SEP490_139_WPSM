namespace WPSM.Application.Persistence;

public interface IEmailOutbox
{
    /// <summary>Adds the row to the current unit of work so it commits with the business change; JOB-07 sends it.</summary>
    void Enqueue(string toEmail, string subject, string bodyHtml, string kind, int? projectId);
}

public interface IEmailSender
{
    /// <summary>API-03. Returns the provider message id matched by the API-04 callback.</summary>
    Task<string> SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default);
}

public interface IEmailDeliveryService
{
    bool IsConfigured { get; }
    bool VerifySignature(byte[] body, string? signature);
    /// <summary>API-04. Returns false when no email matches the message id.</summary>
    Task<bool> ApplyStatusAsync(string messageId, string status, string? error, CancellationToken cancellationToken = default);
}
