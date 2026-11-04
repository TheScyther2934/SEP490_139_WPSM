using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence;

public sealed class WpsmPortalSnapshotReader(
    IProjectRepository projectRepository,
    ISpecificationRoadmapRepository specificationRoadmapRepository,
    IConfigurationRepository configurationRepository,
    IAccountProfileRepository accounts,
    WpsmDbContext db) : IWpsmPortalSnapshotReader
{
    public async Task<WpsmPortalSnapshot> GetSnapshotAsync(int? userId, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var profile = userId is int id ? await accounts.GetProfileAsync(id, cancellationToken) : null;
        var projects = await projectRepository.GetProjectsAsync(userId, profile?.Role, cancellationToken);
        var selected = string.IsNullOrWhiteSpace(projectCode)
            ? projects.FirstOrDefault()
            : projects.FirstOrDefault(x => x.Id == projectCode) ?? throw new KeyNotFoundException("Không tìm thấy dự án trong phạm vi tài khoản.");
        var roadmap = selected is null
            ? Array.Empty<RoadmapTaskView>()
            : await specificationRoadmapRepository.GetRoadmapAsync(selected.Id, cancellationToken);

        var codes = projects.Select(p => p.Id).ToArray();
        var today = DateOnly.FromDateTime(DateTime.Today);
        return new WpsmPortalSnapshot(
            selected,
            projects,
            await configurationRepository.GetCatalogAsync(cancellationToken),
            await configurationRepository.GetPriceFramesAsync(cancellationToken),
            await configurationRepository.GetPoliciesAsync(cancellationToken),
            profile ?? new ProfileView("", "", "", ""),
            roadmap)
        {
            UnreadNotifications = await db.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead, cancellationToken),
            OverdueTasks = await db.RoadmapTasks.CountAsync(t => codes.Contains(t.Project.ProjectCode) && t.Status != "Done" && t.DueDate < today, cancellationToken)
                + await db.DayOfTasks.CountAsync(t => codes.Contains(t.Project.ProjectCode) && t.Status != "Done" && t.DeadlineTime < DateTime.UtcNow, cancellationToken)
        };
    }
}
