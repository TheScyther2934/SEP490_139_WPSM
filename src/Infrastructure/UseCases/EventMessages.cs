using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Application.UseCases;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;

namespace Infrastructure.UseCases;

/// <summary>Wording, links and email templates of the business events (staged in the DbContext, saved by the use case).</summary>
public sealed class EventMessages(WpsmDbContext db, IEmailOutbox emailOutbox, EmailOptions emailOptions) : IEventMessages
{
    private string BaseUrl => emailOptions.PublicBaseUrl?.TrimEnd('/') ?? "http://localhost:5173";

    public Task EventBookedAsync(EventParty party, bool depositPaid, string? temporaryPassword, CancellationToken cancellationToken = default)
    {
        // G-04: the customer learns the event exists and whether the deposit is still due.
        OperationsSupport.Notify(db, party.CustomerId, NotificationTypes.Payment, depositPaid
                ? $"Sự kiện {party.ProjectCode} đã được tạo. Tiền đặt cọc đã được xác nhận."
                : $"Sự kiện {party.ProjectCode} đã được tạo. Vui lòng thanh toán tiền đặt cọc.",
            "/customer/payments");
        // One delivery path only: the outbox row commits with the event and JOB-07 sends it (SDS outbox pattern).
        if (temporaryPassword is not null) QueueCredentials(party, temporaryPassword);
        return Task.CompletedTask;
    }

