param(
    [string]$GodotPath = $env:GODOT_BIN,
    [string]$OutputDirectory = 'artifacts/performance/accepted-scene-baseline',
    [ValidateRange(1, 3600)]
    [int]$WarmupFrames = 120,
    [ValidateRange(40, 3600)]
    [int]$MeasuredFrames = 300,
    [ValidateRange(30, 1800)]
    [int]$ChildTimeoutSeconds = 300,
    [switch]$AllowDirtySourceForSmoke,
    [switch]$FixedDeltaOnlyDiagnostic,
    [switch]$RealtimeOnlyDiagnostic
)

$ErrorActionPreference = 'Stop'
$baseSha = '31ea1d6012d6fd932d0bfe0dbc621e668fd58c80'
$preset = 'Windows Accepted Scene Baseline Release'
$trialSchema = 'societies_accepted_scene_baseline/v4'
$bundleSchema = 'societies_accepted_scene_baseline_bundle/v4'
$routeId = 'snow-globe-voxel-four-leg-edit-reload-replay/v4'
$environmentSchema = 'societies_accepted_scene_environment/v1'
$realtimeMode = 'realtime_performance'
$identityMode = 'fixed_delta_identity'
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectRoot = Join-Path $repositoryRoot 'src\societies'
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))

function Get-RepositoryContentIdentity {
    $rows = [System.Collections.Generic.List[string]]::new()
    $files = @(git -C $repositoryRoot ls-files --cached --others --exclude-standard | Sort-Object -Unique)
    foreach ($relativePath in $files) {
        $path = Join-Path $repositoryRoot $relativePath
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $blob = (git -C $repositoryRoot hash-object -- $relativePath).Trim()
            $rows.Add("$relativePath=$blob")
        } else {
            $rows.Add("$relativePath=<deleted>")
        }
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($rows -join "`n"))
    return [System.Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-RepositoryStatus {
    return @(git -C $repositoryRoot status --porcelain=v1 --untracked-files=all)
}

function Assert-SameSequence {
    param([object[]]$Expected, [object[]]$Actual, [string]$Label)
    if ($Expected.Count -ne $Actual.Count) { throw "$Label count changed." }
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if ([string]$Expected[$index] -cne [string]$Actual[$index]) {
            throw "$Label changed at row $index."
        }
    }
}

function Invoke-ExactChild {
    param([string]$Executable, [string[]]$Arguments, [string]$WorkingDirectory, [string]$Label)
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $false
    $startInfo.RedirectStandardError = $false
    foreach ($argument in $Arguments) { [void]$startInfo.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) { throw "Could not start $Label." }
        if (-not $process.WaitForExit($ChildTimeoutSeconds * 1000)) {
            try { $process.Kill($true) } finally { $process.WaitForExit() }
            throw "$Label exceeded the exact-child timeout of $ChildTimeoutSeconds seconds."
        }
        return $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
}

function Assert-DoubleEqual {
    param([double]$Expected, [double]$Actual, [string]$Label)
    if (-not [double]::IsFinite($Actual) -or [math]::Abs($Expected - $Actual) -gt 0.000000001) {
        throw "$Label mismatch: expected $Expected, got $Actual."
    }
}

function Assert-Near {
    param([double]$Expected, [double]$Actual, [double]$Tolerance, [string]$Label)
    if (-not [double]::IsFinite($Actual) -or [math]::Abs($Expected - $Actual) -gt $Tolerance) {
        throw "$Label mismatch: expected $Expected +/- $Tolerance, got $Actual."
    }
}

