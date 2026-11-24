using Domain.Rules;

namespace Domain.Entities;

public partial class User
{
    /// <summary>A customer account created at signing: it must change the temporary password at first sign-in.</summary>
    public static User NewCustomer(BookingCustomer customer, string passwordHash, DateTime nowUtc) => new()
    {
        Email = customer.Email,
        FullName = customer.Name,
        Phone = customer.PhoneDigits, // CK_Users_Phone: digits only
        Role = "Customer",
        Status = "Active",
        MustChangePassword = true,
        CreatedAt = nowUtc,
        PasswordHash = passwordHash
    };

    /// <summary>An existing customer may book another event (one event = one contract); a deactivated account is reactivated. Staff emails are refused.</summary>
    public void PrepareForNewBooking()
    {
        if (Role != "Customer") throw new BusinessRuleException("Email này thuộc tài khoản nhân viên, không dùng làm khách hàng.");
        if (Status == "Inactive") Status = "Active";
    }

    /// <summary>A Manager or Admin account created by an Admin (A-19); the temporary password must be changed at first sign-in.</summary>
    public static User NewStaff(AccountDetails details, string role, string passwordHash, DateTime nowUtc) => new()
    {
        Email = details.Email,
        FullName = details.FullName,
        Phone = details.Phone,
        Role = role,
        Status = "Active",
        PasswordHash = passwordHash,
        MustChangePassword = true,
        CreatedAt = nowUtc
    };

    /// <summary>Details and role changed by the Admin (role rules: AccountPolicy.EnsureCanChangeRole).</summary>
    public void EditDetails(AccountDetails details, string role)
    {
        FullName = details.FullName;
        Email = details.Email;
        Phone = details.Phone;
        Role = role;
    }

    public void SetActive(bool active) => Status = active ? "Active" : "Inactive";

    /// <summary>The Admin issues a new temporary password (A-19); a locked account must be unlocked first.</summary>
    public void IssueTemporaryPassword(string passwordHash)
    {
        if (Status != "Active") throw new BusinessRuleException("Tài khoản đang bị khóa. Hãy mở khóa trước khi cấp lại mật khẩu.");
        PasswordHash = passwordHash;
        MustChangePassword = true;
    }

    /// <summary>The user chose their own password: the temporary one is no longer required to change.</summary>
    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        MustChangePassword = false;
    }

    /// <summary>The user edits their own profile (name and phone; email and role stay with the Admin).</summary>
    public void EditProfile(string fullName, string? phone)
    {
        FullName = fullName;
        Phone = AccountPolicy.PhoneDigits(phone);
    }

    /// <summary>A new temporary password, only while the customer has never signed in and changed it.</summary>
    public void ResetTemporaryPassword(string passwordHash)
    {
        if (!MustChangePassword) throw new BusinessRuleException("Khách hàng đã kích hoạt tài khoản nên không cần gửi lại thông tin đăng nhập.");
        PasswordHash = passwordHash;
    }
}
