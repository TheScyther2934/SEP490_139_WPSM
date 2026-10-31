using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using WPSM.API;
using WPSM.Application.Persistence;
using WPSM.Infrastructure.Persistence;
using WPSM.Infrastructure.Persistence.Entities;
using WPSM.Infrastructure.Persistence.Repositories;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("WpsmDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Thiếu ConnectionStrings:WpsmDb. Hãy cấu hình bằng User Secrets hoặc biến môi trường.");

builder.Services.AddDbContext<WpsmDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ProjectReadModel>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ISpecificationRoadmapRepository, SpecificationRoadmapRepository>();
builder.Services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
builder.Services.AddScoped<IAccountProfileRepository, AccountProfileRepository>();
builder.Services.AddScoped<IWpsmPortalSnapshotReader, WpsmPortalSnapshotReader>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<DevelopmentDatabaseSeeder>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "WPSM.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();

var app = builder.Build();
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Wpsm:SeedDevelopmentData"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentDatabaseSeeder>().SeedAsync();
}
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (InvalidOperationException ex) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
    }
    catch (DbUpdateException ex) when (!context.Response.HasStarted)
    {
        app.Logger.LogWarning(ex, "WPSM data change violated a database constraint.");
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { message = "Dữ liệu xung đột với quy tắc database. Kiểm tra mã, email, lịch sảnh và các ràng buộc liên quan." });
    }
    catch (SqlException ex) when (!context.Response.HasStarted)
    {
        app.Logger.LogError(ex, "WPSM database request failed.");
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { message = "Không kết nối được WPSM_DB. Kiểm tra SQL Server và connection string." });
    }
});
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api/wpsm");
api.MapGet("/bootstrap", async (string? projectCode, IWpsmPortalSnapshotReader repo, ClaimsPrincipal user, CancellationToken ct) =>
    Results.Ok(await repo.GetSnapshotAsync(UserId(user), projectCode, ct))).RequireAuthorization();
api.MapGet("/projects", async (IProjectRepository repo, CancellationToken ct) => Results.Ok(await repo.GetProjectsAsync(cancellationToken: ct)))
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));
api.MapGet("/projects/{id}", async (string id, IProjectRepository repo, CancellationToken ct) =>
    await repo.GetProjectAsync(id, ct) is { } project ? Results.Ok(project) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));
