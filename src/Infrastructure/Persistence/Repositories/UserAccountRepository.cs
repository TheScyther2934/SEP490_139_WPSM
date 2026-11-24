using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>A-19 account list (read). Creating, editing, locking and reissuing passwords are use cases: Application/UseCases/Accounts.</summary>
public sealed class UserAccountRepository(WpsmDbContext db) : IUserAccountRepository
{
    private static readonly string[] ClosedStatuses = ["Settled", "Closed"];

    public async Task<IReadOnlyList<UserAccountView>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
        await Views(db.Users.OrderBy(x => x.UserId)).ToListAsync(cancellationToken);

    public Task<UserAccountView?> GetAccountAsync(int userId, CancellationToken cancellationToken = default) =>
        Views(db.Users.Where(x => x.UserId == userId)).SingleOrDefaultAsync(cancellationToken);

    // Filter/order on Users before this projection: EF cannot translate OrderBy/Where over the projected record.
    private IQueryable<UserAccountView> Views(IQueryable<User>? source = null) =>
        (source ?? db.Users).AsNoTracking().Select(x => new UserAccountView(x.UserId, x.Email, x.FullName, x.Phone ?? "", x.Role, x.Status == "Active",
            x.MustChangePassword, x.CreatedAt,
            db.WeddingProjects.Count(p => (p.CustomerId == x.UserId || (p.ManagerId == x.UserId || p.ProjectManagers.Any(pm => pm.ManagerId == x.UserId && pm.RemovedAt == null))) && !ClosedStatuses.Contains(p.Status))));
}
