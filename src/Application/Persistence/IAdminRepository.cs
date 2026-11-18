namespace Application.Persistence;

public sealed record OverdueAlertView(
    int TaskId,
    string ProjectCode,
    string TaskName,
    DateTime Deadline,
    int OverdueMinutes,
    string Status = "Overdue",
    string? Hall = null,
    DateOnly? EventDate = null,
    string? CustomerName = null,
    string? ManagerName = null);
public sealed record ProofPhotoView(int PhotoId, string ProjectCode, string HallName, string TaskName, string TaskStatus, string UploaderName, DateTime UploadedAt)
{
    /// <summary>Signed, short-lived URL filled in by the API layer (img tags cannot send a Bearer token).</summary>
    public string ImageUrl { get; init; } = "";
}
public sealed record PhotoAuditView(int PhotoId, string ProjectCode, string HallName, string SlotName, string TaskName, DateTime Deadline, DateTime UploadedAt, bool IsLate, string UploaderName)
{
    public string ImageUrl { get; init; } = "";
}
public sealed record PhotoFeedFilter(string? ProjectCode, int? HallId, string? TaskStatus, DateOnly? Date);
public sealed record AdminDashboardView(int UpcomingEvents, decimal HallUtilization, decimal NetRevenueThisMonth, int ActiveCrs, int OverdueTasks, int EscalatedComplaints,
    IReadOnlyList<OverdueAlertView> OverdueAlerts, IReadOnlyList<ProofPhotoView> LatestPhotos);

public sealed record HallUtilizationRow(string Hall, int Available, int Booked, decimal Utilization);
public sealed record NetRevenueRow(string Month, decimal BaseValue, decimal CrVariance, decimal Received, decimal Outstanding);
public sealed record CrStatusCount(string Status, int Count);
public sealed record CrTypeRow(string Type, int Count, decimal Amount, decimal? AverageDays);
public sealed record AdminReportView(DateOnly From, DateOnly To, decimal Utilization, decimal NetRevenue, int CrsApproved,
    IReadOnlyList<HallUtilizationRow> Halls, IReadOnlyList<NetRevenueRow> Revenue, IReadOnlyList<CrStatusCount> CrByStatus, IReadOnlyList<CrTypeRow> CrByType);

public sealed record EscalatedComplaintView(int ComplaintId, string Code, string ProjectCode, string Description, string? ManagerNote, DateTime? EscalatedAt, int BeoVersion, int PhotoCount);
public sealed record ExceptionRequestView(int Id, string Code, string SourceType, string Source, string ProjectCode, string Request, string Status,
    string ManagerNote, string Context, string? DecisionNote, DateTime CreatedAt);

/// <summary>Admin screens A-01, A-09..A-18. Admins see aggregates and escalations only (GBR-12).</summary>
public interface IAdminRepository
{
    Task<AdminDashboardView> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<AdminReportView> GetReportAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProofPhotoView>> GetPhotosAsync(PhotoFeedFilter filter, CancellationToken cancellationToken = default);
    Task<PhotoAuditView?> GetPhotoAsync(int photoId, CancellationToken cancellationToken = default);
    Task<string?> GetPhotoPathAsync(int photoId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OverdueAlertView>> GetOverdueAlertsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EscalatedComplaintView>> GetEscalatedComplaintsAsync(CancellationToken cancellationToken = default);
    /// <summary>UC-46. Returns false when the complaint does not exist or is not Escalated.</summary>
    Task<bool> ResolveComplaintAsync(int complaintId, string decision, int adminId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExceptionRequestView>> GetExceptionRequestsAsync(CancellationToken cancellationToken = default);
    /// <summary>UC-47. Returns false when the request does not exist.</summary>
    Task<bool> DecideExceptionAsync(int requestId, bool approve, string note, int adminId, CancellationToken cancellationToken = default);
}
