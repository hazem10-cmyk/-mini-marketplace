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

$r = Call GET "/api/health"
if ($r.Status -ne 200) {
    Write-Host "The API is not running at $BaseUrl. Start it first (dotnet run) and try again." -ForegroundColor Red
    exit 1
}

Write-Host "== Identity =="
Check "health returns 200" ($r.Status -eq 200)

$r = Call GET "/api/auth/me"
Check "me without identity returns 401" ($r.Status -eq 401) "got $($r.Status)"

$r = Call GET "/api/auth/me" 2
Check "me as Sara returns 200 and role Seller" ($r.Status -eq 200 -and $r.Json.role -eq "Seller") "got $($r.Status)"

$r = Call GET "/api/auth/me" 99
Check "me with an unknown user returns 401" ($r.Status -eq 401) "got $($r.Status)"

Write-Host "== Public services =="
$r = Call GET "/api/services"
Check "services list returns 200 with at least 6 services" ($r.Status -eq 200 -and $r.Json.totalItems -ge 6) "got $($r.Status)"

$r = Call GET "/api/services/1"
Check "service 1 details (title, 1 review, rating 5)" ($r.Status -eq 200 -and $r.Json.title -eq "Professional logo design" -and $r.Json.reviewCount -eq 1 -and $r.Json.averageRating -eq 5) "got $($r.Status)"

$r = Call GET "/api/services/7"
Check "inactive service 7 returns 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call GET "/api/services/abc"
Check "service id abc returns 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call GET "/api/services?pageSize=100"
Check "pageSize=100 returns 400" ($r.Status -eq 400) "got $($r.Status)"

Write-Host "== Seller services =="
$serviceOk = '{"title":"Smoke test service","description":"Created by the smoke test.","price":30,"deliveryDays":4}'
$serviceBad = '{"title":"abc","description":"","price":0,"deliveryDays":99}'
$serviceUpdate = '{"title":"Smoke test service v2","description":"Updated by the smoke test.","price":35.5,"deliveryDays":5}'

$r = Call POST "/api/services" 4 $serviceOk
Check "POST service as a buyer returns 403" ($r.Status -eq 403) "got $($r.Status)"

$r = Call POST "/api/services" 2 $serviceBad
Check "POST invalid service returns 400 with field errors" ($r.Status -eq 400 -and $r.Json.errors.title -and $r.Json.errors.price) "got $($r.Status)"

$r = Call POST "/api/services" 2 $serviceOk
$newId = $r.Json.id
Check "POST valid service returns 201 with an id" ($r.Status -eq 201 -and $newId -gt 0) "got $($r.Status)"

if ($newId -gt 0) {
    $r = Call GET "/api/services/mine" 2
    Check "mine lists the new service" ($r.Status -eq 200 -and ($r.Json.items | Where-Object { $_.id -eq $newId })) "got $($r.Status)"

    $r = Call PUT "/api/services/$newId" 2 $serviceUpdate
    Check "PUT own service returns 200 with the new price" ($r.Status -eq 200 -and $r.Json.price -eq 35.5) "got $($r.Status)"

    $r = Call PUT "/api/services/2" 2 $serviceUpdate
    Check "PUT someone else's service returns 403" ($r.Status -eq 403) "got $($r.Status)"

    $r = Call PUT "/api/services/9999" 2 $serviceUpdate
    Check "PUT missing service returns 404" ($r.Status -eq 404) "got $($r.Status)"

    $r = Call DELETE "/api/services/2" 2
    Check "DELETE someone else's service returns 403" ($r.Status -eq 403) "got $($r.Status)"

    $r = Call DELETE "/api/services/$newId" 2
    Check "DELETE own service returns 204" ($r.Status -eq 204) "got $($r.Status)"

    $r = Call DELETE "/api/services/$newId" 2
    Check "DELETE again returns 404" ($r.Status -eq 404) "got $($r.Status)"

    $r = Call GET "/api/services/$newId"
    Check "deleted service is hidden (404)" ($r.Status -eq 404) "got $($r.Status)"
}

Write-Host "== Orders: lists =="
$r = Call GET "/api/orders"
Check "orders list without identity returns 401" ($r.Status -eq 401) "got $($r.Status)"

