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

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("WpsmDb");
builder.Services.AddDbContext<WpsmDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddDataProtection();
builder.Services.AddScoped<ProjectReadModel>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ISpecificationRoadmapRepository, SpecificationRoadmapRepository>();
builder.Services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IAccountProfileRepository, AccountProfileRepository>();
builder.Services.AddScoped<IWpsmPortalSnapshotReader, WpsmPortalSnapshotReader>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<DevelopmentDatabaseSeeder>();
builder.Services.AddScoped<CredentialEmailSender>();
builder.Services.AddHostedService<CredentialEmailDispatcher>();
builder.Services.AddScoped<ICustomerDashboardRepository, CustomerDashboardRepository>();
builder.Services.AddScoped<ICustomerRoadmapRepository, CustomerRoadmapRepository>();
builder.Services.AddScoped<IAddendumRepository, AddendumRepository>();
builder.Services.AddScoped<IChangeRequestRepository, ChangeRequestRepository>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddSingleton<PhotoLinks>();

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
    From = builder.Configuration["Smtp:From"] ?? "no-reply@wpsm.local",
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
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Wpsm:SeedDevelopmentData", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentDatabaseSeeder>().SeedAsync();
}
if (backgroundJobsEnabled)
{
    app.Services.GetRequiredService<IRecurringJobManager>()
        .AddOrUpdate<EmailDispatchJob>("job-07-email-dispatch", job => job.RunAsync(CancellationToken.None), "*/1 * * * *");
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
// Built React client (src/Client → wwwroot) is served from the same origin as the API.
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapWpsmApi();
app.MapSpaFallback();

app.Run();
