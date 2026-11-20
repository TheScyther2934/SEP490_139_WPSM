# SEP490_139_WPSM
Wedding Planning and Service Management

Team onboarding and development workflow: [Dev guide/WPSM_Developer_Guide.md](Dev%20guide/WPSM_Developer_Guide.md).

## Chạy ứng dụng

Giao diện hiện tại là React trong `src/Client` (Vite); API ASP.NET Core chạy riêng. Cần Node.js/npm, .NET 8 SDK và một database PostgreSQL (Supabase).

Trước lần chạy đầu, cấu hình database theo mục **Database (PostgreSQL on Supabase)** bên dưới. Sau đó mở hai terminal tại thư mục solution:

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

Open `http://localhost:5173`. Vite proxies `/api/wpsm/*` to the API at `http://localhost:5128`. For a production build, run `npm run build` from `src/Client`; the built React app is written to `src/API/wwwroot` and served by the API. The API uses PostgreSQL through `WpsmDbContext` in `src/Infrastructure`. Persistence contracts are feature-scoped in `src/Application/Persistence`: `IProjectRepository`, `ISpecificationRoadmapRepository`, `IConfigurationRepository`, and `IAccountProfileRepository`. Their EF implementations live in `src/Infrastructure/Persistence/Repositories`; `IWpsmPortalSnapshotReader` composes data for the React bootstrap endpoint. API endpoints should depend on the feature contract they use, not on a catch-all repository or `DbContext`.

## Database (PostgreSQL on Supabase)

The API uses PostgreSQL (hosted on Supabase) through EF Core (`Npgsql.EntityFrameworkCore.PostgreSQL`). The schema is managed with EF Core migrations in `src/Infrastructure/Persistence/Migrations`; rules the EF model does not describe (CHECK constraints, the append-only audit log trigger, row level security) are in `InitialPostgresExtras.cs`. Hangfire stores its jobs in the `hangfire` schema of the same database (created on first start).

1. Put the connection string in User Secrets (never in tracked files). Use the Supabase **Session pooler** string (.NET format, port 5432): the direct connection is IPv6 only.

```powershell
dotnet user-secrets set "ConnectionStrings:WpsmDb" "Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true;Maximum Pool Size=10;Keepalive=30" --project src/API
```

On a server, set the environment variable `ConnectionStrings__WpsmDb` instead (two underscores).

2. Create or update the schema:

```powershell
dotnet ef database update --project src/Infrastructure --startup-project src/API
```

After a model change: `dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/API --output-dir Persistence/Migrations`.

3. Optional development accounts, created on first API start when missing:

```powershell
dotnet user-secrets set "Wpsm:SeedDevelopmentData" "true" --project src/API/API.csproj
dotnet user-secrets set "Wpsm:Seed:Admin:Email" "admin@wpsm.local" --project src/API/API.csproj
dotnet user-secrets set "Wpsm:Seed:Admin:Password" "<your-admin-password-12-chars-or-more>" --project src/API/API.csproj
dotnet user-secrets set "Wpsm:Seed:Manager:Email" "manager@wpsm.local" --project src/API/API.csproj
dotnet user-secrets set "Wpsm:Seed:Manager:Password" "<your-manager-password-12-chars-or-more>" --project src/API/API.csproj
```

The development seeder adds halls, lunch/evening slots, the service catalog, price frames and policies when missing, and creates the demo events once (guarded by the `DemoEventFlowSeeded` setting). Creating an event creates a Customer account; its temporary password is returned once to the Manager.

Notes for PostgreSQL:
- `DateTime` columns are `timestamp without time zone` holding UTC (legacy Npgsql timestamp mode, set in `WpsmDbContext`), as `datetime2` did on SQL Server.
- `Users.Email` is `citext` (case-insensitive). Text search uses `EF.Functions.ILike` with `SqlLike.Contains`.
- Table and column names are PascalCase: quote them in hand-written SQL (`select * from "WeddingProjects"`).

The SQL Server scripts in `Database/` (`WPSM_DB_Schema.sql`, `Database/Updates/*.sql`, seeds) are the former SQL Server schema, kept for reference only; they do not run on PostgreSQL.
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

Validation: the PowerShell scripts in `tests/` and `tools/e2e/run.ps1` were written for SQL Server/LocalDB (sqlcmd, throwaway databases) and do not run against PostgreSQL yet; they need rewriting.

## File storage (Cloudinary)

Proof photos, receipts and supplier documents are stored on Cloudinary when `Cloudinary__CloudName`, `Cloudinary__ApiKey` and `Cloudinary__ApiSecret` are set (copy `src/API/.env.example` to `src/API/.env`, which is git-ignored, or use User Secrets / environment variables). Without them the API falls back to the local disk (`Files:RootPath`). Rules, all enforced in `FileService` (`src/Infrastructure/Files`):

- The database keeps file metadata in `StoredFiles` and the storage key (`ExecutionPhotos.ImageFilePath`, `Payments.ReceiptFilePath`); never a URL and never the bytes.
- Key: `{event code}/{proof|receipts|suppliers}/{file id}.{ext}`; Cloudinary adds the environment folder (`wpsm/dev`, `wpsm/prod`, set with `Cloudinary:Folder`).
- Accepted: JPG, PNG, PDF and phone photos HEIC/HEIF/WebP/AVIF, recognised by content (not extension), 10 MB at most. Phone photos are converted to JPEG and every photo is limited to 2560 px on its long edge (quality auto). JPG/PNG metadata (GPS) is removed.
- Every asset is private. Photos are shown through a 302 redirect to a Cloudinary download link valid 5 minutes; PDFs and receipts are streamed by the API after the access check. Customers see photos only.
- Files are kept forever; job-08 (Sundays 03:00) removes only storage files no table refers to.

## Files, email and background jobs (API-01..04, JOB-07)

`StoredFiles` and `EmailOutbox` are part of the EF migrations. Hangfire creates its own `hangfire` schema when WPSM.API starts.

Set these with User Secrets or environment variables on WPSM.API:

| Key | Purpose |
| --- | --- |
| `Files:SigningKey` | Signs file URLs (valid 5 minutes). Required outside Development. |
| `Files:RootPath` | Private file folder. Default: `App_Data/files`. |
| `Smtp:Host`, `Smtp:Port`, `Smtp:User`, `Smtp:Password`, `Smtp:EnableSsl`, `Smtp:From` | SMTP server. In Development without `Smtp:Host`, emails are written as `.eml` files to `App_Data/mail`. |
| `Smtp:WebhookSecret` | HMAC-SHA256 secret for `POST /api/wpsm/notifications/email-status`, sent as the hex `X-Wpsm-Signature` header. The webhook returns 503 until it is set. |
| `Wpsm:PublicBaseUrl` | Site address used in email links. |
| `Hangfire:Enabled` | Set to `false` to stop background jobs. |
