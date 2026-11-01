using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
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
builder.Services.AddScoped<CredentialEmailSender>();
builder.Services.AddHostedService<CredentialEmailDispatcher>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "WPSM.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnValidatePrincipal = async context =>
    {
        var account = await context.HttpContext.RequestServices.GetRequiredService<IAccountProfileRepository>()
            .FindAccountAsync(context.Principal?.Identity?.Name ?? "", context.HttpContext.RequestAborted);
        if (account is null || account.Status != "Active" ||
            context.Principal?.FindFirstValue("PasswordVersion") != PasswordVersion(account.PasswordHash) ||
            context.Principal?.FindFirstValue(ClaimTypes.Role) != account.Role)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
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
    catch (KeyNotFoundException ex) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
    }
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
api.AddEndpointFilter(async (invocation, next) =>
{
    var context = invocation.HttpContext;
    var path = context.Request.Path.Value ?? "";
    if (context.User.Identity?.IsAuthenticated == true)
    {
        if (context.User.FindFirstValue("MustChangePassword") == bool.TrueString &&
            path is not ("/api/wpsm/auth/me" or "/api/wpsm/auth/change-password" or "/api/wpsm/auth/logout" or "/api/wpsm/profile"))
            return Results.Json(new { message = "Vui lòng đổi mật khẩu trước khi sử dụng hệ thống." }, statusCode: 403);
        if (path.StartsWith("/api/wpsm/projects/", StringComparison.OrdinalIgnoreCase) &&
            context.Request.RouteValues["id"] is string code &&
            !await context.RequestServices.GetRequiredService<IProjectRepository>().CanAccessAsync(code, UserId(context.User),
                context.User.FindFirstValue(ClaimTypes.Role) ?? "", context.RequestAborted))
            return Results.NotFound(new { message = "Không tìm thấy dự án trong phạm vi tài khoản." });
    }
    return await next(invocation);
});
api.MapGet("/bootstrap", async (string? projectCode, IWpsmPortalSnapshotReader repo, ClaimsPrincipal user, CancellationToken ct) =>
    Results.Ok(await repo.GetSnapshotAsync(UserId(user), projectCode, ct))).RequireAuthorization();
api.MapGet("/projects", async (IProjectRepository repo, ClaimsPrincipal user, CancellationToken ct) => Results.Ok(await repo.GetProjectsAsync(UserId(user), user.FindFirstValue(ClaimTypes.Role), ct)))
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));
api.MapGet("/projects/{id}", async (string id, IProjectRepository repo, CancellationToken ct) =>
    await repo.GetProjectAsync(id, ct) is { } project ? Results.Ok(project) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));
