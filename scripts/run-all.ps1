param([string]$BaseUrl = "http://localhost:5080")

$scripts = "smoke-test.ps1", "smoke-test-reviews.ps1", "smoke-test-admin.ps1"

$totalPassed = 0
$totalFailed = 0
$problems = @()

foreach ($name in $scripts) {
    Write-Host ""
    Write-Host "===== $name =====" -ForegroundColor Cyan

    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $name) -BaseUrl $BaseUrl 2>&1
    $exitCode = $LASTEXITCODE

    $sawSummary = $false
    foreach ($line in $output) {
        $text = "$line"
        if ($text.StartsWith("FAIL")) { Write-Host $text -ForegroundColor Red }
        elseif ($text.StartsWith("PASS")) { Write-Host $text -ForegroundColor Green }
        else { Write-Host $text }

        if ($text -match "^Passed:\s*(\d+)\s+Failed:\s*(\d+)") {
            $sawSummary = $true
            $totalPassed += [int]$Matches[1]
            $totalFailed += [int]$Matches[2]
        }
    }

    if ($exitCode -ne 0 -or -not $sawSummary) { $problems += $name }
}

Write-Host ""
Write-Host "TOTAL  Passed: $totalPassed   Failed: $totalFailed"

if ($problems.Count -gt 0) {
    Write-Host "Problems in: $($problems -join ', ')" -ForegroundColor Red
    exit 1
}