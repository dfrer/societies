[CmdletBinding()]
param(
    [string]$Scenario = "balanced_basin",
    [int]$Seed = 1337,
    [int]$PreconditioningTicks = 2,
    [ValidateSet("exact_branch_and_bound", "exhaustive_reference")]
    [string]$SelectorMode = "exact_branch_and_bound",
    [string[]]$CaseId,
    [string]$OutputRoot,
    [string]$GodotPath,
    [string]$ExportPreset = "Windows Performance Release",
    [switch]$PlanOnly,
    [switch]$DebugCharacterization,
    [switch]$AllowDirtySource
)

$ErrorActionPreference = "Stop"
$SelectorMode = $SelectorMode.ToLowerInvariant()
if ($SelectorMode -ne "exact_branch_and_bound") {
    throw "The canonical baseline workflow requires SelectorMode 'exact_branch_and_bound'."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$pairScript = Join-Path $PSScriptRoot "run-performance-pair.ps1"
$releaseExport = -not $DebugCharacterization
$exactInvocation = if ([string]::IsNullOrWhiteSpace($MyInvocation.Line)) {
    [System.Environment]::CommandLine
}
else {
    $MyInvocation.Line.Trim()
}
Set-Location $repoRoot

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Write-JsonArtifact {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    Write-Utf8NoBom -Path $Path -Content ($Value | ConvertTo-Json -Depth 32)
}

function Read-JsonArtifact {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
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

function Get-CombinedStateAndEventSha256 {
    param(
        [Parameter(Mandatory = $true)][string]$SnapshotPath,
        [Parameter(Mandatory = $true)][string]$EventLogPath
    )

    if (-not (Test-Path -LiteralPath $SnapshotPath -PathType Leaf)) {
        throw "Cannot hash missing snapshot artifact: $SnapshotPath"
    }
    if (-not (Test-Path -LiteralPath $EventLogPath -PathType Leaf)) {
        throw "Cannot hash missing event-log artifact: $EventLogPath"
    }

    $snapshotBytes = [System.IO.File]::ReadAllBytes($SnapshotPath)
    $separatorBytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes("`n--event-log--`n")
    $eventLogBytes = [System.IO.File]::ReadAllBytes($EventLogPath)
    $combinedBytes = [byte[]]::new($snapshotBytes.Length + $separatorBytes.Length + $eventLogBytes.Length)
    [System.Buffer]::BlockCopy($snapshotBytes, 0, $combinedBytes, 0, $snapshotBytes.Length)
    [System.Buffer]::BlockCopy($separatorBytes, 0, $combinedBytes, $snapshotBytes.Length, $separatorBytes.Length)
    [System.Buffer]::BlockCopy(
        $eventLogBytes,
        0,
        $combinedBytes,
        $snapshotBytes.Length + $separatorBytes.Length,
        $eventLogBytes.Length)

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($combinedBytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Test-Sha256 {
    param([object]$Value)

    return $null -ne $Value -and [string]$Value -match '^[0-9a-fA-F]{64}$'
}

function Test-GitSha {
    param([object]$Value)

    return $null -ne $Value -and [string]$Value -match '^[0-9a-fA-F]{40}$'
}

function Get-GitDirtyState {
    & git diff --quiet --ignore-submodules --
    $unstagedExitCode = $LASTEXITCODE
    if ($unstagedExitCode -gt 1) {
        throw "Could not inspect unstaged source changes."
    }

    & git diff --cached --quiet --ignore-submodules --
    $stagedExitCode = $LASTEXITCODE
    if ($stagedExitCode -gt 1) {
        throw "Could not inspect staged source changes."
    }

    $untrackedPaths = @(& git ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect untracked source files."
    }

    return $unstagedExitCode -eq 1 -or
        $stagedExitCode -eq 1 -or
        $untrackedPaths.Count -gt 0
}

function Test-FiniteNumber {
    param([object]$Value)

    if ($null -eq $Value) {
        return $false
    }

    try {
        $number = [double]$Value
        return -not [double]::IsNaN($number) -and -not [double]::IsInfinity($number)
    }
    catch {
        return $false
    }
}

function Test-NonNegativeFiniteNumber {
    param([object]$Value)

    return (Test-FiniteNumber $Value) -and [double]$Value -ge 0.0
}

function Get-Median {
    param([Parameter(Mandatory = $true)][double[]]$Values)

    if ($Values.Count -eq 0) {
        throw "Cannot calculate a median from an empty value set."
    }

    foreach ($value in $Values) {
        if (-not (Test-FiniteNumber $value)) {
            throw "Median values must be finite."
        }
    }

    $sorted = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if (($sorted.Count % 2) -eq 1) {
        return [double]$sorted[$middle]
    }

    return ([double]$sorted[$middle - 1] + [double]$sorted[$middle]) / 2.0
}

function Assert-Contract {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function New-BaselineCase {
    param(
        [Parameter(Mandatory = $true)][string]$Id,
        [Parameter(Mandatory = $true)][string[]]$Roles,
        [Parameter(Mandatory = $true)][int]$Citizens,
        [Parameter(Mandatory = $true)][int]$Ticks,
        [Parameter(Mandatory = $true)]
        [ValidateSet("cold", "natural_warm", "forced_invalidation")]
        [string]$CacheMode,
        [Parameter(Mandatory = $true)][int]$TrialIndex
    )

    return [pscustomobject][ordered]@{
        id = $Id
        roles = @($Roles)
        scenarioId = $Scenario
        simulationSeed = $Seed
        citizens = $Citizens
        preconditioningTicks = $PreconditioningTicks
        measuredTicks = $Ticks
        cacheMode = $CacheMode
        trialIndex = $TrialIndex
        comparisonGroup = "w103c-$($Id -replace '-t[0-9]+$', '')"
    }
}

function Get-CanonicalCases {
    $cases = New-Object System.Collections.ArrayList
    foreach ($citizens in @(3, 6, 12, 16)) {
        $coldRoles = @("matrix")
        if ($citizens -eq 16) {
            $coldRoles += "release_reference"
        }

        [void]$cases.Add((New-BaselineCase `
            -Id "matrix-c$citizens-cold-t1" `
            -Roles $coldRoles `
            -Citizens $citizens `
            -Ticks 300 `
            -CacheMode "cold" `
            -TrialIndex 1))
        [void]$cases.Add((New-BaselineCase `
            -Id "matrix-c$citizens-warm-t1" `
            -Roles @("matrix", "warm_diagnostic") `
            -Citizens $citizens `
            -Ticks 300 `
            -CacheMode "natural_warm" `
            -TrialIndex 1))
    }

    foreach ($trial in @(2, 3)) {
        [void]$cases.Add((New-BaselineCase `
            -Id "reference-c16-cold-t$trial" `
            -Roles @("release_reference") `
            -Citizens 16 `
            -Ticks 300 `
            -CacheMode "cold" `
            -TrialIndex $trial))
    }

    foreach ($trial in @(1, 2)) {
        [void]$cases.Add((New-BaselineCase `
            -Id "soak-c16-cold-t$trial" `
            -Roles @("soak", "determinism_repeat") `
            -Citizens 16 `
            -Ticks 1000 `
            -CacheMode "cold" `
            -TrialIndex $trial))
    }

    [void]$cases.Add((New-BaselineCase `
        -Id "stress-c24-cold-t1" `
        -Roles @("stress") `
        -Citizens 24 `
        -Ticks 300 `
        -CacheMode "cold" `
        -TrialIndex 1))
    [void]$cases.Add((New-BaselineCase `
        -Id "forced-c16-t1" `
        -Roles @("forced_invalidation") `
        -Citizens 16 `
        -Ticks 1 `
        -CacheMode "forced_invalidation" `
        -TrialIndex 1))

    return @($cases)
}

function Get-EnvironmentFingerprint {
    param([Parameter(Mandatory = $true)][object]$Environment)

    $properties = @(
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
    return ($properties | ForEach-Object { "$_=$($Environment.$_)" }) -join "|"
}

function Get-BuildFingerprint {
    param([Parameter(Mandatory = $true)][string]$PairRoot)

    $releaseRoot = Join-Path $PairRoot "release-runner"
    if (-not (Test-Path -LiteralPath $releaseRoot -PathType Container)) {
        if ($releaseExport) {
            throw "Release bundle directory is missing: $releaseRoot"
        }
        return $null
    }

    $consoleWrapper = Join-Path $releaseRoot "SocietiesPerformance.console.exe"
    $pack = Join-Path $releaseRoot "SocietiesPerformance.pck"
    $managedAssembly = Get-ChildItem -LiteralPath $releaseRoot -Recurse -Filter "Societies.dll" -File |
        Select-Object -First 1
    Assert-Contract (Test-Path -LiteralPath $consoleWrapper -PathType Leaf) "Release console wrapper is missing: $consoleWrapper"
    Assert-Contract (Test-Path -LiteralPath $pack -PathType Leaf) "Release pack is missing: $pack"
    Assert-Contract ($null -ne $managedAssembly) "Exported Societies.dll is missing below $releaseRoot"

    return [pscustomobject][ordered]@{
        consoleWrapperSha256 = Get-Sha256 $consoleWrapper
        packSha256 = Get-Sha256 $pack
        managedAssemblySha256 = Get-Sha256 $managedAssembly.FullName
    }
}

function Get-CaseResult {
    param(
        [Parameter(Mandatory = $true)][object]$Case,
        [Parameter(Mandatory = $true)][string]$PairRoot,
        [Parameter(Mandatory = $true)][string]$ExpectedGitSha,
        [Parameter(Mandatory = $true)][bool]$ExpectedGitDirty
    )

    $equivalencePath = Join-Path $PairRoot "equivalence-results.json"
    $offResultPath = Join-Path $PairRoot "metrics-off\perf-results.json"
    $onResultPath = Join-Path $PairRoot "metrics-on\perf-results.json"
    $offManifestPath = Join-Path $PairRoot "metrics-off\validation-manifest.json"
    $onManifestPath = Join-Path $PairRoot "metrics-on\validation-manifest.json"
    $matrixPath = Join-Path $PairRoot "perf-matrix.csv"
    $summaryPath = Join-Path $PairRoot "pair-summary.txt"

    $equivalence = Read-JsonArtifact $equivalencePath "$($Case.id) equivalence result"
    $offResult = Read-JsonArtifact $offResultPath "$($Case.id) metrics-off result"
    $onResult = Read-JsonArtifact $onResultPath "$($Case.id) metrics-on result"
    $offManifest = Read-JsonArtifact $offManifestPath "$($Case.id) metrics-off manifest"
    $onManifest = Read-JsonArtifact $onManifestPath "$($Case.id) metrics-on manifest"
    Assert-Contract (Test-Path -LiteralPath $matrixPath -PathType Leaf) "$($Case.id) pair matrix is missing."
    Assert-Contract (Test-Path -LiteralPath $summaryPath -PathType Leaf) "$($Case.id) pair summary is missing."

    Assert-Contract ($equivalence.schemaVersion -eq 4) "$($Case.id) equivalence schema is not v4."
    Assert-Contract (@("pass", "pass_dirty_source") -contains $equivalence.status) "$($Case.id) equivalence status is '$($equivalence.status)'."
    Assert-Contract ($equivalence.contractStatus -eq $equivalence.status) "$($Case.id) equivalence contract status does not match its status."
    Assert-Contract ($equivalence.singleModeTransitionEvidence -eq $true) "$($Case.id) did not prove its single-mode transition."
    Assert-Contract ($equivalence.selectorMode -eq $SelectorMode) "$($Case.id) equivalence selector mode does not match the optimized baseline contract."
    Assert-Contract ($equivalence.cacheModeEvidence -eq $false) "$($Case.id) leaf pair made a cross-mode evidence claim."
    Assert-Contract ($equivalence.releaseBaselineEvidence -eq $false) "$($Case.id) leaf pair made a baseline claim."
    Assert-Contract ($equivalence.fullMatrixCaptured -eq $false) "$($Case.id) leaf pair made a full-matrix claim."
    Assert-Contract ($equivalence.medianOfThreeCaptured -eq $false) "$($Case.id) leaf pair made a median claim."
    Assert-Contract ($equivalence.targetOrSafetyClaimMade -eq $false) "$($Case.id) leaf pair made an aggregate budget claim."

    foreach ($result in @($offResult, $onResult)) {
        Assert-Contract ($result.schemaVersion -eq 4) "$($Case.id) leaf result schema is not v4."
        Assert-Contract ($result.assessmentScope -eq "single_run_indicator_not_median_of_three_gate") "$($Case.id) leaf assessment scope is not single-run-only."
        Assert-Contract ($result.configuration.scenarioId -eq $Case.scenarioId) "$($Case.id) scenario does not match the plan."
        Assert-Contract ($result.configuration.simulationSeed -eq $Case.simulationSeed) "$($Case.id) seed does not match the plan."
        Assert-Contract ($result.configuration.citizenCount -eq $Case.citizens) "$($Case.id) citizen count does not match the plan."
        Assert-Contract ($result.configuration.warmupTicks -eq $Case.preconditioningTicks) "$($Case.id) preconditioning ticks do not match the plan."
        Assert-Contract ($result.configuration.measuredTicks -eq $Case.measuredTicks) "$($Case.id) measured ticks do not match the plan."
        Assert-Contract ($result.configuration.cacheMode -eq $Case.cacheMode) "$($Case.id) cache mode does not match the plan."
        Assert-Contract ($result.configuration.selectorMode -eq $SelectorMode) "$($Case.id) selector mode does not match the optimized baseline contract."
        Assert-Contract ($result.configuration.trialIndex -eq $Case.trialIndex) "$($Case.id) trial index does not match the plan."
        Assert-Contract ($result.configuration.comparisonGroup -eq $Case.comparisonGroup) "$($Case.id) comparison group does not match the plan."
        Assert-Contract ($result.configuration.gitSha -eq $ExpectedGitSha) "$($Case.id) source commit does not match the matrix commit."
        Assert-Contract ($result.configuration.gitDirty -eq $ExpectedGitDirty) "$($Case.id) dirty-source identity does not match the matrix invocation."
        Assert-Contract (Test-Sha256 $result.hashes.snapshotSha256) "$($Case.id) snapshot hash is invalid."
        Assert-Contract (Test-Sha256 $result.hashes.eventLogSha256) "$($Case.id) event-log hash is invalid."
        Assert-Contract (Test-Sha256 $result.hashes.deterministicStateAndEventSha256) "$($Case.id) combined hash is invalid."

        foreach ($intervalName in @(
            "sceneSetupMilliseconds",
            "bootstrapMilliseconds",
            "warmupMilliseconds",
            "cachePreparationMilliseconds",
            "measuredTicksMilliseconds",
            "coreArtifactSerializationMilliseconds"
        )) {
            $intervalValue = $result.intervals.PSObject.Properties[$intervalName].Value
            Assert-Contract (Test-NonNegativeFiniteNumber $intervalValue) "$($Case.id) interval '$intervalName' is missing, negative, or non-finite."
        }

        Assert-Contract ($result.externalTickStatistics.count -eq $Case.measuredTicks) "$($Case.id) external sample count does not match measured ticks."
        foreach ($statisticName in @(
            "meanMilliseconds",
            "p50Milliseconds",
            "p95Milliseconds",
            "p99Milliseconds",
            "maximumMilliseconds",
            "totalMilliseconds"
        )) {
            $statisticValue = $result.externalTickStatistics.PSObject.Properties[$statisticName].Value
            Assert-Contract (Test-NonNegativeFiniteNumber $statisticValue) "$($Case.id) statistic '$statisticName' is missing, negative, or non-finite."
        }
        Assert-Contract (
            [double]$result.externalTickStatistics.p50Milliseconds -le [double]$result.externalTickStatistics.p95Milliseconds -and
            [double]$result.externalTickStatistics.p95Milliseconds -le [double]$result.externalTickStatistics.p99Milliseconds -and
            [double]$result.externalTickStatistics.p99Milliseconds -le [double]$result.externalTickStatistics.maximumMilliseconds -and
            [double]$result.externalTickStatistics.maximumMilliseconds -le [double]$result.externalTickStatistics.totalMilliseconds
        ) "$($Case.id) external statistics are not internally ordered."

        foreach ($componentName in @(
            "targetBootstrap",
            "targetP95",
            "targetP99",
            "targetMaximum",
            "targetTotal",
            "safetyBootstrap",
            "safetyP95",
            "safetyMaximum"
        )) {
            $component = $result.budget.PSObject.Properties[$componentName].Value
            Assert-Contract ($null -ne $component) "$($Case.id) budget component '$componentName' is missing."
            Assert-Contract (Test-NonNegativeFiniteNumber $component.actualMilliseconds) "$($Case.id) budget component '$componentName' has an invalid actual value."
            Assert-Contract (Test-NonNegativeFiniteNumber $component.limitMilliseconds) "$($Case.id) budget component '$componentName' has an invalid limit."
            Assert-Contract ($component.isApplied -is [bool] -and $component.passed -is [bool]) "$($Case.id) budget component '$componentName' has invalid boolean state."
        }

        $expectedBudgetProfile = if ($result.configuration.metricsEnabled) {
            "metricsDiagnostic"
        }
        elseif (-not $result.environment.verifiedReleaseExecution) {
            "debugCharacterization"
        }
        elseif ($Case.scenarioId -eq "balanced_basin" -and $Case.simulationSeed -eq 1337 -and $Case.citizens -eq 16 -and $Case.measuredTicks -eq 300) {
            "releaseReference300"
        }
        elseif ($Case.scenarioId -eq "balanced_basin" -and $Case.simulationSeed -eq 1337 -and $Case.citizens -eq 16 -and $Case.measuredTicks -eq 1000) {
            "releaseSoak1000"
        }
        elseif ($Case.scenarioId -eq "balanced_basin" -and $Case.simulationSeed -eq 1337 -and $Case.citizens -eq 24 -and $Case.measuredTicks -eq 300) {
            "stress24Characterization"
        }
        else {
            "characterization"
        }
        Assert-Contract ($result.budget.profile -eq $expectedBudgetProfile) "$($Case.id) budget profile '$($result.budget.profile)' does not match expected '$expectedBudgetProfile'."

        $snapshotPath = [string]$result.artifacts.snapshot
        $eventLogPath = [string]$result.artifacts.eventLog
        $actualSnapshotHash = Get-Sha256 $snapshotPath
        $actualEventLogHash = Get-Sha256 $eventLogPath
        $actualCombinedHash = Get-CombinedStateAndEventSha256 -SnapshotPath $snapshotPath -EventLogPath $eventLogPath
        Assert-Contract ($actualSnapshotHash -eq $result.hashes.snapshotSha256) "$($Case.id) snapshot bytes do not match the claimed hash."
        Assert-Contract ($actualEventLogHash -eq $result.hashes.eventLogSha256) "$($Case.id) event-log bytes do not match the claimed hash."
        Assert-Contract ($actualCombinedHash -eq $result.hashes.deterministicStateAndEventSha256) "$($Case.id) combined snapshot/event bytes do not match the claimed hash."
    }

    Assert-Contract ($offResult.configuration.metricsEnabled -eq $false) "$($Case.id) primary result has metrics enabled."
    Assert-Contract ($onResult.configuration.metricsEnabled -eq $true) "$($Case.id) diagnostic result has metrics disabled."
    Assert-Contract ($offResult.hashes.snapshotSha256 -eq $onResult.hashes.snapshotSha256) "$($Case.id) metrics modes changed the snapshot hash."
    Assert-Contract ($offResult.hashes.eventLogSha256 -eq $onResult.hashes.eventLogSha256) "$($Case.id) metrics modes changed the event-log hash."
    Assert-Contract ($offResult.hashes.deterministicStateAndEventSha256 -eq $onResult.hashes.deterministicStateAndEventSha256) "$($Case.id) metrics modes changed the combined hash."
    Assert-Contract ($offManifest.schemaVersion -eq 4 -and $onManifest.schemaVersion -eq 4) "$($Case.id) manifest schema is not v4."
    Assert-Contract ($offManifest.configuration.selectorMode -eq $SelectorMode -and $onManifest.configuration.selectorMode -eq $SelectorMode) "$($Case.id) manifest selector mode does not match the optimized baseline contract."
    Assert-Contract ($offManifest.gitSha -eq $ExpectedGitSha -and $onManifest.gitSha -eq $ExpectedGitSha) "$($Case.id) manifest source commit does not match."
    Assert-Contract ($offManifest.gitDirty -eq $ExpectedGitDirty -and $onManifest.gitDirty -eq $ExpectedGitDirty) "$($Case.id) manifest dirty-source identity does not match."
    Assert-Contract (
        $offManifest.hashes.snapshotSha256 -eq $offResult.hashes.snapshotSha256 -and
        $offManifest.hashes.eventLogSha256 -eq $offResult.hashes.eventLogSha256 -and
        $offManifest.hashes.deterministicStateAndEventSha256 -eq $offResult.hashes.deterministicStateAndEventSha256
    ) "$($Case.id) metrics-off manifest hashes do not match the leaf result."
    Assert-Contract (
        $onManifest.hashes.snapshotSha256 -eq $onResult.hashes.snapshotSha256 -and
        $onManifest.hashes.eventLogSha256 -eq $onResult.hashes.eventLogSha256 -and
        $onManifest.hashes.deterministicStateAndEventSha256 -eq $onResult.hashes.deterministicStateAndEventSha256
    ) "$($Case.id) metrics-on manifest hashes do not match the leaf result."
    Assert-Contract ($equivalence.metricsOffHash -eq $offResult.hashes.deterministicStateAndEventSha256) "$($Case.id) equivalence metrics-off hash does not match the leaf result."
    Assert-Contract ($equivalence.metricsOnHash -eq $onResult.hashes.deterministicStateAndEventSha256) "$($Case.id) equivalence metrics-on hash does not match the leaf result."
    Assert-Contract ($onResult.diagnostics.batchCount -eq $Case.measuredTicks) "$($Case.id) diagnostic batch count does not match measured ticks."
    Assert-Contract ($onResult.diagnostics.droppedBatchCount -eq 0) "$($Case.id) dropped diagnostic batches."

    if ($releaseExport) {
        Assert-Contract ($equivalence.releaseExport -eq $true) "$($Case.id) did not use the Release export route."
        Assert-Contract ($equivalence.releaseEnvironmentValid -eq $true) "$($Case.id) Release environment is invalid."
        Assert-Contract ($offResult.environment.verifiedReleaseExecution -eq $true -and $onResult.environment.verifiedReleaseExecution -eq $true) "$($Case.id) did not prove verified Release execution."
    }
    else {
        Assert-Contract ($equivalence.releaseBaselineEvidence -eq $false) "$($Case.id) debug characterization promoted baseline evidence."
    }

    $buildFingerprint = Get-BuildFingerprint $PairRoot
    return [pscustomobject][ordered]@{
        case = $Case
        pairRoot = $PairRoot
        equivalence = $equivalence
        offResult = $offResult
        onResult = $onResult
        offManifest = $offManifest
        onManifest = $onManifest
        matrixPath = $matrixPath
        summaryPath = $summaryPath
        equivalencePath = $equivalencePath
        offResultPath = $offResultPath
        onResultPath = $onResultPath
        offManifestPath = $offManifestPath
        onManifestPath = $onManifestPath
        environmentFingerprint = Get-EnvironmentFingerprint $offResult.environment
        buildFingerprint = $buildFingerprint
    }
}

function Get-CaseById {
    param(
        [Parameter(Mandatory = $true)][object[]]$Results,
        [Parameter(Mandatory = $true)][string]$Id
    )

    $matches = @($Results | Where-Object { $_.case.id -eq $Id })
    Assert-Contract ($matches.Count -eq 1) "Expected exactly one completed case '$Id', found $($matches.Count)."
    return $matches[0]
}

function Get-ArtifactInventory {
    param([Parameter(Mandatory = $true)][object[]]$Results)

    $paths = [System.Collections.Generic.Dictionary[string, string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($result in $Results) {
        $fixedArtifacts = [ordered]@{
            "$($result.case.id):equivalence" = $result.equivalencePath
            "$($result.case.id):metrics_off_result" = $result.offResultPath
            "$($result.case.id):metrics_on_result" = $result.onResultPath
            "$($result.case.id):metrics_off_manifest" = $result.offManifestPath
            "$($result.case.id):metrics_on_manifest" = $result.onManifestPath
            "$($result.case.id):pair_matrix" = $result.matrixPath
            "$($result.case.id):pair_summary" = $result.summaryPath
        }
        foreach ($entry in $fixedArtifacts.GetEnumerator()) {
            $fullPath = [System.IO.Path]::GetFullPath([string]$entry.Value)
            $paths[$fullPath] = [string]$entry.Key
        }

        foreach ($mode in @("offResult", "onResult")) {
            $leaf = $result.$mode
            foreach ($property in $leaf.artifacts.PSObject.Properties) {
                if ($property.Value -is [string] -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
                    $fullPath = [System.IO.Path]::GetFullPath([string]$property.Value)
                    $paths[$fullPath] = "$($result.case.id):$mode`_$($property.Name)"
                }
            }
        }

        if ($null -ne $result.buildFingerprint) {
            $releaseRoot = Join-Path $result.pairRoot "release-runner"
            foreach ($path in @(
                (Join-Path $releaseRoot "SocietiesPerformance.console.exe"),
                (Join-Path $releaseRoot "SocietiesPerformance.pck")
            )) {
                $fullPath = [System.IO.Path]::GetFullPath($path)
                $paths[$fullPath] = "$($result.case.id):release_bundle"
            }
            $assembly = Get-ChildItem -LiteralPath $releaseRoot -Recurse -Filter "Societies.dll" -File |
                Select-Object -First 1
            if ($null -ne $assembly) {
                $paths[$assembly.FullName] = "$($result.case.id):managed_assembly"
            }
        }
    }

    $inventory = New-Object System.Collections.ArrayList
    foreach ($path in @($paths.Keys | Sort-Object)) {
        Assert-Contract (Test-Path -LiteralPath $path -PathType Leaf) "Referenced artifact is missing: $path"
        [void]$inventory.Add([pscustomobject][ordered]@{
            role = $paths[$path]
            path = $path
            sha256 = Get-Sha256 $path
        })
    }
    return @($inventory)
}

function Get-PublicCaseSummary {
    param([Parameter(Mandatory = $true)][object]$Result)

    return [pscustomobject][ordered]@{
        id = $Result.case.id
        roles = @($Result.case.roles)
        citizens = $Result.case.citizens
        preconditioningTicks = $Result.case.preconditioningTicks
        measuredTicks = $Result.case.measuredTicks
        cacheMode = $Result.case.cacheMode
        selectorMode = $Result.offResult.configuration.selectorMode
        trialIndex = $Result.case.trialIndex
        comparisonGroup = $Result.case.comparisonGroup
        pairStatus = $Result.equivalence.status
        verifiedReleaseExecution = [bool]$Result.offResult.environment.verifiedReleaseExecution
        bootstrapMilliseconds = [double]$Result.offResult.intervals.bootstrapMilliseconds
        warmupMilliseconds = [double]$Result.offResult.intervals.warmupMilliseconds
        p50Milliseconds = [double]$Result.offResult.externalTickStatistics.p50Milliseconds
        p95Milliseconds = [double]$Result.offResult.externalTickStatistics.p95Milliseconds
        p99Milliseconds = [double]$Result.offResult.externalTickStatistics.p99Milliseconds
        maximumMilliseconds = [double]$Result.offResult.externalTickStatistics.maximumMilliseconds
        totalMilliseconds = [double]$Result.offResult.externalTickStatistics.totalMilliseconds
        budgetProfile = $Result.offResult.budget.profile
        singleRunTargetPassed = $Result.offResult.budget.targetPassed
        singleRunSafetyPassed = $Result.offResult.budget.safetyPassed
        deterministicHash = $Result.offResult.hashes.deterministicStateAndEventSha256
        pathPlanLookups = $Result.onResult.diagnostics.pathPlanLookups
        pathPlanCacheHits = $Result.onResult.diagnostics.pathPlanCacheHits
        pathPlanCacheMisses = $Result.onResult.diagnostics.pathPlanCacheMisses
        pathPlanCacheHitRate = $Result.onResult.diagnostics.pathPlanCacheHitRate
        pathPlanCacheSizeLast = $Result.onResult.diagnostics.pathPlanCacheSizeLast
        candidateOrdersPerIdleCitizen = $Result.onResult.diagnostics.candidateOrdersPerIdleCitizen
        navigationInvalidations = $Result.onResult.diagnostics.navigationInvalidations
        navigationRebuildMilliseconds = $Result.onResult.diagnostics.phases.navigationRebuildMilliseconds
        workOrdersGenerated = $Result.onResult.diagnostics.workOrdersGenerated
        workOrdersGeneratedUncapped = $Result.onResult.diagnostics.workOrdersGeneratedUncapped
        workOrdersClaimed = $Result.onResult.diagnostics.workOrdersClaimed
        workOrdersRemainingLast = $Result.onResult.diagnostics.workOrdersRemainingLast
        pairRoot = $Result.pairRoot
    }
}

$canonicalCases = @(Get-CanonicalCases)
$canonicalIds = @($canonicalCases | ForEach-Object { $_.id })
Assert-Contract ($canonicalCases.Count -eq 14) "The W1-03c canonical plan must contain exactly 14 pairs."
Assert-Contract (@($canonicalIds | Select-Object -Unique).Count -eq 14) "The W1-03c canonical plan contains duplicate case IDs."
Assert-Contract ($PreconditioningTicks -ge 1 -and $PreconditioningTicks -le 100000) "PreconditioningTicks must be between 1 and 100000."

$selectedCases = $canonicalCases
if ($null -ne $CaseId -and $CaseId.Count -gt 0) {
    $requestedIds = @($CaseId | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $unknownIds = @($requestedIds | Where-Object { $canonicalIds -notcontains $_ })
    Assert-Contract ($unknownIds.Count -eq 0) "Unknown CaseId value(s): $($unknownIds -join ', ')."
    Assert-Contract (@($requestedIds | Select-Object -Unique).Count -eq $requestedIds.Count) "CaseId contains duplicates."
    $selectedCases = @($canonicalCases | Where-Object { $requestedIds -contains $_.id })
}
Assert-Contract ($selectedCases.Count -gt 0) "At least one W1-03c case must be selected."

$canonicalRequest =
    $selectedCases.Count -eq $canonicalCases.Count -and
    $Scenario -eq "balanced_basin" -and
    $Seed -eq 1337 -and
    $PreconditioningTicks -eq 2

$timestamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss-fff")
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $suffix = if ($PlanOnly) { "plan" } elseif ($DebugCharacterization) { "debug" } else { "release" }
    $OutputRoot = Join-Path $repoRoot "artifacts\performance\w103c-$suffix-$timestamp"
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "W1-03c output already exists: $OutputRoot"
}
[System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null

$planPath = Join-Path $OutputRoot "run-plan.json"
$matrixPath = Join-Path $OutputRoot "perf-matrix.csv"
$resultsPath = Join-Path $OutputRoot "perf-results.json"
$manifestPath = Join-Path $OutputRoot "validation-manifest.json"
$summaryPath = Join-Path $OutputRoot "summary.txt"
$completedResults = New-Object System.Collections.ArrayList
$failedPhase = "initialization"

try {
    $gitSha = (git rev-parse HEAD).Trim()
    Assert-Contract ($LASTEXITCODE -eq 0 -and (Test-GitSha $gitSha)) "Could not resolve a full current git SHA."
    $gitDirty = Get-GitDirtyState
    if ($gitDirty -and -not $AllowDirtySource) {
        throw "W1-03c requires a clean committed source tree. Pass -AllowDirtySource only for non-baseline characterization."
    }

    $plan = [ordered]@{
        schemaVersion = 1
        capturedUtc = [DateTime]::UtcNow.ToString("o")
        scope = "V3-W1-03c_publication_grade_performance_baseline_matrix"
        exactInvocation = $exactInvocation
        source = [ordered]@{
            gitSha = $gitSha
            gitDirty = $gitDirty
        }
        configuration = [ordered]@{
            scenarioId = $Scenario
            simulationSeed = $Seed
            preconditioningTicks = $PreconditioningTicks
            selectorMode = $SelectorMode
            releaseExport = $releaseExport
            exportPreset = if ($releaseExport) { $ExportPreset } else { $null }
            planOnly = [bool]$PlanOnly
            caseFilterApplied = $selectedCases.Count -ne $canonicalCases.Count
            canonicalRequest = $canonicalRequest
        }
        canonicalPairCount = 14
        selectedPairCount = $selectedCases.Count
        cases = @($selectedCases)
        publicationRequirements = [ordered]@{
            cleanSource = $true
            verifiedReleaseExecution = $true
            exactCanonicalInventory = $true
            metricsOffPrimaryAndMetricsOnDiagnostic = $true
            coldWarmDeterministicEquivalence = $true
            referenceMedianTrials = 3
            soakRepeatTrials = 2
            consistentMachineAndBuild = $true
            optimizedSelectorMode = $true
        }
        claims = [ordered]@{
            releaseBaselineEvidence = $false
            fullMatrixCaptured = $false
            medianOfThreeCaptured = $false
            targetOrSafetyClaimMade = $false
        }
    }
    Write-JsonArtifact $planPath $plan

    if ($PlanOnly) {
        $planSummary = @(
            "Societies W1-03c performance baseline matrix",
            "Status: planned_only",
            "Canonical request: $canonicalRequest",
            "Selected pairs: $($selectedCases.Count) of 14",
            "Selector mode: $SelectorMode",
            "Release route: $releaseExport",
            "Baseline/full-matrix/median/target-safety claims: false",
            "Output: $OutputRoot"
        )
        Write-Utf8NoBom $summaryPath ($planSummary -join [System.Environment]::NewLine)
        $planManifest = [ordered]@{
            schemaVersion = 1
            capturedUtc = [DateTime]::UtcNow.ToString("o")
            status = "planned_only"
            scope = "V3-W1-03c_plan_validation_only"
            exactInvocation = $exactInvocation
            source = $plan.source
            claims = $plan.claims
            artifacts = @(
                [ordered]@{ role = "run_plan"; path = $planPath; sha256 = Get-Sha256 $planPath },
                [ordered]@{ role = "summary"; path = $summaryPath; sha256 = Get-Sha256 $summaryPath }
            )
        }
        Write-JsonArtifact $manifestPath $planManifest
        Write-Host "W1-03c plan validation completed. Output: $OutputRoot"
        return
    }

    foreach ($case in $selectedCases) {
        $failedPhase = "run_$($case.id)"
        $caseRoot = Join-Path $OutputRoot ("cases\" + $case.id)
        $pairArguments = @{
            Scenario = $case.scenarioId
            Seed = $case.simulationSeed
            Citizens = $case.citizens
            Ticks = $case.measuredTicks
            WarmupTicks = $case.preconditioningTicks
            CacheMode = $case.cacheMode
            SelectorMode = $SelectorMode
            ComparisonGroup = $case.comparisonGroup
            TrialIndex = $case.trialIndex
            OutputRoot = $caseRoot
            ExportPreset = $ExportPreset
            AllowPrimarySafetyFailure = $true
        }
        if (-not [string]::IsNullOrWhiteSpace($GodotPath)) {
            $pairArguments.GodotPath = $GodotPath
        }
        if ($releaseExport) {
            $pairArguments.ReleaseExport = $true
        }
        else {
            $pairArguments.AllowDebugReference = $true
        }
        if ($AllowDirtySource) {
            $pairArguments.AllowDirtySource = $true
        }

        Write-Host "[$($completedResults.Count + 1)/$($selectedCases.Count)] Running $($case.id)..."
        & $pairScript @pairArguments
        Assert-Contract ($LASTEXITCODE -eq 0) "Pair runner exited with code $LASTEXITCODE for $($case.id)."

        $failedPhase = "validate_$($case.id)"
        $caseResult = Get-CaseResult `
            -Case $case `
            -PairRoot $caseRoot `
            -ExpectedGitSha $gitSha `
            -ExpectedGitDirty $gitDirty
        [void]$completedResults.Add($caseResult)
    }

    $failedPhase = "aggregate_contracts"
    $resultArray = @($completedResults)
    Assert-Contract ($resultArray.Count -eq $selectedCases.Count) "Completed pair count does not match the selected plan."
    Assert-Contract (@($resultArray | ForEach-Object { $_.case.id } | Select-Object -Unique).Count -eq $resultArray.Count) "Completed results contain duplicate case IDs."

    $environmentFingerprints = @($resultArray | ForEach-Object { $_.environmentFingerprint } | Select-Object -Unique)
    $sameEnvironment = $environmentFingerprints.Count -eq 1
    Assert-Contract $sameEnvironment "Matrix pairs do not share one machine/runtime environment."

    $releaseBuildRows = @($resultArray | Where-Object { $null -ne $_.buildFingerprint })
    $buildFingerprints = @($releaseBuildRows | ForEach-Object {
        "$($_.buildFingerprint.consoleWrapperSha256)|$($_.buildFingerprint.packSha256)|$($_.buildFingerprint.managedAssemblySha256)"
    } | Select-Object -Unique)
    $sameReleaseBuild = -not $releaseExport -or (
        $releaseBuildRows.Count -eq $resultArray.Count -and
        $buildFingerprints.Count -eq 1)
    Assert-Contract $sameReleaseBuild "Matrix pairs were not executed from content-identical Release bundles."

    $sourceClean = -not $gitDirty
    $verifiedRelease =
        $releaseExport -and
        @($resultArray | Where-Object { $_.offResult.environment.verifiedReleaseExecution -ne $true -or $_.onResult.environment.verifiedReleaseExecution -ne $true }).Count -eq 0
    $canonicalInventoryCaptured = $canonicalRequest -and $resultArray.Count -eq 14
    $fullMatrixCaptured =
        $canonicalInventoryCaptured -and
        $sourceClean -and
        $verifiedRelease -and
        $sameEnvironment -and
        $sameReleaseBuild
    $coldWarmChecks = New-Object System.Collections.ArrayList
    if ($canonicalInventoryCaptured) {
        foreach ($citizens in @(3, 6, 12, 16)) {
            $cold = Get-CaseById -Results $resultArray -Id "matrix-c$citizens-cold-t1"
            $warm = Get-CaseById -Results $resultArray -Id "matrix-c$citizens-warm-t1"
            $match =
                $cold.offResult.hashes.snapshotSha256 -eq $warm.offResult.hashes.snapshotSha256 -and
                $cold.offResult.hashes.eventLogSha256 -eq $warm.offResult.hashes.eventLogSha256 -and
                $cold.offResult.hashes.deterministicStateAndEventSha256 -eq $warm.offResult.hashes.deterministicStateAndEventSha256
            [void]$coldWarmChecks.Add([pscustomobject][ordered]@{
                citizens = $citizens
                deterministicHashesMatch = $match
                coldHash = $cold.offResult.hashes.deterministicStateAndEventSha256
                naturalWarmHash = $warm.offResult.hashes.deterministicStateAndEventSha256
            })
        }
    }
    $coldWarmEquivalent = $canonicalInventoryCaptured -and @($coldWarmChecks | Where-Object { -not $_.deterministicHashesMatch }).Count -eq 0

    $referenceAssessment = $null
    $referenceTrialsComparable = $false
    $medianOfThreeCaptured = $false
    if ($canonicalInventoryCaptured) {
        $reference = @(
            (Get-CaseById -Results $resultArray -Id "matrix-c16-cold-t1"),
            (Get-CaseById -Results $resultArray -Id "reference-c16-cold-t2"),
            (Get-CaseById -Results $resultArray -Id "reference-c16-cold-t3")
        )
        $referenceHashes = @($reference | ForEach-Object { $_.offResult.hashes.deterministicStateAndEventSha256 } | Select-Object -Unique)
        $referenceEnvironments = @($reference | ForEach-Object { $_.environmentFingerprint } | Select-Object -Unique)
        $referenceTrialsComparable =
            $reference.Count -eq 3 -and
            $referenceHashes.Count -eq 1 -and
            $referenceEnvironments.Count -eq 1
        Assert-Contract $referenceTrialsComparable "The three 16-citizen reference trials are not comparable and deterministic."
        $medianOfThreeCaptured = $referenceTrialsComparable -and $fullMatrixCaptured

        $medianBootstrap = Get-Median @($reference | ForEach-Object { [double]$_.offResult.intervals.bootstrapMilliseconds })
        $medianP95 = Get-Median @($reference | ForEach-Object { [double]$_.offResult.externalTickStatistics.p95Milliseconds })
        $medianP99 = Get-Median @($reference | ForEach-Object { [double]$_.offResult.externalTickStatistics.p99Milliseconds })
        $medianMaximum = Get-Median @($reference | ForEach-Object { [double]$_.offResult.externalTickStatistics.maximumMilliseconds })
        $medianTotal = Get-Median @($reference | ForEach-Object { [double]$_.offResult.externalTickStatistics.totalMilliseconds })
        $referenceLimits = [ordered]@{
            targetBootstrapMilliseconds = 3000.0
            targetP95Milliseconds = 25.0
            targetP99Milliseconds = 50.0
            targetMaximumMilliseconds = 100.0
            targetTotalMilliseconds = 6000.0
            safetyBootstrapMilliseconds = 5000.0
            safetyP95Milliseconds = 50.0
            safetyMaximumMilliseconds = 250.0
        }
        $targetPassed = $null
        $safetyPassed = $null
        $assessmentScope = if ($releaseExport) {
            "nonpublication_release_characterization_not_a_budget_gate"
        }
        else {
            "debug_characterization_not_a_budget_gate"
        }
        if ($fullMatrixCaptured) {
            foreach ($trial in $reference) {
                $budget = $trial.offResult.budget
                Assert-Contract ($budget.profile -eq "releaseReference300") "Reference budget profile drifted from releaseReference300."
                Assert-Contract (
                    $budget.targetBootstrap.isApplied -eq $true -and [double]$budget.targetBootstrap.limitMilliseconds -eq $referenceLimits.targetBootstrapMilliseconds -and
                    $budget.targetP95.isApplied -eq $true -and [double]$budget.targetP95.limitMilliseconds -eq $referenceLimits.targetP95Milliseconds -and
                    $budget.targetP99.isApplied -eq $true -and [double]$budget.targetP99.limitMilliseconds -eq $referenceLimits.targetP99Milliseconds -and
                    $budget.targetMaximum.isApplied -eq $true -and [double]$budget.targetMaximum.limitMilliseconds -eq $referenceLimits.targetMaximumMilliseconds -and
                    $budget.targetTotal.isApplied -eq $true -and [double]$budget.targetTotal.limitMilliseconds -eq $referenceLimits.targetTotalMilliseconds -and
                    $budget.safetyBootstrap.isApplied -eq $true -and [double]$budget.safetyBootstrap.limitMilliseconds -eq $referenceLimits.safetyBootstrapMilliseconds -and
                    $budget.safetyP95.isApplied -eq $true -and [double]$budget.safetyP95.limitMilliseconds -eq $referenceLimits.safetyP95Milliseconds -and
                    $budget.safetyMaximum.isApplied -eq $true -and [double]$budget.safetyMaximum.limitMilliseconds -eq $referenceLimits.safetyMaximumMilliseconds
                ) "Reference budget limits drifted from the W1-03 contract."
            }
            $targetPassed =
                $medianBootstrap -le $referenceLimits.targetBootstrapMilliseconds -and
                $medianP95 -le $referenceLimits.targetP95Milliseconds -and
                $medianP99 -le $referenceLimits.targetP99Milliseconds -and
                $medianMaximum -le $referenceLimits.targetMaximumMilliseconds -and
                $medianTotal -le $referenceLimits.targetTotalMilliseconds
            $safetyPassed = @($reference | Where-Object {
                [double]$_.offResult.intervals.bootstrapMilliseconds -gt $referenceLimits.safetyBootstrapMilliseconds -or
                [double]$_.offResult.externalTickStatistics.p95Milliseconds -gt $referenceLimits.safetyP95Milliseconds -or
                [double]$_.offResult.externalTickStatistics.maximumMilliseconds -gt $referenceLimits.safetyMaximumMilliseconds
            }).Count -eq 0
            $assessmentScope = "clean_verified_release_median_of_three_gate"
        }
        $referenceAssessment = [ordered]@{
            trialCount = 3
            deterministicHash = $referenceHashes[0]
            assessmentScope = $assessmentScope
            medians = [ordered]@{
                bootstrapMilliseconds = $medianBootstrap
                p95Milliseconds = $medianP95
                p99Milliseconds = $medianP99
                maximumMilliseconds = $medianMaximum
                totalMilliseconds = $medianTotal
            }
            limits = $referenceLimits
            targetPassed = $targetPassed
            safetyPassed = $safetyPassed
        }
    }

    $soakAssessment = $null
    $stressAssessment = $null
    $forcedAssessment = $null
    $warmAssessments = New-Object System.Collections.ArrayList
    $productionWarmupDecision = $null
    if ($canonicalInventoryCaptured) {
        $soaks = @(
            (Get-CaseById -Results $resultArray -Id "soak-c16-cold-t1"),
            (Get-CaseById -Results $resultArray -Id "soak-c16-cold-t2")
        )
        $soakHashes = @($soaks | ForEach-Object { $_.offResult.hashes.deterministicStateAndEventSha256 } | Select-Object -Unique)
        $soakDeterministic = $soakHashes.Count -eq 1
        $soakDiagnosticsComplete = @($soaks | Where-Object {
            $_.onResult.diagnostics.batchCount -ne 1000 -or $_.onResult.diagnostics.droppedBatchCount -ne 0
        }).Count -eq 0
        $soakLimits = [ordered]@{
            targetBootstrapMilliseconds = 3000.0
            targetP95Milliseconds = 25.0
            safetyBootstrapMilliseconds = 5000.0
            safetyP95Milliseconds = 50.0
            safetyMaximumMilliseconds = 250.0
        }
        $soakTargetPassed = $null
        $soakSafetyPassed = $null
        $soakAssessmentScope = if ($releaseExport) {
            "nonpublication_release_characterization_not_a_budget_gate"
        }
        else {
            "debug_characterization_not_a_budget_gate"
        }
        if ($fullMatrixCaptured) {
            foreach ($soak in $soaks) {
                $budget = $soak.offResult.budget
                Assert-Contract ($budget.profile -eq "releaseSoak1000") "Soak budget profile drifted from releaseSoak1000."
                Assert-Contract (
                    $budget.targetBootstrap.isApplied -eq $true -and [double]$budget.targetBootstrap.limitMilliseconds -eq $soakLimits.targetBootstrapMilliseconds -and
                    $budget.targetP95.isApplied -eq $true -and [double]$budget.targetP95.limitMilliseconds -eq $soakLimits.targetP95Milliseconds -and
                    $budget.safetyBootstrap.isApplied -eq $true -and [double]$budget.safetyBootstrap.limitMilliseconds -eq $soakLimits.safetyBootstrapMilliseconds -and
                    $budget.safetyP95.isApplied -eq $true -and [double]$budget.safetyP95.limitMilliseconds -eq $soakLimits.safetyP95Milliseconds -and
                    $budget.safetyMaximum.isApplied -eq $true -and [double]$budget.safetyMaximum.limitMilliseconds -eq $soakLimits.safetyMaximumMilliseconds
                ) "Soak budget limits drifted from the W1-03 contract."
            }
            $soakTargetPassed = @($soaks | Where-Object {
                [double]$_.offResult.intervals.bootstrapMilliseconds -gt $soakLimits.targetBootstrapMilliseconds -or
                [double]$_.offResult.externalTickStatistics.p95Milliseconds -gt $soakLimits.targetP95Milliseconds
            }).Count -eq 0
            $soakSafetyPassed = @($soaks | Where-Object {
                [double]$_.offResult.intervals.bootstrapMilliseconds -gt $soakLimits.safetyBootstrapMilliseconds -or
                [double]$_.offResult.externalTickStatistics.p95Milliseconds -gt $soakLimits.safetyP95Milliseconds -or
                [double]$_.offResult.externalTickStatistics.maximumMilliseconds -gt $soakLimits.safetyMaximumMilliseconds
            }).Count -eq 0
            $soakAssessmentScope = "clean_verified_release_repeat_gate"
        }
        $soakAssessment = [ordered]@{
            trialCount = 2
            deterministicRepeat = $soakDeterministic
            deterministicHash = if ($soakDeterministic) { $soakHashes[0] } else { $null }
            diagnosticsComplete = $soakDiagnosticsComplete
            assessmentScope = $soakAssessmentScope
            limits = $soakLimits
            targetPassed = $soakTargetPassed
            safetyPassed = $soakSafetyPassed
            p95Milliseconds = @($soaks | ForEach-Object { $_.offResult.externalTickStatistics.p95Milliseconds })
            maximumMilliseconds = @($soaks | ForEach-Object { $_.offResult.externalTickStatistics.maximumMilliseconds })
        }
        Assert-Contract ($soakDeterministic -and $soakDiagnosticsComplete) "The 1,000-tick soak did not prove deterministic, complete repeated execution."

        $stress = Get-CaseById -Results $resultArray -Id "stress-c24-cold-t1"
        $stressTargetPassed = $null
        $stressAssessmentScope = if ($releaseExport) {
            "nonpublication_release_characterization_not_a_budget_gate"
        }
        else {
            "debug_characterization_not_a_budget_gate"
        }
        if ($fullMatrixCaptured) {
            $stressBudget = $stress.offResult.budget
            Assert-Contract ($stressBudget.profile -eq "stress24Characterization") "Stress budget profile drifted from stress24Characterization."
            Assert-Contract (
                $stressBudget.targetP95.isApplied -eq $true -and [double]$stressBudget.targetP95.limitMilliseconds -eq 50.0 -and
                $stressBudget.targetMaximum.isApplied -eq $true -and [double]$stressBudget.targetMaximum.limitMilliseconds -eq 200.0 -and
                $stressBudget.hasSafetyGate -eq $false
            ) "Stress budget limits drifted from the W1-03 contract."
            $stressTargetPassed =
                [double]$stress.offResult.externalTickStatistics.p95Milliseconds -le 50.0 -and
                [double]$stress.offResult.externalTickStatistics.maximumMilliseconds -le 200.0
            $stressAssessmentScope = "clean_verified_release_characterization_target"
        }
        $stressAssessment = [ordered]@{
            classification = "characterization"
            assessmentScope = $stressAssessmentScope
            p95Milliseconds = $stress.offResult.externalTickStatistics.p95Milliseconds
            maximumMilliseconds = $stress.offResult.externalTickStatistics.maximumMilliseconds
            targetP95Milliseconds = 50.0
            targetMaximumMilliseconds = 200.0
            targetPassed = $stressTargetPassed
            safetyGateApplied = $false
        }

        $forced = Get-CaseById -Results $resultArray -Id "forced-c16-t1"
        $probe = $forced.offResult.cacheEvidence.forcedInvalidation.probe
        $forcedCorrect =
            $forced.equivalence.singleModeTransitionEvidence -eq $true -and
            $probe.prepared -eq $true -and
            $probe.committed -eq $true -and
            $probe.firstPostChangeLookupObserved -eq $true -and
            $probe.firstPostChangeLookupWasCacheMiss -eq $true -and
            $probe.firstPostChangeLookupUsedNewVersion -eq $true -and
            $probe.exactEndpointsMatch -eq $true -and
            $probe.changedCellIncludedInPostChangePlan -eq $true
        Assert-Contract (Test-NonNegativeFiniteNumber $probe.commitToFirstLookupMilliseconds) "Forced invalidation timing is missing, negative, or non-finite."
        $forcedMilliseconds = [double]$probe.commitToFirstLookupMilliseconds
        $forcedAssessmentScope = if ($fullMatrixCaptured) {
            "clean_verified_release_threshold_gate"
        }
        elseif ($releaseExport) {
            "nonpublication_release_characterization_not_a_budget_gate"
        }
        else {
            "debug_characterization_not_a_budget_gate"
        }
        $forcedAssessment = [ordered]@{
            transitionCorrect = $forcedCorrect
            assessmentScope = $forcedAssessmentScope
            commitToFirstCorrectLookupMilliseconds = $forcedMilliseconds
            targetLimitMilliseconds = 150.0
            safetyLimitMilliseconds = 250.0
            targetPassed = if ($fullMatrixCaptured) { $forcedCorrect -and $forcedMilliseconds -le 150.0 } else { $null }
            safetyPassed = if ($fullMatrixCaptured) { $forcedCorrect -and $forcedMilliseconds -le 250.0 } else { $null }
        }
        Assert-Contract $forcedCorrect "The forced invalidation case did not prove the correct first post-change lookup."

        foreach ($citizens in @(3, 6, 12, 16)) {
            $warm = Get-CaseById -Results $resultArray -Id "matrix-c$citizens-warm-t1"
            $hitRate = $warm.onResult.diagnostics.pathPlanCacheHitRate
            Assert-Contract (
                (Test-NonNegativeFiniteNumber $hitRate) -and [double]$hitRate -le 1.0
            ) "Natural-warm cache hit rate is missing or outside [0, 1] for $citizens citizens."
            $classification = if ($null -eq $hitRate) {
                "unavailable"
            }
            elseif ([double]$hitRate -ge 0.99) {
                "target_met"
            }
            elseif ([double]$hitRate -lt 0.98) {
                "investigate"
            }
            else {
                "acceptable_below_target"
            }
            [void]$warmAssessments.Add([pscustomobject][ordered]@{
                citizens = $citizens
                hitRate = $hitRate
                target = 0.99
                investigationThreshold = 0.98
                classification = $classification
            })
        }
        $warmNeedsInvestigation = @($warmAssessments | Where-Object { $_.classification -in @("investigate", "unavailable") }).Count -gt 0
        $productionWarmupDecision = [ordered]@{
            decision = if ($warmNeedsInvestigation) { "benchmark_only_investigate_natural_warm_cache" } else { "benchmark_only" }
            eagerAllPairsProductionWarmupRequired = $false
            rationale = if ($warmNeedsInvestigation) {
                "Natural warm-cache diagnostics need investigation; the endpoint-sensitive cache contract still makes eager all-pairs prefill unsuitable for production."
            }
            else {
                "Natural warm-cache behavior is sufficient without production eager/all-pairs prefill, and deterministic results match cold-cache execution."
            }
        }
    }

    $releaseBaselineEvidence =
        $fullMatrixCaptured -and
        $medianOfThreeCaptured -and
        $coldWarmEquivalent -and
        $sameEnvironment -and
        $sameReleaseBuild -and
        $soakAssessment.deterministicRepeat -eq $true -and
        $forcedAssessment.transitionCorrect -eq $true
    $targetOrSafetyClaimMade = $releaseBaselineEvidence

    $failedPhase = "aggregate_matrix"
    $matrixRows = New-Object System.Collections.ArrayList
    foreach ($result in $resultArray) {
        $rows = @(Import-Csv -LiteralPath $result.matrixPath)
        Assert-Contract ($rows.Count -eq 2) "$($result.case.id) pair matrix must contain exactly metrics-off and metrics-on rows."
        foreach ($row in $rows) {
            $values = [ordered]@{
                case_id = $result.case.id
                case_roles = ($result.case.roles -join ";")
            }
            foreach ($property in $row.PSObject.Properties) {
                $values[$property.Name] = $property.Value
            }
            [void]$matrixRows.Add([pscustomobject]$values)
        }
    }
    $matrixCsv = @($matrixRows | ConvertTo-Csv -NoTypeInformation)
    Write-Utf8NoBom $matrixPath ($matrixCsv -join [System.Environment]::NewLine)

    $artifactInventory = Get-ArtifactInventory -Results $resultArray
    $budgetStatus = if (-not $releaseBaselineEvidence) {
        "not_claimed"
    }
    elseif ($referenceAssessment.safetyPassed -ne $true -or $soakAssessment.safetyPassed -ne $true -or $forcedAssessment.safetyPassed -ne $true) {
        "safety_failure"
    }
    elseif ($referenceAssessment.targetPassed -ne $true -or $soakAssessment.targetPassed -ne $true -or $forcedAssessment.targetPassed -ne $true -or $stressAssessment.targetPassed -ne $true) {
        "target_missed"
    }
    else {
        "target_passed"
    }

    $aggregate = [ordered]@{
        schemaVersion = 1
        sourceResultSchemaVersion = 4
        capturedUtc = [DateTime]::UtcNow.ToString("o")
        status = "pass"
        contractStatus = "pass"
        budgetStatus = $budgetStatus
        scope = "V3-W1-03c_publication_grade_performance_baseline_matrix"
        exactInvocation = $exactInvocation
        source = [ordered]@{
            gitSha = $gitSha
            gitDirty = $gitDirty
            sourceClean = $sourceClean
        }
        configuration = $plan.configuration
        claims = [ordered]@{
            releaseBaselineEvidence = $releaseBaselineEvidence
            fullMatrixCaptured = $fullMatrixCaptured
            medianOfThreeCaptured = $medianOfThreeCaptured
            targetOrSafetyClaimMade = $targetOrSafetyClaimMade
        }
        contracts = [ordered]@{
            selectedPairCount = $selectedCases.Count
            canonicalPairCount = 14
            exactCanonicalInventory = $canonicalInventoryCaptured
            sameEnvironment = $sameEnvironment
            sameReleaseBuild = $sameReleaseBuild
            verifiedReleaseExecution = $verifiedRelease
            coldWarmDeterministicEquivalence = $coldWarmEquivalent
            referenceTrialsComparable = $referenceTrialsComparable
            soakDeterministicRepeat = if ($null -eq $soakAssessment) { $false } else { $soakAssessment.deterministicRepeat }
            forcedInvalidationCorrect = if ($null -eq $forcedAssessment) { $false } else { $forcedAssessment.transitionCorrect }
        }
        environment = if ($resultArray.Count -gt 0) { $resultArray[0].offResult.environment } else { $null }
        releaseBuildFingerprint = if ($buildFingerprints.Count -eq 1) { $resultArray[0].buildFingerprint } else { $null }
        coldWarmComparisons = @($coldWarmChecks)
        referenceAssessment = $referenceAssessment
        soakAssessment = $soakAssessment
        stressAssessment = $stressAssessment
        forcedInvalidationAssessment = $forcedAssessment
        naturalWarmAssessments = @($warmAssessments)
        productionWarmupDecision = $productionWarmupDecision
        cases = @($resultArray | ForEach-Object { Get-PublicCaseSummary $_ })
        artifactInventory = $artifactInventory
        artifacts = [ordered]@{
            runPlan = $planPath
            performanceMatrix = $matrixPath
            performanceResults = $resultsPath
            validationManifest = $manifestPath
            summary = $summaryPath
        }
        failedChecks = @()
        error = $null
    }
    Write-JsonArtifact $resultsPath $aggregate

    $summaryLines = @(
        "Societies W1-03c performance baseline matrix",
        "Status: pass",
        "Contract: pass; budget: $budgetStatus",
        "Source: $gitSha; clean: $sourceClean",
        "Selector mode: $SelectorMode",
        "Pairs: $($resultArray.Count) of 14; verified Release: $verifiedRelease",
        "Full matrix: $fullMatrixCaptured; median of three: $medianOfThreeCaptured; release baseline evidence: $releaseBaselineEvidence",
        "Cold/warm deterministic equivalence: $coldWarmEquivalent",
        "Reference target: $(if ($null -eq $referenceAssessment) { 'not claimed' } else { $referenceAssessment.targetPassed }); safety: $(if ($null -eq $referenceAssessment) { 'not claimed' } else { $referenceAssessment.safetyPassed })",
        "Soak deterministic: $(if ($null -eq $soakAssessment) { 'not claimed' } else { $soakAssessment.deterministicRepeat }); safety: $(if ($null -eq $soakAssessment) { 'not claimed' } else { $soakAssessment.safetyPassed })",
        "Forced invalidation target: $(if ($null -eq $forcedAssessment) { 'not claimed' } else { $forcedAssessment.targetPassed }); safety: $(if ($null -eq $forcedAssessment) { 'not claimed' } else { $forcedAssessment.safetyPassed })",
        "Production warmup: $(if ($null -eq $productionWarmupDecision) { 'not decided' } else { $productionWarmupDecision.decision })",
        "Output: $OutputRoot"
    )
    Write-Utf8NoBom $summaryPath ($summaryLines -join [System.Environment]::NewLine)

    $manifestArtifacts = New-Object System.Collections.ArrayList
    foreach ($entry in @(
        [ordered]@{ role = "run_plan"; path = $planPath },
        [ordered]@{ role = "performance_matrix"; path = $matrixPath },
        [ordered]@{ role = "performance_results"; path = $resultsPath },
        [ordered]@{ role = "summary"; path = $summaryPath }
    )) {
        [void]$manifestArtifacts.Add([ordered]@{
            role = $entry.role
            path = $entry.path
            sha256 = Get-Sha256 $entry.path
        })
    }
    foreach ($artifact in $artifactInventory) {
        [void]$manifestArtifacts.Add($artifact)
    }

    $manifest = [ordered]@{
        schemaVersion = 1
        sourceResultSchemaVersion = 4
        capturedUtc = [DateTime]::UtcNow.ToString("o")
        status = "pass"
        contractStatus = "pass"
        budgetStatus = $budgetStatus
        scope = $aggregate.scope
        exactInvocation = $exactInvocation
        source = $aggregate.source
        configuration = $aggregate.configuration
        claims = $aggregate.claims
        contracts = $aggregate.contracts
        environment = $aggregate.environment
        releaseBuildFingerprint = $aggregate.releaseBuildFingerprint
        assessments = [ordered]@{
            reference = $referenceAssessment
            soak = $soakAssessment
            stress = $stressAssessment
            forcedInvalidation = $forcedAssessment
            naturalWarm = @($warmAssessments)
            productionWarmupDecision = $productionWarmupDecision
        }
        artifacts = @($manifestArtifacts)
        failedChecks = @()
        error = $null
    }
    Write-JsonArtifact $manifestPath $manifest

    Write-Host "W1-03c matrix completed. Contract: pass; budget: $budgetStatus."
    Write-Host "Output: $OutputRoot"
}
catch {
    $failureMessage = $_.Exception.Message
    try {
        $failureClaims = [ordered]@{
            releaseBaselineEvidence = $false
            fullMatrixCaptured = $false
            medianOfThreeCaptured = $false
            targetOrSafetyClaimMade = $false
        }
        $failureSummary = @(
            "Societies W1-03c performance baseline matrix",
            "Status: fail",
            "Failed phase: $failedPhase",
            "Error: $failureMessage",
            "Completed pairs: $($completedResults.Count) of $($selectedCases.Count)",
            "Baseline/full-matrix/median/target-safety claims: false",
            "Output: $OutputRoot"
        )
        Write-Utf8NoBom $summaryPath ($failureSummary -join [System.Environment]::NewLine)
        $failure = [ordered]@{
            schemaVersion = 1
            sourceResultSchemaVersion = 4
            capturedUtc = [DateTime]::UtcNow.ToString("o")
            status = "fail"
            contractStatus = "fail"
            budgetStatus = "not_claimed"
            scope = "V3-W1-03c_publication_grade_performance_baseline_matrix"
            exactInvocation = $exactInvocation
            claims = $failureClaims
            completedCaseIds = @($completedResults | ForEach-Object { $_.case.id })
            selectedCaseIds = @($selectedCases | ForEach-Object { $_.id })
            failedChecks = @($failedPhase)
            error = $failureMessage
            artifacts = @(
                [ordered]@{ role = "run_plan"; path = $planPath; sha256 = if (Test-Path -LiteralPath $planPath -PathType Leaf) { Get-Sha256 $planPath } else { $null } },
                [ordered]@{ role = "summary"; path = $summaryPath; sha256 = Get-Sha256 $summaryPath }
            )
        }
        Write-JsonArtifact $resultsPath $failure
        Write-JsonArtifact $manifestPath $failure
    }
    catch {
        Write-Error "Could not write complete W1-03c failure artifacts: $($_.Exception.Message)"
    }

    throw
}
