using System.Security.Claims;
using Application.Persistence;
using static API.Endpoints.EndpointHelpers;

namespace API.Endpoints;

// Entry point for every /api/wpsm endpoint; each feature group lives in its own *Endpoints.cs file.
public static class WpsmApiEndpoints
{
    public static RouteGroupBuilder MapWpsmApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/wpsm");
        api.AddEndpointFilter(async (invocation, next) =>
        {
            var context = invocation.HttpContext;
            var path = context.Request.Path.Value ?? "";
            if (context.User.Identity?.IsAuthenticated == true)
            {
                if (context.User.FindFirstValue("MustChangePassword") == bool.TrueString &&
                    path is not ("/api/wpsm/auth/change-password" or "/api/wpsm/profile"))
                    return Results.Json(new { message = "Vui lòng đổi mật khẩu trước khi sử dụng hệ thống." }, statusCode: 403);
                if (path.StartsWith("/api/wpsm/projects/", StringComparison.OrdinalIgnoreCase) &&
                    context.Request.RouteValues["id"] is string code &&
                    !await context.RequestServices.GetRequiredService<IProjectRepository>().CanAccessAsync(code, UserId(context.User),
                        context.User.FindFirstValue(ClaimTypes.Role) ?? "", context.RequestAborted))
                    return Results.NotFound(new { message = "Không tìm thấy dự án trong phạm vi tài khoản." });
            }
            return await next(invocation);
        });

        api.MapProjectEndpoints();
        api.MapFileEndpoints();
        api.MapConfigurationEndpoints();
        api.MapAdminEndpoints();
        api.MapAuthEndpoints();
        api.MapCustomerEndpoints();
        api.MapChangeRequestEndpoints();
        return api;
    }

    // React Router owns every non-API path; unknown /api paths stay 404.
    public static WebApplication MapSpaFallback(this WebApplication app)
    {
        app.MapFallback((HttpContext context, IWebHostEnvironment environment) =>
        {
            if (context.Request.Path.StartsWithSegments("/api")) return Results.NotFound();
            var index = environment.WebRootFileProvider.GetFileInfo("index.html");
            return index.Exists && index.PhysicalPath is { } path ? Results.File(path, "text/html; charset=utf-8") : Results.NotFound();
        });
        return app;
    }
}
