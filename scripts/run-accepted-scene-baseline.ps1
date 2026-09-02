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
    [switch]$RealtimeOnlyDiagnostic,
    [ValidateSet('packet01-v4', 'packet02-v5')]
    [string]$Profile = 'packet01-v4',
    [switch]$ReuseExistingExport,
    [switch]$ReuseValidationOnly,
    [string]$VerifyTrialCausewayArtifactOnly = '',
    [string]$VerifyExportAttestationOnly = '',
    [string]$ExpectedAttestationRequestPath = '',
    [switch]$ProfileContractOnly
)

$ErrorActionPreference = 'Stop'
if ($args.Count -ne 0) {
    throw "Unsupported accepted-scene profile argument(s): $($args -join ' ')"
}
$causewayVerifierRequested = -not [string]::IsNullOrWhiteSpace($VerifyTrialCausewayArtifactOnly)
$attestationVerifierRequested = -not [string]::IsNullOrWhiteSpace($VerifyExportAttestationOnly)
$attestationRequestSupplied = -not [string]::IsNullOrWhiteSpace($ExpectedAttestationRequestPath)
if ($attestationVerifierRequested -xor $attestationRequestSupplied) {
    throw '-VerifyExportAttestationOnly and -ExpectedAttestationRequestPath must be supplied together.'
}
$exclusiveModeCount = @(
    $ProfileContractOnly.IsPresent,
    $causewayVerifierRequested,
    $attestationVerifierRequested
).Where({ $_ }).Count
if ($exclusiveModeCount -gt 1) {
    throw 'Profile-contract, Causeway-verifier, and attestation-verifier modes are mutually exclusive.'
}
$executionOptionRequested = $ReuseExistingExport -or $ReuseValidationOnly -or
    $AllowDirtySourceForSmoke -or $FixedDeltaOnlyDiagnostic -or $RealtimeOnlyDiagnostic
if (($ProfileContractOnly -or $causewayVerifierRequested -or $attestationVerifierRequested) -and
    $executionOptionRequested) {
    throw 'Profile and validation-only modes cannot be combined with normal run, reuse, or diagnostic options.'
}
if ($ReuseValidationOnly -and -not $ReuseExistingExport) {
    throw '-ReuseValidationOnly requires the explicit -ReuseExistingExport opt-in.'
}
if ($FixedDeltaOnlyDiagnostic -and $RealtimeOnlyDiagnostic) {
    throw 'Fixed-delta-only and real-time-only diagnostic modes are mutually exclusive.'
}
if ($ReuseValidationOnly -and ($FixedDeltaOnlyDiagnostic -or $RealtimeOnlyDiagnostic)) {
    throw '-ReuseValidationOnly cannot be combined with a trial diagnostic mode.'
}
$preset = 'Windows Accepted Scene Baseline Release'
$profileContract = if ($Profile -eq 'packet01-v4') {
    [ordered]@{
        profile = 'packet01-v4'
        baseSha = '31ea1d6012d6fd932d0bfe0dbc621e668fd58c80'
        expectedBranch = 'feature/social-kernel-01-baseline'
        trialSchema = 'societies_accepted_scene_baseline/v4'
        bundleSchema = 'societies_accepted_scene_baseline_bundle/v4'
        routeId = 'snow-globe-voxel-four-leg-edit-reload-replay/v4'
        trialArtifactFileName = 'accepted-scene-baseline-trial-v4.json'
        bundleFileName = 'accepted-scene-baseline-v4.json'
        requireCauseway = $false
        includeSameRouteComparison = $false
        baselineProcessP95Milliseconds = $null
        baselinePhysicsP95Milliseconds = $null
    }
} else {
    [ordered]@{
        profile = 'packet02-v5'
        baseSha = '1745896535124bd39ca6321fe6430d93de81bf43'
        expectedBranch = 'feature/social-kernel-02a-causeway-substrate'
        trialSchema = 'societies_accepted_scene_baseline/v5'
        bundleSchema = 'societies_accepted_scene_baseline_bundle/v5'
        routeId = 'snow-globe-voxel-causeway-state-edit-reload-replay/v5'
        trialArtifactFileName = 'accepted-scene-baseline-trial-v5.json'
        bundleFileName = 'accepted-scene-baseline-v5.json'
        requireCauseway = $true
        includeSameRouteComparison = $true
        baselineProcessP95Milliseconds = 8.8501
        baselinePhysicsP95Milliseconds = 23.109
    }
}
$baseSha = [string]$profileContract.baseSha
$trialSchema = [string]$profileContract.trialSchema
$bundleSchema = [string]$profileContract.bundleSchema
$routeId = [string]$profileContract.routeId
$trialArtifactFileName = [string]$profileContract.trialArtifactFileName
$environmentSchema = 'societies_accepted_scene_environment/v1'
$realtimeMode = 'realtime_performance'
$identityMode = 'fixed_delta_identity'
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectRoot = Join-Path $repositoryRoot 'src\societies'
$requestedOutput = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $repositoryRoot $OutputDirectory }
$outputRoot = [System.IO.Path]::GetFullPath($requestedOutput)
$repositoryPrefix = $repositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $outputRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Accepted-scene output must remain inside the repository: $outputRoot"
}
$profileContract.outputRoot = $outputRoot
$profileContract.exportMode = if ($ReuseExistingExport) { 'exact_existing_export_opt_in' } else { 'fresh_export_required' }
$profileContract.reuseValidationOnly = $ReuseValidationOnly.IsPresent
$profileContract.reuseValidationRequirements = @(
    'repository-contained-output',
    'pre-existing-completed-attestation',
    'exact-current-worktree-content-identity',
    'exact-packet-profile-and-export-preset',
    'exact-full-project-input-manifest-and-digest',
    'exact-project-runner-tool-and-build-input-digests',
    'complete-release-runner-layout',
    'packaged-exportrelease-managed-input-digests',
    'godot-export-cache-source-digests',
    'exact-resulting-pck-digest',
    'exact-release-file-manifest'
)
$profileContract.bundleProperties = @('schema', 'status', 'source', 'environment', 'environmentIdentitySha256', 'route', 'classification')
if ([bool]$profileContract.includeSameRouteComparison) { $profileContract.bundleProperties += 'sameRouteComparison' }
$profileContract.bundleProperties += @('realtimePerformanceTrials', 'fixedDeltaIdentityTrials')
if ($ProfileContractOnly) {
    $profileContract | ConvertTo-Json -Depth 4 -Compress
    return
}

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
    return Get-BytesSha256 $bytes
}

