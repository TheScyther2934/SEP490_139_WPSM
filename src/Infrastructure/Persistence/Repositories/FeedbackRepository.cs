using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>UC-14..UC-16, UC-35, UC-36 (GBR-10). Admin decisions on escalations stay in AdminRepository (UC-46).</summary>
public sealed class FeedbackRepository(WpsmDbContext db) : IFeedbackRepository
{
    private static readonly string[] Modules = ["Hall", "Menu", "Decor", "MC"];
    private static readonly string[] AfterSettlement = ["Settled", "Closed"];

    public async Task<PostEventView?> GetPostEventAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
        if (project is null) return null;
        var ratings = await db.Ratings.AsNoTracking().Where(x => x.ProjectId == project.ProjectId).OrderBy(x => x.RatingId)
            .Select(x => new RatingView(x.Module, x.Stars, x.Comment, x.CreatedAt)).ToListAsync(cancellationToken);
        var versions = await db.Beos.AsNoTracking().Where(x => x.ProjectId == project.ProjectId && x.FrozenAt != null).OrderBy(x => x.Version)
            .Select(x => x.Version).ToListAsync(cancellationToken);
        var settled = AfterSettlement.Contains(project.Status);
        return new PostEventView(project.ProjectCode, project.Status, settled && ratings.Count < Modules.Length, settled && versions.Count > 0,
            ratings.Select(x => x with { CreatedAt = Utc(x.CreatedAt) }).ToList(), versions);
    }

    public async Task<PostEventView?> SubmitRatingsAsync(string projectCode, IReadOnlyList<RatingCommand> ratings, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
        if (project is null) return null;
        if (!AfterSettlement.Contains(project.Status)) throw new InvalidOperationException("Chỉ đánh giá được sau khi dự án đã quyết toán (GBR-10).");
        if (ratings.Count == 0) throw new InvalidOperationException("Hãy chấm sao ít nhất một hạng mục.");
        var existing = await db.Ratings.Where(x => x.ProjectId == project.ProjectId).Select(x => x.Module).ToListAsync(cancellationToken);
        foreach (var rating in ratings)
        {
            if (!Modules.Contains(rating.Module)) throw new InvalidOperationException("Hạng mục đánh giá không hợp lệ.");
            if (rating.Stars is < 1 or > 5) throw new InvalidOperationException("Số sao phải từ 1 đến 5.");
            if (rating.Comment?.Trim().Length > 500) throw new InvalidOperationException("Vượt quá độ dài tối đa 500 ký tự.");
            if (existing.Contains(rating.Module) || ratings.Count(x => x.Module == rating.Module) > 1)
                throw new InvalidOperationException("Mỗi hạng mục chỉ được đánh giá một lần và không sửa được (GBR-10).");
            db.Ratings.Add(new Rating
            {
                ProjectId = project.ProjectId, Module = rating.Module, Stars = (byte)rating.Stars,
                Comment = string.IsNullOrWhiteSpace(rating.Comment) ? null : rating.Comment.Trim(), CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetPostEventAsync(projectCode, cancellationToken);
    }

    public async Task<IReadOnlyList<ComplaintSummaryView>> ListComplaintsAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var rows = await db.Complaints.AsNoTracking().Where(x => x.Project.ProjectCode == projectCode).OrderByDescending(x => x.CreatedAt)
            .Select(x => new ComplaintSummaryView(x.ComplaintCode, x.Beo.Version, x.Description, x.Status, x.CreatedAt, x.ResolvedAt))
            .ToListAsync(cancellationToken);
        return rows.Select(x => x with { CreatedAt = Utc(x.CreatedAt), ResolvedAt = x.ResolvedAt is { } r ? Utc(r) : null }).ToList();
    }

    public async Task<ComplaintDetailView?> GetComplaintAsync(string projectCode, string complaintCode, CancellationToken cancellationToken = default)
    {
        var complaint = await db.Complaints.AsNoTracking().Include(x => x.Project).ThenInclude(x => x.Customer)
            .Include(x => x.Beo).ThenInclude(x => x.BeodepartmentSheets)
            .FirstOrDefaultAsync(x => x.ComplaintCode == complaintCode && x.Project.ProjectCode == projectCode, cancellationToken);
        if (complaint is null) return null;
        var photos = await db.ExecutionPhotos.AsNoTracking().Where(x => x.DayOfTask.ProjectId == complaint.ProjectId).OrderBy(x => x.UploadedAt)
            .Select(x => new DayOfPhotoView(x.PhotoId, x.UploadedAt)).ToListAsync(cancellationToken);
        return new ComplaintDetailView(complaint.ComplaintCode, complaint.Project.ProjectCode, complaint.Project.Customer.FullName, complaint.Beo.Version,
            complaint.Description, complaint.Status, complaint.EscalationNote, complaint.ResolutionNote, Utc(complaint.CreatedAt),
            complaint.EscalatedAt is { } e ? Utc(e) : null, complaint.ResolvedAt is { } r ? Utc(r) : null,
            complaint.Beo.BeodepartmentSheets.Select(x => new BeoSheetView(x.Department, Utc(x.GeneratedAt), Guid.TryParse(x.PdfFilePath, out var id) ? id : null)).ToList(),
            photos.Select(x => x with { UploadedAt = Utc(x.UploadedAt) }).ToList());
    }

    public async Task<ComplaintDetailView> FileComplaintAsync(string projectCode, int beoVersion, string description, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy dự án.");
        if (!AfterSettlement.Contains(project.Status)) throw new InvalidOperationException("Chỉ gửi khiếu nại sau khi dự án đã quyết toán.");
        description = description?.Trim() ?? "";
        if (description.Length == 0) throw new InvalidOperationException("Trường Mô tả là bắt buộc.");
        if (description.Length > 1000) throw new InvalidOperationException("Vượt quá độ dài tối đa 1000 ký tự.");
        var beo = await db.Beos.FirstOrDefaultAsync(x => x.ProjectId == project.ProjectId && x.Version == beoVersion && x.FrozenAt != null, cancellationToken)
            ?? throw new InvalidOperationException("Trường Phiên bản BEO là bắt buộc.");
        var complaint = new Complaint
        {
            ComplaintCode = await NextCodeAsync(db.Complaints.Select(x => x.ComplaintCode), "CPL", cancellationToken),
            ProjectId = project.ProjectId, Beoid = beo.Beoid, Description = description, Status = "Open", CreatedAt = DateTime.UtcNow
        };
        db.Complaints.Add(complaint);
        Notify(db, project.ManagerId, "Complaint", $"Khách hàng gửi khiếu nại {complaint.ComplaintCode} cho dự án {project.ProjectCode}.",
            Links.ManagerComplaint(project.ProjectCode, complaint.ComplaintCode));
        await db.SaveChangesAsync(cancellationToken);
        return (await GetComplaintAsync(projectCode, complaint.ComplaintCode, cancellationToken))!;
    }

    public async Task<ComplaintDetailView?> ResolveAsync(string projectCode, string complaintCode, int managerId, string response, CancellationToken cancellationToken = default)
    {
        var complaint = await LoadAsync(projectCode, complaintCode, cancellationToken);
        if (complaint is null) return null;
        if (complaint.Status != "Open") throw new InvalidOperationException("Chỉ xử lý được khiếu nại đang mở.");
        response = response?.Trim() ?? "";
        if (response.Length == 0) throw new InvalidOperationException("Trường Phản hồi là bắt buộc.");
        if (response.Length > 1000) throw new InvalidOperationException("Vượt quá độ dài tối đa 1000 ký tự.");
        complaint.Status = "Resolved";
        complaint.ResolutionNote = response;
        complaint.ResolvedBy = managerId;
        complaint.ResolvedAt = DateTime.UtcNow;
        Notify(db, complaint.Project.CustomerId, "Complaint", $"Khiếu nại {complaint.ComplaintCode} đã được giải quyết.", Links.CustomerComplaint(complaint.ComplaintCode));
        await db.SaveChangesAsync(cancellationToken);
        return await GetComplaintAsync(projectCode, complaintCode, cancellationToken);
    }

    public async Task<ComplaintDetailView?> EscalateAsync(string projectCode, string complaintCode, string note, CancellationToken cancellationToken = default)
    {
        var complaint = await LoadAsync(projectCode, complaintCode, cancellationToken);
        if (complaint is null) return null;
        if (complaint.Status != "Open") throw new InvalidOperationException("Chỉ chuyển Admin được khiếu nại đang mở.");
        note = note?.Trim() ?? "";
        if (note.Length == 0) throw new InvalidOperationException("Trường Ghi chú chuyển cấp là bắt buộc.");
        if (note.Length > 500) throw new InvalidOperationException("Vượt quá độ dài tối đa 500 ký tự.");
        complaint.Status = "Escalated";
        complaint.EscalationNote = note;
        complaint.EscalatedAt = DateTime.UtcNow;
        Notify(db, complaint.Project.CustomerId, "Complaint", $"Khiếu nại {complaint.ComplaintCode} đã được chuyển lên Admin.", Links.CustomerComplaint(complaint.ComplaintCode));
        // Admin has no notification screen; the escalation appears on A-01 and A-16.
        await db.SaveChangesAsync(cancellationToken);
        return await GetComplaintAsync(projectCode, complaintCode, cancellationToken);
    }

    private Task<Complaint?> LoadAsync(string projectCode, string complaintCode, CancellationToken cancellationToken) =>
        db.Complaints.Include(x => x.Project).FirstOrDefaultAsync(x => x.ComplaintCode == complaintCode && x.Project.ProjectCode == projectCode, cancellationToken);
}