function Get-EnvironmentNormalizedText {
    param([object]$Environment)
    if ($null -eq $Environment -or $Environment.schema -ne $environmentSchema -or
        -not [bool]$Environment.headless) {
        throw 'Accepted-scene environment identity must be complete and explicitly headless.'
    }
    $stringFields = @(
        'godotVersion', 'osName', 'osDescription', 'osVersion', 'osArchitecture',
        'processArchitecture', 'dotnetRuntime', 'cpuModel', 'displayServer',
        'renderingMethod', 'renderingDriver', 'renderingAdapter', 'viewportWidth',
        'viewportHeight', 'audioDriver'
    )
    foreach ($field in $stringFields) {
        $value = [string]$Environment.$field
        if ([string]::IsNullOrWhiteSpace($value) -or $value.Length -gt 160) {
            throw "Accepted-scene environment field '$field' is empty or unbounded."
        }
        foreach ($character in $value.ToCharArray()) {
            if ([char]::IsControl($character)) {
                throw "Accepted-scene environment field '$field' contains a control character."
            }
        }
    }
    if ([string]$Environment.displayServer -cne 'headless' -or
        [string]$Environment.viewportWidth -cne 'unavailable_headless' -or
        [string]$Environment.viewportHeight -cne 'unavailable_headless') {
        throw 'Accepted-scene environment must identify the headless display and unavailable headless viewport.'
    }
    if ([int]$Environment.logicalProcessorCount -le 0 -or
        [int]$Environment.logicalProcessorCount -gt 4096 -or
        [int]$Environment.physicsTicksPerSecond -le 0 -or
        [int]$Environment.physicsTicksPerSecond -gt 1000 -or
        [int]$Environment.maxFps -lt 0 -or [int]$Environment.maxFps -gt 100000 -or
        -not [double]::IsFinite([double]$Environment.timeScale) -or
        [double]$Environment.timeScale -le 0.0 -or
        -not [double]::IsFinite([double]$Environment.physicsJitterFix) -or
        [double]$Environment.physicsJitterFix -lt 0.0 -or
        [int]$Environment.maxPhysicsStepsPerFrame -le 0 -or
        [int]$Environment.maxPhysicsStepsPerFrame -gt 1000) {
        throw 'Accepted-scene environment numeric field is invalid or unbounded.'
    }
    $invariant = [System.Globalization.CultureInfo]::InvariantCulture
    return @(
        "schema=$($Environment.schema)",
        "godotVersion=$($Environment.godotVersion)",
        "osName=$($Environment.osName)",
        "osDescription=$($Environment.osDescription)",
        "osVersion=$($Environment.osVersion)",
        "osArchitecture=$($Environment.osArchitecture)",
        "processArchitecture=$($Environment.processArchitecture)",
        "dotnetRuntime=$($Environment.dotnetRuntime)",
        "cpuModel=$($Environment.cpuModel)",
        "logicalProcessorCount=$([int]$Environment.logicalProcessorCount)",
        "displayServer=$($Environment.displayServer)",
        "renderingMethod=$($Environment.renderingMethod)",
        "renderingDriver=$($Environment.renderingDriver)",
        "renderingAdapter=$($Environment.renderingAdapter)",
        "viewportWidth=$($Environment.viewportWidth)",
        "viewportHeight=$($Environment.viewportHeight)",
        "audioDriver=$($Environment.audioDriver)",
        'headless=true',
        "physicsTicksPerSecond=$([int]$Environment.physicsTicksPerSecond)",
        "maxFps=$([int]$Environment.maxFps)",
        "timeScale=$(([double]$Environment.timeScale).ToString('R', $invariant))",
        "physicsJitterFix=$(([double]$Environment.physicsJitterFix).ToString('R', $invariant))",
        "maxPhysicsStepsPerFrame=$([int]$Environment.maxPhysicsStepsPerFrame)"
    ) -join "`n"
}

