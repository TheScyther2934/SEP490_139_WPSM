using Application.Persistence;
using Domain.Entities;
using Domain.Rules;

namespace Application.UseCases.PostEvent;

/// <summary>Data access for ratings and complaints; implemented in Infrastructure. Entities come back tracked.</summary>
public interface IPostEventStore
{
    /// <summary>The customer's event with its ratings: the one with this code, or the newest when no code is given.</summary>
    Task<WeddingProject?> GetCustomerEventAsync(int customerId, string? projectCode, CancellationToken cancellationToken = default);
    void Add(Rating rating);
    /// <summary>The sheet version a complaint refers to: this one, or the latest when no id is given; null when not found.</summary>
    Task<Beo?> GetSheetAsync(int projectId, int? beoId, CancellationToken cancellationToken = default);
    void Add(Beo sheet);
    Task<int> CountComplaintsAsync(int projectId, CancellationToken cancellationToken = default);
    /// <summary>Complaint codes already used that start with this prefix (codes are unique across events).</summary>
    Task<IReadOnlyCollection<string>> GetComplaintCodesAsync(string prefix, CancellationToken cancellationToken = default);
    void Add(Complaint complaint);
    /// <summary>The complaint (by code or id) with its event, when the caller operates the event; null otherwise.</summary>
    Task<Complaint?> GetOperatorComplaintAsync(int managerId, string complaintCode, CancellationToken cancellationToken = default);
    Task<Complaint?> GetComplaintAsync(int complaintId, CancellationToken cancellationToken = default);
}

/// <summary>Use case (UC-14, C-13): the customer rates the four modules once, after the settlement (GBR-10).</summary>
public sealed class SubmitRatings(IPostEventStore store, IComplaintRatingRepository reads, IUnitOfWork unitOfWork)
{
    public async Task<PostEventSummaryView?> HandleAsync(int customerId, string? projectCode, IReadOnlyList<RatingInput>? ratings,
        CancellationToken cancellationToken = default)
    {
        var project = await store.GetCustomerEventAsync(customerId, projectCode, cancellationToken);
        if (project is null) return null;
        var checkedRatings = PostEventPolicy.ValidateRatings(project.Status, project.Ratings.Count > 0, ratings);
        var now = DateTime.UtcNow;
        foreach (var rating in checkedRatings) store.Add(Rating.Submit(project.ProjectId, rating, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await reads.GetPostEventSummaryAsync(customerId, project.ProjectCode, cancellationToken);
    }
}

/// <summary>Use case (UC-15, C-15, MSG25): the customer files a complaint about a coordination sheet version, after the event.</summary>
public sealed class FileComplaint(IPostEventStore store, IComplaintRatingRepository reads, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<ComplaintView> HandleAsync(int customerId, string? projectCode, int? beoId, string? description, CancellationToken cancellationToken = default)
    {
        var project = await store.GetCustomerEventAsync(customerId, projectCode, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy sự kiện của khách hàng.");
        var text = PostEventPolicy.ValidateComplaint(project.Status, description);

        var sheet = await store.GetSheetAsync(project.ProjectId, beoId, cancellationToken);
        if (sheet is null && beoId is not null) throw new BusinessRuleException("Phiên bản phiếu điều phối không thuộc sự kiện này.");
        if (sheet is null)
        {
            // An event without any sheet still gets one to point at, so the complaint keeps its reference.
            sheet = new Beo { ProjectId = project.ProjectId, Version = 1, Status = "Draft", Notes = "BEO mặc định phục vụ đối chiếu khiếu nại." };
            store.Add(sheet);
        }

        var suffix = project.ProjectCode.Split('-').LastOrDefault() ?? project.ProjectCode;
        var code = PostEventPolicy.NextComplaintCode(project.ProjectCode, await store.CountComplaintsAsync(project.ProjectId, cancellationToken),
            await store.GetComplaintCodesAsync($"CMP-{suffix}-", cancellationToken));
        store.Add(Complaint.File(code, project, customerId, sheet, text, DateTime.UtcNow));
        await messages.ComplaintFiledAsync(project.ManagerId, project.ProjectCode, code, sheet.Version, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await reads.GetCustomerComplaintAsync(customerId, code, cancellationToken))!;
    }
}

/// <summary>Use case (UC-35, UC-36, M-19): the operator resolves an open complaint or escalates it to the Admin.</summary>
public sealed class RespondToComplaint(IPostEventStore store, IComplaintRatingRepository reads, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public const string Resolve = "Resolve";
    public const string Escalate = "Escalate";

    public async Task<ComplaintView?> HandleAsync(int managerId, string complaintCode, string? action, string? note, CancellationToken cancellationToken = default)
    {
        var complaint = await store.GetOperatorComplaintAsync(managerId, complaintCode, cancellationToken);
        if (complaint is null) return null;
        var now = DateTime.UtcNow;
        var kind = (action ?? "").Trim();
        if (string.Equals(kind, Resolve, StringComparison.OrdinalIgnoreCase))
        {
            complaint.ResolveByOperator(managerId, note, now);
            await messages.ComplaintResolvedByOperatorAsync(complaint.Project.CustomerId, complaint.ComplaintCode, cancellationToken);
        }
        else if (string.Equals(kind, Escalate, StringComparison.OrdinalIgnoreCase))
        {
            complaint.Escalate(note, now);
            await messages.ComplaintEscalatedAsync(complaint.Project.CustomerId, complaint.Project.ProjectCode, complaint.ComplaintCode, cancellationToken);
        }
        else throw new BusinessRuleException("Thao tác không hợp lệ. Chỉ chấp nhận 'Resolve' hoặc 'Escalate'.");

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await reads.GetManagerComplaintAsync(managerId, complaint.ComplaintCode, cancellationToken);
    }
}

/// <summary>Use case (UC-46, A-17): the Admin decides an escalated complaint; the operator and the customer are told.</summary>
public sealed class ResolveEscalatedComplaint(IPostEventStore store, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(int complaintId, string? decision, int adminId, CancellationToken cancellationToken = default)
    {
        var text = Complaint.ValidateAdminDecision(decision);
        var complaint = await store.GetComplaintAsync(complaintId, cancellationToken);
        if (complaint is null) return false;
        complaint.ResolveByAdmin(adminId, text, DateTime.UtcNow);
        await messages.ComplaintResolvedByAdminAsync(complaint.Project.ManagerId, complaint.Project.CustomerId, complaint.Project.ProjectCode,
            complaint.ComplaintCode, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
