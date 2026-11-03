using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using API;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Files;
using Infrastructure.Jobs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Domain.Enums;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("WpsmDb");
builder.Services.AddDbContext<WpsmDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddDataProtection();
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
builder.Services.AddScoped<ICustomerDashboardRepository, CustomerDashboardRepository>();
builder.Services.AddScoped<ICustomerRoadmapRepository, CustomerRoadmapRepository>();
builder.Services.AddScoped<IManagerRoadmapRepository, ManagerRoadmapRepository>();
builder.Services.AddScoped<IAddendumRepository, AddendumRepository>();

var signingKey = builder.Configuration["Files:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKey))
{
    if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Thiếu Files:SigningKey. Hãy cấu hình bằng User Secrets hoặc biến môi trường.");
    signingKey = RandomNumberGenerator.GetHexString(64);
}
var appDataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
builder.Services.AddSingleton(new FileStorageOptions
{
    RootPath = builder.Configuration["Files:RootPath"] ?? Path.Combine(appDataPath, "files"),
    SigningKey = signingKey
});
builder.Services.AddSingleton(new EmailOptions
{
    Host = builder.Configuration["Smtp:Host"],
    Port = builder.Configuration.GetValue("Smtp:Port", 587),
    User = builder.Configuration["Smtp:User"],
    Password = builder.Configuration["Smtp:Password"],
    EnableSsl = builder.Configuration.GetValue("Smtp:EnableSsl", true),
    From = builder.Configuration["Smtp:From"] ?? "no-reply@wpsm.local",
    PickupDirectory = builder.Configuration["Smtp:PickupDirectory"] ?? (builder.Environment.IsDevelopment() ? Path.Combine(appDataPath, "mail") : null),
    WebhookSecret = builder.Configuration["Smtp:WebhookSecret"],
    PublicBaseUrl = builder.Configuration["Wpsm:PublicBaseUrl"] ?? "http://localhost:5231"
});
builder.Services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IEmailOutbox, EmailOutbox>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailDeliveryService, EmailDeliveryService>();
builder.Services.AddScoped<EmailDispatchJob>();

var backgroundJobsEnabled = builder.Configuration.GetValue("Hangfire:Enabled", true) && !string.IsNullOrWhiteSpace(connectionString);
if (backgroundJobsEnabled)
{
    builder.Services.AddHangfire(config => config.UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings()
        .UseSqlServerStorage(() => new SqlConnection(connectionString)));
    builder.Services.AddHangfireServer(options => options.WorkerCount = 2);
}

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
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5000", "https://localhost:5001", "http://localhost:5030", "https://localhost:7030", "http://localhost:5231")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Wpsm:SeedDevelopmentData", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentDatabaseSeeder>().SeedAsync();
}
if (backgroundJobsEnabled)
{
    RecurringJob.AddOrUpdate<EmailDispatchJob>("job-07-email-dispatch", job => job.RunAsync(CancellationToken.None), "*/1 * * * *");
}

app.UseCors();
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

