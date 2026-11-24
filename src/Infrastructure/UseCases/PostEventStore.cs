using Microsoft.EntityFrameworkCore;
using Application.UseCases.PostEvent;
using Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.UseCases;

/// <summary>Ratings and complaints data for the use cases (tracked entities, scoped lookups, complaint codes).</summary>
public sealed class PostEventStore(WpsmDbContext db) : IPostEventStore
{
    public Task<WeddingProject?> GetCustomerEventAsync(int customerId, string? projectCode, CancellationToken cancellationToken = default)
    {
        var query = db.WeddingProjects.Include(p => p.Ratings).Where(p => p.CustomerId == customerId);
        var code = projectCode?.Trim();
        return string.IsNullOrEmpty(code)
            ? query.OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(cancellationToken)
            : query.FirstOrDefaultAsync(p => p.ProjectCode == code, cancellationToken);
    }

    public void Add(Rating rating) => db.Ratings.Add(rating);

    public Task<Beo?> GetSheetAsync(int projectId, int? beoId, CancellationToken cancellationToken = default) =>
        beoId is { } id
            ? db.Beos.FirstOrDefaultAsync(b => b.ProjectId == projectId && b.Beoid == id, cancellationToken)
            : db.Beos.Where(b => b.ProjectId == projectId).OrderByDescending(b => b.Version).FirstOrDefaultAsync(cancellationToken);

    public void Add(Beo sheet) => db.Beos.Add(sheet);

    public Task<int> CountComplaintsAsync(int projectId, CancellationToken cancellationToken = default) =>
        db.Complaints.CountAsync(c => c.ProjectId == projectId, cancellationToken);

    public async Task<IReadOnlyCollection<string>> GetComplaintCodesAsync(string prefix, CancellationToken cancellationToken = default) =>
        (await db.Complaints.Where(c => c.ComplaintCode.StartsWith(prefix)).Select(c => c.ComplaintCode).ToListAsync(cancellationToken)).ToHashSet();

    public void Add(Complaint complaint) => db.Complaints.Add(complaint);

    public Task<Complaint?> GetOperatorComplaintAsync(int managerId, string complaintCode, CancellationToken cancellationToken = default)
    {
        var code = (complaintCode ?? "").Trim();
        return db.Complaints.Include(c => c.Project)
            .FirstOrDefaultAsync(c => (c.Project.ManagerId == managerId || c.Project.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null))
                && (c.ComplaintCode == code || c.ComplaintId.ToString() == code), cancellationToken);
    }

    public Task<Complaint?> GetComplaintAsync(int complaintId, CancellationToken cancellationToken = default) =>
        db.Complaints.Include(c => c.Project).FirstOrDefaultAsync(c => c.ComplaintId == complaintId, cancellationToken);
}
