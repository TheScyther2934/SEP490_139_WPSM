using Microsoft.EntityFrameworkCore;
using Application.UseCases;
using Application.UseCases.AddonContracts;
using Domain.Rules;
using Domain.Services;
using Infrastructure.Persistence;
using Domain.Entities;

namespace Infrastructure.UseCases;

/// <summary>Addon contract data for the use cases: reads contracts and events, stages the changes (saved by the use case).</summary>
public sealed class AddonContractStore(WpsmDbContext db) : IAddonContractStore
{
    public Task<EventParty?> GetEventAsync(string eventCode, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.AsNoTracking().Where(p => p.ProjectCode == eventCode)
            .Select(p => new EventParty(p.ProjectId, p.ProjectCode, p.Status, p.EventDate, p.CustomerId, p.Customer.FullName, p.Customer.Email))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsSettlementPaidAsync(int projectId, CancellationToken cancellationToken = default) =>
        db.SettlementInvoices.AnyAsync(i => i.ProjectId == projectId && i.Status == "Paid", cancellationToken);

    public async Task<bool> AreCatalogItemsActiveAsync(IReadOnlyCollection<int> catalogItemIds, CancellationToken cancellationToken = default) =>
        await db.ServiceCatalogItems.CountAsync(c => catalogItemIds.Contains(c.ItemId) && c.Status == "Active", cancellationToken) == catalogItemIds.Count;

    public async Task<string> StageNewAsync(EventParty party, string title, string? note, IReadOnlyList<AddonLine> lines, int createdBy, DateTime now,
        CancellationToken cancellationToken = default)
    {
        var count = await db.AddonContracts.CountAsync(a => a.ProjectId == party.ProjectId, cancellationToken);
        var addon = new AddonContract
        {
            AddonCode = $"PS-{party.ProjectCode}-{count + 1:D2}",
            ProjectId = party.ProjectId, Title = title, Note = note,
            Status = AddonContractPolicy.PendingCustomer, CreatedBy = createdBy, CreatedAt = now, RemindedAt = now
        };
        foreach (var line in lines)
            addon.Items.Add(new AddonContractItem { CatalogItemId = line.CatalogItemId, Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice });
        db.AddonContracts.Add(addon);
        return addon.AddonCode;
    }

    public Task<AddonContractState?> GetAsync(string eventCode, string addonCode, CancellationToken cancellationToken = default) =>
        States(db.AddonContracts.Where(a => a.AddonCode == addonCode && a.Project.ProjectCode == eventCode)).FirstOrDefaultAsync(cancellationToken);

    public Task<AddonContractState?> GetForCustomerAsync(int customerId, string addonCode, CancellationToken cancellationToken = default) =>
        States(db.AddonContracts.Where(a => a.AddonCode == addonCode && a.Project.CustomerId == customerId)).FirstOrDefaultAsync(cancellationToken);

    public async Task StageRemindedAsync(int addonId, DateTime now, CancellationToken cancellationToken = default) =>
        (await TrackAsync(addonId, cancellationToken)).RemindedAt = now;

    public async Task StageCancelledAsync(int addonId, string reason, DateTime now, CancellationToken cancellationToken = default)
    {
        var addon = await TrackAsync(addonId, cancellationToken);
        addon.Status = AddonContractPolicy.Cancelled;
        addon.CancelledAt = now;
        addon.CancelReason = reason;
    }

    public async Task StageConfirmedAsync(int addonId, DateTime now, CancellationToken cancellationToken = default)
    {
        var addon = await TrackAsync(addonId, cancellationToken);
        addon.Status = AddonContractPolicy.Confirmed;
        addon.ConfirmedAt = now;
    }

    public Task<string?> GetCoordinationSheetStatusAsync(int projectId, CancellationToken cancellationToken = default) =>
        db.Beos.Where(b => b.ProjectId == projectId).OrderByDescending(b => b.Version).Select(b => b.Status).FirstOrDefaultAsync(cancellationToken);

    public async Task StageCoordinationSheetStaleAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var sheet = await db.Beos.Where(b => b.ProjectId == projectId).OrderByDescending(b => b.Version).FirstAsync(cancellationToken);
        sheet.Status = CoordinationSheetPolicy.Stale;
    }

    public async Task StageOnIssuedSettlementAsync(AddonContractState addon, CancellationToken cancellationToken = default)
    {
        var invoice = await db.SettlementInvoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.ProjectId == addon.Event.ProjectId, cancellationToken);
        if (invoice is not { Status: "Issued" }) return;
        invoice.AddonTotal += addon.Total;
        invoice.Lines.Add(new SettlementInvoiceLine { LineType = "Addon", AddonId = addon.AddonId, Description = $"Hợp đồng phát sinh {addon.Code}: {addon.Title}", Amount = addon.Total });
        var settlement = await db.Payments.FirstOrDefaultAsync(p => p.ProjectId == addon.Event.ProjectId && p.PaymentType == PaymentKinds.Settlement && p.Status != "Paid", cancellationToken);
        if (settlement is not null) settlement.Amount = (settlement.Amount ?? 0m) + addon.Total;
    }

    private static IQueryable<AddonContractState> States(IQueryable<AddonContract> query) =>
        query.Select(a => new AddonContractState(a.AddonId, a.AddonCode, a.Status, a.Title, a.Items.Sum(i => i.Quantity * i.UnitPrice), a.RemindedAt,
            new EventParty(a.ProjectId, a.Project.ProjectCode, a.Project.Status, a.Project.EventDate, a.Project.CustomerId, a.Project.Customer.FullName, a.Project.Customer.Email)));

    // Loaded (tracked) before changing it, so the audit log records the real previous values.
    private async Task<AddonContract> TrackAsync(int addonId, CancellationToken cancellationToken) =>
        await db.AddonContracts.FindAsync([addonId], cancellationToken) ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng phát sinh.");
}
