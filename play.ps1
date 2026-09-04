<#
    Launcher. This window owns the game: starting it again kills every previous
    instance, then rebuilds, serves, and opens the browser. Close it to stop.
#>

[CmdletBinding()]
param(
    [string]$Open = 'http://localhost:5080/chart/'
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$port = 5080

function Stop-PreviousInstances {
    # The launcher is the single owner. Anything already serving this game — the
    # host binary, a leftover `dotnet run`, whoever is sitting on the port — dies
    # before we rebuild, otherwise the DLLs stay locked and the build fails.
    $ids = [System.Collections.Generic.HashSet[int]]::new()

    try {
        foreach ($c in @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)) {
            if ($c.OwningProcess) { [void]$ids.Add([int]$c.OwningProcess) }
        }
    } catch {}

    foreach ($p in @(Get-Process -Name MechaTrader.Host -ErrorAction SilentlyContinue)) {
        [void]$ids.Add($p.Id)
    }

    try {
        foreach ($p in @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue)) {
            if ($p.CommandLine -match 'MechaTrader\.Host') {
                [void]$ids.Add([int]$p.ProcessId)
            }
        }
    } catch {}

    $ids.Remove($PID) | Out-Null
    foreach ($id in $ids) {
        if ($id -le 4) { continue }
        Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
    }

    if ($ids.Count -gt 0) {
        Write-Host '  stopping previous instance...' -ForegroundColor DarkGray
        for ($i = 0; $i -lt 20; $i++) {
            $still = $null
            try { $still = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) } catch {}
            if (-not $still -or $still.Count -eq 0) { break }
            Start-Sleep -Milliseconds 100
        }
        Start-Sleep -Milliseconds 200
    }
}

# The chart's world.js is generated from data/ — regenerate it on every launch so the
# front-end can never lag the content files (biomes, roads, off-road rates, cities).
function Update-ChartData {
    $generator = Join-Path $PSScriptRoot 'web\chart\make-world.js'
    $dataDir = Join-Path $PSScriptRoot 'data'
    $worldJs = Join-Path $PSScriptRoot 'web\chart\world.js'

    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        throw 'node is required to generate web/chart/world.js'
    }
    if (-not (Test-Path -LiteralPath $generator -PathType Leaf)) {
        throw "required chart generator not found: $generator"
    }
    if (-not (Test-Path -LiteralPath $dataDir -PathType Container)) {
        throw "required chart data directory not found: $dataDir"
    }
    foreach ($name in @('cities.json', 'routes.json', 'terrain.json', 'map.json', 'trucks.json', 'config.json')) {
        $inputPath = Join-Path $dataDir $name
        if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
            throw "required chart data file not found: $inputPath"
        }
    }

    & node $generator $dataDir
    if ($LASTEXITCODE -ne 0) {
        throw "chart data generation failed with exit code $LASTEXITCODE"
    }
    if (-not (Test-Path -LiteralPath $worldJs -PathType Leaf)) {
        throw "chart generator produced no output: $worldJs"
    }

    Write-Host '  chart data regenerated' -ForegroundColor DarkGray
}

# The front-end files (chart.html, game-bridge.js, world.js) are plain static files;
# a cache-busting query param keeps a stale browser copy from shadowing a new build.
$cacheBuster = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
$url = $Open
if ($url -match '[\?&]') { $url += '&v=' + $cacheBuster } else { $url += '?v=' + $cacheBuster }

Write-Host ''
Write-Host '  MECHA TRADER' -ForegroundColor Cyan
Stop-PreviousInstances

Write-Host '  updating chart data...' -ForegroundColor DarkGray
Update-ChartData

Write-Host '  building...' -ForegroundColor DarkGray
$build = dotnet build MechaTrader.sln -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host '  build failed' -ForegroundColor Red
    $build | Write-Host
    exit 1
}

# Open the browser as soon as the server answers, not before.
Start-Job -ScriptBlock {
    param($target)
    for ($i = 0; $i -lt 60; $i++) {
        try {
            $client = [Net.Sockets.TcpClient]::new()
            $client.Connect('127.0.0.1', 5080)
            $client.Close()
            Start-Process $target
            return
        }
        catch { Start-Sleep -Milliseconds 500 }
    }
} -ArgumentList $url | Out-Null

Write-Host "  opening $url" -ForegroundColor Green
Write-Host '  close this window to stop the game' -ForegroundColor DarkGray
Write-Host ''

dotnet run --project src/MechaTrader.Host -c Release --no-build
