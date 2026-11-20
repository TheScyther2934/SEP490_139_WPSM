using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Auditing;

/// <summary>Who is acting now. The API reads it from the signed-in user; background jobs have no user (system).</summary>
public interface IAuditActor
{
    int? UserId { get; }
    string? Name { get; }
    string? Email { get; }
    string? Role { get; }
    string? IpAddress { get; }
}

public sealed class SystemAuditActor : IAuditActor
{
    public int? UserId => null;
    public string? Name => "Hệ thống";
    public string? Email => null;
    public string? Role => "System";
    public string? IpAddress => null;
}

/// <summary>
/// Writes one AuditLogs row for every added, modified or deleted entity saved through EF (all writes in this code base go
/// through SaveChanges). Keys of added rows are only known after the save, so rows are captured before and written after.
/// </summary>
public sealed class AuditSaveChangesInterceptor(IAuditActor actor) : SaveChangesInterceptor
{
    // Technical tables: noise for people reading the audit log.
    private static readonly HashSet<Type> Ignored = [typeof(AuditLog), typeof(Notification), typeof(EmailOutboxMessage), typeof(RefreshToken)];
    // Columns that change on their own (reminder stamps, concurrency tokens) or must never be shown.
    private static readonly HashSet<string> SkippedProperties = ["RowVersion", "RemindedAt", "OverdueFlaggedAt"];
    private static readonly HashSet<string> MaskedProperties = ["PasswordHash", "TokenHash", "SecurityStamp"];

    private static readonly Dictionary<Type, (string Impact, string Label)> Kinds = new()
    {
        [typeof(ServiceCatalogItem)] = ("Service", "Dịch vụ"),
        [typeof(PriceFrame)] = ("Service", "Khung giá"),
        [typeof(PolicyConfig)] = ("Policy", "Chính sách"),
        [typeof(User)] = ("Account", "Tài khoản"),
        [typeof(WeddingProject)] = ("Project", "Sự kiện"),
        [typeof(ProjectManager)] = ("Project", "Người điều hành"),
        [typeof(SpecItem)] = ("Project", "Dịch vụ đã đặt"),
        [typeof(StageNote)] = ("Project", "Ghi chú giai đoạn"),
        [typeof(SupplierEvidence)] = ("Upload", "Minh chứng nhà cung cấp"),
        [typeof(SystemSetting)] = ("Policy", "Cài đặt hệ thống"),
        [typeof(DayOfTask)] = ("Project", "Nhiệm vụ ngày cưới"),
        [typeof(Beo)] = ("Project", "Phiếu điều phối"),
        [typeof(BeodepartmentSheet)] = ("Project", "Phiếu điều phối bộ phận"),
        [typeof(ResourceAllocation)] = ("Project", "Phân bổ tài nguyên"),
        [typeof(Resource)] = ("Service", "Tài nguyên"),
        [typeof(Hall)] = ("Service", "Sảnh"),
        [typeof(Slot)] = ("Service", "Ca tiệc"),
        [typeof(Payment)] = ("Payment", "Thanh toán"),
        [typeof(SettlementInvoice)] = ("Payment", "Hóa đơn tất toán"),
        [typeof(SettlementInvoiceLine)] = ("Payment", "Dòng hóa đơn"),
        [typeof(AddonContract)] = ("Addon", "Hợp đồng phát sinh"),
        [typeof(AddonContractItem)] = ("Addon", "Dòng phát sinh"),
        [typeof(ExecutionPhoto)] = ("Upload", "Ảnh minh chứng"),
        [typeof(StoredFile)] = ("Upload", "Tệp tải lên"),
        [typeof(Complaint)] = ("Complaint", "Khiếu nại"),
        [typeof(Rating)] = ("Complaint", "Đánh giá"),
        [typeof(ExceptionRequest)] = ("Exception", "Yêu cầu đặc biệt"),
    };

    private static readonly Dictionary<string, string> Verbs = new() { ["Create"] = "Thêm", ["Update"] = "Sửa", ["Delete"] = "Xóa" };

    // Keep Vietnamese readable (and searchable) in the stored JSON instead of escaped code points.
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private List<Pending> _pending = [];
    private bool _writing;

