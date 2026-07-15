using Godot;
using Societies.Core;
using Societies.Presentation;
using Societies.Simulation;
using Societies.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Societies.Tests
{
    /// <summary>
    /// Produces the five reproducible V3-W2-VIS reference frames. It deliberately drives only
    /// GameManager's deterministic visual-capture API; it does not synthesize world state.
    /// </summary>
    public partial class VisualCaptureRunner : Node
    {
        private static readonly string[] RequiredPresets =
        {
            "arrival", "settlement_overview", "contribution_point", "citizen_inspection", "terminal_crisis"
        };

        public override void _Ready() => RunAsync();

        private async void RunAsync()
        {
            try
            {
                string outputDirectory = ResolveRequiredArgument("--output-dir");
                Directory.CreateDirectory(outputDirectory);
                string gitSha = ResolveOptionalArgument("--git-sha") ?? "unknown";
                await CaptureAsync(outputDirectory, gitSha);
                GD.Print("V3-W2-VIS capture completed.");
                GetTree().Quit(0);
            }
            catch (Exception exception)
            {
                GD.PrintErr($"V3-W2-VIS capture failed: {exception}");
                GetTree().Quit(1);
            }
        }

        private async Task CaptureAsync(string outputDirectory, string gitSha)
        {
            PackedScene mainScene = GD.Load<PackedScene>("res://scenes/main.tscn")
                ?? throw new InvalidOperationException("Main scene failed to load.");
            GameManager manager = mainScene.Instantiate<GameManager>();
            manager.ConfigureVisualCaptureStartup();
            AddChild(manager);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            if (!manager.ApplyVisualCaptureScenario())
            {
                throw new InvalidOperationException("Canonical visual capture scenario could not be applied.");
            }
            LockSettlementAnimation(manager);

            string[] actualPresets = manager.VisualCapturePresetIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (!RequiredPresets.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(actualPresets))
            {
                throw new InvalidOperationException("Visual capture presets do not match the V3-W2-VIS contract.");
            }

            List<VisualCaptureImageRecord> images = new();
            VisualCaptureGraphicsRecord graphics = CaptureGraphicsSettings();
            float simulationDayLengthSeconds = GetSimulationDayLengthSeconds(manager);
            foreach (string presetId in RequiredPresets)
            {
                long expectedTick = GetExpectedTick(presetId);
                bool expectedTerminalCrisis = presetId == "terminal_crisis";
                if (presetId == "citizen_inspection")
                {
                    if (!manager.ApplyVisualCaptureScenario())
                    {
                        throw new InvalidOperationException("Could not reset the canonical citizen-inspection capture scenario.");
                    }
                    LockSettlementAnimation(manager);
                    if (
                        !manager.AdvanceVisualCaptureToTick(PrototypeVisualCaptureConfiguration.CitizenInspectionTick) ||
                        !manager.SelectVisualCaptureInspectionCitizen())
                    {
                        throw new InvalidOperationException("Could not prepare the canonical assigned citizen-inspection frame.");
                    }
                }
                if (presetId == "terminal_crisis")
                {
                    if (!manager.AdvanceVisualCaptureToTick(PrototypeVisualCaptureConfiguration.TerminalCrisisTick))
                    {
                        throw new InvalidOperationException("Could not advance the canonical terminal-crisis capture through authoritative ticks.");
                    }

                }

                if (presetId == "contribution_point" && !manager.PositionVisualCapturePlayerAtDepot())
                {
                    throw new InvalidOperationException("Could not place the visual-capture player in central-depot contribution range.");
                }

                if (!manager.SelectVisualCapturePreset(presetId))
                {
                    throw new InvalidOperationException($"Could not select visual capture preset '{presetId}'.");
                }

                FreezeCaptureControllers(manager);
                double expectedAnimationPhase = LockSettlementAnimation(manager);
                VisualCaptureCameraRecord canonicalCamera = ToCameraRecord(manager);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (presetId == "contribution_point")
                {
                    if (!manager.SubmitVisualCaptureContribution())
                    {
                        throw new InvalidOperationException("Canonical contribution capture did not produce a successful authoritative player contribution.");
                    }

                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                AssertCitizenInspectionAssignment(manager, presetId);
                PrototypeVisualCaptureMetadata metadata = manager.VisualCaptureMetadata;
                float expectedSimulationHour = DeriveSimulationHour(simulationDayLengthSeconds, expectedTick);
                VisualCaptureCrisisRecord crisis = CaptureCrisisState(manager, expectedTerminalCrisis);
                VisualCaptureContributionRecord contribution = CaptureContributionState(manager, presetId);
                double settledAnimationPhase = GetSettlementAnimationPhase(manager);
                VisualCapturePresentationRecord presentation = CapturePresentationState(manager);
                VisualCaptureCameraRecord settledCamera = ToCameraRecord(manager);
                AssertExpectedCaptureState(
                    metadata,
                    presetId,
                    expectedTick,
                    expectedSimulationHour,
                    expectedTerminalCrisis,
                    crisis,
                    expectedAnimationPhase,
                    settledAnimationPhase,
                    presentation,
                    canonicalCamera,
                    settledCamera);
                string imageFile = $"{presetId}.png";
                string imagePath = Path.Combine(outputDirectory, imageFile);
                Error saveResult = GetViewport().GetTexture().GetImage().SavePng(imagePath);
                if (saveResult != Error.Ok)
                {
                    throw new InvalidOperationException($"Could not save '{imageFile}': {saveResult}.");
                }

                images.Add(new VisualCaptureImageRecord(
                    presetId,
                    imageFile,
                    expectedTick,
                    expectedSimulationHour,
                    expectedTerminalCrisis,
                    presetId,
                    metadata.SimulationTick,
                    metadata.SimulationHour,
                    metadata.IsTerminalCrisis,
                    metadata.SelectedPresetId,
                    manager.SelectedVisualCaptureCitizenId,
                    crisis,
                    contribution,
                    expectedAnimationPhase,
                    settledAnimationPhase,
                    presentation,
                    canonicalCamera,
                    settledCamera));
            }

            PrototypeVisualCaptureMetadata finalMetadata = manager.VisualCaptureMetadata;
            VisualCaptureManifest manifest = new(
                4,
                gitSha,
                finalMetadata.ScenarioId,
                finalMetadata.SimulationSeed,
                PrototypeVisualCaptureConfiguration.TerminalCrisisTick,
                PrototypeVisualCaptureConfiguration.TerminalCrisisEventCount,
                PrototypeVisualCaptureConfiguration.TerminalCrisisTraceSha256,
                PrototypeVisualCaptureConfiguration.LightingHour,
                PrototypeVisualCaptureConfiguration.LightingMultiplier,
                PrototypeVisualCaptureConfiguration.SettlementAnimationPhase,
                simulationDayLengthSeconds,
                PrototypeSimulationTime.TickIntervalSeconds,
                GetViewport().GetVisibleRect().Size.X,
                GetViewport().GetVisibleRect().Size.Y,
                graphics,
                RequiredPresets,
                images);
            string manifestPath = Path.Combine(outputDirectory, "capture-manifest.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            manager.QueueFree();
        }

        private static long GetExpectedTick(string presetId) => presetId switch
        {
            "citizen_inspection" => PrototypeVisualCaptureConfiguration.CitizenInspectionTick,
            "terminal_crisis" => PrototypeVisualCaptureConfiguration.TerminalCrisisTick,
            _ => PrototypeVisualCaptureConfiguration.InitialTick
        };

        private static void AssertCitizenInspectionAssignment(GameManager manager, string presetId)
        {
            if (presetId != "citizen_inspection")
            {
                return;
            }

            PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI")
                ?? throw new InvalidOperationException("Visual capture citizen inspection requires the prototype HUD.");
            if (string.IsNullOrWhiteSpace(manager.SelectedVisualCaptureCitizenId) ||
                !hud.InspectorText.Contains("Why:", StringComparison.Ordinal) ||
                hud.InspectorText.Contains("Why: none", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Citizen-inspection capture requires one stable assigned citizen with a causal Why explanation.");
            }
        }

        private static VisualCaptureCameraRecord ToCameraRecord(GameManager manager)
        {
            PrototypeVisualCapturePoseMetadata metadata = manager.VisualCapturePoseMetadata;
            Camera3D camera = (metadata.CameraMode == "Player"
                ? manager.GetNodeOrNull<Camera3D>("World/Players/LocalPlayer/CameraPivot/Camera3D")
                : manager.GetNodeOrNull<Camera3D>("World/Players/ObserverCamera/Camera3D"))
                ?? throw new InvalidOperationException($"Visual capture camera for mode '{metadata.CameraMode}' is unavailable.");
            return new VisualCaptureCameraRecord(
            metadata.CameraMode,
            ToVectorArray(metadata.CameraPosition),
            ToVectorArray(metadata.CameraRotation),
            ToVectorArray(metadata.PlayerBodyPosition),
            ToVectorArray(metadata.PlayerBodyRotation),
            camera.Fov);
        }

        private static float[] ToVectorArray(Vector3 value) => new[] { value.X, value.Y, value.Z };

        private static void FreezeCaptureControllers(GameManager manager)
        {
            foreach (Node controller in new Node[]
            {
                manager.GetNodeOrNull<Node>("World/Players/LocalPlayer"),
                manager.GetNodeOrNull<Node>("World/Players/ObserverCamera")
            }.Where(node => node != null))
            {
                controller.SetProcess(false);
                controller.SetPhysicsProcess(false);
                controller.SetProcessInput(false);
                controller.SetProcessUnhandledInput(false);
            }
        }

        private static double LockSettlementAnimation(GameManager manager)
        {
            PrototypeSettlementHub hub = manager.GetNodeOrNull<PrototypeSettlementHub>("World/Environment/SettlementHub")
                ?? throw new InvalidOperationException("Visual capture requires the settlement hub.");
            hub.SetVisualCaptureAnimationPhase(PrototypeVisualCaptureConfiguration.SettlementAnimationPhase);
            if (hub.IsProcessing() || Math.Abs(hub.AnimationPhase - PrototypeVisualCaptureConfiguration.SettlementAnimationPhase) > 0.000001)
            {
                throw new InvalidOperationException("Visual capture settlement animation did not lock to its fixed phase.");
            }

            return hub.AnimationPhase;
        }

        private static double GetSettlementAnimationPhase(GameManager manager)
        {
            PrototypeSettlementHub hub = manager.GetNodeOrNull<PrototypeSettlementHub>("World/Environment/SettlementHub")
                ?? throw new InvalidOperationException("Visual capture requires the settlement hub.");
            if (hub.IsProcessing())
            {
                throw new InvalidOperationException("Visual capture settlement hub resumed time-dependent processing.");
            }

            return hub.AnimationPhase;
        }

        private static VisualCapturePresentationRecord CapturePresentationState(GameManager manager)
        {
            PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI")
                ?? throw new InvalidOperationException("Visual capture requires the prototype HUD.");
            EnvironmentController environment = manager.GetNodeOrNull<EnvironmentController>("World/Environment/Environment")
                ?? throw new InvalidOperationException("Visual capture requires EnvironmentController presentation state.");
            float lightingHour = environment.PresentationLightingHour
                ?? throw new InvalidOperationException("Visual capture lighting is not presentation-locked.");
            float lightingMultiplier = environment.PresentationLightingMultiplier
                ?? throw new InvalidOperationException("Visual capture lighting multiplier is not presentation-locked.");
            VisualCapturePresentationRecord presentation = new(
                hud.IsDebugVisible,
                manager.CurrentOverlayMode.ToString(),
                environment.IsPresentationLightingLocked,
                lightingHour,
                lightingMultiplier);
            if (presentation.IsDebugVisible || presentation.TerrainOverlayMode != TerrainOverlayMode.None.ToString() ||
                !presentation.IsPresentationLightingLocked ||
                !Mathf.IsEqualApprox(presentation.PresentationLightingHour, PrototypeVisualCaptureConfiguration.LightingHour) ||
                !Mathf.IsEqualApprox(presentation.PresentationLightingMultiplier, PrototypeVisualCaptureConfiguration.LightingMultiplier))
            {
                throw new InvalidOperationException("Visual capture must hide debug UI, clear terrain overlays, and retain the configured presentation lighting lock.");
            }

            return presentation;
        }

        private static float GetSimulationDayLengthSeconds(GameManager manager)
        {
            EnvironmentController environment = manager.GetNodeOrNull<EnvironmentController>("World/Environment/Environment")
                ?? throw new InvalidOperationException("Visual capture requires EnvironmentController time settings.");
            if (!float.IsFinite(environment.DayLengthSeconds) || environment.DayLengthSeconds <= 0.0f)
            {
                throw new InvalidOperationException("Visual capture requires a finite positive simulation day length.");
            }

            return environment.DayLengthSeconds;
        }

        private static float DeriveSimulationHour(float dayLengthSeconds, long expectedTick)
        {
            float hour = PrototypeVisualCaptureConfiguration.LightingHour;
            double hoursPerTick = 24.0 * PrototypeSimulationTime.TickIntervalSeconds / dayLengthSeconds;
            for (long tick = 0; tick < expectedTick; tick++)
            {
                hour = (float)(hour + hoursPerTick);
                while (hour >= 24.0f)
                {
                    hour -= 24.0f;
                }
            }

            return hour;
        }

        private static VisualCaptureCrisisRecord CaptureCrisisState(GameManager manager, bool expectedTerminalCrisis)
        {
            PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI")
                ?? throw new InvalidOperationException("Visual capture requires the prototype HUD crisis summary.");
            string crisisText = hud.CrisisText;
            const string outcomePrefix = "Outcome: ";
            int outcomeIndex = crisisText.IndexOf(outcomePrefix, StringComparison.Ordinal);
            if (!expectedTerminalCrisis)
            {
                if (outcomeIndex >= 0)
                {
                    throw new InvalidOperationException("A non-terminal capture unexpectedly exposes a crisis outcome.");
                }

                return new VisualCaptureCrisisRecord("Active", "None", string.Empty);
            }

            if (outcomeIndex < 0)
            {
                throw new InvalidOperationException("Terminal capture must expose an Outcome causal summary.");
            }

            string causalSummary = crisisText[(outcomeIndex + outcomePrefix.Length)..].Split('\n', 2)[0].Trim();
            if (!causalSummary.StartsWith("Collapsed: incapacity held ", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Terminal capture must be Collapsed via IncapacitatedHold, but reported '{causalSummary}'.");
            }

            return new VisualCaptureCrisisRecord("Collapsed", "IncapacitatedHold", causalSummary);
        }

        private static VisualCaptureGraphicsRecord CaptureGraphicsSettings()
        {
            string rendererMethod = RenderingServer.GetCurrentRenderingMethod();
            if (string.IsNullOrWhiteSpace(rendererMethod))
            {
                throw new InvalidOperationException("Godot did not report an active rendering method.");
            }

            const string rendererSetting = "rendering/renderer/rendering_method";
            string configuredRenderer = ProjectSettings.HasSetting(rendererSetting)
                ? ProjectSettings.GetSetting(rendererSetting).AsString()
                : "engine_default";
            return new VisualCaptureGraphicsRecord(rendererMethod, configuredRenderer);
        }

        private static VisualCaptureContributionRecord CaptureContributionState(GameManager manager, string presetId)
        {
            if (presetId != "contribution_point")
            {
                return new VisualCaptureContributionRecord(false, false, 0.0f, Array.Empty<float>(), Array.Empty<float>(), string.Empty);
            }

            PlayerCharacter player = manager.GetNodeOrNull<PlayerCharacter>("World/Players/LocalPlayer")
                ?? throw new InvalidOperationException("Contribution capture requires the canonical local player.");
            PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI")
                ?? throw new InvalidOperationException("Contribution capture requires the prototype HUD status cue.");
            Vector3 depotPosition = manager.CentralDepotPosition;
            bool withinRange = player.GlobalPosition.DistanceTo(depotPosition) <= player.ContributionRangeMeters;
            if (!withinRange || !hud.StatusText.Contains("Contributed", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Contribution capture must retain the canonical in-range depot pose and visible success cue.");
            }

            return new VisualCaptureContributionRecord(
                true,
                true,
                player.ContributionRangeMeters,
                ToVectorArray(player.GlobalPosition),
                ToVectorArray(depotPosition),
                hud.StatusText);
        }

        private static void AssertExpectedCaptureState(
            PrototypeVisualCaptureMetadata metadata,
            string presetId,
            long expectedTick,
            float expectedSimulationHour,
            bool expectedTerminalCrisis,
            VisualCaptureCrisisRecord crisis,
            double expectedAnimationPhase,
            double settledAnimationPhase,
            VisualCapturePresentationRecord presentation,
            VisualCaptureCameraRecord canonicalCamera,
            VisualCaptureCameraRecord settledCamera)
        {
            if (metadata.SimulationTick != expectedTick ||
                !Mathf.IsEqualApprox(metadata.SimulationHour, expectedSimulationHour) ||
                metadata.IsTerminalCrisis != expectedTerminalCrisis ||
                !string.Equals(metadata.SelectedPresetId, presetId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Capture preset '{presetId}' settled at tick {metadata.SimulationTick} with terminal state " +
                    $"{metadata.IsTerminalCrisis} and simulation hour {metadata.SimulationHour}, expected tick {expectedTick}, " +
                    $"terminal state {expectedTerminalCrisis}, and simulation hour {expectedSimulationHour}.");
            }

            if (Math.Abs(expectedAnimationPhase - PrototypeVisualCaptureConfiguration.SettlementAnimationPhase) > 0.000001 ||
                Math.Abs(settledAnimationPhase - expectedAnimationPhase) > 0.000001)
            {
                throw new InvalidOperationException($"Capture preset '{presetId}' drifted from the fixed settlement animation phase.");
            }

            if (expectedTerminalCrisis &&
                (crisis.Outcome != "Collapsed" || crisis.CollapseCause != "IncapacitatedHold" || string.IsNullOrWhiteSpace(crisis.CausalSummary)))
            {
                throw new InvalidOperationException("Terminal capture did not preserve the required Collapsed/IncapacitatedHold causal state.");
            }

            AssertExactCameraRecord(canonicalCamera, settledCamera, presetId);
            if (!PrototypeVisualCaptureConfiguration.TryGetPreset(presetId, out PrototypeVisualCapturePreset preset) ||
                !Mathf.IsEqualApprox(settledCamera.FieldOfView, preset.FieldOfView))
            {
                throw new InvalidOperationException($"Capture preset '{presetId}' did not retain its configured camera field of view.");
            }

            if (presentation.IsDebugVisible || presentation.TerrainOverlayMode != TerrainOverlayMode.None.ToString() ||
                !presentation.IsPresentationLightingLocked ||
                !Mathf.IsEqualApprox(presentation.PresentationLightingHour, PrototypeVisualCaptureConfiguration.LightingHour) ||
                !Mathf.IsEqualApprox(presentation.PresentationLightingMultiplier, PrototypeVisualCaptureConfiguration.LightingMultiplier))
            {
                throw new InvalidOperationException($"Capture preset '{presetId}' drifted from the required presentation state.");
            }
        }

        private static void AssertExactCameraRecord(
            VisualCaptureCameraRecord expected,
            VisualCaptureCameraRecord actual,
            string presetId)
        {
            if (!string.Equals(expected.CameraMode, actual.CameraMode, StringComparison.Ordinal) ||
                !VectorsMatch(expected.CameraPosition, actual.CameraPosition) ||
                !VectorsMatch(expected.CameraRotation, actual.CameraRotation) ||
                !VectorsMatch(expected.PlayerBodyPosition, actual.PlayerBodyPosition) ||
                !VectorsMatch(expected.PlayerBodyRotation, actual.PlayerBodyRotation) ||
                !Mathf.IsEqualApprox(expected.FieldOfView, actual.FieldOfView))
            {
                throw new InvalidOperationException($"Capture preset '{presetId}' drifted from its canonical camera/player transform while settling frames.");
            }
        }

        private static bool VectorsMatch(float[] expected, float[] actual) =>
            expected.Length == actual.Length && expected.Zip(actual, (left, right) => Mathf.IsEqualApprox(left, right)).All(matches => matches);

        private static string ResolveRequiredArgument(string name) =>
            ResolveOptionalArgument(name) ?? throw new ArgumentException($"Missing required argument '{name}'.");

        private static string? ResolveOptionalArgument(string name)
        {
            string[] arguments = OS.GetCmdlineUserArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }

        private sealed record VisualCaptureImageRecord(
            string PresetId,
            string File,
            long ExpectedSimulationTick,
            float ExpectedSimulationHour,
            bool ExpectedTerminalCrisis,
            string ExpectedSelectedPresetId,
            long SimulationTick,
            float SimulationHour,
            bool IsTerminalCrisis,
            string SelectedPresetId,
            string SelectedCitizenId,
            VisualCaptureCrisisRecord Crisis,
            VisualCaptureContributionRecord Contribution,
            double ExpectedSettlementAnimationPhase,
            double SettlementAnimationPhase,
            VisualCapturePresentationRecord Presentation,
            VisualCaptureCameraRecord CanonicalCamera,
            VisualCaptureCameraRecord Camera);

        private sealed record VisualCaptureCrisisRecord(
            string Outcome,
            string CollapseCause,
            string CausalSummary);

        private sealed record VisualCaptureGraphicsRecord(
            string RuntimeRendererMethod,
            string ProjectRendererMethod);

        private sealed record VisualCaptureContributionRecord(
            bool IsContributionFrame,
            bool PlayerWithinDepotRange,
            float ContributionRangeMeters,
            float[] PlayerPosition,
            float[] DepotPosition,
            string StatusText);

        private sealed record VisualCapturePresentationRecord(
            bool IsDebugVisible,
            string TerrainOverlayMode,
            bool IsPresentationLightingLocked,
            float PresentationLightingHour,
            float PresentationLightingMultiplier);

        private sealed record VisualCaptureCameraRecord(
            string CameraMode,
            float[] CameraPosition,
            float[] CameraRotation,
            float[] PlayerBodyPosition,
            float[] PlayerBodyRotation,
            float FieldOfView);

        private sealed record VisualCaptureManifest(
            int SchemaVersion,
            string BuildSha,
            string Scenario,
            int Seed,
            long TerminalCrisisTick,
            int TerminalCrisisEventCount,
            string TerminalCrisisTraceSha256,
            float LightingHour,
            float LightingMultiplier,
            double SettlementAnimationPhase,
            float SimulationDayLengthSeconds,
            double SimulationTickIntervalSeconds,
            float ResolutionWidth,
            float ResolutionHeight,
            VisualCaptureGraphicsRecord GraphicsSettings,
            IReadOnlyList<string> Presets,
            IReadOnlyList<VisualCaptureImageRecord> Images);
    }
}
