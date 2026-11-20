using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Addon contracts: extra services for an event whose services are locked. No deposit; the operator creates it, reminds the
/// customer, and it is carried out and billed (in the settlement invoice) only after the customer confirms it.
/// </summary>
public sealed class AddonContractRepository(WpsmDbContext db, IEmailOutbox emailOutbox, EmailOptions emailOptions) : IAddonContractRepository
{
    public async Task<IReadOnlyList<AddonContractView>> ListAsync(string eventCode, CancellationToken cancellationToken = default)
    {
        var addons = await Query().Where(a => a.Project.ProjectCode == eventCode).OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
        return addons.Select(a => EventFlowSupport.MapAddon(a, eventCode)).ToList();
    }

    public async Task<AddonContractView> CreateAsync(string eventCode, int userId, AddonCreateCommand command, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.Include(p => p.Customer).FirstOrDefaultAsync(p => p.ProjectCode == eventCode, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy sự kiện.");
        if (!EventStageRules.CanAddAddon(project.Status))
            throw new InvalidOperationException(project.Status == EventStatus.Adjusting
                ? "Sự kiện còn trong giai đoạn điều chỉnh: hãy sửa trực tiếp dịch vụ đã đặt. Hợp đồng phát sinh chỉ có sau khi khóa."
                : "Sự kiện đã tất toán, đóng hoặc hủy nên không thêm phát sinh.");
        if (await db.SettlementInvoices.AnyAsync(i => i.ProjectId == project.ProjectId && i.Status == "Paid", cancellationToken))
            throw new InvalidOperationException("Hóa đơn tất toán đã thanh toán, không thêm phát sinh.");

        var title = command.Title?.Trim() ?? "";
        if (title.Length is < 3 or > 200) throw new InvalidOperationException("Tiêu đề phát sinh cần từ 3 đến 200 ký tự.");
        if (command.Items is null || command.Items.Count == 0) throw new InvalidOperationException("Hợp đồng phát sinh cần ít nhất một dòng dịch vụ.");
        if (command.Items.Count > 30) throw new InvalidOperationException("Tối đa 30 dòng dịch vụ.");
        foreach (var item in command.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Description) || item.Description.Trim().Length > 300)
                throw new InvalidOperationException("Mỗi dòng cần mô tả (tối đa 300 ký tự).");
            if (item.Quantity <= 0 || item.UnitPrice <= 0) throw new InvalidOperationException("Số lượng và đơn giá phải lớn hơn 0.");
        }
        var catalogIds = command.Items.Where(i => i.CatalogItemId is not null).Select(i => i.CatalogItemId!.Value).Distinct().ToList();
        if (catalogIds.Count > 0 && await db.ServiceCatalogItems.CountAsync(c => catalogIds.Contains(c.ItemId) && c.Status == "Active", cancellationToken) != catalogIds.Count)
            throw new InvalidOperationException("Có dịch vụ không còn hoạt động trong bảng dịch vụ.");

