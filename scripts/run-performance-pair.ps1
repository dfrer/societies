[CmdletBinding()]
param(
    [string]$Scenario = "balanced_basin",
    [int]$Seed = 1337,
    [int]$Citizens = 16,
    [int]$Ticks = 300,
    [int]$WarmupTicks = 0,
    [ValidateSet("cold", "natural_warm", "forced_invalidation")]
    [string]$CacheMode = "cold",
    [ValidateSet("exact_branch_and_bound", "exhaustive_reference")]
    [string]$SelectorMode = "exact_branch_and_bound",
    [ValidateSet("exact_bounded", "exhaustive_reference")]
    [string]$ExtractionPlanningMode = "exact_bounded",
    [ValidateSet("cached_distance_only", "full_materialization_reference")]
    [string]$RouteDistanceMode = "cached_distance_only",
    [string]$ComparisonGroup,
    [int]$TrialIndex = 1,
    [string]$OutputRoot,
    [string]$GodotPath,
    [string]$ExportPreset = "Windows Performance Release",
    [string]$ExistingReleaseRunner,
    [switch]$SkipBuild,
    [switch]$ReleaseExport,
    [switch]$AllowPrimarySafetyFailure,
    [switch]$AllowDirtySource,
    [switch]$AllowDebugReference,
    [switch]$RequireRelease
)

$ErrorActionPreference = "Stop"
$CacheMode = $CacheMode.ToLowerInvariant()
$SelectorMode = $SelectorMode.ToLowerInvariant()
$ExtractionPlanningMode = $ExtractionPlanningMode.ToLowerInvariant()
$RouteDistanceMode = $RouteDistanceMode.ToLowerInvariant()

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
        $candidate = Get-ChildItem -LiteralPath $wingetRoot -Recurse -Filter "Godot*.exe" -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notlike "*_console.exe" } |
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

