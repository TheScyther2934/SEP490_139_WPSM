using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;

namespace API;

/// <summary>
/// Development data. Runs on every start and only adds what is missing. The event stage flow demo (stage notes, addon contracts,
/// supplier evidence, invoices, a cancelled event) is created once, guarded by a SystemSettings flag.
/// </summary>
public sealed class DevelopmentDatabaseSeeder(
    WpsmDbContext db,
    IPasswordHasher<User> passwordHasher,
    IFileStorage storage,
    IConfiguration configuration,
    ILogger<DevelopmentDatabaseSeeder> logger)
{
    private const string DemoFlag = "DemoEventFlowSeeded";

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
            new CatalogSeed("Thuê sảnh tiệc", "Hall", "Gói", 25_000_000m, false),
            new CatalogSeed("Thực đơn tiệc cưới", "Catering", "Bàn", 3_500_000m, true),
            new CatalogSeed("Trang trí sân khấu", "Decor", "Gói", 12_000_000m, true),
            new CatalogSeed("MC chương trình", "MC", "Người", 4_000_000m, false),
            new CatalogSeed("Âm thanh và ánh sáng", "AV", "Gói", 8_000_000m, false),
            new CatalogSeed("Mâm quả bê lễ 7 tráp", "Ceremony", "Bộ", 3_500_000m, true),
            new CatalogSeed("Tiết mục văn nghệ", "Performance", "Tiết mục", 1_500_000m, true),
            new CatalogSeed("Bàn ghế Chiavari", "Furniture", "Bộ", 150_000m, true)
        };
        foreach (var item in catalog)
        {
            var entity = await db.ServiceCatalogItems.FirstOrDefaultAsync(x => x.Category == item.Category && x.Name == item.Name, cancellationToken);
            if (entity is null)
            {
                entity = new ServiceCatalogItem { Name = item.Name, Category = item.Category, Unit = item.Unit, DefaultPrice = item.Price, Status = "Active", RequiresSupplier = item.RequiresSupplier };
                db.ServiceCatalogItems.Add(entity);
                await db.SaveChangesAsync(cancellationToken);
            }
            if (!await db.PriceFrames.AnyAsync(x => x.ItemId == entity.ItemId, cancellationToken))
                db.PriceFrames.Add(new PriceFrame
                {
                    ItemId = entity.ItemId, MinPrice = decimal.Round(entity.DefaultPrice * 0.8m, 2), MaxPrice = decimal.Round(entity.DefaultPrice * 1.2m, 2),
                    EffectiveFrom = new DateOnly(2026, 1, 1), UpdatedBy = admin.UserId, UpdatedAt = DateTime.UtcNow
                });
        }
        await db.SaveChangesAsync(cancellationToken);

        var defaults = EventPolicy.Default;
        foreach (var (key, value) in new (string, decimal)[]
                 {
                     (EventPolicy.Keys.DepositRatio, defaults.DepositRatio), (EventPolicy.Keys.MaxDecreaseRatio, defaults.MaxDecreaseRatio),
                     (EventPolicy.Keys.BookingMinDays, defaults.BookingMinDays), (EventPolicy.Keys.BookingMaxDays, defaults.BookingMaxDays),
                     (EventPolicy.Keys.LockDays, defaults.LockDays), (EventPolicy.Keys.SupplierDeadlineDays, defaults.SupplierDeadlineDays),
                     (EventPolicy.Keys.SettlementDueDays, defaults.SettlementDueDays)
                 })
            if (!await db.PolicyConfigs.AnyAsync(x => x.PolicyType == key, cancellationToken))
                db.PolicyConfigs.Add(new PolicyConfig { PolicyType = key, Value = value, EffectiveFrom = new DateOnly(2026, 1, 1), UpdatedBy = admin.UserId, UpdatedAt = DateTime.UtcNow });
        if (!await db.SystemSettings.AnyAsync(x => x.SettingKey == EventFlowSupport.HotlineKey, cancellationToken))
            db.SystemSettings.Add(new SystemSetting { SettingKey = EventFlowSupport.HotlineKey, SettingValue = EventFlowSupport.DefaultHotline, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);

        var manager = await db.Users.FirstAsync(x => x.Email == configuration["Wpsm:Seed:Manager:Email"], cancellationToken);
        var today = VietnamTime.GetVietnamToday();
        var catalogEntities = await db.ServiceCatalogItems.Where(x => x.Status == "Active").OrderBy(x => x.ItemId).ToListAsync(cancellationToken);
        var priceFrames = await db.PriceFrames.AsNoTracking().Where(x => x.EffectiveFrom <= today)
            .GroupBy(x => x.ItemId).Select(g => g.OrderByDescending(x => x.EffectiveFrom).First())
            .ToDictionaryAsync(x => x.ItemId, cancellationToken);

        // Fixed sample events plus demo events relative to today, so every stage has data whatever the date.
        // "anchi.tran" books a second event: an existing customer may book again (one event = one contract).
        var sampleProjects = new[]
        {
            new ProjectSeed("WPSM-2026-014", "Nguyễn Minh Anh & Trần Thu Hà", "minhanh.wedding@gmail.com", "0912345678", "Sảnh Hoàng Gia", "Evening", new DateOnly(2026, 12, 20), 185_000_000m),
            new ProjectSeed("WPSM-2025-008", "Nguyễn Bảo Long & Trần Phương Linh", "long.nguyenbaolong@gmail.com", "0908123456", "Sảnh Kim Cương", "Evening", new DateOnly(2027, 1, 16), 210_000_000m),
            new ProjectSeed("WPSM-2025-019", "Lê Hoàng Nam & Vũ Hoài Phương", "nam.lehoang@corp.vn", "0934567890", "Sảnh Pha Lê", "Lunch", new DateOnly(2027, 1, 30), 160_000_000m),
            new ProjectSeed("WPSM-2025-002", "Phạm Tuấn & Đỗ Lan Hương", "tuanlan.wedding25@yahoo.com", "0987654321", "Sảnh Vườn Ngọc", "Evening", new DateOnly(2027, 1, 23), 140_000_000m),
            new ProjectSeed("WPSM-2025-001", "Trần Văn An & Lê Mai Chi", "anchi.tran@gmail.com", "0971234567", "Sảnh Kim Cương", "Evening", new DateOnly(2025, 10, 25), 195_000_000m),
            new ProjectSeed("WPSM-2025-011", "Hoàng Quốc Bảo & Phạm Thảo Linh", "baolinh2025@outlook.com", "0962345678", "Sảnh Pha Lê", "Evening", new DateOnly(2025, 10, 5), 175_000_000m, EventStatus.Settled),
            new ProjectSeed("WPSM-2025-004", "Đặng Tuấn Kiệt & Bùi Thúy Hạnh", "kiethanh.wedding@gmail.com", "0943210987", "Sảnh Hoàng Gia", "Lunch", new DateOnly(2025, 9, 18), 220_000_000m, EventStatus.Closed),
            new ProjectSeed("WPSM-DEMO-01", "Trần Văn An & Lê Mai Chi", "anchi.tran@gmail.com", "0971234567", "Sảnh Vườn Ngọc", "Lunch", today.AddDays(45), 150_000_000m),
            new ProjectSeed("WPSM-DEMO-02", "Võ Đức Thịnh & Ngô Bảo Trân", "thinhtran.cuoi@gmail.com", "0915678234", "Sảnh Pha Lê", "Evening", today.AddDays(6), 170_000_000m),
            new ProjectSeed("WPSM-DEMO-03", "Lý Minh Khoa & Đinh Ngọc Ánh", "khoaanh.wedding@gmail.com", "0938112233", "Sảnh Kim Cương", "Lunch", today.AddDays(-3), 205_000_000m),
            new ProjectSeed("WPSM-DEMO-04", "Phan Gia Huy & Tạ Mỹ Duyên", "huyduyen.2027@gmail.com", "0977445566", "Sảnh Hoàng Gia", "Lunch", today.AddDays(30), 180_000_000m, EventStatus.Cancelled)
        };

        var policy = await EventFlowSupport.LoadPolicyAsync(db, cancellationToken);
        foreach (var p in sampleProjects)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var customer = await db.Users.FirstOrDefaultAsync(x => x.Email == p.Email, cancellationToken);
            if (customer is null)
            {
                customer = new User { Email = p.Email, FullName = p.Couple, Phone = p.Phone, Role = "Customer", Status = "Active", MustChangePassword = false, CreatedAt = DateTime.UtcNow };
                customer.PasswordHash = passwordHasher.HashPassword(customer, "Wedding2025!");
                db.Users.Add(customer);
                await db.SaveChangesAsync(cancellationToken);
            }

            // An existing customer booking a new event is reactivated (same rule as the create form).
            if (customer.Status == "Inactive" && !await db.WeddingProjects.AnyAsync(x => x.ProjectCode == p.Code, cancellationToken))
            {
                customer.Status = "Active";
                await db.SaveChangesAsync(cancellationToken);
            }
            var hall = await db.Halls.FirstAsync(x => x.Name == p.Hall, cancellationToken);
            var slot = await db.Slots.FirstAsync(x => x.HallId == hall.HallId && x.SlotName == p.Slot, cancellationToken);
            var project = await db.WeddingProjects.FirstOrDefaultAsync(x => x.ProjectCode == p.Code, cancellationToken);
            if (project is null && await db.WeddingProjects.AnyAsync(x => x.HallId == hall.HallId && x.SlotId == slot.SlotId && x.EventDate == p.EventDate && x.Status != EventStatus.Cancelled, cancellationToken))
            {
                logger.LogInformation("Bỏ qua sự kiện mẫu {Code}: sảnh {Hall} ca {Slot} ngày {Date} đã được đặt.", p.Code, p.Hall, p.Slot, p.EventDate);
                continue;
            }
            if (project is null)
            {
                // Signed between 15 and 90 days before the event (stage 1).
                var signedOn = p.EventDate.AddDays(-Math.Min(policy.BookingMaxDays, Math.Max(policy.BookingMinDays, 60)));
                project = new WeddingProject
                {
                    ProjectCode = p.Code, CustomerId = customer.UserId, ManagerId = manager.UserId, CreatedBy = manager.UserId,
                    HallId = hall.HallId, SlotId = slot.SlotId, EventDate = p.EventDate,
                    BaseContractValue = p.Value, DepositAmount = EventStageRules.DepositFor(p.Value, policy), CurrentTotal = p.Value,
                    Status = EventStatus.Adjusting, CreatedAt = signedOn.ToDateTime(new TimeOnly(2, 0))
                };
                db.WeddingProjects.Add(project);
                await db.SaveChangesAsync(cancellationToken);
                db.ProjectManagers.Add(new ProjectManager { ProjectId = project.ProjectId, ManagerId = manager.UserId, Role = "Primary", AssignedBy = manager.UserId, AssignedAt = project.CreatedAt, Reason = "Người tạo sự kiện" });
                db.SpecItems.AddRange(BuildSampleSpecification(project.ProjectId, p.Value, catalogEntities, priceFrames));
                db.Payments.Add(new Payment
                {
                    ProjectId = project.ProjectId, PaymentType = PaymentKinds.Deposit, Status = "Paid", DueDate = signedOn, Amount = project.DepositAmount,
                    PaymentMethod = "Cash", PaidDate = signedOn, RecordedBy = manager.UserId
                });
                if (p.Status is EventStatus.Settled or EventStatus.Closed)
                {
                    project.Status = p.Status;
                    project.LockedAt = EventStageRules.LockDate(p.EventDate, policy).ToDateTime(new TimeOnly(0, 5));
                    db.Payments.Add(new Payment
                    {
                        ProjectId = project.ProjectId, PaymentType = PaymentKinds.Settlement, Status = "Paid", DueDate = EventStageRules.SettlementDueDate(p.EventDate, policy),
                        Amount = p.Value - project.DepositAmount, PaymentMethod = "Bank", ReceiptFilePath = "seed", PaidDate = p.EventDate.AddDays(2), RecordedBy = manager.UserId
                    });
                }
                if (p.Status == EventStatus.Cancelled)
                {
                    project.Status = EventStatus.Cancelled;
                    project.CancelledAt = DateTime.UtcNow.AddDays(-2);
                    project.CancelledBy = manager.UserId;
                    project.CancelReason = "Gia đình đổi kế hoạch, dời đám cưới sang năm sau. Khách đồng ý không hoàn cọc.";
                }
                await db.SaveChangesAsync(cancellationToken);
                await SeedDayOfTasksAsync(project, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }

        await SeedEventFlowDemoAsync(manager, policy, today, cancellationToken);
        await SeedNotificationsAsync(manager, cancellationToken);
    }

    /// <summary>
    /// One-time demo of the stage flow on whatever events exist: align statuses with the event date, then add stage notes,
    /// supplier evidence, addon contracts and invoices so each screen has something to show.
    /// </summary>
    private async Task SeedEventFlowDemoAsync(User manager, EventPolicy policy, DateOnly today, CancellationToken cancellationToken)
    {
        if (await db.SystemSettings.AnyAsync(x => x.SettingKey == DemoFlag, cancellationToken)) return;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var events = await db.WeddingProjects.Include(p => p.SpecItems).ThenInclude(s => s.CatalogItem).Include(p => p.Payments)
            .Where(p => p.Status != EventStatus.Cancelled).OrderBy(p => p.EventDate).ToListAsync(cancellationToken);
        foreach (var p in events)
        {
            // Contract total follows the service lines; a value signed above what the floor allows is reset to the lines.
            var specTotal = EventFlowSupport.SpecTotal(p.SpecItems);
            if (specTotal > 0)
            {
                p.CurrentTotal = specTotal;
                if (specTotal < EventStageRules.MinimumTotal(p.BaseContractValue, policy)) p.BaseContractValue = specTotal;
                if (p.DepositAmount > p.BaseContractValue) p.DepositAmount = EventStageRules.DepositFor(p.BaseContractValue, policy);
            }
            if (p.Status is EventStatus.Settled or EventStatus.Closed) continue;
            var lockDate = EventStageRules.LockDate(p.EventDate, policy);
            if (p.EventDate < today) p.Status = EventStatus.Completed;
            else if (today >= lockDate) p.Status = EventStatus.Locked;
            else p.Status = EventStatus.Adjusting;
            p.LockedAt = p.Status == EventStatus.Adjusting ? null : p.LockedAt ?? lockDate.ToDateTime(new TimeOnly(0, 5));
        }
        await db.SaveChangesAsync(cancellationToken);

        DateTime At(DateOnly date, int hour) => VietnamTime.ConvertVietnamToUtc(date, new TimeOnly(hour, 0));
        var adjusting = events.Where(p => p.Status == EventStatus.Adjusting).Take(4).ToList();
        foreach (var p in adjusting)
        {
            var signed = DateOnly.FromDateTime(p.CreatedAt);
            var noteDay = signed.AddDays(Math.Max(1, (today.DayNumber - signed.DayNumber) / 2));
            if (noteDay > today) noteDay = today;
            db.StageNotes.AddRange(
                new StageNote { ProjectId = p.ProjectId, Stage = 2, NoteType = "Tasting", CreatedBy = manager.UserId, CreatedAt = At(noteDay, 10),
                    Content = "Khách thử 8 món của thực đơn B, đổi tôm hùm nướng sang cá song hấp xì dầu, giữ nguyên số món." },
                new StageNote { ProjectId = p.ProjectId, Stage = 2, NoteType = "Fitting", CreatedBy = manager.UserId, CreatedAt = At(noteDay, 15),
                    Content = "Cô dâu thử 2 mẫu áo, chọn mẫu đuôi cá trắng ngà. Chú rể tự thuê vest bên ngoài." });
            var catering = p.SpecItems.FirstOrDefault(s => s.CatalogItem.Category == "Catering");
            if (catering is not null && catering.Quantity > 5)
            {
                var before = p.CurrentTotal;
                var after = before - 2 * catering.UnitPrice;
                if (EventStageRules.ValidateAdjustedTotal(p.BaseContractValue, after, policy) is null)
                {
                    catering.Quantity -= 2;
                    p.CurrentTotal = after;
                    db.StageNotes.Add(new StageNote { ProjectId = p.ProjectId, Stage = 2, NoteType = "Adjustment", CreatedBy = manager.UserId, CreatedAt = At(today, 9),
                        ValueBefore = before, ValueAfter = after, Content = $"Khách giảm 2 bàn tiệc (còn {catering.Quantity} bàn) vì danh sách khách mời bên nhà trai giảm." });
                }
            }
        }

        var locked = events.Where(p => p.Status == EventStatus.Locked).ToList();
        for (var i = 0; i < locked.Count; i++)
        {
            var p = locked[i];
            var needs = p.SpecItems.Where(s => s.CatalogItem.RequiresSupplier).ToList();
            // The first locked event keeps one service without evidence, to show the supplier deadline reminder.
            foreach (var s in i == 0 ? needs.Skip(1) : needs)
                await AddEvidenceAsync(p, s, manager, cancellationToken);
            db.StageNotes.Add(new StageNote { ProjectId = p.ProjectId, Stage = 3, NoteType = "Supplier", CreatedBy = manager.UserId, CreatedAt = At(today, 8),
                Content = "Đã gọi xác nhận lịch giao hoa tươi và bàn ghế, nhà cung cấp giao trước 4 tiếng." });
            if (i == 0) AddAddon(p, manager, "Thêm 3 bàn tiệc cho khách phát sinh", "PendingCustomer", [("Thực đơn tiệc cưới (bàn thêm)", 3, 3_500_000m)]);
            if (i == 1 || locked.Count == 1) AddAddon(p, manager, "Thêm tiết mục múa lân khai tiệc", "Confirmed", [("Múa lân khai tiệc", 1, 3_000_000m)]);
        }

        foreach (var p in events.Where(p => p.Status == EventStatus.Completed))
        {
            AddAddon(p, manager, "Kéo dài giờ tiệc thêm 1 tiếng", "Confirmed", [("Phụ phí kéo dài giờ sảnh", 1, 5_000_000m), ("Phục vụ thêm giờ", 10, 200_000m)]);
            await db.SaveChangesAsync(cancellationToken);
            await EventFlowSupport.IssueInvoiceAsync(db, p, manager.UserId, policy, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);

        // Settled events show their paid invoice too.
        foreach (var p in events.Where(p => p.Status is EventStatus.Settled or EventStatus.Closed))
        {
            var invoice = await EventFlowSupport.IssueInvoiceAsync(db, p, manager.UserId, policy, cancellationToken);
            invoice.Status = "Paid";
            invoice.PaidAt = p.Payments.Where(x => x.PaymentType == PaymentKinds.Settlement).Select(x => x.PaidDate).FirstOrDefault()?.ToDateTime(new TimeOnly(10, 0))
                ?? p.EventDate.AddDays(2).ToDateTime(new TimeOnly(10, 0));
        }

        db.SystemSettings.Add(new SystemSetting { SettingKey = DemoFlag, SettingValue = today.ToString("yyyy-MM-dd"), UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Seeded the event stage flow demo on {Count} events.", events.Count);
    }

    private void AddAddon(WeddingProject p, User manager, string title, string status, IReadOnlyList<(string Description, int Quantity, decimal Price)> lines)
    {
        var count = db.AddonContracts.Local.Count(a => a.ProjectId == p.ProjectId) + db.AddonContracts.Count(a => a.ProjectId == p.ProjectId);
        var addon = new AddonContract
        {
            AddonCode = $"PS-{p.ProjectCode}-{count + 1:D2}", ProjectId = p.ProjectId, Title = title, Status = status, CreatedBy = manager.UserId,
            CreatedAt = DateTime.UtcNow.AddDays(-1), RemindedAt = DateTime.UtcNow.AddDays(-1),
            ConfirmedAt = status == "Confirmed" ? DateTime.UtcNow.AddHours(-6) : null,
            Note = status == "PendingCustomer" ? "Khách báo qua điện thoại, cần khách xác nhận trên hệ thống trước khi báo bếp." : "Khách đã xác nhận trên hệ thống."
        };
        foreach (var (description, quantity, price) in lines)
            addon.Items.Add(new AddonContractItem { Description = description, Quantity = quantity, UnitPrice = price });
        db.AddonContracts.Add(addon);
    }

    private async Task AddEvidenceAsync(WeddingProject p, SpecItem item, User manager, CancellationToken cancellationToken)
    {
        var supplier = item.CatalogItem.Category switch
        {
            "Catering" => "Công ty Thực phẩm Sạch Hòa Bình",
            "Decor" => "Hoa tươi Đà Lạt Garden",
            "Ceremony" => "Tráp cưới Hồng Phúc",
            "Performance" => "Đoàn nghệ thuật Ánh Dương",
            "Furniture" => "Cho thuê bàn ghế Thành Đạt",
            "Attire" => "Áo cưới Linh Nga",
            _ => "Nhà cung cấp Phúc An"
        };
        var type = item.CatalogItem.Category == "Catering" ? "Contract" : "Transfer";
        var bytes = DemoPdf($"MINH CHUNG {(type == "Contract" ? "HOP DONG" : "CHUYEN KHOAN")} - {p.ProjectCode}", $"{supplier} | {item.CatalogItem.Name}");
        var key = $"suppliers/{p.ProjectCode}/{Guid.NewGuid():N}.pdf";
        using (var stream = new MemoryStream(bytes)) await storage.UploadAsync(stream, key, cancellationToken);
        var file = new StoredFile
        {
            FileId = Guid.NewGuid(), ProjectId = p.ProjectId, Purpose = "SupplierEvidence", StorageKey = key, OriginalName = $"minh-chung-{item.CatalogItem.Category.ToLowerInvariant()}.pdf",
            ContentType = "application/pdf", SizeBytes = bytes.Length, UploadedBy = manager.UserId, UploadedAt = DateTime.UtcNow
        };
        db.StoredFiles.Add(file);
        db.SupplierEvidences.Add(new SupplierEvidence
        {
            ProjectId = p.ProjectId, SpecItemId = item.SpecItemId, SupplierName = supplier, EvidenceType = type, FileId = file.FileId,
            Note = type == "Contract" ? "Hợp đồng nguyên liệu, giao sáng ngày cưới." : "Đã chuyển khoản đặt cọc cho nhà cung cấp.",
            UploadedBy = manager.UserId, UploadedAt = DateTime.UtcNow
        });
    }

    /// <summary>A one-page PDF with two lines of ASCII text (offsets computed so any viewer opens it).</summary>
    private static byte[] DemoPdf(string title, string line)
    {
        static string Esc(string s) => new string(s.Normalize(NormalizationForm.FormD).Where(c => c < 128).ToArray()).Replace("(", "[").Replace(")", "]");
        var stream = $"BT /F1 16 Tf 60 760 Td ({Esc(title)}) Tj ET\nBT /F1 12 Tf 60 730 Td ({Esc(line)}) Tj ET\n";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}endstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = sb.Length;
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) sb.Append($"{offset:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private async Task SeedDayOfTasksAsync(WeddingProject project, CancellationToken cancellationToken)
    {
        if (await db.DayOfTasks.AnyAsync(x => x.ProjectId == project.ProjectId, cancellationToken)) return;
        var baseUtc = VietnamTime.ConvertVietnamToUtc(project.EventDate, new TimeOnly(11, 0));
        var done = project.EventDate < VietnamTime.GetVietnamToday() && project.Status != EventStatus.Cancelled;
        DayOfTask Task(string name, int minutes, bool photo) => new()
        {
            ProjectId = project.ProjectId, TaskName = name, DeadlineTime = baseUtc.AddMinutes(minutes), PhotoRequired = photo,
            Status = done ? "Done" : "Pending", CompletedAt = done ? baseUtc.AddMinutes(minutes) : null, CompletedBy = done ? project.ManagerId : null
        };
        db.DayOfTasks.AddRange(
            Task("Kiểm tra set-up bàn tiệc, âm thanh và ánh sáng", -60, true),
            Task("Đón tiếp khách mời và điều phối khai tiệc", 0, false),
            Task("Giám sát phục vụ tiệc theo phiếu điều phối", 60, true),
            Task("Đối soát phát sinh và nghiệm thu tiệc", 120, false));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedNotificationsAsync(User manager, CancellationToken cancellationToken)
    {
        if (await db.Notifications.AnyAsync(cancellationToken)) return;
        var customer = await db.Users.FirstOrDefaultAsync(u => u.Role == "Customer", cancellationToken);
        var now = DateTime.UtcNow;
        db.Notifications.AddRange(
            new Notification { RecipientId = manager.UserId, Type = "Stage reminder", Message = "[WPSM-DEMO-02] Đã khóa dịch vụ. Đặt nhà cung cấp và tải minh chứng trước hạn.", LinkUrl = "/manager/projects/WPSM-DEMO-02/suppliers", CreatedAt = now.AddMinutes(-35) },
            new Notification { RecipientId = manager.UserId, Type = "Addon contract", Message = "[WPSM-DEMO-02] Hợp đồng phát sinh đang chờ khách xác nhận.", LinkUrl = "/manager/projects/WPSM-DEMO-02/addons", CreatedAt = now.AddHours(-2) },
            new Notification { RecipientId = manager.UserId, Type = "Payment", Message = "[WPSM-DEMO-03] Hóa đơn tất toán đã phát hành, chờ thu.", LinkUrl = "/manager/projects/WPSM-DEMO-03/payments", CreatedAt = now.AddHours(-4) });
        if (customer is not null)
            db.Notifications.AddRange(
                new Notification { RecipientId = customer.UserId, Type = "Stage reminder", Message = "Dịch vụ của sự kiện đã được chốt. Cần thay đổi, hãy liên hệ người phụ trách.", LinkUrl = "/customer/event", CreatedAt = now.AddHours(-1) },
                new Notification { RecipientId = customer.UserId, Type = "Payment", Message = "Tiền đặt cọc đã được xác nhận.", LinkUrl = "/customer/payments", IsRead = true, CreatedAt = now.AddDays(-1) });
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

    private sealed record HallSeed(string Name, int Minimum, int Maximum, decimal BasePrice);
    private sealed record CatalogSeed(string Name, string Category, string Unit, decimal Price, bool RequiresSupplier);
    private sealed record ProjectSeed(string Code, string Couple, string Email, string Phone, string Hall, string Slot, DateOnly EventDate, decimal Value, string? Status = null);

    // Core combo (Hall, Decor, MC, AV at default price) plus one catering line whose tables × unit price fills the rest,
    // so the contract value equals the service lines and stays inside the PriceFrames.
    private static IEnumerable<SpecItem> BuildSampleSpecification(int projectId, decimal value, IReadOnlyList<ServiceCatalogItem> catalog, IReadOnlyDictionary<int, PriceFrame> frames)
    {
        var lines = new List<SpecItem>();
        foreach (var category in new[] { "Hall", "Decor", "MC", "AV" })
        {
            var item = catalog.FirstOrDefault(x => x.Category == category);
            if (item is not null) lines.Add(new SpecItem { ProjectId = projectId, CatalogItemId = item.ItemId, Description = item.Name, Quantity = 1, UnitPrice = item.DefaultPrice });
        }
        var catering = catalog.FirstOrDefault(x => x.Category == "Catering");
        if (catering is null) return lines;

        var hall = lines.FirstOrDefault(x => catalog.First(c => c.ItemId == x.CatalogItemId).Category == "Hall");
        if (hall is not null && catering.DefaultPrice > 0 && frames.TryGetValue(hall.CatalogItemId, out var hallFrame))
        {
            var others = lines.Where(x => x != hall).Sum(x => x.UnitPrice);
            for (var step = 0m; step <= hallFrame.MaxPrice - hallFrame.MinPrice; step += 500_000m)
                foreach (var hallPrice in new[] { hall.UnitPrice - step, hall.UnitPrice + step })
                {
                    if (hallPrice < hallFrame.MinPrice || hallPrice > hallFrame.MaxPrice) continue;
                    var remaining = value - others - hallPrice;
                    if (remaining <= 0 || remaining % catering.DefaultPrice != 0) continue;
                    hall.UnitPrice = hallPrice;
                    lines.Add(new SpecItem { ProjectId = projectId, CatalogItemId = catering.ItemId, Description = catering.Name, Quantity = (int)(remaining / catering.DefaultPrice), UnitPrice = catering.DefaultPrice });
                    return lines;
                }
        }
        var rest = value - lines.Sum(x => x.UnitPrice);
        var tables = Math.Max(1, (int)Math.Round(rest / catering.DefaultPrice));
        lines.Add(new SpecItem { ProjectId = projectId, CatalogItemId = catering.ItemId, Description = catering.Name, Quantity = tables, UnitPrice = catering.DefaultPrice });
        return lines;
    }
}
