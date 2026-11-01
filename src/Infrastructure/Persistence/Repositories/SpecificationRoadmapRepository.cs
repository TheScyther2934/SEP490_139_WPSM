using Microsoft.EntityFrameworkCore;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Persistence;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.Infrastructure.Persistence.Repositories;

public sealed class SpecificationRoadmapRepository(WpsmDbContext db, ProjectReadModel readModel) : ISpecificationRoadmapRepository
{
    public async Task<WeddingProjectView?> UpdateSpecificationAsync(string projectCode, int managerId, SpecificationCommand command, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.Include(x => x.SpecItems)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode && x.ManagerId == managerId, cancellationToken);
        if (project is null) return null;
        if (project.Status != "Active Baseline") throw new InvalidOperationException("Baseline đã đóng băng; hãy tạo Change Request để thay đổi.");
        if (command.GuestCount < 0) throw new InvalidOperationException("Số lượng khách không hợp lệ.");

        if (command.Items is not null)
        {
            var duplicates = command.Items.GroupBy(x => x.CatalogItemId).Any(x => x.Count() > 1);
            if (duplicates) throw new InvalidOperationException("Mỗi hạng mục chỉ được xuất hiện một lần trong đặc tả.");
            if (command.Items.Any(x => x.Quantity <= 0 || x.UnitPrice < 0 || string.IsNullOrWhiteSpace(x.Description)))
                throw new InvalidOperationException("Đặc tả cần có mô tả, số lượng dương và đơn giá không âm.");

            var catalogIds = command.Items.Select(x => x.CatalogItemId).Distinct().ToArray();
            var validIds = await db.ServiceCatalogItems.Where(x => catalogIds.Contains(x.ItemId) && x.Status == "Active")
                .Select(x => x.ItemId).ToListAsync(cancellationToken);
            if (validIds.Count != catalogIds.Length) throw new InvalidOperationException("Đặc tả chứa hạng mục không còn hoạt động trong Catalog.");

            var incomingIds = command.Items.Select(x => x.CatalogItemId).ToHashSet();
            db.SpecItems.RemoveRange(project.SpecItems.Where(x => !incomingIds.Contains(x.CatalogItemId)));
            foreach (var item in command.Items)
            {
                var existing = project.SpecItems.SingleOrDefault(x => x.CatalogItemId == item.CatalogItemId && x.Version == 1);
                if (existing is null)
                {
                    project.SpecItems.Add(new SpecItem
                    {
                        ProjectId = project.ProjectId,
                        CatalogItemId = item.CatalogItemId,
                        Description = item.Description.Trim(),
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        IsBaseline = true,
                        Version = 1
                    });
                }
                else
                {
                    existing.Description = item.Description.Trim();
                    existing.Quantity = item.Quantity;
                    existing.UnitPrice = item.UnitPrice;
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        var loaded = await readModel.ProjectViewsQuery().SingleAsync(x => x.ProjectId == project.ProjectId, cancellationToken);
        return await readModel.MapProjectAsync(loaded, cancellationToken);
    }

    public async Task<WeddingProjectView?> FreezeBaselineAsync(string projectCode, int managerId, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.Include(x => x.SpecItems).ThenInclude(x => x.CatalogItem)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode && x.ManagerId == managerId, cancellationToken);
        if (project is null) return null;
        if (project.Status != "Active Baseline") throw new InvalidOperationException("Dự án không còn ở trạng thái có thể đóng băng baseline.");

        var required = new[] { "Hall", "Catering", "Decor", "MC", "AV" };
        var configuredCategories = project.SpecItems.Where(x => x.IsBaseline && x.Version == 1).Select(x => x.CatalogItem.Category).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(x => !configuredCategories.Contains(x)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"Cần cấu hình đủ 5 hạng mục nền tảng trước khi đóng băng: {string.Join(", ", missing)}.");
        if (project.SpecItems.Any(x => x.IsBaseline && x.Version == 1 && x.CatalogItem.Status != "Active"))
            throw new InvalidOperationException("Đặc tả đang chứa dịch vụ đã ngừng hoạt động trong Catalog. Hãy thay thế bằng dịch vụ đang hoạt động trước khi đóng băng.");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var frames = await db.PriceFrames.AsNoTracking().Where(x => x.EffectiveFrom <= today)
            .GroupBy(x => x.ItemId).Select(g => g.OrderByDescending(x => x.EffectiveFrom).First()).ToDictionaryAsync(x => x.ItemId, cancellationToken);
        foreach (var item in project.SpecItems.Where(x => x.IsBaseline && x.Version == 1))
        {
            if (!frames.TryGetValue(item.CatalogItemId, out var frame))
                throw new InvalidOperationException($"'{item.CatalogItem.Name}' chưa có PriceFrame hiện hành.");
            if (item.UnitPrice < frame.MinPrice || item.UnitPrice > frame.MaxPrice)
                throw new InvalidOperationException($"Đơn giá của '{item.CatalogItem.Name}' nằm ngoài PriceFrame hiện hành.");
        }

        project.BaselineFrozenAt = DateTime.UtcNow;
        project.Status = "Baseline Frozen";
        var milestones = new[]
        {
            ("T-90", "Chốt danh sách dịch vụ", "Customer", -90),
            ("T-30", "Xác nhận số lượng khách", "Customer", -30),
            ("T-7", "Đóng băng BEO", "Manager", -7),
            ("Event Day", "Điều phối ngày tiệc", "Manager", 0)
        };
        foreach (var (milestone, title, assignee, days) in milestones)
        {
            if (!await db.RoadmapTasks.AnyAsync(x => x.ProjectId == project.ProjectId && x.Milestone == milestone, cancellationToken))
                db.RoadmapTasks.Add(new RoadmapTask
                {
                    ProjectId = project.ProjectId,
                    Milestone = milestone,
                    Title = title,
                    AssigneeRole = assignee,
                    DueDate = project.EventDate.AddDays(days),
                    Status = "Pending"
                });
        }
        await db.SaveChangesAsync(cancellationToken);
        var loaded = await readModel.ProjectViewsQuery().SingleAsync(x => x.ProjectId == project.ProjectId, cancellationToken);
        return await readModel.MapProjectAsync(loaded, cancellationToken);
    }

    public async Task<IReadOnlyList<RoadmapTaskView>> GetRoadmapAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var projectId = await db.WeddingProjects.AsNoTracking().Where(x => x.ProjectCode == projectCode)
            .Select(x => (int?)x.ProjectId).FirstOrDefaultAsync(cancellationToken);
        if (projectId is null) return [];
        return await db.RoadmapTasks.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.DueDate)
            .Select(x => new RoadmapTaskView(x.Milestone, x.Title, x.AssigneeRole, x.DueDate, x.Status))
            .ToListAsync(cancellationToken);
    }
}
