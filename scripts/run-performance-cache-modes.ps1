[CmdletBinding()]
param(
    [string]$Scenario = "balanced_basin",
    [int]$Seed = 1337,
    [int]$Citizens = 16,
    [int]$Ticks = 300,
    [Alias("WarmupTicks")]
    [int]$PreconditioningTicks = 1,
    [ValidateSet("exact_branch_and_bound", "exhaustive_reference")]
    [string]$SelectorMode = "exact_branch_and_bound",
    [ValidateSet("cached_distance_only", "full_materialization_reference")]
    [string]$RouteDistanceMode = "cached_distance_only",
    [string]$ComparisonGroup,
    [int]$TrialIndex = 1,
    [string]$OutputRoot,
    [string]$GodotPath,
    [string]$ExportPreset = "Windows Performance Release",
    [switch]$ReleaseExport,
    [switch]$AllowDirtySource,
    [switch]$AllowDebugReference,
    [switch]$AllowPrimarySafetyFailure
)

$ErrorActionPreference = "Stop"
$SelectorMode = $SelectorMode.ToLowerInvariant()
$RouteDistanceMode = $RouteDistanceMode.ToLowerInvariant()
$ExtractionPlanningMode = "exact_bounded"
if ($SelectorMode -ne "exact_branch_and_bound") {
    throw "The canonical cache-mode workflow requires SelectorMode 'exact_branch_and_bound'."
}
if ($RouteDistanceMode -ne "cached_distance_only") {
    throw "The canonical cache-mode workflow requires RouteDistanceMode 'cached_distance_only'."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$pairScript = Join-Path $PSScriptRoot "run-performance-pair.ps1"
Set-Location $repoRoot

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Read-JsonArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

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
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Cannot hash missing artifact: $Path"
    }

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-PropertiesEqual {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Left,
        [Parameter(Mandatory = $true)]
        [object]$Right,
        [Parameter(Mandatory = $true)]
        [string[]]$Properties
    )

    foreach ($property in $Properties) {
        if ($Left.$property -ne $Right.$property) {
            return $false
        }
    }

    return $true
}

function Test-PathEqual {
    param([object]$Left, [object]$Right)

    if ($null -eq $Left -or $null -eq $Right) {
        return $null -eq $Left -and $null -eq $Right
    }

    return [string]::Equals(
        [System.IO.Path]::GetFullPath([string]$Left),
        [System.IO.Path]::GetFullPath([string]$Right),
        [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-ManifestMatchesResult {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Manifest,
        [Parameter(Mandatory = $true)]
        [object]$Result
    )

    if ($Manifest.schemaVersion -ne 6 -or
        $Manifest.status -ne $Result.status -or
        $Manifest.exactInvocation -ne $Result.exactInvocation -or
        $Manifest.gitSha -ne $Result.configuration.gitSha -or
        $Manifest.gitDirty -ne $Result.configuration.gitDirty) {
        return $false
    }

    $manifestConfiguration = $Manifest.configuration | ConvertTo-Json -Compress -Depth 12
    $resultConfiguration = $Result.configuration | ConvertTo-Json -Compress -Depth 12
    $manifestEnvironment = $Manifest.environment | ConvertTo-Json -Compress -Depth 12
    $resultEnvironment = $Result.environment | ConvertTo-Json -Compress -Depth 12
    $manifestCacheEvidence = $Manifest.cacheEvidence | ConvertTo-Json -Compress -Depth 12
    $resultCacheEvidence = $Result.cacheEvidence | ConvertTo-Json -Compress -Depth 12
    $manifestHashes = $Manifest.hashes | ConvertTo-Json -Compress -Depth 12
    $resultHashes = $Result.hashes | ConvertTo-Json -Compress -Depth 12

    return $manifestConfiguration -eq $resultConfiguration -and
        $manifestEnvironment -eq $resultEnvironment -and
        $manifestCacheEvidence -eq $resultCacheEvidence -and
        $Manifest.measuredCachedRouteDistanceFastPathHits -eq $Result.measuredCachedRouteDistanceFastPathHits -and
        $manifestHashes -eq $resultHashes
}

function Invoke-ModePair {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("cold", "natural_warm", "forced_invalidation")]
        [string]$Mode,
        [Parameter(Mandatory = $true)]
        [string]$ModeOutputRoot,
        [Parameter(Mandatory = $true)]
        [int]$MeasuredTicks,
        [Parameter(Mandatory = $true)]
        [bool]$SkipDebugBuild
    )

    $arguments = @{
        Scenario = $Scenario
        Seed = $Seed
        Citizens = $Citizens
        Ticks = $MeasuredTicks
        WarmupTicks = $PreconditioningTicks
        CacheMode = $Mode
        SelectorMode = $SelectorMode
        ExtractionPlanningMode = $ExtractionPlanningMode
        RouteDistanceMode = $RouteDistanceMode
        ComparisonGroup = $script:resolvedComparisonGroup
        TrialIndex = $TrialIndex
        OutputRoot = $ModeOutputRoot
        ExportPreset = $ExportPreset
    }

    if (-not [string]::IsNullOrWhiteSpace($GodotPath)) {
        $arguments.GodotPath = $GodotPath
    }
    if ($ReleaseExport) {
        $arguments.ReleaseExport = $true
    }
    elseif ($SkipDebugBuild) {
        $arguments.SkipBuild = $true
    }
    if ($AllowDirtySource) {
        $arguments.AllowDirtySource = $true
    }
    if ($AllowDebugReference) {
        $arguments.AllowDebugReference = $true
    }
    if ($AllowPrimarySafetyFailure) {
        $arguments.AllowPrimarySafetyFailure = $true
    }

    & $pairScript @arguments
}

