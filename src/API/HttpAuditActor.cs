using System.Security.Claims;
using Infrastructure.Auditing;

namespace API;

/// <summary>The signed-in user of the current request; outside a request (Hangfire jobs, seeding) it is the system.</summary>
public sealed class HttpAuditActor(IHttpContextAccessor accessor) : IAuditActor
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;

    public int? UserId => int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string? Name => User is null ? "Hệ thống" : User.FindFirstValue("FullName") is { Length: > 0 } name ? name : User.FindFirstValue(ClaimTypes.Name);
    public string? Email => User?.FindFirstValue(ClaimTypes.Name);
    public string? Role => User is null ? "System" : User.FindFirstValue(ClaimTypes.Role);
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
