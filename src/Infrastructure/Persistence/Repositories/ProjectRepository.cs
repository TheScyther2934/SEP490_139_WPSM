using Microsoft.EntityFrameworkCore;
using Domain.Services;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository(WpsmDbContext db, ProjectReadModel readModel, IEmailOutbox emailOutbox, EmailOptions emailOptions) : IProjectRepository
{
    public Task<bool> CanAccessAsync(string projectCode, int userId, string role, CancellationToken cancellationToken = default) =>
        db.WeddingProjects.AnyAsync(x => x.ProjectCode == projectCode &&
            (role == "Admin" || role == "Manager" && (x.ManagerId == userId || x.ProjectManagers.Any(pm => pm.ManagerId == userId && pm.RemovedAt == null)) || role == "Customer" && x.CustomerId == userId), cancellationToken);

    public async Task<IReadOnlyList<WeddingProjectView>> GetProjectsAsync(int? userId = null, string? role = null, CancellationToken cancellationToken = default)
    {
        var query = readModel.ProjectViewsQuery();
        if (role == "Customer") query = query.Where(x => x.CustomerId == userId);
        else if (role == "Manager") query = query.Where(x => (x.ManagerId == userId || x.ProjectManagers.Any(pm => pm.ManagerId == userId && pm.RemovedAt == null)));
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
        if (await db.WeddingProjects.AnyAsync(x => x.SlotId == slot.SlotId && x.EventDate == command.EventDate && x.Status != "Cancelled", cancellationToken))
            throw new InvalidOperationException("Ca tiệc đã được đặt vào ngày này. Vui lòng chọn ca khác.");

        // Stage 1: the event date must be 15..90 days away (policy), the value is the sum of the services chosen at signing.
        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        var todayVn = VietnamTime.GetVietnamToday();
        if (EventStageRules.ValidateBookingDate(command.EventDate, todayVn, policy) is { } dateError)
            throw new InvalidOperationException(dateError);
        if (command.Items is null || command.Items.Count == 0)
            throw new InvalidOperationException("Hãy chọn các dịch vụ khách đặt.");
        if (command.Items.GroupBy(i => i.CatalogItemId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Mỗi dịch vụ chỉ chọn một lần.");
        if (command.Items.Any(i => i.Quantity <= 0 || i.UnitPrice < 0))
            throw new InvalidOperationException("Số lượng phải lớn hơn 0 và đơn giá không âm.");
        var itemIds = command.Items.Select(i => i.CatalogItemId).ToArray();
        var chosen = await db.ServiceCatalogItems.Where(x => itemIds.Contains(x.ItemId) && x.Status == "Active").ToDictionaryAsync(x => x.ItemId, cancellationToken);
        if (chosen.Count != itemIds.Length) throw new InvalidOperationException("Có dịch vụ không còn hoạt động trong bảng dịch vụ.");
        var frames = await db.PriceFrames.AsNoTracking().Where(x => itemIds.Contains(x.ItemId) && x.EffectiveFrom <= todayVn)
            .GroupBy(x => x.ItemId).Select(g => g.OrderByDescending(x => x.EffectiveFrom).First()).ToDictionaryAsync(x => x.ItemId, cancellationToken);
        foreach (var item in command.Items)
            if (frames.TryGetValue(item.CatalogItemId, out var frame) && (item.UnitPrice < frame.MinPrice || item.UnitPrice > frame.MaxPrice))
                throw new InvalidOperationException($"Đơn giá của '{chosen[item.CatalogItemId].Name}' phải trong khoảng {frame.MinPrice:N0} - {EventStageRules.Vnd(frame.MaxPrice)}.");
        var contractValue = command.Items.Sum(i => i.Quantity * i.UnitPrice);
        if (contractValue <= 0) throw new InvalidOperationException("Giá trị hợp đồng phải lớn hơn 0.");
        var requiredDeposit = EventStageRules.DepositFor(contractValue, policy);
        var depositAmount = command.DepositAmount <= 0 ? requiredDeposit : command.DepositAmount;
        if (depositAmount < requiredDeposit || depositAmount > contractValue)
            throw new InvalidOperationException($"Tiền đặt cọc phải từ {EventStageRules.Vnd(requiredDeposit)} ({policy.DepositRatio:G29}% giá trị hợp đồng) đến {EventStageRules.Vnd(contractValue)}.");

        var customer = await db.Users.SingleOrDefaultAsync(x => x.Email == command.CustomerEmail.Trim(), cancellationToken);
        // An existing customer may book a new event (one event = one contract); a deactivated account is reactivated.
        if (customer is not null && customer.Role != "Customer")
            throw new InvalidOperationException("Email này thuộc tài khoản nhân viên, không dùng làm khách hàng.");
        if (customer is { Status: "Inactive" }) customer.Status = "Active";
        var newCustomer = customer is null;
        if (newCustomer)
        {
            customer = new User
            {
                Email = command.CustomerEmail.Trim(),
                FullName = command.CoupleName.Trim(),
                Phone = WpsmValueNormalizer.NormalizePhone(command.CustomerPhone), // CK_Users_Phone: digits only
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
            CreatedBy = manager.UserId,
            HallId = hall.HallId,
            SlotId = slot.SlotId,
            EventDate = command.EventDate,
            BaseContractValue = contractValue,
            DepositAmount = depositAmount,
            // Signed in stage 1, adjustable in stage 2 until the lock date; CurrentTotal follows the service lines.
            CurrentTotal = contractValue,
            Status = EventStatus.Adjusting,
            CreatedAt = today
        };
        db.WeddingProjects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        // The creator is the Primary operator; co-managers are added later from M-04 or by an Admin.
        db.ProjectManagers.Add(new ProjectManager { ProjectId = project.ProjectId, ManagerId = manager.UserId, Role = "Primary", AssignedBy = manager.UserId, AssignedAt = DateTime.UtcNow, Reason = "Người tạo sự kiện" });

        foreach (var item in command.Items)
            db.SpecItems.Add(new SpecItem
            {
                ProjectId = project.ProjectId, CatalogItemId = item.CatalogItemId,
                Description = string.IsNullOrWhiteSpace(item.Description) ? chosen[item.CatalogItemId].Name : item.Description.Trim(),
                Quantity = item.Quantity, UnitPrice = item.UnitPrice
            });
        var payByCash = string.Equals(command.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase);
        db.Payments.Add(new Payment
        {
            ProjectId = project.ProjectId,
            PaymentType = "Deposit",
            Status = payByCash ? "Paid" : "Due",
            DueDate = DateOnly.FromDateTime(DateTime.Today),
            Amount = depositAmount,
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
                ? $"Sự kiện {project.ProjectCode} đã được tạo. Tiền đặt cọc đã được xác nhận."
                : $"Sự kiện {project.ProjectCode} đã được tạo. Vui lòng thanh toán tiền đặt cọc.",
            LinkUrl = "/customer/payments",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        if (newCustomer)
            emailOutbox.Enqueue(customer.Email, $"Tài khoản WPSM cho sự kiện {project.ProjectCode}",
                EmailTemplates.Credentials(customer.FullName, customer.Email, temporaryPassword, project.ProjectCode, emailOptions.PublicBaseUrl),
                "Credentials", project.ProjectId);
        // One delivery path only: the outbox row above commits with the project and JOB-07 sends it (SDS outbox pattern).
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var loaded = await readModel.ProjectViewsQuery().SingleAsync(x => x.ProjectId == project.ProjectId, cancellationToken);
        return new CreatedProjectView(await readModel.MapProjectAsync(loaded, cancellationToken), newCustomer ? temporaryPassword : null);
    }

    public async Task<bool> ResendCredentialsAsync(string projectCode, int managerId, string customerPasswordHash, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        var project = await db.WeddingProjects.Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.ProjectCode == projectCode && (x.ManagerId == managerId || x.ProjectManagers.Any(pm => pm.ManagerId == managerId && pm.RemovedAt == null)), cancellationToken);
        if (project is null) return false;
        if (!project.Customer.MustChangePassword) throw new InvalidOperationException("Khách hàng đã kích hoạt tài khoản nên không cần gửi lại thông tin đăng nhập.");
        project.Customer.PasswordHash = customerPasswordHash;
        emailOutbox.Enqueue(project.Customer.Email, $"Tài khoản WPSM cho sự kiện {project.ProjectCode}",
            EmailTemplates.Credentials(project.Customer.FullName, project.Customer.Email, temporaryPassword, project.ProjectCode, emailOptions.PublicBaseUrl),
            "Credentials", project.ProjectId);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordDepositReceiptAsync(string projectCode, int managerId, StoredFileView receipt, CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments.Include(x => x.Project)
            .SingleOrDefaultAsync(x => x.Project.ProjectCode == projectCode && x.PaymentType == "Deposit", cancellationToken);
        if (payment is null || payment.Project.ManagerId != managerId) return false;
        if (payment.Status == "Paid") throw new InvalidOperationException("Tiền đặt cọc đã được ghi nhận.");
        payment.Status = "Paid";
        payment.PaymentMethod = "Bank";
        payment.ReceiptFilePath = receipt.StorageKey;
        payment.ReceiptFileName = receipt.OriginalName;
        payment.ReceiptContentType = receipt.ContentType;
        payment.PaidDate = DateOnly.FromDateTime(DateTime.Today);
        payment.RecordedBy = managerId;

        // G-04: Notify customer that NRD payment is recorded
        db.Notifications.Add(new Notification
        {
            RecipientId = payment.Project.CustomerId,
            Type = "Payment",
            Message = $"Biên nhận tiền đặt cọc của sự kiện {projectCode} đã được ghi nhận.",
            LinkUrl = "/customer/payments",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
