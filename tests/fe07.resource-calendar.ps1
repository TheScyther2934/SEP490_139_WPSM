# FE-07 Resource Calendar checks (M-21, M-22) on a throwaway database.
# Creates and drops WPSM_FE07_Test only; the application database is never touched.
$ErrorActionPreference = "Stop"
$Here = $PSScriptRoot
$Root = (Resolve-Path "$Here\..").Path
$Out = Join-Path $env:TEMP "wpsm-fe07-test"
$Db = "WPSM_FE07_Test"
$Sql = "(localdb)\MSSQLLocalDB"
$Port = 5313
$Base = "http://localhost:$Port/api/wpsm"
$Password = "Fe07-Manager-2026"
$bad = 0

function Check([string]$name, $ok, [string]$detail = "") {
    $passed = $false
    if ($ok -is [bool]) { $passed = $ok }
    elseif ($ok -is [array]) { $passed = ($ok.Count -gt 0 -and $ok -notcontains $false) }
    else { $passed = [bool]$ok }
    if ($passed) { Write-Host "PASS $name" } else { $script:bad++; Write-Host "FAIL $name $detail" -ForegroundColor Red }
}
function Stop-TestPort {
    Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -Confirm:$false -ErrorAction SilentlyContinue }
}
function Drop-TestDatabase {
    sqlcmd -S $Sql -b -Q "IF DB_ID('$Db') IS NOT NULL BEGIN ALTER DATABASE $Db SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE $Db; END" | Out-Null
}
function New-Result([int]$status, $data) { [pscustomobject]@{ Status = $status; Data = $data } }
function Read-Json([byte[]]$bytes) {
    if ($null -eq $bytes -or $bytes.Length -eq 0) { return $null }
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if (-not $text) { return $null }
    try { return $text | ConvertFrom-Json } catch { return $text }
}
function Call([string]$method, [string]$path, $token = $null, $body = $null) {
    $headers = @{}
    if ($token) { $headers.Authorization = "Bearer $token" }
    $params = @{ Uri = "$Base$path"; Method = $method; Headers = $headers; ContentType = "application/json; charset=utf-8"; UseBasicParsing = $true }
    if ($null -ne $body) { $params.Body = [System.Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 8)) }
    try {
        $response = Invoke-WebRequest @params
        return New-Result ([int]$response.StatusCode) (Read-Json $response.RawContentStream.ToArray())
    } catch {
        $response = $_.Exception.Response
        if ($null -eq $response) { throw }
        $stream = New-Object System.IO.MemoryStream
        if ($response.PSObject.Methods["GetResponseStream"]) { $response.GetResponseStream().CopyTo($stream) }
        else { $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult().CopyTo($stream) }
        return New-Result ([int]$response.StatusCode) (Read-Json $stream.ToArray())
    }
}
function Login([string]$email, [string]$password) {
    (Call "POST" "/auth/token" $null @{ email = $email; password = $password }).Data.accessToken
}

Stop-TestPort
Drop-TestDatabase
Remove-Item $Out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $Out | Out-Null

