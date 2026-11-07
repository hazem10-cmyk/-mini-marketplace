param([string]$BaseUrl = "http://localhost:5080")

$passed = 0
$failed = 0

function Call {
    param([string]$Method, [string]$Path, [int]$User = 0, [string]$Body = "")

    $curlArgs = @("-s", "-w", "\n%{http_code}", "-X", $Method)
    if ($User -gt 0) { $curlArgs += @("-H", "X-Dev-User: $User") }
    if ($Body -ne "") { $curlArgs += @("-H", "Content-Type: application/json", "--data-binary", "@-") }

    $url = "$BaseUrl$Path"
    if ($Body -ne "") { $raw = $Body | & curl.exe @curlArgs $url }
    else { $raw = & curl.exe @curlArgs $url }

    $text = ($raw -join "`n")
    $cut = $text.LastIndexOf("`n")
    $status = 0
    $json = $null
    if ($cut -ge 0) {
        [void][int]::TryParse($text.Substring($cut + 1).Trim(), [ref]$status)
        $payload = $text.Substring(0, $cut).Trim()
        if ($payload -ne "") {
            try { $json = $payload | ConvertFrom-Json } catch { $json = $null }
        }
    }
    [pscustomobject]@{ Status = $status; Json = $json }
}

function Check {
    param([string]$Name, [bool]$Ok, [string]$Detail = "")
    if ($Ok) {
        $script:passed++
        Write-Host "PASS  $Name" -ForegroundColor Green
    } else {
        $script:failed++
        Write-Host "FAIL  $Name   $Detail" -ForegroundColor Red
    }
}

# Ali orders service 4 from Sara.
function New-Order {
    $r = Call POST "/api/orders" 4 '{"serviceId":4}'
    if ($r.Status -eq 201) { return [int]$r.Json.id }
    return 0
}

# Drives a new order through accept, deliver, and complete.
function New-CompletedOrder {
    $id = New-Order
    if ($id -le 0) { return 0 }
    if ((Call POST "/api/orders/$id/accept" 2).Status -ne 200) { return 0 }
    if ((Call POST "/api/orders/$id/deliver" 2).Status -ne 200) { return 0 }
    if ((Call POST "/api/orders/$id/complete" 4).Status -ne 200) { return 0 }
    return $id
}

$r = Call GET "/api/health"
if ($r.Status -ne 200) {
    Write-Host "The API is not running at $BaseUrl. Start it first (dotnet run) and try again." -ForegroundColor Red
    exit 1
}

$good = '{"rating":4,"comment":"Smoke test review"}'

Write-Host "== Reviews: who can review, and what is valid =="
$flow = New-CompletedOrder
Check "a completed order is ready" ($flow -gt 0)

$r = Call POST "/api/orders/$flow/review" 0 $good
Check "review without identity returns 401" ($r.Status -eq 401) "got $($r.Status)"

$r = Call POST "/api/orders/$flow/review" 2 $good
Check "the seller cannot review (403)" ($r.Status -eq 403) "got $($r.Status)"

$r = Call POST "/api/orders/$flow/review" 5 $good
Check "another buyer gets 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call POST "/api/orders/$flow/review" 1 $good
Check "admin gets 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call POST "/api/orders/9999/review" 4 $good
Check "review on a missing order returns 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call POST "/api/orders/$flow/review" 4 '{"rating":6}'
Check "rating 6 returns 400 with a rating error" ($r.Status -eq 400 -and $r.Json.errors.rating) "got $($r.Status)"

$r = Call POST "/api/orders/$flow/review" 4 '{"comment":"no rating"}'
Check "missing rating returns 400" ($r.Status -eq 400) "got $($r.Status)"

$long = '{"rating":5,"comment":"' + ('x' * 1001) + '"}'
$r = Call POST "/api/orders/$flow/review" 4 $long
Check "a comment of 1001 characters returns 400 with a comment error" ($r.Status -eq 400 -and $r.Json.errors.comment) "got $($r.Status)"

Write-Host "== Reviews: only a Completed order, only once =="
$pending = New-Order
$r = Call POST "/api/orders/$pending/review" 4 $good
Check "review on a Pending order returns 409" ($r.Status -eq 409) "got $($r.Status)"

$cancelled = New-Order
[void](Call POST "/api/orders/$cancelled/cancel" 4)
$r = Call POST "/api/orders/$cancelled/review" 4 $good
Check "review on a Cancelled order returns 409" ($r.Status -eq 409) "got $($r.Status)"

Write-Host "== Reviews: the happy path =="
$before = (Call GET "/api/services/4").Json.reviewCount

$r = Call POST "/api/orders/$flow/review" 4 $good
Check "the buyer reviews a Completed order: 201, rating 4, comment saved" ($r.Status -eq 201 -and $r.Json.rating -eq 4 -and $r.Json.comment -eq "Smoke test review") "got $($r.Status)"

$r = Call POST "/api/orders/$flow/review" 4 $good
Check "a second review on the same order returns 409" ($r.Status -eq 409) "got $($r.Status)"

$r = Call GET "/api/orders/$flow" 4
Check "the order details show the review" ($r.Status -eq 200 -and $r.Json.review.rating -eq 4) "got $($r.Status)"

$r = Call GET "/api/services/4"
Check "the service review count went up by 1" ($r.Status -eq 200 -and $r.Json.reviewCount -eq ($before + 1)) "before $before, now $($r.Json.reviewCount)"

$r = Call GET "/api/services/4/reviews?pageSize=5"
Check "the review appears in the service reviews (newest first)" ($r.Status -eq 200 -and ($r.Json.items | Where-Object { $_.comment -eq "Smoke test review" -and $_.buyerName -eq "Ali" })) "got $($r.Status)"

Write-Host "== Reviews: the comment is optional =="
$optional = New-CompletedOrder
Check "another completed order is ready" ($optional -gt 0)

$r = Call POST "/api/orders/$optional/review" 4 '{"rating":3}'
Check "a review with only a rating returns 201 and a null comment" ($r.Status -eq 201 -and $r.Json.rating -eq 3 -and $null -eq $r.Json.comment) "got $($r.Status)"

Write-Host "== Reviews: five simultaneous reviews on one order =="
$raceOrder = New-CompletedOrder
Check "a completed order is ready for the race" ($raceOrder -gt 0)

if ($raceOrder -gt 0) {
    $jobs = 1..5 | ForEach-Object {
        Start-Job -ArgumentList $BaseUrl, $raceOrder -ScriptBlock {
            param($base, $id)
            '{"rating":5,"comment":"Race review"}' | & curl.exe -s -o NUL -w "%{http_code}" -X POST -H "Content-Type: application/json" -H "X-Dev-User: 4" --data-binary "@-" "$base/api/orders/$id/review"
        }
    }
    $codes = @($jobs | Wait-Job | Receive-Job)
    $jobs | Remove-Job

    $createdCount = @($codes | Where-Object { "$_" -eq "201" }).Count
    $conflictCount = @($codes | Where-Object { "$_" -eq "409" }).Count
    Check "exactly one review is created (201) and four get 409" ($createdCount -eq 1 -and $conflictCount -eq 4) "got: $($codes -join ', ')"
}

Write-Host ""
Write-Host "Passed: $passed   Failed: $failed"
if ($failed -gt 0) { exit 1 }