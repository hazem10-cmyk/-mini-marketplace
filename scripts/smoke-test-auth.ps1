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

function Register-Body {
    param([string]$Email, [string]$Password, [string]$Name, [string]$Role)
    ConvertTo-Json -Compress @{ email = $Email; password = $Password; displayName = $Name; role = $Role }
}

$r = Call GET "/api/health"
if ($r.Status -ne 200) {
    Write-Host "The API is not running at $BaseUrl. Start it first (dotnet run) and try again." -ForegroundColor Red
    exit 1
}

# A new email for every run, so the script can be run again and again.
$stamp = [guid]::NewGuid().ToString("N").Substring(0, 12)
$password = "Correct-Horse-Battery-9"

Write-Host "== Register: invalid input =="
$r = Call POST "/api/auth/register" 0 (Register-Body "not-an-email" "short" "x" "Admin")
Check "all invalid fields are reported (400)" ($r.Status -eq 400 -and $r.Json.errors.email -and $r.Json.errors.password -and $r.Json.errors.displayName -and $r.Json.errors.role) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 (Register-Body "admin.$stamp@example.com" $password "Mallory" "Admin")
Check "registering as Admin is refused (400)" ($r.Status -eq 400 -and $r.Json.errors.role) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 (Register-Body "num.$stamp@example.com" $password "Mallory" "1")
Check "a numeric role is refused (400)" ($r.Status -eq 400 -and $r.Json.errors.role) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 (Register-Body "short.$stamp@example.com" "1234567" "Shorty" "Buyer")
Check "a 7-character password is refused (400)" ($r.Status -eq 400 -and $r.Json.errors.password) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 (Register-Body "long.$stamp@example.com" ("p" * 129) "Longy" "Buyer")
Check "a 129-character password is refused (400)" ($r.Status -eq 400 -and $r.Json.errors.password) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 '{}'
Check "an empty object is refused (400)" ($r.Status -eq 400) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 (Register-Body "Mallory <mal.$stamp@example.com>" $password "Mallory" "Buyer")
Check "an address with a display name is refused (400)" ($r.Status -eq 400 -and $r.Json.errors.email) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 (Register-Body "blank.$stamp@example.com" $password "   " "Buyer")
Check "a blank display name is refused (400)" ($r.Status -eq 400 -and $r.Json.errors.displayName) "got $($r.Status)"

Write-Host "== Register: success and duplicates =="
$email = "Smoke.$stamp@Example.com"
$r = Call POST "/api/auth/register" 0 (Register-Body $email $password "  Smoke Tester  " "buyer")
$names = @($r.Json.PSObject.Properties.Name)
Check "register returns 201: id, lower-case email, trimmed name, role Buyer" ($r.Status -eq 201 -and $r.Json.id -gt 0 -and $r.Json.email -ceq $email.ToLowerInvariant() -and $r.Json.displayName -ceq "Smoke Tester" -and $r.Json.role -eq "Buyer") "got $($r.Status)"
Check "the response has no password field" (($names -notcontains "password") -and ($names -notcontains "passwordHash")) ($names -join ",")

$r = Call POST "/api/auth/register" 0 (Register-Body ($email.ToUpperInvariant()) $password "Copycat" "Seller")
Check "the same email in another letter case is refused (409)" ($r.Status -eq 409) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 (Register-Body "sara@marketplace.test" $password "Fake Sara" "Seller")
Check "the email of a seeded user is refused (409)" ($r.Status -eq 409) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 (Register-Body "seller.$stamp@example.com" $password "Smoke Seller" "Seller")
Check "a Seller can register (201, role Seller)" ($r.Status -eq 201 -and $r.Json.role -eq "Seller") "got $($r.Status)"

Write-Host ""
Write-Host "Passed: $passed   Failed: $failed"
if ($failed -gt 0) { exit 1 }