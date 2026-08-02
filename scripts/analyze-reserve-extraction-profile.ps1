[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [double]$SpikeThresholdMilliseconds = 50.0,
    [switch]$AllowDirtySource
)

$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$analyzerVersion = "1.1.0"
$negativeResidualToleranceMilliseconds = 0.001
$parentWallToleranceMilliseconds = 0.001
$maximumJsonBytes = 2MB
$maximumCsvBytes = 4MB
$maximumCsvLines = 301
$maximumCsvLineCharacters = 16384
$maximumJsonNestingDepth = 32
$runtimeFileNames = @("runtime-batch-metrics-v6.csv")
$requiredColumns = @(
    "sequence", "batch_kind", "start_simulation_tick", "end_simulation_tick", "completed_ticks",
    "wall_ms", "build_work_orders_ms", "build_work_orders_reserve_extraction_ms",
    "reserve_extraction_class_preparation_ms",
    "reserve_extraction_candidate_enumeration_and_bound_selection_ms",
    "reserve_extraction_active_frontier_and_claim_evaluation_ms",
    "reserve_extraction_retained_materialization_ms"
)
$profileMetrics = [ordered]@{
    class_preparation = "classPreparationMilliseconds"
    candidate_enumeration_and_bound_selection = "candidateEnumerationAndBoundSelectionMilliseconds"
    active_frontier_and_claim_evaluation = "activeFrontierAndClaimEvaluationMilliseconds"
    retained_materialization = "retainedMaterializationMilliseconds"
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

function Read-Json {
    param([string]$Path, [string]$Label)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Label is missing: $Path"
    }
    Assert-JsonBounds $Path $Label
    try {
        return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    }
    catch {
        throw "$Label is not valid JSON: $Path. $($_.Exception.Message)"
    }
}

