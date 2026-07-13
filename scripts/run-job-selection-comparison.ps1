[CmdletBinding()]
param(
    [string]$Scenario = "balanced_basin",
    [int]$Seed = 1337,
    [int]$Citizens = 16,
    [int]$Ticks = 300,
    [int]$Trials = 3,
    [string]$ComparisonGroup,
    [string]$OutputRoot,
    [string]$GodotPath,
    [string]$ExportPreset = "Windows Performance Release",
    [switch]$ReleaseExport,
    [switch]$AllowDirtySource
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$pairScript = Join-Path $PSScriptRoot "run-performance-pair.ps1"
Set-Location $repoRoot

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
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

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-Median {
    param([Parameter(Mandatory = $true)][double[]]$Values)

    if ($Values.Count -lt 1) {
        throw "Cannot compute a median from an empty collection."
    }

    $ordered = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($ordered.Count / 2)
    if (($ordered.Count % 2) -eq 1) {
        return [double]$ordered[$middle]
    }

    return ([double]$ordered[$middle - 1] + [double]$ordered[$middle]) / 2.0
}

function Get-BundleIdentity {
    param([Parameter(Mandatory = $true)][string]$Root)

    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    $files = @(Get-ChildItem -LiteralPath $Root -File -Recurse | Sort-Object FullName)
    if ($files.Count -lt 3) {
        throw "The Release bundle is incomplete: $Root"
    }

    $entries = @($files | ForEach-Object {
        $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
        if (-not $fullPath.StartsWith($rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Release bundle file escaped its root: $fullPath"
        }
        $relativePath = $fullPath.Substring($rootPath.Length).Replace('\', '/')
        [ordered]@{
            path = $relativePath
            sizeBytes = $_.Length
            sha256 = Get-Sha256 $_.FullName
        }
    })
    $descriptor = ($entries | ForEach-Object { "$($_.path)|$($_.sizeBytes)|$($_.sha256)" }) -join "`n"
    $descriptorBytes = [System.Text.Encoding]::UTF8.GetBytes($descriptor)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $aggregateHash = -join ($sha256.ComputeHash($descriptorBytes) | ForEach-Object {
            $_.ToString("x2", [System.Globalization.CultureInfo]::InvariantCulture)
        })
    }
    finally {
        $sha256.Dispose()
    }

    return [ordered]@{
        root = [System.IO.Path]::GetFullPath($Root)
        fileCount = $entries.Count
        aggregateSha256 = $aggregateHash
        files = $entries
    }
}

if (-not $ReleaseExport) {
    throw "W1-05 comparison evidence requires -ReleaseExport."
}
if ($env:OS -ne "Windows_NT") {
    throw "The tracked Release comparison route currently supports Windows only."
}
if ($Citizens -lt 1 -or $Citizens -gt 256) {
    throw "Citizens must be between 1 and 256."
}
if ($Ticks -lt 1 -or $Ticks -gt 4096) {
    throw "Ticks must be between 1 and 4096."
}
if ($Trials -lt 3 -or ($Trials % 2) -eq 0) {
    throw "Trials must be an odd number of at least three so the p95 gate uses a stable median."
}

$safeScenario = $Scenario -replace '[^A-Za-z0-9._-]', '-'
if ([string]::IsNullOrWhiteSpace($safeScenario)) {
    $safeScenario = "scenario"
}
$resolvedComparisonGroup = if ([string]::IsNullOrWhiteSpace($ComparisonGroup)) {
    "$safeScenario-seed$Seed-c$Citizens-t$Ticks-job-selection"
}
else {
    $ComparisonGroup
}
if ($resolvedComparisonGroup.Length -gt 96 -or
    $resolvedComparisonGroup.Contains("..") -or
    $resolvedComparisonGroup -notmatch '^[A-Za-z0-9._-]+$') {
    throw "ComparisonGroup may contain only letters, digits, '.', '_' and '-' and may not contain '..' or exceed 96 characters."
}

if (-not $OutputRoot) {
    $timestamp = [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss-fff")
    $OutputRoot = Join-Path $repoRoot "artifacts\performance\$timestamp-w105-job-selection"
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) {
    throw "Job-selection comparison output already exists: $OutputRoot"
}
[System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null

$trialRecords = New-Object System.Collections.Generic.List[object]
$leafArtifacts = New-Object System.Collections.Generic.List[string]
$bundleReuseChecks = New-Object System.Collections.Generic.List[object]
$sharedRunner = $null
$releaseDirectory = $null
$initialBundleIdentity = $null

for ($trial = 1; $trial -le $Trials; $trial++) {
    $modeResults = @{}
    $trialSelectors = if (($trial % 2) -eq 1) {
        @("exhaustive_reference", "exact_branch_and_bound")
    }
    else {
        @("exact_branch_and_bound", "exhaustive_reference")
    }
    foreach ($selector in $trialSelectors) {
        $modeRoot = Join-Path $OutputRoot "trial-$trial\$selector"
        $arguments = @{
            Scenario = $Scenario
            Seed = $Seed
            Citizens = $Citizens
            Ticks = $Ticks
            WarmupTicks = 0
            CacheMode = "cold"
            SelectorMode = $selector
            ComparisonGroup = $resolvedComparisonGroup
            TrialIndex = $trial
            OutputRoot = $modeRoot
            ExportPreset = $ExportPreset
            AllowPrimarySafetyFailure = $true
        }
        if (-not [string]::IsNullOrWhiteSpace($GodotPath)) {
            $arguments.GodotPath = $GodotPath
        }
        if ($AllowDirtySource) {
            $arguments.AllowDirtySource = $true
        }

        $reuseIdentityBefore = $null
        if ($null -eq $sharedRunner) {
            $arguments.ReleaseExport = $true
        }
        else {
            $reuseIdentityBefore = Get-BundleIdentity $releaseDirectory
            if ($reuseIdentityBefore.aggregateSha256 -ne $initialBundleIdentity.aggregateSha256) {
                throw "The shared Release bundle changed before $selector trial $trial."
            }
            $arguments.ExistingReleaseRunner = $sharedRunner
        }

        & $pairScript @arguments

        $equivalencePath = Join-Path $modeRoot "equivalence-results.json"
        $offResultPath = Join-Path $modeRoot "metrics-off\perf-results.json"
        $onResultPath = Join-Path $modeRoot "metrics-on\perf-results.json"
        $equivalence = Read-JsonArtifact $equivalencePath "$selector trial $trial equivalence"
        $offResult = Read-JsonArtifact $offResultPath "$selector trial $trial metrics-off result"
        $onResult = Read-JsonArtifact $onResultPath "$selector trial $trial metrics-on result"

        if ($null -eq $sharedRunner) {
            $releaseDirectory = Join-Path $modeRoot "release-runner"
            $sharedRunner = Join-Path $releaseDirectory "SocietiesPerformance.console.exe"
            if (-not (Test-Path -LiteralPath $sharedRunner -PathType Leaf)) {
                throw "The first pair did not publish the reusable Release console runner."
            }
            $initialBundleIdentity = Get-BundleIdentity $releaseDirectory
        }
        else {
            $reuseIdentityAfter = Get-BundleIdentity $releaseDirectory
            $identityPreserved =
                $reuseIdentityBefore.aggregateSha256 -eq $initialBundleIdentity.aggregateSha256 -and
                $reuseIdentityAfter.aggregateSha256 -eq $initialBundleIdentity.aggregateSha256
            $bundleReuseChecks.Add([ordered]@{
                trialIndex = $trial
                selectorMode = $selector
                beforeSha256 = $reuseIdentityBefore.aggregateSha256
                afterSha256 = $reuseIdentityAfter.aggregateSha256
                identityPreserved = $identityPreserved
            })
            if (-not $identityPreserved) {
                throw "The shared Release bundle changed while running $selector trial $trial."
            }
        }

        $expectedPairStatus = if ($AllowDirtySource) { @("pass", "pass_dirty_source") } else { @("pass") }
        if ($equivalence.schemaVersion -ne 4 -or
            $expectedPairStatus -notcontains $equivalence.status -or
            $equivalence.releaseEnvironmentValid -ne $true -or
            $equivalence.selectorMode -ne $selector -or
            $offResult.schemaVersion -ne 4 -or
            $onResult.schemaVersion -ne 4 -or
            $offResult.configuration.selectorMode -ne $selector -or
            $onResult.configuration.selectorMode -ne $selector -or
            $offResult.environment.verifiedReleaseExecution -ne $true -or
            $onResult.environment.verifiedReleaseExecution -ne $true) {
            throw "$selector trial $trial did not satisfy the schema-v4 Release pair contract."
        }

        $leafArtifacts.Add($equivalencePath)
        $leafArtifacts.Add($offResultPath)
        $leafArtifacts.Add($onResultPath)
        $modeResults[$selector] = [ordered]@{
            equivalence = $equivalence
            offResult = $offResult
            onResult = $onResult
            equivalencePath = $equivalencePath
            offResultPath = $offResultPath
            onResultPath = $onResultPath
        }
    }

    $exhaustive = $modeResults["exhaustive_reference"]
    $optimized = $modeResults["exact_branch_and_bound"]
    $exhaustiveQueries = [long]$exhaustive.onResult.diagnostics.selectorExactPathQueries
    $optimizedQueries = [long]$optimized.onResult.diagnostics.selectorExactPathQueries
    if ($exhaustiveQueries -le 0) {
        throw "Exhaustive trial $trial reported no selector exact-path queries."
    }

    $reduction = 1.0 - ($optimizedQueries / [double]$exhaustiveQueries)
    $queryAccountingValid =
        $exhaustiveQueries -eq
            ([long]$exhaustive.onResult.diagnostics.selectorPathCacheHits +
             [long]$exhaustive.onResult.diagnostics.selectorPathCacheMisses) -and
        $optimizedQueries -eq
            ([long]$optimized.onResult.diagnostics.selectorPathCacheHits +
             [long]$optimized.onResult.diagnostics.selectorPathCacheMisses)
    $hashesMatch =
        $exhaustive.offResult.hashes.snapshotSha256 -eq $optimized.offResult.hashes.snapshotSha256 -and
        $exhaustive.offResult.hashes.eventLogSha256 -eq $optimized.offResult.hashes.eventLogSha256 -and
        $exhaustive.offResult.hashes.deterministicStateAndEventSha256 -eq
            $optimized.offResult.hashes.deterministicStateAndEventSha256

    $trialRecords.Add([ordered]@{
        trialIndex = $trial
        deterministicHashesMatch = $hashesMatch
        selectorQueryAccountingValid = $queryAccountingValid
        exactQueryReductionPassed = $reduction -ge 0.60
        exactQueryReduction = $reduction
        exhaustive = [ordered]@{
            tickP95Milliseconds = [double]$exhaustive.offResult.externalTickStatistics.p95Milliseconds
            selectorExactPathQueries = $exhaustiveQueries
            selectorPathCacheHits = [long]$exhaustive.onResult.diagnostics.selectorPathCacheHits
            selectorPathCacheMisses = [long]$exhaustive.onResult.diagnostics.selectorPathCacheMisses
            selectorCandidatesExactScored = [long]$exhaustive.onResult.diagnostics.selectorCandidatesExactScored
            selectorCandidatesPruned = [long]$exhaustive.onResult.diagnostics.selectorCandidatesPruned
            routeSelectionMilliseconds = [double]$exhaustive.onResult.diagnostics.phases.routeSelectionMilliseconds
            deterministicStateAndEventSha256 = $exhaustive.offResult.hashes.deterministicStateAndEventSha256
            metricsOffResult = $exhaustive.offResultPath
            metricsOnResult = $exhaustive.onResultPath
        }
        optimized = [ordered]@{
            tickP95Milliseconds = [double]$optimized.offResult.externalTickStatistics.p95Milliseconds
            selectorExactPathQueries = $optimizedQueries
            selectorPathCacheHits = [long]$optimized.onResult.diagnostics.selectorPathCacheHits
            selectorPathCacheMisses = [long]$optimized.onResult.diagnostics.selectorPathCacheMisses
            selectorCandidatesExactScored = [long]$optimized.onResult.diagnostics.selectorCandidatesExactScored
            selectorCandidatesPruned = [long]$optimized.onResult.diagnostics.selectorCandidatesPruned
            routeSelectionMilliseconds = [double]$optimized.onResult.diagnostics.phases.routeSelectionMilliseconds
            deterministicStateAndEventSha256 = $optimized.offResult.hashes.deterministicStateAndEventSha256
            metricsOffResult = $optimized.offResultPath
            metricsOnResult = $optimized.onResultPath
        }
    })
}

$records = $trialRecords.ToArray()
$bundleReuseCheckArray = $bundleReuseChecks.ToArray()
$leafArtifactArray = $leafArtifacts.ToArray()
$finalBundleIdentity = Get-BundleIdentity $releaseDirectory
$sourceShas = @($records | ForEach-Object {
    (Read-JsonArtifact $_.exhaustive.metricsOffResult "exhaustive source result").configuration.gitSha
    (Read-JsonArtifact $_.optimized.metricsOffResult "optimized source result").configuration.gitSha
} | Select-Object -Unique)
$sourceDirtyValues = @($records | ForEach-Object {
    (Read-JsonArtifact $_.exhaustive.metricsOffResult "exhaustive source result").configuration.gitDirty
    (Read-JsonArtifact $_.optimized.metricsOffResult "optimized source result").configuration.gitDirty
} | Select-Object -Unique)
$processPaths = @($records | ForEach-Object {
    (Read-JsonArtifact $_.exhaustive.metricsOffResult "exhaustive environment result").environment.processExecutablePath
    (Read-JsonArtifact $_.optimized.metricsOffResult "optimized environment result").environment.processExecutablePath
} | Select-Object -Unique)

$exhaustiveMedianP95 = Get-Median ([double[]]@($records | ForEach-Object { $_.exhaustive.tickP95Milliseconds }))
$optimizedMedianP95 = Get-Median ([double[]]@($records | ForEach-Object { $_.optimized.tickP95Milliseconds }))
$p95Limit = $exhaustiveMedianP95 * 1.10
$contracts = [ordered]@{
    resultSchemaV4 = $true
    verifiedReleaseExecution = $true
    singleSourceIdentity = $sourceShas.Count -eq 1
    cleanSource = $sourceDirtyValues.Count -eq 1 -and $sourceDirtyValues[0] -eq $false
    singleReleaseBundle =
        $processPaths.Count -eq 1 -and
        $initialBundleIdentity.aggregateSha256 -eq $finalBundleIdentity.aggregateSha256 -and
        @($bundleReuseCheckArray | Where-Object { -not $_.identityPreserved }).Count -eq 0
    deterministicHashesMatch = @($records | Where-Object { -not $_.deterministicHashesMatch }).Count -eq 0
    selectorQueryAccountingValid = @($records | Where-Object { -not $_.selectorQueryAccountingValid }).Count -eq 0
    exactQueryReductionAtLeast60Percent = @($records | Where-Object { -not $_.exactQueryReductionPassed }).Count -eq 0
    medianP95RegressionWithin10Percent = $optimizedMedianP95 -le $p95Limit
}
$contractPassed =
    $contracts.singleSourceIdentity -and
    ($contracts.cleanSource -or $AllowDirtySource) -and
    $contracts.singleReleaseBundle -and
    $contracts.deterministicHashesMatch -and
    $contracts.selectorQueryAccountingValid -and
    $contracts.exactQueryReductionAtLeast60Percent -and
    $contracts.medianP95RegressionWithin10Percent
$status = if (-not $contractPassed) { "fail" } elseif ($contracts.cleanSource) { "pass" } else { "pass_dirty_source" }

$result = [ordered]@{
    schemaVersion = 1
    sourceResultSchemaVersion = 4
    capturedUtc = [DateTime]::UtcNow.ToString("o")
    status = $status
    contractStatus = $status
    configuration = [ordered]@{
        scenarioId = $Scenario
        simulationSeed = $Seed
        citizenCount = $Citizens
        measuredTicks = $Ticks
        trialsPerSelector = $Trials
        cacheMode = "cold"
        comparisonGroup = $resolvedComparisonGroup
        executionRoute = "export_release"
        exportPreset = $ExportPreset
    }
    source = [ordered]@{
        gitSha = if ($sourceShas.Count -eq 1) { $sourceShas[0] } else { $null }
        gitDirty = if ($sourceDirtyValues.Count -eq 1) { [bool]$sourceDirtyValues[0] } else { $null }
    }
    releaseBundle = [ordered]@{
        initial = $initialBundleIdentity
        final = $finalBundleIdentity
        reuseChecks = $bundleReuseCheckArray
    }
    contracts = $contracts
    performance = [ordered]@{
        exhaustiveMedianP95Milliseconds = $exhaustiveMedianP95
        optimizedMedianP95Milliseconds = $optimizedMedianP95
        optimizedP95LimitMilliseconds = $p95Limit
        p95Ratio = if ($exhaustiveMedianP95 -gt 0) { $optimizedMedianP95 / $exhaustiveMedianP95 } else { $null }
    }
    trials = $records
    claims = [ordered]@{
        exactSelectionEquivalence = $contractPassed
        releaseComparisonEvidence = $contractPassed -and $contracts.cleanSource
        cacheModeEvidence = $false
        fullMatrixCaptured = $false
        safetyGatePassed = $false
    }
}

$resultPath = Join-Path $OutputRoot "job-selection-comparison.json"
Write-Utf8NoBom $resultPath ($result | ConvertTo-Json -Depth 16)
$summaryLines = @(
    "Societies W1-05 job-selection comparison",
    "Status: $status",
    "Source: $($result.source.gitSha); clean: $($contracts.cleanSource)",
    "Release bundle: $($initialBundleIdentity.aggregateSha256); files: $($initialBundleIdentity.fileCount); reuse checks: $($bundleReuseChecks.Count)",
    "Scenario: $Scenario; seed: $Seed; citizens: $Citizens; measured ticks: $Ticks; trials: $Trials",
    "Exhaustive median p95: $exhaustiveMedianP95 ms",
    "Optimized median p95: $optimizedMedianP95 ms; limit: $p95Limit ms",
    "Exact-query reductions: $((@($records | ForEach-Object { [Math]::Round($_.exactQueryReduction * 100.0, 3) })) -join ', ') percent",
    "Hash equivalence: $($contracts.deterministicHashesMatch); query accounting: $($contracts.selectorQueryAccountingValid)",
    "Output: $OutputRoot"
)
$summaryPath = Join-Path $OutputRoot "job-selection-comparison-summary.txt"
Write-Utf8NoBom $summaryPath ($summaryLines -join [System.Environment]::NewLine)

$manifestArtifacts = $leafArtifactArray + @($resultPath, $summaryPath)
$manifest = [ordered]@{
    schemaVersion = 1
    sourceResultSchemaVersion = 4
    capturedUtc = [DateTime]::UtcNow.ToString("o")
    status = $status
    result = $resultPath
    artifacts = @($manifestArtifacts | Sort-Object | ForEach-Object {
        [ordered]@{
            path = [System.IO.Path]::GetFullPath($_)
            sizeBytes = (Get-Item -LiteralPath $_).Length
            sha256 = Get-Sha256 $_
        }
    })
}
$manifestPath = Join-Path $OutputRoot "job-selection-comparison-manifest.json"
Write-Utf8NoBom $manifestPath ($manifest | ConvertTo-Json -Depth 8)

if (-not $contractPassed) {
    $failedContracts = @($contracts.GetEnumerator() | Where-Object { $_.Value -eq $false } | ForEach-Object { $_.Key })
    throw "W1-05 job-selection comparison failed: $($failedContracts -join ', ')."
}

Write-Host "W1-05 job-selection comparison completed with status '$status'."
Write-Host "Output: $OutputRoot"
