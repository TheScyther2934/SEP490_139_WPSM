namespace Domain.Rules;

/// <summary>
/// Vietnamese phone numbers: stored as digits only, starting with 0 and 10 digits long (mobile) or 11 (old landline).
/// Spaces, dots and dashes are dropped; a +84 / 84 country prefix becomes the leading 0.
/// </summary>
public static class PhonePolicy
{
    public const string InvalidMessage = "Số điện thoại phải bắt đầu bằng số 0 và có 10–11 chữ số (ví dụ 0912345678).";

    /// <summary>Digits only, with a leading 84 turned into 0; null when nothing was entered.</summary>
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length is 11 or 12) digits = "0" + digits[2..];
        return digits;
    }

    public static bool IsValid(string? digits) => digits is { Length: 10 or 11 } && digits[0] == '0' && digits.All(char.IsAsciiDigit);

    /// <summary>A required phone: normalized and checked.</summary>
    public static string Require(string? phone)
    {
        var digits = Normalize(phone);
        if (!IsValid(digits)) throw new BusinessRuleException(InvalidMessage);
        return digits!;
    }

    /// <summary>An optional phone: null when empty, otherwise normalized and checked.</summary>
    public static string? Optional(string? phone) => Normalize(phone) is null ? null : Require(phone);
}