        var addon = new AddonContract
        {
            AddonCode = await EventFlowSupport.NextAddonCodeAsync(db, project, cancellationToken),
            ProjectId = project.ProjectId, Title = title, Note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim(),
            Status = "PendingCustomer", CreatedBy = userId, CreatedAt = DateTime.UtcNow, RemindedAt = DateTime.UtcNow
        };
        foreach (var item in command.Items)
            addon.Items.Add(new AddonContractItem { CatalogItemId = item.CatalogItemId, Description = item.Description.Trim(), Quantity = item.Quantity, UnitPrice = item.UnitPrice });
        db.AddonContracts.Add(addon);
        var total = command.Items.Sum(i => i.Quantity * i.UnitPrice);
        NotifyCustomer(project, addon, total);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(addon.AddonId, eventCode, cancellationToken);
    }

    public async Task<AddonContractView?> RemindCustomerAsync(string eventCode, string addonCode, int userId, CancellationToken cancellationToken = default)
    {
        var addon = await db.AddonContracts.Include(a => a.Items).Include(a => a.Project).ThenInclude(p => p.Customer)
            .FirstOrDefaultAsync(a => a.AddonCode == addonCode && a.Project.ProjectCode == eventCode, cancellationToken);
        if (addon is null) return null;
        if (addon.Status != "PendingCustomer") throw new InvalidOperationException("Chỉ nhắc được phát sinh đang chờ khách xác nhận.");
        if (addon.RemindedAt is { } last && DateTime.UtcNow - last < TimeSpan.FromHours(1))
            throw new InvalidOperationException("Vừa nhắc khách xong, hãy đợi ít nhất 1 giờ trước khi nhắc lại.");
        addon.RemindedAt = DateTime.UtcNow;
        NotifyCustomer(addon.Project, addon, addon.Items.Sum(i => i.Quantity * i.UnitPrice));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(addon.AddonId, eventCode, cancellationToken);
    }

    public async Task<AddonContractView?> CancelAsync(string eventCode, string addonCode, int userId, string reason, CancellationToken cancellationToken = default)
    {
        var addon = await db.AddonContracts.Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.AddonCode == addonCode && a.Project.ProjectCode == eventCode, cancellationToken);
        if (addon is null) return null;
        reason = reason?.Trim() ?? "";
        if (reason.Length is < 5 or > 500) throw new InvalidOperationException("Hãy ghi lý do hủy (5 đến 500 ký tự).");
        if (addon.Status != "PendingCustomer") throw new InvalidOperationException("Chỉ hủy được phát sinh chưa được khách xác nhận.");
        addon.Status = "Cancelled";
        addon.CancelledAt = DateTime.UtcNow;
        addon.CancelReason = reason;
        OperationsSupport.Notify(db, addon.Project.CustomerId, NotificationTypes.AddonContract,
            $"Hợp đồng phát sinh {addon.AddonCode} của sự kiện {eventCode} đã được hủy: {reason}", OperationsSupport.Links.CustomerEvent);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(addon.AddonId, eventCode, cancellationToken);
    }

    public async Task<AddonContractView?> ConfirmByCustomerAsync(int customerId, string addonCode, CancellationToken cancellationToken = default)
    {
        var addon = await db.AddonContracts.Include(a => a.Items).Include(a => a.Project)
            .FirstOrDefaultAsync(a => a.AddonCode == addonCode && a.Project.CustomerId == customerId, cancellationToken);
        if (addon is null) return null;
        if (addon.Status != "PendingCustomer") throw new InvalidOperationException("Hợp đồng phát sinh này không còn chờ xác nhận.");
        var project = addon.Project;
        if (!EventStageRules.CanAddAddon(project.Status)) throw new InvalidOperationException("Sự kiện đã tất toán hoặc đóng, không xác nhận thêm phát sinh.");

        addon.Status = "Confirmed";
        addon.ConfirmedAt = DateTime.UtcNow;
        var total = addon.Items.Sum(i => i.Quantity * i.UnitPrice);
        // A confirmed addon changes what the departments must deliver: the frozen coordination sheet becomes stale.
        var beo = await db.Beos.Where(b => b.ProjectId == project.ProjectId).OrderByDescending(b => b.Version).FirstOrDefaultAsync(cancellationToken);
        if (beo?.Status == "Frozen") beo.Status = "Stale";
        // An invoice already issued (event held) is not paid yet: add the line so the settlement includes it.
        var invoice = await db.SettlementInvoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.ProjectId == project.ProjectId, cancellationToken);
        if (invoice is { Status: "Issued" })
        {
            invoice.AddonTotal += total;
            invoice.Lines.Add(new SettlementInvoiceLine { LineType = "Addon", AddonId = addon.AddonId, Description = $"Hợp đồng phát sinh {addon.AddonCode}: {addon.Title}", Amount = total });
            var settlement = await db.Payments.FirstOrDefaultAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == PaymentKinds.Settlement && p.Status != "Paid", cancellationToken);
            if (settlement is not null) settlement.Amount = (settlement.Amount ?? 0m) + total;
        }
        foreach (var operatorId in await EventFlowSupport.OperatorIdsAsync(db, project.ProjectId, cancellationToken))
            OperationsSupport.Notify(db, operatorId, NotificationTypes.AddonContract,
                $"[{project.ProjectCode}] Khách đã xác nhận hợp đồng phát sinh {addon.AddonCode} ({EventStageRules.Vnd(total)}). Có thể thực hiện.",
                OperationsSupport.Links.ManagerAddons(project.ProjectCode));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(addon.AddonId, project.ProjectCode, cancellationToken);
    }

    private IQueryable<AddonContract> Query() => db.AddonContracts.AsNoTracking().Include(a => a.Items).Include(a => a.CreatedByNavigation);

    private async Task<AddonContractView> GetAsync(int addonId, string eventCode, CancellationToken cancellationToken) =>
        EventFlowSupport.MapAddon(await Query().SingleAsync(a => a.AddonId == addonId, cancellationToken), eventCode);

    private void NotifyCustomer(WeddingProject project, AddonContract addon, decimal total)
    {
        OperationsSupport.Notify(db, project.CustomerId, NotificationTypes.AddonContract,
            $"Sự kiện {project.ProjectCode} có hợp đồng phát sinh {addon.AddonCode} ({EventStageRules.Vnd(total)}) cần bạn đối chiếu và xác nhận.",
            OperationsSupport.Links.CustomerEvent);
        var baseUrl = emailOptions.PublicBaseUrl?.TrimEnd('/') ?? "http://localhost:5173";
        emailOutbox.Enqueue(project.Customer.Email, $"Xác nhận hợp đồng phát sinh {addon.AddonCode}",
            EmailTemplates.AddonConfirmRequest(project.Customer.FullName, project.ProjectCode, addon.AddonCode, addon.Title, total, baseUrl), "Reminder", project.ProjectId);
    }
}
