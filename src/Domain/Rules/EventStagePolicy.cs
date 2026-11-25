using Domain.Services;

namespace Domain.Rules;

/// <summary>One service line the operator wants on the event after an adjustment (stage 2).</summary>
public sealed record AdjustedLine(int CatalogItemId, string Description, int Quantity, decimal UnitPrice);

/// <summary>
/// What the operator does along the 5 stages, besides signing, locking, coordination and settlement: stage notes, cancelling an event,
/// adjusting services before the lock (stage 2) and attaching supplier evidence after it (stage 3). Pure functions.
/// </summary>
public static class EventStagePolicy
{
    public static readonly IReadOnlySet<string> NoteTypes = new HashSet<string> { "Tasting", "Fitting", "Adjustment", "Supplier", "General" };
    public static readonly IReadOnlySet<string> AdjustmentNoteTypes = new HashSet<string> { "Adjustment", "Tasting", "Fitting" };
    public static readonly IReadOnlySet<string> EvidenceTypes = new HashSet<string> { "Transfer", "Invoice", "Contract", "Other" };

    /// <summary>A free stage note (an adjustment note is written with the adjustment itself). Returns type and content trimmed.</summary>
    public static (string Type, string Content) ValidateNote(string eventStatus, string? noteType, string? content)
    {
        var type = noteType?.Trim() ?? "";
        if (!NoteTypes.Contains(type) || type == "Adjustment")
            throw new BusinessRuleException("Loại ghi chú không hợp lệ (điều chỉnh dịch vụ được ghi khi lưu thay đổi dịch vụ).");
        var text = content?.Trim() ?? "";
        if (text.Length is < 3 or > 1000) throw new BusinessRuleException("Nội dung ghi chú cần từ 3 đến 1000 ký tự.");
        if (eventStatus == EventStatus.Cancelled) throw new BusinessRuleException("Sự kiện đã hủy.");
        return (type, text);
    }

    /// <summary>The customer may cancel an active event before its day; the deposit is kept. Returns the reason trimmed.</summary>
    public static string ValidateCancel(string eventStatus, DateOnly eventDate, DateOnly todayVn, string? reason)
    {
        var text = reason?.Trim() ?? "";
        if (text.Length is < 5 or > 500) throw new BusinessRuleException("Hãy ghi lý do hủy (5 đến 500 ký tự).");
        if (!EventStatus.IsActive(eventStatus) || todayVn >= eventDate)
            throw new BusinessRuleException("Chỉ hủy được sự kiện đang chuẩn bị (trước ngày cưới).");
        return text;
    }

    /// <summary>Stage 2 only, before the lock date; after it, changes go through an addon contract.</summary>
    public static void EnsureCanAdjust(string eventStatus, DateOnly eventDate, DateOnly todayVn, EventPolicy policy)
    {
        if (EventStageRules.CanAdjust(eventStatus, eventDate, todayVn, policy)) return;
        throw new BusinessRuleException(eventStatus == EventStatus.Adjusting
            ? $"Đã qua hạn điều chỉnh ({EventStageRules.LockDate(eventDate, policy):dd/MM/yyyy}). Dịch vụ đã khóa, thay đổi thêm hãy tạo hợp đồng phát sinh."
            : "Dịch vụ đã khóa. Thay đổi thêm hãy tạo hợp đồng phát sinh.");
    }

