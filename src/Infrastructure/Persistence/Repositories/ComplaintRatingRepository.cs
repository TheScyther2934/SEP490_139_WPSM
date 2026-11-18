using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class ComplaintRatingRepository(WpsmDbContext db) : IComplaintRatingRepository
{
    private static readonly (string Code, string Label)[] RatingModules =
    [
        ("Hall", "Sảnh tiệc & Không gian"),
        ("Menu", "Thực đơn & Ẩm thực"),
        ("Decor", "Trang trí & Hoa tươi"),
        ("MC", "MC & Điều phối chương trình")
    ];

    public async Task<PostEventSummaryView?> GetPostEventSummaryAsync(int customerId, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var project = await ResolveCustomerProjectAsync(customerId, projectCode, cancellationToken);
        return project is null ? null : MapPostEventSummary(project);
    }

    public async Task<PostEventSummaryView?> SubmitRatingsAsync(
        int customerId,
        string? projectCode,
        IReadOnlyList<ModuleRatingInput> ratings,
        CancellationToken cancellationToken = default)
    {
        var project = await ResolveCustomerProjectAsync(customerId, projectCode, cancellationToken);
        if (project is null) return null;

        // GBR-10: Ratings are immutable once submitted
        if (project.Ratings.Count > 0)
            throw new InvalidOperationException("Đánh giá dịch vụ đã được gửi trước đó và không thể chỉnh sửa (GBR-10 / MSG26).");

        if (ratings is null || ratings.Count == 0)
            throw new InvalidOperationException("Vui lòng đánh giá đầy đủ 4 hạng mục dịch vụ (MSG16).");

        var validCodes = RatingModules.Select(m => m.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<string, ModuleRatingInput>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in ratings)
        {
            var code = RatingModules.FirstOrDefault(m => string.Equals(m.Code, item.Module?.Trim(), StringComparison.OrdinalIgnoreCase)).Code;
            if (code is null || !validCodes.Contains(code))
                throw new InvalidOperationException($"Hạng mục đánh giá '{item.Module}' không hợp lệ. Chỉ chấp nhận Hall, Menu, Decor, MC.");
            if (item.Stars is < 1 or > 5)
                throw new InvalidOperationException("Số sao đánh giá cho mỗi hạng mục phải từ 1 đến 5 sao (MSG16).");
            if (item.Comment is { Length: > 500 })
                throw new InvalidOperationException("Nhận xét cho mỗi hạng mục tối đa 500 ký tự.");
            normalized[code] = item with { Module = code };
        }

        if (normalized.Count < RatingModules.Length)
            throw new InvalidOperationException("Vui lòng chọn số sao (1–5) cho cả 4 hạng mục: Sảnh tiệc, Thực đơn, Trang trí và MC (MSG16).");

        var now = DateTime.UtcNow;
        foreach (var (code, _) in RatingModules)
        {
            var input = normalized[code];
            db.Ratings.Add(new Rating
            {
                ProjectId = project.ProjectId,
                Module = code,
                Stars = input.Stars,
                Comment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim(),
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        var reloaded = await ResolveCustomerProjectAsync(customerId, project.ProjectCode, cancellationToken);
        return reloaded is null ? null : MapPostEventSummary(reloaded);
    }

    public async Task<IReadOnlyList<ComplaintView>> GetCustomerComplaintsAsync(int customerId, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = LoadComplaintsQuery().Where(c => c.Project.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(c => c.Project.ProjectCode == projectCode.Trim());

        var rows = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
        return rows.Select(MapComplaintView).ToList();
    }

    public async Task<ComplaintView?> GetCustomerComplaintAsync(int customerId, string complaintCode, CancellationToken cancellationToken = default)
    {
        var code = (complaintCode ?? "").Trim();
        var row = await LoadComplaintsQuery()
            .FirstOrDefaultAsync(c => c.Project.CustomerId == customerId &&
                (c.ComplaintCode == code || c.ComplaintId.ToString() == code), cancellationToken);
        return row is null ? null : MapComplaintView(row);
    }

    public async Task<IReadOnlyList<BeoOptionView>> GetCustomerBeoOptionsAsync(int customerId, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = db.WeddingProjects.AsNoTracking().Where(p => p.CustomerId == customerId);
        var project = !string.IsNullOrWhiteSpace(projectCode)
            ? await query.FirstOrDefaultAsync(p => p.ProjectCode == projectCode.Trim(), cancellationToken)
            : await query.OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        if (project is null) return [];

        return await db.Beos.AsNoTracking()
            .Where(b => b.ProjectId == project.ProjectId)
            .OrderByDescending(b => b.Version)
            .Select(b => new BeoOptionView(b.Beoid, b.Version, b.Status, b.FrozenAt, b.Notes))
            .ToListAsync(cancellationToken);
    }

    public async Task<ComplaintView> CreateCustomerComplaintAsync(
        int customerId,
        string? projectCode,
        int? beoId,
        string description,
        CancellationToken cancellationToken = default)
    {
        var trimmedDesc = (description ?? "").Trim();
        if (trimmedDesc.Length == 0)
            throw new InvalidOperationException("Vui lòng nhập nội dung mô tả khiếu nại (MSG25).");
        if (trimmedDesc.Length > 1000)
            throw new InvalidOperationException("Nội dung khiếu nại không được vượt quá 1000 ký tự.");

        var project = await db.WeddingProjects
            .Include(p => p.Beos)
            .FirstOrDefaultAsync(p => p.CustomerId == customerId &&
                (string.IsNullOrWhiteSpace(projectCode) || p.ProjectCode == projectCode.Trim()), cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy dự án của khách hàng.");

        var targetBeo = beoId.HasValue
            ? project.Beos.FirstOrDefault(b => b.Beoid == beoId.Value)
            : project.Beos.OrderByDescending(b => b.Version).FirstOrDefault();

        if (targetBeo is null)
        {
            targetBeo = new Beo
            {
                ProjectId = project.ProjectId,
                Version = 1,
                Status = "Draft",
                Notes = "BEO mặc định phục vụ đối chiếu khiếu nại."
            };
            db.Beos.Add(targetBeo);
            await db.SaveChangesAsync(cancellationToken);
        }

        var seq = await db.Complaints.CountAsync(c => c.ProjectId == project.ProjectId, cancellationToken) + 1;
        var suffix = project.ProjectCode.Split('-').LastOrDefault() ?? project.ProjectId.ToString();
        var code = $"CMP-{suffix}-{seq:D2}";
        while (await db.Complaints.AnyAsync(c => c.ComplaintCode == code, cancellationToken))
        {
            seq++;
            code = $"CMP-{suffix}-{seq:D2}";
        }

        var now = DateTime.UtcNow;
        var complaint = new Complaint
        {
            ComplaintCode = code,
            ProjectId = project.ProjectId,
            CustomerId = customerId,
            Title = trimmedDesc.Length > 80 ? trimmedDesc[..80] : trimmedDesc,
            Beoid = targetBeo.Beoid,
            Description = trimmedDesc,
            Status = "Open",
            CreatedAt = now
        };
        db.Complaints.Add(complaint);

        db.Notifications.Add(new Notification
        {
            RecipientId = project.ManagerId,
            Type = "Complaint",
            Message = $"Khách hàng dự án {project.ProjectCode} vừa gửi khiếu nại {code} (đối chiếu BEO v{targetBeo.Version}).",
            LinkUrl = $"/M19?id={project.ProjectCode}&complaint={code}",
            IsRead = false,
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        var loaded = await LoadComplaintsQuery().FirstAsync(c => c.ComplaintId == complaint.ComplaintId, cancellationToken);
        return MapComplaintView(loaded);
    }

    public async Task<IReadOnlyList<ComplaintView>> GetManagerComplaintsAsync(int managerId, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var query = LoadComplaintsQuery().Where(c => c.Project.ManagerId == managerId);
        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(c => c.Project.ProjectCode == projectCode.Trim());

        var rows = await query
            .OrderBy(c => c.Status == "Open" ? 0 : c.Status == "Escalated" ? 1 : 2)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(MapComplaintView).ToList();
    }

    public async Task<ComplaintView?> GetManagerComplaintAsync(int managerId, string complaintCode, CancellationToken cancellationToken = default)
    {
        var code = (complaintCode ?? "").Trim();
        var row = await LoadComplaintsQuery()
            .FirstOrDefaultAsync(c => c.Project.ManagerId == managerId &&
                (c.ComplaintCode == code || c.ComplaintId.ToString() == code), cancellationToken);
        return row is null ? null : MapComplaintView(row);
    }

    public async Task<ComplaintView?> RespondOrEscalateComplaintAsync(
        int managerId,
        string complaintCode,
        string action,
        string note,
        CancellationToken cancellationToken = default)
    {
        var code = (complaintCode ?? "").Trim();
        var complaint = await db.Complaints
            .Include(c => c.Project)
            .FirstOrDefaultAsync(c => c.Project.ManagerId == managerId &&
                (c.ComplaintCode == code || c.ComplaintId.ToString() == code), cancellationToken);
        if (complaint is null) return null;

        if (complaint.Status == "Resolved")
            throw new InvalidOperationException("Khiếu nại này đã được giải quyết hoàn tất.");

        var normalizedAction = (action ?? "").Trim();
        var trimmedNote = (note ?? "").Trim();
        if (trimmedNote.Length == 0)
            throw new InvalidOperationException("Vui lòng nhập nội dung phản hồi hoặc lý do chuyển cấp.");

        var now = DateTime.UtcNow;

        if (string.Equals(normalizedAction, "Resolve", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmedNote.Length > 1000)
                throw new InvalidOperationException("Phương án giải quyết không được vượt quá 1000 ký tự.");

            complaint.Status = "Resolved";
            complaint.ResolutionNote = trimmedNote;
            complaint.ResolvedBy = managerId;
            complaint.ResolvedAt = now;

            db.Notifications.Add(new Notification
            {
                RecipientId = complaint.Project.CustomerId,
                Type = "Complaint",
                Message = $"Khiếu nại {complaint.ComplaintCode} đã được Quản lý dự án phản hồi giải quyết.",
                LinkUrl = $"/C16?complaint={complaint.ComplaintCode}",
                IsRead = false,
                CreatedAt = now
            });
        }
        else if (string.Equals(normalizedAction, "Escalate", StringComparison.OrdinalIgnoreCase))
        {
            if (complaint.Status == "Escalated")
                throw new InvalidOperationException("Khiếu nại này đã được chuyển cấp lên Admin trước đó.");
            if (trimmedNote.Length > 500)
                throw new InvalidOperationException("Ghi chú chuyển cấp không được vượt quá 500 ký tự.");

            complaint.Status = "Escalated";
            complaint.EscalationNote = trimmedNote;
            complaint.EscalatedAt = now;

            var admins = await db.Users.AsNoTracking()
                .Where(u => u.Role == "Admin" && u.Status == "Active")
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken);

            foreach (var adminId in admins)
            {
                db.Notifications.Add(new Notification
                {
                    RecipientId = adminId,
                    Type = "Complaint",
                    Message = $"Khiếu nại {complaint.ComplaintCode} của dự án {complaint.Project.ProjectCode} đã được chuyển cấp (Escalated) lên Admin.",
                    LinkUrl = "/A16",
                    IsRead = false,
                    CreatedAt = now
                });
            }

            db.Notifications.Add(new Notification
            {
                RecipientId = complaint.Project.CustomerId,
                Type = "Complaint",
                Message = $"Khiếu nại {complaint.ComplaintCode} đã được chuyển cấp lên Ban Quản Trị (Admin) để ra quyết định cuối cùng.",
                LinkUrl = $"/C16?complaint={complaint.ComplaintCode}",
                IsRead = false,
                CreatedAt = now
            });
        }
        else
        {
            throw new InvalidOperationException("Thao tác không hợp lệ. Chỉ chấp nhận 'Resolve' hoặc 'Escalate'.");
        }

        await db.SaveChangesAsync(cancellationToken);
        var reloaded = await LoadComplaintsQuery().FirstAsync(c => c.ComplaintId == complaint.ComplaintId, cancellationToken);
        return MapComplaintView(reloaded);
    }

    private async Task<WeddingProject?> ResolveCustomerProjectAsync(int customerId, string? projectCode, CancellationToken cancellationToken)
    {
        var query = db.WeddingProjects.AsNoTracking().AsSplitQuery()
            .Include(p => p.Customer)
            .Include(p => p.Hall)
            .Include(p => p.Slot)
            .Include(p => p.Ratings)
            .Include(p => p.Payments)
            .Include(p => p.DayOfTasks)
            .Include(p => p.Complaints)
            .Include(p => p.Beos)
            .Where(p => p.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(projectCode))
            return await query.FirstOrDefaultAsync(p => p.ProjectCode == projectCode.Trim(), cancellationToken);

        return await query.OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<Complaint> LoadComplaintsQuery() =>
        db.Complaints.AsNoTracking().AsSplitQuery()
            .Include(c => c.Beo)
            .Include(c => c.Project).ThenInclude(p => p.Customer)
            .Include(c => c.Project).ThenInclude(p => p.Hall)
            .Include(c => c.Project).ThenInclude(p => p.DayOfTasks).ThenInclude(t => t.ExecutionPhotos)
            .Include(c => c.Project).ThenInclude(p => p.SpecItems).ThenInclude(s => s.CatalogItem)
            .Include(c => c.Project).ThenInclude(p => p.ChangeRequests).ThenInclude(cr => cr.Critems);

    private static PostEventSummaryView MapPostEventSummary(WeddingProject project)
    {
        var existingByModule = project.Ratings.ToDictionary(r => r.Module, StringComparer.OrdinalIgnoreCase);
        var ratings = RatingModules.Select(m =>
        {
            existingByModule.TryGetValue(m.Code, out var r);
            return new ModuleRatingView(
                m.Code,
                m.Label,
                r is null ? null : r.Stars,
                r?.Comment,
                r is null ? null : DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc));
        }).ToList();

        var isLocked = project.Ratings.Count > 0;
        var paidTotal = project.Payments.Where(p => p.Status == "Paid").Sum(p => p.Amount ?? 0m);
        var completedTasks = project.DayOfTasks.Count(t => t.Status == "Done");
        var openComplaints = project.Complaints.Count(c => c.Status != "Resolved");

        return new PostEventSummaryView(
            project.ProjectCode,
            project.Customer.FullName,
            project.Hall.Name,
            project.Slot.SlotName,
            project.EventDate,
            project.Status,
            !isLocked,
            isLocked,
            project.CurrentTotal,
            paidTotal,
            completedTasks,
            project.DayOfTasks.Count,
            openComplaints,
            ratings);
    }

    private static ComplaintView MapComplaintView(Complaint c)
    {
        var project = c.Project;
        var approvedCrItems = project.ChangeRequests
            .Where(cr => cr.Status == "Approved")
            .OrderBy(cr => cr.CreatedAt)
            .SelectMany(cr => cr.Critems)
            .ToList();

        var beoItems = project.SpecItems
            .GroupBy(s => s.CatalogItemId)
            .OrderBy(g => g.Min(s => s.SpecItemId))
            .Select(g =>
            {
                var baseline = g.OrderBy(s => s.Version).First();
                var latest = g.OrderByDescending(s => s.Version).First();
                var crLine = approvedCrItems.LastOrDefault(x => g.Any(s => s.SpecItemId == x.TargetSpecItemId));
                var baselineQty = baseline.Quantity;
                var effectiveQty = crLine?.NewQuantity ?? latest.Quantity;
                var effectivePrice = crLine?.NewUnitPrice ?? latest.UnitPrice;
                var dept = baseline.CatalogItem.Category switch
                {
                    "Catering" => "Kitchen",
                    "Hall" or "Transport" => "Service",
                    "Decor" or "Attire" or "Makeup" => "Decor",
                    _ => "Tech-MC"
                };
                var deptLabel = dept switch
                {
                    "Kitchen" => "Bộ phận Bếp (Kitchen)",
                    "Service" => "Bộ phận Phục vụ bàn & Sảnh (Service)",
                    "Decor" => "Bộ phận Trang trí & Hoa (Decor)",
                    _ => "Bộ phận Kỹ thuật Âm thanh - Ánh sáng & MC (Tech-MC)"
                };
                return new BeoItemLineView(
                    baseline.SpecItemId,
                    baseline.CatalogItem.Name,
                    baseline.CatalogItem.Category,
                    dept,
                    deptLabel,
                    baseline.CatalogItem.Unit,
                    baselineQty,
                    effectiveQty,
                    effectivePrice,
                    effectiveQty * effectivePrice,
                    latest.Description ?? baseline.Description ?? "",
                    crLine is not null || latest.Version > baseline.Version);
            })
            .ToList();

        var photosCount = project.DayOfTasks.SelectMany(t => t.ExecutionPhotos).Count();

        return new ComplaintView(
            c.ComplaintId,
            c.ComplaintCode,
            project.ProjectCode,
            project.Customer.FullName,
            project.Hall.Name,
            project.EventDate,
            c.Beoid,
            c.Beo.Version,
            c.Beo.Status,
            c.Beo.Notes,
            c.Description,
            c.Status,
            c.EscalationNote,
            c.EscalatedAt is { } esc ? DateTime.SpecifyKind(esc, DateTimeKind.Utc) : null,
            c.ResolutionNote,
            c.ResolvedAt is { } res ? DateTime.SpecifyKind(res, DateTimeKind.Utc) : null,
            c.ResolvedBy.HasValue ? (c.EscalatedAt.HasValue ? "Admin" : "Manager") : null,
            DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc),
            photosCount,
            beoItems);
    }
}
