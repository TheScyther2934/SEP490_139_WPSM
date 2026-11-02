using Microsoft.EntityFrameworkCore;
using Application.Persistence;

namespace Infrastructure.Persistence.Repositories;

public sealed class AddendumRepository(WpsmDbContext db) : IAddendumRepository
{
    public async Task<IReadOnlyList<AddendumView>?> GetAddendaAsync(string? projectCode, int userId, string? role, CancellationToken cancellationToken = default)
    {
        var projects = db.WeddingProjects.AsNoTracking().Where(x => (role == "Customer" && x.CustomerId == userId) || (role == "Manager" && x.ManagerId == userId));
        if (!string.IsNullOrWhiteSpace(projectCode)) projects = projects.Where(x => x.ProjectCode == projectCode);
        var projectId = await projects.OrderByDescending(x => x.CreatedAt).Select(x => (int?)x.ProjectId).FirstOrDefaultAsync(cancellationToken);
        if (projectId is null) return null;

        var addenda = await db.ContractAddenda.AsNoTracking().AsSplitQuery()
            .Include(x => x.Cr).ThenInclude(x => x.Critems).ThenInclude(x => x.TargetSpecItem).ThenInclude(x => x.CatalogItem)
            .Where(x => x.ProjectId == projectId).OrderByDescending(x => x.AddendumId).ToListAsync(cancellationToken);
        return addenda.Select(x => new AddendumView(x.AddendumCode, x.Cr.Crcode, x.Status, x.TotalVariance, x.SignedAt,
            Guid.TryParse(x.FilePath, out var fileId) ? fileId : null,
            x.Cr.Critems.Select(i => new AddendumItemView(i.TargetSpecItem.CatalogItem.Name, i.OldQuantity, i.OldUnitPrice, i.NewQuantity, i.NewUnitPrice,
                i.DeltaAmount ?? i.NewQuantity * i.NewUnitPrice - i.OldQuantity * i.OldUnitPrice)).ToList())).ToList();
    }
}
