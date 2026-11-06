# End-to-end tests on a throwaway database (WPSM_E2E_Test). Never touches WPSM_DB.
$ErrorActionPreference = "Continue"
$Here = $PSScriptRoot
$Root = (Resolve-Path "$Here\..\..").Path
$Out = Join-Path $env:TEMP "wpsm-e2e"
$Db = "WPSM_E2E_Test"
$Sql = "(localdb)\MSSQLLocalDB"

function Stop-TestPorts {
    Get-NetTCPConnection -State Listen -LocalPort 5300,5301,5302,5303 -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -Confirm:$false -ErrorAction SilentlyContinue }
}
function Drop-TestDatabase {
    sqlcmd -S $Sql -b -Q "IF DB_ID('$Db') IS NOT NULL BEGIN ALTER DATABASE $Db SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE $Db; END"
}

Set-Location $Root
Stop-TestPorts
Drop-TestDatabase
Remove-Item "$Out\files","$Out\mail" -Recurse -Force -ErrorAction SilentlyContinue
if (-not (Test-Path "$Here\node_modules")) { Push-Location $Here; npm install --no-audit --no-fund | Out-Null; Pop-Location }

dotnet build src\API\API.csproj -c Release -o "$Out\api" -v q --nologo | Select-Object -Last 3
dotnet build WPSM.Web\Web.csproj -c Release -o "$Out\web" -v q --nologo | Select-Object -Last 3
# React client: the test API serves it from its own wwwroot (content root = working directory).
$Client = Join-Path $Root "src\Client"
if (-not (Test-Path "$Client\node_modules")) { Push-Location $Client; npm install --no-audit --no-fund | Out-Null; Pop-Location }
Push-Location $Client; npx vite build --outDir "$Out\api\wwwroot" --emptyOutDir --logLevel error; "client build exit $LASTEXITCODE"; Pop-Location

sqlcmd -S $Sql -b -Q "CREATE DATABASE $Db"
$schema = [IO.File]::ReadAllText("$Root\Database\WPSM_DB_Schema.sql").Replace("WPSM_DB", $Db)
[IO.File]::WriteAllText("$Out\schema.sql", $schema, (New-Object Text.UTF8Encoding($true)))
sqlcmd -S $Sql -I -b -f 65001 -i "$Out\schema.sql" | Out-Null
"schema exit $LASTEXITCODE"

$pw = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 20)
$hook = -join ((48..57) + (97..122) | Get-Random -Count 32)
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ConnectionStrings__WpsmDb = "Server=$Sql;Database=$Db;Trusted_Connection=True;TrustServerCertificate=True;"
$env:Wpsm__SeedDevelopmentData = "true"
$env:Wpsm__Seed__Admin__Email = "admin@test.local"; $env:Wpsm__Seed__Admin__Password = $pw
$env:Wpsm__Seed__Manager__Email = "manager@test.local"; $env:Wpsm__Seed__Manager__Password = $pw
$env:Files__RootPath = "$Out\files"; $env:Smtp__PickupDirectory = "$Out\mail"; $env:Smtp__WebhookSecret = $hook
# Every suite signs in from 127.0.0.1; the production limit (5/min, SDS III.1.1) would reject later suites with 429.
$env:Auth__SignInPermitLimit = "1000"

$env:ASPNETCORE_URLS = "http://localhost:5300"
Start-Process "$Out\api\API.exe" -WorkingDirectory "$Out\api" -WindowStyle Hidden -RedirectStandardOutput "$Out\api.out" -RedirectStandardError "$Out\api.err" | Out-Null
$env:ASPNETCORE_URLS = "http://localhost:5301"; $env:WpsmApi__BaseUrl = "http://localhost:5300/"
Start-Process "$Out\web\Web.exe" -ArgumentList "--contentRoot", "$Root\WPSM.Web" -WorkingDirectory "$Out\web" -WindowStyle Hidden -RedirectStandardOutput "$Out\web.out" -RedirectStandardError "$Out\web.err" | Out-Null
foreach ($i in 1..30) { try { Invoke-WebRequest "http://localhost:5300/api/wpsm/auth/me" -UseBasicParsing -TimeoutSec 3 | Out-Null } catch { if ($_.Exception.Response) { break } }; Start-Sleep 1 }
foreach ($i in 1..30) { try { Invoke-WebRequest "http://localhost:5301/G01" -UseBasicParsing -TimeoutSec 3 | Out-Null; break } catch { Start-Sleep 1 } }