    /// <summary>The lines and the reason of an adjustment. Returns the note and its type.</summary>
    public static (string Note, string NoteType) ValidateAdjustment(IReadOnlyList<AdjustedLine>? lines, string? note, string? noteType)
    {
        if (lines is null || lines.Count == 0) throw new BusinessRuleException("Cần ít nhất một dịch vụ.");
        var text = note?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < 5 || text.Length > 1000)
            throw new BusinessRuleException("Hãy ghi chú khách yêu cầu thay đổi gì (5 đến 1000 ký tự).");
        var type = string.IsNullOrWhiteSpace(noteType) ? "Adjustment" : noteType.Trim();
        if (!AdjustmentNoteTypes.Contains(type)) throw new BusinessRuleException("Loại ghi chú không hợp lệ.");
        if (lines.GroupBy(x => x.CatalogItemId).Any(x => x.Count() > 1)) throw new BusinessRuleException("Mỗi dịch vụ chỉ xuất hiện một lần.");
        if (lines.Any(x => x.Quantity <= 0 || x.UnitPrice < 0 || string.IsNullOrWhiteSpace(x.Description) || x.Description.Trim().Length > 500))
            throw new BusinessRuleException("Mỗi dịch vụ cần mô tả (tối đa 500 ký tự), số lượng dương và đơn giá không âm.");
        return (text, type);
    }

    /// <summary>A service already on the contract may stay even if deactivated since; a new line must be an active service.</summary>
    public static void EnsureServicesAvailable(IEnumerable<int> wanted, IReadOnlySet<int> onContract, IReadOnlyDictionary<int, bool> activeById)
    {
        if (wanted.Any(id => !activeById.ContainsKey(id) || (!onContract.Contains(id) && !activeById[id])))
            throw new BusinessRuleException("Có dịch vụ không còn hoạt động trong bảng dịch vụ.");
    }

    /// <summary>The unit price stays within the price frame in force (when the service has one).</summary>
    public static void EnsureWithinFrame(string serviceName, decimal unitPrice, decimal? min, decimal? max)
    {
        if (min is null || max is null || (unitPrice >= min && unitPrice <= max)) return;
        throw new BusinessRuleException($"Đơn giá của '{serviceName}' phải trong khoảng {min:N0} - {EventStageRules.Vnd(max.Value)}.");
    }

    /// <summary>The new total may not fall below the floor of the value signed in stage 1.</summary>
    public static void EnsureAboveFloor(decimal signedValue, decimal newTotal, EventPolicy policy)
    {
        if (EventStageRules.ValidateAdjustedTotal(signedValue, newTotal, policy) is { } error) throw new BusinessRuleException(error);
    }

    /// <summary>Supplier evidence (stage 3): after the lock; supplier name, type, an optional line of this event, note, a file.</summary>
    public static (string Supplier, string? Note) ValidateEvidence(string eventStatus, string? supplierName, string? evidenceType, bool lineBelongsToEvent,
        string? note, bool hasFile)
    {
        if (eventStatus is not (EventStatus.Locked or EventStatus.Completed))
            throw new BusinessRuleException("Minh chứng nhà cung cấp được tải lên sau khi sự kiện đã khóa dịch vụ (giai đoạn 3).");
        var supplier = supplierName?.Trim() ?? "";
        if (supplier.Length is < 2 or > 150) throw new BusinessRuleException("Tên nhà cung cấp cần từ 2 đến 150 ký tự.");
        if (evidenceType is null || !EvidenceTypes.Contains(evidenceType))
            throw new BusinessRuleException("Loại minh chứng phải là chuyển khoản, hóa đơn, hợp đồng hoặc khác.");
        if (!lineBelongsToEvent) throw new BusinessRuleException("Dịch vụ không thuộc sự kiện này.");
        if (note is { Length: > 500 }) throw new BusinessRuleException("Ghi chú tối đa 500 ký tự.");
        if (!hasFile) throw new BusinessRuleException("Hãy chọn tệp minh chứng.");
        return (supplier, string.IsNullOrWhiteSpace(note) ? null : note.Trim());
    }

    /// <summary>A wrong or replaced supplier evidence is removed while the event is still open (locked or held, not settled).</summary>
    public static void EnsureCanRemoveEvidence(string eventStatus)
    {
        if (eventStatus is not (EventStatus.Locked or EventStatus.Completed))
            throw new BusinessRuleException("Chỉ xóa được minh chứng nhà cung cấp khi sự kiện đã khóa dịch vụ và chưa tất toán.");
    }
}
