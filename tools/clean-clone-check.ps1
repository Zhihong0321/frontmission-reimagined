<#
    Phase B PB-ROOT-03: prove the consolidated chart path from a full, isolated clone.

    This clones the current commit, deliberately removes representative repository-local
    generator inputs to prove play.ps1 fails closed, regenerates web/chart/world.js from
    data/, runs the full nine-gate acceptance suite, and runs the browser smoke separately.

    Usage: .\tools\clean-clone-check.ps1
#>

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$port = 5080

function Fail($message) {
    Write-Host "FAIL  $message" -ForegroundColor Red
    exit 1
}

function Assert-PortIsFree($context) {
    $listeners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -gt 0) {
        Fail "port $port is still listening after $context"
    }
}

function Remove-ExactTree($path) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    Get-ChildItem -LiteralPath $path -Recurse -Force -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Attributes = [System.IO.FileAttributes]::Normal }
    $item = Get-Item -LiteralPath $path -Force
    $item.Attributes = [System.IO.FileAttributes]::Normal
    [System.IO.Directory]::Delete($item.FullName, $true)
}

# Refuse a temp location whose ancestors could defeat the isolation proof.
$walk = Get-Item $env:TEMP
while ($walk) {
    if (Test-Path -LiteralPath (Join-Path $walk.FullName 'FrontMission-MapLab\chart.html')) {
        Fail "%TEMP% ($($env:TEMP)) has a FrontMission-MapLab tree above it ($($walk.FullName))."
    }
    if (Test-Path -LiteralPath (Join-Path $walk.FullName 'data\config.json')) {
        Fail "%TEMP% ($($env:TEMP)) has a foreign data/config.json above it ($($walk.FullName))."
    }
    $walk = $walk.Parent
}

$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $sourceCommit) { Fail 'could not resolve the source commit' }

$cloneRoot = Join-Path $env:TEMP ("pb-root-03-clean-clone-" + [guid]::NewGuid().ToString('N'))
$clonePath = Join-Path $cloneRoot 'repo'
$stdoutPath = Join-Path $cloneRoot 'launcher.stdout.log'
$stderrPath = Join-Path $cloneRoot 'launcher.stderr.log'
$powershellExe = (Get-Process -Id $PID).Path
New-Item -ItemType Directory -Path $cloneRoot | Out-Null

function Invoke-LauncherExpectedFailure($label, $pattern) {
    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath $powershellExe `
        -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + (Join-Path $clonePath 'play.ps1') + '"') `
        -WorkingDirectory $clonePath -PassThru -Wait -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath

    if ($process.ExitCode -eq 0) { Fail "$label unexpectedly succeeded" }
    $output = ''
    if (Test-Path -LiteralPath $stdoutPath) { $output += Get-Content -LiteralPath $stdoutPath -Raw }
    if (Test-Path -LiteralPath $stderrPath) { $output += Get-Content -LiteralPath $stderrPath -Raw }
    if ($output -notmatch $pattern) {
        Fail "$label did not report the expected failure pattern '$pattern'"
    }
    Assert-PortIsFree $label
    Write-Host "PASS  $label fails closed before host startup" -ForegroundColor Green
}

try {
    Assert-PortIsFree 'clean-clone preflight'
    Write-Host "Cloning $repoRoot -> $clonePath (full history, committed HEAD)..." -ForegroundColor DarkGray
    & git clone --no-hardlinks $repoRoot $clonePath
    if ($LASTEXITCODE -ne 0) { Fail 'git clone failed (see output above)' }

    $cloneCommit = (& git -C $clonePath rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $cloneCommit -ne $sourceCommit) {
        Fail "clone commit $cloneCommit does not match source commit $sourceCommit"
    }

    Push-Location $clonePath
    try {
        $generator = Join-Path $clonePath 'web\chart\make-world.js'
        $generatorBackup = Join-Path $cloneRoot 'make-world.js.backup'
        Move-Item -LiteralPath $generator -Destination $generatorBackup
        try { Invoke-LauncherExpectedFailure 'missing repository-local generator' 'required chart generator not found' }
        finally { Move-Item -LiteralPath $generatorBackup -Destination $generator }

        $config = Join-Path $clonePath 'data\config.json'
        $configBackup = Join-Path $cloneRoot 'config.json.backup'
        Move-Item -LiteralPath $config -Destination $configBackup
        try { Invoke-LauncherExpectedFailure 'missing repository-local input' 'required chart data file not found' }
        finally { Move-Item -LiteralPath $configBackup -Destination $config }

        $statusAfterFailures = @(& git status --porcelain)
        if ($statusAfterFailures.Count -gt 0) {
            Fail "negative launcher checks left a tracked or untracked diff:`n$($statusAfterFailures -join "`n")"
        }

        Write-Host 'Regenerating web/chart/world.js from repository-local data/...' -ForegroundColor DarkGray
        & node '.\web\chart\make-world.js' '.\data'
        if ($LASTEXITCODE -ne 0) { Fail 'repository-local world generator failed' }
        & powershell -NoProfile -ExecutionPolicy Bypass -File '.\tools\verify-worldjs.ps1'
        if ($LASTEXITCODE -ne 0) { Fail 'world.js verifier failed after regeneration' }

        $statusAfterGeneration = @(& git status --porcelain)
        if ($statusAfterGeneration.Count -gt 0) {
            Fail "deterministic regeneration changed the clean clone:`n$($statusAfterGeneration -join "`n")"
        }

        Write-Host 'Running the full nine-gate check.ps1...' -ForegroundColor DarkGray
        & powershell -NoProfile -ExecutionPolicy Bypass -File '.\check.ps1'
        if ($LASTEXITCODE -ne 0) { Fail 'check.ps1 failed inside the isolated clone' }

        Write-Host 'Installing pinned browser dependencies and running the browser smoke...' -ForegroundColor DarkGray
        Push-Location (Join-Path $clonePath 'tests\browser')
        try {
            & npm ci
            if ($LASTEXITCODE -ne 0) { Fail 'npm ci failed inside the isolated clone' }
            & npx playwright install chromium
            if ($LASTEXITCODE -ne 0) { Fail 'Playwright Chromium installation failed inside the isolated clone' }
            & npm test
            if ($LASTEXITCODE -ne 0) { Fail 'browser smoke failed inside the isolated clone' }
        }
        finally {
            Pop-Location
        }

        Assert-PortIsFree 'browser smoke completion'
        Remove-ExactTree (Join-Path $clonePath 'tests\browser\node_modules')
        Remove-ExactTree (Join-Path $clonePath 'tests\browser\test-results')
        Remove-ExactTree (Join-Path $clonePath 'tests\browser\playwright-report')

        # check.ps1's balance harness regenerates FIGURES.md; no other diff is allowed.
        $statusOut = @(& git status --porcelain)
        $unexpected = @($statusOut | Where-Object { $_.Trim().Length -gt 0 -and $_ -notmatch 'FIGURES\.md$' })
        if ($unexpected.Count -gt 0) {
            Fail "unexpected post-run diff in the isolated clone:`n$($unexpected -join "`n")"
        }

        Write-Host 'PASS  clean clone: deterministic local generation, fatal launcher checks, nine gates, browser provenance, and cleanup' -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
finally {
    Remove-ExactTree $cloneRoot
}
