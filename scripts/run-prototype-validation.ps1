$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Resolve-Godot {
    if ($env:GODOT_BIN) {
        return $env:GODOT_BIN
    }

    $godotCommand = Get-Command godot -ErrorAction SilentlyContinue
    if ($godotCommand) {
        return $godotCommand.Source
    }

    $wingetRoot = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
    if (Test-Path $wingetRoot) {
        $candidate = Get-ChildItem -Path $wingetRoot -Recurse -Filter "Godot*_console.exe" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1

        if ($candidate) {
            return $candidate.FullName
        }
    }

    throw "Godot .NET executable not found. Set GODOT_BIN or install Godot 4 Mono."
}

$godot = Resolve-Godot

Write-Host "Using Godot: $godot"
Invoke-Step { dotnet build "src/societies/Societies.csproj" }
Invoke-Step { dotnet test "tests/Societies.Core.Tests/Societies.Core.Tests.csproj" --configuration Release }
Invoke-Step { & $godot --headless --path "src/societies" --build-solutions --quit }
Invoke-Step { & $godot --headless --path "src/societies" "res://tests/HeadlessTestRunner.tscn" }