api.MapPost("/projects", async (CreateProjectRequest request, IProjectRepository repo, IAccountProfileRepository accounts, IPasswordHasher<User> hasher, IDataProtectionProvider protection, ClaimsPrincipal user, CancellationToken ct) =>
{
    var temporaryPassword = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*", 16);
    var newCustomer = await accounts.FindAccountAsync(request.CustomerEmail, ct) is null;
    var passwordHash = hasher.HashPassword(new User { Email = request.CustomerEmail, Role = "Customer" }, temporaryPassword);
    var created = await repo.CreateProjectAsync(new CreateProjectCommand(request.CoupleName, request.CustomerEmail, request.CustomerPhone,
        request.Hall, request.Slot, request.EventDate, request.BaseValue, request.NrdAmount, request.PaymentMethod, request.ReceiptFilePath,
        protection.CreateProtector("WPSM.CredentialEmail.v1").Protect(System.Text.Json.JsonSerializer.Serialize(new CredentialEmailPayload(request.CoupleName, temporaryPassword)))),
        UserId(user), passwordHash, temporaryPassword, ct);
    var emailStatus = created.TemporaryPassword is not null ? "Pending" : "ExistingAccount";
    return Results.Created($"/api/wpsm/projects/{created.Project.Id}", new CreatedProjectResponse(created.Project, newCustomer ? created.TemporaryPassword : null, emailStatus));
}).RequireAuthorization(policy => policy.RequireRole("Manager"));
api.MapPut("/projects/{id}/specification", async (string id, SpecificationRequest request, ISpecificationRoadmapRepository repo, CancellationToken ct) =>
    await repo.UpdateSpecificationAsync(id, new SpecificationCommand(request.Summary, request.GuestCount, request.Items), ct) is { } project
        ? Results.Ok(project) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Manager"));
api.MapPost("/projects/{id}/freeze", async (string id, ISpecificationRoadmapRepository repo, CancellationToken ct) =>
    await repo.FreezeBaselineAsync(id, ct) is { } project ? Results.Ok(project) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Manager"));
api.MapPost("/projects/{id}/nrd-receipt", async (string id, IFormFile receipt, IProjectRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
{
    var extension = Path.GetExtension(receipt.FileName).ToLowerInvariant();
    if (receipt.Length is <= 0 or > 10 * 1024 * 1024 || extension is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
        return Results.BadRequest(new { message = "Chứng từ phải là PDF/JPG/PNG và không vượt quá 10 MB." });
    await using var stream = new MemoryStream();
    await receipt.CopyToAsync(stream, ct);
    var content = stream.ToArray();
    var valid = extension switch
    {
        ".pdf" => content.AsSpan().StartsWith("%PDF-"u8),
        ".png" => content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        _ => content.AsSpan().StartsWith(new byte[] { 255, 216, 255 })
    };
    if (!valid) return Results.BadRequest(new { message = "Nội dung tệp không khớp định dạng PDF/JPG/PNG." });
    var contentType = extension == ".pdf" ? "application/pdf" : extension == ".png" ? "image/png" : "image/jpeg";
    return await repo.RecordNrdReceiptAsync(id, UserId(user), content, Path.GetFileName(receipt.FileName), contentType, ct)
        ? Results.Ok(new { projectCode = id, paymentStatus = "Paid" }) : Results.NotFound();
}).RequireAuthorization(policy => policy.RequireRole("Manager")).DisableAntiforgery();
api.MapGet("/halls", async (DateOnly? date, WpsmDbContext dbContext, CancellationToken ct) =>
{
    var halls = await dbContext.Halls.Where(h => h.Status == "Active")
        .Include(h => h.Slots).OrderBy(h => h.Name).ToListAsync(ct);
    var checkDate = date ?? DateOnly.FromDateTime(DateTime.Today);
    var bookedSlots = await dbContext.WeddingProjects
        .Where(wp => wp.EventDate == checkDate)
        .Select(wp => wp.SlotId).ToListAsync(ct);
    var result = halls.Select(h => new HallView(h.HallId, h.Name, h.MinCapacity, h.MaxCapacity, h.BasePrice, h.Status,
        h.Slots.OrderBy(s => s.StartTime).Select(s => new SlotView(s.SlotId, s.SlotName, s.StartTime, s.EndTime, !bookedSlots.Contains(s.SlotId))).ToList()));
    return Results.Ok(result);
}).RequireAuthorization();
api.MapGet("/projects/{id}/nrd-receipt", async (string id, WpsmDbContext db, IWebHostEnvironment environment, CancellationToken ct) =>
{
    var payment = await db.Payments.Where(p => p.Project.ProjectCode == id && p.PaymentType == "NRD")
        .Select(p => new { p.ReceiptFilePath, p.ReceiptContent, p.ReceiptFileName, p.ReceiptContentType }).SingleOrDefaultAsync(ct);
    if (payment?.ReceiptContent is { } bytes) return Results.File(bytes, payment.ReceiptContentType ?? "application/octet-stream", payment.ReceiptFileName ?? "NRD-receipt");
    var path = payment?.ReceiptFilePath;
    if (string.IsNullOrWhiteSpace(path)) return Results.NotFound();
    var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "receipts")) + Path.DirectorySeparatorChar;
    var absolutePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", path));
    if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolutePath)) return Results.NotFound();
    var contentType = Path.GetExtension(absolutePath).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg"
    };
    return Results.File(absolutePath, contentType, $"{id}-NRD{Path.GetExtension(absolutePath)}");
}).RequireAuthorization();
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
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        return Results.BadRequest(new { message = "Vui lòng nhập email và mật khẩu." });
    var account = await repo.FindAccountAsync(request.Email, ct);
    if (account is null || account.Status != "Active") return Results.Json(new { message = "Email hoặc mật khẩu không đúng, hoặc tài khoản đã bị vô hiệu hóa." }, statusCode: 401);
    var verification = VerifyPassword(hasher, account, request.Password);
    if (verification == PasswordVerificationResult.Failed) return Results.Json(new { message = "Email hoặc mật khẩu không đúng, hoặc tài khoản đã bị vô hiệu hóa." }, statusCode: 401);
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, account.UserId.ToString()),
        new Claim(ClaimTypes.Name, account.Email),
        new Claim(ClaimTypes.Role, account.Role),
        new Claim("FullName", account.FullName),
        new Claim("PasswordVersion", PasswordVersion(account.PasswordHash)),
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
    if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 8 || request.NewPassword != request.ConfirmPassword)
        return Results.BadRequest(new { message = "Mật khẩu mới phải dài ít nhất 8 ký tự và hai lần nhập phải trùng nhau." });
    if (request.NewPassword == request.CurrentPassword)
        return Results.BadRequest(new { message = "Mật khẩu mới không được trùng với mật khẩu hiện tại." });
    var account = await repo.FindAccountAsync(user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Name) ?? "", ct);
    if (account is null || VerifyPassword(hasher, account, request.CurrentPassword) == PasswordVerificationResult.Failed)
        return Results.BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
    var hash = hasher.HashPassword(new User { UserId = account.UserId, Email = account.Email }, request.NewPassword);
    if (!await repo.UpdatePasswordAsync(account.UserId, hash, ct)) return Results.Unauthorized();
    var refreshed = await repo.FindAccountAsync(account.Email, ct);
    if (refreshed is not null)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, refreshed.UserId.ToString()),
            new Claim(ClaimTypes.Name, refreshed.Email),
            new Claim(ClaimTypes.Role, refreshed.Role),
            new Claim("FullName", refreshed.FullName),
            new Claim("PasswordVersion", PasswordVersion(refreshed.PasswordHash)),
            new Claim("MustChangePassword", refreshed.MustChangePassword.ToString())
        };
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    }
    return Results.NoContent();
}).RequireAuthorization();