    private sealed record Pending(EntityEntry Entry, string Action, Dictionary<string, object?> Changes, int? ProjectId);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        WriteAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await WriteAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pending = [];

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending = [];
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (_writing || context is null) return;
        _pending = [];
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (Ignored.Contains(entry.Metadata.ClrType)) continue;
            var action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => null
            };
            if (action is null) continue;

            var changes = new Dictionary<string, object?>();
            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                if (SkippedProperties.Contains(name) || property.Metadata.IsPrimaryKey() && action == "Create") continue;
                // Compare raw values (masked ones all read "***"), show the masked form.
                if (action == "Update" && (!property.IsModified || Same(property.OriginalValue, property.CurrentValue))) continue;
                var oldValue = action == "Create" ? null : Display(name, property.OriginalValue);
                var newValue = action == "Delete" ? null : Display(name, property.CurrentValue);
                if (action == "Update" && MaskedProperties.Contains(name)) newValue = "*** (đã thay đổi)";
                if (action == "Create" && newValue is null) continue;
                changes[name] = action switch
                {
                    "Create" => new { @new = newValue },
                    "Delete" => new { old = oldValue },
                    _ => new { old = oldValue, @new = newValue }
                };
            }
            if (action == "Update" && changes.Count == 0) continue;

            int? projectId = entry.Entity is WeddingProject ? null
                : entry.Properties.FirstOrDefault(p => p.Metadata.Name == "ProjectId")?.CurrentValue as int?;
            _pending.Add(new Pending(entry, action, changes, projectId));
        }
    }

    private async Task WriteAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (_writing || context is not WpsmDbContext db || _pending.Count == 0) return;
        var pending = _pending;
        _pending = [];

        var rows = pending.Select(p =>
        {
            var key = string.Join(",", p.Entry.Metadata.FindPrimaryKey()?.Properties.Select(k => p.Entry.Property(k.Name).CurrentValue) ?? []);
            var projectId = p.Entry.Entity is WeddingProject wp ? wp.ProjectId : p.ProjectId;
            var (impact, label) = Kinds.TryGetValue(p.Entry.Metadata.ClrType, out var kind) ? kind : ("Other", p.Entry.Metadata.ClrType.Name);
            return new AuditLog
            {
                OccurredAt = DateTime.UtcNow,
                UserId = actor.UserId,
                AccountName = actor.Name,
                Email = actor.Email,
                Role = actor.Role,
                Action = p.Action,
                Impact = impact,
                EntityType = p.Entry.Metadata.ClrType.Name,
                EntityId = key.Length > 60 ? key[..60] : key,
                ProjectId = projectId is > 0 ? projectId : null,
                Summary = Truncate($"{Verbs[p.Action]} {label}{(key.Length > 0 ? $" #{key}" : "")}{Describe(p.Entry.Entity)}", 500),
                Changes = p.Changes.Count == 0 ? null : JsonSerializer.Serialize(p.Changes, Json),
                IpAddress = actor.IpAddress
            };
        }).ToList();

        var projectIds = rows.Where(r => r.ProjectId is not null).Select(r => r.ProjectId!.Value).Distinct().ToList();
        _writing = true;
        try
        {
            var codes = projectIds.Count == 0 ? new Dictionary<int, string>()
                : await db.WeddingProjects.AsNoTracking().Where(x => projectIds.Contains(x.ProjectId))
                    .ToDictionaryAsync(x => x.ProjectId, x => x.ProjectCode, cancellationToken);
            foreach (var row in rows)
            {
                if (row.ProjectId is { } id && codes.TryGetValue(id, out var code)) row.ProjectCode = code;
                else if (row.ProjectId is not null && !codes.ContainsKey(row.ProjectId.Value)) row.ProjectId = null; // project row itself deleted
            }
            db.AuditLogs.AddRange(rows);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _writing = false;
        }
    }

    // A short human description next to the id, so the list is readable without opening the details.
    private static string Describe(object entity) => entity switch
    {
        WeddingProject x => $" ({x.ProjectCode})",
        ProjectManager x => $" ({x.Manager?.FullName ?? $"Manager {x.ManagerId}"} · {(x.Role == "Primary" ? "Manager chính" : "đồng điều hành")}{(x.RemovedAt is null ? "" : " · gỡ quyền")})",
        User x => $" ({x.FullName} · {x.Email})",
        ServiceCatalogItem x => $" ({x.Name})",
        Payment x => $" ({x.PaymentType} · {x.Status})",
        AddonContract x => $" ({x.AddonCode} · {x.Status})",
        SettlementInvoice x => $" ({x.InvoiceCode} · {x.Status})",
        StageNote x => $" (giai đoạn {x.Stage} · {x.NoteType})",
        SupplierEvidence x => $" ({x.SupplierName} · {x.EvidenceType})",
        Complaint x => $" ({x.ComplaintCode} · {x.Status})",
        ExceptionRequest x => $" ({x.RequestCode} · {x.Status})",
        DayOfTask x => $" ({x.TaskName})",
        StoredFile x => $" ({x.OriginalName})",
        PolicyConfig x => $" ({x.PolicyType})",
        _ => ""
    };

    private static object? Display(string name, object? value) => value switch
    {
        null => null,
        _ when MaskedProperties.Contains(name) => "***",
        byte[] bytes => $"[tệp {bytes.Length:N0} byte]",
        DateTime d => DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("O"),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        TimeOnly t => t.ToString("HH:mm"),
        string s when s.Length > 300 => s[..300] + "…",
        _ => value
    };

    private static bool Same(object? a, object? b) =>
        a is byte[] x && b is byte[] y ? x.AsSpan().SequenceEqual(y) : Equals(a, b);

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
