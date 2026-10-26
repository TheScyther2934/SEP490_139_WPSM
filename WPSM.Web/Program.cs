var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient("WpsmApi", client =>
{
    var apiBaseUrl = builder.Configuration["WpsmApi:BaseUrl"] ?? "http://localhost:5128/";
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(5);
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

app.MapMethods("/api/wpsm/{**path}", ["GET", "POST", "PUT", "DELETE"], async (
    HttpContext context, IHttpClientFactory clients, string? path) =>
{
    var apiPath = $"api/wpsm/{path ?? string.Empty}{context.Request.QueryString}";
    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), apiPath);
    if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        request.Content = new StreamContent(context.Request.Body);
        if (context.Request.ContentType is not null)
            request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
    }

    using var response = await clients.CreateClient("WpsmApi").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    context.Response.StatusCode = (int)response.StatusCode;
    if (response.Content.Headers.ContentType is not null)
        context.Response.ContentType = response.Content.Headers.ContentType.ToString();
    await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
});

app.MapRazorPages();
app.MapGet("/A03", () => Results.Redirect("/A02"));
app.MapGet("/A05", () => Results.Redirect("/A04"));
app.MapGet("/A07", () => Results.Redirect("/A06"));
app.MapGet("/A08", () => Results.Redirect("/A06"));
app.MapGet("/C04", () => Results.Redirect("/C03"));

app.Run();
