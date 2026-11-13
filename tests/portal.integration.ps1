param([string]$Server = 'localhost', [switch]$HttpsApi, [string]$ApiAssembly = '')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$testId = [Guid]::NewGuid().ToString('N')
$database = "WPSM_Test_$testId"
$testDir = Join-Path $repo "artifacts/portal-$testId"
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
$api = $null
$createdDatabase = $false
$checks = 0
$baseUri = if ($HttpsApi) { 'https://localhost:15387' } else { 'http://localhost:15382' }
$apiAssemblyPath = if ($ApiAssembly) { (Resolve-Path -LiteralPath $ApiAssembly).Path } else { Join-Path $repo 'src/API/bin/Debug/net8.0/API.dll' }

function Check($condition, $name) {
    if (!$condition) { throw "FAILED: $name" }
    $script:checks++
    Write-Output "PASS: $name"
}
function Sql($query) {
    $result = & sqlcmd -S $Server -E -d $database -I -b -W -h -1 -Q "SET NOCOUNT ON; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL check failed" }
    return ($result -join "`n").Trim()
}
function Start-App($dll, $contentRoot, $urls, $settings) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = $contentRoot
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($arg in @($dll, '--urls', $urls, '--environment', 'Development')) { $start.ArgumentList.Add($arg) }
    foreach ($key in $settings.Keys) { $start.Environment[$key] = $settings[$key] }
    $process = [Diagnostics.Process]::Start($start)
    $process | Add-Member -NotePropertyName OutputLogTask -NotePropertyValue $process.StandardOutput.ReadToEndAsync()
    $process | Add-Member -NotePropertyName ErrorLogTask -NotePropertyValue $process.StandardError.ReadToEndAsync()
    return $process
}
function Request($path, $session, $method = 'GET', $body = $null) {
    $cookieSession = if ($session -and $session.WebSession) { $session.WebSession } else { $session }
    $arguments = @{ Uri = "$baseUri/api/wpsm/$path"; Method = $method; WebSession = $cookieSession; SkipHttpErrorCheck = $true; TimeoutSec = 15 }
    if ($session -and $session.AccessToken) { $arguments.Headers = @{ Authorization = "Bearer $($session.AccessToken)" } }
    if ($null -ne $body) { $arguments.ContentType = 'application/json'; $arguments.Body = $body | ConvertTo-Json -Depth 8 }
    return Invoke-WebRequest @arguments
}
function Login($email, $password) {
    $session = [pscustomobject]@{ WebSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new(); AccessToken = '' }
    $response = Request 'auth/token' $session 'POST' @{ email = $email; password = $password }
    if ($response.StatusCode -ne 200) { throw "Fixture login failed: $email ($($response.StatusCode))" }
    $session.AccessToken = ($response.Content | ConvertFrom-Json).accessToken
    return $session
}

