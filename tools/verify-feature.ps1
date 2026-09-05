<#
    FEATURE-scoped verification entrypoint (Phase E, MIGRATION_PLAN.md "Verification modes").

    Runs one feature's targeted checks. THIS IS AN ITERATION AID ONLY: a passing
    feature check must NEVER be reported as a green or finished state. Use
    tools\verify-full.ps1 for anything you intend to claim.

    Usage:
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-feature.ps1 -List
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-feature.ps1 -Feature determinism

    Exit 0 when the named feature's checks pass, else 1.
#>

param(
    [string]$Feature = '',
    [switch]$List
)

$ErrorActionPreference = 'Continue'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$features = [ordered]@{
    architecture = @('dotnet-test', 'FullyQualifiedName~ArchitectureTests')
    economy      = @('dotnet-test', 'FullyQualifiedName~EconomyTests')
    commands     = @('dotnet-test', 'FullyQualifiedName~CommandTests')
    crew         = @('dotnet-test', 'FullyQualifiedName~CrewTests')
    citystats    = @('dotnet-test', 'FullyQualifiedName~CityStatsTests')
    standing     = @('dotnet-test', 'FullyQualifiedName~StandingTests')
    events       = @('dotnet-test', 'FullyQualifiedName~EventTests')
    quality      = @('dotnet-test', 'FullyQualifiedName~QualityTests')
    warehouse    = @('dotnet-test', 'FullyQualifiedName~WarehouseTests')
    map          = @('dotnet-test', 'FullyQualifiedName~MapTests')
    product      = @('dotnet-test', 'FullyQualifiedName~ProductTests')
    station      = @('dotnet-test', 'FullyQualifiedName~StationTests')
    worldloader  = @('dotnet-test', 'FullyQualifiedName~WorldLoaderTests')
    invariants   = @('dotnet-test', 'FullyQualifiedName~SimulationInvariantTests')
    playtest     = @('dotnet-test', 'FullyQualifiedName~PlaytestTests')
    buildinfo    = @('dotnet-test', 'FullyQualifiedName~BuildInfoTests')
    determinism  = @('dotnet-test', 'FullyQualifiedName~DeterminismFingerprintTests')
    save         = @('dotnet-test', 'FullyQualifiedName~SaveFixtureTests')
    world        = @('script', 'tools\verify-worldjs.ps1')
    api          = @('script', 'tools\verify-api-shape.ps1')
    browser      = @('browser', '')
    balance      = @('balance', '')
}

Write-Host ''
Write-Host 'MECHA TRADER - Feature check' -ForegroundColor Cyan
Write-Host ('-' * 52)

if ($List) {
    Write-Host 'Available features:'
    foreach ($name in $features.Keys) { Write-Host "  $name" }
    Write-Host ''
    Write-Host 'Feature checks are ITERATION AIDS ONLY; they never certify a green state.' -ForegroundColor Yellow
    exit 0
}

if ($Feature -eq '' -or -not $features.Contains($Feature)) {
    Write-Host "Unknown or missing -Feature '$Feature'. Use -List to enumerate." -ForegroundColor Red
    exit 1
}

# Build once, warnings are fatal (same bar as Fast).
$build = & dotnet build MechaTrader.sln -c Release --nologo -v q 2>&1 | Out-String
$warnCount = 0
if ($build -match '(\d+) Warning\(s\)') { $warnCount = [int]$Matches[1] }
$buildOk = ($LASTEXITCODE -eq 0) -and ($warnCount -eq 0)
Write-Host ("  {0}  Release build (warnings={1})" -f $(if ($buildOk) { 'PASS' } else { 'FAIL' }), $warnCount) -ForegroundColor $(if ($buildOk) { 'Green' } else { 'Red' })

$kind = $features[$Feature][0]
$arg = $features[$Feature][1]
$ok = $false
$detail = ''

if (-not $buildOk) {
    Write-Host $build -ForegroundColor DarkGray
}
else {
    if ($kind -eq 'dotnet-test') {
        & dotnet test tests/MechaTrader.Core.Tests/MechaTrader.Core.Tests.csproj -c Release --no-build --nologo -v q --filter $arg 2>&1 | Out-String | ForEach-Object { $detail = $_ }
        $ok = ($LASTEXITCODE -eq 0)
        Write-Host ("  {0}  dotnet test --filter {1}" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $arg) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
        if (-not $ok) { Write-Host $detail -ForegroundColor DarkGray }
    }
    elseif ($kind -eq 'script') {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root $arg) 2>&1 | Out-String | ForEach-Object { $detail = $_ }
        $ok = ($LASTEXITCODE -eq 0)
        Write-Host ("  {0}  {1}" -f $(if ($ok) { 'PASS' } else { 'FAIL' }), $arg) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
        if (-not $ok) { Write-Host $detail -ForegroundColor DarkGray }
    }
    elseif ($kind -eq 'browser') {
        Push-Location (Join-Path $root 'tests\browser')
        try {
            if (-not (Test-Path 'node_modules')) {
                & npm ci 2>&1 | Out-Null
                if ($LASTEXITCODE -ne 0) { Write-Host '  FAIL  npm ci' -ForegroundColor Red; Pop-Location; exit 1 }
            }
            & npx playwright install chromium 2>&1 | Out-Null
            & npm test 2>&1 | Out-String | ForEach-Object { $detail = $_ }
            $ok = ($LASTEXITCODE -eq 0)
        }
        finally { Pop-Location }
        Write-Host ("  {0}  browser smoke (npm test in tests/browser)" -f $(if ($ok) { 'PASS' } else { 'FAIL' })) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
        if (-not $ok) { Write-Host $detail -ForegroundColor DarkGray }
    }
    elseif ($kind -eq 'balance') {
        $out = & dotnet run --project tools/MechaTrader.BalanceSim -c Release --no-build 2>&1 | Out-String
        $ok = ($LASTEXITCODE -eq 0)
        Write-Host ("  {0}  balance harness (tools/MechaTrader.BalanceSim)" -f $(if ($ok) { 'PASS' } else { 'FAIL' })) -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
        if ($out -match 'tick time: ([\d.]+) ms') { Write-Host ("         tick {0} ms" -f $Matches[1]) }
        if (-not $ok) { Write-Host $out -ForegroundColor DarkGray }
        $fig = & git status --porcelain -- FIGURES.md
        if ($fig) {
            Write-Host '  NOTE  FIGURES.md was rewritten by this run (expected timing line only).' -ForegroundColor Yellow
            Write-Host '        Restore it: git checkout -- FIGURES.md   (never commit the diff).' -ForegroundColor Yellow
        }
    }
}

Write-Host ('-' * 52)
if (-not ($buildOk -and $ok)) {
    Write-Host 'FEATURE CHECK FAILED.' -ForegroundColor Red
    exit 1
}

Write-Host 'FEATURE CHECK PASSED.' -ForegroundColor Green
Write-Host ''
Write-Host '  NOTE: Feature checks are ITERATION AIDS ONLY. This output does NOT' -ForegroundColor Yellow
Write-Host '  certify a green or finished state. The only basis for such a claim is' -ForegroundColor Yellow
Write-Host '  a same-run Full battery: tools\verify-full.ps1' -ForegroundColor Yellow
exit 0
