using System.Net.Mail;

namespace Domain.Rules;

/// <summary>Checked account details: name and email trimmed, phone as digits only (null when not given).</summary>
public sealed record AccountDetails(string FullName, string Email, string? Phone);

/// <summary>
/// Account rules (A-19, profile, password). Emails are unique; accounts are locked, never deleted; customer accounts only come from
/// signing an event (M-03); an Admin cannot lock or demote themselves; a Manager with open events can be neither locked nor made Admin.
/// </summary>
public static class AccountPolicy
{
    public const string Customer = "Customer";
    public const string Manager = "Manager";
    public const string Admin = "Admin";
    public static readonly IReadOnlyList<string> StaffRoles = [Manager, Admin];
    public const int MinPasswordLength = 8;

    public static string? PhoneDigits(string? phone) => string.IsNullOrWhiteSpace(phone) ? null : new string(phone.Where(char.IsDigit).ToArray());

    public static AccountDetails ValidateDetails(string? fullName, string? email, string? phone)
    {
        var name = fullName?.Trim() ?? "";
        if (name.Length is < 1 or > 100) throw new BusinessRuleException("Họ tên là bắt buộc và tối đa 100 ký tự.");
        var address = email?.Trim() ?? "";
        if (address.Length > 100 || !MailAddress.TryCreate(address, out var parsed) || parsed.Address != address || !address.Contains('.', StringComparison.Ordinal))
            throw new BusinessRuleException("Email không hợp lệ.");
        var digits = PhoneDigits(phone);
        if (digits is not null && digits.Length is < 9 or > 11) throw new BusinessRuleException("Số điện thoại phải có 9–11 chữ số.");
        return new AccountDetails(name, address, digits);
    }

    public static void EnsureEmailFree(bool taken)
    {
        if (taken) throw new BusinessRuleException("Email đã được dùng cho tài khoản khác.");
    }

    /// <summary>The Admin creates staff accounts only. Returns the role trimmed.</summary>
    public static string EnsureStaffRole(string? role)
    {
        var value = role?.Trim() ?? "";
        if (!StaffRoles.Contains(value))
            throw new BusinessRuleException("Admin chỉ tạo được tài khoản Manager hoặc Admin. Tài khoản khách hàng được tạo khi Manager khởi tạo sự kiện (M-03).");
        return value;
    }

    /// <summary>Roles move between Manager and Admin only, never by the Admin on their own account, never for a Manager with open events.</summary>
    public static void EnsureCanChangeRole(string currentRole, string newRole, bool isSelf, int openEvents)
    {
        if (currentRole == newRole) return;
        if (currentRole == Customer || newRole == Customer || !StaffRoles.Contains(newRole))
            throw new BusinessRuleException("Chỉ đổi được vai trò giữa Manager và Admin; tài khoản khách hàng giữ nguyên vai trò.");
        if (isSelf) throw new BusinessRuleException("Bạn không thể tự đổi vai trò của chính mình.");
        if (currentRole == Manager && openEvents > 0)
            throw new BusinessRuleException($"Manager đang phụ trách {openEvents} sự kiện chưa đóng nên không thể chuyển sang Admin.");
    }

    public static void EnsureCanLock(string role, bool isSelf, int openEvents)
    {
        if (isSelf) throw new BusinessRuleException("Bạn không thể tự khóa tài khoản của chính mình.");
        if (role == Manager && openEvents > 0)
            throw new BusinessRuleException($"Manager đang phụ trách {openEvents} sự kiện chưa đóng. Hãy hoàn tất hoặc chuyển sự kiện trước khi khóa.");
    }

    /// <summary>The profile name a user writes for themselves. Returns it trimmed.</summary>
    public static string ValidateProfileName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 100) throw new BusinessRuleException("Họ tên không hợp lệ.");
        return fullName.Trim();
    }

    /// <summary>A new password: at least 8 characters, typed twice the same, different from the current one.</summary>
    public static void ValidateNewPassword(string? current, string? next, string? confirm)
    {
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(next) || next.Length < MinPasswordLength || next != confirm)
            throw new BusinessRuleException("Mật khẩu mới phải dài ít nhất 8 ký tự và hai lần nhập phải trùng nhau.");
        if (next == current) throw new BusinessRuleException("Mật khẩu mới không được trùng với mật khẩu hiện tại.");
    }
}