function Assert-Environment {
    param([object]$Environment, [string]$Label)
    $normalized = Get-EnvironmentNormalizedText $Environment
    $hash = [string]$Environment.identitySha256
    if ($hash -cnotmatch '^[0-9a-f]{64}$') {
        throw "$Label environment identity hash is missing or malformed."
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($normalized)
    $expected = [System.Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    if ($hash -cne $expected) {
        throw "$Label environment identity hash mismatch."
    }
    return $normalized
}

function Get-NearestRank {
    param([double[]]$Sorted, [double]$Percentile)
    return $Sorted[[math]::Ceiling($Percentile * $Sorted.Count) - 1]
}

function Assert-Statistics {
    param([double[]]$Samples, [object]$Statistics, [string]$Label)
    if ($Samples.Count -le 0 -or [int]$Statistics.count -ne $Samples.Count) {
        throw "$Label statistics count mismatch."
    }
    [double[]]$sorted = @($Samples | Sort-Object)
    $total = [double](($Samples | Measure-Object -Sum).Sum)
    Assert-DoubleEqual ($total / $Samples.Count) ([double]$Statistics.meanMilliseconds) "$Label mean"
    Assert-DoubleEqual (Get-NearestRank $sorted 0.50) ([double]$Statistics.p50Milliseconds) "$Label p50"
    Assert-DoubleEqual (Get-NearestRank $sorted 0.95) ([double]$Statistics.p95Milliseconds) "$Label p95"
    Assert-DoubleEqual (Get-NearestRank $sorted 0.99) ([double]$Statistics.p99Milliseconds) "$Label p99"
    Assert-DoubleEqual $sorted[-1] ([double]$Statistics.maximumMilliseconds) "$Label maximum"
    Assert-DoubleEqual $total ([double]$Statistics.totalMilliseconds) "$Label total"
}

function Assert-IntervalSeries {
    param(
        [object]$Series,
        [string]$Label,
        [string]$MetricId,
        [int]$ExpectedCount,
        [int]$MaximumCount)
    $timestamps = @($Series.rawTimestamps)
    $ordinals = @($Series.rawSignalOrdinals)
    $samples = @($Series.rawIntervalMilliseconds | ForEach-Object { [double]$_ })
    $phaseCodes = @($Series.rawSampleRoutePhaseCodes | ForEach-Object { [int]$_ })
    $legCodes = @($Series.rawSampleLegCodes | ForEach-Object { [int]$_ })
    $frequency = [long]$Series.timestampFrequencyHertz
    if ($Series.metricId -ne $MetricId -or $frequency -le 0 -or $samples.Count -le 0 -or
        $samples.Count -gt $MaximumCount -or
        ($ExpectedCount -gt 0 -and $samples.Count -ne $ExpectedCount) -or
        $timestamps.Count -ne ($samples.Count + 1) -or
        $ordinals.Count -ne ($samples.Count + 1) -or
        $phaseCodes.Count -ne $samples.Count -or $legCodes.Count -ne $samples.Count) {
        throw "$Label raw series count/frequency is invalid or unbounded."
    }
    for ($index = 0; $index -lt $samples.Count; $index++) {
        $start = [long]$timestamps[$index]
        $end = [long]$timestamps[$index + 1]
        if ($end -le $start -or -not [double]::IsFinite($samples[$index]) -or $samples[$index] -le 0) {
            throw "$Label contains missing, non-finite, or non-monotonic raw evidence."
        }
        if (([uint64]$ordinals[$index + 1] - [uint64]$ordinals[$index]) -ne 1) {
            throw "$Label raw signal ordinals are not consecutive unit steps at sample $index."
        }
        $derived = ($end - $start) * 1000.0 / $frequency
        Assert-DoubleEqual $derived $samples[$index] "$Label raw interval $index"
    }
    if (@($samples | Select-Object -Unique).Count -le 1) {
        throw "$Label raw interval series is degenerate."
    }
    if (@($phaseCodes | Where-Object { $_ -notin @(0, 1) }).Count -gt 0 -or
        @($legCodes | Where-Object { $_ -lt 0 -or $_ -gt 4 }).Count -gt 0) {
        throw "$Label route phase/leg codes are invalid."
    }
    [double[]]$activeSamples = @(for ($index = 0; $index -lt $samples.Count; $index++) {
        if ($phaseCodes[$index] -eq 1) { $samples[$index] }
    })
    if ($activeSamples.Count -le 0 -or [int]$Series.activeRouteSampleCount -ne $activeSamples.Count) {
        throw "$Label active-route subset is missing or miscounted."
    }
    Assert-Statistics $samples $Series.statistics $Label
    Assert-Statistics $activeSamples $Series.activeRouteStatistics "$Label active-route subset"
}

function Assert-RouteTrace {
    param([object]$Trace, [string]$Label)
    if ($Trace.sceneTreePaused -or -not $Trace.managerProcessActive -or
        -not $Trace.playerPhysicsProcessActive) {
        throw "$Label did not execute with live process/physics state."
    }
    $processStart = [uint64]$Trace.processFrameStart
    $processEnd = [uint64]$Trace.processFrameEnd
    $physicsStart = [uint64]$Trace.physicsFrameStart
    $physicsEnd = [uint64]$Trace.physicsFrameEnd
    if ($processEnd -le $processStart -or $physicsEnd -le $physicsStart -or
        ($physicsEnd - $physicsStart) -lt $MeasuredFrames) {
        throw "$Label process/physics frame counters did not advance."
    }
    $checkpoints = @($Trace.legCheckpoints)
    $legs = @('move_right', 'move_forward', 'move_left', 'move_backward')
    $pitch = @(-8.0, -12.0, -8.0, -12.0)
    $yaw = @(0.0, 18.0, 0.0, -18.0)
    if ($checkpoints.Count -ne 4) { throw "$Label checkpoint count mismatch." }
    $previousX = [double]$Trace.startPlayerX
    $previousY = [double]$Trace.startPlayerY
    $previousZ = [double]$Trace.startPlayerZ
    $minimum = [double]::PositiveInfinity
    for ($index = 0; $index -lt 4; $index++) {
        $checkpoint = $checkpoints[$index]
        if ($checkpoint.legId -ne $legs[$index] -or
            [int]$checkpoint.completedFrameCount -ne (($index + 1) * 10)) {
            throw "$Label checkpoint identity mismatch."
        }
        Assert-Near $pitch[$index] ([double]$checkpoint.cameraPitchDegrees) 0.001 "$Label camera pitch"
        Assert-Near $yaw[$index] ([double]$checkpoint.cameraYawDegrees) 0.001 "$Label camera yaw"
        Assert-Near 0.0 ([double]$checkpoint.cameraRollDegrees) 0.001 "$Label camera roll"
        $dx = [double]$checkpoint.playerX - $previousX
        $dy = [double]$checkpoint.playerY - $previousY
        $dz = [double]$checkpoint.playerZ - $previousZ
        $minimum = [math]::Min($minimum, [math]::Sqrt(($dx * $dx) + ($dy * $dy) + ($dz * $dz)))
        $previousX = [double]$checkpoint.playerX
        $previousY = [double]$checkpoint.playerY
        $previousZ = [double]$checkpoint.playerZ
    }
    Assert-DoubleEqual $minimum ([double]$Trace.minimumObservedLegDisplacementMeters) "$Label minimum displacement"
    if ($minimum -lt 0.25 -or [double]$Trace.minimumRequiredLegDisplacementMeters -ne 0.25) {
        throw "$Label did not clear the minimum per-leg displacement contract."
    }
}

function Get-CheckpointIdentity {
    param([object]$Trace)
    return ([ordered]@{
        start = @([double]$Trace.startPlayerX, [double]$Trace.startPlayerY, [double]$Trace.startPlayerZ)
        minimum = [double]$Trace.minimumObservedLegDisplacementMeters
        checkpoints = @($Trace.legCheckpoints)
    } | ConvertTo-Json -Depth 8 -Compress)
}

function Assert-Trial {
    param([object]$Trial, [string]$Mode, [int]$TrialIndex, [int]$FixedFps)
    if ($Trial.schema -ne $trialSchema -or $Trial.route.routeId -ne $routeId -or
        $Trial.route.scenePath -ne 'res://scenes/snow_globe_voxel_foundation.tscn' -or
        $Trial.route.trialMode -ne $Mode -or [int]$Trial.route.fixedFps -ne $FixedFps -or
        [int]$Trial.route.trialIndex -ne $TrialIndex -or
        $Trial.route.sourceSha -ne $sourceSha -or $Trial.route.sourceTree -ne $sourceTree -or
        $Trial.route.sourceStateIdentity -ne $sourceStateIdentity -or
        $Trial.route.managedAssemblyConfiguration -ne 'ExportRelease' -or
        $Trial.route.managedAssemblySha256 -ne $managedAssemblySha256 -or
        -not $Trial.route.verifiedExportReleaseExecution) {
        throw "$Mode trial $TrialIndex failed mode/source/tree/assembly/scene/route validation."
    }
    if ([bool]$Trial.route.sourceDirty -ne $sourceDirty -or
        [bool]$Trial.route.dirtySourceOverrideUsed -ne $AllowDirtySourceForSmoke.IsPresent) {
        throw "$Mode trial $TrialIndex source cleanliness/override mismatch."
    }
    [void](Assert-Environment $Trial.environment "$Mode trial $TrialIndex")
    if ($Trial.scenario.scenarioId -ne 'snow_globe_voxel' -or
        [int]$Trial.scenario.simulationSeed -ne 260827 -or
        [int]$Trial.scenario.declaredInitialCitizens -ne 0 -or
        [int]$Trial.scenario.runtimeCitizenCount -ne 0 -or
        [int]$Trial.scenario.runtimeResourceCount -ne 0 -or
        [int]$Trial.scenario.runtimeStructureCount -ne 0 -or
        [int]$Trial.scenario.runtimeBuildQueueCount -ne 0) {
        throw "$Mode trial $TrialIndex scenario characterization mismatch."
    }
    if ([int]$Trial.collisions.initialBodyCount -ne 64 -or
        [int]$Trial.collisions.initialShapeCount -ne 12777 -or
        [int]$Trial.collisions.afterEditBodyCount -ne 64 -or
        [int]$Trial.collisions.afterEditShapeCount -ne 12781) {
        throw "$Mode trial $TrialIndex collision characterization mismatch."
    }
    Assert-RouteTrace $Trial.routeExecution.primary "$Mode trial $TrialIndex primary route"
    if (-not $Trial.persistence.instrumentationExcludedFromAuthority -or
        -not $Trial.persistence.snapshotWritten -or -not $Trial.persistence.snapshotReloaded -or
        $Trial.persistence.afterEditStateIdentity -ne $Trial.persistence.reloadedStateIdentity) {
        throw "$Mode trial $TrialIndex persistence/instrumentation validation failed."
    }

    if ($Mode -eq $realtimeMode) {
        Assert-IntervalSeries $Trial.timing.frameIntervals "trial $TrialIndex process cadence" `
            'process_frame_start_interval_ms' 0 7200
        Assert-IntervalSeries $Trial.timing.physicsIntervals "trial $TrialIndex physics cadence" `
            'physics_frame_start_interval_ms' $MeasuredFrames $MeasuredFrames
        $processTimestamps = @($Trial.timing.frameIntervals.rawTimestamps)
        $physicsTimestamps = @($Trial.timing.physicsIntervals.rawTimestamps)
        $processOrdinals = @($Trial.timing.frameIntervals.rawSignalOrdinals | ForEach-Object { [uint64]$_ })
        if ([long]$processTimestamps[0] -lt [long]$physicsTimestamps[0] -or
            [long]$processTimestamps[-1] -gt [long]$physicsTimestamps[-1]) {
            throw "Real-time trial $TrialIndex process cadence escaped the physics-bounded window."
        }
        if ([uint64]$Trial.routeExecution.primary.processFrameStart -gt $processOrdinals[0] -or
            [uint64]$Trial.routeExecution.primary.processFrameEnd -lt $processOrdinals[-1]) {
            throw "Real-time trial $TrialIndex process trace does not cover raw process ordinals."
        }
        $physicsPhases = @($Trial.timing.physicsIntervals.rawSampleRoutePhaseCodes)
        $physicsLegs = @($Trial.timing.physicsIntervals.rawSampleLegCodes)
        for ($index = 0; $index -lt $MeasuredFrames; $index++) {
            $expectedPhase = if ($index -lt 40) { 1 } else { 0 }
            $expectedLeg = if ($index -lt 40) { [math]::Floor($index / 10) + 1 } else { 0 }
            if ([int]$physicsPhases[$index] -ne $expectedPhase -or
                [int]$physicsLegs[$index] -ne $expectedLeg) {
                throw "Real-time trial $TrialIndex physics route tag mismatch at sample $index."
            }
        }
        $frameP95 = [double]$Trial.timing.frameIntervals.statistics.p95Milliseconds
        $physicsP95 = [double]$Trial.timing.physicsIntervals.statistics.p95Milliseconds
        $assessed = [math]::Max($frameP95, $physicsP95)
        Assert-DoubleEqual $assessed ([double]$Trial.timing.assessedP95Milliseconds) "trial $TrialIndex assessed p95"
        $raw = if ($assessed -gt 33.33) { 'safety_failure' } elseif ($assessed -le 16.67) { 'target_passed' } else { 'target_missed' }
        $eligible = [bool]$Trial.route.verifiedExportReleaseExecution -and
            [bool]$Trial.environment.headless -and
            -not [bool]$Trial.route.sourceDirty -and -not [bool]$Trial.route.dirtySourceOverrideUsed
        if ([bool]$Trial.timing.targetSafetyClaimEligible -ne $eligible -or
            $Trial.timing.rawThresholdClassification -ne $raw) {
            throw "Real-time trial $TrialIndex claim eligibility/raw classification mismatch."
        }
        $expectedClassification = if ($eligible) { $raw } else { 'not_applied_characterization_only' }
        $expectedStatus = if ($eligible) {
            if ($raw -eq 'safety_failure') { 'characterized_safety_failure' } else { 'characterized' }
        } else { 'smoke_characterized_dirty_source' }
        if ($Trial.timing.classification -ne $expectedClassification -or $Trial.status -ne $expectedStatus) {
            throw "Real-time trial $TrialIndex silently downgraded or mislabeled status/classification."
        }
        $backlog = @($Trial.backlog.rawPendingSimulationTickSamples | ForEach-Object { [double]$_ })
        $backlogOrdinals = @($Trial.backlog.rawProcessFrameOrdinals | ForEach-Object { [uint64]$_ })
        $processSampleCount = @($Trial.timing.frameIntervals.rawIntervalMilliseconds).Count
        if ($backlog.Count -ne $processSampleCount -or $backlogOrdinals.Count -ne $processSampleCount -or
            [int]$Trial.backlog.sampleCount -ne $processSampleCount -or
            @($backlog | Where-Object { $_ -lt 0 }).Count -gt 0) {
            throw "Real-time trial $TrialIndex backlog raw samples are invalid."
        }
        for ($index = 0; $index -lt $processSampleCount; $index++) {
            if ($backlogOrdinals[$index] -ne $processOrdinals[$index + 1]) {
                throw "Real-time trial $TrialIndex backlog ordinal mismatch at sample $index."
            }
        }
        [double[]]$sortedBacklog = @($backlog | Sort-Object)
        Assert-DoubleEqual (Get-NearestRank $sortedBacklog 0.50) ([double]$Trial.backlog.p50PendingSimulationTicks) "backlog p50"
        Assert-DoubleEqual (Get-NearestRank $sortedBacklog 0.95) ([double]$Trial.backlog.p95PendingSimulationTicks) "backlog p95"
        Assert-DoubleEqual $sortedBacklog[-1] ([double]$Trial.backlog.maximumPendingSimulationTicks) "backlog maximum"
        if ($Trial.persistence.routeReplayed -or $null -ne $Trial.routeExecution.replay) {
            throw "Real-time trial $TrialIndex contains fixed-delta replay claims."
        }
    }
    else {
        if ($Trial.status -ne $(if ($sourceDirty) { 'identity_replay_verified_dirty_source_smoke' } else { 'identity_replay_verified' }) -or
            $Trial.timing.classification -ne 'not_applicable_identity_only' -or
            $Trial.timing.targetSafetyClaimEligible -or
            @($Trial.timing.frameIntervals.rawTimestamps).Count -ne 0 -or
            @($Trial.timing.frameIntervals.rawSignalOrdinals).Count -ne 0 -or
            @($Trial.timing.frameIntervals.rawSampleRoutePhaseCodes).Count -ne 0 -or
            @($Trial.timing.physicsIntervals.rawTimestamps).Count -ne 0 -or
            @($Trial.timing.physicsIntervals.rawSignalOrdinals).Count -ne 0 -or
            @($Trial.timing.physicsIntervals.rawSampleRoutePhaseCodes).Count -ne 0 -or
            @($Trial.backlog.rawProcessFrameOrdinals).Count -ne 0 -or
            -not $Trial.persistence.routeReplayed -or $null -eq $Trial.routeExecution.replay) {
            throw "Fixed-delta trial $TrialIndex status/timing/replay contract mismatch."
        }
        Assert-RouteTrace $Trial.routeExecution.replay "fixed-delta trial $TrialIndex replay route"
        if ((Get-CheckpointIdentity $Trial.routeExecution.primary) -cne
            (Get-CheckpointIdentity $Trial.routeExecution.replay) -or
            $Trial.persistence.measurementStartStateIdentity -ne $Trial.persistence.replayedMeasurementStartStateIdentity -or
            $Trial.persistence.measurementEndStateIdentity -ne $Trial.persistence.replayedMeasurementEndStateIdentity -or
            $Trial.persistence.afterEditStateIdentity -ne $Trial.persistence.replayedStateIdentity) {
            throw "Fixed-delta trial $TrialIndex replay/checkpoint identity mismatch."
        }
    }
}

if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $command = Get-Command godot -ErrorAction SilentlyContinue
    if ($null -ne $command) { $GodotPath = $command.Source }
}
if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $wingetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    $GodotPath = Get-ChildItem -LiteralPath $wingetRoot -Recurse -Filter 'Godot*_console.exe' -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if ([string]::IsNullOrWhiteSpace($GodotPath) -or -not (Test-Path -LiteralPath $GodotPath -PathType Leaf)) {
    throw 'Godot 4.6.2 Mono was not found. Pass -GodotPath with its full executable path.'
}
$GodotPath = [System.IO.Path]::GetFullPath($GodotPath)
$version = (& $GodotPath --version | Select-Object -First 1).Trim()
if ($version -notmatch '^4\.6\.2(?:\.|\b)') {
    throw "Packet 01 requires Godot 4.6.2; resolved version was '$version'."
}

$branch = (git -C $repositoryRoot branch --show-current).Trim()
$sourceSha = (git -C $repositoryRoot rev-parse HEAD).Trim()
$sourceTree = (git -C $repositoryRoot rev-parse 'HEAD^{tree}').Trim()
if ($branch -ne 'feature/social-kernel-01-baseline') {
    throw "Source branch mismatch: expected feature/social-kernel-01-baseline, got '$branch'."
}
git -C $repositoryRoot merge-base --is-ancestor $baseSha $sourceSha
if ($LASTEXITCODE -ne 0) {
    throw "Source HEAD $sourceSha does not descend from Packet 01 base $baseSha."
}
$statusBefore = @(Get-RepositoryStatus)
$sourceDirty = $statusBefore.Count -gt 0
if ($sourceDirty -and -not $AllowDirtySourceForSmoke) {
    throw 'Canonical Packet 01 performance evidence requires clean source. Use -AllowDirtySourceForSmoke only for local smoke validation.'
}
if (-not $sourceDirty -and $AllowDirtySourceForSmoke) {
    throw 'Dirty-source override is forbidden for clean trials because it would silently downgrade claim eligibility.'
}
if ($FixedDeltaOnlyDiagnostic -and $RealtimeOnlyDiagnostic) {
    throw 'Fixed-delta-only and real-time-only diagnostic modes are mutually exclusive.'
}
if (($FixedDeltaOnlyDiagnostic -or $RealtimeOnlyDiagnostic) -and -not $AllowDirtySourceForSmoke) {
    throw 'Selective diagnostic modes require -AllowDirtySourceForSmoke and cannot emit canonical claims.'
}

$sourceStateIdentity = $sourceSha + ':' + $sourceTree
$repositoryContentIdentityBefore = Get-RepositoryContentIdentity
if ($sourceDirty) {
    $sourceStateIdentity = 'dirty-smoke:' + $repositoryContentIdentityBefore
}

if (Test-Path -LiteralPath $outputRoot) {
    throw "Packet 01 output directory already exists: $outputRoot"
}
[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$releaseDirectory = Join-Path $outputRoot 'release-runner'
[System.IO.Directory]::CreateDirectory($releaseDirectory) | Out-Null
$releaseExecutable = Join-Path $releaseDirectory 'SocietiesAcceptedSceneBaseline.exe'
$exportExitCode = Invoke-ExactChild $GodotPath @(
    '--headless', '--path', $projectRoot, '--export-release', $preset, $releaseExecutable
) $repositoryRoot 'Godot accepted-scene Release export'
if ($exportExitCode -ne 0) {
    throw "Godot accepted-scene Release export exited with code $exportExitCode."
}
$consoleWrapper = Join-Path $releaseDirectory 'SocietiesAcceptedSceneBaseline.console.exe'
$packagedAssemblies = @(Get-ChildItem -LiteralPath $releaseDirectory -Recurse -Filter 'Societies.dll')
if (-not (Test-Path -LiteralPath $consoleWrapper -PathType Leaf) -or $packagedAssemblies.Count -ne 1) {
    throw 'Accepted-scene ExportRelease wrapper or managed assembly is missing.'
}
$assemblyPath = $packagedAssemblies[0].FullName
$managedAssemblySha256 = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash.ToLowerInvariant()
$repositoryContentIdentityAfter = Get-RepositoryContentIdentity
if ($repositoryContentIdentityBefore -ne $repositoryContentIdentityAfter) {
    throw 'Godot export changed tracked or untracked source state; refusing mixed-identity evidence.'
}
$statusAfterExport = @(Get-RepositoryStatus)
Assert-SameSequence $statusBefore $statusAfterExport 'Git status after export'

$performanceTrials = [System.Collections.Generic.List[object]]::new()
$identityTrials = [System.Collections.Generic.List[object]]::new()
$safetyExitObserved = $false
if (-not $FixedDeltaOnlyDiagnostic) {
$performanceTrialCount = if ($RealtimeOnlyDiagnostic) { 1 } else { 3 }
for ($trialIndex = 1; $trialIndex -le $performanceTrialCount; $trialIndex++) {
    $trialDirectory = Join-Path $outputRoot "realtime-trial-$trialIndex"
    [System.IO.Directory]::CreateDirectory($trialDirectory) | Out-Null
    $arguments = @(
        '--headless',
        '--audio-driver', 'Dummy',
        '--',
        '--output-dir', $trialDirectory,
        '--trial-index', "$trialIndex",
        '--trial-mode', $realtimeMode,
        '--fixed-fps', '0',
        '--warmup-frames', "$WarmupFrames",
        '--measured-frames', "$MeasuredFrames",
        '--base-sha', $baseSha,
        '--source-sha', $sourceSha,
        '--source-tree', $sourceTree,
        '--source-state-identity', $sourceStateIdentity,
        '--source-dirty', $sourceDirty.ToString().ToLowerInvariant(),
        '--dirty-source-override', $AllowDirtySourceForSmoke.IsPresent.ToString().ToLowerInvariant(),
        '--managed-assembly-sha256', $managedAssemblySha256
    )
    $trialExitCode = Invoke-ExactChild $consoleWrapper $arguments $releaseDirectory "real-time trial $trialIndex"
    if ($trialExitCode -eq 2) {
        $safetyExitObserved = $true
    } elseif ($trialExitCode -ne 0) {
        throw "Accepted-scene real-time trial $trialIndex failed with exit code $trialExitCode."
    }
    $resultPath = Join-Path $trialDirectory 'accepted-scene-baseline-trial-v4.json'
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "Accepted-scene real-time trial $trialIndex did not emit its JSON artifact."
    }
    $trial = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    Assert-Trial $trial $realtimeMode $trialIndex 0
    $performanceTrials.Add($trial)
}
}

if ($RealtimeOnlyDiagnostic) {
    $repositoryContentIdentityFinal = Get-RepositoryContentIdentity
    if ($repositoryContentIdentityBefore -ne $repositoryContentIdentityFinal) {
        throw 'Diagnostic trial execution changed tracked or untracked source state.'
    }
    $statusAfterTrials = @(Get-RepositoryStatus)
    Assert-SameSequence $statusBefore $statusAfterTrials 'Git status after diagnostic trial'
    Write-Host "Real-time-only diagnostic artifact: $(Join-Path $outputRoot 'realtime-trial-1\accepted-scene-baseline-trial-v4.json')"
    return
}

$identityTrialCount = if ($FixedDeltaOnlyDiagnostic) { 1 } else { 3 }
for ($trialIndex = 1; $trialIndex -le $identityTrialCount; $trialIndex++) {
    $trialDirectory = Join-Path $outputRoot "fixed-delta-identity-trial-$trialIndex"
    [System.IO.Directory]::CreateDirectory($trialDirectory) | Out-Null
    $arguments = @(
        '--headless',
        '--fixed-fps', '60',
        '--audio-driver', 'Dummy',
        '--',
        '--output-dir', $trialDirectory,
        '--trial-index', "$trialIndex",
        '--trial-mode', $identityMode,
        '--fixed-fps', '60',
        '--warmup-frames', "$WarmupFrames",
        '--measured-frames', "$MeasuredFrames",
        '--base-sha', $baseSha,
        '--source-sha', $sourceSha,
        '--source-tree', $sourceTree,
        '--source-state-identity', $sourceStateIdentity,
        '--source-dirty', $sourceDirty.ToString().ToLowerInvariant(),
        '--dirty-source-override', $AllowDirtySourceForSmoke.IsPresent.ToString().ToLowerInvariant(),
        '--managed-assembly-sha256', $managedAssemblySha256
    )
    $trialExitCode = Invoke-ExactChild $consoleWrapper $arguments $releaseDirectory "fixed-delta identity trial $trialIndex"
    if ($trialExitCode -ne 0) {
        throw "Accepted-scene fixed-delta identity trial $trialIndex failed with exit code $trialExitCode."
    }
    $resultPath = Join-Path $trialDirectory 'accepted-scene-baseline-trial-v4.json'
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "Accepted-scene fixed-delta identity trial $trialIndex did not emit its JSON artifact."
    }
    $trial = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    Assert-Trial $trial $identityMode $trialIndex 60
    $identityTrials.Add($trial)
}

