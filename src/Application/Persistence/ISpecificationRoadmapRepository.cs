namespace WPSM.Application.Persistence;

public interface ISpecificationRoadmapRepository
{
    Task<WeddingProjectView?> UpdateSpecificationAsync(string projectCode, SpecificationCommand command, CancellationToken cancellationToken = default);
    Task<WeddingProjectView?> FreezeBaselineAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoadmapTaskView>> GetRoadmapAsync(string projectCode, CancellationToken cancellationToken = default);
}
