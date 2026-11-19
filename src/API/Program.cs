using System.Security.Claims;
using System.Security.Cryptography;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using API;
using API.Endpoints;
using Application.Auth;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Files;
using Infrastructure.Identity;
using Infrastructure.Jobs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Infrastructure.Persistence.Repositories;
using Domain.Enums;
using Domain.Services;
using Application.Payments;
using Infrastructure.Payments;
using Application.Dashboard;
using Application.DayOfTasks;
using Application.OverdueAlerts;
using Application.Events;
using Domain.Events;
using Infrastructure.Events;
using Infrastructure.Dashboard;
using Infrastructure.DayOfTasks;
using Infrastructure.OverdueAlerts;
using Infrastructure.Auditing;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("WpsmDb");
// Audit log: every EF save writes AuditLogs rows with the signed-in user (system for jobs).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditActor, API.HttpAuditActor>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<WpsmDbContext>((services, options) => options.UseSqlServer(connectionString)
    .AddInterceptors(services.GetRequiredService<AuditSaveChangesInterceptor>()));
builder.Services.AddDataProtection();
builder.Services.AddScoped<ProjectReadModel>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ISpecificationRepository, SpecificationRepository>();
builder.Services.AddScoped<IEventFlowRepository, EventFlowRepository>();
builder.Services.AddScoped<IAddonContractRepository, AddonContractRepository>();
builder.Services.AddScoped<ISupplierEvidenceRepository, SupplierEvidenceRepository>();
builder.Services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IAccountProfileRepository, AccountProfileRepository>();
builder.Services.AddScoped<IWpsmPortalSnapshotReader, WpsmPortalSnapshotReader>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<DevelopmentDatabaseSeeder>();
builder.Services.AddScoped<ICustomerDashboardRepository, CustomerDashboardRepository>();
builder.Services.AddScoped<ICustomerPaymentRepository, CustomerPaymentRepository>();
builder.Services.AddScoped<IManagerPaymentRepository, ManagerPaymentRepository>();
builder.Services.AddScoped<IAdminProjectRepository, AdminProjectRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IProjectManagerRepository, ProjectManagerRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IResourceCalendarRepository, ResourceCalendarRepository>();
builder.Services.AddScoped<IComplaintRatingRepository, ComplaintRatingRepository>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddSingleton<PhotoLinks>();
// Report 4 use cases added on 2026-11-05 (docs/explain/sds-gaps.md).
builder.Services.AddSingleton<IPdfGenerator, Infrastructure.Pdf.QuestPdfGenerator>();
builder.Services.AddScoped<IBeoRepository, BeoRepository>();
builder.Services.AddScoped<IDayOfRepository, DayOfRepository>();
builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<IResourceRepository, ResourceRepository>();
builder.Services.AddScoped<IConfigurationAdminRepository, ConfigurationAdminRepository>();
builder.Services.AddScoped<IUserAccountRepository, UserAccountRepository>();

// SDS III.1.1: HS256 with a 256-bit secret from User Secrets or the environment.
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Thiếu Jwt:Secret. Hãy cấu hình bằng User Secrets hoặc biến môi trường.");
    jwtSecret = RandomNumberGenerator.GetHexString(64);
}
builder.Services.AddSingleton(new JwtOptions { Secret = jwtSecret });

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
    // Brevo SMTP relay: Smtp:From must be a sender verified in Brevo (Smtp:User is the SMTP login, not a mailbox).
    // The Smtp:User fallback only suits providers where the login is the mailbox (e.g. Gmail).
    From = builder.Configuration["Smtp:From"] ?? builder.Configuration["Smtp:User"] ?? "no-reply@wpsm.local",
    PickupDirectory = builder.Configuration["Smtp:PickupDirectory"] ?? (builder.Environment.IsDevelopment() ? Path.Combine(appDataPath, "mail") : null),
    WebhookSecret = builder.Configuration["Smtp:WebhookSecret"],
    PublicBaseUrl = builder.Configuration["Wpsm:PublicBaseUrl"] ?? "http://localhost:5173"
});
builder.Services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IEmailOutbox, EmailOutbox>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IEmailDeliveryService, EmailDeliveryService>();
builder.Services.AddScoped<EmailDispatchJob>();
builder.Services.AddScoped<BeoCompilationJob>();
builder.Services.AddScoped<EventStageJob>();
builder.Services.AddScoped<IFinalSettlementService, FinalSettlementService>();
builder.Services.AddScoped<FinalSettlementJob>();
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddScoped<IDomainEventHandler<DayOfTaskOverdueEvent>, DayOfTaskOverdueEventHandler>();
builder.Services.AddScoped<ILoadDayOfTimelineService, LoadDayOfTimelineService>();
builder.Services.AddScoped<LoadDayOfTimelineJob>();
builder.Services.AddScoped<IDetectOverdueTasksService, DetectOverdueTasksService>();
builder.Services.AddScoped<DetectOverdueTasksJob>();
builder.Services.AddScoped<IManagerOverdueAlertService, ManagerOverdueAlertService>();
builder.Services.AddScoped<IManagerDashboardService, ManagerDashboardService>();
builder.Services.AddScoped<IDayOfTaskService, DayOfTaskService>();

