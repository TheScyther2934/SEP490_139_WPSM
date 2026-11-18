# FE-02 Specification integration checks (M-05 Specification Editor & Freeze Baseline, C-02 read-only
# baseline) on a throwaway database. Creates and drops WPSM_FE02_Test only; the application database is
# never touched. Covers UC-20: spec entry, PriceFrame/Core-combo validation, baseline freeze, Master
# Roadmap generation and the read-only customer view.
$ErrorActionPreference = "Stop"
$Here = $PSScriptRoot
$Root = (Resolve-Path "$Here\..").Path
$Out = Join-Path $env:TEMP "wpsm-fe02-test"
$Db = "WPSM_FE02_Test"
$Sql = "(localdb)\MSSQLLocalDB"
$Port = 5315
$Base = "http://localhost:$Port/api/wpsm"
$Password = "Fe02-Manager-2026"
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

    # ── M-03: a project whose event is T-30, with a Customer account ─────────
    $eventDate = (Get-Date).Date.AddDays(30).ToString("yyyy-MM-dd")
    $halls = (Call "GET" "/halls?date=$eventDate" $manager).Data
    $hall = $halls | Where-Object { ($_.slots | Where-Object { $_.available }) } | Select-Object -First 1
    $slot = $hall.slots | Where-Object { $_.available } | Select-Object -First 1
    $email = "fe02.$([guid]::NewGuid().ToString('N').Substring(0,8))@example.com"
    $created = Call "POST" "/projects" $manager @{
        coupleName = "FE02 Test Couple"; customerEmail = $email; customerPhone = "0900002222"
        hall = $hall.name; slot = $slot.slotName; eventDate = $eventDate
        baseValue = 100000000; nrdAmount = 30000000; paymentMethod = "Cash"
    }
    Check "M-03 creates the project" ($created.Status -eq 201) ($created.Data | ConvertTo-Json -Compress -Depth 3)
    $projectCode = $created.Data.project.id
    $temporaryPassword = $created.Data.temporaryPassword

    # ── reference data for the itemised specification ─────────────────────────
    $snapshot = (Call "GET" "/bootstrap?projectCode=$projectCode" $manager).Data
    $catalog = $snapshot.catalog | Where-Object { $_.active }
    $picks = @()
    # The portal maps catalog categories to their Vietnamese display names (WpsmCategoryNames).
    foreach ($category in @("Sảnh", "Ẩm thực", "Trang trí", "MC", "Âm thanh & ánh sáng")) {
        $item = $catalog | Where-Object { $_.category -eq $category } | Select-Object -First 1
        if ($item) { $picks += $item }
    }
    Check "catalog exposes the five Core Combo categories" ($picks.Count -eq 5) "categories=$(($picks | ForEach-Object { $_.category }) -join ',')"

    $lines = @()
    foreach ($item in $picks) {
        $lines += @{
            catalogItemId = [int]$item.id
            description = "$($item.name) - đặc tả tiệc cưới"
            quantity = if ($item.category -eq "Ẩm thực") { 30 } else { 1 }
            unitPrice = $item.basePrice
        }
    }
    $firstHall = $picks[0]
    $catering = $picks[1]

    # ── M-05: enter the specification ────────────────────────────────────────
    $saved = Call "PUT" "/projects/$projectCode/specification" $manager @{
        summary = "Đặc tả tiệc cưới 30 bàn"; guestCount = 300; items = $lines
    }
    Check "M-05 saves the itemised specification" ($saved.Status -eq 200) ($saved.Data | ConvertTo-Json -Compress -Depth 2)
    Check "M-05 persists all five specification lines" (($saved.Data.specification | Measure-Object).Count -eq 5) "lines=$(($saved.Data.specification | Measure-Object).Count)"
    Check "M-05 derives the guest count from the catering line" ($saved.Data.guestCount -eq 30) "guestCount=$($saved.Data.guestCount)"

    $negative = @(@{ catalogItemId = [int]$catering.id; description = "Giá ngoài khung"; quantity = 30; unitPrice = [decimal]$catering.basePrice * 10 })
    $overpriced = Call "PUT" "/projects/$projectCode/specification" $manager @{ summary = "Sai giá"; guestCount = 300; items = $negative }
    Check "GBR-02 rejects a unit price outside the PriceFrame" ($overpriced.Status -eq 400) "status=$($overpriced.Status)"

    $duplicate = @(
        @{ catalogItemId = [int]$catering.id; description = "Dòng 1"; quantity = 30; unitPrice = $catering.basePrice },
        @{ catalogItemId = [int]$catering.id; description = "Dòng 2"; quantity = 31; unitPrice = $catering.basePrice }
    )
    $duplicated = Call "PUT" "/projects/$projectCode/specification" $manager @{ summary = "Trùng"; guestCount = 300; items = $duplicate }
    Check "M-05 rejects a duplicated catalog item" ($duplicated.Status -eq 400) "status=$($duplicated.Status)"

    $zeroQuantity = @(@{ catalogItemId = [int]$catering.id; description = "Số lượng 0"; quantity = 0; unitPrice = $catering.basePrice })
    $invalidQuantity = Call "PUT" "/projects/$projectCode/specification" $manager @{ summary = "Số lượng"; guestCount = 300; items = $zeroQuantity }
    Check "M-05 rejects a non-positive quantity" ($invalidQuantity.Status -eq 400) "status=$($invalidQuantity.Status)"

    # ── M-05: freeze the baseline and generate the Master Roadmap ────────────
    $frozen = Call "POST" "/projects/$projectCode/freeze" $manager
    Check "M-05 freezes the original baseline" ($frozen.Status -eq 200 -and $frozen.Data.status -eq "Baseline Frozen") "status=$($frozen.Status)/$($frozen.Data.status)"
    Check "M-05 records the freeze timestamp" ([bool]$frozen.Data.baselineFrozenAt) $frozen.Data.baselineFrozenAt

    $roadmap = (Call "GET" "/projects/$projectCode/roadmap" $manager).Data
    Check "M-05 generates the 11 Master Roadmap milestones" ($roadmap.totalTasks -eq 11 -and ($roadmap.tasks | Measure-Object).Count -eq 11) "tasks=$($roadmap.totalTasks)"
    Check "the roadmap covers T-90, T-30, T-7 and Event Day" (($roadmap.milestoneGroups | Measure-Object).Count -eq 4) (($roadmap.milestoneGroups | ForEach-Object { $_.milestone }) -join ',')
    Check "the roadmap is ordered by due date" (
        ([datetime]$roadmap.tasks[0].dueDate) -le ([datetime]$roadmap.tasks[-1].dueDate)
    ) "$($roadmap.tasks[0].dueDate) .. $($roadmap.tasks[-1].dueDate)"

    # ── C-02: the customer sees the frozen baseline read-only ────────────────
    $customer = Login $email $temporaryPassword
    $changed = Call "POST" "/auth/change-password" $customer @{
        currentPassword = $temporaryPassword; newPassword = "Cust0mer-FE02-2026"; confirmPassword = "Cust0mer-FE02-2026"
    }
    Check "customer changes the temporary password" ($changed.Status -eq 200) "status=$($changed.Status)"
    $customer = $changed.Data.accessToken
    $customerView = (Call "GET" "/bootstrap?projectCode=$projectCode" $customer).Data
    Check "C-02 shows the frozen specification to the customer" (($customerView.project.specification | Measure-Object).Count -eq 5) "lines=$(($customerView.project.specification | Measure-Object).Count)"
    Check "C-02 shows the baseline status" ($customerView.project.status -eq "Baseline Frozen") $customerView.project.status
    Check "C-02 shows the 11 roadmap milestones" (($customerView.roadmap | Measure-Object).Count -eq 11) "tasks=$(($customerView.roadmap | Measure-Object).Count)"

    $customerEdit = Call "PUT" "/projects/$projectCode/specification" $customer @{ summary = "Sửa"; guestCount = 300; items = $lines }
    Check "C-02 is read-only (customer cannot edit)" ($customerEdit.Status -eq 403) "status=$($customerEdit.Status)"

    # ── GBR-01: a frozen baseline cannot be edited from the Manager screen ───
    $frozenEdit = Call "PUT" "/projects/$projectCode/specification" $manager @{ summary = "Sửa sau chốt"; guestCount = 300; items = $lines }
    Check "GBR-01 blocks edits after the freeze (MSG09)" ($frozenEdit.Status -eq 400) "status=$($frozenEdit.Status)"
    $secondFreeze = Call "POST" "/projects/$projectCode/freeze" $manager
    Check "a second freeze is rejected" ($secondFreeze.Status -eq 400) "status=$($secondFreeze.Status)"

    # ── scope and authentication ────────────────────────────────────────────
    $anonymous = Call "PUT" "/projects/$projectCode/specification" $null @{ summary = "x"; guestCount = 1; items = @() }
    Check "the specification API requires authentication" ($anonymous.Status -eq 401) "status=$($anonymous.Status)"
    $admin = Login "admin@test.local" $Password
    $adminEdit = Call "PUT" "/projects/$projectCode/specification" $admin @{ summary = "x"; guestCount = 1; items = @() }
    Check "Admin cannot edit a Manager specification" ($adminEdit.Status -eq 403) "status=$($adminEdit.Status)"
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

Write-Host "FE-02 checks failed: $bad"
exit $(if ($bad) { 1 } else { 0 })