$r = Call GET "/api/orders" 1
Check "orders list as admin returns 403" ($r.Status -eq 403) "got $($r.Status)"

$r = Call GET "/api/orders?status=Completed&pageSize=50" 4
Check "Ali sees completed order 1 with Sara as the other party" ($r.Status -eq 200 -and ($r.Json.items | Where-Object { $_.id -eq 1 -and $_.otherPartyName -eq "Sara" })) "got $($r.Status)"

$r = Call GET "/api/orders?status=Completed&pageSize=50" 2
Check "Sara sees completed order 1 with Ali as the other party" ($r.Status -eq 200 -and ($r.Json.items | Where-Object { $_.id -eq 1 -and $_.otherPartyName -eq "Ali" })) "got $($r.Status)"

$r = Call GET "/api/orders?status=pending" 4
Check "status filter ignores letter case" ($r.Status -eq 200 -and $r.Json.totalItems -ge 1) "got $($r.Status)"

$r = Call GET "/api/orders?status=3" 4
Check "status=3 returns 400" ($r.Status -eq 400) "got $($r.Status)"

$r = Call GET "/api/orders?status=foo" 4
Check "status=foo returns 400" ($r.Status -eq 400) "got $($r.Status)"

Write-Host "== Orders: details =="
$r = Call GET "/api/orders/1" 4
Check "order 1 as its buyer (price 20, Completed, review 5)" ($r.Status -eq 200 -and $r.Json.priceAtOrder -eq 20 -and $r.Json.status -eq "Completed" -and $r.Json.buyer.displayName -eq "Ali" -and $r.Json.seller.displayName -eq "Sara" -and $r.Json.review.rating -eq 5) "got $($r.Status)"

$r = Call GET "/api/orders/1" 2
Check "order 1 as its seller returns 200" ($r.Status -eq 200) "got $($r.Status)"

$r = Call GET "/api/orders/1" 5
Check "order 1 as another buyer returns 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call GET "/api/orders/1" 1
Check "order 1 as admin returns 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call GET "/api/orders/9999" 4
Check "missing order returns 404" ($r.Status -eq 404) "got $($r.Status)"

Write-Host "== Orders: creation =="
$r = Call POST "/api/orders" 0 '{"serviceId":4}'
Check "POST order without identity returns 401" ($r.Status -eq 401) "got $($r.Status)"

$r = Call POST "/api/orders" 2 '{"serviceId":4}'
Check "POST order as a seller returns 403" ($r.Status -eq 403) "got $($r.Status)"

$r = Call POST "/api/orders" 4 '{}'
Check "POST order without serviceId returns 400" ($r.Status -eq 400) "got $($r.Status)"

$r = Call POST "/api/orders" 4 '{"serviceId":7}'
Check "POST order for an inactive service returns 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call POST "/api/orders" 4 '{"serviceId":9999}'
Check "POST order for a missing service returns 404" ($r.Status -eq 404) "got $($r.Status)"

Write-Host "== Orders: the price is copied (decision D3) =="
$banner15 = '{"title":"Social media banner pack","description":"Five banners sized for the main platforms.","price":15,"deliveryDays":2}'
$banner20 = '{"title":"Social media banner pack","description":"Five banners sized for the main platforms.","price":20,"deliveryDays":2}'

[void](Call PUT "/api/services/4" 2 $banner15)
$r = Call POST "/api/orders" 4 '{"serviceId":4}'
$orderId = $r.Json.id
Check "Ali orders service 4: 201, Pending, price copied (15)" ($r.Status -eq 201 -and $r.Json.status -eq "Pending" -and $r.Json.priceAtOrder -eq 15) "got $($r.Status)"

if ($orderId -gt 0) {
    $r = Call PUT "/api/services/4" 2 $banner20
    Check "Sara raises the service price to 20" ($r.Status -eq 200 -and $r.Json.price -eq 20) "got $($r.Status)"

    $r = Call GET "/api/orders/$orderId" 4
    Check "the order keeps its old price (15)" ($r.Status -eq 200 -and $r.Json.priceAtOrder -eq 15) "got $($r.Status)"

    [void](Call PUT "/api/services/4" 2 $banner15)
}

Write-Host ""
Write-Host "Passed: $passed   Failed: $failed"
if ($failed -gt 0) { exit 1 }