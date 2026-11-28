using Application.Persistence;
using Domain.Services;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>Shared pieces of the event stage flow: policy, money of an event, mapping and the settlement invoice.</summary>
public static class EventFlowSupport
{
    public const string HotlineKey = SystemSetting.HotlineKey;
    public const string DefaultHotline = "0976586668";

    /// <summary>The policy in force today: only for signing new events and for the Admin policy screen.</summary>
    public static async Task<EventPolicy> LoadPolicyAsync(WpsmDbContext db, CancellationToken cancellationToken) =>
        (await LoadPolicyVersionAsync(db, VietnamTime.GetVietnamToday(), cancellationToken)).Policy;

    /// <summary>The policy in force on a date and the effective date of its newest row (null when only defaults apply).</summary>
    public static async Task<(EventPolicy Policy, DateOnly? EffectiveFrom)> LoadPolicyVersionAsync(WpsmDbContext db, DateOnly onDate,
        CancellationToken cancellationToken)
    {
        var rows = await db.PolicyConfigs.AsNoTracking().Where(p => p.EffectiveFrom <= onDate)
            .Select(p => new { p.PolicyType, p.Value, p.EffectiveFrom, p.PolicyId }).ToListAsync(cancellationToken);
        var latest = rows.Where(r => EventPolicy.Keys.All.Contains(r.PolicyType)).GroupBy(r => r.PolicyType)
            .Select(g => g.OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.PolicyId).First()).ToList();
        DateOnly? effective = latest.Count == 0 ? null : latest.Max(r => r.EffectiveFrom);
        return (EventPolicy.From(latest.ToDictionary(r => r.PolicyType, r => r.Value)), effective);
    }

    /// <summary>The terms one event was signed with (EventTerms); the policy in force only when the event has none.</summary>
    public static async Task<EventPolicy> LoadEventPolicyAsync(WpsmDbContext db, int projectId, CancellationToken cancellationToken)
    {
        var terms = await db.EventTerms.AsNoTracking().FirstOrDefaultAsync(t => t.ProjectId == projectId, cancellationToken);
        return terms?.ToPolicy() ?? await LoadPolicyAsync(db, cancellationToken);
    }

    /// <summary>The signed terms of several events; events without terms get the policy in force.</summary>
    public static async Task<Func<int, EventPolicy>> LoadEventPoliciesAsync(WpsmDbContext db, IReadOnlyCollection<int> projectIds,
        CancellationToken cancellationToken)
    {
        var terms = await db.EventTerms.AsNoTracking().Where(t => projectIds.Contains(t.ProjectId)).ToListAsync(cancellationToken);
        var byProject = terms.ToDictionary(t => t.ProjectId, t => t.ToPolicy());
        var inForce = byProject.Count == projectIds.Distinct().Count() ? EventPolicy.Default : await LoadPolicyAsync(db, cancellationToken);
        return id => byProject.TryGetValue(id, out var p) ? p : inForce;
    }

    /// <summary>What the screens show about the terms of an event; the policy in force decides whether they are older.</summary>
    public static EventTermsView TermsView(WeddingProject project, EventPolicy inForce)
    {
        var terms = EventTerms.PolicyOf(project.Terms, inForce);
        return new EventTermsView(terms.DepositRatio, terms.MaxDecreaseRatio, terms.LockDays, terms.SupplierDeadlineDays, terms.SettlementDueDays,
            project.Terms?.PolicyEffectiveFrom, DateOnly.FromDateTime(project.CreatedAt), terms.Stamp() != inForce.Stamp());
    }

    public static async Task<string> LoadHotlineAsync(WpsmDbContext db, CancellationToken cancellationToken) =>
        await db.SystemSettings.AsNoTracking().Where(s => s.SettingKey == HotlineKey).Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken) ?? DefaultHotline;

    /// <summary>Sum of the confirmed addon contracts of an event.</summary>
    public static async Task<decimal> ConfirmedAddonTotalAsync(WpsmDbContext db, int projectId, CancellationToken cancellationToken) =>
        await db.AddonContractItems.Where(i => i.Addon.ProjectId == projectId && i.Addon.Status == "Confirmed")
            .SumAsync(i => (decimal?)(i.Quantity * i.UnitPrice), cancellationToken) ?? 0m;

    /// <summary>Contract total = sum of the service lines (the value adjusted in stage 2).</summary>
    public static decimal SpecTotal(IEnumerable<SpecItem> items) => items.Sum(s => s.LineTotal ?? s.Quantity * s.UnitPrice);

    public static string AddonStatusLabel(string status) => status switch
    {
        "PendingCustomer" => "Chờ khách xác nhận",
        "Confirmed" => "Khách đã xác nhận",
        "Cancelled" => "Đã hủy",
        _ => status
    };

    public static string NoteTypeLabel(string type) => type switch
    {
        "Tasting" => "Thử món",
        "Fitting" => "Thử đồ",
        "Adjustment" => "Điều chỉnh dịch vụ",
        "Supplier" => "Nhà cung cấp",
        "General" => "Lưu ý chung",
        _ => type
    };

    public static readonly IReadOnlySet<string> NoteTypes = new HashSet<string> { "Tasting", "Fitting", "Adjustment", "Supplier", "General" };

    public static StageNoteView MapNote(StageNote n) => new(n.StageNoteId, n.Stage, EventStageRules.StageTitle(n.Stage), n.NoteType, n.Content,
        n.ValueBefore, n.ValueAfter, n.CreatedByNavigation?.FullName ?? "", n.CreatedAt);

    public static AddonContractView MapAddon(AddonContract a, string eventCode) => new(a.AddonCode, eventCode, a.Title, a.Note, a.Status,
        a.Items.Sum(i => i.LineTotal ?? i.Quantity * i.UnitPrice), a.CreatedByNavigation?.FullName ?? "", a.CreatedAt, a.ConfirmedAt,
        a.CancelledAt, a.CancelReason, a.RemindedAt,
        a.Items.OrderBy(i => i.AddonItemId).Select(i => new AddonItemView(i.CatalogItemId, i.Description, i.Quantity, i.UnitPrice,
            i.LineTotal ?? i.Quantity * i.UnitPrice)).ToList());

    public static SettlementInvoiceView? MapInvoice(SettlementInvoice? invoice, string eventCode, IReadOnlyDictionary<int, string> addonCodes) =>
        invoice is null ? null : new SettlementInvoiceView(invoice.InvoiceCode, eventCode, invoice.ContractTotal, invoice.AddonTotal,
            invoice.DepositPaid, invoice.AmountDue ?? invoice.ContractTotal + invoice.AddonTotal - invoice.DepositPaid, invoice.DueDate,
            invoice.Status, invoice.IssuedAt, invoice.PaidAt,
            invoice.Lines.OrderBy(l => l.LineType == "Contract" ? 0 : l.LineType == "Addon" ? 1 : 2).ThenBy(l => l.LineId)
                .Select(l => new InvoiceLineView(l.LineType, l.Description, l.Amount,
                    l.AddonId is int id && addonCodes.TryGetValue(id, out var code) ? code : null)).ToList());

    public static async Task<SettlementInvoiceView?> LoadInvoiceViewAsync(WpsmDbContext db, int projectId, string eventCode, CancellationToken cancellationToken)
    {
        var invoice = await db.SettlementInvoices.AsNoTracking().Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.ProjectId == projectId, cancellationToken);
        if (invoice is null) return null;
        var codes = await db.AddonContracts.AsNoTracking().Where(a => a.ProjectId == projectId)
            .ToDictionaryAsync(a => a.AddonId, a => a.AddonCode, cancellationToken);
        return MapInvoice(invoice, eventCode, codes);
    }

    /// <summary>Everyone who currently operates the event (Primary first) with their contact details.</summary>
    public static async Task<IReadOnlyList<OperatorContactView>> LoadContactsAsync(WpsmDbContext db, int projectId, CancellationToken cancellationToken) =>
        await db.ProjectManagers.AsNoTracking().Where(m => m.ProjectId == projectId && m.RemovedAt == null)
            .OrderBy(m => m.Role == "Primary" ? 0 : 1).ThenBy(m => m.AssignedAt)
            .Select(m => new OperatorContactView(m.Manager.FullName, m.Role, m.Manager.Phone, m.Manager.Email))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Issues the final invoice of an event (idempotent): contract total + confirmed addon contracts - deposit paid,
    /// and makes the Settlement payment Due for that amount. Called the day after the event.
    /// </summary>
    public static async Task<SettlementInvoice> IssueInvoiceAsync(WpsmDbContext db, WeddingProject project, int? issuedBy, EventPolicy policy,
        CancellationToken cancellationToken)
    {
        var existing = await db.SettlementInvoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.ProjectId == project.ProjectId, cancellationToken);
        if (existing is not null) return existing;

        var addons = await db.AddonContracts.Include(a => a.Items)
            .Where(a => a.ProjectId == project.ProjectId && a.Status == "Confirmed").OrderBy(a => a.AddonId).ToListAsync(cancellationToken);
        var depositPaid = await db.Payments.Where(p => p.ProjectId == project.ProjectId && p.PaymentType == PaymentKinds.Deposit && p.Status == "Paid")
            .SumAsync(p => p.Amount ?? 0m, cancellationToken);
        var contractTotal = project.CurrentTotal;
        var invoice = new SettlementInvoice
        {
            InvoiceCode = $"HD-{project.ProjectCode}",
            ProjectId = project.ProjectId,
            ContractTotal = contractTotal,
            AddonTotal = addons.Sum(a => a.Items.Sum(i => i.Quantity * i.UnitPrice)),
            DepositPaid = depositPaid,
            DueDate = EventStageRules.SettlementDueDate(project.EventDate, policy),
            Status = "Issued",
            IssuedBy = issuedBy,
            IssuedAt = DateTime.UtcNow
        };
        invoice.Lines.Add(new SettlementInvoiceLine { LineType = "Contract", Description = "Hợp đồng dịch vụ tiệc cưới (sau điều chỉnh)", Amount = contractTotal });
        foreach (var addon in addons)
            invoice.Lines.Add(new SettlementInvoiceLine { LineType = "Addon", AddonId = addon.AddonId, Description = $"Hợp đồng phát sinh {addon.AddonCode}: {addon.Title}",
                Amount = addon.Items.Sum(i => i.Quantity * i.UnitPrice) });
        if (depositPaid > 0)
            invoice.Lines.Add(new SettlementInvoiceLine { LineType = "Deposit", Description = "Trừ tiền đặt cọc đã thanh toán", Amount = -depositPaid });
        db.SettlementInvoices.Add(invoice);

        var amountDue = PaymentCalculator.CalculateSettlementAmount(contractTotal, invoice.AddonTotal, depositPaid);
        var settlement = await db.Payments.FirstOrDefaultAsync(p => p.ProjectId == project.ProjectId && p.PaymentType == PaymentKinds.Settlement, cancellationToken);
        // Nothing left to pay (deposit covers everything): no settlement payment row, the invoice shows 0.
        if (settlement is null && amountDue > 0)
            db.Payments.Add(new Payment { ProjectId = project.ProjectId, PaymentType = PaymentKinds.Settlement, Status = "Due", DueDate = invoice.DueDate, Amount = amountDue });
        else if (settlement is not null && settlement.Status != "Paid")
        {
            settlement.Status = "Due";
            settlement.DueDate = invoice.DueDate;
            settlement.Amount = amountDue > 0 ? amountDue : null;
        }
        return invoice;
    }

    /// <summary>Next event code number for addon contracts: PS-{event code}-{n}.</summary>
    public static async Task<string> NextAddonCodeAsync(WpsmDbContext db, WeddingProject project, CancellationToken cancellationToken)
    {
        var count = await db.AddonContracts.CountAsync(a => a.ProjectId == project.ProjectId, cancellationToken);
        return $"PS-{project.ProjectCode}-{count + 1:D2}";
    }

    /// <summary>Active operators of an event (Primary + co-managers), used to notify them.</summary>
    public static async Task<IReadOnlyList<int>> OperatorIdsAsync(WpsmDbContext db, int projectId, CancellationToken cancellationToken) =>
        await db.ProjectManagers.Where(m => m.ProjectId == projectId && m.RemovedAt == null).Select(m => m.ManagerId).Distinct().ToListAsync(cancellationToken);
}