    public Task DepositReceiptRecordedAsync(EventParty party, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, party.CustomerId, NotificationTypes.Payment,
            $"Biên nhận tiền đặt cọc của sự kiện {party.ProjectCode} đã được ghi nhận.", "/customer/payments");
        return Task.CompletedTask;
    }

    public Task CredentialsResentAsync(EventParty party, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        QueueCredentials(party, temporaryPassword);
        return Task.CompletedTask;
    }

    public Task SettlementRecordedAsync(int customerId, string projectCode, decimal amount, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, customerId, NotificationTypes.Payment,
            $"Sự kiện {projectCode} đã tất toán {EventStageRules.Vnd(amount)}. Biên nhận tất toán đã sẵn sàng tải về.", OperationsSupport.Links.CustomerPayments);
        return Task.CompletedTask;
    }

    public Task EventCancelledAsync(int customerId, string projectCode, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, customerId, NotificationTypes.Payment,
            $"Sự kiện {projectCode} đã được hủy theo yêu cầu. Tiền đặt cọc không được hoàn lại theo hợp đồng.", OperationsSupport.Links.CustomerEvent);
        return Task.CompletedTask;
    }

    /// <summary>The start of a supplier reminder: used to send one reminder per operator per day.</summary>
    public static string SupplierReminderMarker(string projectCode) => $"[{projectCode}] Quá hạn đặt nhà cung cấp";

    public Task SupplierEvidenceMissingAsync(int operatorId, string projectCode, IReadOnlyList<string> services, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, operatorId, NotificationTypes.StageReminder,
            $"{SupplierReminderMarker(projectCode)}: còn thiếu minh chứng cho {string.Join(", ", services.Take(4))}{(services.Count > 4 ? "…" : "")}.",
            OperationsSupport.Links.ManagerSuppliers(projectCode));
        return Task.CompletedTask;
    }

    public Task SettlementInvoiceIssuedAsync(IReadOnlyList<int> operatorIds, int customerId, string projectCode, string invoiceCode, decimal amountDue,
        DateOnly dueDate, CancellationToken cancellationToken = default)
    {
        foreach (var operatorId in operatorIds)
            OperationsSupport.Notify(db, operatorId, NotificationTypes.Payment,
                $"[{projectCode}] Hóa đơn tất toán {invoiceCode}: cần thu {EventStageRules.Vnd(amountDue)}, hạn {dueDate:dd/MM/yyyy}.", OperationsSupport.Links.ManagerPayments(projectCode));
        OperationsSupport.Notify(db, customerId, NotificationTypes.Payment,
            $"Sự kiện {projectCode} đã hoàn tất. Hóa đơn tất toán {EventStageRules.Vnd(amountDue)}, hạn {dueDate:dd/MM/yyyy}.", OperationsSupport.Links.CustomerPayments);
        return Task.CompletedTask;
    }

    public Task SettlementInvoiceEmailAsync(Domain.Entities.WeddingProject project, Domain.Entities.SettlementInvoice invoice, decimal amountDue,
        CancellationToken cancellationToken = default)
    {
        emailOutbox.Enqueue(project.Customer.Email, $"Hóa đơn tất toán sự kiện {project.ProjectCode}",
            EmailTemplates.SettlementInvoice(project.Customer.FullName, project.ProjectCode, invoice.ContractTotal, invoice.AddonTotal, invoice.DepositPaid,
                amountDue, invoice.DueDate, $"{BaseUrl}/customer/payments"), "PaymentRequest", project.ProjectId);
        return Task.CompletedTask;
    }

    public Task CoOperatorAddedAsync(int managerId, string projectCode, string reason, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, managerId, NotificationTypes.Delegation,
            $"Bạn được ủy quyền đồng điều hành sự kiện {projectCode}. Lý do: {reason}", OperationsSupport.Links.ManagerProject(projectCode));
        return Task.CompletedTask;
    }

    public Task CoOperatorRemovedAsync(int managerId, string projectCode, string reason, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, managerId, NotificationTypes.Delegation,
            $"Quyền đồng điều hành sự kiện {projectCode} của bạn đã kết thúc. Lý do: {reason}", "/manager/projects");
        return Task.CompletedTask;
    }

    public Task CoordinationDraftReadyAsync(int managerId, string projectCode, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, managerId, NotificationTypes.Beo,
            $"Sự kiện {projectCode} đã khóa dịch vụ. Bản thảo phiếu điều phối v1 đã sẵn sàng, vui lòng kiểm tra và chốt.", OperationsSupport.Links.ManagerBeo(projectCode));
        return Task.CompletedTask;
    }

    public Task ComplaintFiledAsync(int managerId, string projectCode, string complaintCode, int sheetVersion, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, managerId, NotificationTypes.Complaint,
            $"Khách hàng sự kiện {projectCode} vừa gửi khiếu nại {complaintCode} (đối chiếu BEO v{sheetVersion}).",
            OperationsSupport.Links.ManagerComplaint(projectCode, complaintCode));
        return Task.CompletedTask;
    }

    public Task ComplaintResolvedByOperatorAsync(int customerId, string complaintCode, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, customerId, NotificationTypes.Complaint,
            $"Khiếu nại {complaintCode} đã được Quản lý sự kiện phản hồi giải quyết.", OperationsSupport.Links.CustomerComplaint(complaintCode));
        return Task.CompletedTask;
    }

    public async Task ComplaintEscalatedAsync(int customerId, string projectCode, string complaintCode, CancellationToken cancellationToken = default)
    {
        var admins = await db.Users.AsNoTracking().Where(u => u.Role == "Admin" && u.Status == "Active").Select(u => u.UserId).ToListAsync(cancellationToken);
        foreach (var adminId in admins)
            OperationsSupport.Notify(db, adminId, NotificationTypes.Complaint,
                $"Khiếu nại {complaintCode} của sự kiện {projectCode} đã được chuyển cấp (Escalated) lên Admin.", OperationsSupport.Links.AdminComplaints);
        OperationsSupport.Notify(db, customerId, NotificationTypes.Complaint,
            $"Khiếu nại {complaintCode} đã được chuyển cấp lên Ban Quản Trị (Admin) để ra quyết định cuối cùng.", OperationsSupport.Links.CustomerComplaint(complaintCode));
    }

    public Task ComplaintResolvedByAdminAsync(int managerId, int customerId, string projectCode, string complaintCode, CancellationToken cancellationToken = default)
    {
        var message = $"Khiếu nại {complaintCode} đã được Admin giải quyết.";
        OperationsSupport.Notify(db, managerId, NotificationTypes.Complaint, message, OperationsSupport.Links.ManagerComplaint(projectCode, complaintCode));
        OperationsSupport.Notify(db, customerId, NotificationTypes.Complaint, message, OperationsSupport.Links.CustomerComplaint(complaintCode));
        return Task.CompletedTask;
    }

    public Task ExceptionDecidedAsync(int requestedBy, string requestCode, string projectCode, bool approved, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, requestedBy, NotificationTypes.OverdueAlert,
            $"Yêu cầu đặc biệt {requestCode} ({projectCode}) đã được {(approved ? "chấp thuận" : "từ chối")}.", OperationsSupport.Links.ResourceCalendar);
        return Task.CompletedTask;
    }

    private void QueueCredentials(EventParty party, string temporaryPassword) =>
        emailOutbox.Enqueue(party.CustomerEmail, $"Tài khoản WPSM cho sự kiện {party.ProjectCode}",
            EmailTemplates.Credentials(party.CustomerName, party.CustomerEmail, temporaryPassword, party.ProjectCode, emailOptions.PublicBaseUrl),
            "Credentials", party.ProjectId);

    public Task CoordinationSheetApprovedAsync(EventParty party, int version, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, party.CustomerId, NotificationTypes.Beo,
            $"Phiếu điều phối v{version} của sự kiện {party.ProjectCode} đã được duyệt và chuyển tới 4 bộ phận.", OperationsSupport.Links.CustomerEvent);
        return Task.CompletedTask;
    }

    public Task AddonAwaitingConfirmationAsync(EventParty party, string addonCode, string title, decimal total, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, party.CustomerId, NotificationTypes.AddonContract,
            $"Sự kiện {party.ProjectCode} có hợp đồng phát sinh {addonCode} ({EventStageRules.Vnd(total)}) cần bạn đối chiếu và xác nhận.",
            OperationsSupport.Links.CustomerEvent);
        emailOutbox.Enqueue(party.CustomerEmail, $"Xác nhận hợp đồng phát sinh {addonCode}",
            EmailTemplates.AddonConfirmRequest(party.CustomerName, party.ProjectCode, addonCode, title, total, BaseUrl), "Reminder", party.ProjectId);
        return Task.CompletedTask;
    }

    public Task AddonCancelledAsync(EventParty party, string addonCode, string reason, CancellationToken cancellationToken = default)
    {
        OperationsSupport.Notify(db, party.CustomerId, NotificationTypes.AddonContract,
            $"Hợp đồng phát sinh {addonCode} của sự kiện {party.ProjectCode} đã được hủy: {reason}", OperationsSupport.Links.CustomerEvent);
        return Task.CompletedTask;
    }

    public async Task AddonConfirmedAsync(EventParty party, string addonCode, decimal total, CancellationToken cancellationToken = default)
    {
        foreach (var operatorId in await EventFlowSupport.OperatorIdsAsync(db, party.ProjectId, cancellationToken))
            OperationsSupport.Notify(db, operatorId, NotificationTypes.AddonContract,
                $"[{party.ProjectCode}] Khách đã xác nhận hợp đồng phát sinh {addonCode} ({EventStageRules.Vnd(total)}). Có thể thực hiện.",
                OperationsSupport.Links.ManagerAddons(party.ProjectCode));
    }

    public async Task ServicesLockedAsync(EventParty party, EventPolicy policy, CancellationToken cancellationToken = default)
    {
        var hotline = await EventFlowSupport.LoadHotlineAsync(db, cancellationToken);
        var contacts = await EventFlowSupport.LoadContactsAsync(db, party.ProjectId, cancellationToken);
        var contactText = string.Join("<br>", contacts.Select(c => System.Net.WebUtility.HtmlEncode($"{c.FullName} – {c.Phone ?? c.Email}")));
        OperationsSupport.Notify(db, party.CustomerId, NotificationTypes.StageReminder,
            $"Dịch vụ của sự kiện {party.ProjectCode} đã được chốt. Cần thay đổi, hãy liên hệ người phụ trách hoặc hotline {hotline}.",
            OperationsSupport.Links.CustomerEvent);
        foreach (var operatorId in await EventFlowSupport.OperatorIdsAsync(db, party.ProjectId, cancellationToken))
            OperationsSupport.Notify(db, operatorId, NotificationTypes.StageReminder,
                $"[{party.ProjectCode}] Đã khóa dịch vụ. Đặt nhà cung cấp và tải minh chứng trước {EventStageRules.SupplierDeadline(party.EventDate, policy):dd/MM/yyyy}.",
                OperationsSupport.Links.ManagerSuppliers(party.ProjectCode));
        emailOutbox.Enqueue(party.CustomerEmail, $"Sự kiện {party.ProjectCode} đã chốt dịch vụ",
            EmailTemplates.EventLocked(party.CustomerName, party.ProjectCode, party.EventDate, contactText, hotline, BaseUrl), "Reminder", party.ProjectId);
    }
}
