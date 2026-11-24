using Domain.Rules;

namespace Domain.Entities;

/// <summary>A special exception request (UC-47, A-18): an operator asks the Admin to allow an over-capacity allocation.</summary>
public partial class ExceptionRequest
{
    public const string ResourceConflict = "ResourceConflict";
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    /// <summary>
    /// Linked through the navigation, so a request for an allocation created in the same use case gets its key when both are saved.
    /// </summary>
    public static ExceptionRequest ForResourceConflict(string requestCode, ResourceAllocation allocation, int requestedBy, string note, DateTime nowUtc) => new()
    {
        RequestCode = requestCode,
        SourceType = ResourceConflict,
        ProjectId = allocation.ProjectId,
        Allocation = allocation,
        RequestedBy = requestedBy,
        RequestNote = note,
        Status = Pending,
        CreatedAt = nowUtc
    };

    /// <summary>The Admin decides once; approving lets the allocation exceed capacity, rejecting frees it.</summary>
    public void Decide(bool approve, int adminId, string note, DateTime nowUtc)
    {
        if (Status != Pending) throw new BusinessRuleException("Yêu cầu ngoại lệ này đã được quyết định.");
        Status = approve ? Approved : Rejected;
        DecisionBy = adminId;
        DecisionNote = note;
        DecidedAt = nowUtc;
        if (Allocation is null) return;
        if (approve) Allocation.ApproveException(adminId, nowUtc, note);
        else Allocation.RejectException();
    }
}
