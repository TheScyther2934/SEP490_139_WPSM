using System;
using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Application.Persistence;
using Application.UseCases.Settlement;
using Domain.Services;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

public static class ManagerPaymentEndpoints
{
    public static RouteGroupBuilder MapManagerPaymentEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/projects/{id}")
            .RequireAuthorization(p => p.RequireRole("Manager", "Admin"));

        // GET /api/wpsm/projects/{id}/payments
        group.MapGet("/payments", async (
            string id,
            IManagerPaymentRepository repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var overview = await repo.GetProjectPaymentsAsync(id, UserId(user), Role(user), ct);
            return overview is null
                ? Results.NotFound(new { message = "Không tìm thấy thông tin thanh toán cho sự kiện này." })
                : Results.Ok(overview);
        });

        // POST /api/wpsm/projects/{id}/payments/record
        // Multipart form with fields: paymentType, amount, paymentDate, method, receipt (file)
        group.MapPost("/payments/record", async (
            string id,
            HttpRequest httpRequest,
            RecordSettlement useCase,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (!httpRequest.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Yêu cầu phải ở định dạng multipart/form-data." });
            }

            var form = await httpRequest.ReadFormAsync(ct);
            var paymentType = form["paymentType"].ToString();
            var rawAmount = form["amount"].ToString();
            var rawPaymentDate = form["paymentDate"].ToString();
            var method = form["method"].ToString();

            if (!decimal.TryParse(rawAmount, out var amount))
            {
                return Results.BadRequest(new { message = "Số tiền thanh toán không hợp lệ." });
            }

            DateOnly paymentDate;
            if (string.IsNullOrWhiteSpace(rawPaymentDate) || !DateOnly.TryParse(rawPaymentDate, out paymentDate))
            {
                paymentDate = PaymentCalculator.GetVietnamToday();
            }

            byte[]? receiptBytes = null;
            string? receiptFileName = null;
            string? receiptContentType = null;

            var file = form.Files.GetFile("receipt");
            if (file is { Length: > 0 })
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                receiptBytes = ms.ToArray();
                receiptFileName = file.FileName;
                receiptContentType = file.ContentType;
            }

            var command = new RecordPaymentCommand(
                PaymentType: paymentType,
                Amount: amount,
                PaymentDate: paymentDate,
                Method: method,
                ReceiptBytes: receiptBytes,
                ReceiptFileName: receiptFileName,
                ReceiptContentType: receiptContentType
            );

            var result = await useCase.HandleAsync(id, UserId(user), Role(user), command, ct);
            if (!result.Success)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            return Results.Ok(result);
        }).DisableAntiforgery();

        // GET /api/wpsm/projects/{id}/settlement
        group.MapGet("/settlement", async (
            string id,
            IManagerPaymentRepository repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var calc = await repo.GetSettlementCalculationAsync(id, UserId(user), Role(user), ct);
            return calc is null
                ? Results.NotFound(new { message = "Không tìm thấy thông tin đối soát tất toán." })
                : Results.Ok(calc);
        });

        // POST /api/wpsm/projects/{id}/settlement/receipt
        // Generates receipt, marks project as Settled
        group.MapPost("/settlement/receipt", async (
            string id,
            IssueSettlementReceipt useCase,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            // Rule violations (event not held, money still due) answer 400 through the error middleware.
            var receipt = await useCase.HandleAsync(id, UserId(user), Role(user), ct);
            return receipt is null
                ? Results.NotFound(new { message = "Không tìm thấy sự kiện." })
                : Results.Ok(receipt);
        });

        // GET /api/wpsm/projects/{id}/settlement/receipt
        // Downloads or previews the final settlement receipt PDF
        group.MapGet("/settlement/receipt", async (
            string id,
            IManagerPaymentRepository repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var file = await repo.GetSettlementReceiptFileAsync(id, UserId(user), Role(user), ct);
            return file is null
                ? Results.NotFound(new { message = "Biên nhận tất toán chưa được phát hành hoặc không tồn tại." })
                : Results.File(file.Content, file.ContentType, file.FileName);
        });

        // GET /api/wpsm/projects/{id}/payments/{paymentType}/receipt
        group.MapGet("/payments/{paymentType}/receipt", async (
            string id,
            string paymentType,
            IManagerPaymentRepository repo,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var file = await repo.GetPaymentReceiptFileAsync(id, paymentType, UserId(user), Role(user), ct);
            return file is null
                ? Results.NotFound(new { message = $"Không tìm thấy chứng từ cho đợt {paymentType}." })
                : Results.File(file.Content, file.ContentType, file.FileName);
        });

        return api;
    }
}