function Convert-BytesToLowerHex {
    param([byte[]]$Bytes)
    return -join @($Bytes | ForEach-Object { $_.ToString('x2') })
}

function Get-BytesSha256 {
    param([byte[]]$Bytes)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try { return Convert-BytesToLowerHex $algorithm.ComputeHash($Bytes) }
    finally { $algorithm.Dispose() }
}

function Get-Sha256 {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required identity input is missing: $Path"
    }
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    $stream = [System.IO.File]::OpenRead($Path)
    try { return Convert-BytesToLowerHex $algorithm.ComputeHash($stream) }
    finally {
        $stream.Dispose()
        $algorithm.Dispose()
    }
}

function Get-Md5 {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required identity input is missing: $Path"
    }
    $algorithm = [System.Security.Cryptography.MD5]::Create()
    $stream = [System.IO.File]::OpenRead($Path)
    try { return Convert-BytesToLowerHex $algorithm.ComputeHash($stream) }
    finally {
        $stream.Dispose()
        $algorithm.Dispose()
    }
}

function Assert-ExactJsonPropertySet {
    param([object]$Value, [string[]]$ExpectedNames, [string]$Label)
    if ($null -eq $Value -or $Value.GetType() -ne [System.Management.Automation.PSCustomObject]) {
        throw "$Label must be a JSON object."
    }
    $actualNames = @($Value.PSObject.Properties.Name)
    $missing = @($ExpectedNames | Where-Object { $_ -cnotin $actualNames })
    $extra = @($actualNames | Where-Object { $_ -cnotin $ExpectedNames })
    if ($actualNames.Count -ne $ExpectedNames.Count -or $missing.Count -ne 0 -or $extra.Count -ne 0) {
        throw "$Label property set is invalid; missing=[$($missing -join ',')], extra=[$($extra -join ',')]."
    }
}

function Assert-JsonNativeType {
    param([object]$Value, [type]$ExpectedType, [string]$Label)
    if ($null -eq $Value -or $Value.GetType() -ne $ExpectedType) {
        $actualType = if ($null -eq $Value) { 'null' } else { $Value.GetType().FullName }
        throw "$Label must have native JSON type $($ExpectedType.FullName); actual=$actualType."
    }
}

function Assert-JsonNativeInteger {
    param([object]$Value, [string]$Label)
    if ($null -eq $Value -or
        ($Value.GetType() -ne [int] -and $Value.GetType() -ne [long])) {
        $actualType = if ($null -eq $Value) { 'null' } else { $Value.GetType().FullName }
        throw "$Label must have native JSON integer type System.Int32 or System.Int64; actual=$actualType."
    }
}

function Get-NormalizedContainedRelativePath {
    param([string]$Root, [string]$Path, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($Root) -or [string]::IsNullOrWhiteSpace($Path) -or
        -not [System.IO.Path]::IsPathRooted($Root) -or -not [System.IO.Path]::IsPathRooted($Path)) {
        throw "$Label root and path must be unambiguous absolute paths."
    }
    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath.Equals($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label path cannot be the bound root itself."
    }
    $prefix = $fullRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label path escapes its bound root."
    }
    $relative = $fullPath.Substring($prefix.Length).Replace('\', '/')
    if ([string]::IsNullOrWhiteSpace($relative) -or $relative.StartsWith('/', [System.StringComparison]::Ordinal) -or
        $relative.Split('/') -contains '..' -or $relative.Split('/') -contains '.') {
        throw "$Label produced an empty or ambiguous relative path."
    }
    return $relative
}

function Resolve-ContainedRelativeFile {
    param([string]$Root, [string]$RelativePath, [string]$Label)
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "$Label path must be a non-empty relative path."
    }
    $fullRoot = [System.IO.Path]::GetFullPath($Root)
    $prefix = $fullRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $fullRoot $RelativePath))
    if (-not $fullPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "$Label path is missing or escapes its bound root."
    }
    return $fullPath
}

