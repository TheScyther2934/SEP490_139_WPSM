using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WPSM.Infrastructure.Persistence;
using WPSM.Infrastructure.Persistence.Entities;

namespace WPSM.API;

public sealed class DevelopmentDatabaseSeeder(
    WpsmDbContext db,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration)
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
}