if ($FixedDeltaOnlyDiagnostic) {
    $repositoryContentIdentityFinal = Get-RepositoryContentIdentity
    if ($repositoryContentIdentityBefore -ne $repositoryContentIdentityFinal) {
        throw 'Diagnostic trial execution changed tracked or untracked source state.'
    }
    $statusAfterTrials = @(Get-RepositoryStatus)
    Assert-SameSequence $statusBefore $statusAfterTrials 'Git status after diagnostic trial'
    Write-Host "Fixed-delta-only diagnostic artifact: $(Join-Path $outputRoot 'fixed-delta-identity-trial-1\accepted-scene-baseline-trial-v4.json')"
    return
}

$allTrials = @($performanceTrials) + @($identityTrials)
$sharedEnvironmentText = Assert-Environment $allTrials[0].environment 'shared bundle'
$sharedEnvironmentHash = [string]$allTrials[0].environment.identitySha256
foreach ($trial in $allTrials) {
    if ((Get-EnvironmentNormalizedText $trial.environment) -cne $sharedEnvironmentText -or
        [string]$trial.environment.identitySha256 -cne $sharedEnvironmentHash) {
        throw 'Environment identity differs across real-time and fixed-delta trials.'
    }
}
$sharedEnvironmentIdentityAcrossAllTrials = $true
$checkpointIdentities = @($allTrials | ForEach-Object {
    Get-CheckpointIdentity $_.routeExecution.primary
} | Select-Object -Unique)
if ($checkpointIdentities.Count -ne 1) {
    throw 'Primary route player/camera checkpoints differ across real-time and fixed-delta trials.'
}
foreach ($propertyPath in @(
    'scenario.initialStateIdentity',
    'persistence.measurementStartStateIdentity',
    'persistence.measurementEndStateIdentity',
    'persistence.afterEditStateIdentity'
)) {
    $segments = $propertyPath.Split('.')
    $values = @($identityTrials | ForEach-Object {
        $value = $_
        foreach ($segment in $segments) { $value = $value.$segment }
        $value
    } | Select-Object -Unique)
    if ($values.Count -ne 1) {
        throw "Fixed-delta identity trials did not reproduce matching $propertyPath."
    }
}

