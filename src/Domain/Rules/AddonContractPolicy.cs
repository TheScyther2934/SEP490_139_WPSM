using Domain.Services;

namespace Domain.Rules;

/// <summary>One line of an addon contract (a catalog service, or a free description).</summary>
public sealed record AddonLine(int? CatalogItemId, string Description, int Quantity, decimal UnitPrice);

/// <summary>
/// Addon contract (hợp đồng phát sinh) rules: extra services after the lock. No deposit; the operator creates it and reminds the
/// customer; it is carried out and billed (settlement invoice) only once the customer confirms it. Pure functions.
/// </summary>
public static class AddonContractPolicy
{
    public const string PendingCustomer = "PendingCustomer";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
    public const int MaxLines = 30;
    /// <summary>One reminder per hour at most, so the customer is not flooded.</summary>
    public static readonly TimeSpan ReminderInterval = TimeSpan.FromHours(1);

    public static void EnsureCanCreate(string eventStatus, bool settlementPaid)
    {
        if (!EventStageRules.CanAddAddon(eventStatus))
            throw new BusinessRuleException(eventStatus == EventStatus.Adjusting
                ? "Sự kiện còn trong giai đoạn điều chỉnh: hãy sửa trực tiếp dịch vụ đã đặt. Hợp đồng phát sinh chỉ có sau khi khóa."
                : "Sự kiện đã tất toán, đóng hoặc hủy nên không thêm phát sinh.");
        if (settlementPaid) throw new BusinessRuleException("Hóa đơn tất toán đã thanh toán, không thêm phát sinh.");
    }

    /// <summary>Checks and normalises the title and lines (trimmed); returns them ready to store.</summary>
    public static (string Title, IReadOnlyList<AddonLine> Lines) ValidateContent(string? title, IReadOnlyList<AddonLine>? lines)
    {
        var cleanTitle = title?.Trim() ?? "";
        if (cleanTitle.Length is < 3 or > 200) throw new BusinessRuleException("Tiêu đề phát sinh cần từ 3 đến 200 ký tự.");
        if (lines is null || lines.Count == 0) throw new BusinessRuleException("Hợp đồng phát sinh cần ít nhất một dòng dịch vụ.");
        if (lines.Count > MaxLines) throw new BusinessRuleException($"Tối đa {MaxLines} dòng dịch vụ.");
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Description) || line.Description.Trim().Length > 300)
                throw new BusinessRuleException("Mỗi dòng cần mô tả (tối đa 300 ký tự).");
            if (line.Quantity <= 0 || line.UnitPrice <= 0) throw new BusinessRuleException("Số lượng và đơn giá phải lớn hơn 0.");
        }
        return (cleanTitle, lines.Select(l => l with { Description = l.Description.Trim() }).ToList());
    }

    public static decimal Total(IEnumerable<AddonLine> lines) => lines.Sum(l => l.Quantity * l.UnitPrice);

    public static void EnsureCanRemind(string status, DateTime? lastRemindedAt, DateTime now)
    {
        if (status != PendingCustomer) throw new BusinessRuleException("Chỉ nhắc được phát sinh đang chờ khách xác nhận.");
        if (lastRemindedAt is { } last && now - last < ReminderInterval)
            throw new BusinessRuleException("Vừa nhắc khách xong, hãy đợi ít nhất 1 giờ trước khi nhắc lại.");
    }

    /// <summary>Returns the trimmed reason.</summary>
    public static string EnsureCanCancel(string status, string? reason)
    {
        var clean = reason?.Trim() ?? "";
        if (clean.Length is < 5 or > 500) throw new BusinessRuleException("Hãy ghi lý do hủy (5 đến 500 ký tự).");
        if (status != PendingCustomer) throw new BusinessRuleException("Chỉ hủy được phát sinh chưa được khách xác nhận.");
        return clean;
    }

    public static void EnsureCanConfirm(string status, string eventStatus)
    {
        if (status != PendingCustomer) throw new BusinessRuleException("Hợp đồng phát sinh này không còn chờ xác nhận.");
        if (!EventStageRules.CanAddAddon(eventStatus)) throw new BusinessRuleException("Sự kiện đã tất toán hoặc đóng, không xác nhận thêm phát sinh.");
    }
}
