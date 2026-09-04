<#
    Puts "Mecha Trader" and "Art Lab" shortcuts on the desktop.

    Both point at cmd launchers rather than a built binary, so they rebuild before
    serving and cannot start a stale build. Run this again any time - it overwrites.

    Usage:  .\install-launcher.ps1            install or refresh
            .\install-launcher.ps1 -Remove    take them off the desktop again
#>

[CmdletBinding()]
param(
    [switch]$Remove,

    # Where to put it. Defaults to the desktop; pass another folder to pin it elsewhere.
    [string]$Destination = [Environment]::GetFolderPath('Desktop'),

    [string]$Name = 'Mecha Trader'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$icon = Join-Path $root 'web\favicon.ico'

$launchers = @(
    @{ Name = $Name; Target = 'Play.cmd'; Description = 'Mecha Trader - builds the latest, then opens the game in your browser' }
    @{ Name = 'Art Lab'; Target = 'ArtLab.cmd'; Description = 'Art Lab - builds the latest, then opens the asset generator' }
)

if ($Remove) {
    foreach ($item in $launchers) {
        $link = Join-Path $Destination "$($item.Name).lnk"
        if (Test-Path $link) {
            Remove-Item $link -Force
            Write-Host "  removed $link" -ForegroundColor Yellow
        }
        else {
            Write-Host "  nothing to remove at $link" -ForegroundColor DarkGray
        }
    }
    exit 0
}

if (-not (Test-Path $Destination)) { throw "No such folder: $Destination" }

$shell = New-Object -ComObject WScript.Shell
try {
    foreach ($item in $launchers) {
        $target = Join-Path $root $item.Target
        if (-not (Test-Path $target)) { throw "$($item.Target) is not next to this script (looked in $root)." }
        $link = Join-Path $Destination "$($item.Name).lnk"
        $shortcut = $shell.CreateShortcut($link)
        $shortcut.TargetPath = $target
        $shortcut.WorkingDirectory = $root
        $shortcut.Description = $item.Description
        $shortcut.WindowStyle = 1
        if (Test-Path $icon) { $shortcut.IconLocation = "$icon,0" }
        $shortcut.Save()
        Write-Host "  launcher installed at $link" -ForegroundColor Green
    }
}
finally {
    [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
}

Write-Host ''
Write-Host '  they run Play.cmd / ArtLab.cmd, which rebuild before serving' -ForegroundColor DarkGray
Write-Host ''
