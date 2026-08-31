<#
    Launcher. Builds if needed, starts the game server, opens your browser.
    Close this window to stop the game.
#>

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$url = 'http://localhost:5080'

Write-Host ''
Write-Host '  MECHA TRADER' -ForegroundColor Cyan
Write-Host '  building...' -ForegroundColor DarkGray

dotnet build MechaTrader.sln -c Release --nologo -v q | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host '  build failed' -ForegroundColor Red
    exit 1
}

# Free the port if a previous run is still holding it.
$busy = Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue
foreach ($c in $busy) { Stop-Process -Id $c.OwningProcess -Force -ErrorAction SilentlyContinue }

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