function ConvertTo-WindowsCommandLineArgument {
    param([string]$Argument)

    if ($Argument.Length -eq 0) { return '""' }
    if ($Argument -notmatch '[\s"]') { return $Argument }

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.Append('"')
    $backslashCount = 0
    for ($index = 0; $index -lt $Argument.Length; $index++) {
        $character = $Argument[$index]
        if ($character -eq [char]'\') {
            $backslashCount++
            continue
        }

        if ($character -eq [char]'"') {
            [void]$builder.Append([string]::new([char]'\', (2 * $backslashCount) + 1))
            [void]$builder.Append('"')
            $backslashCount = 0
            continue
        }

        if ($backslashCount -gt 0) {
            [void]$builder.Append([string]::new([char]'\', $backslashCount))
            $backslashCount = 0
        }
        [void]$builder.Append($character)
    }

    if ($backslashCount -gt 0) {
        [void]$builder.Append([string]::new([char]'\', 2 * $backslashCount))
    }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Invoke-ProcessAndWait {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Executable,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    # Inherit standard handles so GUI Godot remains render-capable and redirected streams cannot deadlock.
    $startInfo.RedirectStandardOutput = $false
    $startInfo.RedirectStandardError = $false
    if ($null -ne $startInfo.ArgumentList) {
        foreach ($argument in $Arguments) {
            [void]$startInfo.ArgumentList.Add($argument)
        }
    }
    else {
        $startInfo.Arguments = (($Arguments | ForEach-Object { ConvertTo-WindowsCommandLineArgument -Argument $_ }) -join ' ')
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "Could not start executable '$Executable'."
        }

        $process.WaitForExit()
        return $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
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

function Test-ForcedProbeStructuralMatch {
    param(
        [object]$Left,
        [object]$Right
    )

    if ($null -eq $Left -or $null -eq $Right) {
        return $null -eq $Left -and $null -eq $Right
    }

    return (
        $Left.prepared -eq $Right.prepared -and
        $Left.committed -eq $Right.committed -and
        $Left.pathSegmentStructureId -eq $Right.pathSegmentStructureId -and
        $Left.pathSegmentWasBuiltBefore -eq $Right.pathSegmentWasBuiltBefore -and
        $Left.pathSegmentIsBuiltAfter -eq $Right.pathSegmentIsBuiltAfter -and
        $Left.changedCellGridX -eq $Right.changedCellGridX -and
        $Left.changedCellGridY -eq $Right.changedCellGridY -and
        $Left.completionTick -eq $Right.completionTick -and
        $Left.versionBeforeCommit -eq $Right.versionBeforeCommit -and
        $Left.versionAfterCommit -eq $Right.versionAfterCommit -and
        $Left.totalInvalidationsBeforeCommit -eq $Right.totalInvalidationsBeforeCommit -and
        $Left.totalInvalidationsAfterCommit -eq $Right.totalInvalidationsAfterCommit -and
        $Left.cacheEntriesBeforeRebuild -eq $Right.cacheEntriesBeforeRebuild -and
        $Left.cacheEntriesImmediatelyAfterRebuild -eq $Right.cacheEntriesImmediatelyAfterRebuild -and
        $Left.firstPostChangeLookupObserved -eq $Right.firstPostChangeLookupObserved -and
        $Left.firstPostChangeLookupWasCacheMiss -eq $Right.firstPostChangeLookupWasCacheMiss -and
        $Left.firstPostChangeLookupUsedNewVersion -eq $Right.firstPostChangeLookupUsedNewVersion -and
        $Left.probeStartGridX -eq $Right.probeStartGridX -and
        $Left.probeStartGridY -eq $Right.probeStartGridY -and
        $Left.probeEndGridX -eq $Right.probeEndGridX -and
        $Left.probeEndGridY -eq $Right.probeEndGridY -and
        $Left.preChangeQueryVersion -eq $Right.preChangeQueryVersion -and
        $Left.preChangePlanVersion -eq $Right.preChangePlanVersion -and
        $Left.postChangeQueryVersion -eq $Right.postChangeQueryVersion -and
        $Left.postChangePlanVersion -eq $Right.postChangePlanVersion -and
        $Left.exactEndpointsMatch -eq $Right.exactEndpointsMatch -and
        $Left.changedCellIncludedInPostChangePlan -eq $Right.changedCellIncludedInPostChangePlan -and
        $Left.preChangePlanCost -eq $Right.preChangePlanCost -and
        $Left.postChangePlanCost -eq $Right.postChangePlanCost
    )
}

function Test-ProbeSnapshotStructuralMatch {
    param(
        [object]$Left,
        [object]$Right
    )

    if ($null -eq $Left -or $null -eq $Right) {
        return $false
    }

    return (
        $Left.pathCacheEntryCount -eq $Right.pathCacheEntryCount -and
        $Left.simulationTick -eq $Right.simulationTick -and
        $Left.navigationRulesVersion -eq $Right.navigationRulesVersion -and
        $Left.allPathCacheKeysMatchNavigationRulesVersion -eq
            $Right.allPathCacheKeysMatchNavigationRulesVersion -and
        $Left.totalNavigationInvalidations -eq $Right.totalNavigationInvalidations -and
        $Left.lastPathPlanRulesVersion -eq $Right.lastPathPlanRulesVersion -and
        (Test-ForcedProbeStructuralMatch $Left.forcedInvalidation $Right.forcedInvalidation)
    )
}

function Test-CacheEvidenceStructuralMatch {
    param(
        [object]$Left,
        [object]$Right
    )

    if ($null -eq $Left -or $null -eq $Right) {
        return $false
    }

    $forcedWrapperMatches = if ($null -eq $Left.forcedInvalidation -or $null -eq $Right.forcedInvalidation) {
        $null -eq $Left.forcedInvalidation -and $null -eq $Right.forcedInvalidation
    }
    else {
        $Left.forcedInvalidation.navigationInvalidationCount -eq
            $Right.forcedInvalidation.navigationInvalidationCount -and
        (Test-ForcedProbeStructuralMatch $Left.forcedInvalidation.probe $Right.forcedInvalidation.probe)
    }

    return (
        $Left.cacheMode -eq $Right.cacheMode -and
        $Left.preparationStrategy -eq $Right.preparationStrategy -and
        $Left.clearedEntryCount -eq $Right.clearedEntryCount -and
        (Test-ProbeSnapshotStructuralMatch $Left.afterBootstrap $Right.afterBootstrap) -and
        (Test-ProbeSnapshotStructuralMatch $Left.afterNaturalWarmup $Right.afterNaturalWarmup) -and
        (Test-ProbeSnapshotStructuralMatch $Left.beforeMeasurement $Right.beforeMeasurement) -and
        (Test-ProbeSnapshotStructuralMatch $Left.afterMeasurement $Right.afterMeasurement) -and
        $forcedWrapperMatches
    )
}

function Test-ForcedEvidenceInternalConsistency {
    param([object]$CacheEvidence)

    if ($null -eq $CacheEvidence -or $null -eq $CacheEvidence.forcedInvalidation) {
        return $false
    }

    $wrapper = $CacheEvidence.forcedInvalidation
    $snapshot = $CacheEvidence.afterMeasurement
    $probe = $wrapper.probe
    $snapshotProbe = $snapshot.forcedInvalidation
    return (
        (Test-ForcedProbeStructuralMatch $probe $snapshotProbe) -and
        $probe.commitToFirstLookupMilliseconds -eq $snapshotProbe.commitToFirstLookupMilliseconds -and
        $null -ne $probe.commitToFirstLookupMilliseconds -and
        -not [double]::IsNaN([double]$probe.commitToFirstLookupMilliseconds) -and
        -not [double]::IsInfinity([double]$probe.commitToFirstLookupMilliseconds) -and
        $probe.commitToFirstLookupMilliseconds -ge 0 -and
        $wrapper.navigationInvalidationCount -eq
            ($snapshot.totalNavigationInvalidations - $CacheEvidence.beforeMeasurement.totalNavigationInvalidations) -and
        $snapshot.navigationRulesVersion -eq $probe.versionAfterCommit -and
        $snapshot.lastPathPlanRulesVersion -eq $probe.versionAfterCommit -and
        $probe.versionBeforeCommit -eq $CacheEvidence.beforeMeasurement.navigationRulesVersion -and
        $probe.totalInvalidationsBeforeCommit -eq
            $CacheEvidence.beforeMeasurement.totalNavigationInvalidations -and
        $probe.totalInvalidationsAfterCommit -eq $snapshot.totalNavigationInvalidations
    )
}

function Get-GodotSemanticVersion {
    param([object]$Value)

    if (-not (Test-NonEmptyText $Value)) {
        return $null
    }

    $versionMatch = [regex]::Match([string]$Value, '(?<!\d)(\d+\.\d+\.\d+)(?!\d)')
    if (-not $versionMatch.Success) {
        return $null
    }

    return $versionMatch.Groups[1].Value
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

    $runnerArguments = @("--headless")
    if ($script:executionRoute -eq "editor_scene") {
        $runnerArguments += @(
            "--path",
            $script:projectPath,
            "res://tests/PerfRunner.tscn"
        )
    }
    $runnerArguments += @(
        "--",
        "--scenario", $Scenario,
        "--seed", $Seed.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--citizens", $Citizens.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--ticks", $Ticks.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--warmup-ticks", $WarmupTicks.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--cache-mode", $CacheMode,
        "--selector-mode", $SelectorMode,
        "--extraction-planning-mode", $ExtractionPlanningMode,
        "--route-distance-mode", $RouteDistanceMode,
        "--comparison-group", $script:comparisonGroup,
        "--trial-index", $TrialIndex.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--metrics", $MetricsMode,
        "--output-dir", $RunOutputDirectory,
        "--run-id", $RunId,
        "--git-sha", $script:gitSha,
        "--git-dirty", $gitDirtyText,
        "--execution-route", $script:executionRoute,
        "--project-path", $script:projectPath,
        "--runner-executable", $script:runnerExecutable,
        "--allow-safety-failure", $allowSafetyText
    )

    if ($script:executionRoute -eq "editor_scene") {
        $runnerExitCode = Invoke-ProcessAndWait `
            -Executable $script:runnerExecutable `
            -Arguments $runnerArguments `
            -WorkingDirectory $script:repoRoot
    }
    else {
        & $script:runnerExecutable @runnerArguments
        $runnerExitCode = $LASTEXITCODE
    }

    if ($runnerExitCode -ne 0) {
        throw "Performance runner '$RunId' exited with code $runnerExitCode."
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
if ($CacheMode -in @("natural_warm", "forced_invalidation") -and $WarmupTicks -le 0) {
    throw "CacheMode '$CacheMode' requires WarmupTicks greater than zero so the cache is naturally preconditioned."
}
if ($CacheMode -eq "forced_invalidation" -and $Ticks -ne 1) {
    throw "CacheMode 'forced_invalidation' requires exactly one measured tick."
}
if ($TrialIndex -lt 1 -or $TrialIndex -gt 100) {
    throw "TrialIndex must be between 1 and 100."
}
if ($ReleaseExport -and -not [string]::IsNullOrWhiteSpace($ExistingReleaseRunner)) {
    throw "-ReleaseExport and -ExistingReleaseRunner are mutually exclusive."
}
if (($ReleaseExport -or -not [string]::IsNullOrWhiteSpace($ExistingReleaseRunner)) -and $SkipBuild) {
    throw "-SkipBuild cannot be combined with a Release runner."
}
$releaseRouteRequested = $ReleaseExport -or -not [string]::IsNullOrWhiteSpace($ExistingReleaseRunner)
if ($releaseRouteRequested -and $env:OS -ne "Windows_NT") {
    throw "The tracked Release export route currently supports Windows only."
}
if ($releaseRouteRequested) {
    $RequireRelease = $true
}

$gitSha = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Could not resolve the current git SHA."
}
$gitDirty = Get-GitDirtyState
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
$projectPath = Join-Path $repoRoot "src\societies"
$expectedGodotVersion = "4.6.2"
$godotVersionProbe = $godot
if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
    $godotBaseName = [System.IO.Path]::GetFileNameWithoutExtension($godot)
    if (-not $godotBaseName.EndsWith("_console", [System.StringComparison]::OrdinalIgnoreCase)) {
        $godotConsoleCandidate = Join-Path `
            (Split-Path -Parent $godot) `
            ($godotBaseName + "_console" + [System.IO.Path]::GetExtension($godot))
        if (Test-Path -LiteralPath $godotConsoleCandidate -PathType Leaf) {
            $godotVersionProbe = (Resolve-Path -LiteralPath $godotConsoleCandidate).Path
        }
    }
}
$godotVersionLines = @(& $godotVersionProbe --version 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Could not query the resolved Godot executable version."
}
$godotVersionOutput = ($godotVersionLines | ForEach-Object { [string]$_ }) -join [System.Environment]::NewLine
$resolvedGodotVersion = Get-GodotSemanticVersion $godotVersionOutput
if ($resolvedGodotVersion -ne $expectedGodotVersion) {
    throw "Godot $expectedGodotVersion is required, but '$godot' reported '$godotVersionOutput'."
}

$timestamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss-fff")
$safeScenario = $Scenario -replace '[^A-Za-z0-9._-]', '-'
if ([string]::IsNullOrWhiteSpace($safeScenario)) {
    $safeScenario = "scenario"
}
if ($safeScenario.Length -gt 32) {
    $safeScenario = $safeScenario.Substring(0, 32)
}
$routeDistanceToken = if ($RouteDistanceMode -eq "cached_distance_only") { "cached" } else { "materialized" }
$comparisonGroup = if ([string]::IsNullOrWhiteSpace($ComparisonGroup)) {
    "$safeScenario-seed$Seed-c$Citizens-t$Ticks-w$WarmupTicks-s$SelectorMode-e$ExtractionPlanningMode-r$routeDistanceToken"
}
else {
    $ComparisonGroup
}
if ($comparisonGroup.Length -gt 96 -or
    $comparisonGroup.Contains("..") -or
    $comparisonGroup -notmatch '^[A-Za-z0-9._-]+$') {
    throw "ComparisonGroup may contain only letters, digits, '.', '_' and '-' and may not contain '..' or exceed 96 characters."
}
$nonce = [Guid]::NewGuid().ToString("N").Substring(0, 8)
$runDescriptor = "$timestamp-$safeScenario-seed$Seed-c$Citizens-t$Ticks-w$WarmupTicks-m$CacheMode-r$TrialIndex-$nonce"
if (-not $OutputRoot) {
    $OutputRoot = Join-Path $repoRoot "artifacts\performance\$runDescriptor"
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Performance pair output already exists: $OutputRoot"
}

if ($ReleaseExport) {
    [System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
    $releaseDirectory = Join-Path $OutputRoot "release-runner"
    [System.IO.Directory]::CreateDirectory($releaseDirectory) | Out-Null
    $releaseExecutable = Join-Path $releaseDirectory "SocietiesPerformance.exe"

    $exportExitCode = Invoke-ProcessAndWait `
        -Executable $godot `
        -Arguments @("--headless", "--path", $projectPath, "--export-release", $ExportPreset, $releaseExecutable) `
        -WorkingDirectory $repoRoot
    if ($exportExitCode -ne 0) {
        throw "Godot Release export exited with code $exportExitCode."
    }

    $consoleWrapper = Join-Path $releaseDirectory "SocietiesPerformance.console.exe"
    if (-not (Test-Path -LiteralPath $consoleWrapper -PathType Leaf)) {
        throw "The Windows Release export did not produce the required console wrapper: $consoleWrapper"
    }
    $runnerExecutable = $consoleWrapper
    $executionRoute = "export_release"
}
elseif (-not [string]::IsNullOrWhiteSpace($ExistingReleaseRunner)) {
    [System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
    $runnerExecutable = (Resolve-Path -LiteralPath $ExistingReleaseRunner).Path
    if (-not $runnerExecutable.EndsWith(".console.exe", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "ExistingReleaseRunner must point to the exported .console.exe wrapper."
    }

    $releaseExecutable = $runnerExecutable.Substring(
        0,
        $runnerExecutable.Length - ".console.exe".Length) + ".exe"
    if (-not (Test-Path -LiteralPath $releaseExecutable -PathType Leaf)) {
        throw "The existing Release runner is missing its paired executable: $releaseExecutable"
    }

    $executionRoute = "export_release"
}
else {
    if (-not $SkipBuild) {
        $buildExitCode = Invoke-ProcessAndWait `
            -Executable $godot `
            -Arguments @("--headless", "--path", $projectPath, "--build-solutions", "--quit") `
            -WorkingDirectory $repoRoot
        if ($buildExitCode -ne 0) {
            throw "Godot solution build exited with code $buildExitCode."
        }
    }

    [System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
    $runnerExecutable = $godot
    $executionRoute = "editor_scene"
}

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
$offManifest = Get-Content -Raw -LiteralPath (Join-Path $offDirectory "validation-manifest.json") | ConvertFrom-Json
$onManifest = Get-Content -Raw -LiteralPath (Join-Path $onDirectory "validation-manifest.json") | ConvertFrom-Json

$schemaValid =
    $offResult.schemaVersion -eq 6 -and
    $onResult.schemaVersion -eq 6
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
    $offResult.configuration.cacheWarmupEnabled -eq $onResult.configuration.cacheWarmupEnabled -and
    $offResult.configuration.cacheMode -eq $onResult.configuration.cacheMode -and
    $offResult.configuration.selectorMode -eq $onResult.configuration.selectorMode -and
    $offResult.configuration.extractionPlanningMode -eq $onResult.configuration.extractionPlanningMode -and
    $offResult.configuration.routeDistanceMode -eq $onResult.configuration.routeDistanceMode -and
    $offResult.configuration.comparisonGroup -eq $onResult.configuration.comparisonGroup -and
    $offResult.configuration.trialIndex -eq $onResult.configuration.trialIndex -and
    $offResult.configuration.executionRoute -eq $onResult.configuration.executionRoute -and
    $offResult.configuration.projectPath -eq $onResult.configuration.projectPath -and
    $offResult.configuration.runnerExecutablePath -eq $onResult.configuration.runnerExecutablePath
$commandConfigurationMatches =
    $offResult.configuration.scenarioId -eq $Scenario -and
    $offResult.configuration.simulationSeed -eq $Seed -and
    $offResult.configuration.citizenCount -eq $Citizens -and
    $offResult.configuration.warmupTicks -eq $WarmupTicks -and
    $offResult.configuration.measuredTicks -eq $Ticks -and
    $offResult.configuration.cacheMode -eq $CacheMode -and
    $offResult.configuration.selectorMode -eq $SelectorMode -and
    $offResult.configuration.extractionPlanningMode -eq $ExtractionPlanningMode -and
    $offResult.configuration.routeDistanceMode -eq $RouteDistanceMode -and
    $offResult.configuration.comparisonGroup -eq $comparisonGroup -and
    $offResult.configuration.trialIndex -eq $TrialIndex -and
    $offResult.configuration.executionRoute -eq $executionRoute -and
    $offResult.configuration.projectPath -eq $projectPath -and
    $offResult.configuration.runnerExecutablePath -eq $runnerExecutable
$modeContractValid =
    $offResult.configuration.metricsEnabled -eq $false -and
    $onResult.configuration.metricsEnabled -eq $true -and
    $offResult.configuration.measurementMode -eq "one_manual_step_per_external_sample" -and
    $offResult.configuration.cacheWarmupEnabled -eq $false -and
    $onResult.configuration.cacheWarmupEnabled -eq $false
$routeDistanceEvidenceValid =
    $offResult.measuredCachedRouteDistanceFastPathHits -eq $onResult.measuredCachedRouteDistanceFastPathHits -and
    (($RouteDistanceMode -eq "cached_distance_only" -and
        $offResult.measuredCachedRouteDistanceFastPathHits -gt 0) -or
     ($RouteDistanceMode -eq "full_materialization_reference" -and
        $offResult.measuredCachedRouteDistanceFastPathHits -eq 0))
$routeDistanceManifestValid =
    $offManifest.measuredCachedRouteDistanceFastPathHits -eq $offResult.measuredCachedRouteDistanceFastPathHits -and
    $onManifest.measuredCachedRouteDistanceFastPathHits -eq $onResult.measuredCachedRouteDistanceFastPathHits
$cacheEvidencePairValid = Test-CacheEvidenceStructuralMatch `
    $offResult.cacheEvidence `
    $onResult.cacheEvidence
$cacheEvidenceCommonValid =
    $cacheEvidencePairValid -and
    $offResult.cacheEvidence.cacheMode -eq $CacheMode -and
    (Test-NonEmptyText $offResult.cacheEvidence.preparationStrategy) -and
    $offResult.cacheEvidence.afterBootstrap.pathCacheEntryCount -eq 0 -and
    $offResult.cacheEvidence.afterBootstrap.simulationTick -eq 0 -and
    $offResult.cacheEvidence.afterNaturalWarmup.simulationTick -eq $WarmupTicks -and
    $offResult.cacheEvidence.beforeMeasurement.simulationTick -eq $WarmupTicks -and
    $offResult.cacheEvidence.afterMeasurement.simulationTick -eq ($WarmupTicks + $Ticks) -and
    $offResult.cacheEvidence.afterBootstrap.allPathCacheKeysMatchNavigationRulesVersion -eq $true -and
    $offResult.cacheEvidence.afterNaturalWarmup.allPathCacheKeysMatchNavigationRulesVersion -eq $true -and
    $offResult.cacheEvidence.beforeMeasurement.allPathCacheKeysMatchNavigationRulesVersion -eq $true -and
    $offResult.cacheEvidence.afterMeasurement.allPathCacheKeysMatchNavigationRulesVersion -eq $true
$naturalNavigationVersionDelta =
    [decimal]$offResult.cacheEvidence.afterMeasurement.navigationRulesVersion -
    [decimal]$offResult.cacheEvidence.beforeMeasurement.navigationRulesVersion
$naturalInvalidationDelta =
    [decimal]$offResult.cacheEvidence.afterMeasurement.totalNavigationInvalidations -
    [decimal]$offResult.cacheEvidence.beforeMeasurement.totalNavigationInvalidations
$naturalInvalidationContractValid =
    $naturalNavigationVersionDelta -ge 0 -and
    $naturalInvalidationDelta -ge 0 -and
    $naturalNavigationVersionDelta -eq $naturalInvalidationDelta
$cacheTransitionContractValid = $false
if ($CacheMode -eq "cold") {
    $cacheTransitionContractValid =
        $naturalInvalidationContractValid -and
        $null -eq $offResult.cacheEvidence.forcedInvalidation -and
        $offResult.cacheEvidence.clearedEntryCount -eq
            $offResult.cacheEvidence.afterNaturalWarmup.pathCacheEntryCount -and
        $offResult.cacheEvidence.beforeMeasurement.pathCacheEntryCount -eq 0 -and
        $offResult.cacheEvidence.beforeMeasurement.navigationRulesVersion -eq
            $offResult.cacheEvidence.afterNaturalWarmup.navigationRulesVersion -and
        $offResult.cacheEvidence.beforeMeasurement.totalNavigationInvalidations -eq
            $offResult.cacheEvidence.afterNaturalWarmup.totalNavigationInvalidations -and
        $offResult.cacheEvidence.beforeMeasurement.lastPathPlanRulesVersion -eq
            $offResult.cacheEvidence.afterNaturalWarmup.lastPathPlanRulesVersion -and
        ($WarmupTicks -eq 0 -or $offResult.cacheEvidence.afterNaturalWarmup.pathCacheEntryCount -gt 0)
}
elseif ($CacheMode -eq "natural_warm") {
    $cacheTransitionContractValid =
        $naturalInvalidationContractValid -and
        $null -eq $offResult.cacheEvidence.forcedInvalidation -and
        $offResult.cacheEvidence.clearedEntryCount -eq 0 -and
        $offResult.cacheEvidence.afterNaturalWarmup.pathCacheEntryCount -gt 0 -and
        $offResult.cacheEvidence.beforeMeasurement.pathCacheEntryCount -eq
            $offResult.cacheEvidence.afterNaturalWarmup.pathCacheEntryCount -and
        $offResult.cacheEvidence.beforeMeasurement.navigationRulesVersion -eq
            $offResult.cacheEvidence.afterNaturalWarmup.navigationRulesVersion -and
        $offResult.cacheEvidence.beforeMeasurement.totalNavigationInvalidations -eq
            $offResult.cacheEvidence.afterNaturalWarmup.totalNavigationInvalidations -and
        $offResult.cacheEvidence.beforeMeasurement.lastPathPlanRulesVersion -eq
            $offResult.cacheEvidence.afterNaturalWarmup.lastPathPlanRulesVersion
}
elseif ($CacheMode -eq "forced_invalidation") {
    $forcedProbe = $offResult.cacheEvidence.forcedInvalidation.probe
    $forcedPreparedProbe = $offResult.cacheEvidence.beforeMeasurement.forcedInvalidation
    $cacheTransitionContractValid =
        $null -ne $offResult.cacheEvidence.forcedInvalidation -and
        $offResult.cacheEvidence.clearedEntryCount -eq 0 -and
        $offResult.cacheEvidence.afterNaturalWarmup.pathCacheEntryCount -gt 0 -and
        $offResult.cacheEvidence.beforeMeasurement.pathCacheEntryCount -gt 0 -and
        $offResult.cacheEvidence.beforeMeasurement.navigationRulesVersion -eq
            $offResult.cacheEvidence.afterNaturalWarmup.navigationRulesVersion -and
        $offResult.cacheEvidence.beforeMeasurement.totalNavigationInvalidations -eq
            $offResult.cacheEvidence.afterNaturalWarmup.totalNavigationInvalidations -and
        $offResult.cacheEvidence.beforeMeasurement.lastPathPlanRulesVersion -eq
            $offResult.cacheEvidence.beforeMeasurement.navigationRulesVersion -and
        $offResult.cacheEvidence.forcedInvalidation.navigationInvalidationCount -eq 1 -and
        $forcedProbe.prepared -eq $true -and
        $forcedProbe.committed -eq $true -and
        $forcedProbe.pathSegmentWasBuiltBefore -eq $false -and
        $forcedProbe.pathSegmentIsBuiltAfter -eq $true -and
        $forcedProbe.completionTick -eq ($WarmupTicks + 1) -and
        $forcedProbe.versionAfterCommit -eq ($forcedProbe.versionBeforeCommit + 1) -and
        $forcedProbe.totalInvalidationsAfterCommit -eq ($forcedProbe.totalInvalidationsBeforeCommit + 1) -and
        $forcedProbe.cacheEntriesBeforeRebuild -gt 0 -and
        $forcedProbe.cacheEntriesImmediatelyAfterRebuild -eq 0 -and
        $forcedProbe.firstPostChangeLookupObserved -eq $true -and
        $forcedProbe.firstPostChangeLookupWasCacheMiss -eq $true -and
        $forcedProbe.firstPostChangeLookupUsedNewVersion -eq $true -and
        $forcedProbe.preChangeQueryVersion -eq $forcedProbe.versionBeforeCommit -and
        $forcedProbe.preChangePlanVersion -eq $forcedProbe.versionBeforeCommit -and
        $forcedProbe.postChangeQueryVersion -eq $forcedProbe.versionAfterCommit -and
        $forcedProbe.postChangePlanVersion -eq $forcedProbe.versionAfterCommit -and
        $forcedProbe.exactEndpointsMatch -eq $true -and
        $forcedProbe.changedCellIncludedInPostChangePlan -eq $true -and
        $null -ne $forcedProbe.preChangePlanCost -and
        $null -ne $forcedProbe.postChangePlanCost -and
        $forcedProbe.postChangePlanCost -lt $forcedProbe.preChangePlanCost -and
        $null -ne $forcedProbe.commitToFirstLookupMilliseconds -and
        $forcedProbe.commitToFirstLookupMilliseconds -ge 0
    $cacheTransitionContractValid =
        $cacheTransitionContractValid -and
        $forcedPreparedProbe.pathSegmentStructureId -eq $forcedProbe.pathSegmentStructureId -and
        $forcedPreparedProbe.changedCellGridX -eq $forcedProbe.changedCellGridX -and
        $forcedPreparedProbe.changedCellGridY -eq $forcedProbe.changedCellGridY -and
        $forcedPreparedProbe.probeStartGridX -eq $forcedProbe.probeStartGridX -and
        $forcedPreparedProbe.probeStartGridY -eq $forcedProbe.probeStartGridY -and
        $forcedPreparedProbe.probeEndGridX -eq $forcedProbe.probeEndGridX -and
        $forcedPreparedProbe.probeEndGridY -eq $forcedProbe.probeEndGridY -and
        $forcedPreparedProbe.preChangeQueryVersion -eq $forcedProbe.preChangeQueryVersion -and
        $forcedPreparedProbe.preChangePlanVersion -eq $forcedProbe.preChangePlanVersion -and
        $forcedPreparedProbe.preChangePlanCost -eq $forcedProbe.preChangePlanCost -and
        (Test-ForcedEvidenceInternalConsistency $offResult.cacheEvidence) -and
        (Test-ForcedEvidenceInternalConsistency $onResult.cacheEvidence)
}
$executionRouteValid =
    ($releaseRouteRequested -and $executionRoute -eq "export_release") -or
    (-not $releaseRouteRequested -and $executionRoute -eq "editor_scene")
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
    (Test-NonEmptyText $offResult.environment.processExecutablePath) -and
    (Test-NonEmptyText $offResult.environment.managedBuildConfiguration) -and
    (Test-NonEmptyText $offResult.environment.managedAssemblyConfiguration) -and
    $offResult.environment.machineName -eq $onResult.environment.machineName -and
    $offResult.environment.logicalProcessorCount -eq $onResult.environment.logicalProcessorCount -and
    $offResult.environment.operatingSystem -eq $onResult.environment.operatingSystem -and
    $offResult.environment.processArchitecture -eq $onResult.environment.processArchitecture -and
    $offResult.environment.dotnetRuntime -eq $onResult.environment.dotnetRuntime -and
    $offResult.environment.godotVersion -eq $onResult.environment.godotVersion -and
    $offResult.environment.processExecutablePath -eq $onResult.environment.processExecutablePath -and
    $offResult.environment.managedBuildConfiguration -eq $onResult.environment.managedBuildConfiguration -and
    $offResult.environment.managedAssemblyConfiguration -eq $onResult.environment.managedAssemblyConfiguration -and
    $offResult.environment.godotDebugBuild -eq $onResult.environment.godotDebugBuild -and
    $offResult.environment.godotReleaseFeature -eq $onResult.environment.godotReleaseFeature -and
    $offResult.environment.godotTemplateFeature -eq $onResult.environment.godotTemplateFeature -and
    $offResult.environment.godotEditorFeature -eq $onResult.environment.godotEditorFeature -and
    $offResult.environment.verifiedReleaseExecution -eq $onResult.environment.verifiedReleaseExecution
$offRunGodotVersion = Get-GodotSemanticVersion $offResult.environment.godotVersion
$onRunGodotVersion = Get-GodotSemanticVersion $onResult.environment.godotVersion
$godotVersionValid =
    $resolvedGodotVersion -eq $expectedGodotVersion -and
    $offRunGodotVersion -eq $expectedGodotVersion -and
    $onRunGodotVersion -eq $expectedGodotVersion
$processExecutableMatches =
    (-not $releaseRouteRequested) -or
    ($offResult.environment.processExecutablePath -eq $releaseExecutable -and
        $onResult.environment.processExecutablePath -eq $releaseExecutable)
$releaseEnvironmentValid =
    $offResult.environment.managedBuildConfiguration -eq "Release" -and
    $onResult.environment.managedBuildConfiguration -eq "Release" -and
    $offResult.environment.managedAssemblyConfiguration -eq "ExportRelease" -and
    $onResult.environment.managedAssemblyConfiguration -eq "ExportRelease" -and
    $offResult.environment.godotDebugBuild -eq $false -and
    $onResult.environment.godotDebugBuild -eq $false -and
    $offResult.environment.godotReleaseFeature -eq $true -and
    $onResult.environment.godotReleaseFeature -eq $true -and
    $offResult.environment.godotTemplateFeature -eq $true -and
    $onResult.environment.godotTemplateFeature -eq $true -and
    $offResult.environment.godotEditorFeature -eq $false -and
    $onResult.environment.godotEditorFeature -eq $false -and
    $offResult.environment.verifiedReleaseExecution -eq $true -and
    $onResult.environment.verifiedReleaseExecution -eq $true
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
$cacheDiagnosticsContractValid = if ($CacheMode -eq "cold") {
    $onRuntimeMetricsValid -and
    $onResult.diagnostics.firstMeasuredBatchPathPlanCacheMisses -gt 0 -and
    $onResult.diagnostics.navigationInvalidations -eq $naturalInvalidationDelta
}
elseif ($CacheMode -eq "natural_warm") {
    $onRuntimeMetricsValid -and
    $onResult.diagnostics.firstMeasuredBatchPathPlanCacheHits -gt 0 -and
    $onResult.diagnostics.navigationInvalidations -eq $naturalInvalidationDelta
}
else {
    $onRuntimeMetricsValid -and
    $onResult.diagnostics.batchCount -eq 1 -and
    $onResult.diagnostics.firstMeasuredBatchNavigationInvalidations -eq 1 -and
    $onResult.diagnostics.navigationInvalidations -eq 1 -and
    $onResult.diagnostics.phases.navigationRebuildMilliseconds -gt 0
}
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
$offMatrixPath = Join-Path $offDirectory "perf-matrix.csv"
$onMatrixPath = Join-Path $onDirectory "perf-matrix.csv"
$offMatrix = if (Test-Path -LiteralPath $offMatrixPath -PathType Leaf) {
    @(Get-Content -LiteralPath $offMatrixPath)
}
else {
    @()
}
$onMatrix = if (Test-Path -LiteralPath $onMatrixPath -PathType Leaf) {
    @(Get-Content -LiteralPath $onMatrixPath)
}
else {
    @()
}
$matrixSchemaValid =
    $offMatrix.Count -eq 2 -and
    $onMatrix.Count -eq 2 -and
    $offMatrix[0] -eq $onMatrix[0]
$equivalent =
    $schemaValid -and
    $configurationMatches -and
    $commandConfigurationMatches -and
    $modeContractValid -and
    $routeDistanceEvidenceValid -and
    $routeDistanceManifestValid -and
    $cacheEvidenceCommonValid -and
    $cacheTransitionContractValid -and
    $cacheDiagnosticsContractValid -and
    $executionRouteValid -and
    $gitIdentityMatches -and
    $environmentMatches -and
    $godotVersionValid -and
    $processExecutableMatches -and
    $tickBoundsMatch -and
    $hashesValid -and
    $snapshotHashMatches -and
    $eventLogHashMatches -and
    $combinedHashMatches -and
    $resultStatusesValid -and
    $artifactContractValid -and
    $matrixSchemaValid -and
    (-not $RequireRelease -or $releaseEnvironmentValid)

$equivalence = [ordered]@{
    schemaVersion = 6
    capturedUtc = [DateTime]::UtcNow.ToString("o")
    status = if (-not $equivalent) { "fail" } elseif ($gitDirty) { "pass_dirty_source" } else { "pass" }
    contractStatus = if (-not $equivalent) { "fail" } elseif ($gitDirty) { "pass_dirty_source" } else { "pass" }
    budgetStatus = if ($offResult.budget.safetyPassed -eq $false) {
        "safety_failure"
    }
    elseif ($offResult.budget.targetPassed -eq $false) {
        "target_missed"
    }
    elseif ($offResult.budget.targetPassed -eq $true) {
        "target_passed_single_run"
    }
    else {
        "characterization_only"
    }
    sourceClean = -not $gitDirty
    expectedGodotVersion = $expectedGodotVersion
    resolvedEditorVersion = $resolvedGodotVersion
    resolvedEditorVersionOutput = $godotVersionOutput
    releaseRequired = [bool]$RequireRelease
    releaseExport = [bool]$ReleaseExport
    reusedReleaseRunner = -not [string]::IsNullOrWhiteSpace($ExistingReleaseRunner)
    exportPreset = if ($ReleaseExport) { $ExportPreset } else { $null }
    exportEditorExecutable = if ($ReleaseExport) { $godot } else { $null }
    exportProjectPath = if ($ReleaseExport) { $projectPath } else { $null }
    exportOutputExecutable = if ($releaseRouteRequested) { $releaseExecutable } else { $null }
    executionRoute = $executionRoute
    runnerExecutable = $runnerExecutable
    releaseEnvironmentValid = $releaseEnvironmentValid
    resultSchemaValid = $schemaValid
    configurationMatches = $configurationMatches
    commandConfigurationMatches = $commandConfigurationMatches
    modeContractValid = $modeContractValid
    routeDistanceEvidenceValid = $routeDistanceEvidenceValid
    routeDistanceManifestValid = $routeDistanceManifestValid
    metricsOffMeasuredCachedRouteDistanceFastPathHits = $offResult.measuredCachedRouteDistanceFastPathHits
    metricsOnMeasuredCachedRouteDistanceFastPathHits = $onResult.measuredCachedRouteDistanceFastPathHits
    cacheEvidencePairValid = $cacheEvidencePairValid
    cacheEvidenceCommonValid = $cacheEvidenceCommonValid
    cacheTransitionContractValid = $cacheTransitionContractValid
    cacheDiagnosticsContractValid = $cacheDiagnosticsContractValid
    singleModeTransitionEvidence =
        $cacheEvidenceCommonValid -and
        $cacheTransitionContractValid -and
        $cacheDiagnosticsContractValid
    cacheModeEvidence = $false
    crossModeEquivalenceCaptured = $false
    releaseBaselineEvidence = $false
    fullMatrixCaptured = $false
    medianOfThreeCaptured = $false
    targetOrSafetyClaimMade = $false
    executionRouteValid = $executionRouteValid
    gitIdentityMatches = $gitIdentityMatches
    environmentMatches = $environmentMatches
    godotVersionValid = $godotVersionValid
    processExecutableMatches = $processExecutableMatches
    tickBoundsMatch = $tickBoundsMatch
    hashesValid = $hashesValid
    snapshotHashMatches = $snapshotHashMatches
    eventLogHashMatches = $eventLogHashMatches
    combinedHashMatches = $combinedHashMatches
    resultStatusesValid = $resultStatusesValid
    artifactContractValid = $artifactContractValid
    matrixSchemaValid = $matrixSchemaValid
    metricsOffRuntimeMetricsAbsent = $offRuntimeMetricsAbsent
    metricsOnRuntimeMetricsValid = $onRuntimeMetricsValid
    cacheMode = $CacheMode
    selectorMode = $SelectorMode
    extractionPlanningMode = $ExtractionPlanningMode
    routeDistanceMode = $RouteDistanceMode
    comparisonGroup = $comparisonGroup
    trialIndex = $TrialIndex
    metricsOffCacheEvidence = $offResult.cacheEvidence
    metricsOnCacheEvidence = $onResult.cacheEvidence
    metricsOffHash = $offResult.hashes.deterministicStateAndEventSha256
    metricsOnHash = $onResult.hashes.deterministicStateAndEventSha256
    metricsOffResult = $offResultPath
    metricsOnResult = $onResultPath
}
$equivalencePath = Join-Path $OutputRoot "equivalence-results.json"
Write-Utf8NoBom -Path $equivalencePath -Content ($equivalence | ConvertTo-Json -Depth 8)

if ($matrixSchemaValid) {
    $aggregateMatrixPath = Join-Path $OutputRoot "perf-matrix.csv"
    Write-Utf8NoBom -Path $aggregateMatrixPath -Content (($offMatrix[0], $offMatrix[1], $onMatrix[1]) -join [System.Environment]::NewLine)
}

$summaryLines = @(
    "Societies metrics equivalence pair",
    "Status: $($equivalence.status)",
    "Scenario: $Scenario; seed: $Seed; citizens: $Citizens; warmup ticks: $WarmupTicks; measured ticks: $Ticks",
    "Cache mode: $CacheMode; selector mode: $SelectorMode; extraction planning mode: $ExtractionPlanningMode; route-distance mode: $RouteDistanceMode; comparison group: $comparisonGroup; trial: $TrialIndex",
    "Cache transition contract: $cacheTransitionContractValid; cache diagnostics contract: $cacheDiagnosticsContractValid",
    "Godot: expected $expectedGodotVersion; editor $resolvedGodotVersion; run $offRunGodotVersion",
    "Managed build: $($offResult.environment.managedBuildConfiguration)",
    "Managed assembly configuration: $($offResult.environment.managedAssemblyConfiguration)",
    "Process executable: $($offResult.environment.processExecutablePath)",
    "Execution route: $executionRoute; verified release: $($offResult.environment.verifiedReleaseExecution)",
    "Metrics-off p95: $($offResult.externalTickStatistics.p95Milliseconds) ms",
    "Metrics-on p95: $($onResult.externalTickStatistics.p95Milliseconds) ms",
    "Deterministic state+event hash: $($offResult.hashes.deterministicStateAndEventSha256)",
    "Output: $OutputRoot"
)
Write-Utf8NoBom -Path (Join-Path $OutputRoot "pair-summary.txt") -Content ($summaryLines -join [System.Environment]::NewLine)

if (-not $equivalent) {
    $failedChecks = @()
    if (-not $schemaValid) { $failedChecks += "result_schema" }
    if (-not $configurationMatches) { $failedChecks += "configuration_pair" }
    if (-not $routeDistanceEvidenceValid) { $failedChecks += "route_distance_evidence" }
    if (-not $routeDistanceManifestValid) { $failedChecks += "route_distance_manifest" }
    if (-not $commandConfigurationMatches) { $failedChecks += "command_configuration" }
    if (-not $modeContractValid) { $failedChecks += "mode_contract" }
    if (-not $cacheEvidencePairValid) { $failedChecks += "cache_evidence_pair" }
    if (-not $cacheEvidenceCommonValid) { $failedChecks += "cache_evidence_common" }
    if (-not $cacheTransitionContractValid) { $failedChecks += "cache_transition" }
    if (-not $cacheDiagnosticsContractValid) { $failedChecks += "cache_diagnostics" }
    if (-not $executionRouteValid) { $failedChecks += "execution_route" }
    if (-not $gitIdentityMatches) { $failedChecks += "git_identity" }
    if (-not $environmentMatches) { $failedChecks += "environment_pair" }
    if (-not $godotVersionValid) { $failedChecks += "godot_version" }
    if (-not $processExecutableMatches) { $failedChecks += "process_executable" }
    if (-not $tickBoundsMatch) { $failedChecks += "tick_bounds" }
    if (-not $hashesValid) { $failedChecks += "hash_format" }
    if (-not $snapshotHashMatches) { $failedChecks += "snapshot_hash" }
    if (-not $eventLogHashMatches) { $failedChecks += "event_log_hash" }
    if (-not $combinedHashMatches) { $failedChecks += "combined_hash" }
    if (-not $resultStatusesValid) { $failedChecks += "result_status" }
    if (-not $artifactContractValid) { $failedChecks += "artifact_contract" }
    if (-not $matrixSchemaValid) { $failedChecks += "matrix_schema" }
    if ($RequireRelease -and -not $releaseEnvironmentValid) { $failedChecks += "release_environment" }
    throw "Performance pair contract validation failed: $($failedChecks -join ', ')."
}

Write-Host "Performance pair completed with status '$($equivalence.status)'."
Write-Host "Output: $OutputRoot"
