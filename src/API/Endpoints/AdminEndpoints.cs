using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using static API.Endpoints.EndpointHelpers;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Persistence.Entities;


namespace API.Endpoints;

// A-01, A-09..A-18: Admin dashboard, reports, photo audit, escalations, exceptions (GBR-12: Admin only).
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").RequireAuthorization(policy => policy.RequireRole("Admin"));
        admin.MapGet("/dashboard", async (IAdminRepository repo, CancellationToken ct) => Results.Ok(await repo.GetDashboardAsync(ct)));
        admin.MapGet("/reports", async (DateOnly from, DateOnly to, IAdminRepository repo, CancellationToken ct) => Results.Ok(await repo.GetReportAsync(from, to, ct)));
        // Admin oversight of every project: search/filter list and read-only detail (creator, operators, status, contract, evidence).
        admin.MapGet("/projects", async (string? search, string? status, int? hallId, int? managerId, DateOnly? from, DateOnly? to, string? sort,
            int? page, int? pageSize, IAdminProjectRepository repo, CancellationToken ct) =>
        {
            if (from is { } f && to is { } t && f > t) return Results.BadRequest(new { message = "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc." });
            return Results.Ok(await repo.SearchAsync(new AdminProjectQuery(search, status, hallId, managerId, from, to, sort, page ?? 1, pageSize ?? 20), ct));
        });
        admin.MapGet("/projects/filters", async (IAdminProjectRepository repo, CancellationToken ct) => Results.Ok(await repo.GetFilterOptionsAsync(ct)));
        admin.MapGet("/projects/{projectCode}", async (string projectCode, IAdminProjectRepository repo, PhotoLinks links, CancellationToken ct) =>
            await repo.GetDetailAsync(projectCode, ct) is { } detail
                ? Results.Ok(detail with
                {
                    Evidence = detail.Evidence.Select(t => t with { Photos = t.Photos.Select(p => p with { ImageUrl = links.Url(p.PhotoId) }).ToList() }).ToList()
                })
                : Results.NotFound(new { message = "Không tìm thấy sự kiện." }));
        // Audit log: who changed what (append-only); same filters for the list and the CSV export.
        admin.MapGet("/audit-logs", async (string? search, string? action, string? impact, string? role, int? userId, string? projectCode,
            DateOnly? from, DateOnly? to, int? page, int? pageSize, IAuditLogRepository repo, CancellationToken ct) =>
        {
            if (from is { } f && to is { } t && f > t) return Results.BadRequest(new { message = "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc." });
            return Results.Ok(await repo.SearchAsync(new AuditLogQuery(search, action, impact, role, userId, projectCode, from, to, page ?? 1, pageSize ?? 25), ct));
        });
        admin.MapGet("/audit-logs/filters", async (IAuditLogRepository repo, CancellationToken ct) => Results.Ok(await repo.GetFilterOptionsAsync(ct)));
        admin.MapGet("/audit-logs/export", async (string? search, string? action, string? impact, string? role, int? userId, string? projectCode,
            DateOnly? from, DateOnly? to, IAuditLogRepository repo, CancellationToken ct) =>
        {
            var rows = await repo.ExportAsync(new AuditLogQuery(search, action, impact, role, userId, projectCode, from, to), 10_000, ct);
            return Results.File(AuditCsv.Build(rows), "text/csv; charset=utf-8", $"audit-log_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
        });
        // A-13 photo feed removed: proof photos are reviewed per project (admin project detail, evidence tab).
        admin.MapGet("/photos/{photoId:int}", async (int photoId, IAdminRepository repo, PhotoLinks links, CancellationToken ct) =>
            await repo.GetPhotoAsync(photoId, ct) is { } photo ? Results.Ok(photo with { ImageUrl = links.Url(photo.PhotoId) }) : Results.NotFound());
        admin.MapGet("/photos/{photoId:int}/content", async (int photoId, IAdminRepository repo, IWebHostEnvironment environment, Infrastructure.Files.FileStorageOptions storage, CancellationToken ct) =>
            await repo.GetPhotoPathAsync(photoId, ct) is { } path ? PhotoFile(path, environment, storage) : Results.NotFound());
        admin.MapGet("/overdue-alerts", async (IAdminRepository repo, CancellationToken ct) => Results.Ok(await repo.GetOverdueAlertsAsync(ct)));
        admin.MapGet("/complaints", async (IAdminRepository repo, CancellationToken ct) => Results.Ok(await repo.GetEscalatedComplaintsAsync(ct)));
        admin.MapPost("/complaints/{complaintId:int}/resolve", async (int complaintId, ResolveComplaintRequest request, IAdminRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.ResolveComplaintAsync(complaintId, request.Decision, UserId(user), ct) ? Results.NoContent() : Results.NotFound());
        admin.MapGet("/exception-requests", async (IAdminRepository repo, CancellationToken ct) => Results.Ok(await repo.GetExceptionRequestsAsync(ct)));
        admin.MapPost("/exception-requests/{requestId:int}/decision", async (int requestId, ExceptionDecisionRequest request, IAdminRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.DecideExceptionAsync(requestId, request.Approve, request.Note, UserId(user), ct) ? Results.NoContent() : Results.NotFound());

        // A-19 account management: list, create (Manager/Admin), edit, lock/unlock (never delete), reissue password.
        admin.MapGet("/accounts", async (IUserAccountRepository repo, CancellationToken ct) => Results.Ok(await repo.GetAccountsAsync(ct)));
        admin.MapPost("/accounts", async (AccountRequest request, IUserAccountRepository repo, IPasswordHasher<User> hasher, EmailOptions email, IWebHostEnvironment environment, CancellationToken ct) =>
        {
            var (password, hash) = TemporaryPassword(hasher);
            var created = await repo.CreateAccountAsync(new UserAccountCommand(request.FullName, request.Email, request.Phone, request.Role), hash, password, ct);
            return Results.Created($"/api/wpsm/admin/accounts/{created.Account.Id}",
                new AccountCredentialResponse(created.Account, ShowsPasswordOnScreen(environment, email) ? password : null));
        });
        admin.MapPut("/accounts/{userId:int}", async (int userId, AccountRequest request, IUserAccountRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.UpdateAccountAsync(userId, new UserAccountCommand(request.FullName, request.Email, request.Phone, request.Role), UserId(user), ct) is { } account
                ? Results.Ok(account) : Results.NotFound());
        admin.MapPost("/accounts/{userId:int}/status", async (int userId, AccountStatusRequest request, IUserAccountRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.SetStatusAsync(userId, request.Active, UserId(user), ct) is { } account ? Results.Ok(account) : Results.NotFound());
        admin.MapPost("/accounts/{userId:int}/reset-password", async (int userId, IUserAccountRepository repo, IPasswordHasher<User> hasher, EmailOptions email, IWebHostEnvironment environment, CancellationToken ct) =>
        {
            var (password, hash) = TemporaryPassword(hasher);
            return await repo.ResetPasswordAsync(userId, hash, password, ct) is { } account
                ? Results.Ok(new AccountCredentialResponse(account, ShowsPasswordOnScreen(environment, email) ? password : null))
                : Results.NotFound();
        });
        return api;
    }

    private static (string Password, string Hash) TemporaryPassword(IPasswordHasher<User> hasher)
    {
        // Same alphabet as M-03 credentials; always contains letters and digits in practice, and must be changed at first login.
        var password = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*", 16);
        return (password, hasher.HashPassword(new User(), password));
    }
}
