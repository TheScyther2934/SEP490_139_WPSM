namespace WPSM.Infrastructure.Persistence;

internal static class WpsmValueNormalizer
{
    internal static string NormalizeSlot(string slot) => slot.Trim().ToLowerInvariant() switch
    {
        "lunch" or "trưa" => "Lunch", "evening" or "tối" => "Evening", _ => throw new InvalidOperationException("Ca tiệc không hợp lệ.")
    };

    internal static string? NormalizePhone(string phone) => string.IsNullOrWhiteSpace(phone) ? null : new string(phone.Where(char.IsDigit).ToArray());
}
