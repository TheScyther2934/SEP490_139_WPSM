using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Jobs;
using Infrastructure.Persistence;
using Xunit;

namespace Tests;

/// <summary>An email only written to a file (no SMTP server) is "Saved", never "Sent".</summary>
public sealed class EmailDispatchJobTests : IDisposable
{
    private readonly WpsmDbContext _db = new(new DbContextOptionsBuilder<WpsmDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    private sealed class FakeSender(bool delivers) : IEmailSender
    {
        public bool DeliversEmail => delivers;
        public Task<string> SendAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default) => Task.FromResult("msg-1");
    }

    [Theory]
    [InlineData(true, "Sent")]
    [InlineData(false, "Saved")]
    public async Task Dispatch_MarksTheEmailByWhetherItReallyLeft(bool delivers, string expected)
    {
        new EmailOutbox(_db).Enqueue("khach@example.com", "Tài khoản WPSM", "<p>Mat khau: Abc123xyz</p>", "Credentials", null);
        await _db.SaveChangesAsync();

        await new EmailDispatchJob(_db, new FakeSender(delivers), NullLogger<EmailDispatchJob>.Instance).RunAsync(CancellationToken.None);

        var email = _db.EmailOutbox.Single();
        Assert.Equal(expected, email.Status);
        // The temporary password never stays readable in the outbox, sent or only saved.
        Assert.DoesNotContain("Abc123xyz", email.BodyHtml);
    }
}
