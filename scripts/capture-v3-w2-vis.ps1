[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GodotPath,
    [string]$DotnetPath = "dotnet",
    [string]$OutputPath,
    [switch]$Headless
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    [System.IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding($false)))
}

function Resolve-Executable {
    param([string]$PathOrCommand, [string]$Label)

    if (Test-Path -LiteralPath $PathOrCommand -PathType Leaf) {
        return (Resolve-Path -LiteralPath $PathOrCommand).Path
    }

    $command = Get-Command -Name $PathOrCommand -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "Could not resolve $Label executable '$PathOrCommand'."
    }

    return $command.Source
}

function Require-ManifestProperty {
    param([object]$Object, [string]$Name)

    if ($null -eq $Object -or $Object.PSObject.Properties.Name -notcontains $Name) {
        throw "Capture manifest is missing required property '$Name'."
    }

    return $Object.$Name
}

function Assert-ExactNumber {
    param([object]$Actual, [double]$Expected, [string]$Description)

    if ([math]::Abs(([double]$Actual) - $Expected) -gt 0.0001) {
        throw "Capture manifest $Description is '$Actual'; expected '$Expected'."
    }
}

function Assert-FiniteVector {
    param([object]$Vector, [string]$Description)

    $components = @($Vector)
    if ($components.Count -ne 3) {
        throw "Capture manifest $Description must contain exactly three components."
    }

    foreach ($component in $components) {
        $value = [double]$component
        if ([double]::IsNaN($value) -or [double]::IsInfinity($value)) {
            throw "Capture manifest $Description contains a non-finite component."
        }
    }
}

function Assert-ExactVector {
    param([object]$Actual, [object]$Expected, [string]$Description)

    $actualComponents = @($Actual)
    $expectedComponents = @($Expected)
    if ($actualComponents.Count -ne 3 -or $expectedComponents.Count -ne 3) {
        throw "Capture manifest $Description must contain exactly three components."
    }

    for ($index = 0; $index -lt 3; $index++) {
        Assert-ExactNumber -Actual $actualComponents[$index] -Expected $expectedComponents[$index] -Description "$Description component $index"
    }
}

function Get-ExpectedSimulationHour {
    param([long]$Tick, [double]$DayLengthSeconds, [double]$TickIntervalSeconds)

    if ($DayLengthSeconds -le 0 -or $TickIntervalSeconds -le 0) {
        throw "Capture manifest simulation time settings must be positive."
    }

    [single]$hour = 10.5
    $hoursPerTick = 24.0 * $TickIntervalSeconds / $DayLengthSeconds
    for ($index = 0; $index -lt $Tick; $index++) {
        $hour = [single]($hour + $hoursPerTick)
        while ($hour -ge 24.0) {
            $hour = [single]($hour - 24.0)
        }
    }

    return $hour
}

function Get-VectorDistance {
    param([object]$Left, [object]$Right)

    $leftComponents = @($Left)
    $rightComponents = @($Right)
    if ($leftComponents.Count -ne 3 -or $rightComponents.Count -ne 3) {
        throw "Capture manifest contribution vectors must contain exactly three components."
    }

    $sum = 0.0
    for ($index = 0; $index -lt 3; $index++) {
        $difference = [double]$leftComponents[$index] - [double]$rightComponents[$index]
        $sum += $difference * $difference
    }
    return [math]::Sqrt($sum)
}

