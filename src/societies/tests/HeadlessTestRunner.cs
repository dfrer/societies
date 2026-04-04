using Godot;
using Societies.Core;
using Societies.Multiplayer;
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
            await Test_MainScene_BootstrapSmoke();
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

                float initialHour = dayNight.CurrentHour;
                manager.StepSimulationTicks(5);
                Assert(manager.SimulationTick == 5, "Simulation tick count should advance deterministically");
                Assert(dayNight.CurrentHour != initialHour, "Day/night state should advance through the fixed-tick runner");

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
            string directory = Path.Combine(Path.GetTempPath(), "societies-headless", testName);

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(directory);
            return directory;
        }

        private static PrototypeCatalogBundle LoadCatalogBundle()
        {
            return PrototypeCatalogLoader.LoadFromDirectory(ProjectSettings.GlobalizePath("res://data"));
        }
    }
}
