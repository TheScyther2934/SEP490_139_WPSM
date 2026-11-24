using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class AccountProfileRepository(WpsmDbContext db) : IAccountProfileRepository
{
    public async Task<AccountView?> FindAccountAsync(string email, CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking().Where(x => x.Email == email.Trim())
            .Select(x => new AccountView(x.UserId, x.Email, x.FullName, x.Phone, x.Role, x.Status, x.PasswordHash, x.MustChangePassword))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AccountView?> FindAccountByIdAsync(int userId, CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new AccountView(x.UserId, x.Email, x.FullName, x.Phone, x.Role, x.Status, x.PasswordHash, x.MustChangePassword))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ProfileView?> GetProfileAsync(int userId, CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new ProfileView(x.FullName, x.Email, x.Phone ?? "", x.Role))
            .FirstOrDefaultAsync(cancellationToken);
}
