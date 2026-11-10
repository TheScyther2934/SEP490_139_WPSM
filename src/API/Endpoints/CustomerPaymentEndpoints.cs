using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

public static class CustomerPaymentEndpoints
{
    public static RouteGroupBuilder MapCustomerPaymentEndpoints(this RouteGroupBuilder api)
    {
        // All endpoints: Customer-only, resolve user from JWT claims only.
        var group = api.MapGroup("/customer/payments")
            .RequireAuthorization(p => p.RequireRole("Customer"));

        // GET /api/wpsm/customer/payments
        // Full overview: summary + milestones + final receipt info.
        group.MapGet("/", async (
            ICustomerPaymentRepository repo,
            System.Security.Claims.ClaimsPrincipal user,
            string? projectCode,
            CancellationToken ct) =>
        {
            var customerId = UserId(user);
            var overview = await repo.GetPaymentsOverviewAsync(customerId, projectCode, ct);
            return overview is null
                ? Results.NotFound(new { message = "Tai khoan hien chua co ho so hop dong." })
                : Results.Ok(overview);
        });

        // GET /api/wpsm/customer/payments/summary
        group.MapGet("/summary", async (
            ICustomerPaymentRepository repo,
            System.Security.Claims.ClaimsPrincipal user,
            string? projectCode,
            CancellationToken ct) =>
        {
            var customerId = UserId(user);
            var summary = await repo.GetPaymentSummaryAsync(customerId, projectCode, ct);
            return summary is null
                ? Results.NotFound(new { message = "Tai khoan hien chua co ho so hop dong." })
                : Results.Ok(summary);
        });

        // GET /api/wpsm/customer/payments/milestones
        group.MapGet("/milestones", async (
            ICustomerPaymentRepository repo,
            System.Security.Claims.ClaimsPrincipal user,
            string? projectCode,
            CancellationToken ct) =>
        {
            var customerId = UserId(user);
            var milestones = await repo.GetPaymentMilestonesAsync(customerId, projectCode, ct);
            return milestones is null
                ? Results.NotFound(new { message = "Tai khoan hien chua co ho so hop dong." })
                : Results.Ok(milestones);
        });

        // GET /api/wpsm/customer/payments/{paymentType}/receipt
        // paymentType: NRD | T30 | FINAL
        // Returns the file bytes directly (PDF / image).
        group.MapGet("/{paymentType}/receipt", async (
            string paymentType,
            ICustomerPaymentRepository repo,
            System.Security.Claims.ClaimsPrincipal user,
            string? projectCode,
            CancellationToken ct) =>
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NRD", "T30", "FINAL" };
            if (!allowed.Contains(paymentType))
                return Results.BadRequest(new { message = "paymentType phai la NRD, T30 hoac FINAL." });

            var customerId = UserId(user);
            var file = await repo.GetPaymentReceiptAsync(customerId, paymentId: null, paymentType: paymentType, projectCode: projectCode, cancellationToken: ct);
            if (file is null)
                return Results.NotFound(new { message = "Khong tim thay bien nhan cho giao dich nay." });

            return Results.File(file.Content, file.ContentType, file.FileName);
        });

        // GET /api/wpsm/customer/payments/final-receipt
        // Only available when project status == Settled.
        group.MapGet("/final-receipt", async (
            ICustomerPaymentRepository repo,
            System.Security.Claims.ClaimsPrincipal user,
            string? projectCode,
            CancellationToken ct) =>
        {
            var customerId = UserId(user);
            var file = await repo.GetFinalSettlementReceiptAsync(customerId, projectCode, ct);
            if (file is null)
                return Results.NotFound(new { message = "Bien nhan quyet toan chua co san hoac du an chua o trang thai Settled." });

            return Results.File(file.Content, file.ContentType, file.FileName);
        });

        return api;
    }
}
