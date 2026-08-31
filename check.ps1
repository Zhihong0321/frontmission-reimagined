<#
    Alpha 1 acceptance check.

    Every criterion here is a command with an exit code, so "is it done" is answerable
    without reading any output. Run this and look at the last line.

    Usage:  .\check.ps1
#>

$ErrorActionPreference = 'Continue'

# The SDK was installed user-scope and is not on PATH.
$localDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $localDotnet 'dotnet.exe')) {
    $env:PATH = "$localDotnet;$env:PATH"
    $env:DOTNET_ROOT = $localDotnet
}
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$root = $PSScriptRoot
Set-Location $root

$results = @()

function Record($name, $ok, $detail) {
    $script:results += [pscustomobject]@{ Name = $name; Ok = $ok; Detail = $detail }
    $mark = if ($ok) { '  PASS' } else { '  FAIL' }
    $color = if ($ok) { 'Green' } else { 'Red' }
    Write-Host "$mark  $name" -ForegroundColor $color
    if ($detail) { Write-Host "        $detail" -ForegroundColor DarkGray }
}

Write-Host ''
Write-Host 'MECHA TRADER - Alpha 1 acceptance' -ForegroundColor Cyan
Write-Host ('-' * 52)

# 1 - builds clean, no warnings
$build = & dotnet build MechaTrader.sln -c Release --nologo -v q 2>&1 | Out-String
$buildOk = ($LASTEXITCODE -eq 0)
$warnCount = 0
if ($build -match '(\d+) Warning\(s\)') { $warnCount = [int]$Matches[1] }
Record 'Solution builds in Release with no warnings' ($buildOk -and $warnCount -eq 0) "exit=$LASTEXITCODE warnings=$warnCount"

# 2 - unit tests
$test = & dotnet test tests/MechaTrader.Core.Tests/MechaTrader.Core.Tests.csproj -c Release --no-build --nologo -v q 2>&1 | Out-String
$testOk = ($LASTEXITCODE -eq 0)
$testDetail = ''
if ($test -match 'Passed!.*?Passed:\s+(\d+)') { $testDetail = "$($Matches[1]) tests passed" }
elseif ($test -match 'Failed:\s+(\d+)') { $testDetail = "$($Matches[1]) tests failed" }
Record 'Unit tests pass' $testOk $testDetail

# 3 - economy is sane, interesting, fast, and rewards skill
$sim = & dotnet run --project tools/MechaTrader.BalanceSim -c Release --no-build 2>&1 | Out-String
$simOk = ($LASTEXITCODE -eq 0)
$simDetail = ''
if ($sim -match 'tick time: ([\d.]+) ms') { $simDetail = "tick $($Matches[1]) ms" }
if ($sim -match 'skilled play: ([\-\d,]+) cr') { $simDetail += "; skilled $($Matches[1]) cr" }
if ($sim -match 'careless play: ([\-\d,]+) cr') { $simDetail += "; careless $($Matches[1]) cr" }
Record 'Balance harness green (1000 days, skill beats luck)' $simOk $simDetail

# 4 - the web host actually serves a playable game
$host_ = $null
$hostOk = $false
$hostDetail = ''
try {
    $host_ = Start-Process -FilePath 'dotnet' `
        -ArgumentList 'run', '--project', 'src/MechaTrader.Host', '-c', 'Release', '--no-build' `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $env:TEMP 'mt-host.log') `
        -RedirectStandardError (Join-Path $env:TEMP 'mt-host.err')

    $base = 'http://localhost:5080'
    $ready = $false
    foreach ($i in 1..45) {
        try { Invoke-RestMethod "$base/api/state" -TimeoutSec 2 | Out-Null; $ready = $true; break }
        catch { Start-Sleep -Milliseconds 700 }
    }

    if (-not $ready) {
        $hostDetail = 'server never became ready'
    }
    else {
        Invoke-RestMethod "$base/api/new" -Method Post -Body '{"seed":12345}' -ContentType 'application/json' | Out-Null

        function Cmd($json) {
            Invoke-RestMethod "$base/api/command" -Method Post -Body $json -ContentType 'application/json'
        }

        # -UseBasicParsing: Windows PowerShell 5.1 otherwise routes through the IE engine,
        # which cannot initialise in a non-interactive session.
        $page = Invoke-WebRequest $base -TimeoutSec 5 -UseBasicParsing
        $buy = Cmd '{"type":"buy","goodId":"cells","units":30}'
        $go = Cmd '{"type":"depart","toCityId":"praha"}'
        $wait = Cmd '{"type":"wait","days":3}'
        $sell = Cmd '{"type":"sell","goodId":"cells","units":30}'
        $bad = Cmd '{"type":"depart","toCityId":"lisboa"}'

        $hostOk = ($page.StatusCode -eq 200) -and
                  (-not $buy.error) -and (-not $go.error) -and (-not $wait.error) -and
                  (-not $sell.error) -and ($bad.error) -and
                  ($wait.view.location.id -eq 'praha')

        $hostDetail = "page=$($page.StatusCode); buy/depart/wait/sell ok; arrived=$($wait.view.location.id); illegal move refused=$([bool]$bad.error)"
    }
}
catch {
    $hostDetail = $_.Exception.Message
}
finally {
    if ($host_ -and -not $host_.HasExited) { Stop-Process -Id $host_.Id -Force -ErrorAction SilentlyContinue }
}
Record 'Web host serves a playable buy-haul-sell cycle' $hostOk $hostDetail

# ----- verdict -----
Write-Host ('-' * 52)
$failed = @($results | Where-Object { -not $_.Ok })

if ($failed.Count -eq 0) {
    Write-Host 'ALPHA 1 ACCEPTED - all checks green' -ForegroundColor Green
    Write-Host ''
    Write-Host 'Play it:  dotnet run --project src/MechaTrader.Host   ->  http://localhost:5080'
    Write-Host ''
    exit 0
}

Write-Host "ALPHA 1 NOT ACCEPTED - $($failed.Count) check(s) failed" -ForegroundColor Red
foreach ($f in $failed) { Write-Host "  - $($f.Name)" -ForegroundColor Red }
Write-Host ''
exit 1