$env:TEST_PW = $pw; $env:HOOK_SECRET = $hook; $env:FILE_ROOT = "$Out\files"; $env:MAIL_DIR = "$Out\mail"
$failed = 0
$env:SHOT = "$Out\c02.png"; $env:EVENT_DATE = "2027-03-15"
"--- G-01 form login"; node "$Here\login.js" | Select-String 'FAIL|passed'; if ($LASTEXITCODE) { $failed++ }
"--- integration"; node "$Here\integration.js" | Select-String 'FAIL|passed'; if ($LASTEXITCODE) { $failed++ }
"--- C-09"; node "$Here\c09.js" | Select-String 'FAIL|passed'; if ($LASTEXITCODE) { $failed++ }
"--- Admin A-01, A-09..A-18"; node "$Here\admin.js" | Select-String 'FAIL|passed'; if ($LASTEXITCODE) { $failed++ }
"--- all screens"; node "$Here\screens.js" | Select-String 'FAIL|sweep'; if ($LASTEXITCODE) { $failed++ }

"--- SMTP configured"
$env:ASPNETCORE_URLS = "http://localhost:5302"; $env:Smtp__Host = "127.0.0.1"; $env:Smtp__Port = "1"; $env:Hangfire__Enabled = "false"; $env:Wpsm__SeedDevelopmentData = "false"
Start-Process "$Out\api\API.exe" -WorkingDirectory "$Out\api" -WindowStyle Hidden | Out-Null
foreach ($i in 1..20) { try { Invoke-WebRequest "http://localhost:5302/api/wpsm/auth/me" -UseBasicParsing -TimeoutSec 3 | Out-Null } catch { if ($_.Exception.Response) { break } }; Start-Sleep 1 }
node "$Here\smtp.js"; if ($LASTEXITCODE) { $failed++ }

"--- M-05"
$env:SHOT = "$Out\m05.png"; $env:EVENT_DATE = "2027-04-20"
node "$Here\m05.js" | Select-String 'FAIL|passed'; if ($LASTEXITCODE) { $failed++ }

"--- React client (src/Client)"
$env:EVENT_DATE = "2027-05-18"
node "$Here\react.js" | Select-String 'FAIL|passed'; if ($LASTEXITCODE) { $failed++ }

"--- Report 4 operations: CR, BEO, day-of, payments, feedback, resources, admin config"
# Needs Hangfire on 5300: the JOB-05 check waits for the real recurring job.
node "$Here\operations.js" | Select-String 'FAIL|passed'; if ($LASTEXITCODE) { $failed++ }

"--- Seeder restart when a sample slot is taken (I-018)"
# Another project now holds the hall slot of sample WPSM-2026-014; a restart with seeding on must still start.
sqlcmd -S $Sql -d $Db -I -b -Q "UPDATE dbo.WeddingProjects SET ProjectCode = 'WPSM-2026-USER1' WHERE ProjectCode = 'WPSM-2026-014'" | Out-Null
$env:ASPNETCORE_URLS = "http://localhost:5303"; $env:Wpsm__SeedDevelopmentData = "true"; Remove-Item Env:\Smtp__Host -ErrorAction SilentlyContinue
Start-Process "$Out\api\API.exe" -WorkingDirectory "$Out\api" -WindowStyle Hidden -RedirectStandardError "$Out\seed.err" | Out-Null
$seedUp = $false
foreach ($i in 1..30) { try { Invoke-WebRequest "http://localhost:5303/api/wpsm/auth/me" -UseBasicParsing -TimeoutSec 3 | Out-Null } catch { if ($_.Exception.Response) { $seedUp = $true; break } }; Start-Sleep 1 }
if ($seedUp) { "PASS API starts when a sample slot is taken" } else { "FAIL API starts when a sample slot is taken"; Get-Content "$Out\seed.err" -Tail 5; $failed++ }

Stop-TestPorts
Start-Sleep 1
Drop-TestDatabase
"failed suites: $failed"
exit $failed
