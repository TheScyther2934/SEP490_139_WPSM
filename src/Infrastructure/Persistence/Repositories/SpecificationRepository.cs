using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Stage 2 (adjusting): the customer tells the operator what to change, the operator edits the service lines and writes why.
/// Allowed only before the lock date; the new total may not fall below the agreed floor of the value signed in stage 1.
/// </summary>
public sealed class SpecificationRepository(WpsmDbContext db, ProjectReadModel readModel) : ISpecificationRepository
{
    public async Task<WeddingProjectView?> UpdateSpecificationAsync(string projectCode, int managerId, SpecificationCommand command, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.Include(x => x.SpecItems)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode
                && (x.ManagerId == managerId || x.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)), cancellationToken);
        if (project is null) return null;

        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var today = VietnamTime.GetVietnamToday();
        if (!EventStageRules.CanAdjust(project.Status, project.EventDate, today, policy))
            throw new InvalidOperationException(project.Status == EventStatus.Adjusting
                ? $"Đã qua hạn điều chỉnh ({EventStageRules.LockDate(project.EventDate, policy):dd/MM/yyyy}). Dịch vụ đã khóa, thay đổi thêm hãy tạo hợp đồng phát sinh."
                : "Dịch vụ đã khóa. Thay đổi thêm hãy tạo hợp đồng phát sinh.");
        if (command.Items is null || command.Items.Count == 0) throw new InvalidOperationException("Cần ít nhất một dịch vụ.");
        var note = command.Note?.Trim();
        if (string.IsNullOrEmpty(note) || note.Length < 5 || note.Length > 1000)
            throw new InvalidOperationException("Hãy ghi chú khách yêu cầu thay đổi gì (5 đến 1000 ký tự).");
        var noteType = string.IsNullOrWhiteSpace(command.NoteType) ? "Adjustment" : command.NoteType.Trim();
        if (noteType is not ("Adjustment" or "Tasting" or "Fitting"))
            throw new InvalidOperationException("Loại ghi chú không hợp lệ.");

        if (command.Items.GroupBy(x => x.CatalogItemId).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Mỗi dịch vụ chỉ xuất hiện một lần.");
        if (command.Items.Any(x => x.Quantity <= 0 || x.UnitPrice < 0 || string.IsNullOrWhiteSpace(x.Description) || x.Description.Trim().Length > 500))
            throw new InvalidOperationException("Mỗi dịch vụ cần mô tả (tối đa 500 ký tự), số lượng dương và đơn giá không âm.");

        var catalogIds = command.Items.Select(x => x.CatalogItemId).Distinct().ToArray();
        var catalog = await db.ServiceCatalogItems.Where(x => catalogIds.Contains(x.ItemId)).ToDictionaryAsync(x => x.ItemId, cancellationToken);
        var existingIds = project.SpecItems.Select(s => s.CatalogItemId).ToHashSet();
        // A service already on the contract may stay even if deactivated since; new lines must be active.
        if (catalogIds.Any(id => !catalog.ContainsKey(id) || (!existingIds.Contains(id) && catalog[id].Status != "Active")))
            throw new InvalidOperationException("Có dịch vụ không còn hoạt động trong bảng dịch vụ.");

        var frames = await db.PriceFrames.AsNoTracking().Where(x => catalogIds.Contains(x.ItemId) && x.EffectiveFrom <= today)
            .GroupBy(x => x.ItemId).Select(g => g.OrderByDescending(x => x.EffectiveFrom).First()).ToDictionaryAsync(x => x.ItemId, cancellationToken);
        foreach (var item in command.Items)
            if (frames.TryGetValue(item.CatalogItemId, out var frame) && (item.UnitPrice < frame.MinPrice || item.UnitPrice > frame.MaxPrice))
                throw new InvalidOperationException($"Đơn giá của '{catalog[item.CatalogItemId].Name}' phải trong khoảng {frame.MinPrice:N0} - {EventStageRules.Vnd(frame.MaxPrice)}.");

        var before = project.CurrentTotal;
        var after = command.Items.Sum(i => i.Quantity * i.UnitPrice);
        if (EventStageRules.ValidateAdjustedTotal(project.BaseContractValue, after, policy) is { } floorError)
            throw new InvalidOperationException(floorError);

        var incoming = command.Items.Select(x => x.CatalogItemId).ToHashSet();
        db.SpecItems.RemoveRange(project.SpecItems.Where(x => !incoming.Contains(x.CatalogItemId)));
        foreach (var item in command.Items)
        {
            var line = project.SpecItems.FirstOrDefault(x => x.CatalogItemId == item.CatalogItemId);
            if (line is null)
                project.SpecItems.Add(new SpecItem { ProjectId = project.ProjectId, CatalogItemId = item.CatalogItemId,
                    Description = item.Description.Trim(), Quantity = item.Quantity, UnitPrice = item.UnitPrice });
            else
            {
                line.Description = item.Description.Trim();
                line.Quantity = item.Quantity;
                line.UnitPrice = item.UnitPrice;
            }
        }
        project.CurrentTotal = after;
        db.StageNotes.Add(new StageNote
        {
            ProjectId = project.ProjectId, Stage = EventStageRules.Adjusting, NoteType = noteType, Content = note,
            ValueBefore = before, ValueAfter = after, CreatedBy = managerId, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        var loaded = await readModel.ProjectViewsQuery().SingleAsync(x => x.ProjectId == project.ProjectId, cancellationToken);
        return await readModel.MapProjectAsync(loaded, cancellationToken);
    }
}
