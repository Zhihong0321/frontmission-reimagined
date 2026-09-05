<#
    FULL verification entrypoint (Phase E; coordination/plan-revision-2026-09-05.md
    section 3 six-gate battery; MIGRATION_PLAN.md "Verification modes").

    Full = all six gates, in order, stop on first failure:
      1. dotnet build MechaTrader.sln -c Release  -> 0 warnings, 0 errors
      2. check.ps1                                -> nine gates green
      3. tools/MechaTrader.Fingerprint            -> zero tracked fixture diff,
                                                     pinned F_state / F_view echoed
      4. browser smoke (npm ci + chromium)        -> 1/1
      5. git diff --check                         -> clean
      6. hygiene: port 5080 free (no listener; TIME_WAIT ignored), no
         MechaTrader.Host process, FIGURES.md timing-line-only diff restored,
         new verify-worldjs temp directories removed without touching the
         pre-existing baseline.

    Optional: -IncludeCleanClone additionally runs tools\clean-clone-check.ps1
    (the full-history no-sibling clone proof) as a seventh gate, for events that
    require the clean-path superset element (see docs\decisions\0002).

    Only a same-run Full result may back a green / MERGED / VERIFIED claim.

    Usage:
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-full.ps1
      powershell -NoProfile -ExecutionPolicy Bypass -File tools\verify-full.ps1 -IncludeCleanClone

    The start state must be tracked-clean (the battery itself rewrites FIGURES.md
    and touches fixtures only via the fingerprint tool). Exit 0 only if every gate
    is green; the FIGURES.md timing diff is restored as part of hygiene.
#>

param(
    [switch]$IncludeCleanClone
)

$ErrorActionPreference = 'Continue'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$script:gateResults = @()

function Record-Gate([string]$name, [bool]$ok, [string]$detail) {
    $script:gateResults += [pscustomobject]@{ Name = $name; Ok = $ok }
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    $color = if ($ok) { 'Green' } else { 'Red' }
    Write-Host ("GATE {0}  {1}" -f $mark, $name) -ForegroundColor $color
    if ($detail) { Write-Host "       $detail" -ForegroundColor DarkGray }
}

function Get-TempWorldDirs {
    return @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'verify-worldjs-*' -ErrorAction SilentlyContinue)
}

