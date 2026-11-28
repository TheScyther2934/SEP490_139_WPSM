using Application.Auth;
using Application.Persistence;
using Domain.Entities;
using Domain.Rules;
using Domain.Services;

namespace Application.UseCases.EventBooking;

/// <summary>Data access for signing an event and its deposit; implemented in Infrastructure. Entities come back tracked.</summary>
public interface IEventBookingStore
{
    Task<bool> IsActiveManagerAsync(int managerId, CancellationToken cancellationToken = default);
    Task<Hall?> GetActiveHallAsync(string name, CancellationToken cancellationToken = default);
    Task<Slot?> GetSlotAsync(int hallId, string slotName, CancellationToken cancellationToken = default);
    /// <summary>True when an event that is not cancelled already uses this slot on this date.</summary>
    Task<bool> IsSlotTakenAsync(int slotId, DateOnly eventDate, CancellationToken cancellationToken = default);
    /// <summary>The policy in force today and the effective date of its newest row (null: built-in defaults).</summary>
    Task<(EventPolicy Policy, DateOnly? EffectiveFrom)> GetPolicyInForceAsync(CancellationToken cancellationToken = default);
    /// <summary>Names of the requested services that are active (missing ids are inactive or unknown).</summary>
    Task<IReadOnlyDictionary<int, string>> GetActiveServiceNamesAsync(IReadOnlyCollection<int> catalogItemIds, CancellationToken cancellationToken = default);
    /// <summary>The price frame in force on <paramref name="day"/> for each service that has one.</summary>
    Task<IReadOnlyDictionary<int, PriceRange>> GetPriceFramesAsync(IReadOnlyCollection<int> catalogItemIds, DateOnly day, CancellationToken cancellationToken = default);
    Task<User?> FindUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetProjectCodesAsync(string prefix, CancellationToken cancellationToken = default);
    void Add(WeddingProject project);
    /// <summary>The event with its customer, when <paramref name="managerId"/> operates it (creator or active co-manager).</summary>
    Task<WeddingProject?> GetOperatedEventAsync(string projectCode, int managerId, CancellationToken cancellationToken = default);
    /// <summary>The deposit payment with its event and customer.</summary>
    Task<Payment?> GetDepositAsync(string projectCode, CancellationToken cancellationToken = default);
}

internal static class Parties
{
    public static EventParty Of(WeddingProject project, User customer) =>
        new(project.ProjectId, project.ProjectCode, project.Status, project.EventDate, customer.UserId, customer.FullName, customer.Email);
}