function Read-And-ValidatePair {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("cold", "natural_warm", "forced_invalidation")]
        [string]$Mode,
        [Parameter(Mandatory = $true)]
        [string]$ModeOutputRoot,
        [Parameter(Mandatory = $true)]
        [int]$ExpectedMeasuredTicks
    )

    $equivalencePath = Join-Path $ModeOutputRoot "equivalence-results.json"
    $offResultPath = Join-Path $ModeOutputRoot "metrics-off\perf-results.json"
    $onResultPath = Join-Path $ModeOutputRoot "metrics-on\perf-results.json"
    $offManifestPath = Join-Path $ModeOutputRoot "metrics-off\validation-manifest.json"
    $onManifestPath = Join-Path $ModeOutputRoot "metrics-on\validation-manifest.json"
    $matrixPath = Join-Path $ModeOutputRoot "perf-matrix.csv"

    $equivalence = Read-JsonArtifact -Path $equivalencePath -Label "$Mode equivalence artifact"
    $offResult = Read-JsonArtifact -Path $offResultPath -Label "$Mode metrics-off result"
    $onResult = Read-JsonArtifact -Path $onResultPath -Label "$Mode metrics-on result"
    $offManifest = Read-JsonArtifact -Path $offManifestPath -Label "$Mode metrics-off validation manifest"
    $onManifest = Read-JsonArtifact -Path $onManifestPath -Label "$Mode metrics-on validation manifest"

    $expectedStatus = if ($equivalence.sourceClean -eq $true) { "pass" } else { "pass_dirty_source" }
    if ($equivalence.schemaVersion -ne 6 -or
        $equivalence.status -ne $expectedStatus -or
        $equivalence.contractStatus -ne $expectedStatus -or
        $equivalence.singleModeTransitionEvidence -ne $true -or
        $equivalence.routeDistanceEvidenceValid -ne $true -or
        $equivalence.cacheModeEvidence -ne $false -or
        $equivalence.crossModeEquivalenceCaptured -ne $false -or
        $equivalence.releaseBaselineEvidence -ne $false -or
        $equivalence.fullMatrixCaptured -ne $false -or
        $equivalence.medianOfThreeCaptured -ne $false -or
        $equivalence.targetOrSafetyClaimMade -ne $false -or
        $equivalence.cacheMode -ne $Mode -or
        $equivalence.selectorMode -ne $SelectorMode -or
        $equivalence.extractionPlanningMode -ne $ExtractionPlanningMode -or
        $equivalence.routeDistanceMode -ne $RouteDistanceMode -or
        $equivalence.comparisonGroup -ne $script:resolvedComparisonGroup -or
        $equivalence.trialIndex -ne $TrialIndex) {
        throw "$Mode equivalence artifact did not satisfy the single-mode v6 contract."
    }
    if ($equivalence.sourceClean -ne $true -and -not $AllowDirtySource) {
        throw "$Mode pair reported dirty source without -AllowDirtySource."
    }
    if ($equivalence.releaseExport -ne [bool]$ReleaseExport) {
        throw "$Mode pair execution route does not match the requested ReleaseExport mode."
    }

    foreach ($result in @($offResult, $onResult)) {
        if ($result.schemaVersion -ne 6 -or
            $result.configuration.cacheMode -ne $Mode -or
            $result.configuration.selectorMode -ne $SelectorMode -or
            $result.configuration.extractionPlanningMode -ne $ExtractionPlanningMode -or
            $result.configuration.routeDistanceMode -ne $RouteDistanceMode -or
            $result.configuration.comparisonGroup -ne $script:resolvedComparisonGroup -or
            $result.configuration.trialIndex -ne $TrialIndex -or
            $result.configuration.scenarioId -ne $Scenario -or
            $result.configuration.simulationSeed -ne $Seed -or
            $result.configuration.citizenCount -ne $Citizens -or
            $result.configuration.warmupTicks -ne $PreconditioningTicks -or
            $result.configuration.measuredTicks -ne $ExpectedMeasuredTicks -or
            $result.measuredStartSimulationTick -ne $PreconditioningTicks -or
            $result.finalSimulationTick -ne ($PreconditioningTicks + $ExpectedMeasuredTicks)) {
            throw "$Mode result does not match the requested configuration and tick boundaries."
        }
    }
    if ($offResult.configuration.metricsEnabled -ne $false -or
        $onResult.configuration.metricsEnabled -ne $true) {
        throw "$Mode pair did not contain one metrics-off and one metrics-on run."
    }
    if (-not (Test-ManifestMatchesResult -Manifest $offManifest -Result $offResult) -or
        -not (Test-ManifestMatchesResult -Manifest $onManifest -Result $onResult)) {
        throw "$Mode validation manifest does not match its v6 performance result."
    }
    if ($equivalence.metricsOffHash -ne $offResult.hashes.deterministicStateAndEventSha256 -or
        $equivalence.metricsOnHash -ne $onResult.hashes.deterministicStateAndEventSha256 -or
        $offResult.hashes.snapshotSha256 -ne $onResult.hashes.snapshotSha256 -or
        $offResult.hashes.eventLogSha256 -ne $onResult.hashes.eventLogSha256 -or
        $offResult.hashes.deterministicStateAndEventSha256 -ne
            $onResult.hashes.deterministicStateAndEventSha256) {
        throw "$Mode pair result hashes do not match its equivalence contract."
    }
    if (-not (Test-PathEqual $equivalence.metricsOffResult $offResultPath) -or
        -not (Test-PathEqual $equivalence.metricsOnResult $onResultPath)) {
        throw "$Mode equivalence artifact points at the wrong result files."
    }
    if (-not (Test-Path -LiteralPath $matrixPath -PathType Leaf)) {
        throw "$Mode aggregate matrix is missing: $matrixPath"
    }

    return [pscustomobject]@{
        Mode = $Mode
        OutputRoot = $ModeOutputRoot
        EquivalencePath = $equivalencePath
        OffResultPath = $offResultPath
        OnResultPath = $onResultPath
        OffManifestPath = $offManifestPath
        OnManifestPath = $onManifestPath
        MatrixPath = $matrixPath
        Equivalence = $equivalence
        OffResult = $offResult
        OnResult = $onResult
    }
}