function Remove-TreeExact([string]$path) {
    # Established procedure (D-050): exact-file deletion, then verified-empty
    # nonrecursive directory removal. Never a recursive delete.
    $files = @(Get-ChildItem -LiteralPath $path -Recurse -File -Force -ErrorAction SilentlyContinue)
    foreach ($f in $files) {
        $f.Attributes = [System.IO.FileAttributes]::Normal
        Remove-Item -LiteralPath $f.FullName -Force -ErrorAction SilentlyContinue
    }
    $left = @(Get-ChildItem -LiteralPath $path -Recurse -File -Force -ErrorAction SilentlyContinue)
    if ($left.Count -gt 0) { return $false }
    $dirs = @(Get-ChildItem -LiteralPath $path -Recurse -Directory -Force -ErrorAction SilentlyContinue) |
        Sort-Object { $_.FullName.Length } -Descending
    foreach ($d in $dirs) {
        Remove-Item -LiteralPath $d.FullName -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    return (-not (Test-Path -LiteralPath $path))
}

Write-Host ''
Write-Host 'MECHA TRADER - Full battery (six gates + hygiene)' -ForegroundColor Cyan
Write-Host ('-' * 60)

# --- start-state guard: tracked-clean tree, so hygiene attribution is exact ---
$preDirty = @(git status --porcelain --untracked-files=no | Where-Object { $_ -ne '' })
if ($preDirty.Count -gt 0) {
    Write-Host 'ABORT: start state is not tracked-clean; the battery would not be able' -ForegroundColor Red
    Write-Host 'to attribute FIGURES/fixture changes. Commit or restore first:' -ForegroundColor Red
    $preDirty | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

$tempBefore = Get-TempWorldDirs
$tempBaseline = @($tempBefore | ForEach-Object { $_.Name })
$commit = (& git rev-parse HEAD)

# ================= GATE 1 - zero-warning Release build =================
$build = & dotnet build MechaTrader.sln -c Release --nologo -v q 2>&1 | Out-String
$warnCount = 0
if ($build -match '(\d+) Warning\(s\)') { $warnCount = [int]$Matches[1] }
$errCount = 0
if ($build -match '(\d+) Error\(s\)') { $errCount = [int]$Matches[1] }
$g1 = ($LASTEXITCODE -eq 0) -and ($warnCount -eq 0) -and ($errCount -eq 0)
Record-Gate '1 zero-warning Release build' $g1 ("exit=$LASTEXITCODE warnings=$warnCount errors=$errCount")
if (-not $g1) { Write-Host $build -ForegroundColor DarkGray; exit 1 }

# ================= GATE 2 - complete nine-gate check.ps1 =================
Write-Host '       running check.ps1 (nine gates)...' -ForegroundColor DarkGray
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'check.ps1')
$g2 = ($LASTEXITCODE -eq 0)
Record-Gate '2 nine-gate check.ps1' $g2 ("exit=$LASTEXITCODE")
if (-not $g2) { exit 1 }

# ================= GATE 3 - Fingerprint regeneration, zero tracked diff =====
$fp = & dotnet run --project tools/MechaTrader.Fingerprint -c Release --no-build 2>&1 | Out-String
$fState = ''
$fView = ''
if ($fp -match 'F_state:\s*([0-9a-f]{64})') { $fState = $Matches[1] }
if ($fp -match 'F_view:\s*([0-9a-f]{64})') { $fView = $Matches[1] }
$fixtureDiff = @(git status --porcelain --untracked-files=no -- tests/ | Where-Object { $_ -ne '' })
$g3 = ($LASTEXITCODE -eq 0) -and ($fState -ne '') -and ($fView -ne '') -and ($fixtureDiff.Count -eq 0)
Record-Gate '3 Fingerprint regeneration, zero tracked fixture diff' $g3 ("exit=$LASTEXITCODE; F_state=$fState; F_view=$fView; fixture diffs=$($fixtureDiff.Count)")
if (-not $g3) {
    Write-Host $fp -ForegroundColor DarkGray
    if ($fixtureDiff.Count -gt 0) { $fixtureDiff | ForEach-Object { Write-Host "  $_" -ForegroundColor Red } }
    exit 1
}

# ================= GATE 4 - browser smoke 1/1 =================
Write-Host '       npm ci + playwright install chromium + npm test...' -ForegroundColor DarkGray
Push-Location (Join-Path $root 'tests\browser')
$g4 = $false
$browserLog = ''
try {
    & npm ci 2>&1 | Out-Null
    $ciOk = ($LASTEXITCODE -eq 0)
    & npx playwright install chromium 2>&1 | Out-Null
    $pwOk = ($LASTEXITCODE -eq 0)
    $browserLog = & npm test 2>&1 | Out-String
    $g4 = $ciOk -and $pwOk -and ($LASTEXITCODE -eq 0)
}
finally { Pop-Location }
$banner = ''
if ($browserLog -match 'banner ([^\s"]+)') { $banner = $Matches[1] }
Record-Gate '4 browser smoke 1/1' $g4 ($browserLog.Trim() -replace "`r?`n", ' | ')
if (-not $g4) { exit 1 }

# ================= GATE 5 - git diff --check =================
$diffCheck = (& git diff --check 2>&1 | Out-String)
$g5 = ($LASTEXITCODE -eq 0) -and ([string]::IsNullOrWhiteSpace($diffCheck))
Record-Gate '5 git diff --check clean' $g5 ($diffCheck.Trim())
if (-not $g5) { exit 1 }

# ================= optional clean-path gate =================
if ($IncludeCleanClone) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tools\clean-clone-check.ps1')
    $gcc = ($LASTEXITCODE -eq 0)
    Record-Gate '6a clean-clone (no-sibling full-history proof)' $gcc ("exit=$LASTEXITCODE")
    if (-not $gcc) { exit 1 }
}