function Get-ProjectInputIdentity {
    $rows = [System.Collections.Generic.List[string]]::new()
    $manifest = [System.Collections.Generic.List[object]]::new()
    $relativeProjectRoot = Get-NormalizedContainedRelativePath $repositoryRoot $projectRoot 'Godot project input root'
    $files = @(git -C $repositoryRoot ls-files --cached --others --exclude-standard -- $relativeProjectRoot |
        ForEach-Object { ([string]$_).Replace('\', '/') } | Sort-Object -Unique)
    if ($files.Count -eq 0) { throw 'Godot project input identity resolved no repository files.' }
    foreach ($relativePath in $files) {
        $path = Join-Path $repositoryRoot $relativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Godot project identity input is missing: $relativePath"
        }
        $length = (Get-Item -LiteralPath $path).Length
        $sha256 = Get-Sha256 $path
        $rows.Add("$relativePath|$length|$sha256")
        $manifest.Add([ordered]@{ path = $relativePath; length = $length; sha256 = $sha256 })
    }
    if (@($manifest | Where-Object { $_.path -eq 'src/societies/data/prototype-scenarios.json' }).Count -ne 1 -or
        @($manifest | Where-Object { $_.path.EndsWith('.cs', [System.StringComparison]::OrdinalIgnoreCase) }).Count -eq 0) {
        throw 'Godot project identity must include prototype-scenarios.json and all repository-visible C# inputs.'
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($rows -join "`n"))
    return [ordered]@{
        fileCount = $rows.Count
        aggregateSha256 = Get-BytesSha256 $bytes
        files = $manifest.ToArray()
    }
}

function Get-ReleaseFileManifest {
    param([string]$ReleaseDirectory)
    return @(Get-ChildItem -LiteralPath $ReleaseDirectory -Recurse -File -Force |
        Sort-Object FullName |
        ForEach-Object {
            [ordered]@{
                path = Get-NormalizedContainedRelativePath $ReleaseDirectory $_.FullName 'Release file manifest entry'
                length = $_.Length
                sha256 = Get-Sha256 $_.FullName
            }
        })
}

function Assert-GodotExportCacheMatchesCurrentSources {
    $cacheFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot '.godot\exported') -Recurse -Filter 'file_cache' -File -ErrorAction SilentlyContinue)
    if ($cacheFiles.Count -ne 1) {
        throw "Reusable export requires exactly one Godot export file cache; found $($cacheFiles.Count)."
    }
    $runnerSeen = $false
    $rows = @(Get-Content -LiteralPath $cacheFiles[0].FullName | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($rows.Count -eq 0) { throw 'Reusable export Godot file cache is empty.' }
    foreach ($row in $rows) {
        $parts = $row -split '::', 4
        if ($parts.Count -ne 4 -or -not $parts[0].StartsWith('res://', [System.StringComparison]::Ordinal) -or
            $parts[1] -notmatch '^[0-9a-f]{32}$') {
            throw 'Reusable export Godot file cache contains a malformed source identity row.'
        }
        $sourcePath = Join-Path $projectRoot $parts[0].Substring(6)
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Reusable export cache source is missing: $($parts[0])"
        }
        $actualMd5 = Get-Md5 $sourcePath
        if ($actualMd5 -cne $parts[1]) {
            throw "Reusable export cache source digest is stale: $($parts[0])"
        }
        if ($parts[0] -ceq 'res://tests/AcceptedSceneBaselineRunner.tscn') { $runnerSeen = $true }
    }
    if (-not $runnerSeen) { throw 'Reusable export cache does not bind the accepted-scene runner.' }
    return [ordered]@{
        path = Get-NormalizedContainedRelativePath $projectRoot $cacheFiles[0].FullName 'Godot export cache'
        sha256 = Get-Sha256 $cacheFiles[0].FullName
        sourceCount = $rows.Count
    }
}

