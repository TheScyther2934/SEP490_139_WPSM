# SEP490_139_WPSM
Wedding Planning and Service Management

Team onboarding and development workflow: [Dev guide/WPSM_Developer_Guide.md](Dev%20guide/WPSM_Developer_Guide.md).

## WPSM screen prototype

The Razor UI ports the requested screens. Paired screen IDs share one page: A02/A03, A04/A05, A06/A07/A08, and C03/C04.

| Role | Routes |
|---|---|
| General | `/G01`, `/G02`, `/G03` |
| Admin | `/A02`, `/A04`, `/A06` |
| Manager | `/M02`, `/M03`, `/M04`, `/M05` |
| Customer | `/C01`, `/C02`, `/C03` |

Run the API and web app in separate terminals with the `http` launch profile:

```powershell
dotnet run --project src/API/WPSM.API.csproj --launch-profile http
dotnet run --project WPSM.Web/WPSM.Web.csproj --launch-profile http
```

Then open `http://localhost:5231/G01`. The Web app proxies `/api/wpsm/*` to the API at `http://localhost:5128`, so browser requests stay same-origin. The API uses SQL Server through the database-first `WpsmDbContext` in `src/Infrastructure`. Persistence contracts are feature-scoped in `src/Application/Persistence`: `IProjectRepository`, `ISpecificationRoadmapRepository`, `IConfigurationRepository`, and `IAccountProfileRepository`. Their EF implementations live in `src/Infrastructure/Persistence/Repositories`; `IWpsmPortalSnapshotReader` composes the data needed by the Razor screens' bootstrap endpoint. API endpoints should depend on the feature contract they use, not on a catch-all repository or `DbContext`.

## Local database setup (Iteration 1)

1. In SQL Server Management Studio, run `Database/WPSM_DB_Schema.sql` against the local SQL Server instance. The development connection defaults to `(localdb)\MSSQLLocalDB`; change `ConnectionStrings:WpsmDb` with User Secrets if you use SQL Express or another instance.
2. Set development-only accounts with User Secrets. These secrets create an Admin and Manager on first API start and are not stored in source control:

```powershell
dotnet user-secrets set "Wpsm:SeedDevelopmentData" "true" --project src/API/WPSM.API.csproj
dotnet user-secrets set "Wpsm:Seed:Admin:Email" "admin@wpsm.local" --project src/API/WPSM.API.csproj
dotnet user-secrets set "Wpsm:Seed:Admin:Password" "<your-admin-password-12-chars-or-more>" --project src/API/WPSM.API.csproj
dotnet user-secrets set "Wpsm:Seed:Manager:Email" "manager@wpsm.local" --project src/API/WPSM.API.csproj
dotnet user-secrets set "Wpsm:Seed:Manager:Password" "<your-manager-password-12-chars-or-more>" --project src/API/WPSM.API.csproj
```

3. Start the API and Web projects using the commands above. The opt-in development seeder creates halls, lunch/evening slots, baseline Catalog/PriceFrames and initial policy records. Creating a project creates a Customer account; its temporary password is returned once to the Manager for delivery.

The NRD policy is enforced at project creation; bank transfers require a PDF/JPG/PNG receipt (maximum 10 MB), which is stored privately under the API's `App_Data/receipts` directory and linked to the payment record. Cash payments are recorded as paid at creation. Customer passwords are generated once and displayed to the Manager for secure delivery; email dispatch is not integrated yet. The prototype's late-penalty rule is also not represented by the current schema/service. New projects start with editable v1 specification rows from the five required Catalog categories, and freezing a baseline generates its roadmap.

## Files, email and background jobs (API-01..04, JOB-07)

Re-run `Database/WPSM_DB_Schema.sql` on existing databases; it adds `StoredFiles` and `EmailOutbox`. Hangfire creates its own `HangFire` schema when WPSM.API starts.

Set these with User Secrets or environment variables on WPSM.API:

| Key | Purpose |
| --- | --- |
| `Files:SigningKey` | Signs file URLs (valid 5 minutes). Required outside Development. |
| `Files:RootPath` | Private file folder. Default: `App_Data/files`. |
| `Smtp:Host`, `Smtp:Port`, `Smtp:User`, `Smtp:Password`, `Smtp:EnableSsl`, `Smtp:From` | SMTP server. In Development without `Smtp:Host`, emails are written as `.eml` files to `App_Data/mail`. |
| `Smtp:WebhookSecret` | HMAC-SHA256 secret for `POST /api/wpsm/notifications/email-status`, sent as the hex `X-Wpsm-Signature` header. The webhook returns 503 until it is set. |
| `Wpsm:PublicBaseUrl` | Site address used in email links. |
| `Hangfire:Enabled` | Set to `false` to stop background jobs. |