function Get-PngDimensions {
    param([string]$Path)

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        [byte[]]$header = New-Object byte[] 24
        $offset = 0
        while ($offset -lt $header.Length) {
            $read = $stream.Read($header, $offset, $header.Length - $offset)
            if ($read -le 0) {
                throw "Capture image '$Path' is too short to contain a PNG IHDR header."
            }
            $offset += $read
        }

        [byte[]]$signature = 137, 80, 78, 71, 13, 10, 26, 10
        for ($index = 0; $index -lt $signature.Length; $index++) {
            if ($header[$index] -ne $signature[$index]) {
                throw "Capture image '$Path' is not a PNG file."
            }
        }
        if ([System.Text.Encoding]::ASCII.GetString($header, 12, 4) -cne "IHDR") {
            throw "Capture image '$Path' does not begin with a PNG IHDR chunk."
        }

        [uint32]$width = (([uint32]$header[16] -shl 24) -bor ([uint32]$header[17] -shl 16) -bor ([uint32]$header[18] -shl 8) -bor [uint32]$header[19])
        [uint32]$height = (([uint32]$header[20] -shl 24) -bor ([uint32]$header[21] -shl 16) -bor ([uint32]$header[22] -shl 8) -bor [uint32]$header[23])
        if ($width -eq 0 -or $height -eq 0 -or $width -gt [int]::MaxValue -or $height -gt [int]::MaxValue) {
            throw "Capture image '$Path' has invalid PNG dimensions ${width}x${height}."
        }

        return [pscustomobject]@{ Width = [int]$width; Height = [int]$height }
    }
    finally {
        $stream.Dispose()
    }
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

function Remove-DirectoryWithRetry {
    param(
        [string]$Path,
        [int]$TimeoutMilliseconds = 30000,
        [int]$RetryDelayMilliseconds = 250
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }
    if ($TimeoutMilliseconds -le 0 -or $RetryDelayMilliseconds -le 0) {
        throw "Cleanup timeout and retry delay must be positive."
    }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $lastError = $null
    $attempt = 0
    do {
        $attempt++
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        }
        catch {
            $lastError = $_
        }

        if (-not (Test-Path -LiteralPath $Path)) {
            return
        }

        if ($stopwatch.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
            Start-Sleep -Milliseconds ([Math]::Min($RetryDelayMilliseconds, $TimeoutMilliseconds - [int]$stopwatch.ElapsedMilliseconds))
        }
    }
    while ($stopwatch.ElapsedMilliseconds -lt $TimeoutMilliseconds)

    $detail = if ($null -ne $lastError) { $lastError.Exception.Message } else { "the directory still exists after Remove-Item returned" }
    throw "Capture scratch directory '$Path' could not be removed after $attempt attempts over $($stopwatch.ElapsedMilliseconds)ms. Close processes retaining compiler files and remove it manually. Last error: $detail"
}

function Invoke-ProcessAndWait {
    param(
        [string]$Executable,
        [string[]]$Arguments,
        [string]$WorkingDirectory
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    # Inherit standard handles: GUI rendering remains available and no redirected stream can deadlock.
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
            throw "Could not start capture executable '$Executable'."
        }

        $process.WaitForExit()
        return $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
}

$godot = Resolve-Executable -PathOrCommand $GodotPath -Label "Godot"
$dotnet = Resolve-Executable -PathOrCommand $DotnetPath -Label ".NET SDK"
$version = (& $godot --version 2>&1 | Out-String).Trim()
if ($version -notmatch '(?<!\d)4\.6\.2(?!\d)') {
    throw "V3-W2-VIS requires Godot 4.6.2; '$godot' reported '$version'."
}

$gitSha = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw "Could not resolve the build SHA." }
$dirty = (git status --porcelain --untracked-files=all)
if (-not [string]::IsNullOrWhiteSpace($dirty)) {
    throw "Capture evidence requires a clean build tree. Commit/stash changes before using this route."
}

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot "docs\evidence\v3-w2-vis\$gitSha"
}
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $OutputPath) {
    throw "Capture output already exists: $OutputPath"
}
[System.IO.Directory]::CreateDirectory($OutputPath) | Out-Null

