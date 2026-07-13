[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [double[]]$ThresholdMilliseconds = @(50.0, 250.0, 1000.0)
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$requiredColumns = @(
    "sequence", "batch_kind", "start_simulation_tick", "end_simulation_tick", "completed_ticks",
    "wall_ms", "max_tick_ms", "simulation_tick_ms", "session_advance_ms", "build_work_orders_ms",
    "harvest_apply_ms", "scene_sync_ms", "update_hud_ms", "path_plan_lookups_total",
    "path_plan_cache_hits_total", "path_plan_cache_misses_total", "navigation_invalidations_total",
    "navigation_rebuild_ms", "route_selection_ms", "selector_exact_path_queries_total",
    "selector_path_cache_hits_total", "selector_path_cache_misses_total"
)
$nonNegativeCounterColumns = @(
    "sequence", "start_simulation_tick", "end_simulation_tick", "completed_ticks",
    "work_orders_generated_total", "work_orders_generated_uncapped_total",
    "work_orders_claimed_total", "work_orders_remaining_last", "path_plan_lookups_total",
    "path_plan_cache_hits_total", "citizens_evaluated_total", "path_plan_cache_misses_total",
    "path_plan_cache_size_last", "navigation_invalidations_total", "worker_count_last",
    "idle_citizens_considering_work_orders_total", "candidate_orders_evaluated_total",
    "selector_candidates_bounded_total", "selector_candidates_exact_scored_total",
    "selector_candidates_pruned_total", "selector_exact_path_queries_total",
    "selector_path_cache_hits_total", "selector_path_cache_misses_total",
    "selector_selected_route_reuses_total"
)
$nonNegativeDoubleColumns = @(
    "wall_ms", "max_tick_ms", "simulation_tick_ms", "session_advance_ms", "build_work_orders_ms",
    "harvest_apply_ms", "scene_sync_ms", "update_hud_ms", "candidate_orders_per_idle_citizen",
    "navigation_rebuild_ms", "route_selection_ms"
)

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Read-Json {
    param([string]$Path, [string]$Label)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label is missing: $Path"
    }
    try {
        return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    }
    catch {
        throw "$Label is not valid JSON: $Path. $($_.Exception.Message)"
    }
}

function Get-Sha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Require-Property {
    param([object]$Object, [string]$Name, [string]$Label)
    if ($null -eq $Object -or $null -eq $Object.PSObject.Properties[$Name] -or $null -eq $Object.$Name) {
        throw "$Label is missing required property '$Name'."
    }
    return $Object.$Name
}

function Parse-Double {
    param([string]$Value, [string]$Label)
    $parsed = 0.0
    if (-not [double]::TryParse(
        $Value,
        [System.Globalization.NumberStyles]::Float,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [ref]$parsed)) {
        throw "$Label is not a valid invariant double: '$Value'."
    }
    if ([double]::IsNaN($parsed) -or [double]::IsInfinity($parsed)) {
        throw "$Label must be finite: '$Value'."
    }
    return $parsed
}

function Parse-Int64 {
    param([string]$Value, [string]$Label)
    $parsed = [long]0
    if (-not [long]::TryParse(
        $Value,
        [System.Globalization.NumberStyles]::Integer,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [ref]$parsed)) {
        throw "$Label is not a valid invariant integer: '$Value'."
    }
    return $parsed
}

function Round-Value {
    param([double]$Value)
    return [Math]::Round($Value, 6, [MidpointRounding]::AwayFromZero)
}

function Get-Percent {
    param([double]$Part, [double]$Whole)
    if ($Whole -le 0.0) { return 0.0 }
    return Round-Value (100.0 * $Part / $Whole)
}

