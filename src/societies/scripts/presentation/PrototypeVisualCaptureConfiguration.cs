using Godot;
using System;
using System.Collections.Generic;

namespace Societies.Presentation
{
    /// <summary>Pure, versioned capture contract for the W2 visual reference set.</summary>
    public static class PrototypeVisualCaptureConfiguration
    {
        public const string ScenarioId = "empty_stores";
        public const int SimulationSeed = 1701;
        public const long InitialTick = 0;
        public const long TerminalCrisisTick = 9777;
        public const int TerminalCrisisEventCount = 8148;
        public const string TerminalCrisisTraceSha256 = "69f3e22402e31a53b1d4c16899883956fcc5fdb14fbe47d8a4eb8baef007174f";
        public const long CitizenInspectionTick = 1;
        public const ulong ContributionInputFrame = 1701;
        public const int ContributionLogQuantity = 3;
        public const float LightingHour = 10.5f;
        public const float LightingMultiplier = 1.0f;
        public const double SettlementAnimationPhase = 0.0;

        private static readonly IReadOnlyDictionary<string, PrototypeVisualCapturePreset> Presets =
            new Dictionary<string, PrototypeVisualCapturePreset>(StringComparer.Ordinal)
            {
                ["arrival"] = new("arrival", PrototypeVisualCaptureCameraKind.Player, new Vector3(0, 2.1f, -17), new Vector3(0, 1.7f, 0), 70.0f),
                ["settlement_overview"] = new("settlement_overview", PrototypeVisualCaptureCameraKind.Observer, new Vector3(-36, 42, 36), new Vector3(0, 1.0f, 0), 62.0f),
                ["contribution_point"] = new("contribution_point", PrototypeVisualCaptureCameraKind.Player, new Vector3(-8, 3.1f, -9), new Vector3(-3.6f, 1.0f, -2.2f), 66.0f),
                ["citizen_inspection"] = new("citizen_inspection", PrototypeVisualCaptureCameraKind.Observer, new Vector3(-11.0f, 7.5f, 11.0f), new Vector3(0, 1.15f, 0), 54.0f),
                ["terminal_crisis"] = new("terminal_crisis", PrototypeVisualCaptureCameraKind.Observer, new Vector3(17, 12, 18), new Vector3(0, 1.2f, 0), 62.0f)
            };

        public static IEnumerable<string> PresetIds => Presets.Keys;

        public static bool TryGetPreset(string presetId, out PrototypeVisualCapturePreset preset) =>
            Presets.TryGetValue(presetId, out preset);
    }

    public enum PrototypeVisualCaptureCameraKind { Player, Observer }

    public readonly record struct PrototypeVisualCapturePreset(
        string Id,
        PrototypeVisualCaptureCameraKind CameraKind,
        Vector3 CameraOffset,
        Vector3 LookAtOffset,
        float FieldOfView);

    public readonly record struct PrototypeVisualCaptureMetadata(
        string ScenarioId,
        int SimulationSeed,
        long SimulationTick,
        float LightingHour,
        float LightingMultiplier,
        string SelectedPresetId,
        bool IsTerminalCrisis,
        float SimulationHour);

    public readonly record struct PrototypeVisualCapturePoseMetadata(
        string CameraMode,
        Vector3 CameraPosition,
        Vector3 CameraRotation,
        Vector3 PlayerBodyPosition,
        Vector3 PlayerBodyRotation);
}
