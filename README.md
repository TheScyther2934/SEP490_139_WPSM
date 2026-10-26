# SEP490_139_WPSM
Wedding Planning and Service Management

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

Then open `http://localhost:5231/G01`. The Web app proxies `/api/wpsm/*` to the API at `http://localhost:5128`, so browser requests stay same-origin. The API currently uses an in-memory demonstration store; records reset when the API restarts and are not yet connected to a database or production authentication provider.
