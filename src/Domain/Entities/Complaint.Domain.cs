using Domain.Rules;

namespace Domain.Entities;

/// <summary>
/// A complaint: Open → Resolved by the operator, or Open → Escalated → Resolved by the Admin (UC-35, UC-36, UC-46).
/// </summary>
public partial class Complaint
{
    public const string Open = "Open";
    public const string Escalated = "Escalated";
    public const string Resolved = "Resolved";

    /// <summary>
    /// A new open complaint about one coordination sheet version. The sheet is linked through the navigation, so a sheet created in the
    /// same use case gets its key when both are saved.
    /// </summary>
    public static Complaint File(string code, WeddingProject project, int customerId, Beo sheet, string description, DateTime nowUtc) => new()
    {
        ComplaintCode = code,
        ProjectId = project.ProjectId,
        CustomerId = customerId,
        Title = description.Length > 80 ? description[..80] : description,
        Beo = sheet,
        Beoid = sheet.Beoid,
        Description = description,
        Status = Open,
        CreatedAt = nowUtc
    };

    /// <summary>The operator answers an open complaint. Returns the note trimmed.</summary>
    public void ResolveByOperator(int managerId, string? note, DateTime nowUtc)
    {
        var text = EnsureOperatorCanAct(note);
        if (text.Length > PostEventPolicy.MaxComplaintText) throw new BusinessRuleException("Phương án giải quyết không được vượt quá 1000 ký tự.");
        Status = Resolved;
        ResolutionNote = text;
        ResolvedBy = managerId;
        ResolvedAt = nowUtc;
    }

    /// <summary>The operator hands an open complaint to the Admin for the final decision.</summary>
    public void Escalate(string? note, DateTime nowUtc)
    {
        var text = EnsureOperatorCanAct(note);
        if (text.Length > PostEventPolicy.MaxEscalationNote) throw new BusinessRuleException("Ghi chú chuyển cấp không được vượt quá 500 ký tự.");
        Status = Escalated;
        EscalationNote = text;
        EscalatedAt = nowUtc;
    }

    /// <summary>The Admin decides an escalated complaint (UC-46).</summary>
    public void ResolveByAdmin(int adminId, string decision, DateTime nowUtc)
    {
        if (Status != Escalated) throw new BusinessRuleException("Khiếu nại không còn ở trạng thái Escalated.");
        Status = Resolved;
        ResolutionNote = decision;
        ResolvedBy = adminId;
        ResolvedAt = nowUtc;
    }

    /// <summary>The Admin's decision text: required, at most 1000 characters. Returns it trimmed.</summary>
    public static string ValidateAdminDecision(string? decision)
    {
        var text = (decision ?? "").Trim();
        if (text.Length == 0) throw new BusinessRuleException("Trường Quyết định là bắt buộc.");
        if (text.Length > PostEventPolicy.MaxComplaintText) throw new BusinessRuleException("Vượt quá độ dài tối đa 1000 ký tự.");
        return text;
    }

    /// <summary>Only an open complaint is the operator's to act on: an escalated one waits for the Admin.</summary>
    private string EnsureOperatorCanAct(string? note)
    {
        if (Status == Resolved) throw new BusinessRuleException("Khiếu nại này đã được giải quyết hoàn tất.");
        if (Status == Escalated) throw new BusinessRuleException("Khiếu nại này đã được chuyển cấp lên Admin trước đó.");
        var text = (note ?? "").Trim();
        if (text.Length == 0) throw new BusinessRuleException("Vui lòng nhập nội dung phản hồi hoặc lý do chuyển cấp.");
        return text;
    }
}
