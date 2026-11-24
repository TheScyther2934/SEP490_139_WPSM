using Domain.Services;

namespace Domain.Rules;

/// <summary>The service categories: the names the screens accept (Vietnamese or English) and the name each one is stored under.</summary>
public static class ServiceCategories
{
    private static readonly IReadOnlyDictionary<string, string> Stored = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["hall"] = "Hall", ["sảnh"] = "Hall", ["sảnh tiệc"] = "Hall",
        ["cuisine"] = "Catering", ["catering"] = "Catering", ["ẩm thực"] = "Catering",
        ["decor"] = "Decor", ["trang trí"] = "Decor",
        ["mc"] = "MC",
        ["av"] = "AV", ["âm thanh & ánh sáng"] = "AV", ["âm thanh ánh sáng"] = "AV",
        ["media"] = "Photo/Video", ["photo/video"] = "Photo/Video", ["quay chụp"] = "Photo/Video",
        ["costume"] = "Attire", ["attire"] = "Attire", ["trang phục"] = "Attire",
        ["makeup"] = "Makeup", ["trang điểm"] = "Makeup",
        ["transport"] = "Transport", ["vận chuyển"] = "Transport",
        ["ceremony"] = "Ceremony", ["bê lễ"] = "Ceremony", ["lễ vật"] = "Ceremony",
        ["performance"] = "Performance", ["văn nghệ"] = "Performance",
        ["furniture"] = "Furniture", ["nội thất"] = "Furniture"
    };

    public static string Normalize(string? category) => Stored.TryGetValue((category ?? "").Trim(), out var value)
        ? value : throw new BusinessRuleException("Danh mục dịch vụ không hợp lệ.");

    public static string Display(string category) => category switch
    {
        "Hall" => "Sảnh", "Catering" => "Ẩm thực", "Decor" => "Trang trí", "MC" => "MC", "AV" => "Âm thanh & ánh sáng",
        "Photo/Video" => "Quay chụp", "Attire" => "Trang phục", "Makeup" => "Trang điểm", "Transport" => "Vận chuyển",
        "Ceremony" => "Bê lễ", "Performance" => "Văn nghệ", "Furniture" => "Nội thất", _ => category
    };
}

/// <summary>Admin configuration rules (UC-37, UC-38, UC-39, GBR-14): catalog, price frames, event policy, hotline. Pure functions.</summary>
public static class ConfigurationPolicy
{
    /// <summary>Name 1–100 characters, unit 1–20, price above 0. Returns name and unit trimmed.</summary>
    public static (string Name, string Unit) ValidateCatalogItem(string? name, string? unit, decimal price)
    {
        var cleanName = (name ?? "").Trim();
        var cleanUnit = (unit ?? "").Trim();
        if (cleanName.Length is < 1 or > 100 || cleanUnit.Length is < 1 or > 20 || price <= 0)
            throw new BusinessRuleException("Tên, đơn vị hoặc đơn giá Catalog không hợp lệ.");
        return (cleanName, cleanUnit);
    }

    public static void EnsureNameFree(bool taken)
    {
        if (taken) throw new BusinessRuleException("Tên dịch vụ đã tồn tại trong nhóm này.");
    }

    /// <summary>A price frame takes effect today or later; min not negative and not above max. Returns the effective date.</summary>
    public static DateOnly ValidatePriceFrame(decimal min, decimal max, DateOnly? effectiveFrom, DateOnly todayVn)
    {
        if (min < 0 || max < min) throw new BusinessRuleException("Giá tối thiểu phải không âm và không vượt giá tối đa.");
        var effective = effectiveFrom ?? todayVn;
        if (effective < todayVn) throw new BusinessRuleException("Ngày hiệu lực không được ở trong quá khứ.");
        return effective;
    }

    /// <summary>
    /// The event policy must stay coherent: the value after the largest decrease stays above the deposit, the lock comes after the
    /// supplier deadline and before the earliest signing day. Returns the effective date.
    /// </summary>
    public static DateOnly ValidateEventPolicy(EventPolicy p, DateOnly? effectiveFrom, DateOnly todayVn)
    {
        var effective = effectiveFrom ?? todayVn;
        if (effective < todayVn) throw new BusinessRuleException("Ngày hiệu lực không được ở trong quá khứ.");
        if (p.DepositRatio is <= 0 or > 100 || p.MaxDecreaseRatio is < 0 or >= 100)
            throw new BusinessRuleException("Tỷ lệ đặt cọc phải trong khoảng 1–100%, mức giảm tối đa trong khoảng 0–99%.");
        if (100 - p.MaxDecreaseRatio < p.DepositRatio)
            throw new BusinessRuleException("Mức giảm tối đa quá lớn: giá trị sau khi giảm phải luôn lớn hơn tiền đặt cọc.");
        if (p.BookingMinDays < 1 || p.BookingMaxDays < p.BookingMinDays || p.BookingMaxDays > 365)
            throw new BusinessRuleException("Khoảng ngày ký hợp đồng không hợp lệ (tối thiểu ≥ 1, tối đa ≥ tối thiểu và ≤ 365).");
        if (p.SupplierDeadlineDays < 1 || p.LockDays < p.SupplierDeadlineDays || p.LockDays >= p.BookingMinDays)
            throw new BusinessRuleException("Mốc khóa phải ≥ hạn đặt nhà cung cấp và nhỏ hơn số ngày ký tối thiểu.");
        if (p.SettlementDueDays is < 0 or > 90)
            throw new BusinessRuleException("Hạn tất toán phải từ 0 đến 90 ngày sau ngày cưới.");
        return effective;
    }

    /// <summary>The values of the policy by key, the way they are stored (one row per key and effective date).</summary>
    public static IReadOnlyDictionary<string, decimal> Values(EventPolicy p) => new Dictionary<string, decimal>
    {
        [EventPolicy.Keys.DepositRatio] = p.DepositRatio,
        [EventPolicy.Keys.MaxDecreaseRatio] = p.MaxDecreaseRatio,
        [EventPolicy.Keys.BookingMinDays] = p.BookingMinDays,
        [EventPolicy.Keys.BookingMaxDays] = p.BookingMaxDays,
        [EventPolicy.Keys.LockDays] = p.LockDays,
        [EventPolicy.Keys.SupplierDeadlineDays] = p.SupplierDeadlineDays,
        [EventPolicy.Keys.SettlementDueDays] = p.SettlementDueDays
    };

    /// <summary>Null when not given (the hotline is left as it is); otherwise digits, spaces, dots or +, at most 20 characters.</summary>
    public static string? ValidateHotline(string? hotline)
    {
        var text = hotline?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length > 20 || text.Any(c => !char.IsDigit(c) && c is not (' ' or '.' or '+')))
            throw new BusinessRuleException("Số hotline chỉ gồm chữ số (tối đa 20 ký tự).");
        return text;
    }
}
