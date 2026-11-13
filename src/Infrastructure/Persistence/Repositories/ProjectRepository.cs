using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository(WpsmDbContext db, ProjectReadModel readModel, IEmailOutbox emailOutbox, EmailOptions emailOptions) : IProjectRepository
{
    public Task<bool> CanAccessAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.AnyAsync(x => x.ProjectCode == projectCode &&
            (role == "Admin" || role == "Manager" && x.ManagerId == userId || role == "Customer" && x.CustomerId == userId), cancellationToken);

    public async Task<IReadOnlyList<WeddingProjectView>> GetProjectsAsync(int? userId = null, string? role = null, CancellationToken cancellationToken = default)
    {
        var query = readModel.ProjectViewsQuery();
        if (role == "Customer") query = query.Where(x => x.CustomerId == userId);
        else if (role == "Manager") query = query.Where(x => x.ManagerId == userId);
        var projects = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        var result = new List<WeddingProjectView>(projects.Count);
        foreach (var item in projects) result.Add(await readModel.MapProjectAsync(item, cancellationToken));
        return result;
    }

    public async Task<WeddingProjectView?> GetProjectAsync(string projectCode, CancellationToken cancellationToken = default)
    {
        var project = await readModel.ProjectViewsQuery().FirstOrDefaultAsync(x => x.ProjectCode == projectCode, cancellationToken);
        return project is null ? null : await readModel.MapProjectAsync(project, cancellationToken);
    }

    public async Task<CreatedProjectView> CreateProjectAsync(CreateProjectCommand command, int managerId, string customerPasswordHash, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var manager = await db.Users.SingleOrDefaultAsync(x => x.UserId == managerId && x.Role == "Manager" && x.Status == "Active", cancellationToken)
            ?? throw new InvalidOperationException("Tài khoản quản lý không hợp lệ hoặc đã bị vô hiệu hóa.");
        var hallName = command.Hall.Trim();
        if (string.IsNullOrWhiteSpace(command.CoupleName) || command.CoupleName.Length > 100 ||
            string.IsNullOrWhiteSpace(command.CustomerEmail) || command.CustomerEmail.Length > 100 ||
            !System.Net.Mail.MailAddress.TryCreate(command.CustomerEmail.Trim(), out var address) || address.Address != command.CustomerEmail.Trim())
            throw new InvalidOperationException("Tên khách hàng hoặc email không hợp lệ.");
        var phoneDigits = new string(command.CustomerPhone.Where(char.IsDigit).ToArray());
        if (phoneDigits.Length is < 9 or > 11) throw new InvalidOperationException("Số điện thoại cần có từ 9 đến 11 chữ số.");
        var hall = await db.Halls.SingleOrDefaultAsync(x => x.Status == "Active" && x.Name == hallName, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy sảnh đang hoạt động. Hãy kiểm tra danh mục sảnh trong database.");
        var slotName = WpsmValueNormalizer.NormalizeSlot(command.Slot);
        var slot = await db.Slots.SingleOrDefaultAsync(x => x.HallId == hall.HallId && x.SlotName == slotName, cancellationToken)
            ?? throw new InvalidOperationException("Ca tiệc không tồn tại cho sảnh đã chọn.");
        if (command.PaymentMethod is not ("Cash" or "Bank"))
            throw new InvalidOperationException("Phương thức thanh toán không hợp lệ.");
        if (await db.WeddingProjects.AnyAsync(x => x.SlotId == slot.SlotId && x.EventDate == command.EventDate, cancellationToken))
            throw new InvalidOperationException("Ca tiệc đã được đặt vào ngày này. Vui lòng chọn ca khác.");

        if (command.BaseValue <= 0 || command.NrdAmount < 0 || command.NrdAmount > command.BaseValue)
            throw new InvalidOperationException("Giá trị hợp đồng hoặc tiền NRD không hợp lệ.");
        if (command.EventDate < DateOnly.FromDateTime(DateTime.Today))
            throw new InvalidOperationException("Ngày tổ chức phải từ hôm nay trở đi.");

        var depositRatio = await db.PolicyConfigs.AsNoTracking()
            .Where(x => x.PolicyType == "NRD_RATIO" && x.EffectiveFrom <= DateOnly.FromDateTime(DateTime.Today))
            .OrderByDescending(x => x.EffectiveFrom).Select(x => x.RatePercent).FirstOrDefaultAsync(cancellationToken);
        if (depositRatio == 0) depositRatio = 30m;
        var requiredNrd = decimal.Round(command.BaseValue * (depositRatio / 100m), 2);
        if (command.NrdAmount < requiredNrd)
            throw new InvalidOperationException($"Số tiền đặt cọc NRD tối thiểu là {requiredNrd:N0} đ ({depositRatio:G29}% giá trị hợp đồng).");

        var customer = await db.Users.SingleOrDefaultAsync(x => x.Email == command.CustomerEmail.Trim(), cancellationToken);
        var newCustomer = customer is null;
        if (newCustomer)
        {
            customer = new User
            {
                Email = command.CustomerEmail.Trim(),
                FullName = command.CoupleName.Trim(),
                Phone = command.CustomerPhone.Trim(),
                Role = "Customer",
                Status = "Active",
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow,
                PasswordHash = customerPasswordHash
            };
            db.Users.Add(customer);
            await db.SaveChangesAsync(cancellationToken);
        }

        var today = DateTime.UtcNow;
        var prefix = $"WPSM-{today:yyyy}-";
        var lastNumber = await db.WeddingProjects.Where(x => x.ProjectCode.StartsWith(prefix))
            .Select(x => x.ProjectCode).ToListAsync(cancellationToken);
        var seq = lastNumber.Select(x => int.TryParse(x[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        var code = $"{prefix}{seq:D3}";

        var project = new WeddingProject
        {
            ProjectCode = code,
            CustomerId = customer!.UserId,
            ManagerId = manager.UserId,
            HallId = hall.HallId,
            SlotId = slot.SlotId,
            EventDate = command.EventDate,
            BaseContractValue = command.BaseValue,
            Nrdamount = command.NrdAmount,
            CurrentTotal = command.BaseValue,
            Status = "Active Baseline",
            CreatedAt = today
        };
        db.WeddingProjects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        var defaultItems = await db.ServiceCatalogItems.Where(x => x.Status == "Active").ToListAsync(cancellationToken);
        foreach (var item in defaultItems)
        {
            db.SpecItems.Add(new SpecItem
            {
                ProjectId = project.ProjectId, CatalogItemId = item.ItemId,
                Description = item.Name, Quantity = 1, UnitPrice = item.DefaultPrice, IsBaseline = true, Version = 1
            });
        }
        var payByCash = string.Equals(command.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase);
        db.Payments.Add(new Payment
        {
            ProjectId = project.ProjectId,
            PaymentType = "NRD",
            Status = payByCash ? "Paid" : "Due",
            DueDate = DateOnly.FromDateTime(DateTime.Today),
            Amount = command.NrdAmount,
            PaymentMethod = payByCash ? "Cash" : null,
            PaidDate = payByCash ? DateOnly.FromDateTime(DateTime.Today) : null,
            RecordedBy = payByCash ? manager.UserId : null
        });

        // G-04: Notify customer about project creation
        db.Notifications.Add(new Notification
        {
            RecipientId = customer.UserId,
            Type = "Payment",
            Message = payByCash 
                ? $"Dự án {project.ProjectCode} đã được khởi tạo. Đặt cọc NRD đã được xác nhận."
                : $"Dự án {project.ProjectCode} đã được khởi tạo. Vui lòng thanh toán đặt cọc NRD.",
            LinkUrl = "/customer/payments",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        if (newCustomer)
            emailOutbox.Enqueue(customer.Email, $"Tài khoản WPSM cho dự án {project.ProjectCode}",
                EmailTemplates.Credentials(customer.FullName, customer.Email, temporaryPassword, project.ProjectCode, emailOptions.PublicBaseUrl),
                "Credentials", project.ProjectId);
        await db.SaveChangesAsync(cancellationToken);
        if (newCustomer && command.ProtectedCredentialPayload is not null)
        {
            db.EmailDeliveries.Add(new EmailDelivery
            {
                ProjectId = project.ProjectId, Recipient = customer.Email,
                ProtectedPayload = command.ProtectedCredentialPayload,
                CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        var loaded = await readModel.ProjectViewsQuery().SingleAsync(x => x.ProjectId == project.ProjectId, cancellationToken);
        return new CreatedProjectView(await readModel.MapProjectAsync(loaded, cancellationToken), newCustomer ? temporaryPassword : null);
    }

    public async Task<bool> ResendCredentialsAsync(string projectCode, int managerId, string customerPasswordHash, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode && x.ManagerId == managerId, cancellationToken);
        if (project is null) return false;
        if (!project.Customer.MustChangePassword) throw new InvalidOperationException("Khách hàng đã kích hoạt tài khoản nên không cần gửi lại thông tin đăng nhập.");
        project.Customer.PasswordHash = customerPasswordHash;
        emailOutbox.Enqueue(project.Customer.Email, $"Tài khoản WPSM cho dự án {project.ProjectCode}",
            EmailTemplates.Credentials(project.Customer.FullName, project.Customer.Email, temporaryPassword, project.ProjectCode, emailOptions.PublicBaseUrl),
            "Credentials", project.ProjectId);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordNrdReceiptAsync(string projectCode, int managerId, byte[] content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments.Include(x => x.Project)
            .SingleOrDefaultAsync(x => x.Project.ProjectCode == projectCode && x.PaymentType == "NRD", cancellationToken);
        if (payment is null || payment.Project.ManagerId != managerId) return false;
        if (payment.Status == "Paid") throw new InvalidOperationException("NRD đã được ghi nhận là đã thanh toán.");
        payment.Status = "Paid";
        payment.PaymentMethod = "Bank";
        payment.ReceiptFilePath = "database";
        payment.ReceiptContent = content;
        payment.ReceiptFileName = fileName;
        payment.ReceiptContentType = contentType;
        payment.PaidDate = DateOnly.FromDateTime(DateTime.Today);
        payment.RecordedBy = managerId;

        // G-04: Notify customer that NRD payment is recorded
        db.Notifications.Add(new Notification
        {
            RecipientId = payment.Project.CustomerId,
            Type = "Payment",
            Message = $"Biên nhận đặt cọc NRD cho dự án {projectCode} đã được ghi nhận thành công.",
            LinkUrl = "/customer/payments",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordNrdReceiptAsync(string projectCode, int managerId, string receiptPath, CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments.Include(x => x.Project)
            .SingleOrDefaultAsync(x => x.Project.ProjectCode == projectCode && x.PaymentType == "NRD", cancellationToken);
        if (payment is null || payment.Project.ManagerId != managerId) return false;
        if (payment.Status == "Paid") throw new InvalidOperationException("NRD đã được ghi nhận là đã thanh toán.");
        payment.Status = "Paid";
        payment.PaymentMethod = "Bank";
        payment.ReceiptFilePath = receiptPath;
        payment.PaidDate = DateOnly.FromDateTime(DateTime.Today);
        payment.RecordedBy = managerId;

        // G-04: Notify customer that NRD payment is recorded
        db.Notifications.Add(new Notification
        {
            RecipientId = payment.Project.CustomerId,
            Type = "Payment",
            Message = $"Biên nhận đặt cọc NRD cho dự án {projectCode} đã được ghi nhận thành công.",
            LinkUrl = "/customer/payments",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
