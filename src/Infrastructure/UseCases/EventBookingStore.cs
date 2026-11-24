using Microsoft.EntityFrameworkCore;
using Application.UseCases.EventBooking;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;

namespace Infrastructure.UseCases;

/// <summary>Data for signing an event and recording its deposit: lookups, tracked entities, the policy in force.</summary>
public sealed class EventBookingStore(WpsmDbContext db) : IEventBookingStore
{
    public Task<bool> IsActiveManagerAsync(int managerId, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(x => x.UserId == managerId && x.Role == "Manager" && x.Status == "Active", cancellationToken);

    public Task<Hall?> GetActiveHallAsync(string name, CancellationToken cancellationToken = default) =>
        db.Halls.SingleOrDefaultAsync(x => x.Status == "Active" && x.Name == name, cancellationToken);

    public Task<Slot?> GetSlotAsync(int hallId, string slotName, CancellationToken cancellationToken = default) =>
        db.Slots.SingleOrDefaultAsync(x => x.HallId == hallId && x.SlotName == slotName, cancellationToken);

    public Task<bool> IsSlotTakenAsync(int slotId, DateOnly eventDate, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.AnyAsync(x => x.SlotId == slotId && x.EventDate == eventDate && x.Status != EventStatus.Cancelled, cancellationToken);

    public Task<EventPolicy> GetPolicyAsync(CancellationToken cancellationToken = default) => EventFlowSupport.LoadPolicyAsync(db, cancellationToken);

    public async Task<IReadOnlyDictionary<int, string>> GetActiveServiceNamesAsync(IReadOnlyCollection<int> catalogItemIds, CancellationToken cancellationToken = default) =>
        await db.ServiceCatalogItems.AsNoTracking().Where(x => catalogItemIds.Contains(x.ItemId) && x.Status == "Active")
            .ToDictionaryAsync(x => x.ItemId, x => x.Name, cancellationToken);

    public async Task<IReadOnlyDictionary<int, PriceRange>> GetPriceFramesAsync(IReadOnlyCollection<int> catalogItemIds, DateOnly day, CancellationToken cancellationToken = default) =>
        await db.PriceFrames.AsNoTracking().Where(x => catalogItemIds.Contains(x.ItemId) && x.EffectiveFrom <= day)
            .GroupBy(x => x.ItemId).Select(g => g.OrderByDescending(x => x.EffectiveFrom).First())
            .ToDictionaryAsync(x => x.ItemId, x => new PriceRange(x.MinPrice, x.MaxPrice), cancellationToken);

    public Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

    public async Task<IReadOnlyList<string>> GetProjectCodesAsync(string prefix, CancellationToken cancellationToken = default) =>
        await db.WeddingProjects.Where(x => x.ProjectCode.StartsWith(prefix)).Select(x => x.ProjectCode).ToListAsync(cancellationToken);

    public void Add(WeddingProject project) => db.WeddingProjects.Add(project);

    public Task<WeddingProject?> GetOperatedEventAsync(string projectCode, int managerId, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode
                && (x.ManagerId == managerId || x.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)), cancellationToken);

    public Task<Payment?> GetDepositAsync(string projectCode, CancellationToken cancellationToken = default) =>
        db.Payments.Include(x => x.Project).ThenInclude(p => p.Customer)
            .SingleOrDefaultAsync(x => x.Project.ProjectCode == projectCode && x.PaymentType == "Deposit", cancellationToken);
}
