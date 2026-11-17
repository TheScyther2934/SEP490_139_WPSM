namespace Application.Persistence;

/// <summary>A-19 account management. OpenProjects counts projects not yet Settled/Closed that the user owns or manages.</summary>
public sealed record UserAccountView(int Id, string Email, string FullName, string Phone, string Role, bool Active, bool MustChangePassword,
    DateTime CreatedAt, int OpenProjects);
public sealed record UserAccountCommand(string FullName, string Email, string? Phone, string Role);
public sealed record CreatedUserAccount(UserAccountView Account, string TemporaryPassword);

public interface IUserAccountRepository
{
    Task<IReadOnlyList<UserAccountView>> GetAccountsAsync(CancellationToken cancellationToken = default);
    /// <summary>Creates a Manager or Admin with a system password sent through the outbox (Customer accounts come from M-03).</summary>
    Task<CreatedUserAccount> CreateAccountAsync(UserAccountCommand command, string passwordHash, string temporaryPassword, CancellationToken cancellationToken = default);
    Task<UserAccountView?> UpdateAccountAsync(int userId, UserAccountCommand command, int adminId, CancellationToken cancellationToken = default);
    /// <summary>Accounts are never deleted (SRS): deactivation keeps data and revokes every refresh token (SDS).</summary>
    Task<UserAccountView?> SetStatusAsync(int userId, bool active, int adminId, CancellationToken cancellationToken = default);
    Task<UserAccountView?> ResetPasswordAsync(int userId, string passwordHash, string temporaryPassword, CancellationToken cancellationToken = default);
}
