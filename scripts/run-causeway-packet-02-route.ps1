param(
    [string]$GodotPath = $env:GODOT_BIN,
    [string]$OutputDirectory = 'artifacts/performance/causeway-packet-02-route',
    [ValidateRange(1, 3600)]
    [int]$WarmupFrames = 120,
    [ValidateRange(40, 3600)]
    [int]$MeasuredFrames = 300,
    [ValidateRange(30, 1800)]
    [int]$ChildTimeoutSeconds = 300,
    [switch]$AllowDirtySourceForSmoke,
    [switch]$FixedDeltaOnlyDiagnostic,
    [switch]$RealtimeOnlyDiagnostic,
    [switch]$ReuseExistingExport,
    [switch]$ReuseValidationOnly,
    [string]$VerifyTrialCausewayArtifactOnly = '',
    [string]$VerifyExportAttestationOnly = '',
    [string]$ExpectedAttestationRequestPath = '',
    [switch]$ProfileContractOnly
)

# Packet 01's immutable v4 evidence is deliberately not rewritten. The shared runner owns the
# exact packet02-v5 identity as one immutable profile rather than caller-composable tuple fields.
$ErrorActionPreference = 'Stop'
$parameters = @{
    OutputDirectory = $OutputDirectory
    WarmupFrames = $WarmupFrames
    MeasuredFrames = $MeasuredFrames
    ChildTimeoutSeconds = $ChildTimeoutSeconds
    Profile = 'packet02-v5'
    AllowDirtySourceForSmoke = $AllowDirtySourceForSmoke
    FixedDeltaOnlyDiagnostic = $FixedDeltaOnlyDiagnostic
    RealtimeOnlyDiagnostic = $RealtimeOnlyDiagnostic
    ReuseExistingExport = $ReuseExistingExport
    ReuseValidationOnly = $ReuseValidationOnly
    VerifyTrialCausewayArtifactOnly = $VerifyTrialCausewayArtifactOnly
    VerifyExportAttestationOnly = $VerifyExportAttestationOnly
    ExpectedAttestationRequestPath = $ExpectedAttestationRequestPath
    ProfileContractOnly = $ProfileContractOnly
}
if (-not [string]::IsNullOrWhiteSpace($GodotPath)) { $parameters.GodotPath = $GodotPath }

& (Join-Path $PSScriptRoot 'run-accepted-scene-baseline.ps1') @parameters
exit $LASTEXITCODE