function Get-DisplayPath {
    param([string]$Path)
    $full = [System.IO.Path]::GetFullPath($Path)
    $prefix = $repoRoot + '\'
    if ($full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($prefix.Length).Replace('\', '/')
    }
    return $full.Replace('\', '/')
}

function Get-PearsonCorrelation {
    param([object[]]$Ticks, [string]$Metric)
    if ($Ticks.Count -lt 2) {
        return [ordered]@{ metric = $Metric; status = "insufficient_samples"; sampleCount = $Ticks.Count; coefficient = $null }
    }

    $x = @($Ticks | ForEach-Object { [double]$_.wallMs })
    $y = @($Ticks | ForEach-Object { [double]$_.$Metric })
    $meanX = ($x | Measure-Object -Average).Average
    $meanY = ($y | Measure-Object -Average).Average
    $numerator = 0.0
    $sumX = 0.0
    $sumY = 0.0
    for ($index = 0; $index -lt $x.Count; $index++) {
        $dx = $x[$index] - $meanX
        $dy = $y[$index] - $meanY
        $numerator += $dx * $dy
        $sumX += $dx * $dx
        $sumY += $dy * $dy
    }
    if ($sumX -le 0.0 -or $sumY -le 0.0) {
        return [ordered]@{ metric = $Metric; status = "no_variance"; sampleCount = $Ticks.Count; coefficient = $null }
    }
    return [ordered]@{
        metric = $Metric
        status = "computed"
        sampleCount = $Ticks.Count
        coefficient = Round-Value ($numerator / [Math]::Sqrt($sumX * $sumY))
    }
}

function Get-DominantPhase {
    param([object]$Tick)
    $otherSimulation = [Math]::Max(
        0.0,
        $Tick.simulationTickMs - $Tick.buildWorkOrdersMs - $Tick.routeSelectionMs -
            $Tick.navigationRebuildMs - $Tick.harvestApplyMs - $Tick.sceneSyncMs)
    $otherWall = [Math]::Max(0.0, $Tick.wallMs - $Tick.simulationTickMs - $Tick.updateHudMs)
    $phases = [ordered]@{
        build_work_orders = $Tick.buildWorkOrdersMs
        route_selection = $Tick.routeSelectionMs
        navigation_rebuild = $Tick.navigationRebuildMs
        harvest_apply = $Tick.harvestApplyMs
        scene_sync = $Tick.sceneSyncMs
        update_hud = $Tick.updateHudMs
        other_simulation = $otherSimulation
        other_wall = $otherWall
    }
    $bestName = $null
    $bestValue = -1.0
    foreach ($entry in $phases.GetEnumerator()) {
        if ([double]$entry.Value -gt $bestValue) {
            $bestName = [string]$entry.Key
            $bestValue = [double]$entry.Value
        }
    }
    return [ordered]@{
        phase = $bestName
        milliseconds = Round-Value $bestValue
        wallSharePercent = Get-Percent $bestValue $Tick.wallMs
    }
}

if ($ThresholdMilliseconds.Count -lt 1) {
    throw "At least one threshold is required."
}
for ($index = 0; $index -lt $ThresholdMilliseconds.Count; $index++) {
    if ([double]::IsNaN($ThresholdMilliseconds[$index]) -or
        [double]::IsInfinity($ThresholdMilliseconds[$index]) -or
        $ThresholdMilliseconds[$index] -le 0.0) {
        throw "Thresholds must be finite positive numbers."
    }
    if ($index -gt 0 -and $ThresholdMilliseconds[$index] -le $ThresholdMilliseconds[$index - 1]) {
        throw "Thresholds must be strictly increasing and unique."
    }
}

$runtimePaths = New-Object System.Collections.Generic.List[string]
foreach ($input in $InputPath) {
    if (-not (Test-Path -LiteralPath $input)) {
        throw "InputPath does not exist: $input"
    }
    $item = Get-Item -LiteralPath $input
    if (-not $item.PSIsContainer) {
        if ($item.Name -ne "runtime-batch-metrics-v4.csv") {
            throw "Input files must be named runtime-batch-metrics-v4.csv: $($item.FullName)"
        }
        $runtimePaths.Add($item.FullName)
        continue
    }
    $direct = Join-Path $item.FullName "runtime-batch-metrics-v4.csv"
    if (Test-Path -LiteralPath $direct -PathType Leaf) {
        $runtimePaths.Add((Get-Item -LiteralPath $direct).FullName)
        continue
    }
    $found = @(Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Filter "runtime-batch-metrics-v4.csv" |
        Sort-Object FullName)
    if ($found.Count -eq 0) {
        throw "No runtime-batch-metrics-v4.csv artifacts were found under: $($item.FullName)"
    }
    foreach ($file in $found) { $runtimePaths.Add($file.FullName) }
}
$runtimePaths = @($runtimePaths | Sort-Object -Unique)
if ($runtimePaths.Count -eq 0) { throw "No runtime metrics artifacts were supplied." }

$runs = New-Object System.Collections.Generic.List[object]
$internalRuns = New-Object System.Collections.Generic.List[object]
$compatibilityKey = $null
$compatibility = $null

foreach ($runtimePath in $runtimePaths) {
    $runDirectory = Split-Path -Parent $runtimePath
    $resultPath = Join-Path $runDirectory "perf-results.json"
    $caseDirectory = Split-Path -Parent $runDirectory
    $equivalencePath = Join-Path $caseDirectory "equivalence-results.json"
    $result = Read-Json $resultPath "Performance result"
    $equivalence = Read-Json $equivalencePath "Equivalence result"
    $resultSchemaVersion = [int](Require-Property $result "schemaVersion" $resultPath)
    if ($resultSchemaVersion -notin @(5, 6)) {
        throw "Performance result must use schemaVersion 5 or 6: $resultPath"
    }
    $equivalenceSchemaVersion = [int](Require-Property $equivalence "schemaVersion" $equivalencePath)
    if ($equivalenceSchemaVersion -notin @(5, 6)) {
        throw "Equivalence result must use schemaVersion 5 or 6: $equivalencePath"
    }
    foreach ($contractProperty in @(
        "sourceClean", "releaseRequired", "releaseEnvironmentValid", "resultSchemaValid",
        "configurationMatches", "commandConfigurationMatches", "modeContractValid", "executionRouteValid",
        "gitIdentityMatches", "environmentMatches", "godotVersionValid", "hashesValid", "snapshotHashMatches",
        "eventLogHashMatches", "combinedHashMatches", "resultStatusesValid", "artifactContractValid",
        "cacheEvidencePairValid", "cacheEvidenceCommonValid", "cacheTransitionContractValid",
        "cacheDiagnosticsContractValid", "processExecutableMatches", "tickBoundsMatch", "matrixSchemaValid",
        "metricsOffRuntimeMetricsAbsent", "metricsOnRuntimeMetricsValid")) {
        if ((Require-Property $equivalence $contractProperty $equivalencePath) -ne $true) {
            throw "Equivalence contract '$contractProperty' is not satisfied: $equivalencePath"
        }
    }
    if ($equivalence.releaseExport -ne $true -and $equivalence.reusedReleaseRunner -ne $true) {
        throw "Equivalence must use a new or reused verified Release runner: $equivalencePath"
    }
    if ([string](Require-Property $equivalence "status" $equivalencePath) -ne "pass" -or
        [string](Require-Property $equivalence "contractStatus" $equivalencePath) -ne "pass") {
        throw "Equivalence status and contractStatus must both pass: $equivalencePath"
    }
    $metricsOffHash = [string](Require-Property $equivalence "metricsOffHash" $equivalencePath)
    $metricsOnHash = [string](Require-Property $equivalence "metricsOnHash" $equivalencePath)
    if ([string]::IsNullOrWhiteSpace($metricsOffHash) -or $metricsOffHash -ne $metricsOnHash) {
        throw "Equivalence state/event hashes must be present and equal: $equivalencePath"
    }
    $declaredMetricsOnResult = [System.IO.Path]::GetFullPath(
        [string](Require-Property $equivalence "metricsOnResult" $equivalencePath))
    if (-not $declaredMetricsOnResult.Equals(
        [System.IO.Path]::GetFullPath($resultPath),
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Equivalence result does not identify the analyzed metrics-on result: $equivalencePath"
    }
    $configuration = Require-Property $result "configuration" $resultPath
    $environment = Require-Property $result "environment" $resultPath
    if ((Require-Property $configuration "metricsEnabled" $resultPath) -ne $true) {
        throw "Runtime metrics CSV must belong to a metrics-enabled result: $resultPath"
    }
    if ((Require-Property $configuration "gitDirty" $resultPath) -ne $false) {
        throw "Spike analysis rejects dirty-source evidence: $resultPath"
    }
    if ((Require-Property $environment "verifiedReleaseExecution" $resultPath) -ne $true -or
        [string](Require-Property $environment "managedAssemblyConfiguration" $resultPath) -ne "ExportRelease") {
        throw "Spike analysis requires verified ExportRelease evidence: $resultPath"
    }

    $currentCompatibility = [ordered]@{
        gitSha = [string](Require-Property $configuration "gitSha" $resultPath)
        gitDirty = [bool](Require-Property $configuration "gitDirty" $resultPath)
        executionRoute = [string](Require-Property $configuration "executionRoute" $resultPath)
        machineName = [string](Require-Property $environment "machineName" $resultPath)
        logicalProcessorCount = [int](Require-Property $environment "logicalProcessorCount" $resultPath)
        processArchitecture = [string](Require-Property $environment "processArchitecture" $resultPath)
        dotnetRuntime = [string](Require-Property $environment "dotnetRuntime" $resultPath)
        godotVersion = [string](Require-Property $environment "godotVersion" $resultPath)
        managedAssemblyConfiguration = [string]$environment.managedAssemblyConfiguration
    }
    $currentKey = $currentCompatibility | ConvertTo-Json -Compress
    if ($null -eq $compatibilityKey) {
        $compatibilityKey = $currentKey
        $compatibility = $currentCompatibility
    }
    elseif ($currentKey -ne $compatibilityKey) {
        throw "Input evidence is incompatible with the first run: $resultPath"
    }

    $header = Get-Content -LiteralPath $runtimePath -TotalCount 1
    if ([string]::IsNullOrWhiteSpace($header)) { throw "Runtime CSV is empty: $runtimePath" }
    $columns = @($header.Split(','))
    foreach ($required in $requiredColumns) {
        if ($columns -notcontains $required) { throw "Runtime CSV is missing '$required': $runtimePath" }
    }
    foreach ($counterColumn in $nonNegativeCounterColumns) {
        if ($columns -notcontains $counterColumn) { throw "Runtime CSV is missing counter '$counterColumn': $runtimePath" }
    }
    foreach ($doubleColumn in $nonNegativeDoubleColumns) {
        if ($columns -notcontains $doubleColumn) { throw "Runtime CSV is missing numeric field '$doubleColumn': $runtimePath" }
    }
    $rows = @(Import-Csv -LiteralPath $runtimePath)
    $measuredTicks = [int](Require-Property $configuration "measuredTicks" $resultPath)
    if ($rows.Count -ne $measuredTicks) {
        throw "Runtime CSV row count $($rows.Count) does not match measuredTicks $measuredTicks`: $runtimePath"
    }

    $ticks = New-Object System.Collections.Generic.List[object]
    $previousEnd = $null
    for ($rowIndex = 0; $rowIndex -lt $rows.Count; $rowIndex++) {
        $row = $rows[$rowIndex]
        $label = "$runtimePath row $($rowIndex + 2)"
        foreach ($counterColumn in $nonNegativeCounterColumns) {
            $counterValue = Parse-Int64 $row.$counterColumn "$label $counterColumn"
            if ($counterValue -lt 0) {
                throw "Runtime CSV counter '$counterColumn' must be non-negative: $label"
            }
        }
        foreach ($doubleColumn in $nonNegativeDoubleColumns) {
            if ([string]::IsNullOrWhiteSpace([string]$row.$doubleColumn)) {
                if ($doubleColumn -eq "candidate_orders_per_idle_citizen") { continue }
                throw "Runtime CSV numeric field '$doubleColumn' is empty: $label"
            }
            $doubleValue = Parse-Double $row.$doubleColumn "$label $doubleColumn"
            if ($doubleValue -lt 0.0) {
                throw "Runtime CSV numeric field '$doubleColumn' must be non-negative: $label"
            }
        }
        $sequence = Parse-Int64 $row.sequence "$label sequence"
        $startTick = Parse-Int64 $row.start_simulation_tick "$label start_simulation_tick"
        $endTick = Parse-Int64 $row.end_simulation_tick "$label end_simulation_tick"
        $completed = Parse-Int64 $row.completed_ticks "$label completed_ticks"
        if ($sequence -ne $rowIndex -or $row.batch_kind -ne "manual_step" -or
            $completed -ne 1 -or $endTick -ne ($startTick + 1)) {
            throw "Runtime CSV requires contiguous one-tick manual-step rows with zero-based sequences: $label"
        }
        if ($null -ne $previousEnd -and $startTick -ne $previousEnd) {
            throw "Runtime CSV simulation ticks are not contiguous: $label"
        }
        $previousEnd = $endTick

        $lookups = Parse-Int64 $row.path_plan_lookups_total "$label path_plan_lookups_total"
        $hits = Parse-Int64 $row.path_plan_cache_hits_total "$label path_plan_cache_hits_total"
        $misses = Parse-Int64 $row.path_plan_cache_misses_total "$label path_plan_cache_misses_total"
        $selectorQueries = Parse-Int64 $row.selector_exact_path_queries_total "$label selector_exact_path_queries_total"
        $selectorHits = Parse-Int64 $row.selector_path_cache_hits_total "$label selector_path_cache_hits_total"
        $selectorMisses = Parse-Int64 $row.selector_path_cache_misses_total "$label selector_path_cache_misses_total"
        if ($lookups -ne ($hits + $misses) -or $selectorQueries -ne ($selectorHits + $selectorMisses) -or
            $selectorQueries -gt $lookups -or $selectorHits -gt $hits -or $selectorMisses -gt $misses) {
            throw "Runtime CSV cache counters are internally inconsistent: $label"
        }

        $tick = [pscustomobject][ordered]@{
            sequence = $sequence
            startTick = $startTick
            endTick = $endTick
            wallMs = Parse-Double $row.wall_ms "$label wall_ms"
            simulationTickMs = Parse-Double $row.simulation_tick_ms "$label simulation_tick_ms"
            buildWorkOrdersMs = Parse-Double $row.build_work_orders_ms "$label build_work_orders_ms"
            routeSelectionMs = Parse-Double $row.route_selection_ms "$label route_selection_ms"
            navigationRebuildMs = Parse-Double $row.navigation_rebuild_ms "$label navigation_rebuild_ms"
            harvestApplyMs = Parse-Double $row.harvest_apply_ms "$label harvest_apply_ms"
            sceneSyncMs = Parse-Double $row.scene_sync_ms "$label scene_sync_ms"
            updateHudMs = Parse-Double $row.update_hud_ms "$label update_hud_ms"
            allPathLookups = $lookups
            allPathMisses = $misses
            generalPathMisses = $misses - $selectorMisses
            selectorPathMisses = $selectorMisses
            selectorExactQueries = $selectorQueries
            navigationInvalidations = Parse-Int64 $row.navigation_invalidations_total "$label navigation_invalidations_total"
        }
        foreach ($timeName in @("wallMs", "simulationTickMs", "buildWorkOrdersMs", "routeSelectionMs", "navigationRebuildMs", "harvestApplyMs", "sceneSyncMs", "updateHudMs")) {
            if ([double]$tick.$timeName -lt 0.0) { throw "Runtime CSV contains a negative timing '$timeName': $label" }
        }
        $ticks.Add($tick)
    }

    $cacheMode = [string](Require-Property $configuration "cacheMode" $resultPath)
    $selectorMode = [string](Require-Property $configuration "selectorMode" $resultPath)
    $extractionPlanningMode = [string](Require-Property $configuration "extractionPlanningMode" $resultPath)
    $runId = Get-DisplayPath $runDirectory
    $bucketRecords = New-Object System.Collections.Generic.List[object]
    for ($bucketIndex = 0; $bucketIndex -le $ThresholdMilliseconds.Count; $bucketIndex++) {
        $lower = if ($bucketIndex -eq 0) { $null } else { [double]$ThresholdMilliseconds[$bucketIndex - 1] }
        $upper = if ($bucketIndex -eq $ThresholdMilliseconds.Count) { $null } else { [double]$ThresholdMilliseconds[$bucketIndex] }
        $matches = @($ticks | Where-Object {
            ($null -eq $lower -or $_.wallMs -gt $lower) -and ($null -eq $upper -or $_.wallMs -le $upper)
        })
        $bucketRecords.Add([ordered]@{
            lowerExclusiveMilliseconds = $lower
            upperInclusiveMilliseconds = $upper
            count = $matches.Count
            endSimulationTicks = @($matches | ForEach-Object { $_.endTick })
        })
    }

    $spikeTicks = @($ticks | Where-Object { $_.wallMs -gt $ThresholdMilliseconds[0] })
    $spikes = New-Object System.Collections.Generic.List[object]
    $attributionCounts = [ordered]@{
        initial_cold_population = 0
        immediately_post_invalidation = 0
        forced_transition = 0
        invalidation_tick = 0
        steady_state = 0
    }
    $dominantCounts = [ordered]@{}
    foreach ($tick in $spikeTicks) {
        $isInitialCold = $cacheMode -eq "cold" -and $tick.sequence -eq 0
        $priorInvalidations = if ($tick.sequence -gt 0) {
            [long]$ticks[[int]$tick.sequence - 1].navigationInvalidations
        }
        else { [long]0 }
        $isPostInvalidation = $priorInvalidations -gt 0
        $isForcedTransition = $cacheMode -eq "forced_invalidation" -and $tick.navigationInvalidations -gt 0
        $attribution = if ($isForcedTransition) {
            "forced_transition"
        }
        elseif ($isPostInvalidation) { "immediately_post_invalidation" }
        elseif ($isInitialCold) { "initial_cold_population" }
        elseif ($tick.navigationInvalidations -gt 0) { "invalidation_tick" }
        else { "steady_state" }
        $attributionCounts[$attribution]++
        $dominant = Get-DominantPhase $tick
        if (-not $dominantCounts.Contains($dominant.phase)) { $dominantCounts[$dominant.phase] = 0 }
        $dominantCounts[$dominant.phase]++
        $spikes.Add([ordered]@{
            endSimulationTick = $tick.endTick
            wallMilliseconds = Round-Value $tick.wallMs
            simulationTickMilliseconds = Round-Value $tick.simulationTickMs
            attribution = $attribution
            priorTickNavigationInvalidations = $priorInvalidations
            currentTickNavigationInvalidations = $tick.navigationInvalidations
            allPathCacheMisses = $tick.allPathMisses
            generalPathCacheMisses = $tick.generalPathMisses
            selectorPathCacheMisses = $tick.selectorPathMisses
            dominantPhase = $dominant
        })
    }

    $correlations = @(
        Get-PearsonCorrelation $ticks.ToArray() "buildWorkOrdersMs"
        Get-PearsonCorrelation $ticks.ToArray() "routeSelectionMs"
        Get-PearsonCorrelation $ticks.ToArray() "navigationRebuildMs"
        Get-PearsonCorrelation $ticks.ToArray() "allPathLookups"
        Get-PearsonCorrelation $ticks.ToArray() "allPathMisses"
        Get-PearsonCorrelation $ticks.ToArray() "generalPathMisses"
        Get-PearsonCorrelation $ticks.ToArray() "selectorPathMisses"
        Get-PearsonCorrelation $ticks.ToArray() "selectorExactQueries"
    )

    $forcedScope = $null
    if ($cacheMode -eq "forced_invalidation") {
        $cacheEvidence = Require-Property $result "cacheEvidence" $resultPath
        $forcedEvidence = Require-Property $cacheEvidence "forcedInvalidation" $resultPath
        $probe = Require-Property $forcedEvidence "probe" $resultPath
        foreach ($property in @("committed", "completionTick", "versionBeforeCommit", "versionAfterCommit",
            "cacheEntriesBeforeRebuild", "cacheEntriesImmediatelyAfterRebuild", "firstPostChangeLookupObserved",
            "firstPostChangeLookupWasCacheMiss", "firstPostChangeLookupUsedNewVersion", "exactEndpointsMatch",
            "changedCellIncludedInPostChangePlan", "preChangeQueryVersion", "preChangePlanVersion",
            "postChangeQueryVersion", "postChangePlanVersion", "commitToFirstLookupMilliseconds")) {
            [void](Require-Property $probe $property $resultPath)
        }
        if ($probe.committed -ne $true -or $probe.firstPostChangeLookupObserved -ne $true -or
            $probe.firstPostChangeLookupWasCacheMiss -ne $true -or $probe.firstPostChangeLookupUsedNewVersion -ne $true -or
            $probe.exactEndpointsMatch -ne $true -or $probe.changedCellIncludedInPostChangePlan -ne $true -or
            [long]$probe.cacheEntriesImmediatelyAfterRebuild -ne 0 -or
            [long]$probe.versionAfterCommit -ne ([long]$probe.versionBeforeCommit + 1) -or
            [long]$probe.preChangeQueryVersion -ne [long]$probe.versionBeforeCommit -or
            [long]$probe.preChangePlanVersion -ne [long]$probe.versionBeforeCommit -or
            [long]$probe.postChangeQueryVersion -ne [long]$probe.versionAfterCommit -or
            [long]$probe.postChangePlanVersion -ne [long]$probe.versionAfterCommit) {
            throw "Forced-invalidation evidence contract is not satisfied: $resultPath"
        }
        $transitionRows = @($ticks | Where-Object { $_.endTick -eq [long]$probe.completionTick })
        if ($transitionRows.Count -ne 1 -or $transitionRows[0].navigationInvalidations -lt 1) {
            throw "Forced-invalidation completion tick does not map to exactly one invalidating runtime row: $resultPath"
        }
        $transition = $transitionRows[0]
        $scopeAssessment = if ($transition.buildWorkOrdersMs -gt $transition.navigationRebuildMs) {
            "diagnostic_tick_dominated_by_general_work_order_build"
        }
        else { "diagnostic_tick_dominated_by_navigation_rebuild" }
        $forcedScope = [ordered]@{
            completionTick = [long]$probe.completionTick
            navigationVersionBefore = [long]$probe.versionBeforeCommit
            navigationVersionAfter = [long]$probe.versionAfterCommit
            cacheEntriesBeforeRebuild = [long]$probe.cacheEntriesBeforeRebuild
            cacheEntriesImmediatelyAfterRebuild = [long]$probe.cacheEntriesImmediatelyAfterRebuild
            firstPostChangeLookupWasExactNewVersionMiss = $true
            diagnosticTickWallMilliseconds = Round-Value $transition.wallMs
            commitToFirstLookupMilliseconds = Round-Value ([double]$probe.commitToFirstLookupMilliseconds)
            navigationRebuildMilliseconds = Round-Value $transition.navigationRebuildMs
            navigationRebuildWallSharePercent = Get-Percent $transition.navigationRebuildMs $transition.wallMs
            buildWorkOrdersMilliseconds = Round-Value $transition.buildWorkOrdersMs
            buildWorkOrdersWallSharePercent = Get-Percent $transition.buildWorkOrdersMs $transition.wallMs
            scopeAssessment = $scopeAssessment
        }
    }

    $runRecord = [ordered]@{
        run = $runId
        sourceArtifacts = [ordered]@{
            runtimeMetricsCsv = [ordered]@{
                path = Get-DisplayPath $runtimePath
                sha256 = Get-Sha256 $runtimePath
            }
            performanceResult = [ordered]@{
                path = Get-DisplayPath $resultPath
                sha256 = Get-Sha256 $resultPath
            }
            equivalenceResult = [ordered]@{
                path = Get-DisplayPath $equivalencePath
                sha256 = Get-Sha256 $equivalencePath
            }
        }
        configuration = [ordered]@{
            scenarioId = [string](Require-Property $configuration "scenarioId" $resultPath)
            seed = [long](Require-Property $configuration "simulationSeed" $resultPath)
            citizens = [int](Require-Property $configuration "citizenCount" $resultPath)
            warmupTicks = [int](Require-Property $configuration "warmupTicks" $resultPath)
            measuredTicks = $measuredTicks
            cacheMode = $cacheMode
            selectorMode = $selectorMode
            extractionPlanningMode = $extractionPlanningMode
            trialIndex = [int](Require-Property $configuration "trialIndex" $resultPath)
        }
        thresholdBuckets = $bucketRecords.ToArray()
        spikesAboveFirstThreshold = [ordered]@{
            thresholdMilliseconds = [double]$ThresholdMilliseconds[0]
            count = $spikeTicks.Count
            attributionCounts = $attributionCounts
            dominantPhaseCounts = $dominantCounts
            ticks = $spikes.ToArray()
        }
        cacheMisses = [ordered]@{
            all = [long](($ticks | Measure-Object allPathMisses -Sum).Sum)
            generalNonSelector = [long](($ticks | Measure-Object generalPathMisses -Sum).Sum)
            selector = [long](($ticks | Measure-Object selectorPathMisses -Sum).Sum)
        }
        correlationsWithWallMilliseconds = $correlations
        forcedTransitionScope = $forcedScope
    }
    $runs.Add($runRecord)
    $repeatKey = @(
        $runRecord.configuration.scenarioId,
        $runRecord.configuration.seed,
        $runRecord.configuration.citizens,
        $runRecord.configuration.warmupTicks,
        $runRecord.configuration.measuredTicks,
        $runRecord.configuration.cacheMode,
        $runRecord.configuration.selectorMode,
        $runRecord.configuration.extractionPlanningMode
    ) -join '|'
    $internalRuns.Add([pscustomobject]@{
        run = $runId
        repeatKey = $repeatKey
        spikeTicks = @($spikeTicks | ForEach-Object { [long]$_.endTick } | Sort-Object)
        ticks = $ticks.ToArray()
    })
}

$repeatability = New-Object System.Collections.Generic.List[object]
foreach ($group in @($internalRuns | Group-Object repeatKey | Where-Object { $_.Count -gt 1 } | Sort-Object Name)) {
    $members = @($group.Group | Sort-Object run)
    $pairs = New-Object System.Collections.Generic.List[object]
    for ($leftIndex = 0; $leftIndex -lt $members.Count; $leftIndex++) {
        for ($rightIndex = $leftIndex + 1; $rightIndex -lt $members.Count; $rightIndex++) {
            $left = @($members[$leftIndex].spikeTicks)
            $right = @($members[$rightIndex].spikeTicks)
            $leftSet = New-Object 'System.Collections.Generic.HashSet[long]'
            $rightSet = New-Object 'System.Collections.Generic.HashSet[long]'
            foreach ($value in $left) { [void]$leftSet.Add($value) }
            foreach ($value in $right) { [void]$rightSet.Add($value) }
            $intersection = @($left | Where-Object { $rightSet.Contains($_) })
            $leftOnly = @($left | Where-Object { -not $rightSet.Contains($_) })
            $rightOnly = @($right | Where-Object { -not $leftSet.Contains($_) })
            $unionCount = $leftSet.Count + $rightSet.Count - $intersection.Count
            $pairs.Add([ordered]@{
                leftRun = $members[$leftIndex].run
                rightRun = $members[$rightIndex].run
                exactTickSetIdentity = $leftOnly.Count -eq 0 -and $rightOnly.Count -eq 0
                intersectionCount = $intersection.Count
                unionCount = $unionCount
                jaccard = if ($unionCount -eq 0) { 1.0 } else { Round-Value ($intersection.Count / [double]$unionCount) }
                leftOnlyTicks = $leftOnly
                rightOnlyTicks = $rightOnly
            })
        }
    }
    $repeatability.Add([ordered]@{
        compatibilityKey = $group.Name
        spikeThresholdMilliseconds = [double]$ThresholdMilliseconds[0]
        runCount = $members.Count
        allRunsExactTickSetIdentity = @($pairs | Where-Object { -not $_.exactTickSetIdentity }).Count -eq 0
        pairs = $pairs.ToArray()
    })
}

$allTicks = @($internalRuns | ForEach-Object { $_.ticks })
$aggregateCorrelations = @(
    Get-PearsonCorrelation $allTicks "buildWorkOrdersMs"
    Get-PearsonCorrelation $allTicks "routeSelectionMs"
    Get-PearsonCorrelation $allTicks "navigationRebuildMs"
    Get-PearsonCorrelation $allTicks "allPathLookups"
    Get-PearsonCorrelation $allTicks "allPathMisses"
    Get-PearsonCorrelation $allTicks "generalPathMisses"
    Get-PearsonCorrelation $allTicks "selectorPathMisses"
    Get-PearsonCorrelation $allTicks "selectorExactQueries"
)

$output = [ordered]@{
    schemaVersion = 1
    analysis = "performance_spike_characterization"
    diagnosticScope = [ordered]@{
        timingSource = "metrics_on_runtime_batches"
        claimBoundary = "diagnostic_characterization_only_not_a_release_gate"
        sourceContract = "clean_verified_export_release_equivalence_pairs"
        repeatabilityScope = "tick_set_identity_only_for_configuration-compatible_supplied_runs"
    }
    runtimeMetricsSchemaVersion = 4
    thresholdMilliseconds = @($ThresholdMilliseconds)
    compatibility = $compatibility
    runCount = $runs.Count
    runs = $runs.ToArray()
    repeatedSpikeTickSetIdentity = $repeatability.ToArray()
    aggregateCorrelationsWithWallMilliseconds = $aggregateCorrelations
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
Write-Utf8NoBom $resolvedOutput ($output | ConvertTo-Json -Depth 20)
Write-Output $resolvedOutput
