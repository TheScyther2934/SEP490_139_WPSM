using Domain.Services;

namespace Domain.Rules;

/// <summary>One star rating as the customer sent it.</summary>
public sealed record RatingInput(string? Module, int Stars, string? Comment);

/// <summary>
/// After the event: ratings (UC-14, GBR-10) and complaints (UC-15, UC-16, UC-35, UC-36, UC-46). Pure functions.
/// </summary>
public static class PostEventPolicy
{
    /// <summary>The four modules, in the order they are shown and saved.</summary>
    public static readonly IReadOnlyList<string> RatingModules = ["Hall", "Menu", "Decor", "MC"];

    public const int MaxRatingComment = 500;
    public const int MaxComplaintText = 1000;
    public const int MaxEscalationNote = 500;

    /// <summary>GBR-10: a customer rates only after the settlement.</summary>
    public static bool CanRate(string eventStatus) => eventStatus is EventStatus.Settled or EventStatus.Closed;

    /// <summary>SRS: a complaint can be filed only after the event took place.</summary>
    public static bool CanComplain(string eventStatus) => eventStatus is EventStatus.Completed or EventStatus.Settled or EventStatus.Closed;

    /// <summary>Checks the four ratings and returns them in module order, comments trimmed (null when empty).</summary>
    public static IReadOnlyList<RatingInput> ValidateRatings(string eventStatus, bool alreadyRated, IReadOnlyList<RatingInput>? ratings)
    {
        if (alreadyRated) throw new BusinessRuleException("Đánh giá dịch vụ đã được gửi trước đó và không thể chỉnh sửa (GBR-10 / MSG26).");
        if (!CanRate(eventStatus)) throw new BusinessRuleException("Chỉ đánh giá được sau khi sự kiện đã tất toán (GBR-10).");
        if (ratings is null || ratings.Count == 0) throw new BusinessRuleException("Vui lòng đánh giá đầy đủ 4 hạng mục dịch vụ (MSG16).");

        var byModule = new Dictionary<string, RatingInput>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ratings)
        {
            var module = RatingModules.FirstOrDefault(m => string.Equals(m, item.Module?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException($"Hạng mục đánh giá '{item.Module}' không hợp lệ. Chỉ chấp nhận Hall, Menu, Decor, MC.");
            if (item.Stars is < 1 or > 5) throw new BusinessRuleException("Số sao đánh giá cho mỗi hạng mục phải từ 1 đến 5 sao (MSG16).");
            if (item.Comment is { Length: > MaxRatingComment }) throw new BusinessRuleException("Nhận xét cho mỗi hạng mục tối đa 500 ký tự.");
            byModule[module] = item with { Module = module };
        }
        if (byModule.Count < RatingModules.Count)
            throw new BusinessRuleException("Vui lòng chọn số sao (1–5) cho cả 4 hạng mục: Sảnh tiệc, Thực đơn, Trang trí và MC (MSG16).");

        return RatingModules.Select(m => byModule[m] with { Comment = string.IsNullOrWhiteSpace(byModule[m].Comment) ? null : byModule[m].Comment!.Trim() }).ToList();
    }

    /// <summary>The complaint text (MSG25), trimmed; the event must have taken place.</summary>
    public static string ValidateComplaint(string eventStatus, string? description)
    {
        var text = (description ?? "").Trim();
        if (text.Length == 0) throw new BusinessRuleException("Vui lòng nhập nội dung mô tả khiếu nại (MSG25).");
        if (text.Length > MaxComplaintText) throw new BusinessRuleException("Nội dung khiếu nại không được vượt quá 1000 ký tự.");
        if (!CanComplain(eventStatus)) throw new BusinessRuleException("Chỉ gửi khiếu nại được sau khi sự kiện đã diễn ra.");
        return text;
    }

    /// <summary>
    /// CMP-{last part of the event code}-{nn}: nn starts after the event's complaints and skips codes already taken.
    /// </summary>
    public static string NextComplaintCode(string projectCode, int complaintsOfEvent, IReadOnlyCollection<string> takenCodes)
    {
        var suffix = projectCode.Split('-').LastOrDefault() ?? projectCode;
        var seq = complaintsOfEvent + 1;
        string Code() => $"CMP-{suffix}-{seq:D2}";
        while (takenCodes.Contains(Code())) seq++;
        return Code();
    }
}