function Get-ExportAttestationRequest {
    param(
        [string]$SourceSha,
        [string]$SourceTree,
        [string]$SourceStateIdentity,
        [string]$RepositoryContentIdentity,
        [string]$GodotVersion
    )
    $projectInputs = Get-ProjectInputIdentity
    $toolInputs = [System.Collections.Generic.List[object]]::new()
    foreach ($toolPath in @(
        (Join-Path $repositoryRoot 'scripts\run-accepted-scene-baseline.ps1'),
        $(if ($Profile -eq 'packet02-v5') { Join-Path $repositoryRoot 'scripts\run-causeway-packet-02-route.ps1' })
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
        $toolInputs.Add([ordered]@{
            path = Get-NormalizedContainedRelativePath $repositoryRoot $toolPath 'Accepted-scene tooling input'
            length = (Get-Item -LiteralPath $toolPath).Length
            sha256 = Get-Sha256 $toolPath
        })
    }
    return [ordered]@{
        repositoryRoot = $repositoryRoot
        projectRoot = $projectRoot
        outputRoot = $outputRoot
        releaseDirectory = (Join-Path $outputRoot 'release-runner')
        releaseExecutable = (Join-Path $outputRoot 'release-runner\SocietiesAcceptedSceneBaseline.exe')
        profile = $Profile
        preset = $preset
        managedAssemblyConfiguration = 'ExportRelease'
        sourceSha = $SourceSha
        sourceTree = $SourceTree
        sourceStateIdentity = $SourceStateIdentity
        repositoryContentIdentity = $RepositoryContentIdentity
        godotPath = $GodotPath
        godotSha256 = Get-Sha256 $GodotPath
        godotVersion = $GodotVersion
        projectInputs = $projectInputs
        projectGodotSha256 = Get-Sha256 (Join-Path $projectRoot 'project.godot')
        projectCsprojSha256 = Get-Sha256 (Join-Path $projectRoot 'Societies.csproj')
        exportPresetsSha256 = Get-Sha256 (Join-Path $projectRoot 'export_presets.cfg')
        runnerScenePath = 'res://tests/AcceptedSceneBaselineRunner.tscn'
        runnerSceneSha256 = Get-Sha256 (Join-Path $projectRoot 'tests\AcceptedSceneBaselineRunner.tscn')
        runnerSourcePath = 'res://tests/AcceptedSceneBaselineRunner.cs'
        runnerSourceSha256 = Get-Sha256 (Join-Path $projectRoot 'tests\AcceptedSceneBaselineRunner.cs')
        runnerModelsPath = 'res://tests/AcceptedSceneBaselineModels.cs'
        runnerModelsSha256 = Get-Sha256 (Join-Path $projectRoot 'tests\AcceptedSceneBaselineModels.cs')
        toolInputs = $toolInputs.ToArray()
    }
}

function Get-CompletedExportAttestation {
    param([object]$Request)
    $ReleaseDirectory = [string]$Request.releaseDirectory
    $ReleaseExecutable = [string]$Request.releaseExecutable
    $consoleWrapper = Join-Path $ReleaseDirectory 'SocietiesAcceptedSceneBaseline.console.exe'
    $pack = Join-Path $ReleaseDirectory 'SocietiesAcceptedSceneBaseline.pck'
    foreach ($required in @($ReleaseExecutable, $consoleWrapper, $pack)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf) -or (Get-Item -LiteralPath $required).Length -le 0) {
            throw "Reusable export is incomplete: $required"
        }
    }
    $unexpectedRootEntries = @(Get-ChildItem -LiteralPath $outputRoot -Force | Where-Object {
        $_.Name -notin @('release-runner', 'accepted-scene-export-identity.json')
    })
    if ($unexpectedRootEntries.Count -ne 0) {
        throw 'Reusable export output already contains trial, bundle, or unknown files.'
    }
    $packagedAssemblies = @(Get-ChildItem -LiteralPath $ReleaseDirectory -Recurse -Filter 'Societies.dll' -File)
    if ($packagedAssemblies.Count -ne 1) {
        throw "Reusable export requires exactly one packaged Societies.dll; found $($packagedAssemblies.Count)."
    }
    $exportReleaseRoot = Join-Path $projectRoot '.godot\mono\temp\bin\ExportRelease\win-x64'
    $managedInputs = [ordered]@{}
    foreach ($name in @('Societies.dll', 'Societies.pdb', 'Societies.deps.json', 'Societies.runtimeconfig.json')) {
        $packagedPath = Join-Path $packagedAssemblies[0].DirectoryName $name
        $currentPath = Join-Path $exportReleaseRoot $name
        $packagedHash = Get-Sha256 $packagedPath
        $currentHash = Get-Sha256 $currentPath
        if ($packagedHash -cne $currentHash) {
            throw "Reusable export packaged $name does not match the current ExportRelease input."
        }
        $managedInputs[$name] = $currentHash
    }
    $cacheIdentity = Assert-GodotExportCacheMatchesCurrentSources
    $releaseFiles = @(Get-ReleaseFileManifest $ReleaseDirectory)
    if ($releaseFiles.Count -lt 8) { throw 'Reusable export release file manifest is incomplete.' }
    return [ordered]@{
        schema = 'societies_accepted_scene_export_attestation/v2'
        state = 'completed'
        request = $Request
        completion = [ordered]@{
            pck = [ordered]@{
                path = Get-NormalizedContainedRelativePath $ReleaseDirectory $pack 'Accepted-scene PCK'
                length = (Get-Item -LiteralPath $pack).Length
                sha256 = Get-Sha256 $pack
            }
            exportCache = $cacheIdentity
            managedInputs = $managedInputs
            releaseFiles = $releaseFiles
        }
    }
}

function Write-ExportAttestation {
    param([object]$Attestation)
    $identityPath = Join-Path $outputRoot 'accepted-scene-export-identity.json'
    $temporaryPath = $identityPath + '.tmp'
    [System.IO.File]::WriteAllText($temporaryPath, ($Attestation | ConvertTo-Json -Depth 12), [System.Text.UTF8Encoding]::new($false))
    [System.IO.File]::Move($temporaryPath, $identityPath, $true)
    return $identityPath
}

function Write-PendingExportAttestation {
    param([object]$Request)
    return Write-ExportAttestation ([ordered]@{
        schema = 'societies_accepted_scene_export_attestation/v2'
        state = 'pending'
        request = $Request
        completion = $null
    })
}

function Assert-CompletedExportAttestation {
    param([object]$ExpectedRequest)
    $identityPath = Join-Path $outputRoot 'accepted-scene-export-identity.json'
    $actual = Assert-ExportAttestationDocument $identityPath $ExpectedRequest ([string]$ExpectedRequest.releaseDirectory)
    $expected = Get-CompletedExportAttestation $ExpectedRequest
    $actualJson = $actual | ConvertTo-Json -Depth 12 -Compress
    $expectedJson = $expected | ConvertTo-Json -Depth 12 -Compress
    if ($actualJson -cne $expectedJson) {
        throw 'Reusable export completed identity manifest is stale or mismatched.'
    }
    return $identityPath
}