$processFrameP95 = @($performanceTrials | ForEach-Object {
    [double]$_.timing.frameIntervals.statistics.p95Milliseconds
} | Sort-Object)
$physicsFrameP95 = @($performanceTrials | ForEach-Object {
    [double]$_.timing.physicsIntervals.statistics.p95Milliseconds
} | Sort-Object)
$medianProcessFrameP95 = $processFrameP95[1]
$medianPhysicsFrameP95 = $physicsFrameP95[1]
$worstProcessFrameP95 = $processFrameP95[-1]
$worstPhysicsFrameP95 = $physicsFrameP95[-1]
$secondaryWorstAssessedP95 = [math]::Max($worstProcessFrameP95, $worstPhysicsFrameP95)
$rawClassification = if ($worstProcessFrameP95 -gt 33.33 -or $worstPhysicsFrameP95 -gt 33.33) {
    'safety_failure'
} elseif ($worstProcessFrameP95 -le 16.67 -and $worstPhysicsFrameP95 -le 16.67) {
    'target_passed'
} else {
    'target_missed'
}
$claimEligible = -not $sourceDirty -and -not $AllowDirtySourceForSmoke -and
    $sharedEnvironmentIdentityAcrossAllTrials -and
    @($performanceTrials | Where-Object { -not $_.timing.targetSafetyClaimEligible }).Count -eq 0
