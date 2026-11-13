# SEP490_139_WPSM
Wedding Planning and Service Management

Team onboarding and development workflow: [Dev guide/WPSM_Developer_Guide.md](Dev%20guide/WPSM_Developer_Guide.md).

## Chạy ứng dụng

Giao diện hiện tại là React trong `src/Client` (Vite); API ASP.NET Core chạy riêng. Cần Node.js/npm, .NET 8 SDK và SQL Server LocalDB hoặc SQL Server.

Trước lần chạy đầu, tạo database theo mục **Local database setup** bên dưới. Sau đó mở hai terminal tại thư mục solution:

Terminal 1 – API:

```powershell
dotnet run --project src/API/API.csproj --launch-profile http
```

Terminal 2 – React:

```powershell
cd src/Client
npm ci
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api/wpsm/*` to the API at `http://localhost:5128`. For a production build, run `npm run build` from `src/Client`; the built React app is written to `src/API/wwwroot` and served by the API. The API uses SQL Server through the database-first `WpsmDbContext` in `src/Infrastructure`. Persistence contracts are feature-scoped in `src/Application/Persistence`: `IProjectRepository`, `ISpecificationRoadmapRepository`, `IConfigurationRepository`, and `IAccountProfileRepository`. Their EF implementations live in `src/Infrastructure/Persistence/Repositories`; `IWpsmPortalSnapshotReader` composes data for the React bootstrap endpoint. API endpoints should depend on the feature contract they use, not on a catch-all repository or `DbContext`.

## Local database setup (Iteration 1)

1. In SQL Server Management Studio, run `Database/WPSM_DB_Schema.sql` against the local SQL Server instance. The development connection defaults to `(localdb)\MSSQLLocalDB`; change `ConnectionStrings:WpsmDb` with User Secrets if you use SQL Express or another instance.
2. Set development-only accounts with User Secrets. These secrets create an Admin and Manager on first API start and are not stored in source control:

```powershell
dotnet user-secrets set "Wpsm:SeedDevelopmentData" "true" --project src/API/API.csproj
dotnet user-secrets set "Wpsm:Seed:Admin:Email" "admin@wpsm.local" --project src/API/API.csproj
dotnet user-secrets set "Wpsm:Seed:Admin:Password" "<your-admin-password-12-chars-or-more>" --project src/API/API.csproj
dotnet user-secrets set "Wpsm:Seed:Manager:Email" "manager@wpsm.local" --project src/API/API.csproj
dotnet user-secrets set "Wpsm:Seed:Manager:Password" "<your-manager-password-12-chars-or-more>" --project src/API/API.csproj
```

3. Start the API and React client using the commands above. The opt-in development seeder creates halls, lunch/evening slots, baseline Catalog/PriceFrames and initial policy records. Creating a project creates a Customer account; its temporary password is returned once to the Manager for delivery.

### SQL INSERT demo data (alternative to the API seeder)

On an empty development database, run `Database/WPSM_DB_Schema.sql` followed by `Database/WPSM_DB_Seed.sql` in SSMS. The seed script contains explicit INSERT statements for 9 demo users, 4 halls, 8 slots, 5 services, price frames/policies, 7 projects, 35 specification rows, 7 NRD payments and 28 roadmap tasks. It can be run twice without duplicates and does not overwrite existing accounts or projects. If the same sample project codes already belong to other accounts, it rolls back instead of modifying them.

The SQL demo uses `demo.admin@example.invalid`, `demo.manager@example.invalid` and `demo.customer1@example.invalid` through `demo.customer7@example.invalid`. Their initial demo-only password is `WpsmDemo@2026!`, stored as an ASP.NET Identity hash; first login requires a password change. No live SQL login, real account hash, receipt or refresh token is exported. Set `Wpsm:SeedDevelopmentData=false` when using this SQL demo; choose either SQL demo data or the API development seeder.

For an existing database receiving the merge-all features, run `Database/Updates/20261103_Integration.sql` after the portal persistence update. This adds integration tables and baseline protection without deleting existing data.

For the FE-07 Resource Calendar (M-21, M-22), run `Database/Updates/20261109_ResourceCalendar.sql` against an existing database: it adds the `PendingException` allocation state (an over-capacity allocation waiting for the Admin decision) and inserts the demo resource catalog (audio, follow lights, decor frames, technicians, kitchen) when those rows are missing. Fresh databases created from `Database/WPSM_DB_Schema.sql` already allow the state; the development seeder creates the same resources and one allocated demo event.

The NRD policy is enforced at project creation. Bank transfers require a PDF/JPG/PNG receipt (maximum 10 MB); its content and metadata are stored in SQL Server's `Payments` table and downloads require project access. Cash payments are recorded as paid at creation. New projects start with editable v1 specification rows from the five required Catalog categories, and freezing a baseline generates its roadmap.

For an existing SQL Server database, run `Database/Updates/20261102_PortalPersistence.sql` against that database before starting the updated API. It adds receipt content, email delivery storage and compatibility columns without deleting existing data. Use the same SQL Server instance in SSMS and `ConnectionStrings:WpsmDb`; a named instance such as `MACHINE\INSTANCE` is different from `localhost` or LocalDB. Keep SQL passwords in User Secrets, not in tracked configuration.

For an existing database used by React JWT sign-in, also run `Database/Updates/20261104_RefreshTokens.sql`. It creates `dbo.RefreshTokens`; the API stores only token hashes there. New databases created from `Database/WPSM_DB_Schema.sql` already include this table.

Customer credential emails are queued transactionally in `EmailDeliveries`. The temporary credential payload is protected with ASP.NET Core Data Protection, cleared after successful delivery, and never written to application logs. A worker checks the queue every minute, with up to three retries at five-minute intervals. If SMTP is not configured, delivery stays `Pending`; the UI does not claim that an email was sent. Configure these User Secrets to enable delivery:

```text
Wpsm:Email:Host
Wpsm:Email:Port            (default 587)
Wpsm:Email:EnableSsl       (default true)
Wpsm:Email:From
Wpsm:Email:Username
Wpsm:Email:Password
Wpsm:PortalUrl            (public login URL)
```

Preserve the API's Data Protection key store across restarts so queued credential messages can still be decrypted. For multiple deployed API instances, use a shared key store and a single email dispatcher instance.

Validation: `pwsh -File tests/portal.integration.ps1` exercises API persistence, authentication, project isolation and receipt downloads using a disposable `WPSM_Test_<guid>` database on SQL Server `localhost` (Windows Authentication). `pwsh -File tests/fe05.change-request.ps1` covers the FE-05 Change Request flow (C-05, C-06, M-07, M-08) on a disposable LocalDB database: PriceFrame validation, T-day penalty, counter-proposal, Admin penalty waiver through A-18, mutual approval and addendum generation. `pwsh -File tests/fe07.resource-calendar.ps1` covers the FE-07 Resource Calendar (M-21, M-22): hall/resource grids, GBR-09 over-capacity rejection with the clashing events, the pending-exception state, the A-18 queue and decision audit, rescheduling, substitution and release. `pwsh -File tools/e2e/run.ps1` builds and exercises the React client against a throwaway LocalDB database. All four scripts create and drop only their own test databases; none changes the configured application database.

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
