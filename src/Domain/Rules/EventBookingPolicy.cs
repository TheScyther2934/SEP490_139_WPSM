using System.Net.Mail;
using Domain.Services;

namespace Domain.Rules;

/// <summary>The customer as entered at signing, checked and trimmed; the phone keeps digits only.</summary>
public sealed record BookingCustomer(string Name, string Email, string PhoneDigits);

/// <summary>A service chosen at signing.</summary>
public sealed record BookingLine(int CatalogItemId, string? Description, int Quantity, decimal UnitPrice);

/// <summary>The price frame of a service on the signing day (Admin configuration).</summary>
public sealed record PriceRange(decimal Min, decimal Max);

/// <summary>
/// Stage 1, signing and deposit (UC-03, GBR-01..GBR-03): who the customer is, a free slot 15–90 days ahead,
/// the services chosen within their price frames, and a deposit of at least the policy ratio of the contract value.
/// </summary>
public static class EventBookingPolicy
{
    public const string Cash = "Cash", Bank = "Bank";

    public static BookingCustomer ValidateCustomer(string? coupleName, string? email, string? phone)
    {
        var name = (coupleName ?? "").Trim();
        var mail = (email ?? "").Trim();
        if (name.Length is 0 or > 100 || mail.Length is 0 or > 100 || !MailAddress.TryCreate(mail, out var address) || address.Address != mail)
            throw new BusinessRuleException("Tên khách hàng hoặc email không hợp lệ.");
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length is < 9 or > 11) throw new BusinessRuleException("Số điện thoại cần có từ 9 đến 11 chữ số.");
        return new BookingCustomer(name, mail, digits);
    }

    /// <summary>"Lunch"/"trưa" or "Evening"/"tối", as stored in Slots.</summary>
    public static string NormalizeSlot(string? slot) => (slot ?? "").Trim().ToLowerInvariant() switch
    {
        "lunch" or "trưa" => "Lunch",
        "evening" or "tối" => "Evening",
        _ => throw new BusinessRuleException("Ca tiệc không hợp lệ.")
    };

    public static void EnsurePaymentMethod(string? method)
    {
        if (method is not (Cash or Bank)) throw new BusinessRuleException("Phương thức thanh toán không hợp lệ.");
    }

    public static void EnsureSlotFree(bool taken)
    {
        if (taken) throw new BusinessRuleException("Ca tiệc đã được đặt vào ngày này. Vui lòng chọn ca khác.");
    }

    /// <summary>GBR-01: the event date must be within the booking window of the policy (15–90 days by default).</summary>
    public static void EnsureBookingDate(DateOnly eventDate, DateOnly today, EventPolicy policy)
    {
        if (EventStageRules.ValidateBookingDate(eventDate, today, policy) is { } error) throw new BusinessRuleException(error);
    }

    public static void ValidateLines(IReadOnlyList<BookingLine>? lines)
    {
        if (lines is null || lines.Count == 0) throw new BusinessRuleException("Hãy chọn các dịch vụ khách đặt.");
        if (lines.GroupBy(l => l.CatalogItemId).Any(g => g.Count() > 1)) throw new BusinessRuleException("Mỗi dịch vụ chỉ chọn một lần.");
        if (lines.Any(l => l.Quantity <= 0 || l.UnitPrice < 0)) throw new BusinessRuleException("Số lượng phải lớn hơn 0 và đơn giá không âm.");
    }

    /// <summary>GBR-02: the unit price agreed must stay inside the price frame of the service.</summary>
    public static void EnsurePriceInRange(string serviceName, decimal unitPrice, PriceRange range)
    {
        if (unitPrice < range.Min || unitPrice > range.Max)
            throw new BusinessRuleException($"Đơn giá của '{serviceName}' phải trong khoảng {range.Min.ToString("N0", Vi)} - {EventStageRules.Vnd(range.Max)}.");
    }

    public static decimal ContractValue(IReadOnlyList<BookingLine> lines)
    {
        var value = lines.Sum(l => l.Quantity * l.UnitPrice);
        if (value <= 0) throw new BusinessRuleException("Giá trị hợp đồng phải lớn hơn 0.");
        return value;
    }

    /// <summary>GBR-03: at least the policy ratio of the contract value (the minimum when none is given), at most the whole value.</summary>
    public static decimal Deposit(decimal contractValue, decimal requested, EventPolicy policy)
    {
        var minimum = EventStageRules.DepositFor(contractValue, policy);
        var deposit = requested <= 0 ? minimum : requested;
        if (deposit < minimum || deposit > contractValue)
            throw new BusinessRuleException($"Tiền đặt cọc phải từ {EventStageRules.Vnd(minimum)} ({policy.DepositRatio:G29}% giá trị hợp đồng) đến {EventStageRules.Vnd(contractValue)}.");
        return deposit;
    }

    /// <summary>Event codes are WPSM-{year}-{number}, numbered on from the highest code of the year.</summary>
    public static string NextProjectCode(int year, IEnumerable<string> codesOfTheYear)
    {
        var prefix = $"WPSM-{year}-";
        var last = codesOfTheYear.Where(c => c.StartsWith(prefix, StringComparison.Ordinal))
            .Select(c => int.TryParse(c[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{last + 1:D3}";
    }

    private static readonly System.Globalization.CultureInfo Vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
}
