using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Files;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Payments of an event seen by its operators: the deposit (stage 1) and the settlement (stage 5).
/// Settlement = contract total after adjustments + confirmed addon contracts - deposit, recorded once after the event.
/// Read only: recording the settlement and issuing its receipt are use cases (Application/UseCases/Settlement).
/// </summary>
public sealed class ManagerPaymentRepository(WpsmDbContext db, IFileService files) : IManagerPaymentRepository
{

    internal sealed record Money(decimal ContractTotal, decimal AddonTotal, decimal GrandTotal, decimal DepositPaid, decimal SettlementPaid,
        decimal TotalPaid, decimal Outstanding);

    public async Task<ManagerPaymentOverviewView?> GetProjectPaymentsAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        return project is null ? null : await BuildOverviewAsync(project, cancellationToken);
    }

    public async Task<SettlementCalculationView?> GetSettlementCalculationAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        return project is null ? null : BuildSettlement(project, await ComputeMoneyAsync(project, cancellationToken));
    }

    public async Task<FinalSettlementReceiptView?> GetSettlementReceiptAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;
        var managerName = (await db.Users.FindAsync([userId], cancellationToken))?.FullName ?? "Ban quản lý tiệc cưới";
        return MapReceiptView(project, await ComputeMoneyAsync(project, cancellationToken), managerName, DateTime.UtcNow);
    }

    public async Task<FileContentResultModel?> GetSettlementReceiptFileAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;
        var stored = await db.StoredFiles.AsNoTracking().Where(f => f.ProjectId == project.ProjectId && f.Purpose == "SettlementReceipt")
            .OrderByDescending(f => f.UploadedAt).FirstOrDefaultAsync(cancellationToken);
        if (stored is not null && await ReadStoredAsync(stored, cancellationToken) is { } file) return file;

        var money = await ComputeMoneyAsync(project, cancellationToken);
        if (project.Status != EventStatus.Settled && money.Outstanding > 0) return null;
        var pdf = ReceiptPdfGenerator.GenerateFinalSettlementReceiptPdf(BuildPdfModel(project, money, project.Manager?.FullName ?? "Ban quản lý", DateTime.UtcNow));
        return new FileContentResultModel(pdf, "application/pdf", $"Bien_nhan_tat_toan_{project.ProjectCode}.pdf");
    }

    public async Task<FileContentResultModel?> GetPaymentReceiptFileAsync(string projectCode, string paymentType, int userId, string role, CancellationToken cancellationToken = default)
    {
        var project = await ResolveProjectAsync(projectCode, userId, role, cancellationToken);
        if (project is null) return null;
        var payment = project.Payments.FirstOrDefault(p => string.Equals(p.PaymentType, paymentType, StringComparison.OrdinalIgnoreCase));
        if (payment?.ReceiptFilePath is { Length: > 0 } key && await files.ReadAsync(key, cancellationToken) is { } content)
            return new FileContentResultModel(content, payment.ReceiptContentType ?? "application/pdf", payment.ReceiptFileName ?? $"Receipt_{project.ProjectCode}_{payment.PaymentType}.pdf");
        var purpose = string.Equals(paymentType, PaymentKinds.Deposit, StringComparison.OrdinalIgnoreCase) ? "DepositReceipt" : "PaymentReceipt";
        var stored = await db.StoredFiles.AsNoTracking().Where(f => f.ProjectId == project.ProjectId && f.Purpose == purpose)
            .OrderByDescending(f => f.UploadedAt).FirstOrDefaultAsync(cancellationToken);
        return stored is null ? null : await ReadStoredAsync(stored, cancellationToken);
    }

    private async Task<FileContentResultModel?> ReadStoredAsync(StoredFile stored, CancellationToken cancellationToken)
    {
        var content = await files.ReadAsync(stored.StorageKey, cancellationToken);
        return content is null ? null : new FileContentResultModel(content, stored.ContentType, stored.OriginalName);
    }

    private Task<WeddingProject?> ResolveProjectAsync(string projectCode, int userId, string role, CancellationToken cancellationToken) =>
        ScopedProjects(db, projectCode, userId, role).FirstOrDefaultAsync(cancellationToken);

    /// <summary>The event with what the payment screens need, within the caller's scope (customer: their own; Manager: operated; Admin: all).</summary>
    internal static IQueryable<WeddingProject> ScopedProjects(WpsmDbContext db, string projectCode, int userId, string role)
    {
        var query = db.WeddingProjects.Include(p => p.Customer).Include(p => p.Manager).Include(p => p.Hall).Include(p => p.Slot)
            .Include(p => p.Payments).ThenInclude(p => p.RecordedByNavigation)
            .Where(p => p.ProjectCode == projectCode);
        if (role == "Customer") query = query.Where(p => p.CustomerId == userId);
        else if (role == "Manager") query = query.Where(p => p.ManagerId == userId || p.ProjectManagers.Any(pm => pm.ManagerId == userId && pm.RemovedAt == null));
        return query;
    }

    private Task<Money> ComputeMoneyAsync(WeddingProject project, CancellationToken cancellationToken) => ComputeMoneyAsync(db, project, cancellationToken);

    internal static async Task<Money> ComputeMoneyAsync(WpsmDbContext db, WeddingProject project, CancellationToken cancellationToken)
    {
        var addonTotal = await EventFlowSupport.ConfirmedAddonTotalAsync(db, project.ProjectId, cancellationToken);
        var grand = PaymentCalculator.CalculateGrandTotal(project.CurrentTotal, addonTotal);
        var paid = project.Payments.Where(p => p.Status == "Paid").ToList();
        var deposit = paid.Where(p => p.PaymentType == PaymentKinds.Deposit).Sum(p => p.Amount ?? 0m);
        var settlement = paid.Where(p => p.PaymentType == PaymentKinds.Settlement).Sum(p => p.Amount ?? 0m);
        var total = deposit + settlement;
        return new Money(project.CurrentTotal, addonTotal, grand, deposit, settlement, total, PaymentCalculator.CalculateOutstandingBalance(grand, total));
    }

    private async Task<ManagerPaymentOverviewView> BuildOverviewAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        var today = PaymentCalculator.GetVietnamToday();
        var money = await ComputeMoneyAsync(project, cancellationToken);
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var completed = PaymentCalculator.IsEventCompleted(project.EventDate, project.Status, today);
        var settled = project.Status == EventStatus.Settled;
        var summary = new ManagerPaymentSummaryView(project.ProjectId, project.ProjectCode, project.Customer.FullName, project.Hall.Name, project.Slot.SlotName,
            project.EventDate, project.Status, project.CurrentTotal, money.AddonTotal, money.GrandTotal, money.TotalPaid, money.Outstanding, completed, settled);

        var deposit = project.Payments.FirstOrDefault(p => p.PaymentType == PaymentKinds.Deposit);
        var settlement = project.Payments.FirstOrDefault(p => p.PaymentType == PaymentKinds.Settlement);
        var settlementDue = EventStageRules.SettlementDueDate(project.EventDate, policy);
        var settlementAmount = settlement?.Status == "Paid" ? settlement.Amount ?? 0m
            : PaymentCalculator.CalculateSettlementAmount(money.ContractTotal, money.AddonTotal, money.DepositPaid);
        var milestones = new List<ManagerPaymentMilestoneView>
        {
            new(deposit?.PaymentId, PaymentKinds.Deposit, PaymentKinds.Label(PaymentKinds.Deposit), deposit?.DueDate ?? DateOnly.FromDateTime(project.CreatedAt),
                deposit?.Amount ?? project.DepositAmount, deposit?.Status == "Paid" ? deposit.Amount ?? 0m : 0m,
                PaymentCalculator.ResolvePaymentStatus(deposit?.Status, deposit?.DueDate, today), deposit?.PaymentMethod, deposit?.PaidDate,
                deposit?.RecordedByNavigation?.FullName, HasReceipt(deposit), $"/api/wpsm/projects/{project.ProjectCode}/payments/{PaymentKinds.Deposit}/receipt"),
            new(settlement?.PaymentId, PaymentKinds.Settlement, PaymentKinds.Label(PaymentKinds.Settlement), settlement?.DueDate ?? settlementDue,
                settlementAmount, settlement?.Status == "Paid" ? settlement.Amount ?? 0m : 0m,
                settled || settlement?.Status == "Paid" ? "Paid" : completed ? "Due" : "Upcoming", settlement?.PaymentMethod, settlement?.PaidDate,
                settlement?.RecordedByNavigation?.FullName, HasReceipt(settlement) || settled,
                HasReceipt(settlement) || settled ? $"/api/wpsm/projects/{project.ProjectCode}/settlement/receipt" : null)
        };
        var history = project.Payments.Where(p => p.Status == "Paid").OrderByDescending(p => p.PaidDate).ThenByDescending(p => p.PaymentId)
            .Select(p => new ManagerPaymentHistoryItemView(p.PaymentId, p.PaymentType, p.Amount ?? 0m, p.PaidDate, p.PaymentMethod, p.Status,
                p.RecordedByNavigation?.FullName, HasReceipt(p), $"/api/wpsm/projects/{project.ProjectCode}/payments/{p.PaymentType}/receipt",
                p.PaidDate?.ToDateTime(TimeOnly.MinValue))).ToList();
        return new ManagerPaymentOverviewView(summary, milestones, history, BuildSettlement(project, money))
        {
            Invoice = await EventFlowSupport.LoadInvoiceViewAsync(db, project.ProjectId, project.ProjectCode, cancellationToken)
        };
    }

    private static bool HasReceipt(Payment? p) => p?.ReceiptFilePath is not null;

    private static SettlementCalculationView BuildSettlement(WeddingProject project, Money money)
    {
        var completed = PaymentCalculator.IsEventCompleted(project.EventDate, project.Status, PaymentCalculator.GetVietnamToday());
        return new SettlementCalculationView(project.CurrentTotal, money.AddonTotal, money.GrandTotal, money.DepositPaid, money.SettlementPaid,
            money.TotalPaid, money.Outstanding, completed, completed && money.Outstanding == 0, project.Status == EventStatus.Settled, DateTime.UtcNow);
    }

    internal static FinalSettlementReceiptPdfModel BuildPdfModel(WeddingProject project, Money money, string managerName, DateTime issueDate) =>
        new($"BN-{project.ProjectCode}-{issueDate:yyyyMMddHHmm}", project.ProjectCode, project.Customer.FullName, project.Customer.Email,
            project.Customer.Phone ?? "", project.EventDate, project.Hall.Name, money.ContractTotal, money.AddonTotal, money.GrandTotal, money.TotalPaid,
            0m, EventStatus.Settled, managerName, issueDate,
            project.Payments.Where(p => p.Status == "Paid").OrderBy(p => p.PaidDate)
                .Select(p => (PaymentKinds.Label(p.PaymentType), p.PaidDate, p.PaymentMethod, p.Amount ?? 0m, (string?)(p.RecordedByNavigation?.FullName ?? managerName)))
                .ToList());

    private static FinalSettlementReceiptView MapReceiptView(WeddingProject project, Money money, string managerName, DateTime issueDate) =>
        new($"BN-{project.ProjectCode}-{issueDate:yyyyMMddHHmm}", project.ProjectCode, project.Customer.FullName, project.Customer.Email,
            project.Customer.Phone ?? "", project.EventDate, project.Hall.Name, money.ContractTotal, money.AddonTotal, money.GrandTotal, money.TotalPaid,
            0m, EventStatus.Settled, managerName, issueDate,
            project.Payments.Where(p => p.Status == "Paid").OrderBy(p => p.PaidDate)
                .Select(p => new SettlementPaymentItemView(PaymentKinds.Label(p.PaymentType), p.PaidDate, p.PaymentMethod, p.Amount ?? 0m,
                    p.RecordedByNavigation?.FullName ?? managerName)).ToList(),
            $"/api/wpsm/projects/{project.ProjectCode}/settlement/receipt");
}