app.Run();

static PasswordVerificationResult VerifyPassword(IPasswordHasher<User> hasher, AccountView account, string password)
{
    try
    {
        return hasher.VerifyHashedPassword(new User { UserId = account.UserId, Email = account.Email, Role = account.Role }, account.PasswordHash, password);
    }
    catch (FormatException)
    {
        // Legacy/manual DB records may contain a placeholder rather than an
        // Identity password hash. Reject them without leaking a server error.
        return PasswordVerificationResult.Failed;
    }
}
static string PasswordVersion(string passwordHash) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(passwordHash)));
static int UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
    ? id : throw new InvalidOperationException("Phiên đăng nhập không hợp lệ.");

public sealed record CreateProjectRequest(string CoupleName, string CustomerEmail, string CustomerPhone, string Hall, string Slot,
    DateOnly EventDate, decimal BaseValue, decimal NrdAmount, string PaymentMethod = "Cash", string? ReceiptFilePath = null);
public sealed record CreatedProjectResponse(WeddingProjectView Project, string? TemporaryPassword, string EmailStatus);
public sealed record SpecificationRequest(string Summary, int GuestCount, IReadOnlyList<SpecificationLineCommand>? Items = null);
public sealed record CatalogItemRequest(string Name, string Category, string Unit, decimal BasePrice, bool Active);
public sealed record PriceFrameRequest(decimal MinPrice, decimal MaxPrice, DateOnly? EffectiveFrom = null);
public sealed record PolicyRequest(decimal NrdRatio, decimal T30Ratio, int CrPenaltyDays, decimal CrPenaltyPercent, DateOnly? EffectiveFrom = null);
public sealed record ProfileRequest(string FullName, string Phone);
public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
