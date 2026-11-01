using Microsoft.EntityFrameworkCore;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Email;
using WPSM.Infrastructure.Persistence;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.Infrastructure.Persistence.Repositories;

public sealed class SpecificationRoadmapRepository(WpsmDbContext db, ProjectReadModel readModel, IEmailOutbox emailOutbox, EmailOptions emailOptions) : ISpecificationRoadmapRepository
{
    public async Task<WeddingProjectView?> UpdateSpecificationAsync(string projectCode, int managerId, SpecificationCommand command, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.Include(x => x.SpecItems)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode && x.ManagerId == managerId, cancellationToken);
        if (project is null) return null;
        if (project.Status != "Active Baseline") throw new InvalidOperationException("Baseline đã đóng băng và không thể chỉnh sửa. Hãy tạo Change Request (MSG09).");
        if (command.GuestCount < 0) throw new InvalidOperationException("Số lượng khách không hợp lệ.");

        if (command.Items is not null)
        {
            var duplicates = command.Items.GroupBy(x => x.CatalogItemId).Any(x => x.Count() > 1);
            if (duplicates) throw new InvalidOperationException("Mỗi hạng mục chỉ được xuất hiện một lần trong đặc tả.");
            if (command.Items.Any(x => x.Quantity <= 0 || x.UnitPrice < 0 || string.IsNullOrWhiteSpace(x.Description) || x.Description.Trim().Length > 500))
                throw new InvalidOperationException("Đặc tả cần có mô tả (tối đa 500 ký tự), số lượng dương và đơn giá không âm.");

            var catalogIds = command.Items.Select(x => x.CatalogItemId).Distinct().ToArray();
            var catalogItems = await db.ServiceCatalogItems.Where(x => catalogIds.Contains(x.ItemId) && x.Status == "Active")
                .ToDictionaryAsync(x => x.ItemId, cancellationToken);
            if (catalogItems.Count != catalogIds.Length) throw new InvalidOperationException("Đặc tả chứa hạng mục không còn hoạt động trong Catalog.");

            var today = DateOnly.FromDateTime(DateTime.Today);
            var frames = await db.PriceFrames.AsNoTracking().Where(x => catalogIds.Contains(x.ItemId) && x.EffectiveFrom <= today)
                .GroupBy(x => x.ItemId).Select(g => g.OrderByDescending(x => x.EffectiveFrom).First()).ToDictionaryAsync(x => x.ItemId, cancellationToken);
            foreach (var item in command.Items)
            {
                if (frames.TryGetValue(item.CatalogItemId, out var frame) && (item.UnitPrice < frame.MinPrice || item.UnitPrice > frame.MaxPrice))
                    throw new InvalidOperationException($"Đơn giá của '{catalogItems[item.CatalogItemId].Name}' phải nằm trong khoảng {frame.MinPrice:N0} - {frame.MaxPrice:N0} VND (MSG06).");
            }

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
        var project = await db.WeddingProjects.Include(x => x.Customer).Include(x => x.SpecItems).ThenInclude(x => x.CatalogItem)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode && x.ManagerId == managerId, cancellationToken);
        if (project is null) return null;
        if (project.Status != "Active Baseline") throw new InvalidOperationException("Dự án không còn ở trạng thái có thể đóng băng baseline.");

        var required = new[] { "Hall", "Catering", "Decor", "MC", "AV" };
        var configuredCategories = project.SpecItems.Where(x => x.IsBaseline && x.Version == 1).Select(x => x.CatalogItem.Category).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(x => !configuredCategories.Contains(x)).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"Thiếu dịch vụ bắt buộc: {string.Join(", ", missing.Select(WpsmCategoryNames.ToDisplay))} (MSG12).");

        var today = DateOnly.FromDateTime(DateTime.Today);
        var frames = await db.PriceFrames.AsNoTracking().Where(x => x.EffectiveFrom <= today)
            .GroupBy(x => x.ItemId).Select(g => g.OrderByDescending(x => x.EffectiveFrom).First()).ToDictionaryAsync(x => x.ItemId, cancellationToken);
        foreach (var item in project.SpecItems.Where(x => x.IsBaseline && x.Version == 1))
        {
            if (frames.TryGetValue(item.CatalogItemId, out var frame) && (item.UnitPrice < frame.MinPrice || item.UnitPrice > frame.MaxPrice))
                throw new InvalidOperationException($"Đơn giá của '{item.CatalogItem.Name}' nằm ngoài PriceFrame hiện hành.");
        }

        project.BaselineFrozenAt = DateTime.UtcNow;
        project.Status = "Baseline Frozen";
        // Master Roadmap template: SDS IV.4.1.
        var milestones = new[]
        {
            ("T-90", "Rà soát và ký xác nhận hợp đồng gốc", "Customer", -90),
            ("T-90", "Chọn chủ đề và màu sắc tiệc", "Customer", -90),
            ("T-90", "Xác nhận phối hợp với nhà cung cấp", "Manager", -90),
            ("T-30", "Xác nhận số lượng khách cuối cùng", "Customer", -30),
            ("T-30", "Chốt buổi thử thực đơn", "Manager", -30),
            ("T-30", "Duyệt sơ đồ bàn", "Customer", -30),
            ("T-30", "Thanh toán đợt T-30", "Customer", -30),
            ("T-7", "Duyệt và đóng băng BEO", "Manager", -7),
            ("T-7", "Xác nhận lịch trình ngày cưới", "Customer", -7),
            ("T-7", "Kiểm tra phân bổ tài nguyên", "Manager", -7),
            ("Event Day", "Chuẩn bị sảnh và kiểm tra âm thanh", "Manager", 0)
        };
        var existingTasks = await db.RoadmapTasks.Where(x => x.ProjectId == project.ProjectId).Select(x => x.Title).ToListAsync(cancellationToken);
        foreach (var (milestone, title, assignee, days) in milestones)
        {
            if (existingTasks.Contains(title)) continue;
            var dueDate = project.EventDate.AddDays(days);
            db.RoadmapTasks.Add(new RoadmapTask
            {
                ProjectId = project.ProjectId,
                Milestone = milestone,
                Title = title,
                AssigneeRole = assignee,
                DueDate = milestone == "T-90" && dueDate < today ? today : dueDate,
                Status = "Pending"
            });
        }
        db.Notifications.Add(new Notification
        {
            RecipientId = project.CustomerId,
            Type = "Roadmap reminder",
            Message = $"Hợp đồng gốc {project.ProjectCode} đã được chốt. Vui lòng xem đặc tả và lộ trình.",
            LinkUrl = "/C03",
            CreatedAt = DateTime.UtcNow
        });
        emailOutbox.Enqueue(project.Customer.Email, $"Hợp đồng gốc {project.ProjectCode} đã được chốt",
            EmailTemplates.BaselineFrozen(project.Customer.FullName, project.ProjectCode, emailOptions.PublicBaseUrl), "Reminder", project.ProjectId);
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