api.MapPost("/projects", async (CreateProjectRequest request, IProjectRepository repo, IAccountProfileRepository accounts, IPasswordHasher<User> hasher, ClaimsPrincipal user, CancellationToken ct) =>
{
    var temporaryPassword = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*", 16);
    var newCustomer = await accounts.FindAccountAsync(request.CustomerEmail, ct) is null;
    var passwordHash = hasher.HashPassword(new User { Email = request.CustomerEmail, Role = "Customer" }, temporaryPassword);
    var created = await repo.CreateProjectAsync(new CreateProjectCommand(request.CoupleName, request.CustomerEmail, request.CustomerPhone,
        request.Hall, request.Slot, request.EventDate, request.BaseValue, request.NrdAmount, request.PaymentMethod, request.ReceiptFilePath),
        UserId(user), passwordHash, temporaryPassword, ct);
    return Results.Created($"/api/wpsm/projects/{created.Project.Id}", new CreatedProjectResponse(created.Project, newCustomer ? created.TemporaryPassword : null));
}).RequireAuthorization(policy => policy.RequireRole("Manager"));
api.MapPut("/projects/{id}/specification", async (string id, SpecificationRequest request, ISpecificationRoadmapRepository repo, CancellationToken ct) =>
    await repo.UpdateSpecificationAsync(id, new SpecificationCommand(request.Summary, request.GuestCount, request.Items), ct) is { } project
        ? Results.Ok(project) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Manager"));
api.MapPost("/projects/{id}/freeze", async (string id, ISpecificationRoadmapRepository repo, CancellationToken ct) =>
    await repo.FreezeBaselineAsync(id, ct) is { } project ? Results.Ok(project) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Manager"));
api.MapPost("/projects/{id}/nrd-receipt", async (string id, IFormFile receipt, IProjectRepository repo, ClaimsPrincipal user, IWebHostEnvironment environment, CancellationToken ct) =>
{
    var extension = Path.GetExtension(receipt.FileName).ToLowerInvariant();
    if (receipt.Length is <= 0 or > 10 * 1024 * 1024 || extension is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
        return Results.BadRequest(new { message = "Chứng từ phải là PDF/JPG/PNG và không vượt quá 10 MB." });
    var directory = Path.Combine(environment.ContentRootPath, "App_Data", "receipts");
    Directory.CreateDirectory(directory);
    var fileName = $"{Guid.NewGuid():N}{extension}";
    var absolutePath = Path.Combine(directory, fileName);
    var relativePath = Path.Combine("receipts", fileName).Replace('\\', '/');
    try
    {
        await using (var stream = File.Create(absolutePath)) await receipt.CopyToAsync(stream, ct);
        if (!await repo.RecordNrdReceiptAsync(id, UserId(user), relativePath, ct))
        {
            File.Delete(absolutePath);
            return Results.NotFound();
        }
        return Results.Ok(new { projectCode = id, paymentStatus = "Paid" });
    }
    catch
    {
        if (File.Exists(absolutePath)) File.Delete(absolutePath);
        throw;
    }
}).RequireAuthorization(policy => policy.RequireRole("Manager")).DisableAntiforgery();
api.MapGet("/catalog", async (IConfigurationRepository repo, CancellationToken ct) => Results.Ok(await repo.GetCatalogAsync(ct)))
    .RequireAuthorization();
api.MapPost("/catalog", async (CatalogItemRequest request, IConfigurationRepository repo, CancellationToken ct) =>
    Results.Ok(await repo.AddCatalogItemAsync(new CatalogItemCommand(request.Name, request.Category, request.Unit, request.BasePrice, request.Active), ct)))
    .RequireAuthorization(policy => policy.RequireRole("Admin"));
api.MapGet("/price-frames", async (IConfigurationRepository repo, CancellationToken ct) => Results.Ok(await repo.GetPriceFramesAsync(ct)))
    .RequireAuthorization();
api.MapPut("/price-frames/{id:int}", async (int id, PriceFrameRequest request, IConfigurationRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    await repo.UpdatePriceFrameAsync(id, new PriceFrameCommand(request.MinPrice, request.MaxPrice, request.EffectiveFrom), UserId(user), ct) is { } frame
        ? Results.Ok(frame) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Admin"));
api.MapGet("/policies", async (IConfigurationRepository repo, CancellationToken ct) => Results.Ok(await repo.GetPoliciesAsync(ct)))
    .RequireAuthorization();
api.MapPut("/policies", async (PolicyRequest request, IConfigurationRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    Results.Ok(await repo.UpdatePoliciesAsync(new PolicyCommand(request.NrdRatio, request.T30Ratio, request.CrPenaltyDays, request.CrPenaltyPercent, request.EffectiveFrom), UserId(user), ct)))
    .RequireAuthorization(policy => policy.RequireRole("Admin"));
api.MapGet("/profile", async (IAccountProfileRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    await repo.GetProfileAsync(UserId(user), ct) is { } profile ? Results.Ok(profile) : Results.NotFound())
    .RequireAuthorization();
api.MapPut("/profile", async (ProfileRequest request, IAccountProfileRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    await repo.UpdateProfileAsync(UserId(user), new ProfileCommand(request.FullName, request.Phone), ct) is { } profile
        ? Results.Ok(profile) : Results.NotFound())
    .RequireAuthorization();
api.MapPost("/auth/login", async (LoginRequest request, IAccountProfileRepository repo, IPasswordHasher<User> hasher, HttpContext context, CancellationToken ct) =>
{
    var account = await repo.FindAccountAsync(request.Email, ct);
    if (account is null || account.Status != "Active") return Results.Unauthorized();
    var verification = hasher.VerifyHashedPassword(new User { UserId = account.UserId, Email = account.Email, Role = account.Role }, account.PasswordHash, request.Password);
    if (verification == PasswordVerificationResult.Failed) return Results.Unauthorized();
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, account.UserId.ToString()),
        new Claim(ClaimTypes.Name, account.Email),
        new Claim(ClaimTypes.Role, account.Role),
        new Claim("FullName", account.FullName),
        new Claim("MustChangePassword", account.MustChangePassword.ToString())
    };
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.Ok(new { authenticated = true, user = new ProfileView(account.FullName, account.Email, account.Phone ?? "", account.Role), mustChangePassword = account.MustChangePassword });
});
api.MapPost("/auth/logout", async (HttpContext context) => { await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.NoContent(); })
    .RequireAuthorization();
api.MapGet("/auth/me", async (IAccountProfileRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
{
    var account = await repo.FindAccountAsync(user.Identity?.Name ?? "", ct);
    return account is null || account.Status != "Active"
        ? Results.Unauthorized()
        : Results.Ok(new { authenticated = true, user = new ProfileView(account.FullName, account.Email, account.Phone ?? "", account.Role), mustChangePassword = account.MustChangePassword });
}).RequireAuthorization();
api.MapPost("/auth/change-password", async (ChangePasswordRequest request, IAccountProfileRepository repo, IPasswordHasher<User> hasher, ClaimsPrincipal user, HttpContext context, CancellationToken ct) =>
{
    if (request.NewPassword.Length < 8 || request.NewPassword != request.ConfirmPassword)
        return Results.BadRequest(new { message = "Mật khẩu mới phải dài ít nhất 8 ký tự và hai lần nhập phải trùng nhau." });
    var account = await repo.FindAccountAsync(user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name) ?? "", ct);
    if (account is null || hasher.VerifyHashedPassword(new User { UserId = account.UserId }, account.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        return Results.BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
    var hash = hasher.HashPassword(new User { UserId = account.UserId, Email = account.Email }, request.NewPassword);
    await repo.UpdatePasswordAsync(account.UserId, hash, ct);
    var refreshed = await repo.FindAccountAsync(account.Email, ct);
    if (refreshed is not null)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, refreshed.UserId.ToString()),
            new Claim(ClaimTypes.Name, refreshed.Email),
            new Claim(ClaimTypes.Role, refreshed.Role),
            new Claim("FullName", refreshed.FullName),
            new Claim("MustChangePassword", refreshed.MustChangePassword.ToString())
        };
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    }
    return Results.NoContent();
}).RequireAuthorization();

app.Run();

static int UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
    ? id : throw new InvalidOperationException("Phiên đăng nhập không hợp lệ.");

public sealed record CreateProjectRequest(string CoupleName, string CustomerEmail, string CustomerPhone, string Hall, string Slot,
    DateOnly EventDate, decimal BaseValue, decimal NrdAmount, string PaymentMethod = "Cash", string? ReceiptFilePath = null);
public sealed record CreatedProjectResponse(WeddingProjectView Project, string? TemporaryPassword);
public sealed record SpecificationRequest(string Summary, int GuestCount, IReadOnlyList<SpecificationLineCommand>? Items = null);
public sealed record CatalogItemRequest(string Name, string Category, string Unit, decimal BasePrice, bool Active);
public sealed record PriceFrameRequest(decimal MinPrice, decimal MaxPrice, DateOnly? EffectiveFrom = null);
public sealed record PolicyRequest(decimal NrdRatio, decimal T30Ratio, int CrPenaltyDays, decimal CrPenaltyPercent, DateOnly? EffectiveFrom = null);
public sealed record ProfileRequest(string FullName, string Phone);
public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
