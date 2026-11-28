using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Application.UseCases.Settlement;
using Domain.Entities;
using Domain.Services;
using Infrastructure.Files;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;

namespace Infrastructure.UseCases;

/// <summary>Settlement data for the use cases: the event in the caller's scope, the invoice (shared with JOB-06), receipt files.</summary>
public sealed class SettlementStore(WpsmDbContext db, IFileService files) : ISettlementStore
{
    public Task<WeddingProject?> GetEventAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default) =>
        ManagerPaymentRepository.ScopedProjects(db, projectCode, userId, role).FirstOrDefaultAsync(cancellationToken);

    public Task<decimal> GetConfirmedAddonTotalAsync(int projectId, CancellationToken cancellationToken = default) =>
        EventFlowSupport.ConfirmedAddonTotalAsync(db, projectId, cancellationToken);

    public async Task<SettlementInvoice> StageInvoiceAsync(WeddingProject project, int? issuedBy, CancellationToken cancellationToken = default) =>
        await EventFlowSupport.IssueInvoiceAsync(db, project, issuedBy, await EventFlowSupport.LoadEventPolicyAsync(db, project.ProjectId, cancellationToken), cancellationToken);

    public async Task<StoredReceipt> StoreReceiptAsync(int projectId, string fileName, byte[] content, int uploaderId, CancellationToken cancellationToken = default)
    {
        var file = await files.StoreAsync(projectId, "PaymentReceipt", fileName, content, uploaderId, cancellationToken);
        return new StoredReceipt(file.StorageKey, file.OriginalName, file.ContentType);
    }

    public void Add(Payment payment) => db.Payments.Add(payment);

    public async Task<IReadOnlyList<WeddingProject>> GetLockedEventsHeldUntilAsync(DateOnly lastEventDay, CancellationToken cancellationToken = default) =>
        await db.WeddingProjects.Include(p => p.Customer).Include(p => p.Payments)
            .Where(p => p.EventDate <= lastEventDay && p.Status == EventStatus.Locked).OrderBy(p => p.ProjectId).ToListAsync(cancellationToken);

    public Task<IReadOnlyList<int>> GetOperatorIdsAsync(int projectId, CancellationToken cancellationToken = default) =>
        EventFlowSupport.OperatorIdsAsync(db, projectId, cancellationToken);
}

/// <summary>The settlement receipt PDF of an event, generated from what was paid and stored like every file.</summary>
public sealed class SettlementDocuments(WpsmDbContext db, IFileService files) : ISettlementDocuments
{
    public async Task StoreSettlementReceiptAsync(WeddingProject project, int managerId, CancellationToken cancellationToken = default)
    {
        var managerName = (await db.Users.FindAsync([managerId], cancellationToken))?.FullName ?? "Ban quản lý tiệc cưới";
        var money = await ManagerPaymentRepository.ComputeMoneyAsync(db, project, cancellationToken);
        var pdf = ReceiptPdfGenerator.GenerateFinalSettlementReceiptPdf(ManagerPaymentRepository.BuildPdfModel(project, money, managerName, DateTime.UtcNow));
        await files.StoreAsync(project.ProjectId, "SettlementReceipt", $"Bien_nhan_tat_toan_{project.ProjectCode}.pdf", pdf, managerId, cancellationToken);
    }
}
