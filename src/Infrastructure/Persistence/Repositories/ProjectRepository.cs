using Microsoft.EntityFrameworkCore;
using Application.Persistence;

namespace Infrastructure.Persistence.Repositories;

/// <summary>Events, read side: lists, details, access checks, halls with free slots, the deposit receipt. Signing and the deposit are use cases (Application/UseCases/EventBooking).</summary>
public sealed class ProjectRepository(WpsmDbContext db, ProjectReadModel readModel) : IProjectRepository
{
    public Task<bool> CanAccessAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.AnyAsync(x => x.ProjectCode == projectCode &&
            (role == "Admin" || role == "Manager" && (x.ManagerId == userId || x.ProjectManagers.Any(pm => pm.ManagerId == userId && pm.RemovedAt == null)) || role == "Customer" && x.CustomerId == userId), cancellationToken);

    public async Task<IReadOnlyList<WeddingProjectView>> GetProjectsAsync(int? userId = null, string? role = null, CancellationToken cancellationToken = default)
    {
        var query = readModel.ProjectViewsQuery();
        if (role == "Customer") query = query.Where(x => x.CustomerId == userId);
        else if (role == "Manager") query = query.Where(x => (x.ManagerId == userId || x.ProjectManagers.Any(pm => pm.ManagerId == userId && pm.RemovedAt == null)));
        var projects = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        var result = new List<WeddingProjectView>(projects.Count);
        foreach (var item in projects) result.Add(await readModel.MapProjectAsync(item, cancellationToken));
        return result;
    }

    public async Task<WeddingProjectView?> GetProjectAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await readModel.ProjectViewsQuery().FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
        return project is null ? null : await readModel.MapProjectAsync(project, cancellationToken);
    }

    public async Task<IReadOnlyList<HallView>> GetHallsAsync(DateOnly? date, CancellationToken cancellationToken = default) =>
        await db.Halls.AsNoTracking().Where(h => h.Status == "Active").OrderBy(h => h.Name)
            .Select(h => new HallView(h.HallId, h.Name, h.MinCapacity, h.MaxCapacity, h.BasePrice, h.Status,
                h.Slots.Select(s => new SlotView(s.SlotId, s.SlotName, s.StartTime, s.EndTime,
                    date == null || !s.WeddingProjects.Any(p => p.EventDate == date && p.Status != "Cancelled"))).ToList()))
            .ToListAsync(cancellationToken);

    public Task<StoredFileRef?> GetDepositReceiptAsync(string projectCode, CancellationToken cancellationToken = default) =>
        db.Payments.AsNoTracking()
            .Where(p => p.Project.ProjectCode == projectCode && p.PaymentType == "Deposit" && p.ReceiptFilePath != null)
            .Select(p => new StoredFileRef(p.ReceiptFilePath!, p.ReceiptContentType ?? "application/octet-stream", p.ReceiptFileName ?? "Dat_coc_" + projectCode))
            .FirstOrDefaultAsync(cancellationToken);
}