try {
    # This script creates and destroys only its own randomly named database.
    $schema = [IO.File]::ReadAllText((Join-Path $repo 'Database/WPSM_DB_Schema.sql')).Replace('[WPSM_DB]', "[$database]").Replace("N'WPSM_DB'", "N'$database'")
    $schemaFile = Join-Path $testDir 'schema.sql'
    [IO.File]::WriteAllText($schemaFile, $schema, [Text.UTF8Encoding]::new($true))
    & sqlcmd -S $Server -E -d master -I -b -i $schemaFile | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize disposable test database' }
    $createdDatabase = $true
    $settings = @{
        ConnectionStrings__WpsmDb = "Server=$Server;Database=$database;Integrated Security=True;TrustServerCertificate=True;"
        Wpsm__SeedDevelopmentData = 'true'
        Wpsm__Seed__Admin__Email = 'admin@test.local'
        Wpsm__Seed__Admin__Password = "A!$testId"
        Wpsm__Seed__Manager__Email = 'manager@test.local'
        Wpsm__Seed__Manager__Password = "M!$testId"
        Wpsm__Email__Host = ''
        Logging__LogLevel__Default = 'Warning'
    }
    $apiUrls = if ($HttpsApi) { 'https://localhost:15386;http://localhost:15381' } else { 'http://localhost:15381' }
    $api = Start-App $apiAssemblyPath (Join-Path $repo 'src/API') $apiUrls $settings
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try { $r = Invoke-WebRequest 'http://localhost:15381/api/wpsm/projects' -SkipHttpErrorCheck -TimeoutSec 1; if ($r.StatusCode -eq 401) { $ready = $true; break } } catch {}
        if ($api.HasExited) {
            $startupLog = $api.OutputLogTask.GetAwaiter().GetResult() + $api.ErrorLogTask.GetAwaiter().GetResult()
            throw "Test API exited during initialization: $startupLog"
        }
        Start-Sleep -Milliseconds 250
    }
    Check $ready 'API initialized against isolated SQL Server database'
    $anonymous = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    Check ((Request 'projects' $anonymous).StatusCode -eq 401) 'Anonymous project access rejected'
    Check ((Request 'auth/token' $anonymous 'POST' @{ email = 'manager@test.local'; password = 'wrong' }).StatusCode -eq 401) 'Incorrect password rejected'
    $null = Sql "INSERT dbo.Users (Email,PasswordHash,FullName,Role,Status,MustChangePassword,CreatedAt) VALUES ('legacy@test.local','not-a-valid-identity-hash!',N'Legacy Admin','Admin','Active',0,SYSUTCDATETIME());"
    $legacyLogin = Request 'auth/token' $anonymous 'POST' @{email = 'legacy@test.local'; password = 'anything'}
    Check ($legacyLogin.StatusCode -eq 401) 'Malformed legacy password hash returns 401 instead of throwing 500'
    $manager = Login 'manager@test.local' "M!$testId"
    $admin = Login 'admin@test.local' "A!$testId"
    Check (((Request 'projects' $manager).Content | ConvertFrom-Json).Count -eq 7) 'Manager sees assigned seed projects'
    $null = Sql "INSERT dbo.Users (Email,PasswordHash,FullName,Role,Status,MustChangePassword,CreatedAt) SELECT 'other@test.local',PasswordHash,N'Other Manager','Manager','Active',0,SYSUTCDATETIME() FROM dbo.Users WHERE Email='manager@test.local';"
    $other = Login 'other@test.local' "M!$testId"
    Check (((Request 'projects' $other).Content | ConvertFrom-Json).Count -eq 0) 'Other manager cannot list unassigned projects'
    Check ((Request 'projects/WPSM-2026-014' $other).StatusCode -eq 404) 'Direct project access is scoped'
    Check ((Request 'bootstrap?projectCode=WPSM-2026-014' $other).StatusCode -eq 404) 'Bootstrap rejects an inaccessible project instead of falling back'
    Check ((Request 'projects/WPSM-2026-014/freeze' $other 'POST' @{}).StatusCode -eq 404) 'Other manager cannot mutate project'
    $halls = (Request 'halls?date=2035-04-18' $manager).Content | ConvertFrom-Json
    $command = @{ coupleName = 'Portal Integration Customer'; customerEmail = 'customer@test.local'; customerPhone = '0912345678'; hall = $halls[0].name; slot = 'Evening'; eventDate = '2035-04-18'; baseValue = 100000000; nrdAmount = 30000000; paymentMethod = 'Bank' }
    $invalid = $command.Clone(); $invalid.nrdAmount = 1
    Check ((Request 'projects' $manager 'POST' $invalid).StatusCode -eq 400) 'Insufficient NRD rejected'
    $response = Request 'projects' $manager 'POST' $command
    Check ($response.StatusCode -eq 201) 'Project and customer created'
    $created = $response.Content | ConvertFrom-Json
    $code = $created.project.id
    Check ($created.project.payments[0].status -eq 'Due') 'Bank NRD remains due until receipt upload'
    Check ($created.emailStatus -eq 'Pending') 'Email delivery queued in database'
    Check ((Sql "SELECT COUNT(*) FROM dbo.EmailDeliveries WHERE ProtectedPayload IS NOT NULL AND ProtectedPayload NOT LIKE '%$($created.temporaryPassword)%'") -eq '1') 'Email credential payload stored encrypted'
    Check ((Request 'projects' $manager 'POST' $command).StatusCode -eq 400) 'Duplicate hall/slot/date rejected'
    Check ((Sql "SELECT COUNT(*) FROM dbo.WeddingProjects WHERE ProjectCode='$code'") -eq '1') 'Project persisted exactly once'
    $customer = Login 'customer@test.local' $created.temporaryPassword
    $oldCustomerSession = Login 'customer@test.local' $created.temporaryPassword
    Check ((Request 'bootstrap' $customer).StatusCode -eq 403) 'Temporary-password account must change password before accessing data'
    Check ((Request 'auth/change-password' $customer 'POST' @{ currentPassword = $created.temporaryPassword; newPassword = $created.temporaryPassword; confirmPassword = $created.temporaryPassword }).StatusCode -eq 400) 'Reusing the current password rejected'
    Check ((Request 'auth/change-password' $customer 'POST' @{ currentPassword = 'wrong'; newPassword = 'ChangedTest!123'; confirmPassword = 'ChangedTest!123' }).StatusCode -eq 400) 'Wrong current password rejected'
    $changedPassword = Request 'auth/change-password' $customer 'POST' @{ currentPassword = $created.temporaryPassword; newPassword = 'ChangedTest!123'; confirmPassword = 'ChangedTest!123' }
    Check ($changedPassword.StatusCode -eq 200) 'Password changed and fresh token pair issued'
    $customer.AccessToken = ($changedPassword.Content | ConvertFrom-Json).accessToken
    Check ((Request 'auth/token/refresh' $oldCustomerSession 'POST').StatusCode -eq 401) 'Password change revokes other refresh-token sessions'
    Check (((Request 'bootstrap' $customer).Content | ConvertFrom-Json).projects.Count -eq 1) 'Customer sees only their own project'
    Check ((Request 'projects' $customer 'POST' $command).StatusCode -eq 403) 'Customer cannot create projects'
    Check ((Request 'projects' $admin 'POST' $command).StatusCode -eq 403) 'Admin cannot impersonate Manager creation'
    $pdfFile = Join-Path $testDir 'receipt.pdf'
    [IO.File]::WriteAllBytes($pdfFile, [Text.Encoding]::ASCII.GetBytes('%PDF-1.4 integration fixture'))
    $upload = Invoke-WebRequest "$baseUri/api/wpsm/projects/$code/nrd-receipt" -Method Post -WebSession $manager.WebSession -Headers @{ Authorization = "Bearer $($manager.AccessToken)" } -Form @{receipt = Get-Item $pdfFile} -SkipHttpErrorCheck
    Check ($upload.StatusCode -eq 200) 'Receipt upload confirms NRD'
    Check ((Sql "SELECT DATALENGTH(ReceiptContent) FROM dbo.Payments p JOIN dbo.WeddingProjects w ON p.ProjectId=w.ProjectId WHERE w.ProjectCode='$code'") -gt 0) 'Receipt bytes persisted in SQL Server'
    Check ((Request "projects/$code/nrd-receipt" $manager).Content.Length -gt 0) 'Owner can download persisted receipt'
    Check ((Request "projects/$code/nrd-receipt" $other).StatusCode -eq 404) 'Other manager cannot download receipt'
    $snapshot = (Request "bootstrap?projectCode=$code" $manager).Content | ConvertFrom-Json
    Check ($snapshot.project.outstandingBalance -eq 70000000) 'Hub outstanding balance reflects recorded payment'
    $null = Sql "UPDATE dbo.Users SET Status='Inactive' WHERE Email='other@test.local'"
    Check ((Request 'auth/token/refresh' $other 'POST').StatusCode -eq 401) 'Inactive account cannot refresh its session'
    Check ((Request 'auth/token/revoke' $manager 'POST').StatusCode -eq 204) 'Logout revokes refresh token'
    Check ((Request 'auth/token/refresh' $manager 'POST').StatusCode -eq 401) 'Logged-out refresh token rejected'
    Write-Output "$checks integration checks passed."
}
finally {
    if ($api -and !$api.HasExited) { $api.Kill($true); $api.WaitForExit() }
    if ($createdDatabase -and $database -match '^WPSM_Test_[a-f0-9]{32}$') {
        & sqlcmd -S $Server -E -d master -I -b -Q "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database];" | Out-Null
    }
}
