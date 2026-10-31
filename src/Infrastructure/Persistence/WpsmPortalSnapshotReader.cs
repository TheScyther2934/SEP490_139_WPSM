using Microsoft.EntityFrameworkCore;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.Infrastructure.Persistence;

public sealed class WpsmPortalSnapshotReader(
    IProjectRepository projectRepository,
    ISpecificationRoadmapRepository specificationRoadmapRepository,
    IConfigurationRepository configurationRepository,
    IAccountProfileRepository accounts) : IWpsmPortalSnapshotReader
{
    public async Task<WpsmPortalSnapshot> GetSnapshotAsync(int? userId, string? projectCode = null, CancellationToken cancellationToken = default)
    {
        var profile = userId is int id ? await accounts.GetProfileAsync(id, cancellationToken) : null;
        var projects = await projectRepository.GetProjectsAsync(userId, profile?.Role, cancellationToken);
        var selected = string.IsNullOrWhiteSpace(projectCode)
            ? projects.FirstOrDefault()
            : projects.FirstOrDefault(x => x.Id == projectCode);
        var roadmap = selected is null
            ? Array.Empty<RoadmapTaskView>()
            : await specificationRoadmapRepository.GetRoadmapAsync(selected.Id, cancellationToken);

        return new WpsmPortalSnapshot(
            selected,
            projects,
            await configurationRepository.GetCatalogAsync(cancellationToken),
            await configurationRepository.GetPriceFramesAsync(cancellationToken),
            await configurationRepository.GetPoliciesAsync(cancellationToken),
            profile ?? new ProfileView("", "", "", ""),
            roadmap);
    }
}
