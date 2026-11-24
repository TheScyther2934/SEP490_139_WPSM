using Domain.Services;

namespace Domain.Rules;

/// <summary>Task details after the checks: trimmed, deadline in UTC.</summary>
public sealed record CoordinationTaskDetails(string TaskName, string Department, DateTime DeadlineUtc, bool PhotoRequired, string? Note);

/// <summary>
/// Coordination tasks of an event (UC-30..UC-32, GBR-06, GBR-07): open from the lock until settlement,
/// one of the four departments, a deadline between the lock and the end of the wedding day.
/// Rules of a single task (complete, delete, add proof) live on <see cref="Entities.DayOfTask"/>.
/// </summary>
public static class CoordinationTaskPolicy
{
    /// <summary>Proof files kept per task: 1–3 at completion, more can be added afterwards up to this limit.</summary>
    public const int MaxProofPerTask = 6;
    public const int MaxProofPerUpload = 3;

    public static readonly IReadOnlyList<string> Departments = ["Kitchen", "Service", "Decor", "Tech-MC"];

    /// <summary>Preparation starts at the lock (the stage can be built days before), so tasks are open from then until settlement.</summary>
    public static bool IsOpen(string eventStatus) => eventStatus is EventStatus.Locked or EventStatus.Completed;

    public static void EnsureOpen(string eventStatus)
    {
        if (IsOpen(eventStatus)) return;
        throw new BusinessRuleException(eventStatus == EventStatus.Adjusting
            ? "Nhiệm vụ điều phối mở sau khi khóa dịch vụ."
            : "Sự kiện đã tất toán, đã đóng hoặc đã hủy, không thể cập nhật nhiệm vụ điều phối.");
    }

    /// <summary>Tasks are generated from the locked services, before the event is held.</summary>
    public static void EnsureCanGenerate(string eventStatus)
    {
        if (eventStatus != EventStatus.Locked)
            throw new BusinessRuleException("Chỉ tạo nhiệm vụ điều phối khi sự kiện đã khóa dịch vụ và chưa diễn ra.");
    }

    public static CoordinationTaskDetails ValidateDetails(string? taskName, string? department, DateTime deadline, bool photoRequired, string? note,
        DateOnly eventDate, DateTime? lockedAtUtc)
    {
        var name = (taskName ?? "").Trim();
        if (name.Length is < 3 or > 200) throw new BusinessRuleException("Tên nhiệm vụ cần từ 3 đến 200 ký tự.");
        if (department is null || !Departments.Contains(department)) throw new BusinessRuleException("Bộ phận không hợp lệ.");
        var deadlineUtc = DateTime.SpecifyKind(deadline.Kind == DateTimeKind.Local ? deadline.ToUniversalTime() : deadline, DateTimeKind.Utc);
        if (deadlineUtc > VietnamTime.ConvertVietnamToUtc(eventDate, new TimeOnly(23, 59)))
            throw new BusinessRuleException("Hạn nhiệm vụ không được sau ngày cưới.");
        // One day of slack before the lock, for preparation planned the evening before.
        if (lockedAtUtc is { } locked && deadlineUtc < DateTime.SpecifyKind(locked, DateTimeKind.Utc).AddDays(-1))
            throw new BusinessRuleException("Hạn nhiệm vụ không được trước ngày khóa dịch vụ.");
        return new CoordinationTaskDetails(name, department, deadlineUtc, photoRequired, Text.Clip(note, 500));
    }
}

internal static class Text
{
    /// <summary>Trimmed text cut to <paramref name="max"/> characters; null when blank.</summary>
    public static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length > max ? value[..max] : value;
    }
}