function Assert-JsonBounds {
    param([string]$Path, [string]$Label)
    $item = Get-Item -LiteralPath $Path
    if ($item.Length -gt $maximumJsonBytes) {
        throw "$Label exceeds the $maximumJsonBytes-byte JSON limit: $Path"
    }
    $reader = New-Object System.IO.StreamReader($item.FullName, $true)
    try {
        $depth = 0
        $inString = $false
        $escaped = $false
        while (($value = $reader.Read()) -ne -1) {
            $character = [char]$value
            if ($inString) {
                if ($escaped) { $escaped = $false; continue }
                if ($character -eq '\') { $escaped = $true; continue }
                if ($character -eq '"') { $inString = $false }
                continue
            }
            if ($character -eq '"') { $inString = $true; continue }
            if ($character -eq '{' -or $character -eq '[') {
                $depth++
                if ($depth -gt $maximumJsonNestingDepth) {
                    throw "$Label exceeds the maximum JSON nesting depth $maximumJsonNestingDepth`: $Path"
                }
            }
            elseif ($character -eq '}' -or $character -eq ']') {
                $depth--
                if ($depth -lt 0) { throw "$Label has invalid JSON nesting: $Path" }
            }
        }
        if ($inString -or $depth -ne 0) { throw "$Label has invalid JSON nesting: $Path" }
    }
    finally { $reader.Dispose() }
}

function Assert-CsvBounds {
    param([string]$Path)
    $item = Get-Item -LiteralPath $Path
    if ($item.Length -gt $maximumCsvBytes) {
        throw "Runtime CSV exceeds the $maximumCsvBytes-byte limit: $Path"
    }
    $reader = New-Object System.IO.StreamReader($item.FullName, $true)
    try {
        $lineCount = 0
        while (($line = $reader.ReadLine()) -ne $null) {
            $lineCount++
            if ($lineCount -gt $maximumCsvLines) {
                throw "Runtime CSV exceeds the $maximumCsvLines-line limit: $Path"
            }
            if ($line.Length -gt $maximumCsvLineCharacters) {
                throw "Runtime CSV line $lineCount exceeds the $maximumCsvLineCharacters-character limit: $Path"
            }
        }
    }
    finally { $reader.Dispose() }
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

function Get-Median {
    param([double[]]$Values)
    if ($null -eq $Values -or $Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2.0)
    if (($sorted.Count % 2) -eq 1) { return [double]$sorted[$middle] }
    return ([double]$sorted[$middle - 1] + [double]$sorted[$middle]) / 2.0
}

function Get-Statistics {
    param([double[]]$Values)
    if ($null -eq $Values -or $Values.Count -eq 0) {
        return [ordered]@{
            count = 0
            totalMilliseconds = 0.0
            medianMilliseconds = $null
            meanMilliseconds = $null
            minimumMilliseconds = $null
            maximumMilliseconds = $null
        }
    }
    $measurement = $Values | Measure-Object -Sum -Average -Minimum -Maximum
    return [ordered]@{
        count = $Values.Count
        totalMilliseconds = Round-Value ([double]$measurement.Sum)
        medianMilliseconds = Round-Value (Get-Median $Values)
        meanMilliseconds = Round-Value ([double]$measurement.Average)
        minimumMilliseconds = Round-Value ([double]$measurement.Minimum)
        maximumMilliseconds = Round-Value ([double]$measurement.Maximum)
    }
}

function Get-Variance {
    param([double[]]$Values)
    if ($null -eq $Values -or $Values.Count -eq 0) {
        return [ordered]@{ count = 0; mean = $null; standardDeviation = $null; coefficientOfVariationPercent = $null; range = $null }
    }
    $mean = [double](($Values | Measure-Object -Average).Average)
    $sumSquares = 0.0
    foreach ($value in $Values) { $sumSquares += ([double]$value - $mean) * ([double]$value - $mean) }
    $standardDeviation = [Math]::Sqrt($sumSquares / $Values.Count)
    $minimum = [double](($Values | Measure-Object -Minimum).Minimum)
    $maximum = [double](($Values | Measure-Object -Maximum).Maximum)
    return [ordered]@{
        count = $Values.Count
        mean = Round-Value $mean
        standardDeviation = Round-Value $standardDeviation
        coefficientOfVariationPercent = if ($mean -eq 0.0) { $null } else { Round-Value (100.0 * $standardDeviation / $mean) }
        range = Round-Value ($maximum - $minimum)
    }
}

function Get-Sha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-Sha256 {
    param([string]$Value, [string]$Label)
    if ($Value -cnotmatch '^[0-9a-f]{64}$') { throw "$Label must be a lowercase SHA-256 value." }
}

function Get-NormalizedPath {
    param([string]$Path)
    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Assert-SamePath {
    param([string]$DeclaredPath, [string]$ActualPath, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($DeclaredPath) -or
        -not (Get-NormalizedPath $DeclaredPath).Equals((Get-NormalizedPath $ActualPath), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label does not bind to the analyzed artifact: $ActualPath"
    }
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

function Get-ArtifactRecord {
    param([string]$Path)
    $item = Get-Item -LiteralPath $Path
    if ($item.PSIsContainer -or $item.Length -le 0) { throw "Bound artifact must be a non-empty file: $Path" }
    $sha256 = Get-Sha256 $item.FullName
    Assert-Sha256 $sha256 "Bound artifact hash for $Path"
    return [ordered]@{
        path = Get-DisplayPath $item.FullName
        sizeBytes = [long]$item.Length
        sha256 = $sha256
    }
}

function Get-ProfileSummary {
    param([object[]]$Ticks)
    $metrics = [ordered]@{}
    $parentValues = [double[]]@($Ticks | ForEach-Object { [double]$_.parentMilliseconds })
    $childTotalValues = [double[]]@($Ticks | ForEach-Object { [double]$_.childTotalMilliseconds })
    $residualValues = [double[]]@($Ticks | ForEach-Object { [double]$_.residualMilliseconds })
    $metrics.parent = Get-Statistics $parentValues
    foreach ($entry in $profileMetrics.GetEnumerator()) {
        $property = [string]$entry.Value
        $values = [double[]]@($Ticks | ForEach-Object { [double]$_.PSObject.Properties[$property].Value })
        $statistics = Get-Statistics $values
        $statistics.parentSharePercent = if ($metrics.parent.totalMilliseconds -le 0.0) {
            0.0
        }
        else {
            Round-Value (100.0 * [double]$statistics.totalMilliseconds / [double]$metrics.parent.totalMilliseconds)
        }
        $metrics[[string]$entry.Key] = $statistics
    }
    $metrics.children_total = Get-Statistics $childTotalValues
    $metrics.residual = Get-Statistics $residualValues

    $maximumTotal = [double](($profileMetrics.GetEnumerator() | ForEach-Object {
        [double]$metrics[[string]$_.Key].totalMilliseconds
    } | Measure-Object -Maximum).Maximum)
    $maximumCategories = @($profileMetrics.GetEnumerator() | Where-Object {
        [double]$metrics[[string]$_.Key].totalMilliseconds -eq $maximumTotal
    } | ForEach-Object { [string]$_.Key })
    $hasUniquePositiveMaximum = $maximumTotal -gt 0.0 -and $maximumCategories.Count -eq 1
    return [ordered]@{
        sampleCount = $Ticks.Count
        metrics = $metrics
        reconciliation = [ordered]@{
            parentTotalMilliseconds = $metrics.parent.totalMilliseconds
            childTotalMilliseconds = $metrics.children_total.totalMilliseconds
            residualTotalMilliseconds = $metrics.residual.totalMilliseconds
            exactRoundedIdentity = (Round-Value ([double]$metrics.children_total.totalMilliseconds + [double]$metrics.residual.totalMilliseconds)) -eq [double]$metrics.parent.totalMilliseconds
        }
        dominantExercisedSubCost = [ordered]@{
            category = if ($hasUniquePositiveMaximum) { $maximumCategories[0] } else { $null }
            totalMilliseconds = Round-Value ([Math]::Max(0.0, $maximumTotal))
            parentSharePercent = if ($metrics.parent.totalMilliseconds -le 0.0) { 0.0 } else { Round-Value (100.0 * $maximumTotal / [double]$metrics.parent.totalMilliseconds) }
            uniquePositiveMaximum = $hasUniquePositiveMaximum
            maximumTiedCategories = if ($maximumCategories.Count -gt 1) { $maximumCategories } else { @() }
        }
    }
}

if ([double]::IsNaN($SpikeThresholdMilliseconds) -or
    [double]::IsInfinity($SpikeThresholdMilliseconds) -or
    $SpikeThresholdMilliseconds -le 0.0) {
    throw "SpikeThresholdMilliseconds must be finite and positive."
}
if ($InputPath.Count -ne 3) { throw "Reserve-extraction profile analysis requires exactly 3 input paths." }

$runtimePaths = New-Object System.Collections.Generic.List[string]
foreach ($input in $InputPath) {
    if (-not (Test-Path -LiteralPath $input)) { throw "InputPath does not exist: $input" }
    $item = Get-Item -LiteralPath $input
    if (-not $item.PSIsContainer) {
        if ($item.Name -notin $runtimeFileNames) { throw "Input files must use a supported runtime metrics schema filename: $($item.FullName)" }
        $runtimePaths.Add($item.FullName)
        continue
    }
    $direct = @($runtimeFileNames |
        ForEach-Object { Join-Path $item.FullName $_ } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    if ($direct.Count -eq 1) {
        $runtimePaths.Add((Get-Item -LiteralPath $direct[0]).FullName)
        continue
    }
    if ($direct.Count -gt 1) { throw "Input directory contains multiple runtime metrics schema artifacts: $($item.FullName)" }
    throw "No direct supported runtime metrics artifact was found under: $($item.FullName)"
}
$runtimePaths = @($runtimePaths | Sort-Object -Unique)
if ($runtimePaths.Count -ne 3) { throw "Reserve-extraction profile analysis requires exactly 3 distinct runtime metrics artifacts." }

$internalRuns = New-Object System.Collections.Generic.List[object]
$runOutputs = New-Object System.Collections.Generic.List[object]
$compatibility = $null
$compatibilityKey = $null
$sourceGitSha = $null
$sourceDirty = $null
$trialIndexes = New-Object 'System.Collections.Generic.HashSet[int]'

foreach ($runtimePath in $runtimePaths) {
    $runtimeMetricsSchemaVersion = 6
    $runtimeFileName = Split-Path -Leaf $runtimePath
    $runDirectory = Split-Path -Parent $runtimePath
    $pairDirectory = Split-Path -Parent $runDirectory
    $resultPath = Join-Path $runDirectory "perf-results.json"
    $equivalencePath = Join-Path $pairDirectory "equivalence-results.json"
    $result = Read-Json $resultPath "Performance result"
    $equivalence = Read-Json $equivalencePath "Equivalence result"
    $configuration = Require-Property $result "configuration" $resultPath
    $environment = Require-Property $result "environment" $resultPath
    $artifacts = Require-Property $result "artifacts" $resultPath

    Assert-SamePath ([string](Require-Property $configuration "outputDirectory" $resultPath)) $runDirectory "Performance result outputDirectory"
    Assert-SamePath ([string](Require-Property $artifacts "runtimeMetricsCsv" $resultPath)) $runtimePath "Performance result runtimeMetricsCsv"
    Assert-SamePath ([string](Require-Property $artifacts "performanceResults" $resultPath)) $resultPath "Performance result performanceResults"
    Assert-SamePath ([string](Require-Property $equivalence "metricsOnResult" $equivalencePath)) $resultPath "Equivalence metricsOnResult"

    if ([int](Require-Property $result "schemaVersion" $resultPath) -ne 6 -or
        [int](Require-Property $equivalence "schemaVersion" $equivalencePath) -ne 6) {
        throw "Reserve-extraction profile evidence requires performance and equivalence schemaVersion 6: $runDirectory"
    }
    if ((Require-Property $configuration "metricsEnabled" $resultPath) -ne $true) {
        throw "The analyzed result is not metrics-on: $resultPath"
    }
    $gitDirty = [bool](Require-Property $configuration "gitDirty" $resultPath)
    if ($gitDirty -and -not $AllowDirtySource) {
        throw "Dirty-source profiling evidence requires -AllowDirtySource: $resultPath"
    }
    if ([bool](Require-Property $equivalence "sourceClean" $equivalencePath) -eq $gitDirty) {
        throw "Equivalence source cleanliness disagrees with the metrics-on result: $equivalencePath"
    }
    $expectedStatus = if ($gitDirty) { "pass_dirty_source" } else { "pass" }
    if ([string](Require-Property $equivalence "status" $equivalencePath) -ne $expectedStatus -or
        [string](Require-Property $equivalence "contractStatus" $equivalencePath) -ne $expectedStatus) {
        throw "Equivalence status must be '$expectedStatus': $equivalencePath"
    }
    foreach ($contractProperty in @(
        "releaseEnvironmentValid", "resultSchemaValid", "configurationMatches", "commandConfigurationMatches",
        "modeContractValid", "executionRouteValid", "gitIdentityMatches", "environmentMatches", "godotVersionValid",
        "hashesValid", "snapshotHashMatches", "eventLogHashMatches", "combinedHashMatches", "resultStatusesValid",
        "artifactContractValid", "processExecutableMatches", "tickBoundsMatch", "matrixSchemaValid",
        "metricsOffRuntimeMetricsAbsent", "metricsOnRuntimeMetricsValid")) {
        if ((Require-Property $equivalence $contractProperty $equivalencePath) -ne $true) {
            throw "Equivalence contract '$contractProperty' is not satisfied: $equivalencePath"
        }
    }
    $offHash = [string](Require-Property $equivalence "metricsOffHash" $equivalencePath)
    $onHash = [string](Require-Property $equivalence "metricsOnHash" $equivalencePath)
    Assert-Sha256 $offHash "Equivalence metricsOffHash"
    Assert-Sha256 $onHash "Equivalence metricsOnHash"
    if ($offHash -ne $onHash) {
        throw "Metrics-off and metrics-on deterministic hashes must be present and equal: $equivalencePath"
    }
    $resultHashes = Require-Property $result "hashes" $resultPath
    $resultDeterministicHash = [string](Require-Property $resultHashes "deterministicStateAndEventSha256" $resultPath)
    Assert-Sha256 ([string](Require-Property $resultHashes "snapshotSha256" $resultPath)) "Performance result snapshotSha256"
    Assert-Sha256 ([string](Require-Property $resultHashes "eventLogSha256" $resultPath)) "Performance result eventLogSha256"
    Assert-Sha256 $resultDeterministicHash "Performance result deterministicStateAndEventSha256"
    if ($resultDeterministicHash -ne $onHash) {
        throw "Equivalence deterministic hash does not match the performance result: $resultPath"
    }
    if ((Require-Property $environment "verifiedReleaseExecution" $resultPath) -ne $true -or
        [string](Require-Property $environment "managedAssemblyConfiguration" $resultPath) -ne "ExportRelease") {
        throw "BuildWorkOrders profiling requires verified ExportRelease execution: $resultPath"
    }
    if ([string](Require-Property $configuration "scenarioId" $resultPath) -ne "balanced_basin" -or
        [int](Require-Property $configuration "simulationSeed" $resultPath) -ne 1337 -or
        [int](Require-Property $configuration "citizenCount" $resultPath) -ne 16 -or
        [int](Require-Property $configuration "warmupTicks" $resultPath) -ne 2 -or
        [int](Require-Property $configuration "measuredTicks" $resultPath) -ne 300 -or
        [string](Require-Property $configuration "cacheMode" $resultPath) -ne "cold" -or
        [string](Require-Property $configuration "selectorMode" $resultPath) -ne "exact_branch_and_bound" -or
        [string](Require-Property $configuration "extractionPlanningMode" $resultPath) -ne "exact_bounded" -or
        [string](Require-Property $configuration "routeDistanceMode" $resultPath) -ne "cached_distance_only") {
        throw "Run does not match the canonical 16-citizen reference-path bounds: $resultPath"
    }
    $trialIndex = [int](Require-Property $configuration "trialIndex" $resultPath)
    if (-not $trialIndexes.Add($trialIndex)) { throw "Duplicate trialIndex $trialIndex was supplied." }
    if ($trialIndex -notin @(1, 2, 3)) { throw "Trial indexes must be exactly 1, 2, and 3; found $trialIndex." }

    $releaseExport = [bool](Require-Property $equivalence "releaseExport" $equivalencePath)
    $reusedReleaseRunner = [bool](Require-Property $equivalence "reusedReleaseRunner" $equivalencePath)
    $expectedFresh = $trialIndex -eq 1
    if ($releaseExport -ne $expectedFresh -or $reusedReleaseRunner -eq $expectedFresh) {
        throw "Trial 1 must own the fresh ExportRelease runner and trials 2/3 must reuse it: $equivalencePath"
    }
    if ([string](Require-Property $configuration "executionRoute" $resultPath) -ne "export_release" -or
        [string](Require-Property $equivalence "executionRoute" $equivalencePath) -ne "export_release") {
        throw "All profile trials must use the verified export_release execution route: $resultPath"
    }

    $currentCompatibility = [ordered]@{
        gitSha = [string](Require-Property $configuration "gitSha" $resultPath)
        gitDirty = $gitDirty
        executionRoute = [string](Require-Property $configuration "executionRoute" $resultPath)
        machineName = [string](Require-Property $environment "machineName" $resultPath)
        logicalProcessorCount = [int](Require-Property $environment "logicalProcessorCount" $resultPath)
        operatingSystem = [string](Require-Property $environment "operatingSystem" $resultPath)
        processArchitecture = [string](Require-Property $environment "processArchitecture" $resultPath)
        dotnetRuntime = [string](Require-Property $environment "dotnetRuntime" $resultPath)
        godotVersion = [string](Require-Property $environment "godotVersion" $resultPath)
        managedAssemblyConfiguration = [string](Require-Property $environment "managedAssemblyConfiguration" $resultPath)
        processExecutablePath = [string](Require-Property $environment "processExecutablePath" $resultPath)
        runnerExecutablePath = [string](Require-Property $configuration "runnerExecutablePath" $resultPath)
        runtimeMetricsSchemaVersion = $runtimeMetricsSchemaVersion
    }
    Assert-SamePath ([string](Require-Property $equivalence "runnerExecutable" $equivalencePath)) $currentCompatibility.runnerExecutablePath "Equivalence runnerExecutable"
    Assert-SamePath ([string](Require-Property $equivalence "exportOutputExecutable" $equivalencePath)) $currentCompatibility.processExecutablePath "Equivalence exportOutputExecutable"
    $currentKey = $currentCompatibility | ConvertTo-Json -Compress
    if ($null -eq $compatibilityKey) {
        $compatibilityKey = $currentKey
        $compatibility = $currentCompatibility
        $sourceGitSha = $currentCompatibility.gitSha
        $sourceDirty = $gitDirty
    }
    elseif ($currentKey -ne $compatibilityKey) {
        throw "Input evidence is not from the same source, host, runtime, and Release runner: $resultPath"
    }

    $offDirectory = Join-Path $pairDirectory "metrics-off"
    if (Test-Path -LiteralPath (Join-Path $offDirectory $runtimeFileName) -PathType Leaf) {
        throw "Metrics-off unexpectedly emitted $runtimeFileName`: $offDirectory"
    }

    Assert-CsvBounds $runtimePath
    $header = Get-Content -LiteralPath $runtimePath -TotalCount 1
    if ([string]::IsNullOrWhiteSpace($header)) { throw "Runtime CSV is empty: $runtimePath" }
    $columns = @($header.Split(','))
    $expectedColumnCount = 44
    if ($columns.Count -ne $expectedColumnCount) { throw "Runtime CSV schema v$runtimeMetricsSchemaVersion must contain exactly $expectedColumnCount columns: $runtimePath" }
    foreach ($required in $requiredColumns) {
        if ($columns -notcontains $required) { throw "Runtime CSV is missing '$required': $runtimePath" }
    }
    $rows = @(Import-Csv -LiteralPath $runtimePath)
    if ($rows.Count -ne 300) { throw "Runtime CSV must contain exactly 300 measured rows: $runtimePath" }
    if ([long](Require-Property $result "measuredStartSimulationTick" $resultPath) -ne 2 -or
        [long](Require-Property $result "finalSimulationTick" $resultPath) -ne 302) {
        throw "Performance result must bind the canonical measured tick range 2..302: $resultPath"
    }

    $ticks = New-Object System.Collections.Generic.List[object]
    $previousEndTick = $null
    for ($rowIndex = 0; $rowIndex -lt $rows.Count; $rowIndex++) {
        $row = $rows[$rowIndex]
        $label = "$runtimePath row $($rowIndex + 2)"
        $sequence = Parse-Int64 $row.sequence "$label sequence"
        $startTick = Parse-Int64 $row.start_simulation_tick "$label start_simulation_tick"
        $endTick = Parse-Int64 $row.end_simulation_tick "$label end_simulation_tick"
        $completedTicks = Parse-Int64 $row.completed_ticks "$label completed_ticks"
        if ($sequence -ne $rowIndex -or $row.batch_kind -ne "manual_step" -or
            $startTick -ne ($rowIndex + 2) -or $endTick -ne ($rowIndex + 3) -or
            $completedTicks -ne 1 -or $endTick -ne ($startTick + 1)) {
            throw "Runtime CSV rows must be zero-based, one-tick manual steps: $label"
        }
        if ($null -ne $previousEndTick -and $startTick -ne $previousEndTick) {
            throw "Runtime CSV simulation ticks are not contiguous: $label"
        }
        $previousEndTick = $endTick

        $wall = Parse-Double $row.wall_ms "$label wall_ms"
        $buildWorkOrdersParent = Parse-Double $row.build_work_orders_ms "$label build_work_orders_ms"
        $parent = Parse-Double $row.build_work_orders_reserve_extraction_ms "$label build_work_orders_reserve_extraction_ms"
        $classPreparation = Parse-Double $row.reserve_extraction_class_preparation_ms "$label reserve_extraction_class_preparation_ms"
        $candidateEnumerationAndBoundSelection = Parse-Double $row.reserve_extraction_candidate_enumeration_and_bound_selection_ms "$label reserve_extraction_candidate_enumeration_and_bound_selection_ms"
        $activeFrontierAndClaimEvaluation = Parse-Double $row.reserve_extraction_active_frontier_and_claim_evaluation_ms "$label reserve_extraction_active_frontier_and_claim_evaluation_ms"
        $retainedMaterialization = Parse-Double $row.reserve_extraction_retained_materialization_ms "$label reserve_extraction_retained_materialization_ms"
        foreach ($timing in @($wall, $buildWorkOrdersParent, $parent, $classPreparation, $candidateEnumerationAndBoundSelection, $activeFrontierAndClaimEvaluation, $retainedMaterialization)) {
            if ($timing -lt 0.0) { throw "Runtime CSV timing fields must be non-negative: $label" }
        }
        if ($buildWorkOrdersParent -gt ($wall + $parentWallToleranceMilliseconds)) {
            throw "BuildWorkOrders parent exceeds wall_ms beyond tolerance: $label"
        }
        if ($parent -gt ($buildWorkOrdersParent + $parentWallToleranceMilliseconds)) {
            throw "Reserve-extraction parent exceeds BuildWorkOrders beyond tolerance: $label"
        }
        $childTotal = $classPreparation + $candidateEnumerationAndBoundSelection + $activeFrontierAndClaimEvaluation + $retainedMaterialization
        $rawResidual = $parent - $childTotal
        if ($rawResidual -lt -$negativeResidualToleranceMilliseconds) {
            throw "Reserve-extraction child phase total exceeds its parent by $([Math]::Abs((Round-Value $rawResidual))) ms: $label"
        }
        $ticks.Add([pscustomobject][ordered]@{
            sequence = $sequence
            startTick = $startTick
            endTick = $endTick
            wallMilliseconds = $wall
            buildWorkOrdersParentMilliseconds = $buildWorkOrdersParent
            parentMilliseconds = $parent
            classPreparationMilliseconds = $classPreparation
            candidateEnumerationAndBoundSelectionMilliseconds = $candidateEnumerationAndBoundSelection
            activeFrontierAndClaimEvaluationMilliseconds = $activeFrontierAndClaimEvaluation
            retainedMaterializationMilliseconds = $retainedMaterialization
            childTotalMilliseconds = $childTotal
            residualMilliseconds = [Math]::Max(0.0, $rawResidual)
            residualWasToleranceClamped = $rawResidual -lt 0.0
        })
    }

    $spikeTicks = @($ticks | Where-Object { $_.wallMilliseconds -gt $SpikeThresholdMilliseconds })
    $internalRuns.Add([pscustomobject][ordered]@{
        run = Get-DisplayPath $runDirectory
        trialIndex = $trialIndex
        ticks = $ticks.ToArray()
        spikeTicks = @($spikeTicks | ForEach-Object { $_.endTick })
    })
    $runOutputs.Add([pscustomobject][ordered]@{
        run = Get-DisplayPath $runDirectory
        pairRoot = Get-DisplayPath $pairDirectory
        trialIndex = $trialIndex
        releaseExport = $releaseExport
        reusedReleaseRunner = $reusedReleaseRunner
        exportEditorExecutable = $equivalence.exportEditorExecutable
        pairStatus = [string](Require-Property $equivalence "status" $equivalencePath)
        pairContractStatus = [string](Require-Property $equivalence "contractStatus" $equivalencePath)
        capturedUtc = [string](Require-Property $result "capturedUtc" $resultPath)
        verifiedReleaseExecution = [bool](Require-Property $environment "verifiedReleaseExecution" $resultPath)
        exactInvocation = [string](Require-Property $result "exactInvocation" $resultPath)
        deterministicStateAndEventSha256 = $onHash
        metricsOffRuntimeProfileAbsent = $true
        measuredTickSummary = Get-ProfileSummary $ticks.ToArray()
        spikeCount = $spikeTicks.Count
        spikeEndSimulationTicks = @($spikeTicks | ForEach-Object { $_.endTick })
        spikeSummary = Get-ProfileSummary $spikeTicks
        artifacts = @(
            Get-ArtifactRecord $runtimePath
            Get-ArtifactRecord $resultPath
            Get-ArtifactRecord $equivalencePath
        )
    })
}

$orderedInternalRuns = @($internalRuns | Sort-Object trialIndex)
$orderedRunOutputs = @($runOutputs | Sort-Object trialIndex)
$orderedTrialIndexes = @($orderedRunOutputs | ForEach-Object { $_.trialIndex }) -join ','
if ($orderedTrialIndexes -ne '1,2,3') { throw "Trial indexes must be exactly 1, 2, and 3." }
$exportRun = @($orderedRunOutputs | Where-Object { $_.releaseExport })
if ($exportRun.Count -ne 1 -or $exportRun[0].trialIndex -ne 1 -or
    [string]::IsNullOrWhiteSpace([string]$exportRun[0].exportEditorExecutable)) {
    throw "Trial 1 must own the fresh ExportRelease route."
}
$godotPath = [string]$exportRun[0].exportEditorExecutable
$runnerPath = [string]$compatibility.runnerExecutablePath
foreach ($run in $orderedRunOutputs) {
    $routeArgument = if ($run.releaseExport) {
        "-ReleaseExport"
    }
    else {
        "-ExistingReleaseRunner '$runnerPath'"
    }
    $run | Add-Member -MemberType NoteProperty -Name reproductionCommand -Value (
        ".\scripts\run-performance-pair.ps1 $routeArgument -GodotPath '$godotPath' " +
        "-Scenario balanced_basin -Seed 1337 -Citizens 16 -WarmupTicks 2 -Ticks 300 -CacheMode cold " +
        "-ComparisonGroup w206-reserve-extraction-profile -TrialIndex $($run.trialIndex) " +
        "-OutputRoot '$($run.pairRoot)' -AllowDirtySource -AllowPrimarySafetyFailure")
}
$commonSpikeTicks = @($orderedInternalRuns[0].spikeTicks)
$unionSet = New-Object 'System.Collections.Generic.HashSet[long]'
foreach ($run in $orderedInternalRuns) {
    $runSet = New-Object 'System.Collections.Generic.HashSet[long]'
    foreach ($tick in $run.spikeTicks) {
        [void]$runSet.Add([long]$tick)
        [void]$unionSet.Add([long]$tick)
    }
    $commonSpikeTicks = @($commonSpikeTicks | Where-Object { $runSet.Contains([long]$_) })
}
$commonSpikeTicks = @($commonSpikeTicks | Sort-Object -Unique)
$unionSpikeTicks = @($unionSet | Sort-Object)

$perTrialCommon = New-Object System.Collections.Generic.List[object]
$allCommonSamples = New-Object System.Collections.Generic.List[object]
foreach ($run in $orderedInternalRuns) {
    $samples = @($run.ticks | Where-Object { $commonSpikeTicks -contains $_.endTick })
    foreach ($sample in $samples) { $allCommonSamples.Add($sample) }
    $perTrialCommon.Add([ordered]@{
        run = $run.run
        trialIndex = $run.trialIndex
        commonSpikeSummary = Get-ProfileSummary $samples
    })
}

$aggregateCommonSummary = Get-ProfileSummary $allCommonSamples.ToArray()
$repeatabilityVariance = [ordered]@{}
foreach ($entry in $profileMetrics.GetEnumerator()) {
    $category = [string]$entry.Key
    $totals = [double[]]@($perTrialCommon | ForEach-Object {
        [double]$_['commonSpikeSummary']['metrics'][$category]['totalMilliseconds']
    })
    $medians = [double[]]@($perTrialCommon | ForEach-Object {
        [double]$_['commonSpikeSummary']['metrics'][$category]['medianMilliseconds']
    })
    $repeatabilityVariance[$category] = [ordered]@{
        perTrialCommonSpikeTotalMilliseconds = Get-Variance $totals
        perTrialCommonSpikeMedianMilliseconds = Get-Variance $medians
    }
}

$dominantCommonTotal = [double]$aggregateCommonSummary.dominantExercisedSubCost.totalMilliseconds
$commonParentTotal = [double]$aggregateCommonSummary.metrics.parent.totalMilliseconds
$perTrialDominantCategories = @($perTrialCommon | ForEach-Object {
    [string]$_['commonSpikeSummary']['dominantExercisedSubCost']['category']
})
$uniquePerTrialDominantCategories = @($perTrialDominantCategories | Sort-Object -Unique)
$allTrialsHaveUniquePositiveWinner = @($perTrialCommon | Where-Object {
    $_['commonSpikeSummary']['dominantExercisedSubCost']['uniquePositiveMaximum'] -ne $true
}).Count -eq 0
$aggregateHasUniquePositiveWinner = $aggregateCommonSummary.dominantExercisedSubCost.uniquePositiveMaximum -eq $true
$selectedSubCost = if ($commonSpikeTicks.Count -eq 0) {
    [ordered]@{
        status = "instrumentation_insufficient"
        category = $null
        reason = "No spike tick exceeded the threshold in every supplied reference trial."
    }
}
elseif ($commonParentTotal -le 0.0 -or $dominantCommonTotal -le 0.0) {
    [ordered]@{
        status = "instrumentation_insufficient"
        category = $null
        reason = "Common spike samples do not contain positive parent and winning child-phase timings."
    }
}
elseif (-not $allTrialsHaveUniquePositiveWinner -or
    -not $aggregateHasUniquePositiveWinner -or
    $uniquePerTrialDominantCategories.Count -ne 1 -or
    $uniquePerTrialDominantCategories[0] -ne $aggregateCommonSummary.dominantExercisedSubCost.category) {
    [ordered]@{
        status = "instrumentation_insufficient"
        category = $null
        reason = "No unique positive code-level maximum dominates the common spike samples consistently in trials 1, 2, and 3 and in aggregate; ties are non-selectable."
        perTrialDominantCategories = $perTrialDominantCategories
        perTrialMaximumTiedCategories = @($perTrialCommon | ForEach-Object {
            @($_['commonSpikeSummary']['dominantExercisedSubCost']['maximumTiedCategories'])
        })
        aggregateMaximumTiedCategories = @($aggregateCommonSummary.dominantExercisedSubCost.maximumTiedCategories)
    }
}
else {
    [ordered]@{
        status = "selected"
        category = $aggregateCommonSummary.dominantExercisedSubCost.category
        totalMilliseconds = $aggregateCommonSummary.dominantExercisedSubCost.totalMilliseconds
        parentSharePercent = $aggregateCommonSummary.dominantExercisedSubCost.parentSharePercent
        basis = "single unique positive code-level maximum across common-spike totals in each trial and in aggregate; equal maxima are non-selectable"
        perTrialDominantCategories = $perTrialDominantCategories
    }
}

$allowedSurfaceByCategory = [ordered]@{
    class_preparation = @("AddExtractionOrders resource-class eligibility, priority-bound, and whole-class omission preparation", "pure helpers and focused tests for that preparation only")
    candidate_enumeration_and_bound_selection = @("AddExtractionOrders candidate construction and exact Top-K bound-selection path", "pure candidate-selection helpers and focused tests only")
    active_frontier_and_claim_evaluation = @("TryAddLightweightExtractionFrontier active-claim lookup, priority evaluation, and bounded frontier offers", "private claim/frontier helpers and focused tests only")
    retained_materialization = @("TryAddLightweightExtractionFrontier retained counting and order construction/appends", "CreateExtractionOrder and focused retained-materialization helpers/tests only")
}
$optimizationContract = if ($selectedSubCost.status -eq "selected") {
    [ordered]@{
        selectedOperation = $selectedSubCost.category
        exactBehaviorToPreserve = @(
            "byte-identical ordered final work orders, order IDs, priorities, directive metadata, targets, and virtual uncapped counts",
            "identical active-claim exclusion, duplicate/collision fallback, strict frontier capacity, priority/tie ordering, path-query behavior, and lightweight-frontier activation semantics",
            "identical per-tick assignments, worker state, resources, events, snapshots, and deterministic state/event hashes with metrics off and on"
        )
        allowedImplementationSurface = $allowedSurfaceByCategory[$selectedSubCost.category]
        forbiddenSurface = @("public contracts", "runtime snapshot or persistence schemas", "thresholds", "simulation tick/order semantics", "feature scope")
        referenceAndDifferentialMode = "retain the current selected-operation implementation as an internal opt-in reference; compare reference versus optimized mode per tick across direct fixtures and 300-tick shipped-scenario differentials before performance evidence"
        requiredRegressionCases = @(
            "zero/one/many active claims including ordinal prefix lookalikes",
            "duplicate existing order IDs and extraction-node collisions forcing fallback",
            "underfilled, exact-capacity, and over-capacity frontiers with equal-priority ordinal ties",
            "all five resource classes, no eligible sites, whole-class omission, and uncapped/exhaustive fallbacks",
            "navigation-version changes, reachable/unreachable routes, neutral/FoodAndFuel/Shelter directives, and checkpoint/resume"
        )
        fullValidation = @(
            "fresh focused managed and Godot diagnostic coverage",
            "complete manifest-owned .NET and exact-count Godot suites",
            "zero-warning Debug, Release, and ExportRelease builds",
            "all PowerShell analyzer suites, evidence hash verification, and git diff --check"
        )
        cleanCanonicalMatrixHardSafetyGate = [ordered]@{
            requiredBeforeW206CompletionOrDelivery = $true
            route = "fresh clean committed 14-pair ExportRelease canonical matrix after independent review"
            requiredResults = @(
                "reference median p95 must be less than or equal to 50 ms",
                "reference median maximum must be less than or equal to 250 ms",
                "both soak p95 and maximum safety limits must pass",
                "forced-invalidation transition and timing contract must pass",
                "all matrix, pair-equivalence, deterministic-hash, artifact-binding, and schema contracts must pass"
            )
            failureDisposition = "if any required result fails, W2-06 is not complete or deliverable, Stop Feature Expansion remains active, and the optimization is not mergeable"
            dirtySourceBoundary = "dirty-source characterization cannot satisfy or replace this clean canonical hard-safety gate"
        }
        rollbackCondition = "revert and do not merge the optimization if any direct/reference differential, deterministic hash, artifact/schema contract, build/test gate, canonical matrix/equivalence/hash contract, reference median p95 <= 50 ms, reference median max <= 250 ms, soak safety limit, or forced-invalidation contract fails; Stop Feature Expansion remains active"
    }
} else { $null }

$sourceFiles = @(
    "src/societies/scripts/simulation/SettlementEconomy.cs",
    "src/societies/scripts/simulation/SettlementSimulation.cs",
    "src/societies/scripts/core/RuntimeMetricsCollector.cs",
    "src/societies/scripts/core/PrototypeRunArtifactManager.cs",
    "src/societies/tests/PerfRunner.cs",
    "src/societies/tests/PerformanceRunModels.cs",
    "scripts/run-performance-pair.ps1",
    "scripts/analyze-build-work-orders-profile.ps1",
    "scripts/analyze-reserve-extraction-profile.ps1",
    "tests/scripts/test-analyze-reserve-extraction-profile.ps1",
    "tests/test-manifest.json"
)
$sourceFileRecords = @($sourceFiles | ForEach-Object {
    $path = Join-Path $repoRoot $_
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Profile source file is missing: $path" }
    Get-ArtifactRecord $path
})
$releaseArtifacts = @()
foreach ($path in @($compatibility.processExecutablePath, $compatibility.runnerExecutablePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Release runner artifact is missing: $path" }
    $releaseArtifacts += Get-ArtifactRecord $path
}

$output = [ordered]@{
    schemaVersion = 1
    analyzerVersion = $analyzerVersion
    workItem = "V3-W2-06 AddReserveExtractionOrders diagnostic characterization"
    status = if ($selectedSubCost.status -eq "selected") { "diagnostic_operation_selected" } else { "instrumentation_insufficient" }
    capturedUtc = [string]$orderedRunOutputs[0].capturedUtc
    claimBoundary = "diagnostic_characterization_only_not_release_gate_or_canonical_matrix"
    source = [ordered]@{
        gitSha = $sourceGitSha
        gitDirty = $sourceDirty
        provenance = if ($sourceDirty) { "uncommitted_diagnostic_source" } else { "clean_committed_source" }
        sourceFiles = $sourceFileRecords
        releaseArtifacts = $releaseArtifacts
    }
    configuration = [ordered]@{
        scenarioId = "balanced_basin"
        seed = 1337
        citizens = 16
        warmupTicks = 2
        measuredTicks = 300
        cacheMode = "cold"
        selectorMode = "exact_branch_and_bound"
        extractionPlanningMode = "exact_bounded"
        routeDistanceMode = "cached_distance_only"
        trialCount = $orderedRunOutputs.Count
        spikeThresholdMilliseconds = $SpikeThresholdMilliseconds
    }
    commands = [ordered]@{
        trialPairs = @($orderedRunOutputs | ForEach-Object { $_.reproductionCommand })
        analyzer = ".\scripts\analyze-reserve-extraction-profile.ps1 -InputPath '" +
            (($orderedRunOutputs | ForEach-Object { $_.run }) -join "','") +
            "' -OutputPath '<output-json>' -AllowDirtySource"
    }
    environment = $compatibility
    instrumentationContract = [ordered]@{
        runtimeMetricsSchemaVersion = [int]$compatibility.runtimeMetricsSchemaVersion
        runtimeMetricsFile = "runtime-batch-metrics-v$($compatibility.runtimeMetricsSchemaVersion).csv"
        enabledBoundary = "existing nullable RuntimeMetricsCollector created only when SOCIETIES_PERF_METRICS=1"
        metricsOffContract = "no collector clock reads and no runtime profile CSV"
        outerParentField = "build_work_orders_ms"
        parentField = "build_work_orders_reserve_extraction_ms"
        sequentialNonOverlappingChildFields = @(
            "reserve_extraction_class_preparation_ms",
            "reserve_extraction_candidate_enumeration_and_bound_selection_ms",
            "reserve_extraction_active_frontier_and_claim_evaluation_ms",
            "reserve_extraction_retained_materialization_ms"
        )
        residualFormula = "build_work_orders_reserve_extraction_ms - sum(sequential child fields)"
        residualMeaning = "reserve-target argument evaluation, control-flow, and diagnostic clock overhead inside the inclusive parent"
        negativeResidualToleranceMilliseconds = $negativeResidualToleranceMilliseconds
        parentWallToleranceMilliseconds = $parentWallToleranceMilliseconds
        invalidReconciliationHandling = "reject below negative tolerance; otherwise clamp to zero and flag"
        parserBounds = [ordered]@{
            maximumJsonBytes = $maximumJsonBytes
            maximumCsvBytes = $maximumCsvBytes
            maximumCsvLines = $maximumCsvLines
            maximumCsvLineCharacters = $maximumCsvLineCharacters
            maximumJsonNestingDepth = $maximumJsonNestingDepth
            enforcement = "preflight_before_ConvertFrom-Json_or_Import-Csv_materialization"
        }
    }
    runs = $orderedRunOutputs
    repeatability = [ordered]@{
        commonSpikeTickCount = $commonSpikeTicks.Count
        commonSpikeEndSimulationTicks = $commonSpikeTicks
        unionSpikeTickCount = $unionSpikeTicks.Count
        unionSpikeEndSimulationTicks = $unionSpikeTicks
        allRunsExactSpikeTickSetIdentity = $commonSpikeTicks.Count -eq $unionSpikeTicks.Count
        perTrialCommonSpikeSubCostSummaries = $perTrialCommon.ToArray()
        aggregateCommonSpikeSubCostSummary = $aggregateCommonSummary
        perTrialVariance = $repeatabilityVariance
    }
    selectedSubCost = $selectedSubCost
    proposedOptimizationContract = $optimizationContract
    limitations = @(
        "Metrics-on instrumentation adds monotonic clock reads and is unsuitable for release-gate timing claims.",
        "Dirty-source runs are identified as uncommitted diagnostic evidence and cannot become canonical release evidence.",
        "Only balanced_basin seed 1337, 16 citizens, cold cache, warmup ticks 2, and 300 measured ticks are characterized.",
        "Operation selection is descriptive and authorizes no optimization, threshold change, feature expansion, PR, or merge."
    )
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
Write-Utf8NoBom $resolvedOutput ($output | ConvertTo-Json -Depth 24)
Write-Output $resolvedOutput
