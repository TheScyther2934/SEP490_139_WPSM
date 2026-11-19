namespace Application.Persistence;

/// <summary>Stage 2: the operator changes the service lines of an event with a required note; the total keeps the agreed floor.</summary>
public interface ISpecificationRepository
{
    Task<WeddingProjectView?> UpdateSpecificationAsync(string projectCode, int managerId, SpecificationCommand command, CancellationToken cancellationToken = default);
}
