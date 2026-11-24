using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(WpsmDbContext db) : IAuditLogRepository
{
    private static readonly string[] Actions = ["Create", "Update", "Delete", "Login"];
    private static readonly string[] Impacts = ["Project", "Service", "Upload", "Account", "Payment", "Addon", "Policy", "Complaint", "Exception", "Other"];
    private static readonly string[] Roles = ["Admin", "Manager", "Customer", "System"];
    private static readonly TimeZoneInfo VnTz = Domain.Services.VietnamTime.VietnamTimeZone;

    // Dates are picked in Vietnam time; OccurredAt is stored in UTC.
    private static DateTime StartUtc(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), VnTz);

    private IQueryable<AuditLog> Filter(AuditLogQuery q)
    {
        var logs = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            var like = SqlLike.Contains(term);
            logs = logs.Where(x => EF.Functions.ILike(x.Summary, like) || (x.AccountName != null && EF.Functions.ILike(x.AccountName, like))
                || (x.Email != null && EF.Functions.ILike(x.Email, like)) || (x.ProjectCode != null && EF.Functions.ILike(x.ProjectCode, like))
                || (x.Changes != null && EF.Functions.ILike(x.Changes, like)));
        }
        if (!string.IsNullOrWhiteSpace(q.Action)) logs = logs.Where(x => x.Action == q.Action);
        if (!string.IsNullOrWhiteSpace(q.Impact)) logs = logs.Where(x => x.Impact == q.Impact);
        if (!string.IsNullOrWhiteSpace(q.Role)) logs = q.Role == "System" ? logs.Where(x => x.UserId == null) : logs.Where(x => x.Role == q.Role);
        if (q.UserId is { } userId) logs = logs.Where(x => x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(q.ProjectCode)) logs = logs.Where(x => x.ProjectCode == q.ProjectCode.Trim());
        if (q.From is { } from) { var start = StartUtc(from); logs = logs.Where(x => x.OccurredAt >= start); }
        if (q.To is { } to) { var end = StartUtc(to.AddDays(1)); logs = logs.Where(x => x.OccurredAt < end); }
        return logs.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.AuditId);
    }

    private static AuditLogRow Row(AuditLog x) => new(x.AuditId, DateTime.SpecifyKind(x.OccurredAt, DateTimeKind.Utc), x.UserId, x.AccountName, x.Email,
        x.UserId is null ? "System" : x.Role, x.Action, x.Impact, x.EntityType, x.EntityId, x.ProjectCode, x.Summary, x.Changes, x.IpAddress);

    public async Task<PagedResult<AuditLogRow>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var logs = Filter(query);
        var pageSize = Math.Clamp(query.PageSize, 10, 100);
        var total = await logs.CountAsync(cancellationToken);
        var page = Math.Clamp(query.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var rows = await logs.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AuditLogRow>(rows.Select(Row).ToList(), total, page, pageSize);
    }

    public async Task<IReadOnlyList<AuditLogRow>> ExportAsync(AuditLogQuery query, int max, CancellationToken cancellationToken = default) =>
        (await Filter(query).Take(max).ToListAsync(cancellationToken)).Select(Row).ToList();

    public async Task<AuditLogFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        // Only people who appear in the log, so the dropdown stays short.
        var users = await db.Users.AsNoTracking().Where(u => db.AuditLogs.Any(a => a.UserId == u.UserId)).OrderBy(u => u.FullName)
            .Select(u => new OptionItem(u.UserId, u.FullName + " (" + u.Email + ")")).ToListAsync(cancellationToken);
        return new AuditLogFilterOptions(users, Actions, Impacts, Roles);
    }

    public async Task LogLoginAsync(LoginAuditCommand command, CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow, UserId = command.UserId, AccountName = command.Name, Email = command.Email, Role = command.Role,
            Action = "Login", Impact = "Account", EntityType = nameof(User), EntityId = command.UserId.ToString(),
            Summary = $"Đăng nhập hệ thống ({command.Email})", IpAddress = command.IpAddress
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
