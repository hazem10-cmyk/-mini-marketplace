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

# Start from a known state: Omar (user 3) and service 6 are active.
[void](Call POST "/api/admin/users/3/activate" 1)
[void](Call POST "/api/admin/services/6/activate" 1)

Write-Host "== Admin: access control =="
$r = Call GET "/api/admin/users"
Check "users list without identity returns 401" ($r.Status -eq 401) "got $($r.Status)"

$r = Call GET "/api/admin/users" 4
Check "users list as a buyer returns 403" ($r.Status -eq 403) "got $($r.Status)"

$r = Call GET "/api/admin/users" 2
Check "users list as a seller returns 403" ($r.Status -eq 403) "got $($r.Status)"

$r = Call GET "/api/admin/services" 4
Check "services list as a buyer returns 403" ($r.Status -eq 403) "got $($r.Status)"

$r = Call POST "/api/admin/users/3/deactivate" 2
Check "a seller cannot deactivate a user (403)" ($r.Status -eq 403) "got $($r.Status)"

$r = Call POST "/api/admin/services/1/deactivate" 4
Check "a buyer cannot deactivate a service (403)" ($r.Status -eq 403) "got $($r.Status)"

$r = Call POST "/api/admin/users/3/deactivate"
Check "deactivate without identity returns 401" ($r.Status -eq 401) "got $($r.Status)"

$r = Call GET "/api/auth/me" 3
Check "the refused attempts changed nothing: Omar still logs in" ($r.Status -eq 200) "got $($r.Status)"

Write-Host "== Admin: lists =="
$r = Call GET "/api/admin/users?pageSize=50" 1
Check "users list: 200, at least 5 users, Sara is an active Seller" ($r.Status -eq 200 -and $r.Json.totalItems -ge 5 -and ($r.Json.items | Where-Object { $_.displayName -eq "Sara" -and $_.role -eq "Seller" -and $_.isActive -eq $true -and $_.email -eq "sara@marketplace.test" })) "got $($r.Status)"

$r = Call GET "/api/admin/users?pageSize=100" 1
Check "users list with pageSize=100 returns 400" ($r.Status -eq 400) "got $($r.Status)"

$r = Call GET "/api/admin/services?pageSize=50" 1
Check "services list: 200 and it includes the inactive service 7" ($r.Status -eq 200 -and ($r.Json.items | Where-Object { $_.id -eq 7 -and $_.isActive -eq $false })) "got $($r.Status)"

Write-Host "== Admin: users =="
$r = Call POST "/api/admin/users/1/deactivate" 1
Check "an admin cannot deactivate themselves (409)" ($r.Status -eq 409) "got $($r.Status)"

$r = Call POST "/api/admin/users/9999/deactivate" 1
Check "deactivate a missing user returns 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call POST "/api/admin/users/9999/activate" 1
Check "activate a missing user returns 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call GET "/api/services/2"
Check "before: Omar's service 2 is public" ($r.Status -eq 200) "got $($r.Status)"

$r = Call POST "/api/admin/users/3/deactivate" 1
Check "the admin deactivates Omar: 204" ($r.Status -eq 204) "got $($r.Status)"

$r = Call POST "/api/admin/users/3/deactivate" 1
Check "deactivating again is fine: 204 (idempotent)" ($r.Status -eq 204) "got $($r.Status)"

$r = Call GET "/api/auth/me" 3
Check "a deactivated user cannot log in (401)" ($r.Status -eq 401) "got $($r.Status)"

$r = Call GET "/api/services/2"
Check "a deactivated seller's service disappears (404)" ($r.Status -eq 404) "got $($r.Status)"

$r = Call GET "/api/services?q=translation"
Check "and it is missing from the public search" ($r.Status -eq 200 -and $r.Json.totalItems -eq 0) "got $($r.Status)"

$r = Call POST "/api/orders" 4 '{"serviceId":2}'
Check "ordering from a deactivated seller returns 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call GET "/api/admin/users?pageSize=50" 1
Check "the admin list shows Omar as inactive" ($r.Status -eq 200 -and ($r.Json.items | Where-Object { $_.displayName -eq "Omar" -and $_.isActive -eq $false })) "got $($r.Status)"

$r = Call POST "/api/admin/users/3/activate" 1
Check "the admin activates Omar: 204" ($r.Status -eq 204) "got $($r.Status)"

$r = Call GET "/api/auth/me" 3
Check "Omar can log in again" ($r.Status -eq 200 -and $r.Json.displayName -eq "Omar") "got $($r.Status)"

$r = Call GET "/api/services?q=translation"
Check "his service is back in the public search" ($r.Status -eq 200 -and $r.Json.totalItems -eq 1) "got $($r.Status)"

Write-Host "== Admin: services =="
$r = Call GET "/api/services/6"
Check "before: service 6 is public" ($r.Status -eq 200) "got $($r.Status)"

$r = Call POST "/api/admin/services/9999/deactivate" 1
Check "deactivate a missing service returns 404" ($r.Status -eq 404) "got $($r.Status)"

$r = Call POST "/api/admin/services/6/deactivate" 1
Check "the admin deactivates service 6: 204" ($r.Status -eq 204) "got $($r.Status)"

$r = Call GET "/api/services/6"
Check "a deactivated service disappears (404)" ($r.Status -eq 404) "got $($r.Status)"

$r = Call GET "/api/admin/services?pageSize=50" 1
Check "the admin list shows service 6 as inactive" ($r.Status -eq 200 -and ($r.Json.items | Where-Object { $_.id -eq 6 -and $_.isActive -eq $false })) "got $($r.Status)"

$r = Call POST "/api/admin/services/6/activate" 1
Check "the admin activates service 6: 204" ($r.Status -eq 204) "got $($r.Status)"

$r = Call GET "/api/services/6"
Check "service 6 is public again" ($r.Status -eq 200) "got $($r.Status)"

# Leave the data as we found it, even if a check above failed.
[void](Call POST "/api/admin/users/3/activate" 1)
[void](Call POST "/api/admin/services/6/activate" 1)

Write-Host ""
Write-Host "Passed: $passed   Failed: $failed"
if ($failed -gt 0) { exit 1 }