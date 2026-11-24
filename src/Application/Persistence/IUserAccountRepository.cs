namespace Application.Persistence;

/// <summary>A-19 account management. OpenProjects counts projects not yet Settled/Closed that the user owns or manages.</summary>
public sealed record UserAccountView(int Id, string Email, string FullName, string Phone, string Role, bool Active, bool MustChangePassword,
    DateTime CreatedAt, int OpenProjects);
public sealed record UserAccountCommand(string FullName, string Email, string? Phone, string Role);
public sealed record CreatedUserAccount(UserAccountView Account, string TemporaryPassword);

public interface IUserAccountRepository
{
    Task<IReadOnlyList<UserAccountView>> GetAccountsAsync(CancellationToken cancellationToken = default);
    Task<UserAccountView?> GetAccountAsync(int userId, CancellationToken cancellationToken = default);
    // Changes are use cases: Application/UseCases/Accounts.
}
