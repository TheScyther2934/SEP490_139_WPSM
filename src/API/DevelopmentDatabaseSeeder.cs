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
            if (project is null && await db.WeddingProjects.AnyAsync(x => x.HallId == hall.HallId && x.SlotId == slot.SlotId && x.EventDate == p.EventDate, cancellationToken))
            {
                logger.LogInformation("Bỏ qua seed dự án mẫu {Code} vì sảnh {Hall} ca {Slot} ngày {Date} đã được đặt.", p.Code, p.Hall, p.Slot, p.EventDate);
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
                    Status = p.Status,
                    CreatedAt = p.EventDate.ToDateTime(new TimeOnly(9, 0)).AddDays(-100)
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

            if (!await db.DayOfTasks.AnyAsync(x => x.ProjectId == project.ProjectId, cancellationToken))
            {
                var eventDate = p.EventDate;
                var baseUtc = Domain.Services.RoadmapRules.ConvertVietnamToUtc(eventDate, new TimeOnly(11, 0));
                var isCompleted = p.Status is "Event Completed" or "Settled" or "Closed";

                db.DayOfTasks.AddRange(
                    new DayOfTask
                    {
                        ProjectId = project.ProjectId,
                        TaskName = "Kiểm tra set-up bàn tiệc & âm thanh ánh sáng",
                        DeadlineTime = baseUtc.AddHours(-1),
                        Status = isCompleted ? "Done" : "Pending",
                        CompletedAt = isCompleted ? baseUtc.AddHours(-1).AddMinutes(15) : null,
                        CompletedBy = isCompleted ? project.ManagerId : null,
                        PhotoRequired = true
                    },
                    new DayOfTask
                    {
                        ProjectId = project.ProjectId,
                        TaskName = "Đón tiếp khách mời & điều phối khai tiệc",
                        DeadlineTime = baseUtc,
                        Status = isCompleted ? "Done" : "Pending",
                        CompletedAt = isCompleted ? baseUtc : null,
                        CompletedBy = isCompleted ? project.ManagerId : null,
                        PhotoRequired = false
                    },
                    new DayOfTask
                    {
                        ProjectId = project.ProjectId,
                        TaskName = "Giám sát quy trình phục vụ tiệc theo BEO",
                        DeadlineTime = baseUtc.AddHours(1),
                        Status = isCompleted ? "Done" : "Pending",
                        CompletedAt = isCompleted ? baseUtc.AddHours(1) : null,
                        CompletedBy = isCompleted ? project.ManagerId : null,
                        PhotoRequired = true
                    },
                    new DayOfTask
                    {
                        ProjectId = project.ProjectId,
                        TaskName = "Đối soát phát sinh và nghiệm thu tiệc",
                        DeadlineTime = baseUtc.AddHours(2),
                        Status = isCompleted ? "Done" : "Pending",
                        CompletedAt = isCompleted ? baseUtc.AddHours(2) : null,
                        CompletedBy = isCompleted ? project.ManagerId : null,
                        PhotoRequired = false
                    }
                );
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await SeedNotificationsAsync(manager, cancellationToken);
    }

    private async Task SeedNotificationsAsync(User manager, CancellationToken cancellationToken)
    {
        if (await db.Notifications.AnyAsync(cancellationToken)) return;

        var customer = await db.Users.FirstOrDefaultAsync(u => u.Role == "Customer", cancellationToken);
        var now = DateTime.UtcNow;

        // Sample notifications for Manager
        db.Notifications.AddRange(
            new Notification
            {
                RecipientId = manager.UserId,
                Type = "Roadmap reminder",
                Message = "Dự án WPSM-2026-014: Mốc T-30 sắp đến hạn, cần kiểm tra và chuẩn bị đóng băng baseline.",
                LinkUrl = "/manager/projects/WPSM-2026-014/roadmap",
                IsRead = false,
                CreatedAt = now.AddMinutes(-35)
            },
            new Notification
            {
                RecipientId = manager.UserId,
                Type = "Payment",
                Message = "Dự án WPSM-2025-008: Khách hàng đã gửi yêu cầu xác nhận thanh toán đợt NRD.",
                LinkUrl = "/manager/projects/WPSM-2025-008/payments",
                IsRead = false,
                CreatedAt = now.AddHours(-2)
            },
            new Notification
            {
                RecipientId = manager.UserId,
                Type = "CR update",
                Message = "Dự án WPSM-2026-014: Yêu cầu thay đổi CR-001 cần quản lý thẩm định chi phí và phát sinh.",
                LinkUrl = "/manager/projects/WPSM-2026-014/change-requests",
                IsRead = false,
                CreatedAt = now.AddHours(-4)
            },
            new Notification
            {
                RecipientId = manager.UserId,
                Type = "Overdue alert",
                Message = "Dự án WPSM-2025-002: Nhiệm vụ ngày tiệc đang quá hạn, vui lòng cập nhật ảnh minh chứng.",
                LinkUrl = "/manager/overdue-alerts",
                IsRead = false,
                CreatedAt = now.AddHours(-6)
            },
            new Notification
            {
                RecipientId = manager.UserId,
                Type = "BEO",
                Message = "Dự án WPSM-2025-019: Bản BEO phiên bản 1 đã được cập nhật, cần xác nhận các bộ phận.",
                LinkUrl = "/manager/projects/WPSM-2025-019/beo",
                IsRead = true,
                CreatedAt = now.AddDays(-1)
            },
            new Notification
            {
                RecipientId = manager.UserId,
                Type = "Complaint",
                Message = "Dự án WPSM-2025-001: Khiếu nại COM-001 đã được Admin giải quyết.",
                LinkUrl = "/manager/projects/WPSM-2025-001/complaints",
                IsRead = true,
                CreatedAt = now.AddDays(-2)
            }
        );

        if (customer is not null)
        {
            db.Notifications.AddRange(
                new Notification
                {
                    RecipientId = customer.UserId,
                    Type = "Roadmap reminder",
                    Message = "Chào bạn! Hợp đồng gốc đã được chốt, vui lòng xem đặc tả và danh sách nhiệm vụ cần làm.",
                    LinkUrl = "/customer/roadmap",
                    IsRead = false,
                    CreatedAt = now.AddHours(-1)
                },
                new Notification
                {
                    RecipientId = customer.UserId,
                    Type = "Payment",
                    Message = "Biên nhận thanh toán đặt cọc NRD đã được xác nhận thành công.",
                    LinkUrl = "/customer/payments",
                    IsRead = false,
                    CreatedAt = now.AddHours(-3)
                },
                new Notification
                {
                    RecipientId = customer.UserId,
                    Type = "CR update",
                    Message = "Yêu cầu thay đổi của bạn đã được tiếp nhận và đang trong quá trình thương thảo.",
                    LinkUrl = "/customer/change-requests",
                    IsRead = false,
                    CreatedAt = now.AddHours(-5)
                },
                new Notification
                {
                    RecipientId = customer.UserId,
                    Type = "BEO",
                    Message = "Bảng tóm tắt thông số tổ chức cưới (BEO) đã được cập nhật.",
                    LinkUrl = "/customer/contract",
                    IsRead = true,
                    CreatedAt = now.AddDays(-1)
                },
                new Notification
                {
                    RecipientId = customer.UserId,
                    Type = "Roadmap reminder",
                    Message = "Nhắc nhở: Nhiệm vụ 'Xác nhận số lượng khách cuối cùng' thuộc mốc T-30 cần sớm hoàn tất.",
                    LinkUrl = "/customer/roadmap",
                    IsRead = true,
                    CreatedAt = now.AddDays(-2)
                }
            );
        }

        await db.SaveChangesAsync(cancellationToken);
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
}
