using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace API;

public sealed class DevelopmentDatabaseSeeder(
    WpsmDbContext db,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration,
    ILogger<DevelopmentDatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var admin = await GetOrCreateAccountAsync("Admin", "Admin", cancellationToken);
        _ = await GetOrCreateAccountAsync("Manager", "Manager", cancellationToken);

        var halls = new[]
        {
            new HallSeed("Sảnh Kim Cương", 450, 650, 60_000_000m),
            new HallSeed("Sảnh Pha Lê", 300, 450, 45_000_000m),
            new HallSeed("Sảnh Hoàng Gia", 600, 900, 80_000_000m),
            new HallSeed("Sảnh Vườn Ngọc", 200, 350, 35_000_000m)
        };
        foreach (var hall in halls)
        {
            var entity = await db.Halls.FirstOrDefaultAsync(x => x.Name == hall.Name, cancellationToken);
            if (entity is null)
            {
                entity = new Hall { Name = hall.Name, MinCapacity = hall.Minimum, MaxCapacity = hall.Maximum, BasePrice = hall.BasePrice, Status = "Active" };
                db.Halls.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
            }
            if (!await db.Slots.AnyAsync(x => x.HallId == entity.HallId && x.SlotName == "Lunch", cancellationToken))
                db.Slots.Add(new Slot { HallId = entity.HallId, SlotName = "Lunch", StartTime = new TimeOnly(11, 0), EndTime = new TimeOnly(14, 0) });
            if (!await db.Slots.AnyAsync(x => x.HallId == entity.HallId && x.SlotName == "Evening", cancellationToken))
                db.Slots.Add(new Slot { HallId = entity.HallId, SlotName = "Evening", StartTime = new TimeOnly(17, 30), EndTime = new TimeOnly(21, 30) });
        }
        await db.SaveChangesAsync(cancellationToken);

        var catalog = new[]
        {
            new CatalogSeed("Thuê sảnh tiệc", "Hall", "Gói", 25_000_000m),
            new CatalogSeed("Thực đơn tiệc cưới", "Catering", "Bàn", 3_500_000m),
            new CatalogSeed("Trang trí sân khấu", "Decor", "Gói", 12_000_000m),
            new CatalogSeed("MC chương trình", "MC", "Người", 4_000_000m),
            new CatalogSeed("Âm thanh và ánh sáng", "AV", "Gói", 8_000_000m)
        };
        foreach (var item in catalog)
        {
            var entity = await db.ServiceCatalogItems.FirstOrDefaultAsync(x => x.Category == item.Category && x.Name == item.Name, cancellationToken);
            if (entity is null)
            {
                entity = new ServiceCatalogItem { Name = item.Name, Category = item.Category, Unit = item.Unit, DefaultPrice = item.Price, Status = "Active" };
                db.ServiceCatalogItems.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
            }
            if (!await db.PriceFrames.AnyAsync(x => x.ItemId == entity.ItemId, cancellationToken))
                db.PriceFrames.Add(new PriceFrame
                {
                    ItemId = entity.ItemId, MinPrice = decimal.Round(entity.DefaultPrice * 0.8m, 2),
                    MaxPrice = decimal.Round(entity.DefaultPrice * 1.2m, 2), EffectiveFrom = DateOnly.FromDateTime(DateTime.Today),
                    UpdatedBy = admin.UserId, UpdatedAt = DateTime.UtcNow
                });
        }
        await db.SaveChangesAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Today);
        await EnsurePolicyAsync("NRD_RATIO", null, null, 30m, today, admin.UserId, cancellationToken);
        await EnsurePolicyAsync("T30_RATIO", null, null, 70m, today, admin.UserId, cancellationToken);
        await EnsurePolicyAsync("CR_PENALTY", 0, 14, 5m, today, admin.UserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var manager = await db.Users.FirstAsync(x => x.Email == configuration["Wpsm:Seed:Manager:Email"], cancellationToken);
        var catalogEntities = await db.ServiceCatalogItems.Where(x => x.Status == "Active").ToListAsync(cancellationToken);

        var sampleProjects = new[]
        {
            new ProjectSeed("WPSM-2026-014", "Nguyễn Minh Anh & Trần Thu Hà", "minhanh.wedding@gmail.com", "0912345678", "Sảnh Hoàng Gia", "Evening", new DateOnly(2026, 12, 20), 185_000_000m, 55_500_000m, "Baseline Frozen"),
            new ProjectSeed("WPSM-2025-008", "Nguyễn Bảo Long & Trần Phương Linh", "long.nguyenbaolong@gmail.com", "0908123456", "Sảnh Kim Cương", "Evening", new DateOnly(2025, 12, 28), 210_000_000m, 63_000_000m, "Active Baseline"),
            new ProjectSeed("WPSM-2025-019", "Lê Hoàng Nam & Vũ Hoài Phương", "nam.lehoang@corp.vn", "0934567890", "Sảnh Pha Lê", "Lunch", new DateOnly(2025, 12, 15), 160_000_000m, 48_000_000m, "Active Baseline"),
            new ProjectSeed("WPSM-2025-002", "Phạm Tuấn & Đỗ Lan Hương", "tuanlan.wedding25@yahoo.com", "0987654321", "Sảnh Vườn Ngọc", "Evening", new DateOnly(2025, 11, 10), 140_000_000m, 42_000_000m, "Active Baseline"),
            new ProjectSeed("WPSM-2025-001", "Trần Văn An & Lê Mai Chi", "anchi.tran@gmail.com", "0971234567", "Sảnh Kim Cương", "Evening", new DateOnly(2025, 10, 25), 195_000_000m, 58_500_000m, "Event Completed"),
            new ProjectSeed("WPSM-2025-011", "Hoàng Quốc Bảo & Phạm Thảo Linh", "baolinh2025@outlook.com", "0962345678", "Sảnh Pha Lê", "Evening", new DateOnly(2025, 10, 5), 175_000_000m, 52_500_000m, "Settled"),
            new ProjectSeed("WPSM-2025-004", "Đặng Tuấn Kiệt & Bùi Thúy Hạnh", "kiethanh.wedding@gmail.com", "0943210987", "Sảnh Hoàng Gia", "Lunch", new DateOnly(2025, 9, 18), 220_000_000m, 66_000_000m, "Closed")
        };

        foreach (var p in sampleProjects)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var customer = await db.Users.FirstOrDefaultAsync(x => x.Email == p.Email, cancellationToken);
            if (customer is null)
            {
                customer = new User
                {
                    Email = p.Email,
                    FullName = p.Couple,
                    Phone = p.Phone,
                    Role = "Customer",
                    Status = "Active",
                    MustChangePassword = false,
                    CreatedAt = DateTime.UtcNow
                };
                customer.PasswordHash = passwordHasher.HashPassword(customer, "Wedding2025!");
                db.Users.Add(customer);
                await db.SaveChangesAsync(cancellationToken);
            }

            var hall = await db.Halls.FirstAsync(x => x.Name == p.Hall, cancellationToken);
            var slot = await db.Slots.FirstAsync(x => x.HallId == hall.HallId && x.SlotName == p.Slot, cancellationToken);

            var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == p.Code, cancellationToken);
            // A real project may already hold this hall slot (UQ_Projects_Hall_Slot_Date); skip the sample instead of
            // failing start-up. The open transaction rolls back the sample customer on dispose.
            if (project is null && await db.WeddingProjects.AnyAsync(x => x.HallId == hall.HallId && x.SlotId == slot.SlotId && x.EventDate == p.EventDate, cancellationToken))
            {
                logger.LogWarning("Seed: bỏ qua dự án mẫu {Code} vì {Hall} ca {Slot} ngày {Date} đã có dự án khác.", p.Code, p.Hall, p.Slot, p.EventDate);
                continue;
            }
            if (project is null)
            {
            project = new WeddingProject
            {
                ProjectCode = p.Code,
                CustomerId = customer.UserId,
                ManagerId = manager.UserId,
                HallId = hall.HallId,
                SlotId = slot.SlotId,
                EventDate = p.EventDate,
                BaseContractValue = p.BaseValue,
                Nrdamount = p.Nrd,
                CurrentTotal = p.BaseValue,
                Status = p.Status,
                BaselineFrozenAt = p.Status == "Active Baseline" ? null : DateTime.UtcNow.AddDays(-30),
                CreatedAt = DateTime.UtcNow.AddDays(-40)
            };
            db.WeddingProjects.Add(project);
            await db.SaveChangesAsync(cancellationToken);
            }

            foreach (var item in catalogEntities)
            {
                if (await db.SpecItems.AnyAsync(x => x.ProjectId == project.ProjectId && x.CatalogItemId == item.ItemId && x.Version == 1, cancellationToken)) continue;
                db.SpecItems.Add(new SpecItem
                {
                    ProjectId = project.ProjectId,
                    CatalogItemId = item.ItemId,
                    Description = item.Name,
                    Quantity = item.Category == "Catering" ? 30 : 1,
                    UnitPrice = item.DefaultPrice,
                    IsBaseline = true,
                    Version = 1
                });
            }

            if (!await db.Payments.AnyAsync(x => x.ProjectId == project.ProjectId && x.PaymentType == "NRD", cancellationToken))
            db.Payments.Add(new Payment
            {
                ProjectId = project.ProjectId,
                PaymentType = "NRD",
                Status = "Paid",
                DueDate = p.EventDate.AddDays(-90),
                Amount = p.Nrd,
                PaymentMethod = "Cash",
                PaidDate = p.EventDate.AddDays(-90),
                RecordedBy = manager.UserId
            });

            var milestones = new[]
            {
                new { M = "T-90", T = "Khởi tạo hợp đồng gốc & thu NRD", R = "Manager", D = p.EventDate.AddDays(-90), S = "Done" },
                new { M = "T-30", T = "Đóng băng baseline (Frozen) & BEO chuẩn", R = "Manager", D = p.EventDate.AddDays(-30), S = p.Status == "Active Baseline" ? "Pending" : "Done" },
                new { M = "T-7", T = "Chốt số bàn chính thức & nhân sự điều phối", R = "Manager", D = p.EventDate.AddDays(-7), S = p.Status is "Event Completed" or "Settled" or "Closed" ? "Done" : "Pending" },
                new { M = "Event Day", T = "Vận hành ngày cưới & giám sát tiệc", R = "Manager", D = p.EventDate, S = p.Status is "Event Completed" or "Settled" or "Closed" ? "Done" : "Pending" }
            };
            foreach (var m in milestones)
            {
                if (await db.RoadmapTasks.AnyAsync(x => x.ProjectId == project.ProjectId && x.Milestone == m.M, cancellationToken)) continue;
                db.RoadmapTasks.Add(new RoadmapTask
                {
                    ProjectId = project.ProjectId,
                    Milestone = m.M,
                    Title = m.T,
                    AssigneeRole = m.R,
                    DueDate = m.D,
                    Status = m.S,
                    CompletedAt = m.S == "Done" ? m.D.ToDateTime(new TimeOnly(12, 0)) : null
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await SeedResourcesAsync(cancellationToken);
    }

    // FE-07: shared resources for the Resource Calendar and one allocated demo event.
    private async Task SeedResourcesAsync(CancellationToken cancellationToken)
    {
        var resources = new[]
        {
            new ResourceSeed("Dàn âm thanh sảnh chính", "Audio", 4),
            new ResourceSeed("Dàn âm thanh sân vườn", "Audio", 2),
            new ResourceSeed("Đèn Moving Head & Follow Spot", "Follow Light", 4),
            new ResourceSeed("Đèn rọi sân khấu", "Follow Light", 6),
            new ResourceSeed("Khung trống vòm hoa đôi VIP", "Decor Frame", 3),
            new ResourceSeed("Cổng hoa chính", "Decor Frame", 2),
            new ResourceSeed("Kỹ thuật viên AV & Stage Manager", "Technician", 6),
            new ResourceSeed("Nhân sự ánh sáng", "Technician", 4),
            new ResourceSeed("Đội bếp trưởng & bếp nòng VIP", "Kitchen", 3),
            new ResourceSeed("Nhân sự sơ chế", "Kitchen", 5)
        };
        foreach (var resource in resources)
        {
            if (await db.Resources.AnyAsync(x => x.Type == resource.Type && x.Name == resource.Name, cancellationToken)) continue;
            db.Resources.Add(new Resource { Name = resource.Name, Type = resource.Type, TotalQuantity = resource.Quantity });
        }
        await db.SaveChangesAsync(cancellationToken);

        var project = await db.WeddingProjects.Include(x => x.Slot)
            .FirstOrDefaultAsync(x => x.ProjectCode == "WPSM-2026-014", cancellationToken);
        if (project is null) return;
        if (await db.ResourceAllocations.AnyAsync(x => x.ProjectId == project.ProjectId, cancellationToken)) return;

        var vnTz = FindVietnamTimeZone();
        var start = TimeZoneInfo.ConvertTimeToUtc(project.EventDate.ToDateTime(project.Slot.StartTime), vnTz);
        var end = TimeZoneInfo.ConvertTimeToUtc(project.EventDate.ToDateTime(project.Slot.EndTime), vnTz);
        var picks = new[]
        {
            new ResourcePick("Audio", "Dàn âm thanh sảnh chính", 2),
            new ResourcePick("Follow Light", "Đèn Moving Head & Follow Spot", 2),
            new ResourcePick("Technician", "Kỹ thuật viên AV & Stage Manager", 3)
        };
        foreach (var pick in picks)
        {
            var resource = await db.Resources.FirstOrDefaultAsync(x => x.Type == pick.Type && x.Name == pick.Name, cancellationToken);
            if (resource is null) continue;
            db.ResourceAllocations.Add(new ResourceAllocation
            {
                ResourceId = resource.ResourceId,
                HallId = project.HallId,
                ProjectId = project.ProjectId,
                StartTime = start,
                EndTime = end,
                AllocatedQuantity = pick.Quantity,
                Status = "Reserved"
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static TimeZoneInfo FindVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }

    private async Task<User> GetOrCreateAccountAsync(string role, string key, CancellationToken cancellationToken)
    {
        var email = configuration[$"Wpsm:Seed:{key}:Email"]?.Trim();
        var password = configuration[$"Wpsm:Seed:{key}:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException($"Bật seed development yêu cầu đặt Wpsm:Seed:{key}:Email và Password (tối thiểu 12 ký tự) bằng User Secrets.");

        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is not null)
        {
            if (user.Role != role || user.Status != "Active") throw new InvalidOperationException($"Tài khoản seed {email} cần có Role={role} và Status=Active.");
            return user;
        }

        user = new User
        {
            Email = email, FullName = key == "Admin" ? "WPSM Development Admin" : "WPSM Development Manager",
            Role = role, Status = "Active", MustChangePassword = false, CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task EnsurePolicyAsync(string type, int? min, int? max, decimal rate, DateOnly today, int updatedBy, CancellationToken cancellationToken)
    {
        if (await db.PolicyConfigs.AnyAsync(x => x.PolicyType == type, cancellationToken)) return;
        db.PolicyConfigs.Add(new PolicyConfig
        {
            PolicyType = type, TdayMin = min, TdayMax = max, RatePercent = rate,
            EffectiveFrom = today, UpdatedBy = updatedBy, UpdatedAt = DateTime.UtcNow
        });
    }

    private sealed record HallSeed(string Name, int Minimum, int Maximum, decimal BasePrice);
    private sealed record CatalogSeed(string Name, string Category, string Unit, decimal Price);
    private sealed record ProjectSeed(string Code, string Couple, string Email, string Phone, string Hall, string Slot, DateOnly EventDate, decimal BaseValue, decimal Nrd, string Status);
    private sealed record ResourceSeed(string Name, string Type, int Quantity);
    private sealed record ResourcePick(string Type, string Name, int Quantity);
}
