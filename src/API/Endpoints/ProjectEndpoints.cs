using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Application.Persistence;
using Infrastructure.Email;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// M-02..M-05 and the portal snapshot: projects, specification, NRD receipt, halls, addenda.
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

        // M-09 / C-07: List CRs & Cost Impact for a project
        api.MapGet("/projects/{id}/change-requests", async (string id, IAddendumRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetProjectChangeRequestsAsync(id, UserId(user), user.FindFirstValue(ClaimTypes.Role), ct) is { } crs ? Results.Ok(crs) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Customer", "Manager", "Admin"));

        api.MapGet("/projects/{id}/change-requests/{crCode}/cost-impact", async (string id, string crCode, IAddendumRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GetCostImpactAsync(id, crCode, UserId(user), user.FindFirstValue(ClaimTypes.Role), ct) is { } impact ? Results.Ok(impact) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Customer", "Manager", "Admin"));

        // M-09: Approve CR Cost Impact & Generate Contract Addendum in single transaction (UC-23, UC-24, GBR-03, GBR-05)
        api.MapPost("/projects/{id}/change-requests/{crCode}/addendum", async (string id, string crCode, GenerateAddendumRequest? request, IAddendumRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.GenerateAddendumAsync(id, crCode, UserId(user), request?.SignedFileId, request?.Note, ct) is { } impact ? Results.Ok(impact) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        api.MapPut("/projects/{id}/change-requests/{crCode}/signed-addendum", async (string id, string crCode, AttachSignedAddendumRequest request, IAddendumRepository repo, ClaimsPrincipal user, CancellationToken ct) =>
            await repo.AttachSignedAddendumAsync(id, crCode, UserId(user), request.FileId, ct) is { } impact ? Results.Ok(impact) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole("Manager"));

        // M-10, M-11, M-12: BEO, Department Sheets, Freeze & Re-approve Stale BEO (UC-27, UC-28, UC-29, GBR-05)
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
            Results.Ok(new { compiled = await repo.CompileT7DraftsAsync(ct) }))
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
