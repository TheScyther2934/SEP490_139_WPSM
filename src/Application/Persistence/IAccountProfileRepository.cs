namespace Application.Persistence;

public interface IAccountProfileRepository
{
    Task<AccountView?> FindAccountAsync(string email, CancellationToken cancellationToken = default);
    Task<AccountView?> FindAccountByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<AccountView?> FindByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<ProfileView?> GetProfileAsync(int userId, CancellationToken cancellationToken = default);
    Task<ProfileView?> UpdateProfileAsync(int userId, ProfileCommand command, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default);
    Task<bool> SaveRefreshTokenAsync(int userId, string refreshToken, DateTime expiryTime, CancellationToken cancellationToken = default);
    Task<bool> RevokeRefreshTokenAsync(int userId, CancellationToken cancellationToken = default);
}