function New-PairSummary {
    param([Parameter(Mandatory = $true)][object]$Pair)

    return [ordered]@{
        cacheMode = $Pair.Mode
        status = $Pair.Equivalence.status
        contractStatus = $Pair.Equivalence.contractStatus
        singleModeTransitionEvidence = [bool]$Pair.Equivalence.singleModeTransitionEvidence
        sourceClean = [bool]$Pair.Equivalence.sourceClean
        releaseEnvironmentValid = [bool]$Pair.Equivalence.releaseEnvironmentValid
        executionRoute = $Pair.Equivalence.executionRoute
        comparisonGroup = $Pair.Equivalence.comparisonGroup
        trialIndex = $Pair.Equivalence.trialIndex
        measuredStartSimulationTick = $Pair.OffResult.measuredStartSimulationTick
        finalSimulationTick = $Pair.OffResult.finalSimulationTick
        hashes = $Pair.OffResult.hashes
        outputRoot = $Pair.OutputRoot
        artifacts = @(
            [ordered]@{ role = "equivalence"; path = $Pair.EquivalencePath; sha256 = Get-Sha256 $Pair.EquivalencePath },
            [ordered]@{ role = "metrics_off_result"; path = $Pair.OffResultPath; sha256 = Get-Sha256 $Pair.OffResultPath },
            [ordered]@{ role = "metrics_on_result"; path = $Pair.OnResultPath; sha256 = Get-Sha256 $Pair.OnResultPath },
            [ordered]@{ role = "metrics_off_manifest"; path = $Pair.OffManifestPath; sha256 = Get-Sha256 $Pair.OffManifestPath },
            [ordered]@{ role = "metrics_on_manifest"; path = $Pair.OnManifestPath; sha256 = Get-Sha256 $Pair.OnManifestPath },
            [ordered]@{ role = "pair_matrix"; path = $Pair.MatrixPath; sha256 = Get-Sha256 $Pair.MatrixPath }
        )
    }
}

$timestamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss-fff")
$safeScenario = $Scenario -replace '[^A-Za-z0-9._-]', '-'
if ([string]::IsNullOrWhiteSpace($safeScenario)) {
    $safeScenario = "scenario"
}
if ($safeScenario.Length -gt 32) {
    $safeScenario = $safeScenario.Substring(0, 32)
}
if (-not $OutputRoot) {
    $OutputRoot = Join-Path $repoRoot "artifacts\performance\cache-modes-$timestamp-$safeScenario-seed$Seed-c$Citizens"
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Cache-mode comparison output already exists: $OutputRoot"
}
[System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null

$comparisonPath = Join-Path $OutputRoot "cache-mode-comparison.json"
$combinedMatrixPath = Join-Path $OutputRoot "cache-mode-matrix.csv"
$summaryPath = Join-Path $OutputRoot "cache-mode-summary.txt"
$pairSummaries = @()
$contracts = [ordered]@{}
$failedChecks = New-Object System.Collections.Generic.List[string]
$currentPhase = "input_validation"
$failureMessage = $null
$overallStatus = "fail"
$cacheModeContractEvidence = $false
$cacheModeEvidence = $false
$crossModeEquivalenceCaptured = $false

try {
    if (-not (Test-Path -LiteralPath $pairScript -PathType Leaf)) {
        throw "Pair runner is missing: $pairScript"
    }
    if ([string]::IsNullOrWhiteSpace($Scenario)) {
        throw "Scenario cannot be empty."
    }
    if ($Citizens -lt 1 -or $Citizens -gt 256) {
        throw "Citizens must be between 1 and 256."
    }
    if ($Ticks -lt 1 -or $Ticks -gt 4096) {
        throw "Ticks must be between 1 and 4096."
    }
    if ($PreconditioningTicks -lt 1 -or $PreconditioningTicks -gt 100000) {
        throw "PreconditioningTicks must be between 1 and 100000."
    }
    if ($TrialIndex -lt 1 -or $TrialIndex -gt 100) {
        throw "TrialIndex must be between 1 and 100."
    }

    $script:resolvedComparisonGroup = if ([string]::IsNullOrWhiteSpace($ComparisonGroup)) {
        "$safeScenario-seed$Seed-c$Citizens-t$Ticks-w$PreconditioningTicks-s$SelectorMode-cache-modes"
    }
    else {
        $ComparisonGroup
    }
    if ($script:resolvedComparisonGroup.Length -gt 96 -or
        $script:resolvedComparisonGroup.Contains("..") -or
        $script:resolvedComparisonGroup -notmatch '^[A-Za-z0-9._-]+$') {
        throw "ComparisonGroup may contain only letters, digits, '.', '_' and '-' and may not contain '..' or exceed 96 characters."
    }

    $coldRoot = Join-Path $OutputRoot "cold"
    $warmRoot = Join-Path $OutputRoot "natural-warm"
    $forcedRoot = Join-Path $OutputRoot "forced-invalidation"

    $currentPhase = "cold_pair"
    Invoke-ModePair -Mode "cold" -ModeOutputRoot $coldRoot -MeasuredTicks $Ticks -SkipDebugBuild $false
    $cold = Read-And-ValidatePair -Mode "cold" -ModeOutputRoot $coldRoot -ExpectedMeasuredTicks $Ticks
    $pairSummaries += New-PairSummary $cold

    $currentPhase = "natural_warm_pair"
    Invoke-ModePair -Mode "natural_warm" -ModeOutputRoot $warmRoot -MeasuredTicks $Ticks -SkipDebugBuild (-not $ReleaseExport)
    $warm = Read-And-ValidatePair -Mode "natural_warm" -ModeOutputRoot $warmRoot -ExpectedMeasuredTicks $Ticks
    $pairSummaries += New-PairSummary $warm

    $currentPhase = "forced_invalidation_pair"
    Invoke-ModePair -Mode "forced_invalidation" -ModeOutputRoot $forcedRoot -MeasuredTicks 1 -SkipDebugBuild (-not $ReleaseExport)
    $forced = Read-And-ValidatePair -Mode "forced_invalidation" -ModeOutputRoot $forcedRoot -ExpectedMeasuredTicks 1
    $pairSummaries += New-PairSummary $forced

    $pairs = @($cold, $warm, $forced)

    $currentPhase = "cross_mode_contract"
    $configurationProperties = @(
        "scenarioId",
        "simulationSeed",
        "citizenCount",
        "warmupTicks",
        "measuredTicks",
        "allowSafetyFailure",
        "gitSha",
        "gitDirty",
        "executionRoute",
        "projectPath",
        "comparisonGroup",
        "trialIndex",
        "warmupMode",
        "cacheWarmupEnabled",
        "selectorMode",
        "measurementMode",
        "budgetProfile"
    )
    $sharedConfigurationProperties = @(
        "scenarioId",
        "simulationSeed",
        "citizenCount",
        "warmupTicks",
        "gitSha",
        "gitDirty",
        "executionRoute",
        "projectPath",
        "comparisonGroup",
        "trialIndex",
        "warmupMode",
        "cacheWarmupEnabled",
        "selectorMode",
        "extractionPlanningMode",
        "routeDistanceMode",
        "measurementMode"
    )
    $environmentProperties = @(
        "machineName",
        "logicalProcessorCount",
        "operatingSystem",
        "processArchitecture",
        "dotnetRuntime",
        "godotVersion",
        "managedBuildConfiguration",
        "managedAssemblyConfiguration",
        "godotDebugBuild",
        "godotReleaseFeature",
        "godotTemplateFeature",
        "godotEditorFeature",
        "verifiedReleaseExecution"
    )
    $routeProperties = @(
        "expectedGodotVersion",
        "resolvedEditorVersion",
        "releaseRequired",
        "releaseExport",
        "exportPreset",
        "executionRoute",
        "selectorMode",
        "routeDistanceMode",
        "releaseEnvironmentValid",
        "godotVersionValid"
    )

    $contracts.coldWarmConfigurationIdentityValid = Test-PropertiesEqual `
        $cold.OffResult.configuration $warm.OffResult.configuration $configurationProperties
    $contracts.coldWarmGitIdentityValid =
        $cold.OffResult.configuration.gitSha -eq $warm.OffResult.configuration.gitSha -and
        $cold.OffResult.configuration.gitDirty -eq $warm.OffResult.configuration.gitDirty
    $contracts.coldWarmEnvironmentIdentityValid =
        (Test-PropertiesEqual $cold.OffResult.environment $warm.OffResult.environment $environmentProperties) -and
        ([System.IO.Path]::GetFileName([string]$cold.OffResult.environment.processExecutablePath) -eq
            [System.IO.Path]::GetFileName([string]$warm.OffResult.environment.processExecutablePath))
    $contracts.coldWarmRouteIdentityValid =
        (Test-PropertiesEqual $cold.Equivalence $warm.Equivalence $routeProperties) -and
        ([System.IO.Path]::GetFileName([string]$cold.Equivalence.runnerExecutable) -eq
            [System.IO.Path]::GetFileName([string]$warm.Equivalence.runnerExecutable))
    $contracts.coldWarmTickIdentityValid =
        $cold.OffResult.measuredStartSimulationTick -eq $warm.OffResult.measuredStartSimulationTick -and
        $cold.OffResult.finalSimulationTick -eq $warm.OffResult.finalSimulationTick
    $contracts.coldWarmSnapshotHashMatch =
        $cold.OffResult.hashes.snapshotSha256 -eq $warm.OffResult.hashes.snapshotSha256 -and
        $cold.OnResult.hashes.snapshotSha256 -eq $warm.OnResult.hashes.snapshotSha256
    $contracts.coldWarmEventLogHashMatch =
        $cold.OffResult.hashes.eventLogSha256 -eq $warm.OffResult.hashes.eventLogSha256 -and
        $cold.OnResult.hashes.eventLogSha256 -eq $warm.OnResult.hashes.eventLogSha256
    $contracts.coldWarmCombinedHashMatch =
        $cold.OffResult.hashes.deterministicStateAndEventSha256 -eq
            $warm.OffResult.hashes.deterministicStateAndEventSha256 -and
        $cold.OnResult.hashes.deterministicStateAndEventSha256 -eq
            $warm.OnResult.hashes.deterministicStateAndEventSha256
    $contracts.forcedSharedConfigurationIdentityValid = Test-PropertiesEqual `
        $cold.OffResult.configuration $forced.OffResult.configuration $sharedConfigurationProperties
    $contracts.forcedEnvironmentIdentityValid =
        (Test-PropertiesEqual $cold.OffResult.environment $forced.OffResult.environment $environmentProperties) -and
        ([System.IO.Path]::GetFileName([string]$cold.OffResult.environment.processExecutablePath) -eq
            [System.IO.Path]::GetFileName([string]$forced.OffResult.environment.processExecutablePath))
    $contracts.forcedRouteIdentityValid =
        (Test-PropertiesEqual $cold.Equivalence $forced.Equivalence $routeProperties) -and
        ([System.IO.Path]::GetFileName([string]$cold.Equivalence.runnerExecutable) -eq
            [System.IO.Path]::GetFileName([string]$forced.Equivalence.runnerExecutable))
    $contracts.forcedTickBoundaryValid =
        $forced.OffResult.configuration.measuredTicks -eq 1 -and
        $forced.OffResult.measuredStartSimulationTick -eq $PreconditioningTicks -and
        $forced.OffResult.finalSimulationTick -eq ($PreconditioningTicks + 1)
    $contracts.forcedSingleModeTransitionValid =
        $forced.Equivalence.singleModeTransitionEvidence -eq $true -and
        $forced.Equivalence.cacheTransitionContractValid -eq $true -and
        $forced.Equivalence.cacheDiagnosticsContractValid -eq $true
    $contracts.pairStatusAndSourceIdentityValid =
        $cold.Equivalence.status -eq $warm.Equivalence.status -and
        $cold.Equivalence.status -eq $forced.Equivalence.status -and
        $cold.Equivalence.sourceClean -eq $warm.Equivalence.sourceClean -and
        $cold.Equivalence.sourceClean -eq $forced.Equivalence.sourceClean

    foreach ($contract in $contracts.GetEnumerator()) {
        if ($contract.Value -ne $true) {
            $failedChecks.Add([string]$contract.Key)
        }
    }
    if ($failedChecks.Count -gt 0) {
        throw "Cache-mode comparison contract failed: $($failedChecks -join ', ')."
    }

    $currentPhase = "combined_matrix"
    $matrixHeader = $null
    $matrixRows = New-Object System.Collections.Generic.List[string]
    foreach ($pair in $pairs) {
        $lines = @(Get-Content -LiteralPath $pair.MatrixPath)
        if ($lines.Count -ne 3) {
            throw "$($pair.Mode) pair matrix must contain one header and two rows."
        }
        if ($null -eq $matrixHeader) {
            $matrixHeader = $lines[0]
        }
        elseif ($lines[0] -ne $matrixHeader) {
            throw "$($pair.Mode) pair matrix header does not match the comparison schema."
        }
        $matrixRows.Add($lines[1])
        $matrixRows.Add($lines[2])
    }
    $combinedMatrixLines = @($matrixHeader) + $matrixRows.ToArray()
    Write-Utf8NoBom -Path $combinedMatrixPath -Content ($combinedMatrixLines -join [System.Environment]::NewLine)
    $contracts.combinedMatrixValid = $true

    $overallStatus = if ($cold.Equivalence.sourceClean -eq $true) { "pass" } else { "pass_dirty_source" }
    $cacheModeContractEvidence = $true
    $cacheModeEvidence =
        $cold.Equivalence.sourceClean -eq $true -and
        $ReleaseExport -and
        $cold.Equivalence.releaseEnvironmentValid -eq $true
    $crossModeEquivalenceCaptured = $true

    $summaryLines = @(
        "Societies cache-mode comparison",
        "Status: $overallStatus",
        "Scenario: $Scenario; seed: $Seed; citizens: $Citizens; preconditioning ticks: $PreconditioningTicks",
        "Cold/warm measured ticks: $Ticks; forced-invalidation measured ticks: 1",
        "Selector mode: $SelectorMode; extraction planning mode: $ExtractionPlanningMode; route-distance mode: $RouteDistanceMode; comparison group: $script:resolvedComparisonGroup; trial: $TrialIndex",
        "Execution route: $($cold.Equivalence.executionRoute); verified release: $($cold.Equivalence.releaseEnvironmentValid)",
        "Cold/warm snapshot hash match: $($contracts.coldWarmSnapshotHashMatch)",
        "Cold/warm event-log hash match: $($contracts.coldWarmEventLogHashMatch)",
        "Cold/warm combined hash match: $($contracts.coldWarmCombinedHashMatch)",
        "Forced transition valid: $($contracts.forcedSingleModeTransitionValid)",
        "Route-independent cache-mode contract evidence: $cacheModeContractEvidence",
        "Clean verified-Release cache-mode evidence: $cacheModeEvidence",
        "Baseline/full-matrix/median/target-safety claims: false",
        "Output: $OutputRoot"
    )
    Write-Utf8NoBom -Path $summaryPath -Content ($summaryLines -join [System.Environment]::NewLine)

    $comparison = [ordered]@{
        schemaVersion = 1
        sourceResultSchemaVersion = 6
        capturedUtc = [DateTime]::UtcNow.ToString("o")
        status = $overallStatus
        contractStatus = $overallStatus
        scope = "V3-W1-03b_cache_modes_and_cross_mode_equivalence_only"
        cacheModeContractEvidence = $cacheModeContractEvidence
        cacheModeEvidence = $cacheModeEvidence
        crossModeEquivalenceCaptured = $crossModeEquivalenceCaptured
        releaseBaselineEvidence = $false
        fullMatrixCaptured = $false
        medianOfThreeCaptured = $false
        targetOrSafetyClaimMade = $false
        sourceClean = [bool]$cold.Equivalence.sourceClean
        sourceCommit = $cold.OffResult.configuration.gitSha
        releaseExport = [bool]$ReleaseExport
        verifiedReleaseExecution = [bool]$cold.Equivalence.releaseEnvironmentValid
        configuration = [ordered]@{
            scenarioId = $Scenario
            simulationSeed = $Seed
            citizenCount = $Citizens
            preconditioningTicks = $PreconditioningTicks
            coldWarmMeasuredTicks = $Ticks
            forcedInvalidationMeasuredTicks = 1
            selectorMode = $SelectorMode
            extractionPlanningMode = $ExtractionPlanningMode
            routeDistanceMode = $RouteDistanceMode
            comparisonGroup = $script:resolvedComparisonGroup
            trialIndex = $TrialIndex
        }
        contracts = $contracts
        pairs = $pairSummaries
        artifacts = @(
            [ordered]@{ role = "combined_matrix"; path = $combinedMatrixPath; sha256 = Get-Sha256 $combinedMatrixPath },
            [ordered]@{ role = "summary"; path = $summaryPath; sha256 = Get-Sha256 $summaryPath }
        )
        failedChecks = @()
        error = $null
    }
    Write-Utf8NoBom -Path $comparisonPath -Content ($comparison | ConvertTo-Json -Depth 16)

    Write-Host "Cache-mode comparison completed with status '$overallStatus'."
    Write-Host "Output: $OutputRoot"
}
catch {
    $failureMessage = $_.Exception.Message
    if (-not $failedChecks.Contains($currentPhase)) {
        $failedChecks.Add($currentPhase)
    }

    try {
        $failureSummary = @(
            "Societies cache-mode comparison",
            "Status: fail",
            "Failed phase: $currentPhase",
            "Error: $failureMessage",
            "Baseline/full-matrix/median/target-safety claims: false",
            "Output: $OutputRoot"
        )
        Write-Utf8NoBom -Path $summaryPath -Content ($failureSummary -join [System.Environment]::NewLine)

        $failure = [ordered]@{
            schemaVersion = 1
            sourceResultSchemaVersion = 6
            capturedUtc = [DateTime]::UtcNow.ToString("o")
            status = "fail"
            contractStatus = "fail"
            scope = "V3-W1-03b_cache_modes_and_cross_mode_equivalence_only"
            cacheModeContractEvidence = $false
            cacheModeEvidence = $false
            crossModeEquivalenceCaptured = $false
            releaseBaselineEvidence = $false
            fullMatrixCaptured = $false
            medianOfThreeCaptured = $false
            targetOrSafetyClaimMade = $false
            releaseExport = [bool]$ReleaseExport
            configuration = [ordered]@{
                scenarioId = $Scenario
                simulationSeed = $Seed
                citizenCount = $Citizens
                preconditioningTicks = $PreconditioningTicks
                coldWarmMeasuredTicks = $Ticks
                forcedInvalidationMeasuredTicks = 1
                selectorMode = $SelectorMode
                extractionPlanningMode = $ExtractionPlanningMode
                routeDistanceMode = $RouteDistanceMode
                comparisonGroup = $script:resolvedComparisonGroup
                trialIndex = $TrialIndex
            }
            contracts = $contracts
            pairs = $pairSummaries
            artifacts = @(
                [ordered]@{ role = "summary"; path = $summaryPath; sha256 = Get-Sha256 $summaryPath }
            )
            failedChecks = @($failedChecks)
            error = $failureMessage
        }
        Write-Utf8NoBom -Path $comparisonPath -Content ($failure | ConvertTo-Json -Depth 16)
    }
    catch {
        Write-Error "Could not write cache-mode failure artifact: $($_.Exception.Message)"
    }

    throw
}
