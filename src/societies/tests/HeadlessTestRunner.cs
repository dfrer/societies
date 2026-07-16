using Godot;
using Societies.Core;
using Societies.Multiplayer;
using Societies.Presentation;
using Societies.Simulation;
using Societies.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Societies.Tests
{
    /// <summary>
    /// Headless smoke runner for the authoritative Godot prototype.
    /// Run with: godot --headless --path src/societies res://tests/HeadlessTestRunner.tscn
    /// </summary>
    public partial class HeadlessTestRunner : Node
    {
        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            RunAsync();
        }

        private async void RunAsync()
        {
            PrintHeader();

            try
            {
                await RunAllTests();
            }
            catch (Exception ex)
            {
                _failed++;
                GD.PrintErr($"Headless runner crashed: {ex}");
            }

            PrintSummary();
            GetTree().Quit(_failed > 0 ? 1 : 0);
        }

        private async Task RunAllTests()
        {
            Test_EntityState_Serialization();
            Test_Vector3_Operations();
            Test_Node_Creation();
            Test_SceneTree_Access();
            Test_RunOutputDirectory_IsolatedPerInvocation();
            await Test_MainScene_BootstrapSmoke();
            await Test_MainScene_DepotContributionInputSmoke();
            await Test_MainScene_DirectiveInputSmoke();
            await Test_MainScene_CrisisHudPresentationSmoke();
            Test_VisualCaptureConfigurationAndHudLayout();
            await Test_MainScene_VisualCaptureContractSmoke();
            await Test_MainScene_FrameCatchUpCapSmoke();
            await Test_MainScene_HudRefreshCoalescingSmoke();
            await Test_MainScene_RuntimeMetricsBatchSmoke();
            await Test_MainScene_WorkerVisualizationSmoke();
            await Test_MainScene_CraftingAndSnapshotSmoke();
            await Test_MainScene_ResetAndRestoreSmoke();
            await Test_MainScene_ScenarioSwitchWorldSummarySmoke();
            await Test_MainScene_ObserverAndOverlaySmoke();
            await Test_MainScene_BuildQueueAndInspectorSmoke();
            await Test_MainScene_SettlementLoopSmoke();
            await Test_MainScene_FixedTickSoakSmoke();
        }

        private void Test_EntityState_Serialization()
        {
            try
            {
                EntityState state = new()
                {
                    EntityId = "test-entity",
                    EntityType = "player",
                    Position = new Vector3(10, 5, 20),
                    Rotation = new Vector3(0, 90, 0),
                    Velocity = new Vector3(1, 0, 1),
                    Timestamp = DateTime.UtcNow.Ticks
                };

                Assert(state.EntityId == "test-entity", "Entity ID mismatch");
                Assert(state.Position == new Vector3(10, 5, 20), "Position mismatch");
                Pass(nameof(Test_EntityState_Serialization));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_EntityState_Serialization), ex);
            }
        }

        private void Test_Vector3_Operations()
        {
            try
            {
                Vector3 v1 = new(10, 20, 30);
                Vector3 v2 = new(5, 10, 15);
                Vector3 sum = v1 + v2;
                Vector3 lerped = v1.Lerp(v2, 0.5f);

                Assert(sum == new Vector3(15, 30, 45), "Vector addition failed");
                Assert(lerped == new Vector3(7.5f, 15, 22.5f), "Vector lerp failed");
                Pass(nameof(Test_Vector3_Operations));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_Vector3_Operations), ex);
            }
        }

        private void Test_Node_Creation()
        {
            try
            {
                Node node = new() { Name = "TestNode" };
                Assert(node.Name == "TestNode", "Node name mismatch");
                Assert(!node.IsInsideTree(), "Node should not be in tree yet");

                AddChild(node);

                Assert(node.IsInsideTree(), "Node should be in tree after adding");
                Assert(node.GetParent() == this, "Parent should be this runner");

                node.QueueFree();
                Pass(nameof(Test_Node_Creation));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_Node_Creation), ex);
            }
        }

        private void Test_SceneTree_Access()
        {
            try
            {
                SceneTree? tree = GetTree();
                Assert(tree != null, "Scene tree should be accessible");
                Assert(tree!.Root != null, "Root node should exist");
                Pass(nameof(Test_SceneTree_Access));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_SceneTree_Access), ex);
            }
        }

        private async Task Test_MainScene_BootstrapSmoke()
        {
            Node? scene = null;

            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                NetworkManager network = manager.GetNodeOrNull<NetworkManager>("NetworkManager") ?? throw new Exception("NetworkManager missing");
                PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI") ?? throw new Exception("PrototypeHud missing");
                EnvironmentController? envController = manager.GetNodeOrNull<EnvironmentController>("World/Environment/Environment");
                Assert(envController != null, "EnvironmentController missing (was DayNightCycle)");
                TerrainGenerator terrain = manager.GetNodeOrNull<TerrainGenerator>("World/Systems/Terrain") ?? throw new Exception("TerrainGenerator missing");
                PrototypeScenarioDefinition scenario = LoadCatalogBundle().Scenarios.Resolve("balanced_basin");

                PrototypeRuntimeSnapshot snapshot = manager.CaptureSnapshot();
                float playerSurfaceHeight = terrain.SampleHeight(snapshot.PlayerPosition.ToVector3());

                Assert(manager.IsGameRunning, "GameManager should auto-start the local session");
                Assert(network.IsLocalSession, "NetworkManager should be in local session mode");
                Assert(snapshot.PlayerPosition.Y > playerSurfaceHeight, "Player should spawn above the sampled terrain height");
                Assert(snapshot.Resources.Count(resource => resource.ResourceId == "logs") == scenario.InitialTrees, "Tree spawn count mismatch");
                Assert(snapshot.Resources.Count(resource => resource.ResourceId == "stone") == scenario.InitialRocks, "Rock spawn count mismatch");
                Assert(snapshot.Resources.Count(resource => resource.ResourceId == "berries") == scenario.InitialBerryBushes, "Berry spawn count mismatch");
                Assert(snapshot.Resources.Count(resource => resource.ResourceId == "clay") == scenario.InitialClayDeposits, "Clay spawn count mismatch");
                Assert(snapshot.Resources.Count(resource => resource.ResourceId == "reeds") == scenario.InitialReedBeds, "Reed spawn count mismatch");
                Assert(!string.IsNullOrWhiteSpace(hud.DebugText), "Debug HUD text should not be empty");
                Assert(!string.IsNullOrWhiteSpace(hud.InventoryText), "Inventory HUD text should not be empty");
                Assert(!string.IsNullOrWhiteSpace(hud.CraftingText), "Crafting HUD text should not be empty");
                Assert(!string.IsNullOrWhiteSpace(hud.HelpText), "Help HUD text should not be empty");
                Assert(hud.HelpText.Contains("F11 next build"), "Help HUD should expose build queue controls");

                float initialHour = envController!.CurrentHour;
                manager.StepSimulationTicks(5);
                Assert(manager.SimulationTick == 5, "Simulation tick count should advance deterministically");
                Assert(envController.CurrentHour != initialHour, "Day/night state should advance through the fixed-tick runner");

                Pass(nameof(Test_MainScene_BootstrapSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_BootstrapSmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_DepotContributionInputSmoke()
        {
            Node? scene = null;

            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                manager.SetProcess(false);
                PlayerCharacter player = manager.GetNodeOrNull<PlayerCharacter>("World/Players/LocalPlayer") ??
                    throw new Exception("LocalPlayer missing");
                PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI") ?? throw new Exception("PrototypeHud missing");
                int initialLogs = manager.Stockpile.GetCount("logs");
                manager.Inventory.AddItem("logs", 3);
                manager.Inventory.AddItem("stone_axe", 1);
                player.GlobalPosition = manager.CentralDepotPosition;

                player.ProcessInteractionInput(700);
                player.ProcessInteractionInput(700);

                Assert(manager.Inventory.GetCount("logs") == 0, "Depot input should remove every eligible raw resource");
                Assert(manager.Inventory.GetCount("stone_axe") == 1, "Depot input should keep crafted tools personal");
                Assert(manager.Stockpile.GetCount("logs") == initialLogs + 3, "Depot input should add the exact raw quantity once");
                Assert(hud.StatusText.Contains("Contributed", StringComparison.Ordinal), "Depot input should present deterministic success feedback");
                Assert(hud.InventoryText.Contains("stone axe: 1", StringComparison.Ordinal), "Inventory HUD should retain the crafted tool");
                Assert(!hud.InventoryText.Contains("logs:", StringComparison.Ordinal), "Inventory HUD should remove deposited logs");

                Pass(nameof(Test_MainScene_DepotContributionInputSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_DepotContributionInputSmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_DirectiveInputSmoke()
        {
            Node? scene = null;

            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                manager.SetProcess(false);
                PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI") ?? throw new Exception("PrototypeHud missing");
                Assert(manager.CurrentDirective == PrototypeSettlementDirective.Neutral, "Directive should start neutral");
                Assert(hud.SettlementText.Contains("Directive: Neutral", StringComparison.Ordinal), "HUD should expose neutral directive state");

                manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.Key2 });
                Assert(manager.CurrentDirective == PrototypeSettlementDirective.FoodAndFuel, "Key 2 should select Food & Fuel");
                Assert(hud.SettlementText.Contains("Directive: Food & Fuel", StringComparison.Ordinal), "HUD should expose Food & Fuel");
                Assert(hud.StatusText.Contains("Directive set: Food & Fuel", StringComparison.Ordinal), "Directive input should provide status feedback");

                manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.Key3 });
                Assert(manager.CurrentDirective == PrototypeSettlementDirective.Shelter, "Key 3 should select Shelter");
                Assert(hud.SettlementText.Contains("Directive: Shelter", StringComparison.Ordinal), "HUD should expose Shelter");

                Pass(nameof(Test_MainScene_DirectiveInputSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_DirectiveInputSmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_CrisisHudPresentationSmoke()
        {
            Node? scene = null;

            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");
                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                manager.SetProcess(false);
                manager.SetScenario("empty_stores");
                PlayerCharacter player = manager.GetNodeOrNull<PlayerCharacter>("World/Players/LocalPlayer") ??
                    throw new Exception("LocalPlayer missing");
                PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI") ?? throw new Exception("PrototypeHud missing");
                Assert(hud.CrisisText.Contains("Crisis: Empty Stores", StringComparison.Ordinal), "Crisis HUD should present the active catalog crisis");
                Assert(hud.CrisisText.Contains("Directive: Neutral", StringComparison.Ordinal), "Crisis HUD should present the active directive");

                manager.Inventory.AddItem("logs", 3);
                player.GlobalPosition = manager.CentralDepotPosition;
                player.ProcessInteractionInput(301);
                Assert(hud.CrisisText.Contains("Contributed: 3 (logs x3)", StringComparison.Ordinal), "Crisis HUD should show contribution on the next presentation update");

                manager.ResetPrototypeRun();
                Assert(hud.CrisisText.Contains("Contributed: 0 (none)", StringComparison.Ordinal), "Reset should clear crisis presentation state without replay contamination");

                PrototypeCrisisState terminal = new(new PrototypeCrisisDefinition
                {
                    Id = "hud_terminal",
                    DisplayName = "HUD Terminal",
                    TicksPerSecond = 20,
                    DeadlineTicks = 4,
                    RequiredCapableCitizens = 1,
                    RequiredMeals = 0,
                    RequiredHearthFuel = 0,
                    RequiredBedCoveragePercent = 0,
                    StableHoldTicks = 2,
                    CollapseIncapacitatedCitizens = 9,
                    CollapseHoldTicks = 2,
                    CitizenNeedRateMultiplier = 1.0f
                });
                terminal.Advance(new PrototypeCrisisObservation(1, 1, 0, 0, 0));
                terminal.Advance(new PrototypeCrisisObservation(1, 1, 0, 0, 0));
                PrototypeHudPresenter.Apply(
                    hud, 60, 0, "08:00", "Clear", "Local", 2, new InventoryComponent(),
                    new Dictionary<string, int>(), Array.Empty<PrototypeWorkerState>(), Array.Empty<PrototypeStructureState>(),
                    PrototypeSettlementClassification.Stable, string.Empty, 0, 0, 0, 0.0f, 0.0f, 0.0f,
                    new Dictionary<string, int>(), string.Empty, directive: PrototypeSettlementDirective.Shelter,
                    crisis: terminal, contributionCountsByResource: new Dictionary<string, long> { ["logs"] = 3 });
                Assert(hud.CrisisText.Contains("Outcome: Stable: all conditions held 2/2 ticks", StringComparison.Ordinal), "Live HUD presenter should show terminal causal summary");

                manager.SetScenario("balanced_basin");
                Assert(hud.CrisisText == "Crisis: none", "Crisis-absent scenarios should retain their non-crisis HUD behavior");
                Pass(nameof(Test_MainScene_CrisisHudPresentationSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_CrisisHudPresentationSmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private void Test_VisualCaptureConfigurationAndHudLayout()
        {
            try
            {
                string[] expected = { "arrival", "settlement_overview", "contribution_point", "citizen_inspection", "terminal_crisis" };
                Assert(expected.OrderBy(id => id).SequenceEqual(PrototypeVisualCaptureConfiguration.PresetIds.OrderBy(id => id)), "Capture configuration should define exactly five named presets");
                Assert(PrototypeVisualCaptureConfiguration.ScenarioId == "empty_stores", "Capture scenario should be empty_stores");
                Assert(PrototypeVisualCaptureConfiguration.SimulationSeed == 1701, "Capture seed should be fixed");
                Assert(PrototypeVisualCaptureConfiguration.TerminalCrisisTick == 9777, "Capture terminal-crisis tick should match the observed canonical terminal state");
                Assert(PrototypeVisualCaptureConfiguration.TerminalCrisisEventCount == 8148, "Capture terminal-crisis provenance should retain the 10.5 reference event count");
                Assert(PrototypeVisualCaptureConfiguration.TerminalCrisisTraceSha256 == "69f3e22402e31a53b1d4c16899883956fcc5fdb14fbe47d8a4eb8baef007174f", "Capture terminal-crisis provenance should retain the 10.5 reference trace hash");
                Assert(PrototypeVisualCaptureConfiguration.LightingHour == 10.5f, "Capture lighting hour should be fixed");
                Assert(PrototypeVisualCaptureConfiguration.TryGetPreset("citizen_inspection", out PrototypeVisualCapturePreset citizenInspection), "Citizen inspection capture preset should exist");
                Assert(
                    citizenInspection.CameraKind == PrototypeVisualCaptureCameraKind.Observer &&
                    citizenInspection.CameraOffset == new Vector3(17, 12, 18) &&
                    citizenInspection.LookAtOffset == new Vector3(0, 1.2f, 0) &&
                    citizenInspection.FieldOfView == 62.0f,
                    "Citizen inspection should retain its fixed high observer composition above placeholder terrain");
                foreach ((float width, float height) in new[] { (1920.0f, 1080.0f), (1280.0f, 720.0f) })
                {
                    PrototypeHudLayout layout = PrototypeHudLayout.Calculate(width, height);
                    Assert(!layout.HasOverlaps(), $"HUD cards should not overlap at {width}x{height}");
                    foreach (KeyValuePair<string, PrototypeHudBounds> card in layout.Bounds)
                    {
                        Assert(card.Value.FitsWithin(width, height), $"HUD card {card.Key} should fit at {width}x{height}");
                    }
                }

                Node3D plannedPathCue = PrototypeSettlementScenePresenter.CreatePathStateCue(
                    new PrototypePathSegmentState { StructureId = "path_segment_planned", IsBuilt = false },
                    0);
                Node3D builtPathCue = PrototypeSettlementScenePresenter.CreatePathStateCue(
                    new PrototypePathSegmentState { StructureId = "path_segment_built", IsBuilt = true },
                    1);
                Assert(plannedPathCue.Name == "PathSegment-000" && builtPathCue.Name == "PathSegment-001", "Path-state cues should use stable state-neutral names");
                Assert(plannedPathCue.GetMeta("path_state").AsString() == "planned" && builtPathCue.GetMeta("path_state").AsString() == "built", "Path-state cue metadata should reflect authoritative construction state");
                Assert(plannedPathCue.GetNode<Label3D>("StateLabel").Text == "PLANNED PATH", "Unbuilt path convention should be labeled PLANNED PATH");
                Assert(builtPathCue.GetNode<Label3D>("StateLabel").Text == "BUILT PATH", "Built path convention should be labeled BUILT PATH");
                MeshInstance3D plannedSurface = plannedPathCue.GetNode<MeshInstance3D>("Surface");
                MeshInstance3D builtSurface = builtPathCue.GetNode<MeshInstance3D>("Surface");
                StandardMaterial3D plannedMaterial = plannedSurface.MaterialOverride as StandardMaterial3D
                    ?? throw new Exception("Planned path material missing");
                StandardMaterial3D builtMaterial = builtSurface.MaterialOverride as StandardMaterial3D
                    ?? throw new Exception("Built path material missing");
                Assert(plannedMaterial.AlbedoColor != builtMaterial.AlbedoColor, "Planned and built paths should use distinct materials");
                Assert(plannedSurface.Transparency > builtSurface.Transparency, "Planned path surface should read as incomplete beside the opaque built convention");
                plannedPathCue.Free();
                builtPathCue.Free();

                Pass(nameof(Test_VisualCaptureConfigurationAndHudLayout));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_VisualCaptureConfigurationAndHudLayout), ex);
            }
        }

        private async Task Test_MainScene_VisualCaptureContractSmoke()
        {
            Node? scene = null;
            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");
                GameManager manager = packedScene!.Instantiate<GameManager>();
                manager.ConfigureVisualCaptureStartup();
                scene = manager;
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                Assert(manager.ApplyVisualCaptureScenario(), "Visual capture scenario should apply after ready");
                Assert(manager.CurrentScenarioId == "empty_stores" && manager.SimulationSeed == 1701, "Visual capture should use the canonical scenario and seed");
                Assert(manager.VisualCaptureMetadata.SimulationTick == 0, "Visual capture should start at tick zero");
                PrototypeSettlementHub settlementHub = manager.GetNodeOrNull<PrototypeSettlementHub>("World/Environment/SettlementHub")
                    ?? throw new Exception("Visual capture requires the settlement hub");
                settlementHub.SetVisualCaptureAnimationPhase(PrototypeVisualCaptureConfiguration.SettlementAnimationPhase);
                Assert(!settlementHub.IsProcessing() && Math.Abs(settlementHub.AnimationPhase - PrototypeVisualCaptureConfiguration.SettlementAnimationPhase) <= 0.000001,
                    "Visual capture should lock the settlement hub to its fixed animation phase before frame waits");
                PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI") ?? throw new Exception("PrototypeHud missing");
                Assert(hud.Layer == PrototypeHud.PresentationCanvasLayer, "Normal-play HUD should render on its dedicated presentation canvas layer");
                EnvironmentController environment = manager.GetNodeOrNull<EnvironmentController>("World/Environment/Environment")
                    ?? throw new Exception("Visual capture requires EnvironmentController");
                Assert(!hud.IsDebugVisible && manager.CurrentOverlayMode.ToString() == "None", "Visual capture should hide debug UI and clear terrain overlays");
                Assert(environment.IsPresentationLightingLocked &&
                    environment.PresentationLightingHour == PrototypeVisualCaptureConfiguration.LightingHour &&
                    environment.PresentationLightingMultiplier == PrototypeVisualCaptureConfiguration.LightingMultiplier,
                    "Visual capture should expose its actual locked environment lighting state");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                manager._Process(1.0);
                manager._Process(1.0);
                Assert(manager.VisualCaptureMetadata.SimulationTick == 0, "Rendered frames must not advance canonical visual capture simulation");
                Assert(!settlementHub.IsProcessing() && Math.Abs(settlementHub.AnimationPhase - PrototypeVisualCaptureConfiguration.SettlementAnimationPhase) <= 0.000001,
                    "Visual capture frame waits must retain the fixed settlement animation phase");
                Assert(manager.GetNodeOrNull("World/Environment/SettlementHub/ContributionPoint") != null, "Contribution point should have a stable named node");
                Node3D worldCues = manager.GetNodeOrNull<Node3D>("World/Environment/SettlementWorldCues") ?? throw new Exception("Settlement cues should have a stable named root");
                Node3D plannedPathCue = worldCues.GetNodeOrNull<Node3D>("PathSegment-000") ?? throw new Exception("Planned-path cue should have a stable named node");
                Assert(plannedPathCue.GetMeta("path_state").AsString() == "planned", "Canonical tick-zero path cue should report authoritative planned state");
                Assert(plannedPathCue.GetNode<Label3D>("StateLabel").Text == "PLANNED PATH", "Canonical tick-zero path cue should be labeled PLANNED PATH");
                Assert(worldCues.GetChildren().OfType<Node3D>().All(cue => cue.GetMeta("path_state").AsString() == "planned"), "Canonical tick-zero overview must not fabricate a built path");
                Label3D queuedHutLabel = manager.GetNodeOrNull<Label3D>("World/Environment/SettlementHub/StructureMarkers/hut_3/Label") ?? throw new Exception("Queued hut marker missing");
                Assert(queuedHutLabel.Text == "Hut\nplanned", "Queued hut construction should remain distinct from path-corridor state");

                Assert(hud.CrisisText.Contains("Crisis: Empty Stores", StringComparison.Ordinal) && hud.CrisisText.Contains("Time:", StringComparison.Ordinal) && hud.CrisisText.Contains("Directive: Neutral", StringComparison.Ordinal) && hud.CrisisText.Contains("Contributed:", StringComparison.Ordinal) && hud.CrisisText.Contains("Stable conditions:", StringComparison.Ordinal), "Normal view should expose required crisis state text");
                Assert(manager.SelectDirective(PrototypeSettlementDirective.FoodAndFuel).Changed, "Visual capture smoke should select Food & Fuel");
                Assert(hud.PresentationState.DirectiveCue == PrototypeHudCue.FoodAndFuel, "HUD should expose the Food & Fuel state cue");
                Assert(manager.SelectNextInspectedCitizen(), "Visual capture smoke should select a citizen");
                Assert(hud.InspectorText.Contains("Citizen:", StringComparison.Ordinal), "Normal view should expose an inspected citizen");
                foreach (string presetId in manager.VisualCapturePresetIds)
                {
                    Assert(manager.SelectVisualCapturePreset(presetId), $"Preset {presetId} should apply");
                    Assert(manager.VisualCaptureMetadata.SelectedPresetId == presetId, $"Metadata should record preset {presetId}");
                }
                Assert(manager.PositionVisualCapturePlayerAtDepot(), "Visual capture should place the player body in deterministic depot range");
                Assert(manager.SelectVisualCapturePreset("contribution_point"), "Contribution capture preset should apply after player positioning");
                Assert(manager.SubmitVisualCaptureContribution(), "Contribution capture should use the authoritative player input path and report success");
                Assert(hud.StatusText.Contains("Contributed", StringComparison.Ordinal), "Contribution capture should expose a successful contribution cue");

                Assert(manager.ApplyVisualCaptureScenario(), "Visual capture should reset the canonical no-input scenario after contribution capture");
                Node3D resetWorldCues = manager.GetNodeOrNull<Node3D>("World/Environment/SettlementWorldCues")
                    ?? throw new Exception("Visual capture reset should recreate the stable settlement-cue root synchronously");
                Assert(resetWorldCues.Name == "SettlementWorldCues", "Visual capture reset should retain the exact stable settlement-cue root name");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Node3D settledResetWorldCues = manager.GetNodeOrNull<Node3D>("World/Environment/SettlementWorldCues")
                    ?? throw new Exception("Visual capture reset should retain the stable settlement-cue root after queued frees settle");
                Assert(settledResetWorldCues.Name == "SettlementWorldCues", "Settled visual capture reset should retain the exact stable settlement-cue root name");
                Assert(
                    manager.AdvanceVisualCaptureToTick(PrototypeVisualCaptureConfiguration.CitizenInspectionTick) &&
                    manager.VisualCaptureMetadata.SimulationTick == PrototypeVisualCaptureConfiguration.CitizenInspectionTick,
                    "Visual capture should advance citizen inspection through authoritative ticks");
                Assert(manager.SelectVisualCaptureInspectionCitizen(), "Visual capture should select a stable citizen for matching camera focus and inspector state");
                Assert(hud.InspectorText.Contains("Why:", StringComparison.Ordinal) && !hud.InspectorText.Contains("Why: none", StringComparison.Ordinal), "Citizen inspection should show a non-empty causal Why explanation");
                Assert(manager.SelectVisualCapturePreset("citizen_inspection"), "Citizen inspection capture preset should focus the selected stable citizen");
                Assert(manager.AdvanceVisualCaptureToTick(PrototypeVisualCaptureConfiguration.CitizenInspectionTick), "Visual capture should retain an already reached explicit tick");
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Assert(manager.VisualCaptureMetadata.SimulationTick == PrototypeVisualCaptureConfiguration.CitizenInspectionTick, "Rendered frames must not advance visual capture after an explicit tick advance");
                Pass(nameof(Test_MainScene_VisualCaptureContractSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_VisualCaptureContractSmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_FrameCatchUpCapSmoke()
        {
            Node? scene = null;

            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                manager.ResetPrototypeRun();
                long initialTick = manager.SimulationTick;

                manager._Process(1.0);
                Assert(manager.SimulationTick == initialTick + 12, "A rendered frame must process no more than 12 catch-up ticks");

                manager._Process(0.0);
                Assert(manager.SimulationTick == initialTick + 20, "Deferred catch-up ticks must remain queued for the next frame");

                Pass(nameof(Test_MainScene_FrameCatchUpCapSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_FrameCatchUpCapSmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_HudRefreshCoalescingSmoke()
        {
            Node? scene = null;

            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI") ?? throw new Exception("PrototypeHud missing");
                manager.SetProcess(false);

                string inventoryBeforeMutation = hud.InventoryText;
                manager.Inventory.AddItem("hud_refresh_probe", 1);
                Assert(hud.InventoryText == inventoryBeforeMutation, "Inventory mutation should not rebuild the HUD synchronously");

                manager._Process(0.0);
                Assert(hud.InventoryText.Contains("hud refresh probe: 1"), "The next rendered-frame update should present the inventory mutation");

                Pass(nameof(Test_MainScene_HudRefreshCoalescingSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_HudRefreshCoalescingSmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_RuntimeMetricsBatchSmoke()
        {
            const string metricsEnvironmentVariable = "SOCIETIES_PERF_METRICS";
            const string outputEnvironmentVariable = "SOCIETIES_RUN_OUTPUT_DIR";
            string? previousMetricsSetting = System.Environment.GetEnvironmentVariable(metricsEnvironmentVariable);
            string? previousOutputDirectory = System.Environment.GetEnvironmentVariable(outputEnvironmentVariable);
            string outputDirectory = CreateRunOutputDirectory(nameof(Test_MainScene_RuntimeMetricsBatchSmoke));
            string runtimeMetricsPath = Path.Combine(outputDirectory, "runtime-batch-metrics-v4.csv");
            Node? disabledScene = null;
            Node? scene = null;

            try
            {
                System.Environment.SetEnvironmentVariable(outputEnvironmentVariable, outputDirectory);
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                System.Environment.SetEnvironmentVariable(metricsEnvironmentVariable, null);
                disabledScene = packedScene!.Instantiate();
                GameManager disabledManager = disabledScene as GameManager ?? throw new Exception("Disabled metrics scene root is not GameManager");
                disabledManager.ConfigurePerformanceStartup(
                    "balanced_basin",
                    simulationSeed: 4242,
                    citizenCount: 3,
                    selectorMode: "exhaustive_reference",
                    extractionPlanningMode: "exhaustive_reference");
                disabledManager.SetProcess(false);
                AddChild(disabledScene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Assert(disabledManager.RuntimeMetrics == null, "Runtime metrics should remain unallocated when the environment flag is absent");
                Assert(disabledManager.CurrentScenarioId == "balanced_basin", "Performance startup should preserve the requested scenario");
                Assert(disabledManager.SimulationSeed == 4242, "Performance startup should apply the requested simulation seed");
                Assert(disabledManager.CitizenCount == 3, "Performance startup should apply the requested citizen count");
                Assert(
                    disabledManager.CurrentOrderSelectionMode == PrototypeOrderSelectionMode.ExhaustiveReference,
                    "Performance startup should apply the requested selector mode");
                Assert(
                    disabledManager.CurrentExtractionPlanningMode == PrototypeExtractionPlanningMode.ExhaustiveReference,
                    "Performance startup should apply the requested extraction planning mode");
                Assert(disabledManager.PerformanceBootstrapMilliseconds is > 0.0, "Performance startup should capture the internal bootstrap interval");
                bool reconfigurationRejected = false;
                try
                {
                    disabledManager.ConfigurePerformanceStartup("balanced_basin", simulationSeed: 1337, citizenCount: 16);
                }
                catch (InvalidOperationException)
                {
                    reconfigurationRejected = true;
                }
                Assert(reconfigurationRejected, "Performance startup should reject configuration after the first tree entry");
                File.WriteAllText(runtimeMetricsPath, "stale runtime metrics");
                disabledManager.SaveSnapshotToDisk();
                Assert(!File.Exists(runtimeMetricsPath), "A metrics-disabled save should remove a stale runtime metrics artifact");
                disabledScene.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                disabledScene = null;

                System.Environment.SetEnvironmentVariable(metricsEnvironmentVariable, "1");
                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                manager.SetProcess(false);
                RuntimeMetricsCollector metrics = manager.RuntimeMetrics ?? throw new Exception("Runtime metrics should be enabled by the environment flag");
                manager.ResetPrototypeRun();
                Assert(metrics.Count == 0, "Reset should clear runtime metrics batches");

                Directory.CreateDirectory(runtimeMetricsPath);
                manager.SaveSnapshotToDisk();
                Assert(
                    Directory.Exists(runtimeMetricsPath),
                    "An optional metrics export failure should not fail the core save");
                Directory.Delete(runtimeMetricsPath);

                manager.StepSimulationTicks(2);
                RuntimeMetricsBatch[] afterManualStep = metrics.SnapshotBatches();
                Assert(afterManualStep.Length == 1, "Manual stepping should create one metrics batch");
                Assert(afterManualStep[0].Kind == RuntimeMetricsBatchKind.ManualStep, "Manual stepping must not be reported as a rendered frame");
                Assert(afterManualStep[0].CompletedTicks == 2, "Manual metrics batch should contain both completed ticks");
                Assert(afterManualStep[0].StartSimulationTick == 0 && afterManualStep[0].EndSimulationTick == 2, "Manual metrics tick bounds mismatch");
                Assert(afterManualStep[0].Phases.SimulationTickMilliseconds > 0.0, "Manual batch should measure simulation tick work");
                Assert(afterManualStep[0].Phases.SessionAdvanceMilliseconds > 0.0, "Manual batch should measure session advancement");
                Assert(afterManualStep[0].Phases.BuildWorkOrdersMilliseconds > 0.0, "Manual batch should measure work-order generation at its call site");
                Assert(afterManualStep[0].Phases.SceneSyncMilliseconds > 0.0, "Manual batch should measure scene synchronization");
                Assert(afterManualStep[0].Phases.UpdateHudMilliseconds > 0.0, "Manual batch should measure its coalesced HUD refresh");
                Assert(afterManualStep[0].WorkOrdersGeneratedUncappedTotal >= afterManualStep[0].WorkOrdersGeneratedTotal, "Uncapped work-order diagnostics must be preserved");
                Assert(afterManualStep[0].WorkOrdersRemainingLast.HasValue, "Completed ticks should publish the last work-order gauge");
                Assert(
                    afterManualStep[0].PathPlanCacheHitsTotal + afterManualStep[0].PathPlanCacheMissesTotal == afterManualStep[0].PathPlanLookupsTotal,
                    "Path cache hits and misses should account for every lookup");
                Assert(afterManualStep[0].PathPlanCacheSizeLast.HasValue, "Completed ticks should publish the last path-cache size");
                Assert(afterManualStep[0].WorkerCountLast is > 0, "Completed ticks should publish a positive worker count");
                Assert(afterManualStep[0].IdleCitizensConsideringWorkOrdersTotal > 0, "Assignment diagnostics should report idle citizens considering work orders");
                Assert(afterManualStep[0].CandidateOrdersEvaluatedTotal > 0, "Assignment diagnostics should report evaluated candidate orders");
                Assert(afterManualStep[0].CandidateOrdersPerIdleCitizen is > 0.0, "Completed ticks should publish a positive candidate-orders-per-idle-citizen ratio");
                Assert(afterManualStep[0].Phases.RouteSelectionMilliseconds > 0.0, "Manual batch should measure generic route selection work");
                Assert(afterManualStep[0].SelectorCandidatesBoundedTotal > 0, "Selector diagnostics should report bounded candidates");
                Assert(afterManualStep[0].SelectorCandidatesExactScoredTotal > 0, "Selector diagnostics should report exact-scored candidates");
                Assert(afterManualStep[0].SelectorCandidatesPrunedTotal > 0, "The optimized selector should prune candidates in the runtime smoke");
                Assert(
                    afterManualStep[0].SelectorPathCacheHitsTotal + afterManualStep[0].SelectorPathCacheMissesTotal ==
                    afterManualStep[0].SelectorExactPathQueriesTotal,
                    "Selector cache hits and misses should account for every exact-path query");
                Assert(afterManualStep[0].SelectorSelectedRouteReusesTotal > 0, "The optimized selector should reuse selected routes");
                Assert(afterManualStep[0].CitizensEvaluatedTotal > 0, "Session diagnostics should report evaluated citizens");

                manager._Process(0.1);
                RuntimeMetricsBatch[] afterRenderedFrame = metrics.SnapshotBatches();
                Assert(afterRenderedFrame.Length == 2, "Rendered processing should append a metrics batch");
                Assert(afterRenderedFrame[1].Kind == RuntimeMetricsBatchKind.RenderedFrame, "Rendered work must use the rendered-frame batch kind");
                Assert(afterRenderedFrame[1].CompletedTicks == 2, "Rendered metrics batch should contain the two due ticks");
                Assert(afterRenderedFrame[1].StartSimulationTick == 2 && afterRenderedFrame[1].EndSimulationTick == 4, "Rendered metrics tick bounds mismatch");

                manager._Process(0.0);
                RuntimeMetricsBatch[] afterZeroTickFrame = metrics.SnapshotBatches();
                Assert(afterZeroTickFrame.Length == 3, "A zero-tick rendered frame should still append bounded frame-work telemetry");
                Assert(afterZeroTickFrame[2].Kind == RuntimeMetricsBatchKind.RenderedFrame, "Zero-tick work should remain a rendered-frame batch");
                Assert(afterZeroTickFrame[2].CompletedTicks == 0, "Zero-tick frame should not fabricate a completed simulation tick");
                Assert(!afterZeroTickFrame[2].WorkOrdersRemainingLast.HasValue, "Zero-tick frame should not fabricate a work-order gauge");
                Assert(!afterZeroTickFrame[2].PathPlanCacheSizeLast.HasValue, "Zero-tick frame should not fabricate a path-cache size");
                Assert(!afterZeroTickFrame[2].WorkerCountLast.HasValue, "Zero-tick frame should not fabricate a worker count");
                Assert(!afterZeroTickFrame[2].CandidateOrdersPerIdleCitizen.HasValue, "Zero-tick frame should not fabricate an assignment ratio");
                Assert(afterZeroTickFrame[2].StartSimulationTick == 4 && afterZeroTickFrame[2].EndSimulationTick == 4, "Zero-tick frame bounds should remain unchanged");

                manager.SaveSnapshotToDisk();
                Assert(File.Exists(runtimeMetricsPath), "A metrics-enabled save should export runtime batch metrics");
                string runtimeMetricsCsv = File.ReadAllText(runtimeMetricsPath);
                Assert(runtimeMetricsCsv.StartsWith("sequence,batch_kind,start_simulation_tick", StringComparison.Ordinal), "Runtime metrics CSV header mismatch");
                string[] runtimeMetricsHeader = runtimeMetricsCsv.Split('\n', 2, StringSplitOptions.None)[0].TrimEnd('\r').Split(',');
                string[] requiredDiagnosticHeaders =
                {
                    "navigation_rebuild_ms",
                    "path_plan_cache_misses_total",
                    "path_plan_cache_size_last",
                    "navigation_invalidations_total",
                    "worker_count_last",
                    "idle_citizens_considering_work_orders_total",
                    "candidate_orders_evaluated_total",
                    "candidate_orders_per_idle_citizen",
                    "route_selection_ms",
                    "selector_candidates_bounded_total",
                    "selector_candidates_exact_scored_total",
                    "selector_candidates_pruned_total",
                    "selector_exact_path_queries_total",
                    "selector_path_cache_hits_total",
                    "selector_path_cache_misses_total",
                    "selector_selected_route_reuses_total"
                };
                string[] missingDiagnosticHeaders = requiredDiagnosticHeaders
                    .Where(header => !runtimeMetricsHeader.Contains(header, StringComparer.Ordinal))
                    .ToArray();
                Assert(
                    missingDiagnosticHeaders.Length == 0,
                    $"Runtime metrics CSV is missing navigation/assignment headers: {string.Join(", ", missingDiagnosticHeaders)}");
                Assert(runtimeMetricsCsv.Contains("manual_step", StringComparison.Ordinal), "Runtime metrics CSV should contain the manual batch");
                Assert(runtimeMetricsCsv.Contains("rendered_frame", StringComparison.Ordinal), "Runtime metrics CSV should contain rendered batches");

                manager.ResetPrototypeRun();
                Assert(metrics.Count == 0, "Starting a new run should reset runtime metrics");

                Pass(nameof(Test_MainScene_RuntimeMetricsBatchSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_RuntimeMetricsBatchSmoke), ex);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(metricsEnvironmentVariable, previousMetricsSetting);
                System.Environment.SetEnvironmentVariable(outputEnvironmentVariable, previousOutputDirectory);
                if (disabledScene != null)
                {
                    disabledScene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_CraftingAndSnapshotSmoke()
        {
            Node? scene = null;
            string outputDirectory = CreateRunOutputDirectory(nameof(Test_MainScene_CraftingAndSnapshotSmoke));

            try
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", outputDirectory);
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                manager.Inventory.AddItem("logs", 3);
                manager.Inventory.AddItem("stone", 4);

                bool crafted = manager.TryCraftRecipe("stone_axe");
                Assert(crafted, "Stone axe recipe should craft after adding required resources");
                Assert(manager.Inventory.GetCount("stone_axe") == 1, "Stone axe should be present in inventory");

                string snapshotPath = manager.SaveSnapshotToDisk();
                Assert(File.Exists(snapshotPath), "Snapshot file should exist after saving");
                Assert(File.Exists(Path.Combine(outputDirectory, "latest-event-log.json")), "Event log should exist after saving");
                Assert(File.Exists(Path.Combine(outputDirectory, "latest-run-summary.json")), "Run summary should exist after saving");
                Assert(File.Exists(Path.Combine(outputDirectory, "snapshot-v2.json")), "V2 snapshot should exist after saving");
                Assert(File.Exists(Path.Combine(outputDirectory, "event-log-v2.json")), "V2 event log should exist after saving");
                Assert(File.Exists(Path.Combine(outputDirectory, "run-summary-v2.json")), "V2 run summary should exist after saving");
                Assert(File.Exists(Path.Combine(outputDirectory, "metrics-timeseries-v2.csv")), "V2 metrics csv should exist after saving");
                Assert(File.Exists(Path.Combine(outputDirectory, "world-summary-v2.json")), "V2 world summary should exist after saving");
                PrototypeWorldSummary worldSummary = PrototypePersistenceService.LoadWorldSummary(Path.Combine(outputDirectory, "world-summary-v2.json"));
                Assert(worldSummary.TerrainMode == "heightfield_v1", "World summary should report the heightfield terrain mode");
                Assert(worldSummary.WorldSeed != 0, "World summary should contain a world seed");

                manager.Inventory.ReplaceContents(new Dictionary<string, int>());
                Assert(manager.Inventory.GetCount("stone_axe") == 0, "Inventory should be clear before load");

                bool loaded = manager.LoadLatestSnapshotFromDisk();
                Assert(loaded, "Snapshot load should succeed");
                Assert(manager.Inventory.GetCount("stone_axe") == 1, "Snapshot load should restore crafted item");

                Pass(nameof(Test_MainScene_CraftingAndSnapshotSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_CraftingAndSnapshotSmoke), ex);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", null);

                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_WorkerVisualizationSmoke()
        {
            Node? scene = null;

            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI") ?? throw new Exception("PrototypeHud missing");
                Node3D agentsRoot = manager.GetNodeOrNull<Node3D>("World/Agents") ?? throw new Exception("Agents root missing");
                PrototypeSettlementHub hub = manager.GetNodeOrNull<PrototypeSettlementHub>("World/Environment/SettlementHub") ?? throw new Exception("SettlementHub missing");

                PrototypeRuntimeSnapshot initialSnapshot = manager.CaptureSnapshot();
                manager.StepSimulationTicks(24);
                PrototypeRuntimeSnapshot movedSnapshot = manager.CaptureSnapshot();

                Assert(movedSnapshot.Workers.Any(worker => worker.Position.ToVector3().DistanceTo(worker.HomePosition.ToVector3()) > 0.5f), "At least one worker should physically move away from home");
                Assert(hud.SettlementText.Contains("->"), "Settlement HUD should show worker targets");
                Assert(hud.SettlementText.Contains("Citizens:"), "Settlement HUD should expose citizen state");
                Assert(!string.IsNullOrWhiteSpace(hub.StatusText), "Settlement hub label should not be empty");

                PrototypeWorkerAgent? workerNode = agentsRoot.GetChildren().OfType<PrototypeWorkerAgent>().FirstOrDefault();
                Assert(workerNode != null, "Worker visual should exist");
                Assert(!string.IsNullOrWhiteSpace(workerNode!.LabelText), "Worker label should describe current work");
                Assert(initialSnapshot.Workers.Count == movedSnapshot.Workers.Count, "Worker count should remain stable while moving");

                Pass(nameof(Test_MainScene_WorkerVisualizationSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_WorkerVisualizationSmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_ResetAndRestoreSmoke()
        {
            Node? scene = null;
            string outputDirectory = CreateRunOutputDirectory(nameof(Test_MainScene_ResetAndRestoreSmoke));

            try
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", outputDirectory);
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                Node3D agentsRoot = manager.GetNodeOrNull<Node3D>("World/Agents") ?? throw new Exception("Agents root missing");
                PrototypeScenarioDefinition scenario = LoadCatalogBundle().Scenarios.Resolve("balanced_basin");

                manager.StepSimulationTicks(320);
                manager.SaveSnapshotToDisk();
                PrototypeRuntimeSnapshot savedSnapshot = manager.CaptureSnapshot();

                manager.ResetPrototypeRun();
                PrototypeRuntimeSnapshot resetSnapshot = manager.CaptureSnapshot();

                Assert(resetSnapshot.SimulationTick == 0, "Reset should zero simulation ticks");
                Assert(resetSnapshot.Inventory.Count == 0, "Reset should clear player inventory");
                Assert(resetSnapshot.Stockpile.Values.Sum() >= scenario.StartingStock.Values.Sum(), "Reset should restore starting settlement reserves");
                Assert(resetSnapshot.Workers.Count == scenario.InitialCitizens, "Reset should rebuild citizens");
                Assert(resetSnapshot.Resources.Count == savedSnapshot.Resources.Count, "Reset should respawn the initial resource set");
                Assert(agentsRoot.GetChildCount() == scenario.InitialCitizens, "Reset should rebuild citizen visuals");

                bool loaded = manager.LoadLatestSnapshotFromDisk();
                Assert(loaded, "Snapshot load should succeed after reset");

                PrototypeRuntimeSnapshot restoredSnapshot = manager.CaptureSnapshot();
                Assert(restoredSnapshot.SimulationTick == savedSnapshot.SimulationTick, "Load should restore tick count");
                Assert(
                    restoredSnapshot.Stockpile.OrderBy(pair => pair.Key).SequenceEqual(savedSnapshot.Stockpile.OrderBy(pair => pair.Key)),
                    "Load should restore stockpile");
                Assert(restoredSnapshot.Workers.Count == savedSnapshot.Workers.Count, "Load should restore worker count");
                Assert(agentsRoot.GetChildCount() == restoredSnapshot.Workers.Count, "Worker visuals should match restored workers");

                Pass(nameof(Test_MainScene_ResetAndRestoreSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_ResetAndRestoreSmoke), ex);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", null);

                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_ScenarioSwitchWorldSummarySmoke()
        {
            Node? scene = null;
            string outputDirectory = CreateRunOutputDirectory(nameof(Test_MainScene_ScenarioSwitchWorldSummarySmoke));

            try
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", outputDirectory);
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");

                manager.SaveSnapshotToDisk();
                PrototypeWorldSummary basinSummary = PrototypePersistenceService.LoadWorldSummary(Path.Combine(outputDirectory, "world-summary-v2.json"));

                manager.SetScenario("food_poor_highlands");
                manager.SaveSnapshotToDisk();
                PrototypeWorldSummary highlandsSummary = PrototypePersistenceService.LoadWorldSummary(Path.Combine(outputDirectory, "world-summary-v2.json"));

                Assert(basinSummary.WorldHash != highlandsSummary.WorldHash, "Different scenarios should produce different world hashes");
                Assert(basinSummary.BuildableCellRatio != highlandsSummary.BuildableCellRatio, "Different scenarios should produce different buildable ratios");
                Assert(manager.CurrentScenarioId == "food_poor_highlands", "Scenario switch should update the active scenario");

                Pass(nameof(Test_MainScene_ScenarioSwitchWorldSummarySmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_ScenarioSwitchWorldSummarySmoke), ex);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", null);

                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_ObserverAndOverlaySmoke()
        {
            Node? scene = null;

            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                PrototypeRuntimeSnapshot initialSnapshot = manager.CaptureSnapshot();
                InputEventKey observerToggle = new()
                {
                    Pressed = true,
                    Keycode = Key.F8
                };
                InputEventKey overlayToggle = new()
                {
                    Pressed = true,
                    Keycode = Key.F10
                };

                manager._UnhandledInput(observerToggle);
                Assert(manager.CurrentCameraMode == CameraMode.Observer, "F8 should switch to observer mode");
                Assert(manager.SimulationTick == initialSnapshot.SimulationTick, "Observer toggle should not advance simulation ticks");

                manager._UnhandledInput(overlayToggle);
                Assert(manager.CurrentOverlayMode == TerrainOverlayMode.Biome, "F10 should cycle to the biome overlay first");
                Assert(manager.CaptureSnapshot().WorldHash == initialSnapshot.WorldHash, "Overlay changes should not mutate the world state");

                manager._UnhandledInput(observerToggle);
                Assert(manager.CurrentCameraMode == CameraMode.Player, "F8 should switch back to player mode");

                Pass(nameof(Test_MainScene_ObserverAndOverlaySmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_ObserverAndOverlaySmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_BuildQueueAndInspectorSmoke()
        {
            Node? scene = null;

            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI") ?? throw new Exception("PrototypeHud missing");

                bool buildQueueAdvanced = manager.SelectNextBuildQueueEntry();
                bool buildQueuePaused = manager.ToggleSelectedBuildQueuePause();
                bool citizenSelected = manager.SelectNextInspectedCitizen();
                bool structureSelected = manager.SelectNextInspectedStructure();

                Assert(buildQueueAdvanced, "Build queue focus should advance");
                Assert(buildQueuePaused, "Build queue entry should pause or resume");
                Assert(citizenSelected, "Citizen inspection should cycle");
                Assert(structureSelected, "Structure inspection should cycle");
                Assert(hud.SettlementText.Contains("Build Queue Focus:"), "Settlement HUD should show build queue state");
                Assert(hud.InspectorText.Contains("Inspector"), "Inspector HUD should render");
                Assert(hud.InspectorText.Contains("Citizen:"), "Inspector HUD should show a citizen");
                Assert(hud.InspectorText.Contains("Structure:"), "Inspector HUD should show a structure");

                Pass(nameof(Test_MainScene_BuildQueueAndInspectorSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_BuildQueueAndInspectorSmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_SettlementLoopSmoke()
        {
            Node? scene = null;

            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI") ?? throw new Exception("PrototypeHud missing");
                Node3D agentsRoot = manager.GetNodeOrNull<Node3D>("World/Agents") ?? throw new Exception("Agents root missing");
                PrototypeSettlementHub hub = manager.GetNodeOrNull<PrototypeSettlementHub>("World/Environment/SettlementHub") ?? throw new Exception("SettlementHub missing");

                manager.StepSimulationTicks(2400);
                PrototypeRuntimeSnapshot snapshot = manager.CaptureSnapshot();

                Assert(snapshot.Settlement != null, "Settlement snapshot should exist");
                Assert(snapshot.Workers.Count == snapshot.Settlement!.Citizens.Count, "Settlement citizen count mismatch");
                Assert(agentsRoot.GetChildCount() == snapshot.Workers.Count, "Worker visuals should match simulated workers");
                Assert(snapshot.Stockpile.GetValueOrDefault("meals", 0) > 0 || snapshot.Settlement.ProducedResources.GetValueOrDefault("meals", 0) > 0, "Settlement should produce food during the smoke run");
                Assert(snapshot.Stockpile.GetValueOrDefault("hearth_fuel", 0) > 0 || snapshot.Settlement.HearthLitTicks > 0, "Settlement should maintain hearth fuel");
                Assert(snapshot.Settlement.Structures.Any(structure => structure.StructureKindId == "hut" && structure.IsBuilt), "Settlement should complete at least one hut");
                Assert(!string.IsNullOrWhiteSpace(hud.SettlementText), "Settlement HUD text should not be empty");
                Assert(hud.SettlementText.Contains("Settlement"), "Settlement HUD should include the section header");
                Assert(hud.SettlementText.Contains("Build Queue Focus:"), "Settlement HUD should expose the build queue");
                Assert(
                    hub.IsHearthLit ||
                    snapshot.Stockpile.GetValueOrDefault("hearth_fuel", 0) > 0 ||
                    snapshot.Settlement.HearthLitTicks > 0,
                    "Settlement hub should communicate a fueled hearth");

                Pass(nameof(Test_MainScene_SettlementLoopSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_SettlementLoopSmoke), ex);
            }
            finally
            {
                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private async Task Test_MainScene_FixedTickSoakSmoke()
        {
            Node? scene = null;
            string outputDirectory = CreateRunOutputDirectory(nameof(Test_MainScene_FixedTickSoakSmoke));

            try
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", outputDirectory);
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");

                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                Node3D agentsRoot = manager.GetNodeOrNull<Node3D>("World/Agents") ?? throw new Exception("Agents root missing");
                long initialTick = manager.SimulationTick;

                manager.StepSimulationTicks(1200);
                string snapshotPath = manager.SaveSnapshotToDisk();
                PrototypeRuntimeSnapshot snapshot = manager.CaptureSnapshot();
                PrototypeRunSummary summary = PrototypePersistenceService.LoadRunSummary(Path.Combine(outputDirectory, "latest-run-summary.json"));

                Assert(snapshot.SimulationTick == initialTick + 1200, "Soak should advance exactly 1200 ticks from the starting state");
                Assert(File.Exists(snapshotPath), "Soak snapshot should exist");
                Assert(File.Exists(Path.Combine(outputDirectory, "latest-event-log.json")), "Soak event log should exist");
                Assert(File.Exists(Path.Combine(outputDirectory, "latest-run-summary.json")), "Soak run summary should exist");
                Assert(File.Exists(Path.Combine(outputDirectory, "snapshot-v2.json")), "Soak V2 snapshot should exist");
                Assert(File.Exists(Path.Combine(outputDirectory, "event-log-v2.json")), "Soak V2 event log should exist");
                Assert(File.Exists(Path.Combine(outputDirectory, "run-summary-v2.json")), "Soak V2 run summary should exist");
                Assert(File.Exists(Path.Combine(outputDirectory, "metrics-timeseries-v2.csv")), "Soak V2 metrics csv should exist");
                Assert(File.Exists(Path.Combine(outputDirectory, "world-summary-v2.json")), "Soak V2 world summary should exist");
                Assert(agentsRoot.GetChildCount() == snapshot.Workers.Select(worker => worker.WorkerId).Distinct().Count(), "Worker visuals should remain unique");
                Assert(snapshot.Inventory.Values.All(count => count >= 0), "Player inventory counts should not go negative");
                Assert(snapshot.Stockpile.Values.All(count => count >= 0), "Stockpile counts should not go negative");
                Assert(snapshot.Resources.All(resource => resource.UnitsRemaining >= 0), "Resource units should not go negative");
                Assert(summary.EventCountsByType.Count > 0, "Run summary should include event counts");
                Assert(summary.ProducedResources.Count > 0 || snapshot.Settlement?.ProducedResources.Count > 0, "Soak should produce economy outputs");
                Assert(summary.BedCoveragePercent >= 0, "Run summary should capture bed coverage");
                Assert(summary.BuildQueueStatus.Contains("Build Queue"), "Run summary should capture build queue focus");

                Pass(nameof(Test_MainScene_FixedTickSoakSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_FixedTickSoakSmoke), ex);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", null);

                if (scene != null)
                {
                    scene.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        private void Pass(string testName)
        {
            _passed++;
            GD.Print($"PASS {testName}");
        }

        private void Fail(string testName, Exception ex)
        {
            _failed++;
            GD.PrintErr($"FAIL {testName}: {ex.Message}");
        }

        private void PrintHeader()
        {
            GD.Print("============================================================");
            GD.Print("Societies Headless Test Runner");
            GD.Print("Authoritative target: Godot prototype under src/societies");
            GD.Print("============================================================");
        }

        private void PrintSummary()
        {
            GD.Print("------------------------------------------------------------");
            GD.Print($"Headless results: {_passed} passed, {_failed} failed");
            GD.Print("------------------------------------------------------------");
        }

        private static string CreateRunOutputDirectory(string testName)
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "societies-headless",
                testName,
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(directory);
            return directory;
        }

        private void Test_RunOutputDirectory_IsolatedPerInvocation()
        {
            try
            {
                string firstDirectory = CreateRunOutputDirectory(nameof(Test_RunOutputDirectory_IsolatedPerInvocation));
                string sentinelPath = Path.Combine(firstDirectory, "sentinel.txt");
                File.WriteAllText(sentinelPath, "preserve existing test artifacts");

                string secondDirectory = CreateRunOutputDirectory(nameof(Test_RunOutputDirectory_IsolatedPerInvocation));

                Assert(firstDirectory != secondDirectory, "Each headless test invocation must receive an isolated output directory");
                Assert(File.Exists(sentinelPath), "Creating a run output directory must not delete artifacts from another invocation");
                Pass(nameof(Test_RunOutputDirectory_IsolatedPerInvocation));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_RunOutputDirectory_IsolatedPerInvocation), ex);
            }
        }

        private static PrototypeCatalogBundle LoadCatalogBundle()
        {
            return PrototypeCatalogLoader.LoadFromDirectory(ProjectSettings.GlobalizePath("res://data"));
        }
    }
}
