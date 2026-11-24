using Application.Persistence;
using Domain.Rules;

namespace Application.UseCases.AddonContracts;

/// <summary>An addon contract with its event, as needed to remind, cancel or confirm it.</summary>
public sealed record AddonContractState(int AddonId, string Code, string Status, string Title, decimal Total, DateTime? RemindedAt, EventParty Event);

/// <summary>Reads and writes addon contracts (no rules: those are in AddonContractPolicy).</summary>
public interface IAddonContractStore
{
    Task<EventParty?> GetEventAsync(string eventCode, CancellationToken cancellationToken = default);
    Task<bool> IsSettlementPaidAsync(int projectId, CancellationToken cancellationToken = default);
    /// <summary>True when every catalog item given exists and is active.</summary>
    Task<bool> AreCatalogItemsActiveAsync(IReadOnlyCollection<int> catalogItemIds, CancellationToken cancellationToken = default);
    /// <summary>Stages a new contract waiting for the customer; returns its code (PS-{event}-{n}).</summary>
    Task<string> StageNewAsync(EventParty party, string title, string? note, IReadOnlyList<AddonLine> lines, int createdBy, DateTime now, CancellationToken cancellationToken = default);
    Task<AddonContractState?> GetAsync(string eventCode, string addonCode, CancellationToken cancellationToken = default);
    Task<AddonContractState?> GetForCustomerAsync(int customerId, string addonCode, CancellationToken cancellationToken = default);
    Task StageRemindedAsync(int addonId, DateTime now, CancellationToken cancellationToken = default);
    Task StageCancelledAsync(int addonId, string reason, DateTime now, CancellationToken cancellationToken = default);
    Task StageConfirmedAsync(int addonId, DateTime now, CancellationToken cancellationToken = default);
    /// <summary>Latest coordination sheet status of the event (null when none).</summary>
    Task<string?> GetCoordinationSheetStatusAsync(int projectId, CancellationToken cancellationToken = default);
    Task StageCoordinationSheetStaleAsync(int projectId, CancellationToken cancellationToken = default);
    /// <summary>When the settlement invoice is already issued (event held) and unpaid, the addon is added to it and to the amount due.</summary>
    Task StageOnIssuedSettlementAsync(AddonContractState addon, CancellationToken cancellationToken = default);
}

/// <summary>Use case: the operator creates an addon contract for a locked event; the customer is asked to confirm it.</summary>
public sealed class CreateAddonContract(IAddonContractStore store, IAddonContractRepository addons, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<AddonContractView> HandleAsync(string eventCode, int userId, AddonCreateCommand command, CancellationToken cancellationToken = default)
    {
        var party = await store.GetEventAsync(eventCode, cancellationToken) ?? throw new KeyNotFoundException("Không tìm thấy sự kiện.");
        AddonContractPolicy.EnsureCanCreate(party.Status, await store.IsSettlementPaidAsync(party.ProjectId, cancellationToken));
        var (title, lines) = AddonContractPolicy.ValidateContent(command.Title,
            command.Items?.Select(i => new AddonLine(i.CatalogItemId, i.Description, i.Quantity, i.UnitPrice)).ToList());
        var catalogIds = lines.Where(l => l.CatalogItemId is not null).Select(l => l.CatalogItemId!.Value).Distinct().ToList();
        if (catalogIds.Count > 0 && !await store.AreCatalogItemsActiveAsync(catalogIds, cancellationToken))
            throw new BusinessRuleException("Có dịch vụ không còn hoạt động trong bảng dịch vụ.");

        var note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim();
        var code = await store.StageNewAsync(party, title, note, lines, userId, DateTime.UtcNow, cancellationToken);
        await messages.AddonAwaitingConfirmationAsync(party, code, title, AddonContractPolicy.Total(lines), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await addons.GetAsync(eventCode, code, cancellationToken))!;
    }
}

/// <summary>Use case: the operator reminds the customer of a contract still waiting (at most once an hour).</summary>
public sealed class RemindAddonContract(IAddonContractStore store, IAddonContractRepository addons, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<AddonContractView?> HandleAsync(string eventCode, string addonCode, CancellationToken cancellationToken = default)
    {
        var addon = await store.GetAsync(eventCode, addonCode, cancellationToken);
        if (addon is null) return null;
        var now = DateTime.UtcNow;
        AddonContractPolicy.EnsureCanRemind(addon.Status, addon.RemindedAt, now);
        await store.StageRemindedAsync(addon.AddonId, now, cancellationToken);
        await messages.AddonAwaitingConfirmationAsync(addon.Event, addon.Code, addon.Title, addon.Total, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await addons.GetAsync(eventCode, addonCode, cancellationToken);
    }
}

/// <summary>Use case: the operator cancels a contract the customer has not confirmed (with a reason the customer sees).</summary>
public sealed class CancelAddonContract(IAddonContractStore store, IAddonContractRepository addons, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<AddonContractView?> HandleAsync(string eventCode, string addonCode, string? reason, CancellationToken cancellationToken = default)
    {
        var addon = await store.GetAsync(eventCode, addonCode, cancellationToken);
        if (addon is null) return null;
        var cleanReason = AddonContractPolicy.EnsureCanCancel(addon.Status, reason);
        await store.StageCancelledAsync(addon.AddonId, cleanReason, DateTime.UtcNow, cancellationToken);
        await messages.AddonCancelledAsync(addon.Event, addon.Code, cleanReason, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await addons.GetAsync(eventCode, addonCode, cancellationToken);
    }
}

/// <summary>
/// Use case: the customer confirms a contract. It can then be carried out: a frozen coordination sheet turns stale (to be re-approved),
/// an issued settlement invoice gets the amount, the operators are told.
/// </summary>
public sealed class ConfirmAddonContract(IAddonContractStore store, IAddonContractRepository addons, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<AddonContractView?> HandleAsync(int customerId, string addonCode, CancellationToken cancellationToken = default)
    {
        var addon = await store.GetForCustomerAsync(customerId, addonCode, cancellationToken);
        if (addon is null) return null;
        AddonContractPolicy.EnsureCanConfirm(addon.Status, addon.Event.Status);

        await store.StageConfirmedAsync(addon.AddonId, DateTime.UtcNow, cancellationToken);
        if (CoordinationSheetPolicy.TurnsStaleOnConfirmedAddon(await store.GetCoordinationSheetStatusAsync(addon.Event.ProjectId, cancellationToken)))
            await store.StageCoordinationSheetStaleAsync(addon.Event.ProjectId, cancellationToken);
        await store.StageOnIssuedSettlementAsync(addon, cancellationToken);
        await messages.AddonConfirmedAsync(addon.Event, addon.Code, addon.Total, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await addons.GetAsync(addon.Event.ProjectCode, addon.Code, cancellationToken);
    }
}