$classification = if ($claimEligible) { $rawClassification } else { 'not_applied_characterization_only' }
$repositoryContentIdentityFinal = Get-RepositoryContentIdentity
if ($repositoryContentIdentityBefore -ne $repositoryContentIdentityFinal) {
    throw 'Trial execution changed tracked or untracked source state; refusing mixed-identity evidence.'
}
$statusAfterTrials = @(Get-RepositoryStatus)
Assert-SameSequence $statusBefore $statusAfterTrials 'Git status after trials'
$bundle = [ordered]@{
    schema = $bundleSchema
    status = if ($claimEligible) {
        if ($rawClassification -eq 'safety_failure') { 'characterized_safety_failure' } else { 'characterized' }
    } else {
        'smoke_characterized_dirty_source'
    }
    source = [ordered]@{
        baseSha = $baseSha
        branch = $branch
        gitSha = $sourceSha
        gitTree = $sourceTree
        gitDirty = $sourceDirty
        dirtySourceOverrideUsed = $AllowDirtySourceForSmoke.IsPresent
        sourceStateIdentity = $sourceStateIdentity
        godotVersion = $version
        managedAssemblyConfiguration = 'ExportRelease'
        managedAssemblySha256 = $managedAssemblySha256
        postExportGitStatusMatched = $true
        postTrialGitStatusMatched = $true
        postExportContentIdentityMatched = $true
        postTrialContentIdentityMatched = $true
    }
    environment = $allTrials[0].environment
    environmentIdentitySha256 = $sharedEnvironmentHash
    route = [ordered]@{
        routeId = $routeId
        scenePath = 'res://scenes/snow_globe_voxel_foundation.tscn'
        exportPreset = $preset
        realtimePerformanceTrialCount = 3
        realtimePerformanceFixedFps = $null
        fixedDeltaIdentityTrialCount = 3
        fixedDeltaIdentityFixedFps = 60
        warmupFramesPerTrial = $WarmupFrames
        measuredPhysicsFrameIntervalsPerTrial = $MeasuredFrames
        maximumProcessFrameIntervalsPerTrial = 7200
        sharedEnvironmentIdentityAcrossAllTrials = $sharedEnvironmentIdentityAcrossAllTrials
        sharedSourceAssemblySceneAndRouteIdentity = $true
        primaryRouteCheckpointsEqualAcrossAllTrials = $true
        fixedDeltaStartingStateEqualAcrossTrials = $true
        fixedDeltaPostRouteStateEqualAcrossReplayAndTrials = $true
        fixedDeltaEditedStateEqualAcrossReplayAndTrials = $true
    }
    classification = [ordered]@{
        processFrameMetric = 'process_frame_start_interval_ms'
        physicsFrameMetric = 'physics_frame_start_interval_ms'
        metricSemantics = 'headless ExportRelease scheduling-inclusive callback-start wall-clock cadence; not CPU, GPU, render-thread, or whole-engine duration'
        medianProcessFrameP95Milliseconds = $medianProcessFrameP95
        medianPhysicsFrameP95Milliseconds = $medianPhysicsFrameP95
        worstProcessFrameP95Milliseconds = $worstProcessFrameP95
        worstPhysicsFrameP95Milliseconds = $worstPhysicsFrameP95
        secondaryWorstAssessedP95Milliseconds = $secondaryWorstAssessedP95
        productTargetP95Milliseconds = 16.67
        hardSafetyP95Milliseconds = 33.33
        targetSafetyClaimEligible = $claimEligible
        result = $classification
        rawThresholdClassification = $rawClassification
        historicalContextMilliseconds = 51.9392
        historicalContextClassification = 'historical_context_only'
    }
    realtimePerformanceTrials = @($performanceTrials)
    fixedDeltaIdentityTrials = @($identityTrials)
}
$bundlePath = Join-Path $outputRoot 'accepted-scene-baseline-v4.json'
[System.IO.File]::WriteAllText(
    $bundlePath,
    ($bundle | ConvertTo-Json -Depth 20),
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Accepted-scene baseline artifact: $bundlePath"
Write-Host "Classification: $classification; raw=$rawClassification; process median/worst p95=$medianProcessFrameP95/$worstProcessFrameP95 ms; physics median/worst p95=$medianPhysicsFrameP95/$worstPhysicsFrameP95 ms"
if ($claimEligible -and ($rawClassification -eq 'safety_failure' -or $safetyExitObserved)) {
    throw 'Accepted-scene baseline breached the 33.33 ms p95 hard-safety line. Evidence was emitted before failure.'
}
