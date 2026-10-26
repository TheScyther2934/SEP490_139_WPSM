using WPSM.API;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<WpsmPortalStore>();

var app = builder.Build();
app.UseHttpsRedirection();

var api = app.MapGroup("/api/wpsm");
api.MapGet("/bootstrap", (WpsmPortalStore store) => store.GetBootstrap());
api.MapGet("/projects", (WpsmPortalStore store) => store.Projects);
api.MapGet("/projects/{id}", (string id, WpsmPortalStore store) =>
    store.Projects.FirstOrDefault(project => project.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is { } project
        ? Results.Ok(project) : Results.NotFound());
api.MapPost("/projects", (CreateProjectRequest request, WpsmPortalStore store) =>
    Results.Created($"/api/wpsm/projects/{store.CreateProject(request).Id}", store.Projects.Last()));
api.MapPut("/projects/{id}/specification", (string id, SpecificationRequest request, WpsmPortalStore store) =>
    store.UpdateSpecification(id, request) is { } project ? Results.Ok(project) : Results.NotFound());
api.MapGet("/catalog", (WpsmPortalStore store) => store.Catalog);
api.MapPost("/catalog", (CatalogItemRequest request, WpsmPortalStore store) => Results.Ok(store.AddCatalogItem(request)));
api.MapGet("/price-frames", (WpsmPortalStore store) => store.PriceFrames);
api.MapPut("/price-frames/{id}", (string id, PriceFrameRequest request, WpsmPortalStore store) =>
    store.UpdatePriceFrame(id, request) is { } frame ? Results.Ok(frame) : Results.NotFound());
api.MapGet("/policies", (WpsmPortalStore store) => store.Policies);
api.MapPut("/policies", (PolicyRequest request, WpsmPortalStore store) => Results.Ok(store.UpdatePolicy(request)));
api.MapGet("/profile", (WpsmPortalStore store) => store.Profile);
api.MapPut("/profile", (ProfileRequest request, WpsmPortalStore store) => Results.Ok(store.UpdateProfile(request)));
api.MapPost("/auth/login", (LoginRequest request, WpsmPortalStore store) =>
    request.Email.Contains('@') && request.Password.Length >= 1
        ? Results.Ok(new { authenticated = true, user = store.Profile with { Role = request.Email.Contains("dieuhanh", StringComparison.OrdinalIgnoreCase) || request.Email.Contains("admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : request.Email.Contains("manager", StringComparison.OrdinalIgnoreCase) ? "Manager" : "Customer" } })
        : Results.Unauthorized());
api.MapPost("/auth/change-password", (ChangePasswordRequest request) =>
    request.NewPassword.Length >= 8 && request.NewPassword == request.ConfirmPassword
        ? Results.NoContent() : Results.BadRequest(new { message = "Mật khẩu mới phải dài ít nhất 8 ký tự và hai lần nhập phải trùng nhau." }));

app.Run();
