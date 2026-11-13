using System.Net;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Persistence.Entities;
using static Infrastructure.Persistence.Repositories.OperationsSupport;

namespace Infrastructure.Persistence.Repositories;

/// <summary>UC-12, UC-33, UC-34, JOB-02, JOB-06 (GBR-08, SDS IV.4.4).</summary>
public sealed class PaymentRepository(WpsmDbContext db, IFileService files, IPdfGenerator pdf, IEmailOutbox emailOutbox) : IPaymentRepository
{
    private static readonly string[] Order = ["NRD", "T30", "FINAL"];

    public async Task<PaymentsView?> GetAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await LoadAsync(projectCode, cancellationToken);
        return project is null ? null : await MapAsync(project, cancellationToken);
    }

    public async Task<PaymentsView?> RecordAsync(string projectCode, int managerId, OpRecordPaymentCommand command, CancellationToken cancellationToken = default)
    {
        var project = await LoadAsync(projectCode, cancellationToken);
        if (project is null) return null;
        if (command.Type is not ("T30" or "FINAL")) throw new InvalidOperationException("Trường Loại thanh toán là bắt buộc (T-30 hoặc Quyết toán).");
        if (command.Method is not ("Bank" or "Cash")) throw new InvalidOperationException("Trường Phương thức là bắt buộc.");
        if (command.Amount <= 0) throw new InvalidOperationException("Số tiền phải lớn hơn 0.");
        var today = TodayVn();
        if (command.PaidDate > today) throw new InvalidOperationException("Ngày thanh toán không được ở tương lai.");
        if (command.Method == "Bank" && (command.Receipt is null || command.Receipt.Length == 0))
            throw new InvalidOperationException("Chứng từ chuyển khoản là bắt buộc khi thanh toán qua ngân hàng.");
        if (project.Status is "Active Baseline") throw new InvalidOperationException("Baseline chưa được chốt nên chưa ghi nhận thanh toán đợt sau.");
        if (project.Status is "Settled" or "Closed") throw new InvalidOperationException("Dự án đã quyết toán.");

        var outstanding = Outstanding(project, project.Payments);
        if (command.Amount > outstanding) throw new InvalidOperationException($"Số tiền vượt quá số dư còn lại {Money(outstanding)} (MSG11).");
        if (command.Type == "FINAL")
        {
            // UC-33 alternative flow: the final payment is recorded only after the event.
            if (project.EventDate >= today) throw new InvalidOperationException("Chỉ ghi nhận thanh toán quyết toán sau khi tiệc đã diễn ra.");
            if (command.Amount != outstanding) throw new InvalidOperationException($"Thanh toán quyết toán phải bằng số dư còn lại {Money(outstanding)}.");
            if (project.Status == "Baseline Frozen") project.Status = "Event Completed";
        }

        var payment = project.Payments.FirstOrDefault(x => x.PaymentType == command.Type);
        if (payment?.Status == "Paid") throw new InvalidOperationException("Đợt thanh toán này đã được ghi nhận.");
        if (payment is null)
        {
            payment = new Payment { ProjectId = project.ProjectId, PaymentType = command.Type };
            project.Payments.Add(payment);
        }
        if (command.Receipt is { Length: > 0 })
        {
            var stored = await files.StoreAsync(project.ProjectId, "PaymentReceipt", command.ReceiptName ?? $"{command.Type}.pdf", command.Receipt, managerId, cancellationToken);
            payment.ReceiptFilePath = stored.Id.ToString();
        }
        payment.Status = "Paid";
        payment.Amount = command.Amount;
        payment.PaymentMethod = command.Method;
        payment.PaidDate = command.PaidDate;
        payment.RecordedBy = managerId;
        Notify(db, project.CustomerId, "Payment", $"Đã ghi nhận thanh toán {(command.Type == "T30" ? "đợt T-30" : "quyết toán")}: {Money(command.Amount)}.", Links.CustomerPayments);
        await db.SaveChangesAsync(cancellationToken);

        if (command.Type == "FINAL") return await IssueSettlementReceiptAsync(projectCode, managerId, cancellationToken);
        return await MapAsync(project, cancellationToken);
    }

    public async Task<PaymentsView?> IssueSettlementReceiptAsync(string projectCode, int managerId, CancellationToken cancellationToken = default)
    {
        var project = await LoadAsync(projectCode, cancellationToken);
        if (project is null) return null;
        if (await ReceiptAsync(project.ProjectId, cancellationToken) is not null) return await MapAsync(project, cancellationToken);
        if (project.Status != "Event Completed") throw new InvalidOperationException("Chỉ phát hành biên nhận quyết toán khi tiệc đã hoàn thành.");
        var outstanding = Outstanding(project, project.Payments);
        if (outstanding != 0) throw new InvalidOperationException($"Còn số dư {Money(outstanding)}, chưa thể quyết toán.");

        var manager = await db.Users.AsNoTracking().Where(x => x.UserId == managerId).Select(x => x.FullName).FirstAsync(cancellationToken);
        var bytes = pdf.Render(new PdfDocumentModel($"Biên nhận quyết toán {project.ProjectCode}", "Xác nhận dự án đã thanh toán đủ",
            [
                ("Khách hàng", project.Customer.FullName), ("Ngày tiệc", project.EventDate.ToString("dd/MM/yyyy")),
                ("Giá trị hợp đồng gốc", Money(project.BaseContractValue)), ("Phát sinh từ CR đã duyệt", Money(project.CurrentTotal - project.BaseContractValue)),
                ("Tổng giá trị", Money(project.CurrentTotal)), ("Đã thanh toán", Money(project.CurrentTotal - outstanding)), ("Số dư", Money(0)),
                ("Ngày phát hành", TodayVn().ToString("dd/MM/yyyy")), ("Manager phát hành", manager)
            ],
            [new PdfTable("Các đợt thanh toán", ["Đợt", "Ngày", "Phương thức", "Số tiền"],
                project.Payments.Where(x => x.Status == "Paid").OrderBy(x => Array.IndexOf(Order, x.PaymentType))
                    .Select(x => (IReadOnlyList<string>)[x.PaymentType, x.PaidDate?.ToString("dd/MM/yyyy") ?? "", x.PaymentMethod ?? "", Money(x.Amount ?? 0)]).ToList())],
            []));
        await files.StoreAsync(project.ProjectId, "SettlementReceipt", $"QT-{project.ProjectCode}.pdf", bytes, managerId, cancellationToken);
        project.Status = "Settled";
        Notify(db, project.CustomerId, "Payment", $"Dự án {project.ProjectCode} đã quyết toán. Bạn có thể tải biên nhận và đánh giá dịch vụ.", Links.CustomerPayments);
        await db.SaveChangesAsync(cancellationToken);
        return await MapAsync(project, cancellationToken);
    }

    public async Task<int> IssueT30PaymentsAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var due = today.AddDays(30);
        var projects = await db.WeddingProjects.Include(x => x.Customer).Include(x => x.Payments)
            .Where(x => x.Status == "Baseline Frozen" && x.EventDate <= due && x.EventDate > today && !x.Payments.Any(p => p.PaymentType == "T30" && p.Status != "Upcoming"))
            .ToListAsync(cancellationToken);
        var ratio = await RatioAsync(db, "T30_RATIO", today, 70m, cancellationToken);
        var issued = 0;
        foreach (var project in projects)
        {
            // T-30 ratio is cumulative: the installment tops paid money up to that share of the current total (L-12).
            var amount = Math.Round(project.CurrentTotal * ratio / 100m, 0, MidpointRounding.AwayFromZero) - project.Payments.Where(x => x.Status == "Paid").Sum(x => x.Amount ?? 0);
            if (amount <= 0) continue;
            var payment = project.Payments.FirstOrDefault(x => x.PaymentType == "T30");
            if (payment is null) project.Payments.Add(payment = new Payment { PaymentType = "T30" });
            payment.Status = "Due";
            payment.Amount = amount;
            payment.DueDate = project.EventDate.AddDays(-30) < today ? today : project.EventDate.AddDays(-30);
            Notify(db, project.CustomerId, "Payment", $"Đợt thanh toán T-30 {Money(amount)} đã đến hạn.", Links.CustomerPayments);
            emailOutbox.Enqueue(project.Customer.Email, $"Thanh toán đợt T-30 cho {project.ProjectCode}",
                $"<p>Xin chào {WebUtility.HtmlEncode(project.Customer.FullName)},</p><p>Đợt thanh toán T-30 của dự án {project.ProjectCode} là <b>{Money(amount)}</b>, hạn {payment.DueDate:dd/MM/yyyy}.</p>",
                "PaymentRequest", project.ProjectId);
            issued++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return issued;
    }

    public async Task<int> PrepareFinalSettlementsAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var projects = await db.WeddingProjects.Include(x => x.Payments).Where(x => x.Status == "Baseline Frozen" && x.EventDate < today)
            .ToListAsync(cancellationToken);
        foreach (var project in projects)
        {
            project.Status = "Event Completed";
            var outstanding = Outstanding(project, project.Payments);
            var payment = project.Payments.FirstOrDefault(x => x.PaymentType == "FINAL");
            if (outstanding > 0 && payment?.Status != "Paid")
            {
                if (payment is null) project.Payments.Add(payment = new Payment { PaymentType = "FINAL" });
                payment.Status = "Due";
                payment.Amount = outstanding;
                payment.DueDate = today;
            }
            Notify(db, project.ManagerId, "Payment", $"Tiệc {project.ProjectCode} đã diễn ra. Số dư cần quyết toán: {Money(outstanding)}.", Links.ManagerPayments(project.ProjectCode));
        }
        await db.SaveChangesAsync(cancellationToken);
        return projects.Count;
    }

    private async Task<PaymentsView> MapAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        var paid = project.Payments.Where(x => x.Status == "Paid").Sum(x => x.Amount ?? 0);
        var receipt = await ReceiptAsync(project.ProjectId, cancellationToken);
        return new PaymentsView(project.ProjectCode, project.Customer.FullName, project.Status, project.EventDate, project.BaseContractValue,
            project.CurrentTotal - project.BaseContractValue, project.CurrentTotal, paid, project.CurrentTotal - paid,
            project.Payments.OrderBy(x => Array.IndexOf(Order, x.PaymentType)).Select(x => new PaymentLineView(x.PaymentId, x.PaymentType, x.Status, x.Amount,
                x.DueDate, x.PaidDate, x.PaymentMethod, !string.IsNullOrEmpty(x.ReceiptFilePath) || x.ReceiptContent is { Length: > 0 },
                Guid.TryParse(x.ReceiptFilePath, out var id) ? id : null)).ToList(),
            project.Status is "Baseline Frozen" or "Event Completed" && project.EventDate < TodayVn(), receipt?.FileId, receipt is null ? null : Utc(receipt.UploadedAt));
    }

    private Task<StoredFile?> ReceiptAsync(int projectId, CancellationToken cancellationToken) =>
        db.StoredFiles.AsNoTracking().Where(x => x.ProjectId == projectId && x.Purpose == "SettlementReceipt")
            .OrderByDescending(x => x.UploadedAt).FirstOrDefaultAsync(cancellationToken);

    private Task<WeddingProject?> LoadAsync(string projectCode, CancellationToken cancellationToken) =>
        db.WeddingProjects.Include(x => x.Customer).Include(x => x.Payments).FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
}
