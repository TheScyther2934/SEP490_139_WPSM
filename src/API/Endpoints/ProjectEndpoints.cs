using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// Events: list, create (stage 1), stage 2 service adjustments, deposit receipt, halls, coordination sheet.
public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/bootstrap", async (string? projectCode, IWpsmPortalSnapshotReader repo, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await repo.GetSnapshotAsync(UserId(user), projectCode, ct))).RequireAuthorization();

        api.MapGet("/projects", async (IProjectRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await repo.GetProjectsAsync(UserId(user), user.FindFirstValue(ClaimTypes.Role), ct)))
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        api.MapGet("/projects/{id}", async (string id, IProjectRepository repo, CancellationToken ct) =>
            await repo.GetProjectAsync(id, ct) is { } project ? Results.Ok(project) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        api.MapPost("/projects", async (CreateProjectRequest request, IProjectRepository repo, IAccountProfileRepository accounts, IPasswordHasher<User> hasher, EmailOptions email, IWebHostEnvironment environment, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var temporaryPassword = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*", 16);
            var newCustomer = await accounts.FindAccountAsync(request.CustomerEmail, ct) is null;
            var passwordHash = hasher.HashPassword(new User { Email = request.CustomerEmail, Role = "Customer" }, temporaryPassword);
            var created = await repo.CreateProjectAsync(new CreateProjectCommand(request.CoupleName, request.CustomerEmail, request.CustomerPhone,
                request.Hall, request.Slot, request.EventDate, request.BaseValue, request.DepositAmount, request.PaymentMethod, request.ReceiptFilePath, request.Items),
                UserId(user), passwordHash, temporaryPassword, ct);
            // Without SMTP the credentials email is only saved as a file: say so, the Manager hands the password over.
            var emailStatus = created.TemporaryPassword is null ? "ExistingAccount" : string.IsNullOrWhiteSpace(email.Host) ? "NotConfigured" : "Pending";
            // The password travels only by email once SMTP is configured; the screen shows it only in Development without SMTP.
            var shownPassword = newCustomer && ShowsPasswordOnScreen(environment, email) ? created.TemporaryPassword : null;
            return Results.Created($"/api/wpsm/projects/{created.Project.Id}", new CreatedProjectResponse(created.Project, shownPassword, emailStatus));
        }).RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapPost("/projects/{id}/resend-credentials", async (string id, IProjectRepository repo, IPasswordHasher<User> hasher, EmailOptions email, IWebHostEnvironment environment, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var temporaryPassword = RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*", 16);
            var passwordHash = hasher.HashPassword(new User { Role = "Customer" }, temporaryPassword);
            var sent = await repo.ResendCredentialsAsync(id, UserId(user), passwordHash, temporaryPassword, ct);
            if (!sent) return Results.NotFound();
            return Results.Ok(new { temporaryPassword = ShowsPasswordOnScreen(environment, email) ? temporaryPassword : null, emailQueued = true });
        }).RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapPut("/projects/{id}/specification", async (string id, SpecificationRequest request, ISpecificationRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.UpdateSpecificationAsync(id, UserId(user), new SpecificationCommand(request.Summary, request.GuestCount, request.Items, request.Note, request.NoteType), ct) is { } project
                ? Results.Ok(project) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapPost("/projects/{id}/deposit-receipt", async (string id, IFormFile receipt, IProjectRepository repo, IFileService files, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (receipt.Length is <= 0 or > 10 * 1024 * 1024)
                return Results.BadRequest(new { message = "Chứng từ không được trống và tối đa 10 MB." });
            // Stored like every file (type checked by content, phone photos converted); the payment keeps the storage key.
            await using var content = receipt.OpenReadStream();
            var stored = await files.UploadAsync(new FileUploadCommand(content, receipt.FileName, "DepositReceipt", id, UserId(user)), ct);
            if (stored is null || !await repo.RecordDepositReceiptAsync(id, UserId(user), stored, ct)) return Results.NotFound();
            return Results.Ok(new { message = "Đã lưu biên nhận tiền đặt cọc.", paymentStatus = "Paid" });
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
                        date == null || !s.WeddingProjects.Any(p => p.EventDate == date && p.Status != "Cancelled")
                    )).ToList()
                )).ToListAsync(ct);
            return Results.Ok(halls);
        }).RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapGet("/projects/{id}/deposit-receipt", async (string id, WpsmDbContext db, IProjectRepository repo, IFileService files, ClaimsPrincipal user, CancellationToken ct) =>
        {
            // Only the event's operators (and Admin) may read it.
            if (!await repo.CanAccessAsync(id, UserId(user), user.FindFirstValue(ClaimTypes.Role) ?? "", ct)) return Results.NotFound();
            var payment = await db.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Project.ProjectCode == id && p.PaymentType == "Deposit", ct);
            if (payment?.ReceiptFilePath is { Length: > 0 } key && await files.ReadAsync(key, ct) is { } content)
                return Results.File(content, payment.ReceiptContentType ?? "application/octet-stream", payment.ReceiptFileName ?? $"Dat_coc_{id}");
            return Results.NotFound(new { message = "Không tìm thấy biên nhận tiền đặt cọc." });
        }).RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        // Coordination sheet (BEO) and department sheets: frozen once the event is locked, re-approved after a confirmed addon.
        api.MapGet("/projects/{id}/beo", async (string id, IBeoRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetBeoAsync(id, UserId(user), ct) is { } beo ? Results.Ok(beo) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        api.MapPut("/projects/{id}/beo/draft", async (string id, SaveBeoRequest request, IBeoRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.SaveDraftBeoAsync(id, UserId(user), request.Notes, ct) is { } beo ? Results.Ok(beo) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapPost("/projects/{id}/beo/freeze", async (string id, SaveBeoRequest? request, IBeoRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.FreezeOrReapproveBeoAsync(id, UserId(user), request?.Notes, ct) is { } beo ? Results.Ok(beo) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapPost("/projects/{id}/beo/department-sheets", async (string id, IBeoRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GenerateDepartmentSheetsAsync(id, UserId(user), ct) is { } beo ? Results.Ok(beo) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapGet("/projects/{id}/beo/department-sheets/{department}/pdf", async (string id, string department, IBeoRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.ExportDepartmentSheetAsync(id, department, UserId(user), ct) is { } pdf
                ? Results.File(pdf.Content, pdf.ContentType, pdf.FileName)
                : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        api.MapPost("/projects/beo/compile-t7", async (IBeoRepository repo, CancellationToken ct) =>
            Results.Ok(new { compiled = await repo.CompileLockedDraftsAsync(ct) }))
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Admin"));

        // M-18, M-19, M-20: Manager Complaints List, Detail & Respond / Escalate (UC-35, UC-36, BF-08)
        api.MapGet("/manager/complaints", async (string? projectCode, IComplaintRatingRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            Results.Ok(await repo.GetManagerComplaintsAsync(UserId(user), projectCode, ct)))
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapGet("/manager/complaints/{complaintCode}", async (string complaintCode, IComplaintRatingRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetManagerComplaintAsync(UserId(user), complaintCode, ct) is { } item ? Results.Ok(item) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapPost("/manager/complaints/{complaintCode}/action", async (string complaintCode, ManagerComplaintActionRequest request, IComplaintRatingRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.RespondOrEscalateComplaintAsync(UserId(user), complaintCode, request.Action, request.Note, ct) is { } item ? Results.Ok(item) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        return api;
    }
}
