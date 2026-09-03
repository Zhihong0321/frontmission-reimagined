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
    $mapLab = $null
    $dir = (Get-Item $PSScriptRoot).Parent
    while ($dir -and -not $mapLab) {
        $candidate = Join-Path $dir.FullName 'FrontMission-MapLab'
        if (Test-Path (Join-Path $candidate 'make-world.js')) { $mapLab = $candidate }
        $dir = $dir.Parent
    }
    if (-not $mapLab) {
        Write-Host '  FrontMission-MapLab not found - chart data left as-is' -ForegroundColor DarkGray
        return
    }
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        Write-Host '  node not found - chart data left as-is' -ForegroundColor DarkGray
        return
    }
    & node (Join-Path $mapLab 'make-world.js') (Join-Path $PSScriptRoot 'data') 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host '  chart data regenerated' -ForegroundColor DarkGray
    }
    else {
        Write-Host '  chart data regen failed - continuing anyway' -ForegroundColor DarkGray
    }
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
