using Microsoft.EntityFrameworkCore;
using Application.UseCases.Accounts;
using Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.UseCases;

/// <summary>Account data for the use cases: users, open events of a Manager, ending sessions on every device.</summary>
public sealed class AccountStore(WpsmDbContext db) : IAccountStore
{
    private static readonly string[] ClosedStatuses = ["Settled", "Closed"];

    public Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Users.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<bool> EmailTakenAsync(string email, int? exceptUserId, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(x => x.Email == email && x.UserId != exceptUserId, cancellationToken);

    public Task<int> CountOpenEventsAsync(int userId, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.CountAsync(p => (p.ManagerId == userId || p.ProjectManagers.Any(pm => pm.ManagerId == userId && pm.RemovedAt == null))
            && !ClosedStatuses.Contains(p.Status), cancellationToken);

    public void Add(User user) => db.Users.Add(user);

    // Sessions end and their refresh tokens stop working; access tokens are refused within 30 s (SessionGuard).
    public async Task StageEndSessionsAsync(int userId, string reason, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var session in await db.UserSessions.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(cancellationToken))
        {
            session.RevokedAt = now;
            session.RevokedReason = reason;
        }
        foreach (var token in await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(cancellationToken))
            token.RevokedAt = now;
    }
}
