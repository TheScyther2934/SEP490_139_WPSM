namespace Application.Persistence;

// Admin audit log: who did what, on which object and project, with the values before and after.

public sealed record AuditLogQuery(string? Search, string? Action, string? Impact, string? Role, int? UserId, string? ProjectCode,
    DateOnly? From, DateOnly? To, int Page = 1, int PageSize = 25);

public sealed record AuditLogRow(long AuditId, DateTime OccurredAt, int? UserId, string? AccountName, string? Email, string? Role, string Action,
    string Impact, string EntityType, string? EntityId, string? ProjectCode, string Summary, string? Changes, string? IpAddress);

public sealed record AuditLogFilterOptions(IReadOnlyList<OptionItem> Users, IReadOnlyList<string> Actions, IReadOnlyList<string> Impacts, IReadOnlyList<string> Roles);

public sealed record LoginAuditCommand(int UserId, string Name, string Email, string Role, string? IpAddress);

public interface IAuditLogRepository
{
    Task<PagedResult<AuditLogRow>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
    /// <summary>Same filters as SearchAsync, newest first, capped for export.</summary>
    Task<IReadOnlyList<AuditLogRow>> ExportAsync(AuditLogQuery query, int max, CancellationToken cancellationToken = default);
    Task<AuditLogFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
    Task LogLoginAsync(LoginAuditCommand command, CancellationToken cancellationToken = default);
}
