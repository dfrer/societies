[CmdletBinding()]
param(
    [string]$Scenario = "balanced_basin",
    [int]$Seed = 1337,
    [int]$Citizens = 16,
    [int]$Ticks = 300,
    [int]$WarmupTicks = 0,
    [string]$OutputRoot,
    [string]$GodotPath,
    [switch]$SkipBuild,
    [switch]$AllowPrimarySafetyFailure,
    [switch]$AllowDirtySource,
    [switch]$AllowDebugReference,
    [switch]$RequireRelease
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Resolve-Godot {
    if ($GodotPath) {
        return (Resolve-Path -LiteralPath $GodotPath).Path
    }

    if ($env:GODOT_BIN) {
        return (Resolve-Path -LiteralPath $env:GODOT_BIN).Path
    }

    $godotCommand = Get-Command godot -ErrorAction SilentlyContinue
    if ($godotCommand) {
        return $godotCommand.Source
    }

    $wingetRoot = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
    if (Test-Path -LiteralPath $wingetRoot) {
        $candidate = Get-ChildItem -LiteralPath $wingetRoot -Recurse -Filter "Godot*_console.exe" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1

        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw "Godot .NET executable not found. Set GODOT_BIN, pass -GodotPath, or install Godot 4 Mono."
}

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

function Test-NonEmptyText {
    param([object]$Value)

    return $null -ne $Value -and -not [string]::IsNullOrWhiteSpace([string]$Value)
}

function Test-Sha256 {
    param([object]$Value)

    return (Test-NonEmptyText $Value) -and ([string]$Value -match '^[0-9a-fA-F]{64}$')
}

function Test-GitObjectId {
    param([object]$Value)

    return (Test-NonEmptyText $Value) -and ([string]$Value -match '^(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})$')
}

function Invoke-PerformanceRun {
    param(
        [Parameter(Mandatory = $true)]
        [string]$MetricsMode,
        [Parameter(Mandatory = $true)]
        [string]$RunId,
        [Parameter(Mandatory = $true)]
        [string]$RunOutputDirectory,
        [Parameter(Mandatory = $true)]
        [bool]$AllowSafetyFailure
    )

    $allowSafetyText = $AllowSafetyFailure.ToString().ToLowerInvariant()
    $gitDirtyText = $script:gitDirty.ToString().ToLowerInvariant()

    & $script:godot `
        --headless `
        --path "src/societies" `
        "res://tests/PerfRunner.tscn" `
        -- `
        --scenario $Scenario `
        --seed $Seed `
        --citizens $Citizens `
        --ticks $Ticks `
        --warmup-ticks $WarmupTicks `
        --metrics $MetricsMode `
        --output-dir $RunOutputDirectory `
        --run-id $RunId `
        --git-sha $script:gitSha `
        --git-dirty $gitDirtyText `
        --allow-safety-failure $allowSafetyText

    if ($LASTEXITCODE -ne 0) {
        throw "Performance runner '$RunId' exited with code $LASTEXITCODE."
    }
}

if ($Citizens -lt 1 -or $Citizens -gt 256) {
    throw "Citizens must be between 1 and 256."
}
if ($Ticks -lt 1 -or $Ticks -gt 4096) {
    throw "Ticks must be between 1 and 4096."
}
if ($WarmupTicks -lt 0) {
    throw "WarmupTicks cannot be negative."
}

$gitSha = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Could not resolve the current git SHA."
}
$dirtyLines = @(git status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw "Could not read git working-tree status."
}
$gitDirty = $dirtyLines.Count -gt 0
if ($gitDirty -and -not $AllowDirtySource) {
    throw "The performance pair requires a clean committed source tree. Commit or stash changes, or pass -AllowDirtySource for non-baseline smoke evidence."
}

$releaseReferenceRequested =
    $Scenario -eq "balanced_basin" -and
    $Seed -eq 1337 -and
    $Citizens -eq 16 -and
    ($Ticks -eq 300 -or $Ticks -eq 1000)
if ($releaseReferenceRequested -and -not $RequireRelease -and -not $AllowDebugReference) {
    throw "The fixed 16-citizen reference must be run through a verified Release path. Pass -RequireRelease once that path is available, or -AllowDebugReference for explicitly non-baseline characterization."
}

$godot = Resolve-Godot

$timestamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss-fff")
$safeScenario = $Scenario -replace '[^A-Za-z0-9._-]', '-'
if ([string]::IsNullOrWhiteSpace($safeScenario)) {
    $safeScenario = "scenario"
}
if ($safeScenario.Length -gt 32) {
    $safeScenario = $safeScenario.Substring(0, 32)
}
$nonce = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$runDescriptor = "$timestamp-$safeScenario-seed$Seed-c$Citizens-t$Ticks-w$WarmupTicks-$nonce"
if (-not $OutputRoot) {
    $OutputRoot = Join-Path $repoRoot "artifacts\performance\$runDescriptor"
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Performance pair output already exists: $OutputRoot"
}

if (-not $SkipBuild) {
    & $godot --headless --path "src/societies" --build-solutions --quit
    if ($LASTEXITCODE -ne 0) {
        throw "Godot solution build exited with code $LASTEXITCODE."
    }
}

[System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
$offRunId = "$runDescriptor-off"
$onRunId = "$runDescriptor-on"
$offDirectory = Join-Path $OutputRoot "metrics-off"
$onDirectory = Join-Path $OutputRoot "metrics-on"

Invoke-PerformanceRun `
    -MetricsMode "off" `
    -RunId $offRunId `
    -RunOutputDirectory $offDirectory `
    -AllowSafetyFailure ([bool]$AllowPrimarySafetyFailure)
Invoke-PerformanceRun `
    -MetricsMode "on" `
    -RunId $onRunId `
    -RunOutputDirectory $onDirectory `
    -AllowSafetyFailure $true

$offResultPath = Join-Path $offDirectory "perf-results.json"
$onResultPath = Join-Path $onDirectory "perf-results.json"
$offResult = Get-Content -Raw -LiteralPath $offResultPath | ConvertFrom-Json
$onResult = Get-Content -Raw -LiteralPath $onResultPath | ConvertFrom-Json

$schemaValid =
    $offResult.schemaVersion -eq 1 -and
    $onResult.schemaVersion -eq 1
$configurationMatches =
    (Test-NonEmptyText $offResult.configuration.scenarioId) -and
    (Test-NonEmptyText $offResult.configuration.measurementMode) -and
    (Test-NonEmptyText $offResult.configuration.warmupMode) -and
    $offResult.configuration.scenarioId -eq $onResult.configuration.scenarioId -and
    $offResult.configuration.simulationSeed -eq $onResult.configuration.simulationSeed -and
    $offResult.configuration.citizenCount -eq $onResult.configuration.citizenCount -and
    $offResult.configuration.warmupTicks -eq $onResult.configuration.warmupTicks -and
    $offResult.configuration.measuredTicks -eq $onResult.configuration.measuredTicks -and
    $offResult.configuration.measurementMode -eq $onResult.configuration.measurementMode -and
    $offResult.configuration.warmupMode -eq $onResult.configuration.warmupMode -and
    $offResult.configuration.cacheWarmupEnabled -eq $onResult.configuration.cacheWarmupEnabled
$commandConfigurationMatches =
    $offResult.configuration.scenarioId -eq $Scenario -and
    $offResult.configuration.simulationSeed -eq $Seed -and
    $offResult.configuration.citizenCount -eq $Citizens -and
    $offResult.configuration.warmupTicks -eq $WarmupTicks -and
    $offResult.configuration.measuredTicks -eq $Ticks
$modeContractValid =
    $offResult.configuration.metricsEnabled -eq $false -and
    $onResult.configuration.metricsEnabled -eq $true -and
    $offResult.configuration.measurementMode -eq "one_manual_step_per_external_sample" -and
    $offResult.configuration.cacheWarmupEnabled -eq $false
$gitIdentityMatches =
    (Test-GitObjectId $offResult.configuration.gitSha) -and
    $offResult.configuration.gitSha -eq $onResult.configuration.gitSha -and
    $offResult.configuration.gitSha -eq $gitSha -and
    $offResult.configuration.gitDirty -eq $onResult.configuration.gitDirty -and
    $offResult.configuration.gitDirty -eq $gitDirty
$environmentMatches =
    (Test-NonEmptyText $offResult.environment.machineName) -and
    (Test-NonEmptyText $offResult.environment.operatingSystem) -and
    (Test-NonEmptyText $offResult.environment.processArchitecture) -and
    (Test-NonEmptyText $offResult.environment.dotnetRuntime) -and
    (Test-NonEmptyText $offResult.environment.godotVersion) -and
    (Test-NonEmptyText $offResult.environment.managedBuildConfiguration) -and
    $offResult.environment.machineName -eq $onResult.environment.machineName -and
    $offResult.environment.logicalProcessorCount -eq $onResult.environment.logicalProcessorCount -and
    $offResult.environment.operatingSystem -eq $onResult.environment.operatingSystem -and
    $offResult.environment.processArchitecture -eq $onResult.environment.processArchitecture -and
    $offResult.environment.dotnetRuntime -eq $onResult.environment.dotnetRuntime -and
    $offResult.environment.godotVersion -eq $onResult.environment.godotVersion -and
    $offResult.environment.managedBuildConfiguration -eq $onResult.environment.managedBuildConfiguration -and
    $offResult.environment.godotDebugBuild -eq $onResult.environment.godotDebugBuild
$releaseEnvironmentValid =
    $offResult.environment.managedBuildConfiguration -eq "Release" -and
    $onResult.environment.managedBuildConfiguration -eq "Release" -and
    $offResult.environment.godotDebugBuild -eq $false -and
    $onResult.environment.godotDebugBuild -eq $false
$expectedStartTick = [long]$WarmupTicks
$expectedFinalTick = [long]$WarmupTicks + [long]$Ticks
$tickBoundsMatch =
    $offResult.measuredStartSimulationTick -eq $onResult.measuredStartSimulationTick -and
    $offResult.finalSimulationTick -eq $onResult.finalSimulationTick -and
    $offResult.measuredStartSimulationTick -eq $expectedStartTick -and
    $offResult.finalSimulationTick -eq $expectedFinalTick
$hashesValid =
    (Test-Sha256 $offResult.hashes.snapshotSha256) -and
    (Test-Sha256 $onResult.hashes.snapshotSha256) -and
    (Test-Sha256 $offResult.hashes.eventLogSha256) -and
    (Test-Sha256 $onResult.hashes.eventLogSha256) -and
    (Test-Sha256 $offResult.hashes.deterministicStateAndEventSha256) -and
    (Test-Sha256 $onResult.hashes.deterministicStateAndEventSha256)
$snapshotHashMatches = $hashesValid -and $offResult.hashes.snapshotSha256 -eq $onResult.hashes.snapshotSha256
$eventLogHashMatches = $hashesValid -and $offResult.hashes.eventLogSha256 -eq $onResult.hashes.eventLogSha256
$combinedHashMatches =
    $hashesValid -and
    $offResult.hashes.deterministicStateAndEventSha256 -eq
    $onResult.hashes.deterministicStateAndEventSha256
$resultStatusesValid =
    @("characterization_complete", "pass", "target_missed", "safety_failure_allowed") -contains $offResult.status -and
    $onResult.status -eq "diagnostic_complete"
$offRuntimeMetricsAbsent =
    $null -eq $offResult.diagnostics -and
    $null -eq $offResult.artifacts.runtimeMetricsCsv
$onRuntimeMetricsValid =
    $null -ne $onResult.diagnostics -and
    $onResult.diagnostics.batchCount -eq $Ticks -and
    $onResult.diagnostics.droppedBatchCount -eq 0 -and
    $onResult.diagnostics.pathPlanLookups -eq
        ($onResult.diagnostics.pathPlanCacheHits + $onResult.diagnostics.pathPlanCacheMisses) -and
    (Test-NonEmptyText $onResult.artifacts.runtimeMetricsCsv) -and
    (Test-Path -LiteralPath ([string]$onResult.artifacts.runtimeMetricsCsv) -PathType Leaf)
$artifactContractValid =
    $offRuntimeMetricsAbsent -and
    $onRuntimeMetricsValid -and
    (Test-NonEmptyText $offResult.artifacts.snapshot) -and
    (Test-NonEmptyText $onResult.artifacts.snapshot) -and
    (Test-NonEmptyText $offResult.artifacts.eventLog) -and
    (Test-NonEmptyText $onResult.artifacts.eventLog) -and
    (Test-Path -LiteralPath ([string]$offResult.artifacts.snapshot) -PathType Leaf) -and
    (Test-Path -LiteralPath ([string]$onResult.artifacts.snapshot) -PathType Leaf) -and
    (Test-Path -LiteralPath ([string]$offResult.artifacts.eventLog) -PathType Leaf) -and
    (Test-Path -LiteralPath ([string]$onResult.artifacts.eventLog) -PathType Leaf)
$equivalent =
    $schemaValid -and
    $configurationMatches -and
    $commandConfigurationMatches -and
    $modeContractValid -and
    $gitIdentityMatches -and
    $environmentMatches -and
    $tickBoundsMatch -and
    $hashesValid -and
    $snapshotHashMatches -and
    $eventLogHashMatches -and
    $combinedHashMatches -and
    $resultStatusesValid -and
    $artifactContractValid -and
    (-not $RequireRelease -or $releaseEnvironmentValid)

$equivalence = [ordered]@{
    schemaVersion = 1
    capturedUtc = [DateTime]::UtcNow.ToString("o")
    status = if (-not $equivalent) { "fail" } elseif ($gitDirty) { "pass_dirty_source" } else { "pass" }
    sourceClean = -not $gitDirty
    releaseRequired = [bool]$RequireRelease
    releaseEnvironmentValid = $releaseEnvironmentValid
    resultSchemaValid = $schemaValid
    configurationMatches = $configurationMatches
    commandConfigurationMatches = $commandConfigurationMatches
    modeContractValid = $modeContractValid
    gitIdentityMatches = $gitIdentityMatches
    environmentMatches = $environmentMatches
    tickBoundsMatch = $tickBoundsMatch
    hashesValid = $hashesValid
    snapshotHashMatches = $snapshotHashMatches
    eventLogHashMatches = $eventLogHashMatches
    combinedHashMatches = $combinedHashMatches
    resultStatusesValid = $resultStatusesValid
    artifactContractValid = $artifactContractValid
    metricsOffRuntimeMetricsAbsent = $offRuntimeMetricsAbsent
    metricsOnRuntimeMetricsValid = $onRuntimeMetricsValid
    metricsOffHash = $offResult.hashes.deterministicStateAndEventSha256
    metricsOnHash = $onResult.hashes.deterministicStateAndEventSha256
    metricsOffResult = $offResultPath
    metricsOnResult = $onResultPath
}
$equivalencePath = Join-Path $OutputRoot "equivalence-results.json"
Write-Utf8NoBom -Path $equivalencePath -Content ($equivalence | ConvertTo-Json -Depth 8)

$offMatrix = Get-Content -LiteralPath (Join-Path $offDirectory "perf-matrix.csv")
$onMatrix = Get-Content -LiteralPath (Join-Path $onDirectory "perf-matrix.csv")
if ($offMatrix.Count -ne 2 -or $onMatrix.Count -ne 2 -or $offMatrix[0] -ne $onMatrix[0]) {
    throw "Per-run performance matrix artifacts do not share the expected one-row schema."
}
$aggregateMatrixPath = Join-Path $OutputRoot "perf-matrix.csv"
Write-Utf8NoBom -Path $aggregateMatrixPath -Content (($offMatrix[0], $offMatrix[1], $onMatrix[1]) -join [System.Environment]::NewLine)

$summaryLines = @(
    "Societies metrics equivalence pair",
    "Status: $($equivalence.status)",
    "Scenario: $Scenario; seed: $Seed; citizens: $Citizens; warmup ticks: $WarmupTicks; measured ticks: $Ticks",
    "Managed build: $($offResult.environment.managedBuildConfiguration)",
    "Metrics-off p95: $($offResult.externalTickStatistics.p95Milliseconds) ms",
    "Metrics-on p95: $($onResult.externalTickStatistics.p95Milliseconds) ms",
    "Deterministic state+event hash: $($offResult.hashes.deterministicStateAndEventSha256)",
    "Output: $OutputRoot"
)
Write-Utf8NoBom -Path (Join-Path $OutputRoot "pair-summary.txt") -Content ($summaryLines -join [System.Environment]::NewLine)

if (-not $equivalent) {
    throw "Metrics-off and metrics-on runs did not produce equivalent deterministic state and event ordering."
}

Write-Host "Performance pair completed with status '$($equivalence.status)'."
Write-Host "Output: $OutputRoot"
