namespace WPSM.Application.Persistence;

public interface IAccountProfileRepository
{
    Task<AccountView?> FindAccountAsync(string email, CancellationToken cancellationToken = default);
    Task<ProfileView?> GetProfileAsync(int userId, CancellationToken cancellationToken = default);
    Task<ProfileView?> UpdateProfileAsync(int userId, ProfileCommand command, CancellationToken cancellationToken = default);
    Task<bool> UpdatePasswordAsync(int userId, string passwordHash, CancellationToken cancellationToken = default);
}
