# FE-05 Change Request API checks (C-05, C-06, M-07, M-08) on a throwaway database.
# Creates and drops WPSM_FE05_Test only; the application database is never touched.
$ErrorActionPreference = "Stop"
$Here = $PSScriptRoot
$Root = (Resolve-Path "$Here\..").Path
$Out = Join-Path $env:TEMP "wpsm-fe05"
$Db = "WPSM_FE05_Test"
$Sql = "(localdb)\MSSQLLocalDB"
$Port = 5311
$Base = "http://localhost:$Port/api/wpsm"
$Password = "Fe05-Manager-2026"
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
# Decode explicitly as UTF-8: PowerShell 5.1 falls back to ISO-8859-1 for some JSON responses and
# would mangle Vietnamese category names.
function Read-Json([byte[]]$bytes) {
    if ($null -eq $bytes -or $bytes.Length -eq 0) { return $null }
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if (-not $text) { return $null }
    try { return $text | ConvertFrom-Json } catch { return $text }
}
# Windows PowerShell 5.1 has no -SkipHttpErrorCheck, so the error response body is read from the exception.
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
        # Windows PowerShell exposes HttpWebResponse; PowerShell 7 exposes HttpResponseMessage.
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
    Check "manager signs in" ([bool]$manager)
    $admin = Login "admin@test.local" $Password
    Check "admin signs in" ([bool]$admin)

    # ── A project whose event is 10 days away → inside the T-14 penalty window ──
    $eventDate = (Get-Date).Date.AddDays(10).ToString("yyyy-MM-dd")
    $halls = (Call "GET" "/halls?date=$eventDate" $manager).Data
    $hall = $halls | Where-Object { ($_.slots | Where-Object { $_.available }) } | Select-Object -First 1
    $slot = $hall.slots | Where-Object { $_.available } | Select-Object -First 1
    $email = "fe05.$([guid]::NewGuid().ToString('N').Substring(0,8))@example.com"
    $created = Call "POST" "/projects" $manager @{
        coupleName = "FE05 Test Couple"; customerEmail = $email; customerPhone = "0900001234"
        hall = $hall.name; slot = $slot.slotName; eventDate = $eventDate
        baseValue = 100000000; nrdAmount = 50000000; paymentMethod = "Cash"
    }
    Check "M-03 creates the project" ($created.Status -eq 201) ($created.Data | ConvertTo-Json -Compress)
    $projectCode = $created.Data.project.id
    $tempPassword = $created.Data.temporaryPassword

    $frozen = Call "POST" "/projects/$projectCode/freeze" $manager
    Check "M-05 freezes the baseline" ($frozen.Status -eq 200) ($frozen.Data | ConvertTo-Json -Compress -Depth 3)

    # ── C-06 form data ────────────────────────────────────────────────────────
    $form = (Call "GET" "/projects/$projectCode/change-requests/form" $manager).Data
    Check "C-06 form lists the frozen specification" (($form.specItems | Measure-Object).Count -ge 5)
    Check "C-06 form reports the 5% T-day rate" ($form.applicablePenaltyRate -eq 5) "rate=$($form.applicablePenaltyRate)"
    Check "C-06 form is not blocked" (-not $form.blockedReason) $form.blockedReason
    $catering = $form.specItems | Where-Object { $_.unitPrice -eq 3500000 } | Select-Object -First 1
    Check "C-06 form contains the catering line" ([bool]$catering) (($form.specItems | ForEach-Object { $_.category }) -join ",")

    # PriceFrame guard (GBR-02 / MSG06)
    $overpriced = Call "POST" "/projects/$projectCode/change-requests" $manager @{
        type = "Menu change"; description = "Giá ngoài khung"; submit = $true
        items = @(@{ specItemId = $catering.specItemId; newQuantity = $catering.quantity; newUnitPrice = $catering.maxPrice + 1 })
    }
    Check "GBR-02 rejects a price outside the PriceFrame" ($overpriced.Status -eq 400) "status=$($overpriced.Status)"

    $badType = Call "POST" "/projects/$projectCode/change-requests" $manager @{
        type = "Nonsense"; description = "Loại sai"; submit = $true
        items = @(@{ specItemId = $catering.specItemId; newQuantity = 31; newUnitPrice = $catering.unitPrice })
    }
    Check "C-06 rejects an unknown CR type" ($badType.Status -eq 400) "status=$($badType.Status)"

    # ── Manager creates a CR on the Customer's behalf (M-07) ──────────────────
    $newQty = $catering.quantity + 10
    $delta = ($newQty - $catering.quantity) * $catering.unitPrice
    $penalty = [math]::Round([math]::Abs($delta) * 0.05, 2)
    $cr = Call "POST" "/projects/$projectCode/change-requests" $manager @{
        type = "Menu change"; description = "Tăng 10 bàn tiệc theo yêu cầu qua điện thoại"; submit = $true
        items = @(@{ specItemId = $catering.specItemId; newQuantity = $newQty; newUnitPrice = $catering.unitPrice })
    }
    Check "M-07 creates a CR on behalf of the customer" ($cr.Status -eq 201) ($cr.Data | ConvertTo-Json -Compress -Depth 3)
    $crCode = $cr.Data.code
    Check "created CR is Negotiating" ($cr.Data.status -eq "Negotiating") $cr.Data.status
    Check "GBR-03 price delta is correct" ($cr.Data.cost.priceDelta -eq $delta) "delta=$($cr.Data.cost.priceDelta) expected=$delta"
    Check "GBR-03 penalty is 5% of the delta" ($cr.Data.cost.penaltyFee -eq $penalty) "penalty=$($cr.Data.cost.penaltyFee) expected=$penalty"
    Check "CR records the Manager as creator" ($cr.Data.createdByRole -eq "Manager") $cr.Data.createdByRole

    # ── M-07 list ────────────────────────────────────────────────────────────
    $list = (Call "GET" "/projects/$projectCode/change-requests" $manager).Data
    Check "M-07 lists the ticket" (($list.items | Where-Object { $_.code -eq $crCode } | Measure-Object).Count -eq 1)
    Check "M-07 summary counts the Negotiating ticket" ($list.summary.negotiating -eq 1) "negotiating=$($list.summary.negotiating)"

    # ── M-08 counter-proposal ────────────────────────────────────────────────
    $counterQty = $catering.quantity + 5
    $counter = Call "POST" "/projects/$projectCode/change-requests/$crCode/counter" $manager @{
        items = @(@{ specItemId = $catering.specItemId; newQuantity = $counterQty; newUnitPrice = $catering.unitPrice })
        note = "Đề xuất tăng 5 bàn theo công suất sảnh"
    }
    Check "M-08 counter-proposal is accepted" ($counter.Status -eq 200) "status=$($counter.Status)"
    $counterDelta = ($counterQty - $catering.quantity) * $catering.unitPrice
    Check "counter-proposal recalculates the delta" ($counter.Data.cost.priceDelta -eq $counterDelta) "delta=$($counter.Data.cost.priceDelta)"
    Check "counter-proposal is logged in CR History" (($counter.Data.history | Where-Object { $_.action -eq "CounterProposal" } | Measure-Object).Count -eq 1)

    # ── UC-47 special exception (A-18) ───────────────────────────────────────
    $exception = Call "POST" "/projects/$projectCode/change-requests/$crCode/exception" $manager @{
        note = "Đề nghị miễn phí phạt do sảnh còn trống"
    }
    Check "M-08 requests an Admin penalty waiver" ($exception.Status -eq 200) "status=$($exception.Status)"
    Check "CR reports the pending exception" ($exception.Data.pendingException -eq "Pending") $exception.Data.pendingException
    $queue = (Call "GET" "/admin/exception-requests" $admin).Data
    $pending = $queue.items | Where-Object { $_.cr.crCode -eq $crCode }
    Check "A-18 receives the waiver request" ([bool]$pending) ($queue.items | ConvertTo-Json -Compress -Depth 3)
    if ($pending) {
        Check "A-18 request carries the CR context" ($pending.sourceType -eq "CrPenaltyWaiver" -and $pending.cr.penaltyFee -gt 0) ($pending.cr | ConvertTo-Json -Compress)
        $decided = Call "POST" "/admin/exception-requests/$($pending.id)/decision" $admin @{ approve = $true; note = "Chấp thuận miễn phạt" }
        Check "A-18 approves the waiver" ($decided.Status -eq 204) "status=$($decided.Status)"
        $decidedRequest = ((Call "GET" "/admin/exception-requests" $admin).Data.items | Where-Object { $_.cr.crCode -eq $crCode })
        Check "A-18 records the decision audit" ($decidedRequest.status -eq "Approved" -and $decidedRequest.decisionNote -eq "Chấp thuận miễn phạt" -and [bool]$decidedRequest.decidedByName) ($decidedRequest | ConvertTo-Json -Compress -Depth 3)
        $afterWaiver = (Call "GET" "/projects/$projectCode/change-requests/$crCode" $manager).Data
        Check "approved waiver clears the penalty fee" ($afterWaiver.cost.penaltyFee -eq 0) "penalty=$($afterWaiver.cost.penaltyFee)"
        Check "approved waiver is in CR History" (($afterWaiver.history | Where-Object { $_.action -eq "ExceptionApproved" } | Measure-Object).Count -eq 1)
    }

    # ── Manager approval, then Customer approval (GBR-04) ────────────────────
    $managerApprove = Call "POST" "/projects/$projectCode/change-requests/$crCode/approve" $manager @{ note = $null }
    Check "M-08 records the Manager approval" ($managerApprove.Status -eq 200) "status=$($managerApprove.Status)"
    Check "CR waits for the Customer after Manager approval" ($managerApprove.Data.status -eq "Negotiating") $managerApprove.Data.status
    $doubleApprove = Call "POST" "/projects/$projectCode/change-requests/$crCode/approve" $manager @{ note = $null }
    Check "a second approval by the Manager is rejected" ($doubleApprove.Status -eq 400) "status=$($doubleApprove.Status)"
    $rejectWithoutReason = Call "POST" "/projects/$projectCode/change-requests/$crCode/reject" $manager @{ reason = " " }
    Check "M-08 requires a rejection reason" ($rejectWithoutReason.Status -eq 400) "status=$($rejectWithoutReason.Status)"

    # ── Customer: C-05 list and C-08 consent via the API ────────────────────
    $customer = Login $email $tempPassword
    $changed = Call "POST" "/auth/change-password" $customer @{
        currentPassword = $tempPassword; newPassword = "Cust0mer-FE05-2026"; confirmPassword = "Cust0mer-FE05-2026"
    }
    Check "customer changes the temporary password" ($changed.Status -eq 200) "status=$($changed.Status)"
    $customer = $changed.Data.accessToken

    $customerList = (Call "GET" "/customer/change-requests" $customer).Data
    Check "C-05 lists the customer's CR" (($customerList.items | Where-Object { $_.code -eq $crCode } | Measure-Object).Count -eq 1)
    Check "C-05 shows the negotiation status" (($customerList.items | Where-Object { $_.code -eq $crCode }).status -eq "Negotiating")

    $customerForm = (Call "GET" "/customer/change-requests/form" $customer).Data
    $customerItem = $customerForm.specItems | Where-Object { $_.unitPrice -eq 3500000 } | Select-Object -First 1
    $customerCr = Call "POST" "/customer/change-requests" $customer @{
        type = "Table count"; description = "Điều chỉnh số bàn theo xác nhận của gia đình"; submit = $false
        items = @(@{ specItemId = $customerItem.specItemId; newQuantity = $customerItem.quantity + 1; newUnitPrice = $customerItem.unitPrice })
    }
    Check "C-06 saves a draft" ($customerCr.Status -eq 201) ($customerCr.Data | ConvertTo-Json -Compress -Depth 3)
    Check "draft stays Draft" ($customerCr.Data.status -eq "Draft") $customerCr.Data.status
    $submitted = Call "POST" "/customer/change-requests/$($customerCr.Data.code)/submit" $customer
    Check "C-06 submits the draft" ($submitted.Status -eq 200 -and $submitted.Data.status -eq "Negotiating") "status=$($submitted.Status)/$($submitted.Data.status)"

    $customerApprove = Call "POST" "/customer/change-requests/$crCode/approve" $customer @{ note = $null }
    Check "customer approval completes the mutual consent" ($customerApprove.Data.status -eq "Approved") $customerApprove.Data.status
    Check "approval generates the contract addendum" ([bool]$customerApprove.Data.addendumCode) $customerApprove.Data.addendumCode
    Check "approval logs both approvals and the addendum" (
        ($customerApprove.Data.history | Where-Object { $_.action -in @("ManagerApproved", "CustomerApproved", "Approved", "AddendumGenerated") } | Measure-Object).Count -eq 4
    ) ($customerApprove.Data.history | ConvertTo-Json -Compress -Depth 3)

    $project = (Call "GET" "/projects/$projectCode" $manager).Data
    $expectedTotal = 100000000 + $counterDelta
    Check "approved CR updates the project total" ($project.currentTotal -eq $expectedTotal) "total=$($project.currentTotal) expected=$expectedTotal"
    $addenda = (Call "GET" "/addenda?projectCode=$projectCode" $manager).Data
    Check "the addendum is listed for the project" (($addenda | Measure-Object).Count -eq 1) ($addenda | ConvertTo-Json -Compress -Depth 3)

    # Rejection closes a ticket and keeps the audit trail (GBR-13)
    $rejectCustomer = Call "POST" "/customer/change-requests/$($submitted.Data.code)/reject" $customer @{ reason = "Không cần điều chỉnh nữa" }
    Check "customer rejects with a reason" ($rejectCustomer.Status -eq 200 -and $rejectCustomer.Data.status -eq "Rejected") "status=$($rejectCustomer.Status)/$($rejectCustomer.Data.status)"
    $afterReject = (Call "GET" "/customer/change-requests" $customer).Data
    Check "rejected CR stays in the list for audit" (($afterReject.items | Where-Object { $_.code -eq $submitted.Data.code }).status -eq "Rejected")
    Check "summary separates approved and rejected" ($afterReject.summary.approved -eq 1 -and $afterReject.summary.rejected -eq 1) ($afterReject.summary | ConvertTo-Json -Compress)

    # Cross-account isolation (GBR-12)
    $otherManager = Login "admin@test.local" $Password
    $forbidden = Call "GET" "/projects/$projectCode/change-requests" $otherManager
    Check "a non-assigned account cannot read the CR list" ($forbidden.Status -in @(401, 403, 404)) "status=$($forbidden.Status)"
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

Write-Host "FE-05 checks failed: $bad"
exit $(if ($bad) { 1 } else { 0 })