$workRoot = Join-Path $OutputPath ".capture-work"
$tempRoot = Join-Path $workRoot "temp"
$dotnetHome = Join-Path $workRoot "dotnet-home"
[System.IO.Directory]::CreateDirectory($tempRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($dotnetHome) | Out-Null

$savedEnvironment = @{}
foreach ($name in @("TMP", "TEMP", "DOTNET_CLI_HOME")) {
    $savedEnvironment[$name] = [System.Environment]::GetEnvironmentVariable($name, "Process")
}

try {
    # Isolate temporary compiler/CLI writes. The work directory is deleted in finally so no
    # machine-specific scratch is retained with otherwise reviewable evidence.
    [System.Environment]::SetEnvironmentVariable("TMP", $tempRoot, "Process")
    [System.Environment]::SetEnvironmentVariable("TEMP", $tempRoot, "Process")
    [System.Environment]::SetEnvironmentVariable("DOTNET_CLI_HOME", $dotnetHome, "Process")

    $projectPath = Join-Path $repoRoot "src\societies\Societies.csproj"
    $buildArguments = @("build", $projectPath, "--configuration", "Debug", "--no-restore", "--nologo", "-t:Rebuild")
    & $dotnet @buildArguments
    $buildExitCode = $LASTEXITCODE
    if ($buildExitCode -ne 0) {
        throw "Clean-source Debug build failed with exit code $buildExitCode; capture was not launched."
    }

    $managedAssembly = Join-Path $repoRoot "src\societies\.godot\mono\temp\bin\Debug\Societies.dll"
    if (-not (Test-Path -LiteralPath $managedAssembly -PathType Leaf)) {
        throw "Clean-source Debug build did not produce managed assembly '$managedAssembly'."
    }
    $assemblyInfo = Get-Item -LiteralPath $managedAssembly
    if ($assemblyInfo.Length -le 0) {
        throw "Clean-source Debug build produced an empty managed assembly '$managedAssembly'."
    }
    $assemblyHash = (Get-FileHash -LiteralPath $managedAssembly -Algorithm SHA256).Hash.ToLowerInvariant()

    $arguments = @("--path", (Join-Path $repoRoot "src\societies"), "res://tests/VisualCaptureRunner.tscn", "--", "--output-dir", $OutputPath, "--git-sha", $gitSha)
    if ($Headless) { $arguments = @("--headless") + $arguments }
    $captureExitCode = Invoke-ProcessAndWait -Executable $godot -Arguments $arguments -WorkingDirectory $repoRoot
    if ($captureExitCode -ne 0) { throw "Visual capture runner exited with code $captureExitCode." }

    $manifestPath = Join-Path $OutputPath "capture-manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Capture manifest was not produced." }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ([int](Require-ManifestProperty $manifest "SchemaVersion") -ne 4) {
        throw "Capture manifest schema must be version 4."
    }
    if ((Require-ManifestProperty $manifest "BuildSha") -cne $gitSha -or
        (Require-ManifestProperty $manifest "Scenario") -cne "empty_stores" -or
        [int](Require-ManifestProperty $manifest "Seed") -ne 1701) {
        throw "Capture manifest identity does not match the canonical V3-W2-VIS contract."
    }
    $terminalCrisisTick = [long](Require-ManifestProperty $manifest "TerminalCrisisTick")
    if ($terminalCrisisTick -le 0) { throw "Capture manifest terminal crisis tick must be positive." }
    if ([int](Require-ManifestProperty $manifest "TerminalCrisisEventCount") -ne 8148 -or
        (Require-ManifestProperty $manifest "TerminalCrisisTraceSha256") -cne "69f3e22402e31a53b1d4c16899883956fcc5fdb14fbe47d8a4eb8baef007174f") {
        throw "Capture manifest terminal-crisis provenance does not match the canonical 10.5 reference trace."
    }
    Assert-ExactNumber -Actual (Require-ManifestProperty $manifest "LightingHour") -Expected 10.5 -Description "lighting hour"
    Assert-ExactNumber -Actual (Require-ManifestProperty $manifest "LightingMultiplier") -Expected 1.0 -Description "lighting multiplier"
    $settlementAnimationPhase = [double](Require-ManifestProperty $manifest "SettlementAnimationPhase")
    Assert-ExactNumber -Actual $settlementAnimationPhase -Expected 0.0 -Description "settlement animation phase"
    $simulationDayLengthSeconds = [double](Require-ManifestProperty $manifest "SimulationDayLengthSeconds")
    $simulationTickIntervalSeconds = [double](Require-ManifestProperty $manifest "SimulationTickIntervalSeconds")
    $graphics = Require-ManifestProperty $manifest "GraphicsSettings"
    if ([string]::IsNullOrWhiteSpace([string](Require-ManifestProperty $graphics "RuntimeRendererMethod")) -or
        [string]::IsNullOrWhiteSpace([string](Require-ManifestProperty $graphics "ProjectRendererMethod"))) {
        throw "Capture manifest must record the active runtime renderer and project renderer setting."
    }
    Assert-ExactNumber -Actual (Require-ManifestProperty $manifest "ResolutionWidth") -Expected 1920 -Description "capture width"
    Assert-ExactNumber -Actual (Require-ManifestProperty $manifest "ResolutionHeight") -Expected 1080 -Description "capture height"

    $required = @("arrival", "settlement_overview", "contribution_point", "citizen_inspection", "terminal_crisis")
    $manifestPresets = @(Require-ManifestProperty $manifest "Presets")
    if ($manifestPresets.Count -ne $required.Count -or -not (($manifestPresets -join "`n") -ceq ($required -join "`n"))) {
        throw "Capture manifest preset IDs must be exactly the five ordered V3-W2-VIS presets."
    }

    $images = @(Require-ManifestProperty $manifest "Images")
    if ($images.Count -ne $required.Count) { throw "Capture manifest does not contain five image records." }
    for ($index = 0; $index -lt $required.Count; $index++) {
        $preset = $required[$index]
        $image = $images[$index]
        if ((Require-ManifestProperty $image "PresetId") -cne $preset -or
            (Require-ManifestProperty $image "SelectedPresetId") -cne $preset) {
            throw "Capture manifest image record $index does not preserve the expected selected preset '$preset'."
        }

        $expectedTick = switch ($preset) {
            "citizen_inspection" { 1 }
            "terminal_crisis" { $terminalCrisisTick }
            default { 0 }
        }
        $expectedTerminal = $preset -eq "terminal_crisis"
        $expectedFov = switch ($preset) {
            "arrival" { 70.0 }
            "settlement_overview" { 62.0 }
            "contribution_point" { 66.0 }
            "citizen_inspection" { 54.0 }
            "terminal_crisis" { 62.0 }
            default { throw "Capture manifest has an unknown preset '$preset'." }
        }
        if ([long](Require-ManifestProperty $image "ExpectedSimulationTick") -ne $expectedTick -or
            [bool](Require-ManifestProperty $image "ExpectedTerminalCrisis") -ne $expectedTerminal -or
            (Require-ManifestProperty $image "ExpectedSelectedPresetId") -cne $preset -or
            [long](Require-ManifestProperty $image "SimulationTick") -ne $expectedTick -or
            [bool](Require-ManifestProperty $image "IsTerminalCrisis") -ne $expectedTerminal) {
            throw "Capture manifest image '$preset' has invalid expected or actual tick/terminal-state metadata."
        }

        $expectedSimulationHour = Get-ExpectedSimulationHour -Tick $expectedTick -DayLengthSeconds $simulationDayLengthSeconds -TickIntervalSeconds $simulationTickIntervalSeconds
        Assert-ExactNumber -Actual (Require-ManifestProperty $image "ExpectedSimulationHour") -Expected $expectedSimulationHour -Description "expected simulation hour for '$preset'"
        Assert-ExactNumber -Actual (Require-ManifestProperty $image "SimulationHour") -Expected $expectedSimulationHour -Description "actual simulation hour for '$preset'"
        if ($expectedTick -eq 0) {
            Assert-ExactNumber -Actual (Require-ManifestProperty $image "SimulationHour") -Expected 10.5 -Description "tick-zero simulation hour for '$preset'"
        }
        Assert-ExactNumber -Actual (Require-ManifestProperty $image "ExpectedSettlementAnimationPhase") -Expected $settlementAnimationPhase -Description "expected settlement animation phase for '$preset'"
        Assert-ExactNumber -Actual (Require-ManifestProperty $image "SettlementAnimationPhase") -Expected $settlementAnimationPhase -Description "actual settlement animation phase for '$preset'"

        $presentation = Require-ManifestProperty $image "Presentation"
        if ([bool](Require-ManifestProperty $presentation "IsDebugVisible") -or
            (Require-ManifestProperty $presentation "TerrainOverlayMode") -cne "None" -or
            -not [bool](Require-ManifestProperty $presentation "IsPresentationLightingLocked")) {
            throw "Capture manifest image '$preset' must hide debug UI, disable terrain overlays, and retain the lighting lock."
        }
        Assert-ExactNumber -Actual (Require-ManifestProperty $presentation "PresentationLightingHour") -Expected 10.5 -Description "presentation lighting hour for '$preset'"
        Assert-ExactNumber -Actual (Require-ManifestProperty $presentation "PresentationLightingMultiplier") -Expected 1.0 -Description "presentation lighting multiplier for '$preset'"

        $crisis = Require-ManifestProperty $image "Crisis"
        if ($preset -eq "terminal_crisis") {
            if ((Require-ManifestProperty $crisis "Outcome") -cne "Collapsed" -or
                (Require-ManifestProperty $crisis "CollapseCause") -cne "IncapacitatedHold" -or
                -not ([string](Require-ManifestProperty $crisis "CausalSummary")).StartsWith("Collapsed: incapacity held ", [System.StringComparison]::Ordinal)) {
                throw "Capture manifest terminal crisis must record Collapsed/IncapacitatedHold and its causal summary."
            }
        }
        elseif ((Require-ManifestProperty $crisis "Outcome") -cne "Active" -or
                (Require-ManifestProperty $crisis "CollapseCause") -cne "None" -or
                -not [string]::IsNullOrEmpty([string](Require-ManifestProperty $crisis "CausalSummary"))) {
            throw "Capture manifest non-terminal image '$preset' must retain an active crisis without a collapse cause."
        }

        $contribution = Require-ManifestProperty $image "Contribution"
        if ($preset -eq "contribution_point") {
            if (-not [bool](Require-ManifestProperty $contribution "IsContributionFrame") -or
                -not [bool](Require-ManifestProperty $contribution "PlayerWithinDepotRange") -or
                -not ([string](Require-ManifestProperty $contribution "StatusText")).Contains("Contributed", [System.StringComparison]::Ordinal)) {
                throw "Capture manifest contribution image must record the canonical in-range player/depot pose and visible success cue."
            }
            $range = [double](Require-ManifestProperty $contribution "ContributionRangeMeters")
            if ($range -le 0) { throw "Capture manifest contribution image has a non-positive interaction range." }
            $playerPosition = Require-ManifestProperty $contribution "PlayerPosition"
            $depotPosition = Require-ManifestProperty $contribution "DepotPosition"
            Assert-FiniteVector -Vector $playerPosition -Description "contribution player position"
            Assert-FiniteVector -Vector $depotPosition -Description "contribution depot position"
            if ((Get-VectorDistance -Left $playerPosition -Right $depotPosition) -gt $range) {
                throw "Capture manifest contribution player position is outside the recorded depot interaction range."
            }
        }
        elseif ([bool](Require-ManifestProperty $contribution "IsContributionFrame")) {
            throw "Capture manifest non-contribution image '$preset' cannot claim contribution fixture state."
        }

        $camera = Require-ManifestProperty $image "Camera"
        $canonicalCamera = Require-ManifestProperty $image "CanonicalCamera"
        $expectedCameraMode = if ($preset -in @("arrival", "contribution_point")) { "Player" } else { "Observer" }
        if ((Require-ManifestProperty $camera "CameraMode") -cne $expectedCameraMode) {
            throw "Capture manifest image '$preset' has camera mode '$($camera.CameraMode)'; expected '$expectedCameraMode'."
        }
        foreach ($cameraField in @("CameraPosition", "CameraRotation", "PlayerBodyPosition", "PlayerBodyRotation")) {
            Assert-FiniteVector -Vector (Require-ManifestProperty $camera $cameraField) -Description "camera $cameraField for '$preset'"
            Assert-ExactVector -Actual (Require-ManifestProperty $camera $cameraField) -Expected (Require-ManifestProperty $canonicalCamera $cameraField) -Description "canonical camera $cameraField for '$preset'"
        }
        Assert-ExactNumber -Actual (Require-ManifestProperty $camera "FieldOfView") -Expected $expectedFov -Description "camera field of view for '$preset'"
        Assert-ExactNumber -Actual (Require-ManifestProperty $canonicalCamera "FieldOfView") -Expected $expectedFov -Description "canonical camera field of view for '$preset'"
        Assert-ExactNumber -Actual (Require-ManifestProperty $camera "FieldOfView") -Expected ([double](Require-ManifestProperty $canonicalCamera "FieldOfView")) -Description "settled camera field of view for '$preset'"
        $selectedCitizenId = [string](Require-ManifestProperty $image "SelectedCitizenId")
        if ($preset -eq "citizen_inspection" -and [string]::IsNullOrWhiteSpace($selectedCitizenId)) {
            throw "Capture manifest citizen-inspection image does not identify its selected assigned citizen."
        }

        $file = [string](Require-ManifestProperty $image "File")
        if ([string]::IsNullOrWhiteSpace($file)) { throw "Capture manifest image '$preset' has no file name." }
        $imagePath = [System.IO.Path]::GetFullPath((Join-Path $OutputPath $file))
        if (-not $imagePath.StartsWith($OutputPath + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -or
            -not (Test-Path -LiteralPath $imagePath -PathType Leaf) -or
            (Get-Item -LiteralPath $imagePath).Length -le 0) {
            throw "Capture image '$preset' is missing, outside the evidence directory, or empty."
        }
        $pngDimensions = Get-PngDimensions -Path $imagePath
        if ($pngDimensions.Width -ne 1920 -or $pngDimensions.Height -ne 1080) {
            throw "Capture image '$preset' is $($pngDimensions.Width)x$($pngDimensions.Height); expected 1920x1080."
        }
        if ([int](Require-ManifestProperty $image "PixelWidth") -ne $pngDimensions.Width -or
            [int](Require-ManifestProperty $image "PixelHeight") -ne $pngDimensions.Height) {
            throw "Capture manifest image '$preset' pixel dimensions do not match its PNG header."
        }
    }

    $reproduction = [ordered]@{
        schemaVersion = 2
        buildSha = $gitSha
        scenario = "empty_stores"
        seed = 1701
        godotPath = $godot
        godotVersion = $version
        dotnetPath = $dotnet
        build = [ordered]@{
            configuration = "Debug"
            project = "src/societies/Societies.csproj"
            command = "$dotnet build src/societies/Societies.csproj --configuration Debug --no-restore --nologo -t:Rebuild"
            exitCode = $buildExitCode
            managedAssembly = "src/societies/.godot/mono/temp/bin/Debug/Societies.dll"
            managedAssemblySha256 = $assemblyHash
            managedAssemblyBytes = $assemblyInfo.Length
        }
        command = ".\\scripts\\capture-v3-w2-vis.ps1 -GodotPath `"$godot`" -DotnetPath `"$dotnet`""
        capture = [ordered]@{
            launch = "System.Diagnostics.Process; UseShellExecute=false; inherited standard handles"
            exitCode = $captureExitCode
        }
        headless = [bool]$Headless
        outputPath = $OutputPath
        isolatedEnvironment = [ordered]@{
            temporaryDirectory = $tempRoot
            dotnetCliHome = $dotnetHome
            nugetPackages = "inherited restored package cache; build uses --no-restore"
        }
        graphicsSettings = $graphics
    }
    Write-Utf8NoBom -Path (Join-Path $OutputPath "reproduction.json") -Content ($reproduction | ConvertTo-Json -Depth 6)
    Write-Host "V3-W2-VIS capture completed: $OutputPath"
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [System.Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if (Test-Path -LiteralPath $workRoot) {
        Remove-DirectoryWithRetry -Path $workRoot
    }
}
