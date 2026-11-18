# FE-07 Resource Calendar integration checks (M-21, M-22, UC-25/UC-26) on a throwaway database.
# Creates and drops WPSM_FE07_Test only; the application database is never touched.
# The script drives the live API: POST/PUT/DELETE /resource-allocations (IResourceRepository) for the
# schedule, GET /resource-calendar + GET /resource-allocations/{id} (IResourceCalendarRepository) for
# the grid and the conflict breakdown, and A-18 for the special-exception decisions.
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
    # The base schema predates additive columns; the Updates folder brings a database to HEAD.
    Get-ChildItem "$Root\Database\Updates\*.sql" | Sort-Object Name | ForEach-Object {
        sqlcmd -S $Sql -d $Db -I -b -f 65001 -i $_.FullName | Out-Null
    }

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
    Check "manager signs in" ([bool]$manager)
    $admin = Login "admin@test.local" $Password
    Check "admin signs in" ([bool]$admin)
    $managerQueue = Call "GET" "/admin/exception-requests" $manager
    Check "A-18 queue is Admin-only" ($managerQueue.Status -eq 403) "status=$($managerQueue.Status)"

    # ── A project whose event is 10 days away, with a frozen baseline ─────────
    $eventDate = (Get-Date).Date.AddDays(10).ToString("yyyy-MM-dd")
    $halls = (Call "GET" "/halls?date=$eventDate" $manager).Data
    $hall = $halls | Where-Object { ($_.slots | Where-Object { $_.available }) } | Select-Object -First 1
    $slot = $hall.slots | Where-Object { $_.available } | Select-Object -First 1
    $email = "fe07.$([guid]::NewGuid().ToString('N').Substring(0,8))@example.com"
    $created = Call "POST" "/projects" $manager @{
        coupleName = "FE07 Test Couple"; customerEmail = $email; customerPhone = "0900005678"
        hall = $hall.name; slot = $slot.slotName; eventDate = $eventDate
        baseValue = 100000000; nrdAmount = 50000000; paymentMethod = "Cash"
    }
    Check "M-03 creates the project" ($created.Status -eq 201) ($created.Data | ConvertTo-Json -Compress -Depth 3)
    $projectCode = $created.Data.project.id
    $frozen = Call "POST" "/projects/$projectCode/freeze" $manager
    Check "M-05 freezes the baseline" ($frozen.Status -eq 200) ($frozen.Data | ConvertTo-Json -Compress -Depth 2)

    # ── M-21 grid and resource picker ────────────────────────────────────────
    $calendar = (Call "GET" "/resource-calendar?from=$eventDate&to=$eventDate" $manager).Data
    Check "M-21 returns the active halls" (($calendar.halls | Measure-Object).Count -ge 1) "halls=$(($calendar.halls | Measure-Object).Count)"
    Check "M-21 lists the new booking" (($calendar.bookings | Where-Object { $_.code -eq $projectCode } | Measure-Object).Count -eq 1)
    Check "M-21 returns the shared resources" (($calendar.resources | Measure-Object).Count -ge 5) "resources=$(($calendar.resources | Measure-Object).Count)"
    Check "M-21 returns the resource types" (($calendar.types | Measure-Object).Count -ge 3)

    $resource = $calendar.resources | Where-Object { $_.totalQuantity -ge 4 } | Select-Object -First 1
    Check "a resource with capacity is available" ([bool]$resource) "resource=$($resource | ConvertTo-Json -Compress)"
    $resourceId = $resource.resourceId
    $total = [int]$resource.totalQuantity

    # ── M-21 allocate a reserved quantity ────────────────────────────────────
    $first = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectCode = $projectCode
        startTime = "${eventDate}T17:30:00+07:00"; endTime = "${eventDate}T19:30:00+07:00"; quantity = 3
    }
    Check "M-21 saves a reserved allocation" ($first.Status -eq 200 -and $first.Data.allocation.status -eq "Reserved") "status=$($first.Status)/$($first.Data.allocation.status)"
    $allocationId = $first.Data.allocation.allocationId

    $detail = (Call "GET" "/resource-allocations/$allocationId" $manager).Data
    Check "M-22 opens the allocation" ($detail.allocationId -eq $allocationId)
    Check "M-22 reports the reserved quantity" ($detail.quantity -eq 3 -and $detail.status -eq "Reserved") "qty=$($detail.quantity)/$($detail.status)"
    Check "a reserved allocation has no conflict" ($detail.hasConflict -eq $false)
    Check "M-22 offers the exception only for a conflict" ($detail.canRequestException -eq $false)

    # ── M-21/GBR-09: over-capacity is rejected with the clashing events ──────
    $overflowQuantity = $total - 2
    $over = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectCode = $projectCode
        startTime = "${eventDate}T17:30:00+07:00"; endTime = "${eventDate}T21:30:00+07:00"; quantity = $overflowQuantity
    }
    Check "GBR-09 rejects an over-capacity allocation" ($over.Status -eq 409) "status=$($over.Status)"
    Check "the conflict lists the clashing project" (($over.Data.conflicts | Where-Object { $_.projectCode -eq $projectCode } | Measure-Object).Count -ge 1) ($over.Data.conflicts | ConvertTo-Json -Compress)
    Check "the conflict explains the shortage (MSG08)" ([bool]$over.Data.message) $over.Data.message

    # ── UC-47: request the Admin exception; the pending row does not consume capacity ──
    $pending = Call "POST" "/resource-allocations/exception-requests" $manager @{
        resourceId = $resourceId; projectCode = $projectCode
        startTime = "${eventDate}T17:30:00+07:00"; endTime = "${eventDate}T21:30:00+07:00"; quantity = $overflowQuantity
        note = "Không còn dàn âm thanh nào khác trong ngày, đề nghị Admin chấp thuận"
    }
    Check "M-21 sends the exception request" ($pending.Status -eq 200) ($pending.Data | ConvertTo-Json -Compress -Depth 3)
    Check "the pending request is recorded on the allocation" ($pending.Data.allocation.exceptionStatus -eq "Pending") $pending.Data.allocation.exceptionStatus
    $pendingAllocationId = $pending.Data.allocation.allocationId

    $free = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectCode = $projectCode
        startTime = "${eventDate}T08:00:00+07:00"; endTime = "${eventDate}T09:00:00+07:00"; quantity = 1
    }
    Check "a pending request does not block a free window" ($free.Status -eq 200) "status=$($free.Status)"
    $freeAllocationId = $free.Data.allocation.allocationId

    # ── A-18 receives and decides the resource exception ─────────────────────
    $queueResult = (Call "GET" "/admin/exception-requests" $admin).Data
    $request = $queueResult.items | Where-Object { $_.allocation.id -eq $pendingAllocationId }
    if (-not $request) { $request = $queueResult.items | Where-Object { $_.sourceType -eq "ResourceConflict" -and $_.resource.allocationId -eq $pendingAllocationId } }
    Check "A-18 receives the resource conflict request" ([bool]$request) "pending=$($queueResult.summary.pending)"
    Check "A-18 queue reports the pending count" ($queueResult.summary.pending -ge 1) "pending=$($queueResult.summary.pending)"
    if ($request) {
        Check "A-18 carries the resource context and shortage" ($request.sourceType -eq "ResourceConflict" -and $request.resource.shortage -ge 1) ($request.resource | ConvertTo-Json -Compress -Depth 3)
        Check "A-18 lists the clashing events" (($request.resource.clashing | Measure-Object).Count -ge 1)
        Check "A-18 names the requesting manager" ([bool]$request.requestedByName) $request.requestedByName
        $blankNote = Call "POST" "/admin/exception-requests/$($request.id)/decision" $admin @{ approve = $true; note = " " }
        Check "A-18 requires the decision note" ($blankNote.Status -eq 400) "status=$($blankNote.Status)"
        $decision = Call "POST" "/admin/exception-requests/$($request.id)/decision" $admin @{ approve = $true; note = "Chấp thuận vượt công suất" }
        Check "A-18 approves the resource exception" ($decision.Status -eq 204) "status=$($decision.Status)"
        $decided = ((Call "GET" "/admin/exception-requests" $admin).Data.items | Where-Object { $_.id -eq $request.id })
        Check "A-18 records the decision audit" ($decided.status -eq "Approved" -and $decided.decisionNote -eq "Chấp thuận vượt công suất" -and [bool]$decided.decidedByName) ($decided | ConvertTo-Json -Compress -Depth 3)
        $again = Call "POST" "/admin/exception-requests/$($request.id)/decision" $admin @{ approve = $false; note = "Đổi quyết định" }
        Check "A-18 refuses a second decision" ($again.Status -eq 400) "status=$($again.Status)"
    }
    $approved = (Call "GET" "/resource-allocations/$pendingAllocationId" $manager).Data
    Check "the approved exception marks the allocation" ($approved.status -eq "Exception Approved" -and $approved.isExceptionApproved) $approved.status
    Check "the approved exception keeps the Admin note" ($approved.exceptionNote -eq "Chấp thuận vượt công suất") $approved.exceptionNote

    # ── A-18 rejection releases the pending allocation ──────────────────────
    $secondPending = Call "POST" "/resource-allocations/exception-requests" $manager @{
        resourceId = $resourceId; projectCode = $projectCode
        startTime = "${eventDate}T17:30:00+07:00"; endTime = "${eventDate}T21:30:00+07:00"; quantity = 1
        note = "Đề nghị giữ thêm 1 thiết bị dự phòng"
    }
    Check "M-21 opens a second pending request" ($secondPending.Status -eq 200) "status=$($secondPending.Status)"
    $secondId = $secondPending.Data.allocation.allocationId
    $secondRequest = ((Call "GET" "/admin/exception-requests" $admin).Data.items | Where-Object { $_.resource.allocationId -eq $secondId } | Select-Object -First 1)
    if ($secondRequest) {
        $rejected = Call "POST" "/admin/exception-requests/$($secondRequest.id)/decision" $admin @{ approve = $false; note = "Không đủ thiết bị, giữ nguyên quy tắc" }
        Check "A-18 rejects the resource exception" ($rejected.Status -eq 204) "status=$($rejected.Status)"
        $releasedAfterReject = (Call "GET" "/resource-allocations/$secondId" $manager).Data
        Check "a rejected exception releases the allocation" ($releasedAfterReject.status -eq "Released") $releasedAfterReject.status
    }

    $afterApprove = (Call "GET" "/resource-calendar?from=$eventDate&to=$eventDate" $manager).Data
    Check "M-21 reports the approved over-capacity allocation" ($afterApprove.summary.conflicts -ge 1) "conflicts=$($afterApprove.summary.conflicts)"
    Check "M-21 counts the pending exception" ($afterApprove.summary.pendingExceptions -ge 0)

    # ── M-22 reschedule / substitute ─────────────────────────────────────────
    $conflictingUpdate = Call "PUT" "/resource-allocations/$freeAllocationId" $manager @{
        resourceId = $resourceId; projectCode = $projectCode
        startTime = "${eventDate}T17:00:00+07:00"; endTime = "${eventDate}T22:00:00+07:00"; quantity = 1
    }
    Check "rescheduling into a conflicting window is rejected" ($conflictingUpdate.Status -eq 409) "status=$($conflictingUpdate.Status)"

    $movedUpdate = Call "PUT" "/resource-allocations/$freeAllocationId" $manager @{
        resourceId = $resourceId; projectCode = $projectCode
        startTime = "${eventDate}T09:00:00+07:00"; endTime = "${eventDate}T10:00:00+07:00"; quantity = 1
    }
    Check "rescheduling to a free window succeeds" ($movedUpdate.Status -eq 200 -and $movedUpdate.Data.allocation.status -eq "Reserved") "status=$($movedUpdate.Status)"
    $movedDetail = (Call "GET" "/resource-allocations/$freeAllocationId" $manager).Data
    $movedStart = ([datetime]$movedDetail.startTime).ToUniversalTime()
    $movedEnd = ([datetime]$movedDetail.endTime).ToUniversalTime()
    Check "M-22 shows the new window" ($movedStart.Hour -eq 2 -and $movedEnd.Hour -eq 3) "$($movedDetail.startTime) - $($movedDetail.endTime)"

    $released = Call "DELETE" "/resource-allocations/$freeAllocationId" $manager
    Check "M-22 releases the allocation" ($released.Status -eq 204) "status=$($released.Status)"
    $releasedDetail = (Call "GET" "/resource-allocations/$freeAllocationId" $manager).Data
    Check "the released allocation is no longer reserved" ($releasedDetail.status -eq "Released") $releasedDetail.status
    $editReleased = Call "PUT" "/resource-allocations/$freeAllocationId" $manager @{
        resourceId = $resourceId; projectCode = $projectCode
        startTime = "${eventDate}T09:00:00+07:00"; endTime = "${eventDate}T10:00:00+07:00"; quantity = 1
    }
    Check "a released allocation cannot be edited" ($editReleased.Status -eq 400) "status=$($editReleased.Status)"

    # ── Validation and scope ────────────────────────────────────────────────
    $badTimes = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectCode = $projectCode
        startTime = "${eventDate}T20:00:00+07:00"; endTime = "${eventDate}T19:00:00+07:00"; quantity = 1
    }
    Check "end before start is rejected" ($badTimes.Status -eq 400) "status=$($badTimes.Status)"
    $tooMany = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectCode = $projectCode
        startTime = "${eventDate}T08:00:00+07:00"; endTime = "${eventDate}T09:00:00+07:00"; quantity = $total + 1
    }
    Check "quantity above the total is rejected" ($tooMany.Status -eq 400) "status=$($tooMany.Status)"
    $foreign = Call "POST" "/resource-allocations" $manager @{
        resourceId = $resourceId; projectCode = "WPSM-2026-999"
        startTime = "${eventDate}T08:00:00+07:00"; endTime = "${eventDate}T09:00:00+07:00"; quantity = 1
    }
    Check "a project outside the Manager scope returns 404" ($foreign.Status -eq 404) "status=$($foreign.Status)"
    $forbidden = Call "GET" "/resource-calendar?from=$eventDate&to=$eventDate" $admin
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
