using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Persistence;

namespace Application.Dashboard;

public sealed record ManagerKpiSummary(
    int ActiveProjects,
    int DraftingProjects,
    int EventsIn7Days,
    int OpenChangeRequests,
    int StaleBeos,
    int OverdueTasksCount);

public sealed record ManagerTaskItemView(
    int TaskId,
    string ProjectCode,
    string Title,
    string Milestone,
    DateOnly DueDate,
    string Status,
    bool RequiresProof,
    string TaskType = "Roadmap",
    DateTime? DeadlineTime = null);

public sealed record UpcomingMilestoneView(
    string ProjectCode,
    string CoupleName,
    string HallName,
    DateOnly EventDate,
    string Milestone,
    DateOnly DueDate,
    int DaysRemaining,
    int TotalTasks,
    int CompletedTasks);

public sealed record StaleBeoAlertView(
    int BeoId,
    string ProjectCode,
    string HallName,
    DateOnly EventDate,
    int Version,
    string Status,
    string? Notes = null,
    string? ApprovedCrCode = null);

public sealed record ManagerDashboardResponse(
    ManagerKpiSummary Kpis,
    IReadOnlyList<ManagerTaskItemView> MyTasks,
    IReadOnlyList<UpcomingMilestoneView> UpcomingMilestones,
    IReadOnlyList<OverdueAlertView> OverdueTasks,
    IReadOnlyList<StaleBeoAlertView> StaleBeos);

/// <summary>
/// M-01 Manager Dashboard Service contract (FE-03, FE-09, Report 3 SRS &amp; Report 4 SDS).
/// Returns real operational metrics, my tasks, alert panels (Overdue day-of tasks &amp; Stale BEOs),
/// and upcoming project milestones strictly scoped to the authenticated manager.
/// </summary>
public interface IManagerDashboardService
{
    Task<ManagerDashboardResponse> GetDashboardAsync(
        int managerId,
        CancellationToken cancellationToken = default);
}
