using Application.Auth;
using Application.Persistence;
using Domain.Entities;
using Domain.Rules;

namespace Application.UseCases.Accounts;

/// <summary>Data access for the account use cases; implemented in Infrastructure. Entities come back tracked.</summary>
public interface IAccountStore
{
    Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> EmailTakenAsync(string email, int? exceptUserId, CancellationToken cancellationToken = default);
    /// <summary>Events not settled or closed that the user operates (primary or co-operator).</summary>
    Task<int> CountOpenEventsAsync(int userId, CancellationToken cancellationToken = default);
    void Add(User user);
    /// <summary>Stages the end of every session of the user on every device (their refresh tokens stop working).</summary>
    Task StageEndSessionsAsync(int userId, string reason, CancellationToken cancellationToken = default);
}

/// <summary>Emails about accounts. Wording and templates are a delivery detail (Infrastructure); queued with the use case's changes.</summary>
public interface IAccountMessages
{
    /// <summary>The temporary password of a staff account, new or reissued.</summary>
    void StaffCredentials(User user, string temporaryPassword, bool reset);
}

/// <summary>Use case (A-19): the Admin creates a Manager or Admin account; the temporary password goes out by email.</summary>
public sealed class CreateStaffAccount(IAccountStore store, IPasswordService passwords, IAccountMessages messages, IUserAccountRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<CreatedUserAccount> HandleAsync(UserAccountCommand command, CancellationToken cancellationToken = default)
    {
        var details = AccountPolicy.ValidateDetails(command.FullName, command.Email, command.Phone);
        var role = AccountPolicy.EnsureStaffRole(command.Role);
        AccountPolicy.EnsureEmailFree(await store.EmailTakenAsync(details.Email, null, cancellationToken));
        var password = passwords.NewTemporaryPassword();
        var user = User.NewStaff(details, role, passwords.Hash(password), DateTime.UtcNow);
        store.Add(user);
        messages.StaffCredentials(user, password, reset: false);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreatedUserAccount((await reads.GetAccountAsync(user.UserId, cancellationToken))!, password);
    }
}

/// <summary>Use case (A-19): the Admin edits an account's details and, between Manager and Admin, its role.</summary>
public sealed class UpdateAccount(IAccountStore store, IUserAccountRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<UserAccountView?> HandleAsync(int userId, UserAccountCommand command, int adminId, CancellationToken cancellationToken = default)
    {
        var user = await store.GetUserAsync(userId, cancellationToken);
        if (user is null) return null;
        var details = AccountPolicy.ValidateDetails(command.FullName, command.Email, command.Phone);
        var role = command.Role?.Trim() ?? "";
        if (role != user.Role)
            AccountPolicy.EnsureCanChangeRole(user.Role, role, userId == adminId,
                user.Role == AccountPolicy.Manager ? await store.CountOpenEventsAsync(userId, cancellationToken) : 0);
        AccountPolicy.EnsureEmailFree(await store.EmailTakenAsync(details.Email, userId, cancellationToken));
        user.EditDetails(details, role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await reads.GetAccountAsync(userId, cancellationToken);
    }
}

/// <summary>Use case (A-19): the Admin locks or unlocks an account (never deleted). Locking signs it out of every device.</summary>
public sealed class SetAccountStatus(IAccountStore store, IUserAccountRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<UserAccountView?> HandleAsync(int userId, bool active, int adminId, CancellationToken cancellationToken = default)
    {
        var user = await store.GetUserAsync(userId, cancellationToken);
        if (user is null) return null;
        if (!active)
        {
            AccountPolicy.EnsureCanLock(user.Role, userId == adminId,
                user.Role == AccountPolicy.Manager ? await store.CountOpenEventsAsync(userId, cancellationToken) : 0);
            await store.StageEndSessionsAsync(userId, SessionEndReasons.AccountLocked, cancellationToken);
        }
        user.SetActive(active);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await reads.GetAccountAsync(userId, cancellationToken);
    }
}

/// <summary>Use case (A-19): the Admin reissues a temporary password; every device is signed out and the password goes out by email.</summary>
public sealed class ResetAccountPassword(IAccountStore store, IPasswordService passwords, IAccountMessages messages, IUserAccountRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<CreatedUserAccount?> HandleAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await store.GetUserAsync(userId, cancellationToken);
        if (user is null) return null;
        var password = passwords.NewTemporaryPassword();
        user.IssueTemporaryPassword(passwords.Hash(password));
        await store.StageEndSessionsAsync(userId, SessionEndReasons.PasswordReset, cancellationToken);
        messages.StaffCredentials(user, password, reset: true);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreatedUserAccount((await reads.GetAccountAsync(userId, cancellationToken))!, password);
    }
}

/// <summary>Use case: a signed-in user edits their own name and phone.</summary>
public sealed class UpdateProfile(IAccountStore store, IUnitOfWork unitOfWork)
{
    public async Task<ProfileView?> HandleAsync(int userId, ProfileCommand command, CancellationToken cancellationToken = default)
    {
        var user = await store.GetUserAsync(userId, cancellationToken);
        if (user is null) return null;
        user.EditProfile(AccountPolicy.ValidateProfileName(command.FullName), command.Phone);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ProfileView(user.FullName, user.Email, user.Phone ?? "", user.Role);
    }
}

/// <summary>
/// Use case: a signed-in user changes their password (the temporary one at first sign-in, or any time). False when the account is
/// gone or locked. Signing out the other devices and issuing the new token stay with the API (session delivery).
/// </summary>
public sealed class ChangePassword(IAccountStore store, IPasswordService passwords, IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(int userId, string? currentPassword, string? newPassword, string? confirmPassword, CancellationToken cancellationToken = default)
    {
        AccountPolicy.ValidateNewPassword(currentPassword, newPassword, confirmPassword);
        var user = await store.GetUserAsync(userId, cancellationToken);
        if (user is null || !passwords.Verify(user.PasswordHash, currentPassword!))
            throw new BusinessRuleException("Mật khẩu hiện tại không đúng.");
        if (user.Status != "Active") return false;
        user.ChangePassword(passwords.Hash(newPassword!));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
