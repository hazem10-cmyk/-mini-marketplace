# Shared helpers for the smoke test scripts.
# A script starts with:  param([string]$BaseUrl = "http://localhost:5080")
# and then:              . "$PSScriptRoot\common.ps1"
# and ends with:         Complete-Run

$Stats = @{ Passed = 0; Failed = 0 }

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
        $Stats.Passed++
        Write-Host "PASS  $Name" -ForegroundColor Green
    } else {
        $Stats.Failed++
        Write-Host "FAIL  $Name   $Detail" -ForegroundColor Red
    }
}

function Complete-Run {
    Write-Host ""
    Write-Host "Passed: $($Stats.Passed)   Failed: $($Stats.Failed)"
    if ($Stats.Failed -gt 0) { exit 1 }
}

function Register-Body {
    param([string]$Email, [string]$Password, [string]$Name, [string]$Role)
    ConvertTo-Json -Compress @{ email = $Email; password = $Password; displayName = $Name; role = $Role }
}

function Login-Body {
    param([string]$Email, [string]$Password)
    ConvertTo-Json -Compress @{ email = $Email; password = $Password }
}

# Reads the payload of a JWT. The payload is only Base64Url text: anyone can read it.
function Read-JwtPayload {
    param([string]$Token)
    $p = $Token.Split('.')[1].Replace('-', '+').Replace('_', '/')
    switch ($p.Length % 4) { 2 { $p += '==' } 3 { $p += '=' } }
    [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)) | ConvertFrom-Json
}

$r = Call GET "/api/health"
if ($r.Status -ne 200) {
    Write-Host "The API is not running at $BaseUrl. Start it first (dotnet run) and try again." -ForegroundColor Red
    exit 1
}