using Microsoft.EntityFrameworkCore;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Persistence;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository(WpsmDbContext db, ProjectReadModel readModel) : IProjectRepository
{
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
            string.IsNullOrWhiteSpace(command.CustomerEmail) || command.CustomerEmail.Length > 100 || !command.CustomerEmail.Contains('@'))
            throw new InvalidOperationException("Tên khách hàng hoặc email không hợp lệ.");
        var phoneDigits = new string(command.CustomerPhone.Where(char.IsDigit).ToArray());
        if (phoneDigits.Length is < 9 or > 11) throw new InvalidOperationException("Số điện thoại cần có từ 9 đến 11 chữ số.");
        var hall = await db.Halls.SingleOrDefaultAsync(x => x.Status == "Active" && x.Name == hallName, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy sảnh đang hoạt động. Hãy kiểm tra danh mục sảnh trong database.");
        var slotName = WpsmValueNormalizer.NormalizeSlot(command.Slot);
        var slot = await db.Slots.SingleOrDefaultAsync(x => x.HallId == hall.HallId && x.SlotName == slotName, cancellationToken)
            ?? throw new InvalidOperationException("Ca tiệc không tồn tại cho sảnh đã chọn.");

        if (command.BaseValue <= 0 || command.NrdAmount < 0 || command.NrdAmount > command.BaseValue)
            throw new InvalidOperationException("Giá trị hợp đồng hoặc tiền NRD không hợp lệ.");
        if (command.EventDate < DateOnly.FromDateTime(DateTime.Today))
            throw new InvalidOperationException("Ngày tổ chức phải từ hôm nay trở đi.");

        var depositRatio = await db.PolicyConfigs.AsNoTracking()
            .Where(x => x.PolicyType == "NRD_RATIO" && x.EffectiveFrom <= DateOnly.FromDateTime(DateTime.Today))
            .OrderByDescending(x => x.EffectiveFrom).Select(x => (decimal?)x.RatePercent).FirstOrDefaultAsync(cancellationToken) ?? 30m;
        if (command.NrdAmount < decimal.Round(command.BaseValue * depositRatio / 100m, 2))
            throw new InvalidOperationException($"Tiền NRD thấp hơn mức tối thiểu {depositRatio:0.##}% theo chính sách hiện hành.");

        var email = command.CustomerEmail.Trim();
        var customer = await db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        var newCustomer = customer is null;
        if (customer is null)
        {
            customer = new User
            {
                Email = email,
                FullName = command.CoupleName.Trim(),
                Phone = WpsmValueNormalizer.NormalizePhone(command.CustomerPhone),
                Role = "Customer",
                Status = "Active",
                PasswordHash = customerPasswordHash,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(customer);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (customer.Role != "Customer" || customer.Status != "Active")
        {
            throw new InvalidOperationException("Email đã tồn tại và không thuộc tài khoản khách hàng đang hoạt động.");
        }

        var project = new WeddingProject
        {
            ProjectCode = $"WPSM-{command.EventDate.Year}-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            CustomerId = customer.UserId,
            ManagerId = manager.UserId,
            HallId = hall.HallId,
            SlotId = slot.SlotId,
            EventDate = command.EventDate,
            BaseContractValue = command.BaseValue,
            Nrdamount = command.NrdAmount,
            CurrentTotal = command.BaseValue,
            Status = "Active Baseline",
            CreatedAt = DateTime.UtcNow
        };
        db.WeddingProjects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        var baselineCatalog = await db.ServiceCatalogItems.Where(x => x.Status == "Active" &&
            new[] { "Hall", "Catering", "Decor", "MC", "AV" }.Contains(x.Category))
            .GroupBy(x => x.Category).Select(g => g.OrderBy(x => x.Name).First()).ToListAsync(cancellationToken);
        foreach (var item in baselineCatalog)
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
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var loaded = await readModel.ProjectViewsQuery().SingleAsync(x => x.ProjectId == project.ProjectId, cancellationToken);
        return new CreatedProjectView(await readModel.MapProjectAsync(loaded, cancellationToken), newCustomer ? temporaryPassword : null);
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
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
