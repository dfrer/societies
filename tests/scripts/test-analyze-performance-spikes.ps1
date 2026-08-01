[CmdletBinding()]
param(
    [string]$AnalyzerPath,
    [string]$TemporaryRoot = [System.IO.Path]::GetTempPath()
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($AnalyzerPath)) {
    $repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $AnalyzerPath = Join-Path $repositoryRoot "scripts\analyze-performance-spikes.ps1"
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

function New-EquivalenceResult {
    param([string]$MetricsOnResultPath)
    $result = [ordered]@{
        schemaVersion = 6
        status = "pass"
        contractStatus = "pass"
        releaseExport = $true
        reusedReleaseRunner = $false
        metricsOffHash = "fixture-hash"
        metricsOnHash = "fixture-hash"
        metricsOnResult = [System.IO.Path]::GetFullPath($MetricsOnResultPath)
    }
    foreach ($property in @(
        "sourceClean", "releaseRequired", "releaseEnvironmentValid", "resultSchemaValid",
        "configurationMatches", "commandConfigurationMatches", "modeContractValid", "executionRouteValid",
        "gitIdentityMatches", "environmentMatches", "godotVersionValid", "hashesValid", "snapshotHashMatches",
        "eventLogHashMatches", "combinedHashMatches", "resultStatusesValid", "artifactContractValid",
        "cacheEvidencePairValid", "cacheEvidenceCommonValid", "cacheTransitionContractValid",
        "cacheDiagnosticsContractValid", "processExecutableMatches", "tickBoundsMatch", "matrixSchemaValid",
        "metricsOffRuntimeMetricsAbsent", "metricsOnRuntimeMetricsValid")) {
        $result[$property] = $true
    }
    return $result
}

function New-MetricsRow {
    param(
        [int]$Sequence,
        [double]$WallMilliseconds,
        [double]$BuildWorkOrdersMilliseconds = 30.0,
        [string]$WallOverride = $null
    )
    $startTick = $Sequence
    $endTick = $Sequence + 1
    return [ordered]@{
        sequence = $Sequence
        batch_kind = "manual_step"
        start_simulation_tick = $startTick
        end_simulation_tick = $endTick
        completed_ticks = 1
        wall_ms = if ([string]::IsNullOrEmpty($WallOverride)) { $WallMilliseconds } else { $WallOverride }
        max_tick_ms = $WallMilliseconds
        simulation_tick_ms = [Math]::Max(0.0, $WallMilliseconds - 3.0)
        session_advance_ms = [Math]::Max(0.0, $WallMilliseconds - 8.0)
        build_work_orders_ms = $BuildWorkOrdersMilliseconds
        harvest_apply_ms = 1.0
        scene_sync_ms = 5.0
        update_hud_ms = 2.0
        work_orders_generated_total = 0
        work_orders_generated_uncapped_total = 0
        work_orders_claimed_total = 0
        work_orders_remaining_last = 0
        path_plan_lookups_total = 0
        path_plan_cache_hits_total = 0
        citizens_evaluated_total = 0
        path_plan_cache_misses_total = 0
        path_plan_cache_size_last = 0
        navigation_invalidations_total = 0
        worker_count_last = 16
        idle_citizens_considering_work_orders_total = 0
        candidate_orders_evaluated_total = 0
        candidate_orders_per_idle_citizen = 0.0
        navigation_rebuild_ms = 2.0
        route_selection_ms = 10.0
        selector_candidates_bounded_total = 0
        selector_candidates_exact_scored_total = 0
        selector_candidates_pruned_total = 0
        selector_exact_path_queries_total = 0
        selector_path_cache_hits_total = 0
        selector_path_cache_misses_total = 0
        selector_selected_route_reuses_total = 0
    }
}

function New-RunFixture {
    param(
        [string]$Root,
        [string]$CaseId,
        [int]$TrialIndex,
        [object[]]$Rows,
        [switch]$OmitSceneSyncColumn,
        [ValidateSet(4, 5)][int]$RuntimeSchemaVersion = 4
    )
    $metricsDirectory = Join-Path (Join-Path $Root $CaseId) "metrics-on"
    [System.IO.Directory]::CreateDirectory($metricsDirectory) | Out-Null
    $resultPath = Join-Path $metricsDirectory "perf-results.json"
    $equivalencePath = Join-Path (Split-Path -Parent $metricsDirectory) "equivalence-results.json"
    $runtimePath = Join-Path $metricsDirectory "runtime-batch-metrics-v$RuntimeSchemaVersion.csv"

    $result = [ordered]@{
        schemaVersion = 6
        configuration = [ordered]@{
            scenarioId = "fixture"
            simulationSeed = 1337
            citizenCount = 16
            warmupTicks = 2
            measuredTicks = $Rows.Count
            metricsEnabled = $true
            gitSha = "0123456789abcdef"
            gitDirty = $false
            executionRoute = "export_release"
            cacheMode = "cold"
            selectorMode = "exact_branch_and_bound"
            extractionPlanningMode = "exact_bounded"
            trialIndex = $TrialIndex
        }
        environment = [ordered]@{
            verifiedReleaseExecution = $true
            managedAssemblyConfiguration = "ExportRelease"
            machineName = "fixture-machine"
            logicalProcessorCount = 8
            processArchitecture = "X64"
            dotnetRuntime = "fixture-runtime"
            godotVersion = "4.6.2"
        }
    }
    Write-Utf8NoBom $resultPath ($result | ConvertTo-Json -Depth 8)
    Write-Utf8NoBom $equivalencePath ((New-EquivalenceResult $resultPath) | ConvertTo-Json -Depth 8)

    $csvRows = @($Rows | ForEach-Object {
        $record = [ordered]@{}
        foreach ($entry in $_.GetEnumerator()) {
            if (-not ($OmitSceneSyncColumn -and $entry.Key -eq "scene_sync_ms")) {
                $record[$entry.Key] = $entry.Value
            }
        }
        if ($RuntimeSchemaVersion -eq 5) {
            $record["build_work_orders_input_preparation_ms"] = 1.0
            $record["build_work_orders_non_extraction_ms"] = 5.0
            $record["build_work_orders_reserve_extraction_ms"] = 20.0
            $record["build_work_orders_finalization_ms"] = 4.0
        }
        [pscustomobject]$record
    })
    $csv = @($csvRows | ConvertTo-Csv -NoTypeInformation | ForEach-Object { $_.Replace('"', '') })
    Write-Utf8NoBom $runtimePath (($csv -join [Environment]::NewLine) + [Environment]::NewLine)
    return $metricsDirectory
}

function Invoke-ExpectedFailure {
    param([string[]]$InputPath, [string]$OutputPath, [string]$MessagePattern)
    try {
        & $AnalyzerPath -InputPath $InputPath -OutputPath $OutputPath | Out-Null
        throw "Analyzer unexpectedly accepted invalid fixture; expected '$MessagePattern'."
    }
    catch {
        if ($_.Exception.Message -notmatch $MessagePattern) {
            throw "Analyzer failed for the wrong reason. Expected '$MessagePattern'; got '$($_.Exception.Message)'."
        }
    }
}

$resolvedTemporaryRoot = [System.IO.Path]::GetFullPath($TemporaryRoot).TrimEnd('\')
$ownedRoot = Join-Path $resolvedTemporaryRoot ("societies-spike-analyzer-tests-" + [Guid]::NewGuid().ToString("N"))
[System.IO.Directory]::CreateDirectory($ownedRoot) | Out-Null

try {
    $validRoot = Join-Path $ownedRoot "valid"
    $validInputs = @(
        New-RunFixture $validRoot "trial-1" 1 @(
            (New-MetricsRow 0 60.0),
            (New-MetricsRow 1 40.0 10.0)
        )
        New-RunFixture $validRoot "trial-2" 2 @(
            (New-MetricsRow 0 61.0),
            (New-MetricsRow 1 55.0 20.0)
        )
        New-RunFixture $validRoot "trial-3" 3 @(
            (New-MetricsRow 0 62.0),
            (New-MetricsRow 1 45.0 15.0)
        )
    )
    $validOutput1 = Join-Path $validRoot "analysis-1.json"
    $validOutput2 = Join-Path $validRoot "analysis-2.json"
    & $AnalyzerPath -InputPath $validInputs -OutputPath $validOutput1 | Out-Null
    & $AnalyzerPath -InputPath $validInputs -OutputPath $validOutput2 | Out-Null
    $analysis = Get-Content -Raw -LiteralPath $validOutput1 | ConvertFrom-Json
    $repeatability = @($analysis.repeatedSpikeTickSetIdentity)[0]
    Assert-True ($analysis.schemaVersion -eq 3 -and $analysis.analyzerVersion -eq "2.2.0") "Analyzer schema/version mismatch."
    Assert-True (($analysis.runtimeMetricsSchemaVersions -join ',') -eq "4") "Historical v4 analysis schema provenance is missing."
    Assert-True ($analysis.runtimeMetricsSchemaVersion -eq 4) "Historical v4 scalar schema provenance must remain available."
    Assert-True ($repeatability.allRunsExactTickSetIdentity -eq $false) "Unequal fixture spike sets must not report exact identity."
    Assert-True (($repeatability.commonSpikeEndSimulationTicks -join ',') -eq "1") "Common fixture spike tick must be 1."
    Assert-True (($repeatability.unionSpikeEndSimulationTicks -join ',') -eq "1,2") "Union fixture spike ticks must be 1,2."
    $trial1Tick1 = @($repeatability.unionTickPerTrialPhaseBreakdown[0].trials | Where-Object trialIndex -eq 1)[0]
    Assert-True ($trial1Tick1.phaseBreakdown.measuredLeafPhaseTotalMilliseconds -eq 50.0) "Leaf phase sum mismatch."
    Assert-True ($trial1Tick1.phaseBreakdown.residualUnattributedMilliseconds -eq 10.0) "Residual calculation mismatch."
    Assert-True ($repeatability.commonSpikeTicksAcrossRunsPhaseSummary.dominantExercisedCostByTotal.category -eq "build_work_orders") "Dominant common exercised cost mismatch."
    Assert-True ((Get-FileHash -LiteralPath $validOutput1 -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $validOutput2 -Algorithm SHA256).Hash) "Repeated analyzer outputs must be byte-identical."

    $v5Root = Join-Path $ownedRoot "v5"
    $v5Input = New-RunFixture $v5Root "trial-1" 1 @((New-MetricsRow 0 60.0)) -RuntimeSchemaVersion 5
    $v5Output = Join-Path $v5Root "analysis.json"
    & $AnalyzerPath -InputPath @($v5Input) -OutputPath $v5Output | Out-Null
    $v5Analysis = Get-Content -Raw -LiteralPath $v5Output | ConvertFrom-Json
    Assert-True (($v5Analysis.runtimeMetricsSchemaVersions -join ',') -eq "5") "Runtime v5 analysis schema provenance is missing."
    Assert-True ($v5Analysis.runtimeMetricsSchemaVersion -eq 5) "Runtime v5 scalar schema provenance is missing."
    Assert-True ($v5Analysis.runs[0].runtimeMetricsSchemaVersion -eq 5) "Runtime v5 run provenance is missing."

    $mixedRoot = Join-Path $ownedRoot "mixed"
    $mixedInputs = @(
        New-RunFixture $mixedRoot "trial-1" 1 @((New-MetricsRow 0 60.0)) -RuntimeSchemaVersion 4
        New-RunFixture $mixedRoot "trial-2" 2 @((New-MetricsRow 0 61.0)) -RuntimeSchemaVersion 5
    )
    Invoke-ExpectedFailure $mixedInputs (Join-Path $mixedRoot "analysis.json") "cannot mix runtime metrics schema versions"

    $highResidualRoot = Join-Path $ownedRoot "high-residual"
    $highResidualInput = New-RunFixture $highResidualRoot "trial-1" 1 @((New-MetricsRow 0 100.0 5.0))
    $highResidualOutput = Join-Path $highResidualRoot "analysis.json"
    & $AnalyzerPath -InputPath @($highResidualInput) -OutputPath $highResidualOutput | Out-Null
    $highResidualAnalysis = Get-Content -Raw -LiteralPath $highResidualOutput | ConvertFrom-Json
    $highResidualTick = @($highResidualAnalysis.runs[0].spikesAboveFirstThreshold.ticks)[0]
    Assert-True ($highResidualTick.residualUnattributedMilliseconds -gt $highResidualTick.dominantExercisedCost.milliseconds) "High-residual fixture must exercise the residual exclusion boundary."
    Assert-True ($highResidualTick.dominantExercisedCost.category -eq "route_selection") "Residual must not become the per-tick dominant exercised production cost."
    Assert-True ($highResidualAnalysis.runs[0].spikesAboveFirstThreshold.phaseSummary.dominantExercisedCostByTotal.category -eq "route_selection") "Residual must not become the aggregate dominant exercised production cost."

    $missingRoot = Join-Path $ownedRoot "missing-column"
    $missingInput = New-RunFixture $missingRoot "trial-1" 1 @((New-MetricsRow 0 60.0)) -OmitSceneSyncColumn
    Invoke-ExpectedFailure @($missingInput) (Join-Path $missingRoot "analysis.json") "missing 'scene_sync_ms'"

    $malformedRoot = Join-Path $ownedRoot "malformed"
    $malformedInput = New-RunFixture $malformedRoot "trial-1" 1 @((New-MetricsRow 0 60.0 30.0 "not-a-number"))
    Invoke-ExpectedFailure @($malformedInput) (Join-Path $malformedRoot "analysis.json") "not a valid invariant double"

    $negativeResidualRoot = Join-Path $ownedRoot "negative-residual"
    $negativeResidualInput = New-RunFixture $negativeResidualRoot "trial-1" 1 @((New-MetricsRow 0 40.0 30.0))
    Invoke-ExpectedFailure @($negativeResidualInput) (Join-Path $negativeResidualRoot "analysis.json") "leaf phase total exceeds wall time"

    $oversizedJsonRoot = Join-Path $ownedRoot "oversized-json"
    $oversizedJsonInput = New-RunFixture $oversizedJsonRoot "trial-1" 1 @((New-MetricsRow 0 60.0))
    Write-Utf8NoBom (Join-Path $oversizedJsonInput "perf-results.json") (' ' * (4MB + 1))
    Invoke-ExpectedFailure @($oversizedJsonInput) (Join-Path $oversizedJsonRoot "analysis.json") "JSON limit"

    $oversizedCsvRoot = Join-Path $ownedRoot "oversized-csv"
    $oversizedCsvInput = New-RunFixture $oversizedCsvRoot "trial-1" 1 @((New-MetricsRow 0 60.0))
    Write-Utf8NoBom (Join-Path $oversizedCsvInput "runtime-batch-metrics-v4.csv") ('x' * (16MB + 1))
    Invoke-ExpectedFailure @($oversizedCsvInput) (Join-Path $oversizedCsvRoot "analysis.json") "byte limit"

    $tooManyRowsRoot = Join-Path $ownedRoot "too-many-rows"
    $tooManyRowsInput = New-RunFixture $tooManyRowsRoot "trial-1" 1 @((New-MetricsRow 0 60.0))
    Write-Utf8NoBom (Join-Path $tooManyRowsInput "runtime-batch-metrics-v4.csv") ((@(1..10002 | ForEach-Object { 'x' }) -join [Environment]::NewLine) + [Environment]::NewLine)
    Invoke-ExpectedFailure @($tooManyRowsInput) (Join-Path $tooManyRowsRoot "analysis.json") "10001-line limit"

    $oversizedRowRoot = Join-Path $ownedRoot "oversized-row"
    $oversizedRowInput = New-RunFixture $oversizedRowRoot "trial-1" 1 @((New-MetricsRow 0 60.0))
    Write-Utf8NoBom (Join-Path $oversizedRowInput "runtime-batch-metrics-v4.csv") (('x' * 65537) + [Environment]::NewLine)
    Invoke-ExpectedFailure @($oversizedRowInput) (Join-Path $oversizedRowRoot "analysis.json") "character limit"

    $deepJsonRoot = Join-Path $ownedRoot "deep-json"
    $deepJsonInput = New-RunFixture $deepJsonRoot "trial-1" 1 @((New-MetricsRow 0 60.0))
    Write-Utf8NoBom (Join-Path $deepJsonInput "perf-results.json") ((('[' * 65) + '0' + (']' * 65)))
    Invoke-ExpectedFailure @($deepJsonInput) (Join-Path $deepJsonRoot "analysis.json") "nesting depth"

    Write-Output "PASS: analyzer v4/v5 provenance, mixed-schema rejection, determinism, attribution, malformed inputs, and bounded JSON/CSV parser cases"
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
