using Microsoft.EntityFrameworkCore;
using Application.UseCases.CoOperators;
using Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.UseCases;

/// <summary>Co-operator data for the use cases: the event with its operator rows, the Manager being added.</summary>
public sealed class CoOperatorStore(WpsmDbContext db) : ICoOperatorStore
{
    public Task<WeddingProject?> GetEventAsync(string projectCode, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.Include(x => x.Manager).Include(x => x.ProjectManagers)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);

    public Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
}
