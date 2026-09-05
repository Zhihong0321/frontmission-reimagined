<#
    FAST verification entrypoint (Phase E, MIGRATION_PLAN.md "Verification modes").

    Fast = zero-warning Release build plus the affected tests.

    THIS IS AN ITERATION AID ONLY. A passing Fast run must NEVER be reported as a
    green or finished state. Use tools\verify-full.ps1 for anything you intend to
    claim.

    Usage:
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-fast.ps1
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-fast.ps1 -TestFilter "FullyQualifiedName~EconomyTests"

    Exit 0 when the build is warning-free (and the optional filter passed), else 1.
#>

param(
    [string]$TestFilter = ''
)

$ErrorActionPreference = 'Continue'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$failed = $false

Write-Host ''
Write-Host 'MECHA TRADER - Fast check' -ForegroundColor Cyan
Write-Host ('-' * 52)

# 1 - zero-warning Release build
$build = & dotnet build MechaTrader.sln -c Release --nologo -v q 2>&1 | Out-String
$buildOk = ($LASTEXITCODE -eq 0)
$warnCount = 0
if ($build -match '(\d+) Warning\(s\)') { $warnCount = [int]$Matches[1] }
$buildPass = $buildOk -and ($warnCount -eq 0)
if (-not $buildPass) { $failed = $true }
Write-Host ("  {0}  Release build (exit={1}, warnings={2})" -f $(if ($buildPass) { 'PASS' } else { 'FAIL' }), $LASTEXITCODE, $warnCount) -ForegroundColor $(if ($buildPass) { 'Green' } else { 'Red' })
if (-not $buildPass) { Write-Host $build -ForegroundColor DarkGray }

# 2 - optional affected tests
if ($TestFilter -ne '') {
    $test = & dotnet test tests/MechaTrader.Core.Tests/MechaTrader.Core.Tests.csproj -c Release --no-build --nologo -v q --filter $TestFilter 2>&1 | Out-String
    $testOk = ($LASTEXITCODE -eq 0)
    if (-not $testOk) { $failed = $true }
    Write-Host ("  {0}  dotnet test --filter {1}" -f $(if ($testOk) { 'PASS' } else { 'FAIL' }), $TestFilter) -ForegroundColor $(if ($testOk) { 'Green' } else { 'Red' })
    if (-not $testOk) { Write-Host $test -ForegroundColor DarkGray }
}

Write-Host ('-' * 52)
if ($failed) {
    Write-Host 'FAST FAILED - fix before continuing iteration.' -ForegroundColor Red
    exit 1
}

Write-Host 'FAST CHECK PASSED.' -ForegroundColor Green
Write-Host ''
Write-Host '  NOTE: Fast is an ITERATION AID ONLY. This output does NOT certify a' -ForegroundColor Yellow
Write-Host '  green or finished state. Do not claim "green" or "done" from it.' -ForegroundColor Yellow
Write-Host '  The only basis for such a claim is a same-run Full battery:' -ForegroundColor Yellow
Write-Host '    powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-full.ps1' -ForegroundColor Yellow
exit 0
