# End-to-end tests on a throwaway database (WPSM_E2E_Test). Never touches WPSM_DB.
$ErrorActionPreference = "Continue"
$Here = $PSScriptRoot
$Root = (Resolve-Path "$Here\..\..").Path
$Out = Join-Path $env:TEMP "wpsm-e2e"
$Db = "WPSM_E2E_Test"
$Sql = "(localdb)\MSSQLLocalDB"

function Stop-TestPorts {
    Get-NetTCPConnection -State Listen -LocalPort 5300,5301,5302 -ErrorAction SilentlyContinue |
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
"--- all screens"; node "$Here\screens.js" | Select-String 'FAIL|sweep'; if ($LASTEXITCODE) { $failed++ }

"--- SMTP configured"
$env:ASPNETCORE_URLS = "http://localhost:5302"; $env:Smtp__Host = "127.0.0.1"; $env:Smtp__Port = "1"; $env:Hangfire__Enabled = "false"; $env:Wpsm__SeedDevelopmentData = "false"
Start-Process "$Out\api\API.exe" -WorkingDirectory "$Out\api" -WindowStyle Hidden | Out-Null
foreach ($i in 1..20) { try { Invoke-WebRequest "http://localhost:5302/api/wpsm/auth/me" -UseBasicParsing -TimeoutSec 3 | Out-Null } catch { if ($_.Exception.Response) { break } }; Start-Sleep 1 }
node "$Here\smtp.js"; if ($LASTEXITCODE) { $failed++ }

"--- M-05"
$env:SHOT = "$Out\m05.png"; $env:EVENT_DATE = "2027-04-20"
node "$Here\m05.js" | Select-String 'FAIL|passed'; if ($LASTEXITCODE) { $failed++ }

Stop-TestPorts
Start-Sleep 1
Drop-TestDatabase
"failed suites: $failed"
exit $failed
