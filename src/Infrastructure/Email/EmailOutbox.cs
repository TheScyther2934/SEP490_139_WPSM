using Application.Persistence;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Email;

public sealed class EmailOutbox(WpsmDbContext db) : IEmailOutbox
{
    public void Enqueue(string toEmail, string subject, string bodyHtml, string kind, int? projectId)
    {
        var now = DateTime.UtcNow;
        db.EmailOutbox.Add(new EmailOutboxMessage
        {
            ToEmail = toEmail,
            Subject = subject,
            BodyHtml = bodyHtml,
            Kind = kind,
            Status = "Queued",
            NextAttemptAt = now,
            RelatedProjectId = projectId,
            CreatedAt = now
        });
    }
}
