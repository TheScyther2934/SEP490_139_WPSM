namespace Application.Persistence;

public interface IAccountProfileRepository
{
    Task<AccountView?> FindAccountAsync(string email, CancellationToken cancellationToken = default);
    Task<AccountView?> FindAccountByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<ProfileView?> GetProfileAsync(int userId, CancellationToken cancellationToken = default);
}
