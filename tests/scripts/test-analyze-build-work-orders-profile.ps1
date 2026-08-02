[CmdletBinding()]
param(
    [string]$AnalyzerPath,
    [string]$TemporaryRoot = [System.IO.Path]::GetTempPath()
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($AnalyzerPath)) {
    $repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $AnalyzerPath = Join-Path $repositoryRoot "scripts\analyze-build-work-orders-profile.ps1"
}
$AnalyzerPath = [System.IO.Path]::GetFullPath($AnalyzerPath)
if (-not (Test-Path -LiteralPath $AnalyzerPath -PathType Leaf)) {
    throw "Analyzer script is missing: $AnalyzerPath"
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function New-MetricsRow {
    param(
        [int]$Sequence,
        [double]$WallMilliseconds,
        [double]$ParentMilliseconds,
        [double]$InputMilliseconds = 2.0,
        [double]$NonExtractionMilliseconds = 8.0,
        [double]$ReserveExtractionMilliseconds = 15.0,
        [double]$FinalizationMilliseconds = 3.0
    )
    return [ordered]@{
        sequence = $Sequence
        batch_kind = "manual_step"
        start_simulation_tick = $Sequence + 2
        end_simulation_tick = $Sequence + 3
        completed_ticks = 1
        wall_ms = $WallMilliseconds
        max_tick_ms = $WallMilliseconds
        simulation_tick_ms = [Math]::Max(0.0, $WallMilliseconds - 2.0)
        session_advance_ms = [Math]::Max(0.0, $WallMilliseconds - 4.0)
        build_work_orders_ms = $ParentMilliseconds
        harvest_apply_ms = 0.1
        scene_sync_ms = 0.2
        update_hud_ms = 0.1
        work_orders_generated_total = 1
        work_orders_generated_uncapped_total = 1
        work_orders_claimed_total = 0
        work_orders_remaining_last = 1
        path_plan_lookups_total = 0
        path_plan_cache_hits_total = 0
        citizens_evaluated_total = 16
        path_plan_cache_misses_total = 0
        path_plan_cache_size_last = 0
        navigation_invalidations_total = 0
        worker_count_last = 16
        idle_citizens_considering_work_orders_total = 0
        candidate_orders_evaluated_total = 0
        candidate_orders_per_idle_citizen = 0.0
        navigation_rebuild_ms = 0.0
        route_selection_ms = 0.0
        selector_candidates_bounded_total = 0
        selector_candidates_exact_scored_total = 0
        selector_candidates_pruned_total = 0
        selector_exact_path_queries_total = 0
        selector_path_cache_hits_total = 0
        selector_path_cache_misses_total = 0
        selector_selected_route_reuses_total = 0
        build_work_orders_input_preparation_ms = $InputMilliseconds
        build_work_orders_non_extraction_ms = $NonExtractionMilliseconds
        build_work_orders_reserve_extraction_ms = $ReserveExtractionMilliseconds
        build_work_orders_finalization_ms = $FinalizationMilliseconds
    }
}

function New-RunFixture {
    param(
        [string]$Root,
        [int]$TrialIndex,
        [int]$VariableSpikeSequence,
        [string]$ProcessExecutable,
        [string]$RunnerExecutable,
        [switch]$OmitReserveColumn,
        [switch]$ImpossibleReconciliation,
        [switch]$ParentOverWall,
        [switch]$ZeroChildren,
        [switch]$ShiftTicks
    )
    $pairDirectory = Join-Path $Root ("trial-" + $TrialIndex)
    $metricsDirectory = Join-Path $pairDirectory "metrics-on"
    [System.IO.Directory]::CreateDirectory($metricsDirectory) | Out-Null
    [System.IO.Directory]::CreateDirectory((Join-Path $pairDirectory "metrics-off")) | Out-Null
    $resultPath = Join-Path $metricsDirectory "perf-results.json"
    $equivalencePath = Join-Path $pairDirectory "equivalence-results.json"
    $runtimePath = Join-Path $metricsDirectory "runtime-batch-metrics-v5.csv"

    $result = [ordered]@{
        schemaVersion = 6
        capturedUtc = "2026-08-01T00:00:0$TrialIndex.0000000Z"
        exactInvocation = "fixture-runner --trial-index $TrialIndex --metrics on"
        configuration = [ordered]@{
            scenarioId = "balanced_basin"
            simulationSeed = 1337
            citizenCount = 16
            warmupTicks = 2
            measuredTicks = 300
            metricsEnabled = $true
            outputDirectory = [System.IO.Path]::GetFullPath($metricsDirectory)
            gitSha = "0123456789abcdef"
            gitDirty = $true
            executionRoute = "export_release"
            cacheMode = "cold"
            selectorMode = "exact_branch_and_bound"
            extractionPlanningMode = "exact_bounded"
            routeDistanceMode = "cached_distance_only"
            trialIndex = $TrialIndex
            runnerExecutablePath = $RunnerExecutable
        }
        environment = [ordered]@{
            verifiedReleaseExecution = $true
            managedAssemblyConfiguration = "ExportRelease"
            machineName = "fixture-machine"
            logicalProcessorCount = 8
            operatingSystem = "fixture-os"
            processArchitecture = "X64"
            dotnetRuntime = ".NET fixture"
            godotVersion = "4.6.2-stable"
            processExecutablePath = $ProcessExecutable
        }
        measuredStartSimulationTick = 2
        finalSimulationTick = 302
        hashes = [ordered]@{
            snapshotSha256 = ('a' * 64)
            eventLogSha256 = ('b' * 64)
            deterministicStateAndEventSha256 = ('c' * 64)
        }
        artifacts = [ordered]@{
            runtimeMetricsCsv = [System.IO.Path]::GetFullPath($runtimePath)
            performanceResults = [System.IO.Path]::GetFullPath($resultPath)
        }
    }
    Write-Utf8NoBom $resultPath ($result | ConvertTo-Json -Depth 8)

    $equivalence = [ordered]@{
        schemaVersion = 6
        status = "pass_dirty_source"
        contractStatus = "pass_dirty_source"
        sourceClean = $false
        releaseExport = $TrialIndex -eq 1
        reusedReleaseRunner = $TrialIndex -ne 1
        exportEditorExecutable = if ($TrialIndex -eq 1) { $ProcessExecutable } else { $null }
        executionRoute = "export_release"
        runnerExecutable = $RunnerExecutable
        exportOutputExecutable = $ProcessExecutable
        metricsOffHash = ('c' * 64)
        metricsOnHash = ('c' * 64)
        metricsOnResult = [System.IO.Path]::GetFullPath($resultPath)
    }
    foreach ($property in @(
        "releaseEnvironmentValid", "resultSchemaValid", "configurationMatches", "commandConfigurationMatches",
        "modeContractValid", "executionRouteValid", "gitIdentityMatches", "environmentMatches", "godotVersionValid",
        "hashesValid", "snapshotHashMatches", "eventLogHashMatches", "combinedHashMatches", "resultStatusesValid",
        "artifactContractValid", "processExecutableMatches", "tickBoundsMatch", "matrixSchemaValid",
        "metricsOffRuntimeMetricsAbsent", "metricsOnRuntimeMetricsValid")) {
        $equivalence[$property] = $true
    }
    Write-Utf8NoBom $equivalencePath ($equivalence | ConvertTo-Json -Depth 8)

    $rows = @()
    for ($sequence = 0; $sequence -lt 300; $sequence++) {
        if ($sequence -eq 10 -or $sequence -eq $VariableSpikeSequence) {
            if ($ImpossibleReconciliation -and $sequence -eq 10) {
                $rows += New-MetricsRow $sequence 60.0 20.0
            }
            elseif ($ParentOverWall -and $sequence -eq 10) {
                $rows += New-MetricsRow $sequence 30.0 35.0
            }
            elseif ($ZeroChildren) {
                $rows += New-MetricsRow $sequence 60.0 35.0 0.0 0.0 0.0 0.0
            }
            else {
                $rows += New-MetricsRow $sequence 60.0 35.0 -ReserveExtractionMilliseconds (15.0 + $TrialIndex)
            }
        }
        else {
            $rows += New-MetricsRow $sequence 40.0 30.0
        }
    }
    if ($ShiftTicks) {
        $rows[0].start_simulation_tick = 3
        $rows[0].end_simulation_tick = 4
    }
    $csvRows = @($rows | ForEach-Object {
        $record = [ordered]@{}
        foreach ($entry in $_.GetEnumerator()) {
            if (-not ($OmitReserveColumn -and $entry.Key -eq "build_work_orders_reserve_extraction_ms")) {
                $record[$entry.Key] = $entry.Value
            }
        }
        [pscustomobject]$record
    })
    $csv = @($csvRows | ConvertTo-Csv -NoTypeInformation | ForEach-Object { $_.Replace('"', '') })
    Write-Utf8NoBom $runtimePath (($csv -join [Environment]::NewLine) + [Environment]::NewLine)
    return $metricsDirectory
}

function New-FixtureSet {
    param(
        [string]$Root,
        [string]$ProcessExecutable,
        [string]$RunnerExecutable,
        [hashtable]$FirstTrialOptions = @{}
    )
    $inputs = @()
    foreach ($trialIndex in 1..3) {
        $parameters = @{
            Root = $Root
            TrialIndex = $trialIndex
            VariableSpikeSequence = 19 + $trialIndex
            ProcessExecutable = $ProcessExecutable
            RunnerExecutable = $RunnerExecutable
        }
        if ($trialIndex -eq 1) {
            foreach ($entry in $FirstTrialOptions.GetEnumerator()) { $parameters[$entry.Key] = $entry.Value }
        }
        $inputs += New-RunFixture @parameters
    }
    return $inputs
}

function Update-JsonFile {
    param([string]$Path, [scriptblock]$Mutation)
    $value = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    & $Mutation $value
    Write-Utf8NoBom $Path ($value | ConvertTo-Json -Depth 12)
}

function Invoke-ExpectedFailure {
    param([string[]]$InputPath, [string]$OutputPath, [string]$MessagePattern, [switch]$AllowDirty)
    try {
        if ($AllowDirty) {
            & $AnalyzerPath -InputPath $InputPath -OutputPath $OutputPath -AllowDirtySource | Out-Null
        }
        else {
            & $AnalyzerPath -InputPath $InputPath -OutputPath $OutputPath | Out-Null
        }
        throw "Analyzer unexpectedly accepted invalid fixture; expected '$MessagePattern'."
    }
    catch {
        if ($_.Exception.Message -notmatch $MessagePattern) {
            throw "Analyzer failed for the wrong reason. Expected '$MessagePattern'; got '$($_.Exception.Message)'."
        }
    }
}

$resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($TemporaryRoot).TrimEnd('\')
$ownedRoot = Join-Path $resolvedTemporaryRoot ("societies-build-work-orders-profile-tests-" + [Guid]::NewGuid().ToString("N"))
[System.IO.Directory]::CreateDirectory($ownedRoot) | Out-Null

try {
    $releaseRoot = Join-Path $ownedRoot "release"
    [System.IO.Directory]::CreateDirectory($releaseRoot) | Out-Null
    $processExecutable = Join-Path $releaseRoot "SocietiesPerformance.exe"
    $runnerExecutable = Join-Path $releaseRoot "SocietiesPerformance.console.exe"
    Write-Utf8NoBom $processExecutable "fixture process executable"
    Write-Utf8NoBom $runnerExecutable "fixture console executable"

    $validRoot = Join-Path $ownedRoot "valid"
    $validInputs = @(New-FixtureSet $validRoot $processExecutable $runnerExecutable)
    $validOutput = Join-Path $validRoot "analysis.json"
    & $AnalyzerPath -InputPath $validInputs -OutputPath $validOutput -AllowDirtySource | Out-Null
    $repeatOutput = Join-Path $validRoot "analysis-repeat.json"
    & $AnalyzerPath -InputPath $validInputs -OutputPath $repeatOutput -AllowDirtySource | Out-Null
    $analysis = Get-Content -Raw -LiteralPath $validOutput | ConvertFrom-Json
    Assert-True ($analysis.schemaVersion -eq 2 -and $analysis.analyzerVersion -eq "2.0.0") "Analyzer schema/version mismatch."
    Assert-True ($analysis.status -eq "diagnostic_subcost_selected") "Valid profile must select a sub-cost."
    Assert-True ($analysis.selectedSubCost.category -eq "reserve_extraction") "Reserve extraction should dominate the fixture."
    Assert-True ($analysis.repeatability.commonSpikeTickCount -eq 1) "Fixture should have one common spike tick."
    Assert-True ($analysis.repeatability.unionSpikeTickCount -eq 4) "Fixture should have four union spike ticks."
    Assert-True ($analysis.repeatability.perTrialCommonSpikeSubCostSummaries.Count -eq 3) "Per-trial common summaries are missing."
    Assert-True ($analysis.repeatability.aggregateCommonSpikeSubCostSummary.reconciliation.exactRoundedIdentity -eq $true) "Parent reconciliation should be exact."
    Assert-True ($analysis.repeatability.perTrialVariance.reserve_extraction.perTrialCommonSpikeTotalMilliseconds.range -gt 0.0) "Per-trial variance must use the parsed sub-cost totals."
    Assert-True ($analysis.runs[0].metricsOffRuntimeProfileAbsent -eq $true) "Metrics-off absence must be recorded."
    Assert-True ($analysis.source.provenance -eq "uncommitted_diagnostic_source") "Dirty diagnostic provenance is missing."
    Assert-True ((Get-FileHash $validOutput -Algorithm SHA256).Hash -eq (Get-FileHash $repeatOutput -Algorithm SHA256).Hash) "Repeated profile outputs must be byte-identical."

    Invoke-ExpectedFailure $validInputs (Join-Path $validRoot "dirty-rejected.json") "requires -AllowDirtySource"
    Invoke-ExpectedFailure @($validInputs[0]) (Join-Path $validRoot "one-input.json") "exactly 3 input paths" -AllowDirty
    Invoke-ExpectedFailure @($validInputs[0..1]) (Join-Path $validRoot "two-inputs.json") "exactly 3 input paths" -AllowDirty
    Invoke-ExpectedFailure @($validInputs + $validInputs[0]) (Join-Path $validRoot "four-inputs.json") "exactly 3 input paths" -AllowDirty

    $missingRoot = Join-Path $ownedRoot "missing"
    $missingInputs = @(New-FixtureSet $missingRoot $processExecutable $runnerExecutable @{ OmitReserveColumn = $true })
    Invoke-ExpectedFailure $missingInputs (Join-Path $missingRoot "analysis.json") "exactly 40 columns" -AllowDirty

    $impossibleRoot = Join-Path $ownedRoot "impossible"
    $impossibleInputs = @(New-FixtureSet $impossibleRoot $processExecutable $runnerExecutable @{ ImpossibleReconciliation = $true })
    Invoke-ExpectedFailure $impossibleInputs (Join-Path $impossibleRoot "analysis.json") "child phase total exceeds its parent" -AllowDirty

    $missingIndexRoot = Join-Path $ownedRoot "missing-index"
    $missingIndexInputs = @(
        New-RunFixture $missingIndexRoot 1 20 $processExecutable $runnerExecutable
        New-RunFixture $missingIndexRoot 2 21 $processExecutable $runnerExecutable
        New-RunFixture $missingIndexRoot 4 22 $processExecutable $runnerExecutable
    )
    Invoke-ExpectedFailure $missingIndexInputs (Join-Path $missingIndexRoot "analysis.json") "indexes must be exactly" -AllowDirty

    $wrongRouteRoot = Join-Path $ownedRoot "wrong-route"
    $wrongRouteInputs = @(New-FixtureSet $wrongRouteRoot $processExecutable $runnerExecutable)
    Update-JsonFile (Join-Path $wrongRouteInputs[1] "perf-results.json") { param($json) $json.configuration.executionRoute = "existing_runner" }
    Update-JsonFile (Join-Path (Split-Path -Parent $wrongRouteInputs[1]) "equivalence-results.json") { param($json) $json.executionRoute = "existing_runner" }
    Invoke-ExpectedFailure $wrongRouteInputs (Join-Path $wrongRouteRoot "analysis.json") "export_release execution route" -AllowDirty

    $reuseRoot = Join-Path $ownedRoot "reuse-mismatch"
    $reuseInputs = @(New-FixtureSet $reuseRoot $processExecutable $runnerExecutable)
    Update-JsonFile (Join-Path (Split-Path -Parent $reuseInputs[1]) "equivalence-results.json") { param($json) $json.reusedReleaseRunner = $false }
    Invoke-ExpectedFailure $reuseInputs (Join-Path $reuseRoot "analysis.json") "Trial 1 must own" -AllowDirty

    $pathRoot = Join-Path $ownedRoot "path-mismatch"
    $pathInputs = @(New-FixtureSet $pathRoot $processExecutable $runnerExecutable)
    Update-JsonFile (Join-Path $pathInputs[0] "perf-results.json") { param($json) $json.artifacts.runtimeMetricsCsv = "C:\mismatched\runtime-batch-metrics-v5.csv" }
    Invoke-ExpectedFailure $pathInputs (Join-Path $pathRoot "analysis.json") "does not bind" -AllowDirty

    $hashRoot = Join-Path $ownedRoot "hash-mismatch"
    $hashInputs = @(New-FixtureSet $hashRoot $processExecutable $runnerExecutable)
    Update-JsonFile (Join-Path (Split-Path -Parent $hashInputs[0]) "equivalence-results.json") { param($json) $json.metricsOffHash = ('d' * 64); $json.metricsOnHash = ('d' * 64) }
    Invoke-ExpectedFailure $hashInputs (Join-Path $hashRoot "analysis.json") "does not match the performance result" -AllowDirty

    $shiftedRoot = Join-Path $ownedRoot "shifted-tick"
    $shiftedInputs = @(New-FixtureSet $shiftedRoot $processExecutable $runnerExecutable @{ ShiftTicks = $true })
    Invoke-ExpectedFailure $shiftedInputs (Join-Path $shiftedRoot "analysis.json") "zero-based, one-tick manual steps" -AllowDirty

    $parentRoot = Join-Path $ownedRoot "parent-over-wall"
    $parentInputs = @(New-FixtureSet $parentRoot $processExecutable $runnerExecutable @{ ParentOverWall = $true })
    Invoke-ExpectedFailure $parentInputs (Join-Path $parentRoot "analysis.json") "parent exceeds wall_ms" -AllowDirty

    $zeroRoot = Join-Path $ownedRoot "zero-child"
    $zeroInputs = @(
        New-RunFixture $zeroRoot 1 20 $processExecutable $runnerExecutable -ZeroChildren
        New-RunFixture $zeroRoot 2 21 $processExecutable $runnerExecutable -ZeroChildren
        New-RunFixture $zeroRoot 3 22 $processExecutable $runnerExecutable -ZeroChildren
    )
    $zeroOutput = Join-Path $zeroRoot "analysis.json"
    & $AnalyzerPath -InputPath $zeroInputs -OutputPath $zeroOutput -AllowDirtySource | Out-Null
    $zeroAnalysis = Get-Content -Raw $zeroOutput | ConvertFrom-Json
    Assert-True ($zeroAnalysis.status -eq "instrumentation_insufficient") "Zero-child spikes must not select a sub-cost."

    $oversizedJsonRoot = Join-Path $ownedRoot "oversized-json"
    $oversizedJsonInputs = @(New-FixtureSet $oversizedJsonRoot $processExecutable $runnerExecutable)
    Write-Utf8NoBom (Join-Path $oversizedJsonInputs[0] "perf-results.json") (' ' * (2MB + 1))
    Invoke-ExpectedFailure $oversizedJsonInputs (Join-Path $oversizedJsonRoot "analysis.json") "JSON limit" -AllowDirty

    $oversizedCsvRoot = Join-Path $ownedRoot "oversized-csv"
    $oversizedCsvInputs = @(New-FixtureSet $oversizedCsvRoot $processExecutable $runnerExecutable)
    Write-Utf8NoBom (Join-Path $oversizedCsvInputs[0] "runtime-batch-metrics-v5.csv") ('x' * (4MB + 1))
    Invoke-ExpectedFailure $oversizedCsvInputs (Join-Path $oversizedCsvRoot "analysis.json") "byte limit" -AllowDirty

    $tooManyRowsRoot = Join-Path $ownedRoot "too-many-rows"
    $tooManyRowsInputs = @(New-FixtureSet $tooManyRowsRoot $processExecutable $runnerExecutable)
    Write-Utf8NoBom (Join-Path $tooManyRowsInputs[0] "runtime-batch-metrics-v5.csv") ((@(1..302 | ForEach-Object { 'x' }) -join [Environment]::NewLine) + [Environment]::NewLine)
    Invoke-ExpectedFailure $tooManyRowsInputs (Join-Path $tooManyRowsRoot "analysis.json") "301-line limit" -AllowDirty

    $singleRowRoot = Join-Path $ownedRoot "oversized-row"
    $singleRowInputs = @(New-FixtureSet $singleRowRoot $processExecutable $runnerExecutable)
    Write-Utf8NoBom (Join-Path $singleRowInputs[0] "runtime-batch-metrics-v5.csv") (('x' * 16385) + [Environment]::NewLine)
    Invoke-ExpectedFailure $singleRowInputs (Join-Path $singleRowRoot "analysis.json") "character limit" -AllowDirty

    $deepRoot = Join-Path $ownedRoot "deep-json"
    $deepInputs = @(New-FixtureSet $deepRoot $processExecutable $runnerExecutable)
    Write-Utf8NoBom (Join-Path $deepInputs[0] "perf-results.json") ((('[' * 33) + '0' + (']' * 33)))
    Invoke-ExpectedFailure $deepInputs (Join-Path $deepRoot "analysis.json") "nesting depth" -AllowDirty

    Write-Output "PASS: BuildWorkOrders profile analyzer exact-route, provenance, timing, deterministic-output, and bounded-parser contract cases"
}
finally {
    $expectedPrefix = $resolvedTemporaryRoot + '\'
    $resolvedOwnedRoot = [System.IO.Path]::GetFullPath($ownedRoot)
    if (-not $resolvedOwnedRoot.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected test path: $resolvedOwnedRoot"
    }
    if (Test-Path -LiteralPath $resolvedOwnedRoot) {
        Remove-Item -LiteralPath $resolvedOwnedRoot -Recurse -Force
    }
}