try {
    Push-Location $Root
    dotnet build src\API\API.csproj -c Release -o "$Out\api" -v q --nologo | Select-Object -Last 3
    Pop-Location

    sqlcmd -S $Sql -b -Q "CREATE DATABASE $Db" | Out-Null
    $schema = [IO.File]::ReadAllText("$Root\Database\WPSM_DB_Schema.sql").Replace("WPSM_DB", $Db)
    [IO.File]::WriteAllText("$Out\schema.sql", $schema, (New-Object Text.UTF8Encoding($true)))
    sqlcmd -S $Sql -I -b -f 65001 -i "$Out\schema.sql" | Out-Null

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://localhost:$Port"
    $env:ConnectionStrings__WpsmDb = "Server=$Sql;Database=$Db;Trusted_Connection=True;TrustServerCertificate=True;"
    $env:Wpsm__SeedDevelopmentData = "true"
    $env:Wpsm__Seed__Admin__Email = "admin@test.local"; $env:Wpsm__Seed__Admin__Password = $Password
    $env:Wpsm__Seed__Manager__Email = "manager@test.local"; $env:Wpsm__Seed__Manager__Password = $Password
    $env:Files__RootPath = "$Out\files"; $env:Smtp__PickupDirectory = "$Out\mail"
    $env:Hangfire__Enabled = "false"; $env:Auth__SignInPermitLimit = "1000"

    Start-Process "$Out\api\API.exe" -WorkingDirectory "$Out\api" -WindowStyle Hidden -RedirectStandardOutput "$Out\api.out" -RedirectStandardError "$Out\api.err" | Out-Null
    $ready = $false
    foreach ($i in 1..40) {
        try { Invoke-WebRequest "$Base/projects" -UseBasicParsing -TimeoutSec 3 | Out-Null } catch { if ($_.Exception.Response) { $ready = $true; break } }
        Start-Sleep 1
    }
    if (-not $ready) { throw "API không khởi động. Xem $Out\api.err" }

    $manager = Login "manager@test.local" $Password
    $admin = Login "admin@test.local" $Password
    Check "manager signs in" ([bool]$manager)
    Check "admin signs in" ([bool]$admin)

    # ── M-21 calendar for the demo project's event day (seeders create its allocations) ──
    $day = "2026-12-20"
    $calendar = (Call "GET" "/resource-calendar?from=$day&to=$day" $manager).Data
    Check "M-21 returns the active halls" (($calendar.halls | Measure-Object).Count -eq 4) "halls=$(($calendar.halls | Measure-Object).Count)"
    Check "M-21 lists the demo booking" (($calendar.bookings | Where-Object { $_.code -eq "WPSM-2026-014" } | Measure-Object).Count -eq 1)
    Check "M-21 returns the 10 shared resources" (($calendar.resources | Measure-Object).Count -eq 10) "resources=$(($calendar.resources | Measure-Object).Count)"
    Check "M-21 returns the 5 resource types" (($calendar.types | Measure-Object).Count -eq 5)
    Check "M-21 summary counts the event" ($calendar.summary.events -ge 1) "events=$($calendar.summary.events)"
    $technicianLane = $calendar.lanes | Where-Object { $_.type -eq "Technician" -and $_.usages.Count -gt 0 } | Select-Object -First 1
    Check "M-21 lanes show the seeded technician allocation" ([bool]$technicianLane) "lanes=$(($calendar.lanes | Measure-Object).Count)"
    $resourceId = $technicianLane.resourceId
    $totalQuantity = $technicianLane.totalQuantity
    $usage = $technicianLane.usages | Select-Object -First 1
    Check "lane usage shows 3 of 6 booked" ($usage.reservedQuantity -eq 3 -and $totalQuantity -eq 6) "reserved=$($usage.reservedQuantity) total=$totalQuantity"

    $projectId = ($calendar.bookings | Where-Object { $_.code -eq "WPSM-2026-014" }).projectId
    $seededAllocation = ($usage.allocations | Select-Object -First 1).allocationId

    # ── M-22 detail of an existing allocation ──
    $detail = (Call "GET" "/resource-allocations/$seededAllocation" $manager).Data
    Check "M-22 opens the seeded allocation" ($detail.allocationId -eq $seededAllocation)
    Check "seeded allocation is Reserved" ($detail.status -eq "Reserved") $detail.status
    Check "seeded allocation has no conflict" ($detail.hasConflict -eq $false)
    Check "M-22 can request an exception only when over capacity" ($detail.canRequestException -eq $false)

    # ── GBR-09: over-capacity is rejected with the clashing events ──
    $over = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectId = $projectId
        startTime = "${day}T17:30:00+07:00"; endTime = "${day}T21:30:00+07:00"; quantity = 5
    }
    Check "GBR-09 rejects an over-capacity allocation" ($over.Status -eq 409) "status=$($over.Status)"
    Check "the conflict names the shortage" ($over.Data.conflict.shortage -eq 2) "shortage=$($over.Data.conflict.shortage)"
    Check "the conflict lists the clashing project" (($over.Data.conflict.clashing | Where-Object { $_.projectCode -eq "WPSM-2026-014" } | Measure-Object).Count -eq 1)

    # ── UC-47: request the Admin exception, the pending row does not consume capacity ──
    $pending = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectId = $projectId
        startTime = "${day}T17:30:00+07:00"; endTime = "${day}T21:30:00+07:00"; quantity = 5
        requestException = $true; exceptionNote = "Sảnh cần thêm 2 kỹ thuật viên cho phần ánh sáng"
    }
    Check "M-21 sends the exception request and saves the pending allocation" ($pending.Status -eq 201) "status=$($pending.Status)"
    Check "pending allocation is PendingException" ($pending.Data.status -eq "PendingException") $pending.Data.status
    $requestCode = $pending.Data.pendingRequestCode
    Check "pending allocation carries the exception code" ([bool]$requestCode) $requestCode

    $free = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectId = $projectId
        startTime = "${day}T18:00:00+07:00"; endTime = "${day}T19:00:00+07:00"; quantity = 1
    }
    Check "pending rows are ignored by the conflict query" ($free.Status -eq 201) "status=$($free.Status)"
    $freeAllocation = $free.Data.allocationId

    # ── A-18 receives and decides the resource exception ──
    $queue = (Call "GET" "/admin/exception-requests" $admin).Data
    $request = $queue | Where-Object { $_.code -eq $requestCode }
    Check "A-18 receives the resource conflict request" ([bool]$request) "code=$requestCode"
    if ($request) {
        $decision = Call "POST" "/admin/exception-requests/$($request.id)/decision" $admin @{ approve = $true; note = "Chấp thuận bổ sung kỹ thuật viên" }
        Check "A-18 approves the resource exception" ($decision.Status -eq 204) "status=$($decision.Status)"
    }
    $approved = (Call "GET" "/resource-allocations/$($pending.Data.allocationId)" $manager).Data
    Check "approved exception marks the allocation" ($approved.status -eq "Exception Approved" -and $approved.isExceptionApproved) $approved.status
    Check "approved exception keeps the Admin note" ($approved.exceptionNote -eq "Chấp thuận bổ sung kỹ thuật viên") $approved.exceptionNote

    $afterApprove = (Call "GET" "/resource-calendar?from=$day&to=$day" $manager).Data
    Check "M-21 now reports the capacity conflict" ($afterApprove.summary.conflicts -ge 1) "conflicts=$($afterApprove.summary.conflicts)"

    # ── M-22 reschedule / substitute ──
    $conflictingUpdate = Call "PUT" "/resource-allocations/$freeAllocation" $manager @{
        resourceId = $resourceId; projectId = $projectId
        startTime = "${day}T17:00:00+07:00"; endTime = "${day}T22:00:00+07:00"; quantity = 3
    }
    Check "rescheduling into a conflicting window is rejected" ($conflictingUpdate.Status -eq 409) "status=$($conflictingUpdate.Status)"

    $movedUpdate = Call "PUT" "/resource-allocations/$freeAllocation" $manager @{
        resourceId = $resourceId; projectId = $projectId
        startTime = "${day}T08:00:00+07:00"; endTime = "${day}T09:00:00+07:00"; quantity = 1
    }
    Check "rescheduling to a free window succeeds" ($movedUpdate.Status -eq 200) "status=$($movedUpdate.Status)"

    $released = Call "DELETE" "/resource-allocations/$freeAllocation" $manager
    Check "M-22 releases the allocation" ($released.Status -eq 200 -and $released.Data.status -eq "Released") "status=$($released.Status)/$($released.Data.status)"
    $editReleased = Call "PUT" "/resource-allocations/$freeAllocation" $manager @{
        resourceId = $resourceId; projectId = $projectId
        startTime = "${day}T08:00:00+07:00"; endTime = "${day}T09:00:00+07:00"; quantity = 1
    }
    Check "a released allocation cannot be edited" ($editReleased.Status -eq 400) "status=$($editReleased.Status)"

    # ── Validation and scope ──
    $badTimes = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectId = $projectId
        startTime = "${day}T20:00:00+07:00"; endTime = "${day}T19:00:00+07:00"; quantity = 1
    }
    Check "end before start is rejected" ($badTimes.Status -eq 400) "status=$($badTimes.Status)"
    $tooMany = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectId = $projectId
        startTime = "${day}T08:00:00+07:00"; endTime = "${day}T09:00:00+07:00"; quantity = 99
    }
    Check "quantity above the total is rejected" ($tooMany.Status -eq 400) "status=$($tooMany.Status)"
    $foreign = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectId = 999999
        startTime = "${day}T08:00:00+07:00"; endTime = "${day}T09:00:00+07:00"; quantity = 1
    }
    Check "a project outside the Manager scope returns 404" ($foreign.Status -eq 404) "status=$($foreign.Status)"
    $forbidden = Call "GET" "/resource-calendar?from=$day&to=$day" $admin
    Check "Admin cannot open the Manager calendar" ($forbidden.Status -eq 403) "status=$($forbidden.Status)"
} catch {
    $bad++
    Write-Host "ERROR $($_.Exception.Message)" -ForegroundColor Red
    if (Test-Path "$Out\api.err") { Get-Content "$Out\api.err" -Tail 20 }
} finally {
    Pop-Location -ErrorAction SilentlyContinue
    Stop-TestPort
    Start-Sleep 1
    Drop-TestDatabase
}

Write-Host "FE-07 checks failed: $bad"
exit $(if ($bad) { 1 } else { 0 })