# ================= GATE 6 - hygiene =================
$hygieneNotes = @()
$hygieneOk = $true

# port 5080: no LISTEN (TIME_WAIT remnants are ignored)
$listeners = @(Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue)
if ($listeners.Count -ne 0) { $hygieneOk = $false }
$hygieneNotes += ("port 5080 listeners={0}" -f $listeners.Count)

# no MechaTrader.Host process
$hostProcs = @(Get-Process -Name 'MechaTrader.Host' -ErrorAction SilentlyContinue)
if ($hostProcs.Count -ne 0) { $hygieneOk = $false }
$hygieneNotes += ("MechaTrader.Host processes={0}" -f $hostProcs.Count)

# FIGURES.md: timing-line-only diff, then restore
$figDiff = (& git diff -- FIGURES.md 2>&1 | Out-String)
if ([string]::IsNullOrWhiteSpace($figDiff)) {
    $hygieneNotes += 'FIGURES.md diff: none'
}
else {
    $changed = @($figDiff -split "`r?`n" | Where-Object {
        ($_ -match '^[+-]') -and ($_ -notmatch '^(\+\+\+|---)')
    })
    $timingOnly = $true
    foreach ($line in $changed) { if ($line -notmatch '1000-day tick') { $timingOnly = $false } }
    if (-not $timingOnly) { $hygieneOk = $false }
    $hygieneNotes += ("FIGURES.md diff lines={0} timing-only={1}" -f $changed.Count, $timingOnly)
    & git checkout -- FIGURES.md
    $afterFig = @(git status --porcelain --untracked-files=no -- FIGURES.md | Where-Object { $_ -ne '' })
    if ($afterFig.Count -ne 0) { $hygieneOk = $false }
    $hygieneNotes += 'FIGURES.md restored'
}

# temp directories: baseline preserved, this-run additions cleaned
$tempAfter = Get-TempWorldDirs
$newDirs = @($tempAfter | Where-Object { $tempBaseline -notcontains $_.Name })
$cleaned = 0
foreach ($d in $newDirs) {
    if (Remove-TreeExact $d.FullName) { $cleaned++ } else { $hygieneOk = $false }
}
$tempNow = Get-TempWorldDirs
if ($tempNow.Count -ne $tempBaseline.Count) { $hygieneOk = $false }
$hygieneNotes += ("verify-worldjs temp dirs: baseline={0} new={1} cleaned={2} remaining={3}" -f $tempBaseline.Count, $newDirs.Count, $cleaned, $tempNow.Count)

Record-Gate '6 hygiene (port/process/FIGURES/temp)' $hygieneOk ($hygieneNotes -join '; ')

# ================= verdict =================
Write-Host ('-' * 60)
$failed = @($script:gateResults | Where-Object { -not $_.Ok })
if ($failed.Count -eq 0) {
    Write-Host "FULL BATTERY GREEN - all $($script:gateResults.Count) gates passed on tree $commit" -ForegroundColor Green
    Write-Host ''
    Write-Host '  This same-run result is the ONLY valid basis for a green/MERGED/VERIFIED' -ForegroundColor Yellow
    Write-Host '  claim. Record it with the tree commit hash and this evidence.' -ForegroundColor Yellow
    exit 0
}

Write-Host "FULL BATTERY RED - $($failed.Count) gate(s) failed on tree $commit" -ForegroundColor Red
$failed | ForEach-Object { Write-Host ("  - {0}" -f $_.Name) -ForegroundColor Red }
exit 1