/// <summary>
/// Use case (M-03, UC-03): the Manager signs an event with the customer. Checks the customer, the slot, the booking window, the services and
/// their price frames and the deposit; creates the customer account when needed (temporary password by email) and the event in stage 2.
/// </summary>
public sealed class BookEvent(IEventBookingStore store, IPasswordService passwords, IEventMessages messages, IUnitOfWork unitOfWork, IProjectRepository projects)
{
    public async Task<CreatedProjectView> HandleAsync(CreateProjectCommand command, int managerId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (!await store.IsActiveManagerAsync(managerId, cancellationToken))
            throw new BusinessRuleException("Tài khoản quản lý không hợp lệ hoặc đã bị vô hiệu hóa.");
        var customer = EventBookingPolicy.ValidateCustomer(command.CoupleName, command.CustomerEmail, command.CustomerPhone);
        var hall = await store.GetActiveHallAsync((command.Hall ?? "").Trim(), cancellationToken)
            ?? throw new BusinessRuleException("Không tìm thấy sảnh đang hoạt động. Hãy kiểm tra danh mục sảnh trong database.");
        var slot = await store.GetSlotAsync(hall.HallId, EventBookingPolicy.NormalizeSlot(command.Slot), cancellationToken)
            ?? throw new BusinessRuleException("Ca tiệc không tồn tại cho sảnh đã chọn.");
        EventBookingPolicy.EnsurePaymentMethod(command.PaymentMethod);
        EventBookingPolicy.EnsureSlotFree(await store.IsSlotTakenAsync(slot.SlotId, command.EventDate, cancellationToken));

        var (policy, policyEffectiveFrom) = await store.GetPolicyInForceAsync(cancellationToken);
        EventBookingPolicy.EnsurePolicyUnchanged(command.PolicyStamp, policy);
        var todayVn = VietnamTime.GetVietnamToday();
        EventBookingPolicy.EnsureBookingDate(command.EventDate, todayVn, policy);
        var lines = (command.Items ?? []).Select(i => new BookingLine(i.CatalogItemId, i.Description, i.Quantity, i.UnitPrice)).ToList();
        EventBookingPolicy.ValidateLines(lines);
        var ids = lines.Select(l => l.CatalogItemId).ToArray();
        var names = await store.GetActiveServiceNamesAsync(ids, cancellationToken);
        if (names.Count != ids.Length) throw new BusinessRuleException("Có dịch vụ không còn hoạt động trong bảng dịch vụ.");
        var frames = await store.GetPriceFramesAsync(ids, todayVn, cancellationToken);
        foreach (var line in lines)
            if (frames.TryGetValue(line.CatalogItemId, out var frame)) EventBookingPolicy.EnsurePriceInRange(names[line.CatalogItemId], line.UnitPrice, frame);
        var deposit = EventBookingPolicy.Deposit(EventBookingPolicy.ContractValue(lines), command.DepositAmount, policy);

        var now = DateTime.UtcNow;
        string? temporaryPassword = null;
        var account = await store.FindUserByEmailAsync(customer.Email, cancellationToken);
        if (account is not null) account.PrepareForNewBooking();
        else
        {
            temporaryPassword = passwords.NewTemporaryPassword();
            account = User.NewCustomer(customer, passwords.Hash(temporaryPassword), now);
        }

        var code = EventBookingPolicy.NextProjectCode(now.Year, await store.GetProjectCodesAsync($"WPSM-{now.Year}-", cancellationToken));
        var described = lines.Select(l => (l, string.IsNullOrWhiteSpace(l.Description) ? names[l.CatalogItemId] : l.Description.Trim())).ToList();
        var project = WeddingProject.Book(code, account, managerId, hall.HallId, slot.SlotId, command.EventDate, described, deposit, command.PaymentMethod, now, todayVn,
            EventTerms.Capture(policy, policyEffectiveFrom, now));
        store.Add(project);
        // Saved first so the event and the account have their keys; the messages below refer to them.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await messages.EventBookedAsync(Parties.Of(project, account), command.PaymentMethod == EventBookingPolicy.Cash, temporaryPassword, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var view = await projects.GetProjectAsync(code, cancellationToken) ?? throw new InvalidOperationException("Sự kiện vừa tạo không đọc lại được.");
        return new CreatedProjectView(view, temporaryPassword);
    }
}

/// <summary>Use case: a new temporary password for a customer who has never signed in, sent by email. Returns null when the event is not found.</summary>
public sealed class ResendCustomerCredentials(IEventBookingStore store, IPasswordService passwords, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<string?> HandleAsync(string projectCode, int managerId, CancellationToken cancellationToken = default)
    {
        var project = await store.GetOperatedEventAsync(projectCode, managerId, cancellationToken);
        if (project is null) return null;
        var temporaryPassword = passwords.NewTemporaryPassword();
        project.Customer.ResetTemporaryPassword(passwords.Hash(temporaryPassword));
        await messages.CredentialsResentAsync(Parties.Of(project, project.Customer), temporaryPassword, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return temporaryPassword;
    }
}

/// <summary>Use case (M-03): the creator records the bank receipt of the deposit (already stored). False when the event or its deposit is not theirs.</summary>
public sealed class RecordDepositReceipt(IEventBookingStore store, IEventMessages messages, IUnitOfWork unitOfWork)
{
    public async Task<bool> HandleAsync(string projectCode, int managerId, StoredFileView receipt, CancellationToken cancellationToken = default)
    {
        var deposit = await store.GetDepositAsync(projectCode, cancellationToken);
        if (deposit is null || deposit.Project.ManagerId != managerId) return false;
        deposit.RecordBankReceipt(receipt.StorageKey, receipt.OriginalName, receipt.ContentType, managerId, VietnamTime.GetVietnamToday());
        await messages.DepositReceiptRecordedAsync(Parties.Of(deposit.Project, deposit.Project.Customer), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
