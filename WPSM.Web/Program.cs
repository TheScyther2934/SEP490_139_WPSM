using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient("WpsmApi", client =>
{
    var apiBaseUrl = builder.Configuration["WpsmApi:BaseUrl"] ?? "http://localhost:5128/";
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // The handler is shared by all users; a cookie container here would leak one user's session to everyone.
    UseCookies = false,
    AllowAutoRedirect = false
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.Use(async (context, next) =>
{
    var path = (context.Request.Path.Value ?? string.Empty).TrimEnd('/').ToUpperInvariant();
    var requiredRole = path switch
    {
        "/A02" or "/A03" or "/A04" or "/A05" or "/A06" or "/A07" or "/A08" => "Admin",
        "/M02" or "/M03" or "/M04" or "/M05" => "Manager",
        "/C01" or "/C02" or "/C03" or "/C04" => "Customer",
        _ => null
    };
    var protectedPath = requiredRole is not null || path is "/G02" or "/G03";
    if (!protectedPath) { await next(); return; }

    using var request = new HttpRequestMessage(HttpMethod.Get, "api/wpsm/auth/me");
    if (context.Request.Headers.Cookie.Count > 0)
        request.Headers.TryAddWithoutValidation("Cookie", context.Request.Headers.Cookie.ToArray());
    using var response = await context.RequestServices.GetRequiredService<IHttpClientFactory>()
        .CreateClient("WpsmApi").SendAsync(request, context.RequestAborted);
    if (!response.IsSuccessStatusCode)
    {
        context.Response.Redirect("/G01");
        return;
    }

    var session = await response.Content.ReadFromJsonAsync<WpsmSession>(cancellationToken: context.RequestAborted);
    if (session is null) { context.Response.Redirect("/G01"); return; }
    if (requiredRole is not null && !string.Equals(requiredRole, session.User.Role, StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    if (session.MustChangePassword && !path.Equals("/G03", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/G03");
        return;
    }
    await next();
});

app.MapMethods("/api/wpsm/{**path}", ["GET", "POST", "PUT", "DELETE"], async (
    HttpContext context, IHttpClientFactory clients, string? path) =>
{
    var apiPath = $"api/wpsm/{path ?? string.Empty}{context.Request.QueryString}";
    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), apiPath);
    if (context.Request.Headers.Cookie.Count > 0)
        request.Headers.TryAddWithoutValidation("Cookie", context.Request.Headers.Cookie.ToArray());
    if (context.Request.Headers.TryGetValue("X-Wpsm-Signature", out var webhookSignature))
        request.Headers.TryAddWithoutValidation("X-Wpsm-Signature", webhookSignature.ToArray());
    if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        request.Content = new StreamContent(context.Request.Body);
        if (context.Request.ContentType is not null)
            request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
    }

    using var response = await clients.CreateClient("WpsmApi").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    context.Response.StatusCode = (int)response.StatusCode;
    if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        foreach (var cookie in cookies) context.Response.Headers.Append("Set-Cookie", cookie);
    if (response.Headers.TryGetValues("WWW-Authenticate", out var challenges))
        foreach (var challenge in challenges) context.Response.Headers.Append("WWW-Authenticate", challenge);
    if (response.Content.Headers.ContentType is not null)
        context.Response.ContentType = response.Content.Headers.ContentType.ToString();
    if (response.Content.Headers.ContentDisposition is not null)
        context.Response.Headers.ContentDisposition = response.Content.Headers.ContentDisposition.ToString();
    if (response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions))
        context.Response.Headers.XContentTypeOptions = contentTypeOptions.ToArray();
    await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
});

app.MapRazorPages();
app.MapGet("/A03", () => Results.Redirect("/A02"));
app.MapGet("/A05", () => Results.Redirect("/A04"));
app.MapGet("/A07", () => Results.Redirect("/A06"));
app.MapGet("/A08", () => Results.Redirect("/A06"));
app.MapGet("/C04", () => Results.Redirect("/C03"));

app.Run();

public sealed record WpsmSession(bool Authenticated, WpsmSessionUser User, bool MustChangePassword);
public sealed record WpsmSessionUser(string FullName, string Email, string Phone, string Role);
