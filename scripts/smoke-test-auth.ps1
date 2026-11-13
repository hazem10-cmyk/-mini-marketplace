param([string]$BaseUrl = "http://localhost:5080")
. "$PSScriptRoot\common.ps1"

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
$newId = $r.Json.id
$names = @($r.Json.PSObject.Properties.Name)
Check "register returns 201: id, lower-case email, trimmed name, role Buyer" ($r.Status -eq 201 -and $newId -gt 0 -and $r.Json.email -ceq $email.ToLowerInvariant() -and $r.Json.displayName -ceq "Smoke Tester" -and $r.Json.role -eq "Buyer") "got $($r.Status)"
Check "the response has no password field" (($names -notcontains "password") -and ($names -notcontains "passwordHash")) ($names -join ",")

$r = Call POST "/api/auth/register" 0 (Register-Body ($email.ToUpperInvariant()) $password "Copycat" "Seller")
Check "the same email in another letter case is refused (409)" ($r.Status -eq 409) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 (Register-Body "sara@marketplace.test" $password "Fake Sara" "Seller")
Check "the email of a seeded user is refused (409)" ($r.Status -eq 409) "got $($r.Status)"

$r = Call POST "/api/auth/register" 0 (Register-Body "seller.$stamp@example.com" $password "Smoke Seller" "Seller")
Check "a Seller can register (201, role Seller)" ($r.Status -eq 201 -and $r.Json.role -eq "Seller") "got $($r.Status)"

Write-Host "== Login: the token =="
$r = Call POST "/api/auth/login" 0 (Login-Body $email $password)
$token = "$($r.Json.token)"
$parts = $token.Split('.')
Check "login returns 200 with a three-part token and the user" ($r.Status -eq 200 -and $parts.Count -eq 3 -and $r.Json.user.id -eq $newId -and $r.Json.user.role -eq "Buyer") "got $($r.Status)"

$payload = $null
if ($parts.Count -eq 3) { $payload = Read-JwtPayload $token }
$claimNames = @()
if ($payload) { $claimNames = @($payload.PSObject.Properties.Name) }
Check "the token holds sub, name, role, iss, aud, and exp" ($payload -and $payload.sub -eq "$newId" -and $payload.name -ceq "Smoke Tester" -and $payload.role -eq "Buyer" -and $payload.iss -and $payload.aud -and $payload.exp) "claims: $($claimNames -join ',')"
Check "the token holds no email and no password" (($claimNames.Count -gt 0) -and ($claimNames -notcontains "email") -and ($claimNames -notcontains "password") -and ($claimNames -notcontains "passwordHash")) "claims: $($claimNames -join ',')"

$expires = [datetime]$r.Json.expiresAt
Check "expiresAt is about 60 minutes from now" (($expires -gt (Get-Date).AddMinutes(55)) -and ($expires -lt (Get-Date).AddMinutes(65))) "expiresAt: $($r.Json.expiresAt)"

Write-Host "== Login: credentials =="
$r = Call POST "/api/auth/login" 0 (Login-Body ($email.ToUpperInvariant()) $password)
Check "login works with the email in another letter case" ($r.Status -eq 200) "got $($r.Status)"

$wrong = Call POST "/api/auth/login" 0 (Login-Body $email "wrong-password")
$unknown = Call POST "/api/auth/login" 0 (Login-Body "nobody.$stamp@example.com" $password)
Check "a wrong password returns 401" ($wrong.Status -eq 401) "got $($wrong.Status)"
Check "an unknown email returns 401" ($unknown.Status -eq 401) "got $($unknown.Status)"
Check "both answers are identical, so nobody can find out which emails exist" ($wrong.Json.title -and $wrong.Json.title -eq $unknown.Json.title -and $wrong.Json.detail -eq $unknown.Json.detail -and $wrong.Json.status -eq $unknown.Json.status) "wrong: $($wrong.Json.detail) | unknown: $($unknown.Json.detail)"

$r = Call POST "/api/auth/login" 0 '{}'
Check "login without email and password returns 400" ($r.Status -eq 400 -and $r.Json.errors.email -and $r.Json.errors.password) "got $($r.Status)"

$r = Call POST "/api/auth/login" 0 (Login-Body $email ("p" * 5000))
Check "a huge password is refused with 401 without being hashed" ($r.Status -eq 401) "got $($r.Status)"

$r = Call POST "/api/auth/login" 0 (Login-Body "sara@marketplace.test" "DevPassword123")
Check "the seeded user Sara can log in (role Seller)" ($r.Status -eq 200 -and $r.Json.user.displayName -eq "Sara" -and $r.Json.user.role -eq "Seller") "got $($r.Status)"

$r = Call POST "/api/auth/login" 0 (Login-Body "admin@marketplace.test" "DevPassword123")
Check "the seeded Admin can log in (role Admin)" ($r.Status -eq 200 -and $r.Json.user.role -eq "Admin") "got $($r.Status)"

Write-Host "== Login: a deactivated account =="
$r = Call POST "/api/admin/users/$newId/deactivate" 1
Check "the admin deactivates the new user" ($r.Status -eq 204) "got $($r.Status)"

$r = Call POST "/api/auth/login" 0 (Login-Body $email $password)
Check "the right password on a deactivated account returns 403" ($r.Status -eq 403) "got $($r.Status)"

$r = Call POST "/api/auth/login" 0 (Login-Body $email "wrong-password")
Check "a wrong password on a deactivated account still returns 401 (nothing leaks)" ($r.Status -eq 401) "got $($r.Status)"

Complete-Run