using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// A-19 account management (Admin only). Rules taken from SRS/SDS: emails are unique, accounts are deactivated and never
/// deleted, Customer accounts only come from M-03, deactivation revokes all refresh tokens, system passwords must be changed
/// at first login (MustChangePassword) and are delivered through the email outbox in the same transaction (L-09).
/// </summary>
public sealed class UserAccountRepository(WpsmDbContext db, IEmailOutbox emailOutbox, EmailOptions emailOptions) : IUserAccountRepository
{
    private static readonly string[] StaffRoles = ["Manager", "Admin"];
    private static readonly string[] ClosedStatuses = ["Settled", "Closed"];

    public async Task<IReadOnlyList<UserAccountView>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
        await Views(db.Users.OrderBy(x => x.UserId)).ToListAsync(cancellationToken);

    public async Task<CreatedUserAccount> CreateAccountAsync(UserAccountCommand command, string passwordHash, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var (fullName, email, phone) = Validate(command);
        var role = command.Role?.Trim() ?? "";
        if (!StaffRoles.Contains(role))
            throw new InvalidOperationException("Admin chỉ tạo được tài khoản Manager hoặc Admin. Tài khoản khách hàng được tạo khi Manager khởi tạo dự án (M-03).");
        await EnsureEmailFreeAsync(email, null, cancellationToken);

        var user = new User
        {
            Email = email, FullName = fullName, Phone = phone, Role = role, Status = "Active",
            PasswordHash = passwordHash, MustChangePassword = true, CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        emailOutbox.Enqueue(email, "Tài khoản WPSM của bạn",
            EmailTemplates.AccountCredentials(fullName, email, temporaryPassword, role, reset: false, emailOptions.PublicBaseUrl), "Credentials", null);
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedUserAccount(await Views(db.Users.Where(x => x.UserId == user.UserId)).SingleAsync(cancellationToken), temporaryPassword);
    }

    public async Task<UserAccountView?> UpdateAccountAsync(int userId, UserAccountCommand command, int adminId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (user is null) return null;
        var (fullName, email, phone) = Validate(command);
        var role = command.Role?.Trim() ?? "";
        if (role != user.Role)
        {
            if (user.Role == "Customer" || role == "Customer" || !StaffRoles.Contains(role))
                throw new InvalidOperationException("Chỉ đổi được vai trò giữa Manager và Admin; tài khoản khách hàng giữ nguyên vai trò.");
            if (userId == adminId) throw new InvalidOperationException("Bạn không thể tự đổi vai trò của chính mình.");
            if (user.Role == "Manager" && await OpenManagedProjectsAsync(userId, cancellationToken) is var open and > 0)
                throw new InvalidOperationException($"Manager đang phụ trách {open} dự án chưa đóng nên không thể chuyển sang Admin.");
        }
        await EnsureEmailFreeAsync(email, userId, cancellationToken);
        user.FullName = fullName;
        user.Email = email;
        user.Phone = phone;
        user.Role = role;
        await db.SaveChangesAsync(cancellationToken);
        return await Views(db.Users.Where(x => x.UserId == userId)).SingleAsync(cancellationToken);
    }

    public async Task<UserAccountView?> SetStatusAsync(int userId, bool active, int adminId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (user is null) return null;
        if (!active)
        {
            if (userId == adminId) throw new InvalidOperationException("Bạn không thể tự khóa tài khoản của chính mình.");
            if (user.Role == "Manager" && await OpenManagedProjectsAsync(userId, cancellationToken) is var open and > 0)
                throw new InvalidOperationException($"Manager đang phụ trách {open} dự án chưa đóng. Hãy hoàn tất hoặc chuyển dự án trước khi khóa.");
            var now = DateTime.UtcNow;
            foreach (var token in await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(cancellationToken))
                token.RevokedAt = now;
        }
        user.Status = active ? "Active" : "Inactive";
        await db.SaveChangesAsync(cancellationToken);
        return await Views(db.Users.Where(x => x.UserId == userId)).SingleAsync(cancellationToken);
    }

    public async Task<UserAccountView?> ResetPasswordAsync(int userId, string passwordHash, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (user is null) return null;
        if (user.Status != "Active") throw new InvalidOperationException("Tài khoản đang bị khóa. Hãy mở khóa trước khi cấp lại mật khẩu.");
        user.PasswordHash = passwordHash;
        user.MustChangePassword = true;
        var now = DateTime.UtcNow;
        foreach (var token in await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(cancellationToken))
            token.RevokedAt = now;
        emailOutbox.Enqueue(user.Email, "Cấp lại mật khẩu WPSM",
            EmailTemplates.AccountCredentials(user.FullName, user.Email, temporaryPassword, user.Role, reset: true, emailOptions.PublicBaseUrl), "Credentials", null);
        await db.SaveChangesAsync(cancellationToken);
        return await Views(db.Users.Where(x => x.UserId == userId)).SingleAsync(cancellationToken);
    }

    // Filter/order on Users before this projection: EF cannot translate OrderBy/Where over the projected record.
    private IQueryable<UserAccountView> Views(IQueryable<User>? source = null) =>
        (source ?? db.Users).AsNoTracking().Select(x => new UserAccountView(x.UserId, x.Email, x.FullName, x.Phone ?? "", x.Role, x.Status == "Active",
            x.MustChangePassword, x.CreatedAt,
            db.WeddingProjects.Count(p => (p.CustomerId == x.UserId || p.ManagerId == x.UserId) && !ClosedStatuses.Contains(p.Status))));

    private Task<int> OpenManagedProjectsAsync(int userId, CancellationToken cancellationToken) =>
        db.WeddingProjects.CountAsync(p => p.ManagerId == userId && !ClosedStatuses.Contains(p.Status), cancellationToken);

    private async Task EnsureEmailFreeAsync(string email, int? exceptUserId, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(x => x.Email == email && x.UserId != exceptUserId, cancellationToken))
            throw new InvalidOperationException("Email đã được dùng cho tài khoản khác.");
    }

    private static (string FullName, string Email, string? Phone) Validate(UserAccountCommand command)
    {
        var fullName = command.FullName?.Trim() ?? "";
        if (fullName.Length is < 1 or > 100) throw new InvalidOperationException("Họ tên là bắt buộc và tối đa 100 ký tự.");
        var email = command.Email?.Trim() ?? "";
        if (email.Length > 100 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email || !email.Contains('.', StringComparison.Ordinal))
            throw new InvalidOperationException("Email không hợp lệ.");
        var phone = WpsmValueNormalizer.NormalizePhone(command.Phone ?? "");
        if (phone is not null && phone.Length is < 9 or > 11) throw new InvalidOperationException("Số điện thoại phải có 9–11 chữ số.");
        return (fullName, email, phone);
    }
}
