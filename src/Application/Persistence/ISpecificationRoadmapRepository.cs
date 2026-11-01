namespace WPSM.Application.Persistence;

public interface ISpecificationRoadmapRepository
{
    Task<WeddingProjectView?> UpdateSpecificationAsync(string projectCode, int managerId, SpecificationCommand command, CancellationToken cancellationToken = default);
    Task<WeddingProjectView?> FreezeBaselineAsync(string projectCode, int managerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoadmapTaskView>> GetRoadmapAsync(string projectCode, CancellationToken cancellationToken = default);
}