api.MapGet("/projects", async (IProjectRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    Results.Ok(await repo.GetProjectsAsync(UserId(user), user.FindFirstValue(ClaimTypes.Role), ct)))
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

api.MapGet("/projects/{id}", async (string id, IProjectRepository repo, CancellationToken ct) =>
    await repo.GetProjectAsync(id, ct) is { } project ? Results.Ok(project) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

api.MapPost("/projects", async (CreateProjectRequest request, IProjectRepository repo, IAccountProfileRepository accounts, IPasswordHasher<User> hasher, IDataProtectionProvider protection, EmailOptions email, IWebHostEnvironment environment, ClaimsPrincipal user, CancellationToken ct) =>
{
    var temporaryPassword = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*", 16);
    var newCustomer = await accounts.FindAccountAsync(request.CustomerEmail, ct) is null;
    var passwordHash = hasher.HashPassword(new User { Email = request.CustomerEmail, Role = "Customer" }, temporaryPassword);
    var created = await repo.CreateProjectAsync(new CreateProjectCommand(request.CoupleName, request.CustomerEmail, request.CustomerPhone,
        request.Hall, request.Slot, request.EventDate, request.BaseValue, request.NrdAmount, request.PaymentMethod, request.ReceiptFilePath,
        protection.CreateProtector("WPSM.CredentialEmail.v1").Protect(JsonSerializer.Serialize(new CredentialEmailPayload(request.CoupleName, temporaryPassword)))),
        UserId(user), passwordHash, temporaryPassword, ct);
    var emailStatus = created.TemporaryPassword is not null ? "Pending" : "ExistingAccount";
    return Results.Created($"/api/wpsm/projects/{created.Project.Id}", new CreatedProjectResponse(created.Project, newCustomer ? created.TemporaryPassword : null, emailStatus));
}).RequireAuthorization(policy => policy.RequireRole("Manager"));

api.MapPost("/projects/{id}/resend-credentials", async (string id, IProjectRepository repo, IPasswordHasher<User> hasher, EmailOptions email, IWebHostEnvironment environment, ClaimsPrincipal user, CancellationToken ct) =>
{
    var temporaryPassword = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*", 16);
    var passwordHash = hasher.HashPassword(new User { Role = "Customer" }, temporaryPassword);
    var sent = await repo.ResendCredentialsAsync(id, UserId(user), passwordHash, temporaryPassword, ct);
    if (!sent) return Results.NotFound();
    return Results.Ok(new { temporaryPassword = ShowsPasswordOnScreen(environment, email) ? temporaryPassword : null, emailQueued = true });
}).RequireAuthorization(policy => policy.RequireRole("Manager"));

api.MapPut("/projects/{id}/specification", async (string id, SpecificationRequest request, ISpecificationRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    await repo.UpdateSpecificationAsync(id, UserId(user), new SpecificationCommand(request.Summary, request.GuestCount, request.Items), ct) is { } project
        ? Results.Ok(project) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Manager"));

api.MapPost("/projects/{id}/freeze", async (string id, ISpecificationRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    await repo.FreezeBaselineAsync(id, UserId(user), ct) is { } project ? Results.Ok(project) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Manager"));

api.MapPost("/projects/{id}/nrd-receipt", async (string id, IFormFile receipt, IProjectRepository repo, IFileService files, ClaimsPrincipal user, CancellationToken ct) =>
{
    var extension = Path.GetExtension(receipt.FileName).ToLowerInvariant();
    if (receipt.Length is <= 0 or > 10 * 1024 * 1024 || extension is not (".pdf" or ".jpg" or ".jpeg" or ".png"))
        return Results.BadRequest(new { message = "Chứng từ phải là PDF/JPG/PNG và không vượt quá 10 MB." });

    await using var buffer = new MemoryStream();
    await receipt.CopyToAsync(buffer, ct);
    var bytes = buffer.ToArray();
    var recorded = await repo.RecordNrdReceiptAsync(id, UserId(user), bytes, receipt.FileName, receipt.ContentType ?? "application/octet-stream", ct);
    if (!recorded) return Results.NotFound();

    try
    {
        await using var uploadStream = new MemoryStream(bytes);
        await files.UploadAsync(new FileUploadCommand(uploadStream, receipt.FileName, "NrdReceipt", id, UserId(user)), ct);
    }
    catch { }

    return Results.Ok(new { message = "Đã lưu biên nhận NRD thành công.", paymentStatus = "Paid" });
}).RequireAuthorization(policy => policy.RequireRole("Manager")).DisableAntiforgery();

api.MapGet("/halls", async (DateOnly? date, WpsmDbContext dbContext, CancellationToken ct) =>
{
    var halls = await dbContext.Halls.AsNoTracking().Where(h => h.Status == "Active")
        .OrderBy(h => h.Name)
        .Select(h => new HallView(
            h.HallId,
            h.Name,
            h.MinCapacity,
            h.MaxCapacity,
            h.BasePrice,
            h.Status,
            h.Slots.Select(s => new SlotView(
                s.SlotId,
                s.SlotName,
                s.StartTime,
                s.EndTime,
                date == null || !s.WeddingProjects.Any(p => p.EventDate == date && p.Status != "Closed")
            )).ToList()
        )).ToListAsync(ct);
    return Results.Ok(halls);
}).RequireAuthorization(policy => policy.RequireRole("Manager"));

api.MapGet("/projects/{id}/nrd-receipt", async (string id, WpsmDbContext db, IWebHostEnvironment environment, CancellationToken ct) =>
{
    var payment = await db.Payments.AsNoTracking().Include(p => p.Project)
        .FirstOrDefaultAsync(p => p.Project.ProjectCode == id && p.PaymentType == "NRD", ct);
    if (payment == null) return Results.NotFound();
    if (payment.ReceiptContent != null && payment.ReceiptContent.Length > 0)
    {
        var contentType = payment.ReceiptContentType ?? "application/octet-stream";
        var fileName = payment.ReceiptFileName ?? $"NRD_{id}.dat";
        return Results.File(payment.ReceiptContent, contentType, fileName);
    }
    if (!string.IsNullOrEmpty(payment.ReceiptFilePath))
    {
        var filePath = payment.ReceiptFilePath;
        if (!Path.IsPathRooted(filePath))
            filePath = Path.Combine(environment.ContentRootPath, filePath);
        if (File.Exists(filePath))
        {
            var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };
            return Results.File(filePath, contentType, Path.GetFileName(filePath));
        }
    }
    return Results.NotFound(new { message = "Không tìm thấy file biên nhận NRD." });
}).RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

api.MapGet("/addenda", async (string? projectCode, IAddendumRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    await repo.GetAddendaAsync(projectCode, UserId(user), user.FindFirstValue(ClaimTypes.Role), ct) is { } addenda ? Results.Ok(addenda) : Results.NotFound())
    .RequireAuthorization(policy => policy.RequireRole("Customer", "Manager"));

api.MapPost("/files", async (IFormFile file, [FromForm] string purpose, [FromForm] string projectCode, IFileService files, ClaimsPrincipal user, CancellationToken ct) =>
{
    await using var content = file.OpenReadStream();
    return await files.UploadAsync(new FileUploadCommand(content, file.FileName, purpose, projectCode, UserId(user)), ct) is { } stored
        ? Results.Created($"/api/wpsm/files/{stored.Id}", stored) : Results.NotFound();
}).RequireAuthorization(policy => policy.RequireRole("Manager")).DisableAntiforgery();

api.MapGet("/files/{id:guid}", async (Guid id, IFileService files, ClaimsPrincipal user, CancellationToken ct) =>
    await files.GetSignedUrlAsync(id, UserId(user), user.FindFirstValue(ClaimTypes.Role), ct) is { } url ? Results.Ok(url) : Results.NotFound())
    .RequireAuthorization();

api.MapGet("/files/{id:guid}/content", async (Guid id, long expires, string sig, IFileService files, HttpContext context, CancellationToken ct) =>
{
    if (await files.OpenSignedAsync(id, expires, sig, ct) is not { } file) return Results.StatusCode(StatusCodes.Status403Forbidden);
    context.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(file.Content, file.ContentType, file.OriginalName);
}).AllowAnonymous();

api.MapPost("/notifications/email-status", async (HttpRequest request, IEmailDeliveryService delivery, CancellationToken ct) =>
{
    if (!delivery.IsConfigured) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer, ct);
    if (buffer.Length > 64 * 1024) return Results.BadRequest();
    var body = buffer.ToArray();
    if (!delivery.VerifySignature(body, request.Headers["X-Wpsm-Signature"])) return Results.Unauthorized();
    var payload = JsonSerializer.Deserialize<EmailStatusRequest>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    if (string.IsNullOrWhiteSpace(payload?.MessageId) || string.IsNullOrWhiteSpace(payload.Status)) return Results.BadRequest();
    return await delivery.ApplyStatusAsync(payload.MessageId, payload.Status, payload.Error, ct) ? Results.NoContent() : Results.NotFound();
}).AllowAnonymous();

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
        ? Results.Ok(profile) : Results.BadRequest())
    .RequireAuthorization();

api.MapPost("/auth/login", async (LoginRequest request, IAccountProfileRepository repo, IPasswordHasher<User> hasher, HttpContext context, CancellationToken ct) =>
{
    var account = await repo.FindAccountAsync(request.Email, ct);
    if (account is null || account.Status != "Active" || VerifyPassword(hasher, account, request.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, account.UserId.ToString()),
        new Claim(ClaimTypes.Name, account.Email),
        new Claim(ClaimTypes.Role, account.Role),
        new Claim("FullName", account.FullName),
        new Claim("PasswordVersion", PasswordVersion(account.PasswordHash)),
        new Claim("MustChangePassword", account.MustChangePassword.ToString())
    };
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.Ok(new { account.UserId, account.Email, account.FullName, account.Role, account.MustChangePassword });
});

api.MapPost("/auth/logout", async (HttpContext context) => { await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return Results.NoContent(); })
    .RequireAuthorization();

api.MapGet("/auth/me", async (IAccountProfileRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
{
    var email = user.FindFirstValue(ClaimTypes.Name) ?? user.Identity?.Name;
    if (string.IsNullOrWhiteSpace(email)) return Results.Unauthorized();
    var account = await repo.FindAccountAsync(email, ct);
    return account is null || account.Status != "Active" ? Results.Unauthorized() : Results.Ok(new
    {
        authenticated = true,
        user = new { account.FullName, account.Email, Phone = account.Phone ?? "", account.Role },
        mustChangePassword = account.MustChangePassword
    });
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

// ── C-01: Customer Dashboard ─────────────────────────────────────────────────
api.MapGet("/customer/projects/{projectCode}/dashboard",
    async (string projectCode, ICustomerDashboardRepository dashboard, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var result = await dashboard.GetDashboardAsync(UserId(user), projectCode, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Customer"));

// ── C-11: Serve ExecutionPhoto by ID (Customer-owned projects only) ───────────
api.MapGet("/customer/photos/{photoId:int}",
    async (int photoId, WpsmDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var userId = UserId(user);
        var photo = await db.ExecutionPhotos
            .AsNoTracking()
            .Include(ep => ep.DayOfTask)
                .ThenInclude(dot => dot.Project)
            .FirstOrDefaultAsync(ep => ep.PhotoId == photoId, ct);

        if (photo is null) return Results.NotFound();
        if (photo.DayOfTask.Project.CustomerId != userId)
            return Results.Forbid();

        var filePath = photo.ImageFilePath;
        if (!Path.IsPathRooted(filePath))
        {
            var env = app.Services.GetRequiredService<IWebHostEnvironment>();
            filePath = Path.Combine(env.ContentRootPath, filePath);
        }

        if (!File.Exists(filePath)) return Results.NotFound();

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };

        return Results.File(filePath, contentType, enableRangeProcessing: true);
    })
    .RequireAuthorization(policy => policy.RequireRole("Customer"));

api.MapGet("/customer/dashboard",
    async (IProjectRepository projects, ICustomerDashboardRepository dashboard, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var userId = UserId(user);
        var myProjects = await projects.GetProjectsAsync(userId, "Customer", ct);
        if (myProjects.Count == 0) return Results.NotFound();
        var result = await dashboard.GetDashboardAsync(userId, myProjects[0].Id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Customer"));

// ── C-03: Customer Roadmap (UC-05) ───────────────────────────────────────────
api.MapGet("/customer/projects/{projectCode}/roadmap",
    async (string projectCode, ICustomerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var result = await repo.GetRoadmapAsync(UserId(user), projectCode, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Customer"));

api.MapPost("/customer/projects/{projectCode}/roadmap/tasks/{taskId:int}/complete",
    async (string projectCode, int taskId, ICustomerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var result = await repo.CompleteTaskAsync(UserId(user), projectCode, taskId, ct);
        if (!result.Success)
            return Results.BadRequest(new { error = result.Message });
        return Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Customer"));

api.MapGet("/customer/roadmap",
    async (IProjectRepository projects, ICustomerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var userId = UserId(user);
        var myProjects = await projects.GetProjectsAsync(userId, "Customer", ct);
        if (myProjects.Count == 0) return Results.NotFound();
        var result = await repo.GetRoadmapAsync(userId, myProjects[0].Id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Customer"));

// ── M-06: Manager Master Roadmap & Tasks (FE-03, UC-21) ───────────────────────
api.MapGet("/manager/projects/{id}/roadmap",
    async (string id, [FromQuery] string? owner, IManagerRoadmapRepository repo, WpsmDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var filter = TaskOwnerFilter.All;
        if (!string.IsNullOrWhiteSpace(owner) && !Enum.TryParse(owner, true, out filter))
            return Results.BadRequest(new { message = "Bộ lọc owner không hợp lệ. Giá trị hợp lệ: All, Manager, Customer" });

        var result = await repo.GetRoadmapAsync(UserId(user), Role(user), id, filter, ct);
        if (result is null)
        {
            var exists = await db.WeddingProjects.AnyAsync(p => p.ProjectCode == id || (int.TryParse(id, out var intId) && p.ProjectId == intId), ct);
            if (!exists) return Results.NotFound(new { message = "Dự án không tồn tại." });
            return Results.Json(new { message = "Bạn không có quyền quản lý dự án này." }, statusCode: StatusCodes.Status403Forbidden);
        }
        return Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

api.MapGet("/manager/projects/{id}/roadmap/tasks",
    async (string id, [FromQuery] string? owner, IManagerRoadmapRepository repo, WpsmDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var filter = TaskOwnerFilter.All;
        if (!string.IsNullOrWhiteSpace(owner) && !Enum.TryParse(owner, true, out filter))
            return Results.BadRequest(new { message = "Bộ lọc owner không hợp lệ. Giá trị hợp lệ: All, Manager, Customer" });

        var result = await repo.GetRoadmapTasksAsync(UserId(user), Role(user), id, filter, ct);
        if (result is null)
        {
            var exists = await db.WeddingProjects.AnyAsync(p => p.ProjectCode == id || (int.TryParse(id, out var intId) && p.ProjectId == intId), ct);
            if (!exists) return Results.NotFound(new { message = "Dự án không tồn tại." });
            return Results.Json(new { message = "Bạn không có quyền quản lý dự án này." }, statusCode: StatusCodes.Status403Forbidden);
        }
        return Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

api.MapPost("/manager/projects/{id}/roadmap/tasks/{taskId:int}/complete",
    async (string id, int taskId, CompleteManagerTaskRequest? request, IManagerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var result = await repo.CompleteTaskAsync(UserId(user), Role(user), id, taskId, request?.ProofPhotoPaths, ct);
        if (!result.Success)
        {
            if (result.Message.Contains("không tồn tại"))
                return Results.NotFound(new { message = result.Message });
            if (result.Message.Contains("không có quyền quản lý"))
                return Results.Json(new { message = result.Message }, statusCode: StatusCodes.Status403Forbidden);
            return Results.BadRequest(new { message = result.Message });
        }
        return Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

api.MapGet("/manager/roadmap",
    async ([FromQuery] string? owner, IProjectRepository projects, IManagerRoadmapRepository repo, WpsmDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var filter = TaskOwnerFilter.All;
        if (!string.IsNullOrWhiteSpace(owner) && !Enum.TryParse(owner, true, out filter))
            return Results.BadRequest(new { message = "Bộ lọc owner không hợp lệ. Giá trị hợp lệ: All, Manager, Customer" });

        var userId = UserId(user);
        var role = Role(user);
        var myProjects = await projects.GetProjectsAsync(userId, role, ct);
        if (myProjects.Count == 0) return Results.NotFound(new { message = "Chưa có dự án nào." });

        var result = await repo.GetRoadmapAsync(userId, role, myProjects[0].Id, filter, ct);
        return result is null ? Results.NotFound(new { message = "Không tìm thấy lộ trình." }) : Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

// Alias without /manager prefix
api.MapGet("/projects/{id}/roadmap",
    async (string id, [FromQuery] string? owner, IManagerRoadmapRepository repo, WpsmDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var filter = TaskOwnerFilter.All;
        if (!string.IsNullOrWhiteSpace(owner) && !Enum.TryParse(owner, true, out filter))
            return Results.BadRequest(new { message = "Bộ lọc owner không hợp lệ. Giá trị hợp lệ: All, Manager, Customer" });

        var result = await repo.GetRoadmapAsync(UserId(user), Role(user), id, filter, ct);
        if (result is null)
        {
            var exists = await db.WeddingProjects.AnyAsync(p => p.ProjectCode == id || (int.TryParse(id, out var intId) && p.ProjectId == intId), ct);
            if (!exists) return Results.NotFound(new { message = "Dự án không tồn tại." });
            return Results.Json(new { message = "Bạn không có quyền quản lý dự án này." }, statusCode: StatusCodes.Status403Forbidden);
        }
        return Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

api.MapGet("/projects/{id}/roadmap/tasks",
    async (string id, [FromQuery] string? owner, IManagerRoadmapRepository repo, WpsmDbContext db, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var filter = TaskOwnerFilter.All;
        if (!string.IsNullOrWhiteSpace(owner) && !Enum.TryParse(owner, true, out filter))
            return Results.BadRequest(new { message = "Bộ lọc owner không hợp lệ. Giá trị hợp lệ: All, Manager, Customer" });

        var result = await repo.GetRoadmapTasksAsync(UserId(user), Role(user), id, filter, ct);
        if (result is null)
        {
            var exists = await db.WeddingProjects.AnyAsync(p => p.ProjectCode == id || (int.TryParse(id, out var intId) && p.ProjectId == intId), ct);
            if (!exists) return Results.NotFound(new { message = "Dự án không tồn tại." });
            return Results.Json(new { message = "Bạn không có quyền quản lý dự án này." }, statusCode: StatusCodes.Status403Forbidden);
        }
        return Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

api.MapPost("/projects/{id}/roadmap/tasks/{taskId:int}/complete",
    async (string id, int taskId, CompleteManagerTaskRequest? request, IManagerRoadmapRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
    {
        var result = await repo.CompleteTaskAsync(UserId(user), Role(user), id, taskId, request?.ProofPhotoPaths, ct);
        if (!result.Success)
        {
            if (result.Message.Contains("không tồn tại"))
                return Results.NotFound(new { message = result.Message });
            if (result.Message.Contains("không có quyền quản lý"))
                return Results.Json(new { message = result.Message }, statusCode: StatusCodes.Status403Forbidden);
            return Results.BadRequest(new { message = result.Message });
        }
        return Results.Ok(result);
    })
    .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

app.Run();

static bool ShowsPasswordOnScreen(IWebHostEnvironment environment, EmailOptions email) =>
    environment.IsDevelopment() && string.IsNullOrWhiteSpace(email.Host);

static PasswordVerificationResult VerifyPassword(IPasswordHasher<User> hasher, AccountView account, string password)
{
    try
    {
        return hasher.VerifyHashedPassword(new User { UserId = account.UserId, Email = account.Email, Role = account.Role }, account.PasswordHash, password);
    }
    catch (FormatException)
    {
        return PasswordVerificationResult.Failed;
    }
}

static string PasswordVersion(string passwordHash) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(passwordHash)));
static int UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
    ? id : throw new InvalidOperationException("Phiên đăng nhập không hợp lệ.");

public sealed record CreateProjectRequest(string CoupleName, string CustomerEmail, string CustomerPhone, string Hall, string Slot,
    DateOnly EventDate, decimal BaseValue, decimal NrdAmount, string PaymentMethod = "Cash", string? ReceiptFilePath = null);
public sealed record CreatedProjectResponse(WeddingProjectView Project, string? TemporaryPassword, string EmailStatus = "ExistingAccount");
public sealed record EmailStatusRequest(string? MessageId, string? Status, string? Error);
public sealed record SpecificationRequest(string Summary, int GuestCount, IReadOnlyList<SpecificationLineCommand>? Items = null);
public sealed record CatalogItemRequest(string Name, string Category, string Unit, decimal BasePrice, bool Active);
public sealed record PriceFrameRequest(decimal MinPrice, decimal MaxPrice, DateOnly? EffectiveFrom = null);
public sealed record PolicyRequest(decimal NrdRatio, decimal T30Ratio, int CrPenaltyDays, decimal CrPenaltyPercent, DateOnly? EffectiveFrom = null);
public sealed record ProfileRequest(string FullName, string Phone);
public sealed record LoginRequest(string Email, string Password);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
