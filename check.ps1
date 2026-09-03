<#
    Alpha 1 acceptance check.

    Every criterion here is a command with an exit code, so "is it done" is answerable
    without reading any output. Run this and look at the last line.

    Usage:  .\check.ps1
#>

$ErrorActionPreference = 'Continue'

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
if ($sim -match 'house playtest: ([\-\d,]+) cr') { $simDetail += "; house $($Matches[1]) cr" }
if ($sim -match 'figures written to (.+)') { $simDetail += '; FIGURES.md regenerated' }
Record 'Balance harness green (1000 days, skill beats luck)' $simOk $simDetail

# 4 - the web host actually serves a playable game
$host_ = $null
$hostOk = $false
$hostDetail = ''
$crewOk = $false
$crewDetail = 'not reached'
$cityOk = $false
$cityDetail = 'not reached'
$buildOk2 = $false
$buildDetail2 = 'not reached'
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

        # Take the run the game itself recommends, so this stays valid when content
        # is retuned rather than asserting against hardcoded city names.
        $state = Invoke-RestMethod "$base/api/state"
        $run = $state.view.routes | Where-Object { $_.bestProfit -gt 0 } | Select-Object -First 1
        $noRoad = 'atlantis'

        if (-not $run) {
            $hostDetail = 'no profitable opening run - the first turn would be a dead end'
        }
        else {
            $buy = Cmd ("{`"type`":`"buy`",`"goodId`":`"$($run.bestGoodId)`",`"units`":$($run.bestUnits)}")
            $go = Cmd ("{`"type`":`"depart`",`"toCityId`":`"$($run.toId)`"}")
            $wait = Cmd ("{`"type`":`"wait`",`"days`":$($run.days)}")
            $sell = Cmd ("{`"type`":`"sell`",`"goodId`":`"$($run.bestGoodId)`",`"units`":$($run.bestUnits)}")
            $bad = Cmd ("{`"type`":`"depart`",`"toCityId`":`"$noRoad`"}")

            $profit = $sell.view.cash - $state.view.cash

            $hostOk = ($page.StatusCode -eq 200) -and
                      (-not $buy.error) -and (-not $go.error) -and (-not $wait.error) -and
                      (-not $sell.error) -and ($bad.error) -and
                      ($wait.view.location.id -eq $run.toId) -and ($profit -gt 0)

            $hostDetail = "hauled $($run.bestUnits) $($run.bestGoodName) to $($run.toName) in $($run.days)d for $profit cr; illegal move refused=$([bool]$bad.error)"

            # 5 - the recruitment centre hires, and the payroll actually bites.
            # Run from wherever the haul finished, so this holds for any city rather
            # than only the opening one.
            $parked = $sell.view
            $hand = $parked.crew.recruitment.candidates |
                    Where-Object { $_.affordable -and $_.roomAboard } | Select-Object -First 1

            if (-not $hand) {
                $crewDetail = "no affordable recruit at $($parked.crew.recruitment.cityName)"
            }
            else {
                $cashBeforeHire = $parked.cash
                $hired = Cmd ("{`"type`":`"hireCrew`",`"candidateId`":`"$($hand.id)`"}")
                $ghost = Cmd '{"type":"hireCrew","candidateId":"nobody-r0-0"}'
                $dayOn = Cmd '{"type":"wait","days":1}'
                $paidOff = Cmd ("{`"type`":`"dismissCrew`",`"crewId`":`"$($hand.id)`"}")

                $signingCharged = ($cashBeforeHire - $hired.view.cash) -eq $hand.signingFee
                $wageCharged = ($hired.view.cash - $dayOn.view.cash) -ge $hand.dailyWage
                $offTheBoard = -not ($paidOff.view.crew.recruitment.candidates |
                                     Where-Object { $_.id -eq $hand.id })

                $crewOk = (-not $hired.error) -and ($ghost.error) -and (-not $paidOff.error) -and
                          ($hired.view.crew.size -eq 1) -and ($paidOff.view.crew.size -eq 0) -and
                          ($hired.view.crew.dailyWages -eq $hand.dailyWage) -and
                          $signingCharged -and $wageCharged -and $offTheBoard

                $crewDetail = "signed $($hand.name) at $($parked.crew.recruitment.cityName) for " +
                              "$($hand.signingFee) cr + $($hand.dailyWage) cr/day; " +
                              "unknown recruit refused=$([bool]$ghost.error); paid off clean=$offTheBoard"
            }

            # 6 - the city page reports a living city, not a static one.
            # Read from a fresh run so the world is known to be settled, then confirm
            # the supply figures actually respond to a convoy.
            $fresh = Invoke-RestMethod "$base/api/new" -Method Post -Body '{"seed":777}' `
                        -ContentType 'application/json'
            $here = $fresh.view.location

            $vitalsComplete = ($here.vitals.Count -gt 0) -and
                              -not ($here.vitals | Where-Object {
                                  [string]::IsNullOrWhiteSpace($_.display) -or
                                  [string]::IsNullOrWhiteSpace($_.name) -or
                                  $_.fill -lt 0 -or $_.fill -gt 1 })

            $standing = $here.standing
            $standingOk = $standing -and
                          -not [string]::IsNullOrWhiteSpace($standing.governorName) -and
                          ($standing.actions.Count -gt 0) -and
                          ($standing.permits.Count -gt 0)

            $donate = $standing.actions | Where-Object { $_.id -eq 'donate' } | Select-Object -First 1
            $courted = Cmd '{"type":"favor","actionId":"donate"}'
            $standingRose = (-not $courted.error) -and
                            ($courted.view.location.standing.value -gt $standing.value)

            # A settled world has every city sitting on its own resting stock.
            $atNominal = ($here.supplies.Count -gt 0) -and
                         -not ($here.supplies | Where-Object { [Math]::Abs($_.index - 100) -ge 1 })

            # Re-scout after the gift: donate spends cash, so the day-1 recommendation
            # sized against starting capital may no longer be affordable.
            $parkedAfter = if ($courted.view) { $courted.view } else { $fresh.view }
            $haul = $parkedAfter.routes | Where-Object { $_.bestProfit -gt 0 } | Select-Object -First 1
            $shelf = $parkedAfter.market | Where-Object { $_.goodId -eq $haul.bestGoodId } |
                     Select-Object -First 1
            $band = $parkedAfter.location.supplies | Where-Object { $_.goods -contains $shelf.name } |
                    Select-Object -First 1
            $units = $haul.bestUnits

            if (-not $band) {
                $cityDetail = "no supply band reads $($shelf.name)"
            }
            else {
                $drained = Cmd ("{`"type`":`"buy`",`"goodId`":`"$($shelf.goodId)`",`"units`":$units}")
                $after = $drained.view.location.supplies |
                         Where-Object { $_.id -eq $band.id } | Select-Object -First 1
                $afterRow = $drained.view.market |
                            Where-Object { $_.goodId -eq $shelf.goodId } | Select-Object -First 1

                # The printed index is a whole percent of a four-good band, so a real
                # haul can leave the label at 100 while still emptying the shelf.
                $shelfDropped = $afterRow -and ([double]$afterRow.shelf -lt [double]$shelf.shelf)

                $cityOk = $vitalsComplete -and $atNominal -and $standingOk -and $standingRose -and
                          (-not $drained.error) -and $shelfDropped

                $cityDetail = "$($here.name): $($here.vitals.Count) vitals, " +
                              "$($here.supplies.Count) supplies at nominal; " +
                              "$($standing.governorTitle) $($standing.governorName); " +
                              "donate raised standing $($standing.value) -> $($courted.view.location.standing.value); " +
                              "buying $units $($shelf.name) moved $($band.name) $([Math]::Round($band.index))% " +
                              "-> $([Math]::Round($after.index))%; shelf $($shelf.shelf) -> $($afterRow.shelf)"
            }

            # 7 - the build page tells the truth about what is running.
            # The solution was rebuilt by criterion 1 and nothing has been edited since,
            # so a correct staleness check must report this build as current. If it says
            # otherwise the detector is broken, and a warning nobody can trust is worse
            # than none at all.
            $bi = Invoke-RestMethod "$base/api/build" -TimeoutSec 10
            $declared = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()

            $buildOk2 = ($bi.version -eq $declared) -and
                        $bi.gitAvailable -and
                        ($bi.log.Count -gt 0) -and
                        ($bi.log[0].isHead) -and
                        ($bi.commit -eq $bi.log[0].hash) -and
                        (-not [string]::IsNullOrWhiteSpace($bi.branch)) -and
                        (-not $bi.stale)

            $buildDetail2 = "$($bi.version) built $($bi.builtAgo) from $($bi.commit) on " +
                            "$($bi.branch); $($bi.log.Count) commits listed; stale=$($bi.stale)" +
                            $(if ($bi.stale) { " ($($bi.staleReason))" } else { '' })
        }
    }
}
catch {
    $hostDetail = $_.Exception.Message
}
finally {
    if ($host_ -and -not $host_.HasExited) { Stop-Process -Id $host_.Id -Force -ErrorAction SilentlyContinue }
}
Record 'Web host serves a playable buy-haul-sell cycle' $hostOk $hostDetail
Record 'Recruitment centre hires, pays wages and pays off' $crewOk $crewDetail
Record 'City page reports founding stats and living supply' $cityOk $cityDetail
Record 'Build page names the running build and its commit log' $buildOk2 $buildDetail2

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
