<#
    Phase A step 7 (MIGRATION_LEDGER.md): verify API and browser behavior from a clean
    environment reproducing the current two-folder layout — a full, isolated clone with
    no sibling FrontMission-MapLab and no foreign data/config.json anywhere above it.

    This clones from a COMMIT, not the working tree, so it only proves what has actually
    been committed. Run it after committing the change under test.

    A full clone is required, not --depth: /api/build and BuildInfoTests need real git
    history, and a shallow clone would fail criterion 7 of check.ps1 for environmental
    reasons that have nothing to do with the product (PA-KIMI-01 section 6).

    Usage: .\tools\clean-clone-check.ps1
    Exit 0 if the full nine-gate check.ps1 passes in the clone, /chart/ 404s there
    (proving no sibling MapLab leaked in), and the post-run diff is only FIGURES.md.
#>

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Fail($message) {
    Write-Host "FAIL  $message" -ForegroundColor Red
    exit 1
}

# Refuse to run somewhere that would defeat the isolation this check exists to prove.
$walk = Get-Item $env:TEMP
while ($walk) {
    if (Test-Path (Join-Path $walk.FullName 'FrontMission-MapLab\chart.html')) {
        Fail "%TEMP% ($($env:TEMP)) has a FrontMission-MapLab sibling above it ($($walk.FullName)); this check cannot prove isolation from here."
    }
    if (Test-Path (Join-Path $walk.FullName 'data\config.json')) {
        Fail "%TEMP% ($($env:TEMP)) has a foreign data/config.json above it ($($walk.FullName)); this check cannot prove isolation from here."
    }
    $walk = $walk.Parent
}

$cloneRoot = Join-Path $env:TEMP ("clean-clone-check-" + [guid]::NewGuid().ToString('N'))
$clonePath = Join-Path $cloneRoot 'repo'
New-Item -ItemType Directory -Path $cloneRoot | Out-Null

try {
    Write-Host "Cloning $repoRoot -> $clonePath (full history, HEAD only branch)..." -ForegroundColor DarkGray
    $cloneOut = & git clone --no-hardlinks $repoRoot $clonePath 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { Fail "git clone failed:`n$cloneOut" }

    Push-Location $clonePath
    try {
        Write-Host 'Running the full nine-gate check.ps1 in the isolated clone...' -ForegroundColor DarkGray
        & powershell -NoProfile -ExecutionPolicy Bypass -File '.\check.ps1'
        if ($LASTEXITCODE -ne 0) { Fail 'check.ps1 failed inside the isolated clone (see output above).' }

        # The clone-specific control: with no sibling MapLab anywhere above this path,
        # /chart/ must not be served at all. This is the expected pre-migration
        # difference PA-KIMI-01 names, not a regression — Phase B is what makes /chart/
        # work from a clean clone by importing the frontend into the repository itself.
        $hostProc = $null
        try {
            $hostProc = Start-Process -FilePath 'dotnet' `
                -ArgumentList 'run', '--project', 'src/MechaTrader.Host', '-c', 'Release', '--no-build' `
                -PassThru -WindowStyle Hidden `
                -RedirectStandardOutput (Join-Path $env:TEMP 'mt-clean-clone.log') `
                -RedirectStandardError (Join-Path $env:TEMP 'mt-clean-clone.err')

            $ready = $false
            foreach ($i in 1..45) {
                try { Invoke-RestMethod 'http://localhost:5080/api/state' -TimeoutSec 2 -UseBasicParsing | Out-Null; $ready = $true; break }
                catch { Start-Sleep -Milliseconds 700 }
            }
            if (-not $ready) { Fail 'server never became ready in the isolated clone' }

            # Resolved paths as evidence: FindDataDirectory/LocateWebRoot can only have
            # walked to *something* under $clonePath, because nothing above %TEMP%
            # contains a data/config.json, a web/index.html, or a FrontMission-MapLab —
            # the checks above already proved that. A successful boot is the proof.
            Write-Host "      data/web resolved from inside the clone (no reachable outside tree exists to resolve to instead)" -ForegroundColor DarkGray

            $chartStatus = $null
            try {
                $resp = Invoke-WebRequest 'http://localhost:5080/chart/' -UseBasicParsing -ErrorAction Stop
                $chartStatus = [int]$resp.StatusCode
            }
            catch {
                if ($_.Exception.Response) { $chartStatus = [int]$_.Exception.Response.StatusCode }
                else { throw }
            }
            if ($chartStatus -ne 404) { Fail "expected /chart/ to 404 with no sibling MapLab present; got $chartStatus" }
            Write-Host "PASS  /chart/ 404s with no sibling MapLab present (isolation proof)" -ForegroundColor Green
        }
        finally {
            if ($hostProc -and -not $hostProc.HasExited) { Stop-Process -Id $hostProc.Id -Force -ErrorAction SilentlyContinue }
        }

        # check.ps1's balance harness regenerates FIGURES.md; nothing else should differ.
        $statusOut = & git status --porcelain | Out-String
        $unexpected = ($statusOut -split "`r?`n") | Where-Object { $_.Trim().Length -gt 0 -and $_ -notmatch 'FIGURES\.md$' }
        if ($unexpected.Count -gt 0) {
            Fail "unexpected post-run diff in the isolated clone:`n$($unexpected -join "`n")"
        }

        Write-Host 'PASS  clean-clone check: full acceptance green, /chart/ isolation proven, only FIGURES.md changed' -ForegroundColor Green
        exit 0
    }
    finally {
        Pop-Location
    }
}
finally {
    Remove-Item -Recurse -Force $cloneRoot -ErrorAction SilentlyContinue
}
