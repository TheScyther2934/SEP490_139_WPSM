using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence.Entities;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Coordination tasks of an event, generated from the locked services once the event is locked and again when the
/// coordination sheet is frozen (new lines of confirmed addon contracts). Preparation tasks fall between the lock and the
/// event day (build the stage, receive the furniture...), the event-day timeline follows the slot start.
/// Every task can be completed with proof from the lock onwards. Generating twice adds nothing: each task has a SourceKey.
/// </summary>
public static class CoordinationPlan
{
    /// <summary>
    /// A preparation task. PerItem: one task per service line ({0} = service name), otherwise one per category.
    /// Deadline: DayOffset days from the event day at At (Vietnam time), or MinutesBeforeSlot before the slot start.
    /// </summary>
    private sealed record Rule(string Name, bool PerItem, int DayOffset, TimeOnly? At, int MinutesBeforeSlot, bool ProofRequired);

    private static Rule Day(int dayOffset, string at, string name, bool perItem, bool proof) => new(name, perItem, dayOffset, TimeOnly.Parse(at), 0, proof);
    private static Rule Slot(int minutesBefore, string name, bool perItem, bool proof) => new(name, perItem, 0, null, minutesBefore, proof);

    private static readonly Dictionary<string, Rule[]> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hall"] = [Day(-1, "16:00", "Kiểm tra sảnh, điện nước và lối thoát hiểm", false, true)],
        ["Catering"] =
        [
            Day(-3, "17:00", "Chốt số bàn và thực đơn với bếp", false, true),
            Slot(360, "Nhận nguyên liệu, kiểm tra chất lượng", false, true)
        ],
        ["Decor"] = [Day(-1, "18:00", "Dựng và lắp đặt: {0}", true, true)],
        ["Furniture"] = [Day(-1, "17:00", "Nhận và kê: {0}", true, true)],
        ["AV"] = [Day(-1, "20:00", "Lắp đặt: {0}", true, true)],
        ["MC"] = [Day(-3, "17:00", "Gửi kịch bản chương trình cho MC", false, true)],
        ["Photo/Video"] = [Day(-3, "17:00", "Chốt kịch bản quay chụp với ê-kíp", false, false)],
        ["Performance"] = [Day(-1, "19:00", "Tổng duyệt: {0}", true, true)],
        ["Attire"] = [Day(-2, "17:00", "Nhận và thử: {0}", true, true)],
        ["Makeup"] = [Day(-1, "12:00", "Xác nhận giờ trang điểm với chuyên viên", false, false)],
        ["Transport"] = [Day(-1, "12:00", "Xác nhận lịch đón và lộ trình xe hoa", false, false)],
        ["Ceremony"] = [Slot(240, "Nhận và kiểm tra lễ vật: {0}", true, true)]
    };

    /// <summary>Adds the missing tasks of the project (not saved). Returns how many were added.</summary>
    public static async Task<int> EnsureTasksAsync(WpsmDbContext db, int projectId, CancellationToken cancellationToken)
    {
        var project = await db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(x => x.Slot)
            .Include(x => x.SpecItems).ThenInclude(x => x.CatalogItem)
            .Include(x => x.AddonContracts).ThenInclude(x => x.Items).ThenInclude(x => x.CatalogItem)
            .FirstAsync(x => x.ProjectId == projectId, cancellationToken);
        var existing = (await db.DayOfTasks.Where(x => x.ProjectId == projectId && x.SourceKey != null).Select(x => x.SourceKey!).ToListAsync(cancellationToken))
            .Concat(db.DayOfTasks.Local.Where(x => x.ProjectId == projectId && x.SourceKey != null).Select(x => x.SourceKey!))
            .ToHashSet();

        var slotStart = VnToUtc(project.EventDate, project.Slot.StartTime);
        var earliest = DateTime.UtcNow.AddHours(2);   // a sheet frozen late never starts with overdue tasks
        var added = 0;
        void Add(string key, string name, string department, DateTime deadline, bool proof)
        {
            if (!existing.Add(key)) return;
            db.DayOfTasks.Add(new DayOfTask
            {
                ProjectId = projectId, SourceKey = key, TaskName = name.Length > 200 ? name[..200] : name, Department = department,
                DeadlineTime = deadline < earliest && deadline < slotStart ? Min(earliest, slotStart) : deadline,
                Status = "Pending", PhotoRequired = proof
            });
            added++;
        }
        DateTime DeadlineOf(Rule rule) => rule.At is { } at
            ? VnToUtc(project.EventDate.AddDays(rule.DayOffset), at)
            : slotStart.AddMinutes(-rule.MinutesBeforeSlot);

        var lines = project.SpecItems.OrderBy(x => x.SpecItemId).Select(x => (Key: $"spec:{x.SpecItemId}", x.CatalogItem.Category, x.CatalogItem.Name))
            .Concat(project.AddonContracts.Where(a => a.Status == "Confirmed").SelectMany(a => a.Items).OrderBy(x => x.AddonItemId)
                .Select(x => (Key: $"addon:{x.AddonItemId}", Category: x.CatalogItem?.Category ?? "", Name: x.CatalogItem?.Name ?? x.Description)))
            .ToList();
        foreach (var line in lines)
        {
            if (!Rules.TryGetValue(line.Category, out var rules)) continue;
            for (var i = 0; i < rules.Length; i++)
            {
                var rule = rules[i];
                var key = rule.PerItem ? $"{line.Key}:{i}" : $"cat:{line.Category}:{i}";
                Add(key, string.Format(rule.Name, line.Name), DepartmentOf(line.Category), DeadlineOf(rule), rule.ProofRequired);
            }
        }
        for (var i = 0; i < DayOfTemplate.Length; i++)
        {
            var (offset, name, department, photo) = DayOfTemplate[i];
            Add($"dayof:{i}", name, department, slotStart.AddMinutes(offset), photo);
        }
        return added;
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
