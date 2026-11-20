namespace Infrastructure.Identity;

/// <summary>Short readable label of a device from its User-Agent ("Chrome · Windows"), shown in the signed-in devices list.</summary>
public static class DeviceLabel
{
    public static string From(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Thiết bị không rõ";
        var ua = userAgent;
        // Order matters: Edge, Opera and Cốc Cốc also announce "Chrome"; Chrome also announces "Safari".
        var browser = Has(ua, "Edg/") ? "Edge"
            : Has(ua, "OPR/") || Has(ua, "Opera") ? "Opera"
            : Has(ua, "coc_coc") ? "Cốc Cốc"
            : Has(ua, "Firefox/") || Has(ua, "FxiOS") ? "Firefox"
            : Has(ua, "Chrome/") || Has(ua, "CriOS") ? "Chrome"
            : Has(ua, "Safari/") ? "Safari"
            : Has(ua, "PowerShell") || Has(ua, "curl") || Has(ua, "node") ? "Công cụ dòng lệnh"
            : "Trình duyệt khác";
        var os = Has(ua, "iPhone") ? "iPhone"
            : Has(ua, "iPad") ? "iPad"
            : Has(ua, "Android") ? (Has(ua, "Mobile") ? "Điện thoại Android" : "Máy tính bảng Android")
            : Has(ua, "Windows") ? "Windows"
            : Has(ua, "Mac OS X") || Has(ua, "Macintosh") ? "macOS"
            : Has(ua, "Linux") ? "Linux"
            : null;
        return os is null ? browser : $"{browser} · {os}";
    }

    private static bool Has(string value, string part) => value.Contains(part, StringComparison.OrdinalIgnoreCase);
}
