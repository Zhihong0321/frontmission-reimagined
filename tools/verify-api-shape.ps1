<#
    Phase A step 6 (MIGRATION_LEDGER.md): API response-shape and value-baseline fixtures.

    Launches the host exactly like check.ps1's own web-host criterion, replays a fixed
    seed + command script, and captures each response's RAW text (never a PowerShell
    round-tripped object — S.T.J's number formatting and property order differ from what
    ConvertTo-Json would re-emit, so only the untouched response body is a safe fixture,
    per PA-KIMI-01's recording-discipline note). new/command/map responses depend only on
    the deterministic seed and script, so their raw text is expected to be byte-identical
    to the recorded fixture on every future run — that is both the shape contract and the
    value baseline at once. /api/build is the one exception: its git commit/log and
    wall-clock `builtAgo` genuinely vary run to run, so it is checked for shape (key
    presence and type) only, never for an exact value.

    Usage:
      .\tools\verify-api-shape.ps1            compare live responses to recorded fixtures
      .\tools\verify-api-shape.ps1 -Record    (re)record the fixtures from a fresh host

    Exit 0 on match/record, 1 on mismatch or any failure. Confirms port 5080 is released.
#>
param(
    [switch]$Record
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$repoRoot = Split-Path -Parent $PSScriptRoot
$fixtureDir = Join-Path $repoRoot 'tests\api-fixtures'
$seed = 555555

function Fail($message) {
    Write-Host "FAIL  $message" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $fixtureDir)) { New-Item -ItemType Directory -Path $fixtureDir -Force | Out-Null }

$hostProc = $null
try {
    $hostProc = Start-Process -FilePath 'dotnet' `
        -ArgumentList 'run', '--project', 'src/MechaTrader.Host', '-c', 'Release', '--no-build' `
        -PassThru -WindowStyle Hidden -WorkingDirectory $repoRoot `
        -RedirectStandardOutput (Join-Path $env:TEMP 'mt-api-shape.log') `
        -RedirectStandardError (Join-Path $env:TEMP 'mt-api-shape.err')

    $base = 'http://localhost:5080'
    $ready = $false
    foreach ($i in 1..45) {
        try { Invoke-RestMethod "$base/api/state" -TimeoutSec 2 -UseBasicParsing | Out-Null; $ready = $true; break }
        catch { Start-Sleep -Milliseconds 700 }
    }
    if (-not $ready) { Fail 'server never became ready' }

    function RawPost($path, $body) {
        (Invoke-WebRequest "$base$path" -Method Post -Body $body -ContentType 'application/json' -UseBasicParsing).Content
    }
    function RawGet($path) {
        (Invoke-WebRequest "$base$path" -UseBasicParsing).Content
    }

    $captured = [ordered]@{}
    $captured['new'] = RawPost '/api/new' (@{ seed = $seed } | ConvertTo-Json -Compress)

    # The run the game itself recommends, so this script stays valid if content is
    # retuned rather than asserting against hardcoded city/good names (same convention
    # check.ps1's own web-host criterion already uses).
    $afterNew = $captured['new'] | ConvertFrom-Json
    $run = $afterNew.view.routes | Where-Object { $_.bestProfit -gt 0 } | Select-Object -First 1
    if (-not $run) { Fail "no profitable opening run at seed $seed - pick a different fixed seed" }

    $captured['buy'] = RawPost '/api/command' (@{ type = 'buy'; goodId = $run.bestGoodId; units = $run.bestUnits } | ConvertTo-Json -Compress)
    $captured['depart'] = RawPost '/api/command' (@{ type = 'depart'; toId = $run.toId } | ConvertTo-Json -Compress)
    $captured['wait'] = RawPost '/api/command' (@{ type = 'wait'; days = $run.days } | ConvertTo-Json -Compress)
    $captured['sell'] = RawPost '/api/command' (@{ type = 'sell'; goodId = $run.bestGoodId; units = $run.bestUnits } | ConvertTo-Json -Compress)
    $captured['map'] = RawGet '/api/map'
    $captured['build'] = RawGet '/api/build'

    if ($Record) {
        foreach ($key in $captured.Keys) {
            [IO.File]::WriteAllText((Join-Path $fixtureDir "$key.json"), $captured[$key], [Text.Encoding]::UTF8)
        }
        Write-Host "PASS  recorded $($captured.Keys.Count) API fixtures to $fixtureDir (seed $seed)" -ForegroundColor Green
        exit 0
    }

    $mismatches = New-Object System.Collections.Generic.List[string]
    foreach ($key in @('new', 'buy', 'depart', 'wait', 'sell', 'map')) {
        $fixturePath = Join-Path $fixtureDir "$key.json"
        if (-not (Test-Path $fixturePath)) { $mismatches.Add("${key}: no recorded fixture at $fixturePath"); continue }
        $recorded = [IO.File]::ReadAllText($fixturePath, [Text.Encoding]::UTF8)
        if ($recorded -ne $captured[$key]) {
            $mismatches.Add("${key}: live response no longer matches the recorded fixture byte-for-byte")
        }
    }

    $bi = $captured['build'] | ConvertFrom-Json
    $buildShapeOk = ($bi.version -is [string]) -and ($null -ne $bi.gitAvailable) -and
        ($bi.log -is [array]) -and ($bi.log.Count -gt 0) -and
        ($bi.log[0].hash -is [string]) -and ($null -ne $bi.log[0].isHead) -and
        ($bi.commit -is [string]) -and ($bi.branch -is [string]) -and
        ($null -ne $bi.dirty) -and ($null -ne $bi.stale)
    if (-not $buildShapeOk) { $mismatches.Add('build: response no longer has the expected shape (version/gitAvailable/log/commit/branch/dirty/stale)') }

    if ($mismatches.Count -gt 0) { Fail ($mismatches -join "`n") }

    Write-Host "PASS  API responses match the recorded fixtures (seed $seed)" -ForegroundColor Green
    exit 0
}
finally {
    if ($hostProc -and -not $hostProc.HasExited) { Stop-Process -Id $hostProc.Id -Force -ErrorAction SilentlyContinue }
}