var backgroundJobsEnabled = builder.Configuration.GetValue("Hangfire:Enabled", true) && !string.IsNullOrWhiteSpace(connectionString);
if (backgroundJobsEnabled)
{
    builder.Services.AddHangfire(config => config.UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings()
        .UseSqlServerStorage(() => new SqlConnection(connectionString)));
    builder.Services.AddHangfireServer(options => options.WorkerCount = 2);
}

// React uses JWT access tokens and an HttpOnly rotating refresh-token cookie.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = JwtOptions.Issuer,
        ValidAudience = JwtOptions.Audience,
        IssuerSigningKey = JwtTokenService.SigningKey(jwtSecret),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    // Re-express the short JWT claims with the claim types the endpoints already read.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = context =>
        {
            var token = context.Principal!;
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, token.FindFirstValue("sub") ?? ""),
                new Claim(ClaimTypes.Name, token.FindFirstValue("email") ?? ""),
                new Claim(ClaimTypes.Role, token.FindFirstValue("role") ?? ""),
                new Claim("FullName", token.FindFirstValue("name") ?? ""),
                new Claim("MustChangePassword", token.FindFirstValue(JwtTokenService.MustChangePasswordClaim) == "true" ? bool.TrueString : bool.FalseString)
            ], JwtBearerDefaults.AuthenticationScheme);
            context.Principal = new ClaimsPrincipal(identity);
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
// SDS III.1.1: at most 5 sign-in attempts per minute per client.
// Auth:SignInPermitLimit only exists so the e2e run (every suite from one IP) can raise it.
var signInPermitLimit = builder.Configuration.GetValue("Auth:SignInPermitLimit", 5);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.SignInRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = signInPermitLimit, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();
{
    // Which email transport is active, without secret values (EMAIL-SMTP): helps diagnose provider configuration.
    var mail = app.Services.GetRequiredService<EmailOptions>();
    if (!string.IsNullOrWhiteSpace(mail.Host))
        app.Logger.LogInformation("Email: SMTP {Host}:{Port} SSL={Ssl} user={User} password={HasPassword} from={From}",
            mail.Host, mail.Port, mail.EnableSsl, string.IsNullOrWhiteSpace(mail.User) ? "(none)" : mail.User,
            string.IsNullOrEmpty(mail.Password) ? "(none)" : "set", mail.From);
    else
        app.Logger.LogInformation("Email: no Smtp:Host, messages go to the pickup directory {Directory}", mail.PickupDirectory ?? "(none)");
}
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Wpsm:SeedDevelopmentData", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentDatabaseSeeder>().SeedAsync();
}
if (backgroundJobsEnabled)
{
    var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
    recurringJobs.AddOrUpdate<EmailDispatchJob>("job-07-email-dispatch", job => job.RunAsync(CancellationToken.None), "*/1 * * * *");
    // Stage 3 starts at 00:05: lock events on their lock date, remind operators past the supplier deadline.
    recurringJobs.AddOrUpdate<EventStageJob>(
        "job-01-event-stages",
        job => job.ExecuteAsync(CancellationToken.None),
        "5 0 * * *",
        new RecurringJobOptions { TimeZone = VietnamTime.VietnamTimeZone });
    recurringJobs.AddOrUpdate<BeoCompilationJob>("job-03-compile-beo-draft", job => job.RunAsync(CancellationToken.None), "0 7 * * *",
        new RecurringJobOptions { TimeZone = VietnamTime.VietnamTimeZone });
    recurringJobs.AddOrUpdate<LoadDayOfTimelineJob>(
        "job-04-load-day-of-timeline",
        job => job.ExecuteAsync(),
        "0 6 * * *",
        new RecurringJobOptions { TimeZone = VietnamTime.VietnamTimeZone });
    recurringJobs.AddOrUpdate<DetectOverdueTasksJob>(
        "job-05-detect-overdue-tasks",
        job => job.RunAsync(CancellationToken.None),
        "*/1 * * * *",
        new RecurringJobOptions { TimeZone = VietnamTime.VietnamTimeZone });
    recurringJobs.AddOrUpdate<FinalSettlementJob>(
        "job-06-final-settlement",
        job => job.RunAsync(CancellationToken.None),
        "0 1 * * *",
        new RecurringJobOptions { TimeZone = VietnamTime.VietnamTimeZone });

    // Schedules left in Hangfire storage by earlier builds (removed OperationsJobs, roadmap reminders, T-30 payment request, old e-mail id).
    // Hangfire keeps recurring jobs across restarts, so without this they would keep running next to the jobs above.
    foreach (var legacyId in new[] { "job-01-roadmap-reminders", "job-02-t30-payment", "job-03-beo-draft", "job-04-load-day-of", "job-05-overdue",
                 "JOB-07-dispatch-emails", "job-01-roadmap-milestone-reminders", "job-02-t30-payment-request" })
        recurringJobs.RemoveIfExists(legacyId);
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
// Built React client (src/Client -> wwwroot) is served from the same origin as the API.
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapWpsmApi();
app.MapSpaFallback();

// Merges have repeatedly mapped the same route twice, which only fails when the route is called (AmbiguousMatchException).
// In Development refuse to start instead, naming the duplicates.
if (app.Environment.IsDevelopment())
{
    var duplicates = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
        .SelectMany(e => (e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? ["*"])
            .Select(method => $"{method} /{e.RoutePattern.RawText?.Trim('/')}"))
        .GroupBy(route => route, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
    if (duplicates.Count > 0)
        throw new InvalidOperationException("Duplicate API routes (each must be mapped once): " + string.Join(", ", duplicates));
}

app.Run();
