using Microsoft.EntityFrameworkCore;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Persistence;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.Infrastructure.Persistence.Repositories;

public sealed class AccountProfileRepository(WpsmDbContext db) : IAccountProfileRepository
{
    public async Task<AccountView?> FindAccountAsync(string email, CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking().Where(x => x.Email == email.Trim())
            .Select(x => new AccountView(x.UserId, x.Email, x.FullName, x.Phone, x.Role, x.Status, x.PasswordHash, x.MustChangePassword))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ProfileView?> GetProfileAsync(int userId, CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking().Where(x => x.UserId == userId)
            .Select(x => new ProfileView(x.FullName, x.Email, x.Phone ?? "", x.Role))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ProfileView?> UpdateProfileAsync(int userId, ProfileCommand command, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (user is null) return null;
        if (string.IsNullOrWhiteSpace(command.FullName) || command.FullName.Length > 100) throw new InvalidOperationException("Họ tên không hợp lệ.");
        user.FullName = command.FullName.Trim();
        user.Phone = WpsmValueNormalizer.NormalizePhone(command.Phone);
        await db.SaveChangesAsync(cancellationToken);
        return new ProfileView(user.FullName, user.Email, user.Phone ?? "", user.Role);
    }

    public async Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.UserId == userId && x.Status == "Active", cancellationToken);
        if (user is null) return false;
        user.PasswordHash = passwordHash;
        user.MustChangePassword = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