function Assert-ExportAttestationDocument {
    param([string]$IdentityPath, [object]$ExpectedRequest, [string]$ReleaseDirectory)
    if (-not (Test-Path -LiteralPath $identityPath -PathType Leaf)) {
        throw 'Reusable export requires a pre-existing completed identity manifest.'
    }
    try {
        $actual = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
    }
    catch {
        throw [System.IO.InvalidDataException]::new('Reusable export identity manifest is malformed.', $_.Exception)
    }
    Assert-ExactJsonPropertySet $actual @('schema', 'state', 'request', 'completion') 'Reusable export identity manifest'
    if ($actual.schema -cne 'societies_accepted_scene_export_attestation/v2' -or
        $actual.state -cne 'completed' -or $null -eq $actual.request -or $null -eq $actual.completion) {
        throw 'Reusable export identity manifest is incomplete and was not finalized by a successful fresh export.'
    }
    Assert-JsonNativeType $actual.schema ([string]) 'Reusable export identity manifest schema'
    Assert-JsonNativeType $actual.state ([string]) 'Reusable export identity manifest state'
    if ($actual.request.GetType() -ne [System.Management.Automation.PSCustomObject] -or
        $actual.completion.GetType() -ne [System.Management.Automation.PSCustomObject]) {
        throw 'Reusable export identity manifest request and completion must be JSON objects.'
    }
    if (($actual.request | ConvertTo-Json -Depth 12 -Compress) -cne
        ($ExpectedRequest | ConvertTo-Json -Depth 12 -Compress)) {
        throw 'Reusable export attestation request has source, project-input, preset, profile, runner, or build-input drift.'
    }
    Assert-ExactJsonPropertySet $actual.completion @('pck', 'exportCache', 'managedInputs', 'releaseFiles') `
        'Reusable export attestation completion'

    Assert-ExactJsonPropertySet $actual.completion.pck @('path', 'length', 'sha256') `
        'Reusable export attestation PCK'
    Assert-JsonNativeType $actual.completion.pck.path ([string]) 'Reusable export attestation PCK path'
    Assert-JsonNativeInteger $actual.completion.pck.length 'Reusable export attestation PCK length'
    Assert-JsonNativeType $actual.completion.pck.sha256 ([string]) 'Reusable export attestation PCK digest'
    $pack = Join-Path $ReleaseDirectory 'SocietiesAcceptedSceneBaseline.pck'
    $expectedPck = [ordered]@{
        path = Get-NormalizedContainedRelativePath $ReleaseDirectory $pack 'Reusable export attestation PCK'
        length = $(if (Test-Path -LiteralPath $pack -PathType Leaf) { (Get-Item -LiteralPath $pack).Length } else { -1 })
        sha256 = $(if (Test-Path -LiteralPath $pack -PathType Leaf) { Get-Sha256 $pack } else { '<missing>' })
    }
    if (($actual.completion.pck | ConvertTo-Json -Compress) -cne ($expectedPck | ConvertTo-Json -Compress)) {
        throw 'Reusable export attestation PCK is missing or drifted.'
    }

    Assert-ExactJsonPropertySet $actual.completion.exportCache @('path', 'sha256', 'sourceCount') `
        'Reusable export attestation exportCache'
    Assert-JsonNativeType $actual.completion.exportCache.path ([string]) 'Reusable export attestation exportCache path'
    Assert-JsonNativeType $actual.completion.exportCache.sha256 ([string]) 'Reusable export attestation exportCache digest'
    Assert-JsonNativeInteger $actual.completion.exportCache.sourceCount 'Reusable export attestation exportCache sourceCount'
    Assert-JsonNativeType $ExpectedRequest.projectRoot ([string]) 'Expected attestation projectRoot'
    $cachePath = Resolve-ContainedRelativeFile ([string]$ExpectedRequest.projectRoot) `
        ([string]$actual.completion.exportCache.path) 'Reusable export attestation exportCache'
    $cacheRows = @(Get-Content -LiteralPath $cachePath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ([string]$actual.completion.exportCache.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
        [string]$actual.completion.exportCache.sha256 -cne (Get-Sha256 $cachePath) -or
        [long]$actual.completion.exportCache.sourceCount -ne $cacheRows.Count -or $cacheRows.Count -eq 0) {
        throw 'Reusable export attestation exportCache digest or source count is missing or drifted.'
    }
    $runnerSeen = $false
    foreach ($row in $cacheRows) {
        $parts = $row -split '::', 4
        if ($parts.Count -ne 4 -or -not $parts[0].StartsWith('res://', [System.StringComparison]::Ordinal) -or
            $parts[1] -cnotmatch '^[0-9a-f]{32}$') {
            throw 'Reusable export attestation exportCache contains a malformed source identity row.'
        }
        $sourcePath = Resolve-ContainedRelativeFile ([string]$ExpectedRequest.projectRoot) $parts[0].Substring(6) `
            'Reusable export attestation exportCache source'
        if ((Get-Md5 $sourcePath) -cne $parts[1]) {
            throw "Reusable export attestation exportCache source digest is stale: $($parts[0])"
        }
        if ($parts[0] -ceq 'res://tests/AcceptedSceneBaselineRunner.tscn') { $runnerSeen = $true }
    }
    if (-not $runnerSeen) {
        throw 'Reusable export attestation exportCache does not bind the accepted-scene runner.'
    }

    $managedNames = @('Societies.dll', 'Societies.pdb', 'Societies.deps.json', 'Societies.runtimeconfig.json')
    Assert-ExactJsonPropertySet $actual.completion.managedInputs $managedNames `
        'Reusable export attestation managedInputs'
    $packagedAssemblies = @(Get-ChildItem -LiteralPath $ReleaseDirectory -Recurse -Filter 'Societies.dll' -File)
    if ($packagedAssemblies.Count -ne 1) {
        throw 'Reusable export attestation managedInputs require exactly one packaged Societies.dll.'
    }
    $exportReleaseRoot = Join-Path ([string]$ExpectedRequest.projectRoot) '.godot\mono\temp\bin\ExportRelease\win-x64'
    foreach ($name in $managedNames) {
        $digest = $actual.completion.managedInputs.$name
        Assert-JsonNativeType $digest ([string]) "Reusable export attestation managedInputs $name"
        $packagedPath = Join-Path $packagedAssemblies[0].DirectoryName $name
        $currentPath = Join-Path $exportReleaseRoot $name
        if ([string]$digest -cnotmatch '^[0-9a-f]{64}$' -or
            [string]$digest -cne (Get-Sha256 $packagedPath) -or
            [string]$digest -cne (Get-Sha256 $currentPath)) {
            throw "Reusable export attestation managedInputs digest is missing or drifted for $name."
        }
    }

    if ($null -eq $actual.completion.releaseFiles -or
        $actual.completion.releaseFiles.GetType() -ne [System.Object[]]) {
        throw 'Reusable export attestation releaseFiles must be a JSON array.'
    }
    $releaseFiles = @($actual.completion.releaseFiles)
    if ($releaseFiles.Count -lt 8) {
        throw 'Reusable export attestation release-file manifest is incomplete.'
    }
    $releasePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($entry in $releaseFiles) {
        Assert-ExactJsonPropertySet $entry @('path', 'length', 'sha256') 'Reusable export attestation release-file entry'
        Assert-JsonNativeType $entry.path ([string]) 'Reusable export attestation release-file path'
        Assert-JsonNativeInteger $entry.length 'Reusable export attestation release-file length'
        Assert-JsonNativeType $entry.sha256 ([string]) 'Reusable export attestation release-file digest'
        if (-not $releasePaths.Add([string]$entry.path) -or [long]$entry.length -lt 0 -or
            [string]$entry.sha256 -cnotmatch '^[0-9a-f]{64}$') {
            throw 'Reusable export attestation release-file entry is duplicate or malformed.'
        }
        [void](Resolve-ContainedRelativeFile $ReleaseDirectory ([string]$entry.path) `
            'Reusable export attestation release-file')
    }
    $expectedReleaseFiles = @(Get-ReleaseFileManifest $ReleaseDirectory)
    if (($actual.completion.releaseFiles | ConvertTo-Json -Depth 5 -Compress) -cne
        ($expectedReleaseFiles | ConvertTo-Json -Depth 5 -Compress)) {
        throw 'Reusable export attestation release-file manifest is missing or drifted.'
    }
    return $actual
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
    $expected = Get-BytesSha256 $bytes
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

function Assert-CausewayEvidence {
    param([object]$Trial, [string]$Mode, [int]$TrialIndex)
    if (-not [bool]$profileContract.requireCauseway) {
        if ($null -ne $Trial.causeway) { throw "$Mode trial $TrialIndex unexpectedly contains Causeway evidence." }
        return
    }
    $causeway = $Trial.causeway
    if ($null -eq $causeway) {
        throw "$Mode trial $TrialIndex Causeway command/event/revision evidence is missing or invalid."
    }
    $causewayProperties = @(
        'commandKind', 'commandQuantity', 'accepted', 'eventType', 'previousRevision', 'revision',
        'beforeCommandStateIdentity', 'afterCommandStateIdentity', 'afterVoxelEditStateIdentity',
        'reloadedStateIdentity', 'replayedAfterCommandStateIdentity', 'replayedAfterVoxelEditStateIdentity'
    )
    Assert-ExactJsonPropertySet $causeway $causewayProperties "$Mode trial $TrialIndex Causeway evidence"
    Assert-JsonNativeType $causeway.commandKind ([string]) "$Mode trial $TrialIndex Causeway commandKind"
    Assert-JsonNativeInteger $causeway.commandQuantity "$Mode trial $TrialIndex Causeway commandQuantity"
    Assert-JsonNativeType $causeway.accepted ([bool]) "$Mode trial $TrialIndex Causeway accepted"
    Assert-JsonNativeType $causeway.eventType ([string]) "$Mode trial $TrialIndex Causeway eventType"
    Assert-JsonNativeInteger $causeway.previousRevision "$Mode trial $TrialIndex Causeway previousRevision"
    Assert-JsonNativeInteger $causeway.revision "$Mode trial $TrialIndex Causeway revision"
    $identityNames = @(
        'beforeCommandStateIdentity', 'afterCommandStateIdentity',
        'afterVoxelEditStateIdentity', 'reloadedStateIdentity',
        'replayedAfterCommandStateIdentity', 'replayedAfterVoxelEditStateIdentity'
    )
    foreach ($name in $identityNames) {
        Assert-JsonNativeType $causeway.$name ([string]) "$Mode trial $TrialIndex Causeway $name"
    }
    if ($causeway.commandKind -cne 'ContributeCommunityTimber' -or
        $causeway.commandQuantity -ne 1 -or $causeway.accepted -ne $true -or
        $causeway.eventType -cne 'causeway.material.committed' -or
        $causeway.previousRevision -ne 0 -or $causeway.revision -ne 1) {
        throw "$Mode trial $TrialIndex Causeway command/event/revision evidence is missing or invalid."
    }
    foreach ($name in $identityNames[0..3]) {
        if ($causeway.$name -cnotmatch '^[0-9a-f]{64}$') {
            throw "$Mode trial $TrialIndex Causeway identity '$name' is missing or malformed."
        }
    }
    if ($causeway.beforeCommandStateIdentity -ceq $causeway.afterCommandStateIdentity -or
        $causeway.afterCommandStateIdentity -cne $causeway.afterVoxelEditStateIdentity -or
        $causeway.afterCommandStateIdentity -cne $causeway.reloadedStateIdentity) {
        throw "$Mode trial $TrialIndex Causeway edit/reload equality is invalid."
    }
    $replayedCommand = $causeway.replayedAfterCommandStateIdentity
    $replayedEdit = $causeway.replayedAfterVoxelEditStateIdentity
    if ($Mode -ceq $realtimeMode) {
        if ($replayedCommand -cne '' -or $replayedEdit -cne '') {
            throw "Real-time trial $TrialIndex must not contain fixed-delta Causeway replay evidence."
        }
    }
    elseif ($Mode -ceq $identityMode) {
        if ($replayedCommand -cnotmatch '^[0-9a-f]{64}$' -or $replayedEdit -cnotmatch '^[0-9a-f]{64}$' -or
            $replayedCommand -cne $causeway.afterCommandStateIdentity -or
            $replayedEdit -cne $causeway.afterVoxelEditStateIdentity) {
            throw "Fixed-delta trial $TrialIndex Causeway replay equality is missing or invalid."
        }
    }
    else {
        throw "Causeway verifier received unsupported trial mode '$Mode'."
    }
}

function Get-CausewayPrimaryIdentity {
    param([object]$Causeway)
    return ([ordered]@{
        commandKind = [string]$Causeway.commandKind
        commandQuantity = [int]$Causeway.commandQuantity
        accepted = [bool]$Causeway.accepted
        eventType = [string]$Causeway.eventType
        previousRevision = [long]$Causeway.previousRevision
        revision = [long]$Causeway.revision
        beforeCommandStateIdentity = [string]$Causeway.beforeCommandStateIdentity
        afterCommandStateIdentity = [string]$Causeway.afterCommandStateIdentity
        afterVoxelEditStateIdentity = [string]$Causeway.afterVoxelEditStateIdentity
        reloadedStateIdentity = [string]$Causeway.reloadedStateIdentity
    } | ConvertTo-Json -Compress)
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
    Assert-CausewayEvidence $Trial $Mode $TrialIndex

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

if ($causewayVerifierRequested) {
    $artifactPath = [System.IO.Path]::GetFullPath($VerifyTrialCausewayArtifactOnly)
    if (-not $artifactPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
        throw 'Causeway verifier artifact must be an existing repository-contained file.'
    }
    try {
        $artifact = Get-Content -LiteralPath $artifactPath -Raw | ConvertFrom-Json
    }
    catch {
        throw [System.IO.InvalidDataException]::new('Causeway verifier artifact JSON is malformed.', $_.Exception)
    }
    $mode = [string]$artifact.route.trialMode
    $trialIndex = [int]$artifact.route.trialIndex
    Assert-CausewayEvidence $artifact $mode $trialIndex
    Write-Output "CAUSEWAY_TRIAL_EVIDENCE_VALID $mode $trialIndex"
    return
}
if ($attestationVerifierRequested) {
    $identityPath = [System.IO.Path]::GetFullPath($VerifyExportAttestationOnly)
    $requestPath = [System.IO.Path]::GetFullPath($ExpectedAttestationRequestPath)
    if (-not $identityPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not $requestPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $requestPath -PathType Leaf)) {
        throw 'Attestation verifier inputs must be repository-contained and the expected request must exist.'
    }
    try {
        $expectedRequest = Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
    }
    catch {
        throw [System.IO.InvalidDataException]::new('Expected attestation request JSON is malformed.', $_.Exception)
    }
    $releaseDirectory = Join-Path $outputRoot 'release-runner'
    if ([string]$expectedRequest.outputRoot -cne $outputRoot -or
        [string]$expectedRequest.releaseDirectory -cne $releaseDirectory) {
        throw 'Expected attestation request is not bound to the selected repository-contained output.'
    }
    [void](Assert-ExportAttestationDocument $identityPath $expectedRequest $releaseDirectory)
    Write-Output "EXPORT_ATTESTATION_VALID $identityPath"
    return
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
if ($branch -ne [string]$profileContract.expectedBranch) {
    throw "Source branch mismatch: expected $($profileContract.expectedBranch), got '$branch'."
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

$releaseDirectory = Join-Path $outputRoot 'release-runner'
$releaseExecutable = Join-Path $releaseDirectory 'SocietiesAcceptedSceneBaseline.exe'
$exportRequest = Get-ExportAttestationRequest `
    $sourceSha $sourceTree $sourceStateIdentity $repositoryContentIdentityBefore $version
if ($ReuseExistingExport) {
    if (-not (Test-Path -LiteralPath $outputRoot -PathType Container) -or
        -not (Test-Path -LiteralPath $releaseDirectory -PathType Container)) {
        throw "Explicit reusable export output is missing: $outputRoot"
    }
    $exportIdentityPath = Assert-CompletedExportAttestation $exportRequest
    $exportAttestation = Get-Content -LiteralPath $exportIdentityPath -Raw | ConvertFrom-Json
    Write-Host "Validated pre-existing completed ExportRelease attestation: $exportIdentityPath"
}
else {
    if (Test-Path -LiteralPath $outputRoot) {
        throw "Packet 01 output directory already exists: $outputRoot"
    }
    [System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
    [System.IO.Directory]::CreateDirectory($releaseDirectory) | Out-Null
    $exportIdentityPath = Write-PendingExportAttestation $exportRequest
    $exportExitCode = Invoke-ExactChild $GodotPath @(
        '--headless', '--path', $projectRoot, '--export-release', $preset, $releaseExecutable, '--quit'
    ) $repositoryRoot 'Godot accepted-scene Release export'
    if ($exportExitCode -ne 0) {
        throw "Godot accepted-scene Release export exited with code $exportExitCode."
    }
    $exportAttestation = Get-CompletedExportAttestation $exportRequest
    $exportIdentityPath = Write-ExportAttestation $exportAttestation
}
if ($ReuseValidationOnly) {
    $exportAttestation | ConvertTo-Json -Depth 12 -Compress
    return
}
$consoleWrapper = Join-Path $releaseDirectory 'SocietiesAcceptedSceneBaseline.console.exe'
$packagedAssemblies = @(Get-ChildItem -LiteralPath $releaseDirectory -Recurse -Filter 'Societies.dll')
if (-not (Test-Path -LiteralPath $consoleWrapper -PathType Leaf) -or $packagedAssemblies.Count -ne 1) {
    throw 'Accepted-scene ExportRelease wrapper or managed assembly is missing.'
}
$assemblyPath = $packagedAssemblies[0].FullName
$managedAssemblySha256 = Get-Sha256 $assemblyPath
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
        '--managed-assembly-sha256', $managedAssemblySha256,
        '--artifact-schema', $trialSchema,
        '--route-id', $routeId,
        '--artifact-file-name', $trialArtifactFileName,
        '--require-causeway', ([bool]$profileContract.requireCauseway).ToString().ToLowerInvariant()
    )
    $trialExitCode = Invoke-ExactChild $consoleWrapper $arguments $releaseDirectory "real-time trial $trialIndex"
    if ($trialExitCode -eq 2) {
        $safetyExitObserved = $true
    } elseif ($trialExitCode -ne 0) {
        throw "Accepted-scene real-time trial $trialIndex failed with exit code $trialExitCode."
    }
    $resultPath = Join-Path $trialDirectory $trialArtifactFileName
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
    Write-Host "Real-time-only diagnostic artifact: $(Join-Path $outputRoot "realtime-trial-1\$trialArtifactFileName")"
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
        '--managed-assembly-sha256', $managedAssemblySha256,
        '--artifact-schema', $trialSchema,
        '--route-id', $routeId,
        '--artifact-file-name', $trialArtifactFileName,
        '--require-causeway', ([bool]$profileContract.requireCauseway).ToString().ToLowerInvariant()
    )
    $trialExitCode = Invoke-ExactChild $consoleWrapper $arguments $releaseDirectory "fixed-delta identity trial $trialIndex"
    if ($trialExitCode -ne 0) {
        throw "Accepted-scene fixed-delta identity trial $trialIndex failed with exit code $trialExitCode."
    }
    $resultPath = Join-Path $trialDirectory $trialArtifactFileName
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
    Write-Host "Fixed-delta-only diagnostic artifact: $(Join-Path $outputRoot "fixed-delta-identity-trial-1\$trialArtifactFileName")"
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
if ([bool]$profileContract.requireCauseway) {
    $primaryCausewayIdentities = @($allTrials | ForEach-Object { Get-CausewayPrimaryIdentity $_.causeway } | Select-Object -Unique)
    $fixedReplayCommandIdentities = @($identityTrials | ForEach-Object { [string]$_.causeway.replayedAfterCommandStateIdentity } | Select-Object -Unique)
    $fixedReplayEditIdentities = @($identityTrials | ForEach-Object { [string]$_.causeway.replayedAfterVoxelEditStateIdentity } | Select-Object -Unique)
    if ($primaryCausewayIdentities.Count -ne 1 -or
        $fixedReplayCommandIdentities.Count -ne 1 -or $fixedReplayEditIdentities.Count -ne 1) {
        throw 'Causeway command/edit/reload/replay evidence differs across trials.'
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
if ([bool]$profileContract.includeSameRouteComparison) {
    $baselineProcessP95 = [double]$profileContract.baselineProcessP95Milliseconds
    $baselinePhysicsP95 = [double]$profileContract.baselinePhysicsP95Milliseconds
    if (-not [double]::IsFinite($baselineProcessP95) -or $baselineProcessP95 -le 0.0 -or
        -not [double]::IsFinite($baselinePhysicsP95) -or $baselinePhysicsP95 -le 0.0) {
        throw 'Packet 02 profile contains a non-finite or non-positive Packet 01 comparison baseline.'
    }
    $bundle.sameRouteComparison = [ordered]@{
        packet01WorstProcessP95Milliseconds = $baselineProcessP95
        packet01WorstPhysicsP95Milliseconds = $baselinePhysicsP95
        processP95RegressionPercent = (($worstProcessFrameP95 - $baselineProcessP95) / $baselineProcessP95) * 100.0
        physicsP95RegressionPercent = (($worstPhysicsFrameP95 - $baselinePhysicsP95) / $baselinePhysicsP95) * 100.0
        maximumAllowedRegressionPercent = 10.0
        withinBudget = $worstProcessFrameP95 -le ($baselineProcessP95 * 1.1) -and $worstPhysicsFrameP95 -le ($baselinePhysicsP95 * 1.1)
    }
}
$bundleFileName = [string]$profileContract.bundleFileName
$bundlePath = Join-Path $outputRoot $bundleFileName
[System.IO.File]::WriteAllText(
    $bundlePath,
    ($bundle | ConvertTo-Json -Depth 20),
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Accepted-scene baseline artifact: $bundlePath"
Write-Host "Classification: $classification; raw=$rawClassification; process median/worst p95=$medianProcessFrameP95/$worstProcessFrameP95 ms; physics median/worst p95=$medianPhysicsFrameP95/$worstPhysicsFrameP95 ms"
if ($claimEligible -and ($rawClassification -eq 'safety_failure' -or $safetyExitObserved)) {
    throw 'Accepted-scene baseline breached the 33.33 ms p95 hard-safety line. Evidence was emitted before failure.'
}
if ([bool]$profileContract.requireCauseway -and $claimEligible -and -not [bool]$bundle.sameRouteComparison.withinBudget) {
    throw 'Packet 02 same-route p95 regression exceeded the 10% budget. Evidence was emitted before failure.'
}
