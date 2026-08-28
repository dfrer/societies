param(
    [string]$GodotPath = $env:GODOT_BIN,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectRoot = Join-Path $repositoryRoot 'src\societies'
$scene = 'res://scenes/snow_globe_voxel_foundation.tscn'

if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $command = Get-Command godot -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        $GodotPath = $command.Source
    }
}

if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $wingetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
    $GodotPath = Get-ChildItem -LiteralPath $wingetRoot -Recurse -Filter 'Godot*.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -notlike '*console*' } |
        Select-Object -First 1 -ExpandProperty FullName
}

if ([string]::IsNullOrWhiteSpace($GodotPath) -or -not (Test-Path -LiteralPath $GodotPath -PathType Leaf)) {
    throw 'Godot Mono was not found. Install Godot 4 Mono or pass -GodotPath with its full executable path.'
}

$GodotPath = [System.IO.Path]::GetFullPath($GodotPath)
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'project.godot') -PathType Leaf)) {
    throw "Societies Godot project was not found at $projectRoot"
}

Write-Host "Godot: $GodotPath"
Write-Host "Project: $projectRoot"
Write-Host "Scene: $scene"

if ($VerifyOnly) {
    return
}

& $GodotPath --path $projectRoot $scene
if ($LASTEXITCODE -ne 0) {
    throw "Godot exited with code $LASTEXITCODE."
}
