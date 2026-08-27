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
            await Test_SnowGlobeVoxelFoundationSmoke();
            await Test_SnowGlobeVoxelPlayerGroundingRegression();
            await Test_MainScene_DepotContributionInputSmoke();
            await Test_MainScene_DirectiveInputSmoke();
            await Test_MainScene_CivicPolicySelectionInputSmoke();
            await Test_MainScene_CrisisHudPresentationSmoke();
            Test_GodotCivicDeterministicLoopSmoke();
            await Test_MainScene_CrisisPersistenceInputSmoke();
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

        private async Task Test_SnowGlobeVoxelFoundationSmoke()
        {
            GameManager? manager = null;
            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/snow_globe_voxel_foundation.tscn");
                Assert(packedScene != null, "Voxel foundation scene failed to load");
                manager = packedScene!.Instantiate<GameManager>();
                AddChild(manager);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                AssertVoxelPlayerSpawnClearance(manager, "initial scene setup");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

                Assert(manager.CurrentScenarioId == "snow_globe_voxel", "Voxel foundation must select its catalog scenario");
                Assert(manager.UsesVoxelWorld, "Voxel foundation must select voxel authority");
                Assert(manager.CitizenCount == 0, "SG-VX-01 intentionally excludes citizen simulation");
                TerrainGenerator terrain = manager.GetNode<TerrainGenerator>("World/Systems/Terrain");
                VoxelWorldPresenter presenter = manager.GetNode<VoxelWorldPresenter>("World/VoxelWorldPresenter");
                Assert(!terrain.Visible && terrain.GetChildCount() == 0, "Heightfield presentation must be inactive in the voxel scenario");
                Assert(presenter.GetChildCount() > 0, "Voxel presenter must publish chunk mesh and collision nodes");
                StaticBody3D[] collisionBodies = presenter.GetChildren().OfType<StaticBody3D>().ToArray();
                Assert(collisionBodies.Length == VoxelWorldModule.ChunkCount, "Voxel presenter must keep one physics body per finite chunk");
                Assert(collisionBodies.All(body => body.GetChildren().OfType<CollisionShape3D>().Count() == 1 &&
                    body.GetChild<CollisionShape3D>(0).Shape is HeightMapShape3D), "Each voxel chunk must use one bounded heightmap grounding shape");
                Assert(presenter.HasLitVertexColorMaterial(), "Voxel meshes must publish normals and a vertex-color material");

                PrototypeRuntimeSnapshot before = manager.CaptureSnapshot();
                Assert(before.SchemaVersion == 10 && before.WorldModel == PrototypeWorldModels.Voxel, "Voxel runtime snapshot identity mismatch");
                Assert(before.Workers.Count == 0 && before.Resources.Count == 0, "Voxel snapshot must not smuggle heightfield settlement state");
                VoxelWorldModule snapshotWorld = VoxelWorldModule.Restore(before.VoxelWorld!);
                VoxelCoord target = FindEditableVoxel(snapshotWorld, 0, 0);

                PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
                    new Vector3(target.X + 0.5f, VoxelWorldModule.MaxYExclusive + 4.0f, target.Z + 0.5f),
                    new Vector3(target.X + 0.5f, VoxelWorldModule.MinY - 2.0f, target.Z + 0.5f));
                Godot.Collections.Dictionary hit = presenter.GetWorld3D().DirectSpaceState.IntersectRay(query);
                Assert(hit.Count > 0, "Outside ray must hit clockwise voxel collision geometry");
                Node collider = hit["collider"].AsGodotObject() as Node ?? throw new Exception("Voxel ray collider missing");
                Assert(collider.Name.ToString().StartsWith("VoxelCollision_", StringComparison.Ordinal), "Outside ray hit non-voxel collision");

                VoxelEditResult removed = manager.ApplyVoxelPlayerIntent(VoxelEditKind.Remove, target);
                Assert(removed.Accepted && manager.VoxelWorldRevision == 1, "Player remove intent did not cross the authoritative runtime path");
                PrototypeRuntimeSnapshot afterRemove = manager.CaptureSnapshot();
                Assert(afterRemove.WorldHash == before.WorldHash, "Voxel edit must preserve immutable world identity");
                Assert(afterRemove.VoxelWorld!.RootHash != before.VoxelWorld!.RootHash, "Voxel edit must change mutable state hash");
                Assert(VoxelWorldModule.Restore(afterRemove.VoxelWorld).GetMaterial(target) == VoxelMaterialId.Air, "Removed voxel remained solid");
                Assert(removed.DirtyChunks.All(chunk => presenter.HasChunkGeometryAndCollision(chunk)), "Dirty chunk mesh/collision was not rebuilt");

                VoxelEditResult placed = manager.ApplyVoxelPlayerIntent(VoxelEditKind.Place, target);
                Assert(placed.Accepted && manager.VoxelWorldRevision == 2, "Player place intent did not cross the authoritative runtime path");
                Assert(VoxelWorldModule.Restore(manager.CaptureSnapshot().VoxelWorld!).GetMaterial(target) == VoxelMaterialId.Wood, "Pointer placement must authoritatively place wood");

                manager.SetScenario("balanced_basin");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                Assert(!manager.UsesVoxelWorld && terrain.Visible, "Switching to a legacy scenario must restore heightfield presentation");
                Assert(!presenter.HasActiveCollisions, "Voxel collision shapes remained enabled in heightfield mode");
                Godot.Collections.Dictionary heightfieldHit = presenter.GetWorld3D().DirectSpaceState.IntersectRay(query);
                if (heightfieldHit.Count > 0)
                {
                    Node heightfieldCollider = heightfieldHit["collider"].AsGodotObject() as Node ?? throw new Exception("Heightfield ray collider missing");
                    Assert(!heightfieldCollider.Name.ToString().StartsWith("VoxelCollision_", StringComparison.Ordinal), "Disabled voxel collision remained ray-visible in heightfield mode");
                }
                manager.SetScenario("snow_globe_voxel");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                presenter = manager.GetNode<VoxelWorldPresenter>("World/VoxelWorldPresenter");
                Assert(manager.UsesVoxelWorld && presenter.Visible && presenter.HasLitVertexColorMaterial(), "Voxel presenter lifecycle failed after model switch");

                Pass(nameof(Test_SnowGlobeVoxelFoundationSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_SnowGlobeVoxelFoundationSmoke), ex);
            }
            finally
            {
                if (manager != null)
                {
                    manager.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private static VoxelCoord FindEditableVoxel(VoxelWorldModule world, int x, int z)
        {
            for (int y = 1; y < VoxelWorldModule.MaxYExclusive; y++)
            {
                VoxelCoord coord = new(x, y, z);
                if (world.GetMaterial(coord) is VoxelMaterialId.Soil or VoxelMaterialId.Stone or VoxelMaterialId.Wood)
                {
                    return coord;
                }
            }

            throw new InvalidOperationException("Voxel smoke column has no editable cell.");
        }

        private async Task Test_SnowGlobeVoxelPlayerGroundingRegression()
        {
            GameManager? manager = null;
            try
            {
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/snow_globe_voxel_foundation.tscn");
                Assert(packedScene != null, "Voxel foundation scene failed to load");
                manager = packedScene!.Instantiate<GameManager>();
                AddChild(manager);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                AssertVoxelPlayerSpawnClearance(manager, "initial scene setup");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

                PrototypeRuntimeSnapshot snapshot = manager.CaptureSnapshot();
                VoxelWorldModule world = VoxelWorldModule.Restore(snapshot.VoxelWorld!);
                await AssertVoxelPlayerGrounded(manager, "initial scene setup", 180);

                VoxelCoord target = FindExposedEditableTopVoxel(world);
                VoxelChunkCoord targetChunk = GetVoxelChunk(target);
                VoxelWorldPresenter presenter = manager.GetNode<VoxelWorldPresenter>("World/VoxelWorldPresenter");
                HeightMapShape3D beforeEditCollision = presenter.GetGroundingCollision(targetChunk) ??
                    throw new Exception("Target chunk has no heightmap grounding collision");
                float[] beforeEditMap = beforeEditCollision.MapData;
                int targetSampleIndex = GetHeightMapSampleIndex(target);
                float restoredSurfaceY = GetVoxelSurfaceY(world, GetVoxelColumnCenter(target));
                PositionPlayerAboveVoxelColumn(manager, target, restoredSurfaceY);

                Assert(manager.ApplyVoxelPlayerIntent(VoxelEditKind.Remove, target).Accepted, "Voxel remove must rebuild player collision safely");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                HeightMapShape3D removedCollision = presenter.GetGroundingCollision(targetChunk) ??
                    throw new Exception("Target chunk lost heightmap grounding collision after remove");
                float[] removedMap = removedCollision.MapData;
                PrototypeRuntimeSnapshot removedSnapshot = manager.CaptureSnapshot();
                VoxelWorldModule removedWorld = VoxelWorldModule.Restore(removedSnapshot.VoxelWorld!);
                float removedSurfaceY = GetVoxelSurfaceY(removedWorld, GetVoxelColumnCenter(target));
                Assert(removedSurfaceY < restoredSurfaceY, "Removing the exposed top voxel must lower the authoritative support surface");
                Assert(!ReferenceEquals(beforeEditCollision, removedCollision) && beforeEditMap[targetSampleIndex] != removedMap[targetSampleIndex],
                    "Voxel remove must replace the affected chunk heightmap sample");
                await AssertVoxelPlayerGroundedAtColumn(manager, target, removedSurfaceY, "dirty collision rebuild after remove", 90);

                Assert(manager.ApplyVoxelPlayerIntent(VoxelEditKind.Place, target).Accepted, "Voxel place must rebuild player collision safely");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                HeightMapShape3D restoredCollision = presenter.GetGroundingCollision(targetChunk) ??
                    throw new Exception("Target chunk lost heightmap grounding collision after place");
                float[] restoredMap = restoredCollision.MapData;
                PrototypeRuntimeSnapshot restoredSnapshot = manager.CaptureSnapshot();
                VoxelWorldModule restoredWorld = VoxelWorldModule.Restore(restoredSnapshot.VoxelWorld!);
                float placedSurfaceY = GetVoxelSurfaceY(restoredWorld, GetVoxelColumnCenter(target));
                Assert(placedSurfaceY == restoredSurfaceY, "Replacing the exposed voxel must restore the authoritative support surface");
                Assert(!ReferenceEquals(removedCollision, restoredCollision) && restoredMap[targetSampleIndex] == beforeEditMap[targetSampleIndex],
                    "Voxel place must replace and restore the affected chunk heightmap sample");
                float restoredHeightMapSample = restoredMap[targetSampleIndex];
                PositionPlayerAboveVoxelColumn(manager, target, placedSurfaceY);
                await AssertVoxelPlayerGroundedAtColumn(manager, target, placedSurfaceY, "dirty collision rebuild after place", 90);

                Assert(!string.IsNullOrWhiteSpace(manager.SaveSnapshotToDisk()), "Voxel snapshot save failed");
                Assert(manager.LoadLatestSnapshotFromDisk(), "Voxel snapshot load failed");
                HeightMapShape3D loadedCollision = presenter.GetGroundingCollision(targetChunk) ??
                    throw new Exception("Target chunk lost heightmap grounding collision after snapshot load");
                PrototypeRuntimeSnapshot loadedSnapshot = manager.CaptureSnapshot();
                VoxelWorldModule loadedWorld = VoxelWorldModule.Restore(loadedSnapshot.VoxelWorld!);
                float loadedSurfaceY = GetVoxelSurfaceY(loadedWorld, GetVoxelColumnCenter(target));
                Assert(loadedSurfaceY == placedSurfaceY && loadedCollision.MapData[targetSampleIndex] == restoredHeightMapSample,
                    "Snapshot load must restore the edited column's authoritative surface and heightmap sample");
                PositionPlayerAboveVoxelColumn(manager, target, loadedSurfaceY);
                await AssertVoxelPlayerGroundedAtColumn(manager, target, loadedSurfaceY, "snapshot load", 90);

                manager.ResetPrototypeRun();
                AssertVoxelPlayerSpawnClearance(manager, "reset");
                await AssertVoxelPlayerGrounded(manager, "reset", 90);
                manager.SetScenario("balanced_basin");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                manager.SetScenario("snow_globe_voxel");
                AssertVoxelPlayerSpawnClearance(manager, "heightfield to voxel lifecycle");
                await AssertVoxelPlayerGrounded(manager, "heightfield to voxel lifecycle", 90);
                Pass(nameof(Test_SnowGlobeVoxelPlayerGroundingRegression));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_SnowGlobeVoxelPlayerGroundingRegression), ex);
            }
            finally
            {
                if (manager != null)
                {
                    manager.QueueFree();
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }
        }

        private static float GetVoxelSurfaceY(VoxelWorldModule world, Vector3 position)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(position.X), VoxelWorldModule.MinX, VoxelWorldModule.MaxXExclusive - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(position.Z), VoxelWorldModule.MinZ, VoxelWorldModule.MaxZExclusive - 1);
            for (int y = VoxelWorldModule.MaxYExclusive - 1; y >= VoxelWorldModule.MinY; y--)
            {
                if (world.GetMaterial(new VoxelCoord(x, y, z)) != VoxelMaterialId.Air)
                {
                    return y + 1.0f;
                }
            }

            throw new InvalidOperationException("Player column has no authoritative voxel surface.");
        }

        private static VoxelCoord FindExposedEditableTopVoxel(VoxelWorldModule world)
        {
            for (int z = VoxelWorldModule.MinZ + 1; z < VoxelWorldModule.MaxZExclusive - 1; z++)
            {
                for (int x = VoxelWorldModule.MinX + 1; x < VoxelWorldModule.MaxXExclusive - 1; x++)
                {
                    Vector3 center = new(x + 0.5f, 0.0f, z + 0.5f);
                    int surfaceY = Mathf.FloorToInt(GetVoxelSurfaceY(world, center));
                    VoxelCoord target = new(x, surfaceY - 1, z);
                    if (world.GetMaterial(target) is not (VoxelMaterialId.Soil or VoxelMaterialId.Stone or VoxelMaterialId.Wood))
                    {
                        continue;
                    }

                    if (new[] { new Vector3(x - 0.5f, 0.0f, z + 0.5f), new Vector3(x + 1.5f, 0.0f, z + 0.5f), new Vector3(x + 0.5f, 0.0f, z - 0.5f), new Vector3(x + 0.5f, 0.0f, z + 1.5f) }
                        .All(neighbor => Mathf.FloorToInt(GetVoxelSurfaceY(world, neighbor)) == surfaceY))
                    {
                        return target;
                    }
                }
            }

            throw new InvalidOperationException("Voxel world has no safe exposed editable top cell.");
        }

        private static VoxelChunkCoord GetVoxelChunk(VoxelCoord coord) => new(
            ToChunkCoordinate(coord.X, VoxelWorldModule.ChunkWidth),
            0,
            ToChunkCoordinate(coord.Z, VoxelWorldModule.ChunkDepth));

        private static int GetHeightMapSampleIndex(VoxelCoord coord)
        {
            int localX = coord.X - (ToChunkCoordinate(coord.X, VoxelWorldModule.ChunkWidth) * VoxelWorldModule.ChunkWidth);
            int localZ = coord.Z - (ToChunkCoordinate(coord.Z, VoxelWorldModule.ChunkDepth) * VoxelWorldModule.ChunkDepth);
            return (localZ * (VoxelWorldModule.ChunkWidth + 1)) + localX;
        }

        private static int ToChunkCoordinate(int value, int size) => value >= 0 ? value / size : ((value + 1) / size) - 1;

        private static Vector3 GetVoxelColumnCenter(VoxelCoord coord) => new(coord.X + 0.5f, 0.0f, coord.Z + 0.5f);

        private static void PositionPlayerAboveVoxelColumn(GameManager manager, VoxelCoord column, float surfaceY)
        {
            PlayerCharacter player = manager.GetNode<PlayerCharacter>("World/Players/LocalPlayer");
            player.GlobalPosition = GetVoxelColumnCenter(column) + new Vector3(0.0f, surfaceY + 2.0f, 0.0f);
            player.Velocity = Vector3.Zero;
        }

        private async Task AssertVoxelPlayerGrounded(GameManager manager, string phase, int physicsFrames)
        {
            PlayerCharacter player = manager.GetNode<PlayerCharacter>("World/Players/LocalPlayer");
            PrototypeRuntimeSnapshot snapshot = manager.CaptureSnapshot();
            VoxelWorldModule world = VoxelWorldModule.Restore(snapshot.VoxelWorld!);
            Vector3 spawnColumn = snapshot.SettlementAnchorPosition.ToVector3() + new Vector3(0.0f, 0.0f, -8.0f);
            float surfaceY = GetVoxelSurfaceY(world, spawnColumn);
            for (int frame = 0; frame < physicsFrames; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            Assert(
                player.GlobalPosition.Y >= surfaceY,
                $"Voxel player crossed below its authoritative spawn surface during {phase}: playerY={player.GlobalPosition.Y:F3}, surfaceY={surfaceY:F3}");
        }

        private async Task AssertVoxelPlayerGroundedAtColumn(
            GameManager manager,
            VoxelCoord column,
            float surfaceY,
            string phase,
            int physicsFrames)
        {
            PlayerCharacter player = manager.GetNode<PlayerCharacter>("World/Players/LocalPlayer");
            for (int frame = 0; frame < physicsFrames; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            Assert(
                player.GlobalPosition.Y >= surfaceY,
                $"Voxel player crossed below the edited column's authoritative surface during {phase}: playerY={player.GlobalPosition.Y:F3}, surfaceY={surfaceY:F3}, column={column.X},{column.Z}");
        }

        private void AssertVoxelPlayerSpawnClearance(GameManager manager, string phase)
        {
            PlayerCharacter player = manager.GetNode<PlayerCharacter>("World/Players/LocalPlayer");
            PrototypeRuntimeSnapshot snapshot = manager.CaptureSnapshot();
            VoxelWorldModule world = VoxelWorldModule.Restore(snapshot.VoxelWorld!);
            Vector3 spawnColumn = snapshot.SettlementAnchorPosition.ToVector3() + new Vector3(0.0f, 0.0f, -8.0f);
            float surfaceY = GetVoxelSurfaceY(world, spawnColumn);
            Assert(
                player.GlobalPosition.Y >= surfaceY + 1.5f,
                $"Voxel player spawned without clearance during {phase}: playerY={player.GlobalPosition.Y:F3}, surfaceY={surfaceY:F3}");
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

        private async Task Test_MainScene_CivicPolicySelectionInputSmoke()
        {
            Node? scene = null;
            string outputDirectory = CreateRunOutputDirectory(nameof(Test_MainScene_CivicPolicySelectionInputSmoke));

            try
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", outputDirectory);
                PackedScene packedScene = GD.Load<PackedScene>("res://scenes/main.tscn");
                Assert(packedScene != null, "Main scene failed to load");
                scene = packedScene!.Instantiate();
                AddChild(scene);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

                GameManager manager = scene as GameManager ?? throw new Exception("Main scene root is not GameManager");
                manager.SetProcess(false);
                PrototypeHud hud = manager.GetNodeOrNull<PrototypeHud>("UI") ?? throw new Exception("PrototypeHud missing");

                AssertCivicPolicyInput(
                    manager,
                    hud,
                    Key.Key4,
                    "Protect",
                    "Healthy 75/100",
                    "Reeds: 0/4, 4 left | fewer; preserved",
                    "supports Protect",
                    "opposes Protect");

                manager.ResetPrototypeRun();
                Assert(hud.CrisisText.Contains("Policy: not selected (neutral)", StringComparison.Ordinal),
                    "F7-equivalent reset must restore a neutral player-facing civic reading");

                AssertCivicPolicyInput(
                    manager,
                    hud,
                    Key.Key5,
                    "Drawdown",
                    "Strained 45/100",
                    "Reeds: 0/12, 12 left | more; degrades",
                    "opposes Drawdown",
                    "supports Drawdown");

                AssertCivicCognitionSaveLoadGuard(manager, hud, outputDirectory);

                Pass(nameof(Test_MainScene_CivicPolicySelectionInputSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_CivicPolicySelectionInputSmoke), ex);
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

        private void AssertCivicPolicyInput(
            GameManager manager,
            PrototypeHud hud,
            Key inputKey,
            string policyLabel,
            string healthText,
            string quotaText,
            string expectedFutureReedStance,
            string expectedShelterStance)
        {
            manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = inputKey });
            Assert(hud.StatusText.Contains($"Civic policy selected: {policyLabel}", StringComparison.Ordinal),
                "Player civic input should report the accepted policy");
            Assert(hud.CrisisText.Contains($"Policy: {policyLabel}", StringComparison.Ordinal) &&
                hud.CrisisText.Contains(healthText, StringComparison.Ordinal) &&
                hud.CrisisText.Contains(quotaText, StringComparison.Ordinal),
                "Player civic input should refresh the policy, quota, wetland health, and consequence HUD");

            manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = inputKey });
            Assert(hud.StatusText.Contains("Civic policy rejected: already_selected", StringComparison.Ordinal),
                "A duplicate player civic input must reject through the session's one-selection guard");

            bool foundFutureReed = false;
            bool foundImmediateShelter = false;
            for (int index = 0; index < manager.CitizenCount; index++)
            {
                manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.F3 });
                foundFutureReed |= hud.InspectorText.Contains("future reeds", StringComparison.Ordinal) &&
                    hud.InspectorText.Contains(expectedFutureReedStance, StringComparison.Ordinal);
                foundImmediateShelter |= hud.InspectorText.Contains("shelter now", StringComparison.Ordinal) &&
                    hud.InspectorText.Contains(expectedShelterStance, StringComparison.Ordinal);
            }

            Assert(foundFutureReed,
                "Cycling the main-scene inspector should expose the future-reeds interest with its selected-policy stance");
            Assert(foundImmediateShelter,
                "Cycling the main-scene inspector should expose the immediate-shelter interest with its selected-policy stance");

            int cognitionEventsBefore = manager.CivicCognitionDecisionCount;
            PrototypeRuntimeSnapshot beforeCognition = manager.CaptureSnapshot();
            string policyBeforeCognition = beforeCognition.CivicPolicy?.PolicyId
                ?? throw new Exception("Civic snapshot must contain the selected policy");
            manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.Key6 });
            PrototypeRuntimeSnapshot afterCognition = manager.CaptureSnapshot();
            Assert(hud.StatusText.Contains(
                    "Civic cognition: deterministic_fallback | civic.cognition.decision",
                    StringComparison.Ordinal) &&
                manager.CivicCognitionDecisionCount == cognitionEventsBefore + 1 &&
                afterCognition.CivicPolicy?.PolicyId == policyBeforeCognition,
                "Key 6 should record exactly one offline fallback cognition event without changing policy");
            manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.Key6 });
            Assert(hud.StatusText.Contains("Civic cognition rejected: already applied", StringComparison.Ordinal) &&
                manager.CivicCognitionDecisionCount == cognitionEventsBefore + 1,
                "A duplicate Key 6 input must fail closed from the authoritative cognition event history");
        }

        private void AssertCivicCognitionSaveLoadGuard(
            GameManager manager,
            PrototypeHud hud,
            string outputDirectory)
        {
            PrototypeRuntimeSnapshot beforeSave = manager.CaptureSnapshot();
            string policyBeforeSave = beforeSave.CivicPolicy?.PolicyId
                ?? throw new Exception("Civic snapshot must contain the selected policy before save");
            int cognitionEventsBeforeSave = manager.CivicCognitionDecisionCount;
            string snapshotPath = manager.SaveSnapshotToDisk();
            Assert(File.Exists(snapshotPath) && Path.GetDirectoryName(snapshotPath) == outputDirectory,
                "The civic cognition smoke must save through the isolated GameManager artifact route");

            Assert(manager.LoadLatestSnapshotFromDisk(),
                "GameManager should restore the saved civic cognition artifact generation");
            PrototypeRuntimeSnapshot afterLoad = manager.CaptureSnapshot();
            Assert(
                manager.CivicCognitionDecisionCount == cognitionEventsBeforeSave &&
                afterLoad.CivicPolicy?.PolicyId == policyBeforeSave,
                "Schema-v9 load must preserve both the cognition decision history and selected policy");

            manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.Key6 });
            PrototypeRuntimeSnapshot afterRejectedResume = manager.CaptureSnapshot();
            Assert(
                hud.StatusText.Contains("Civic cognition rejected: already applied", StringComparison.Ordinal) &&
                manager.CivicCognitionDecisionCount == cognitionEventsBeforeSave &&
                afterRejectedResume.CivicPolicy?.PolicyId == policyBeforeSave,
                "A resumed Key 6 must reject without adding an event or changing policy");

            manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.F7 });
            Assert(manager.CivicCognitionDecisionCount == 0,
                "F7 should create a fresh event history for the next author action");
            manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.Key4 });
            manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.Key6 });
            Assert(
                hud.StatusText.Contains(
                    "Civic cognition: deterministic_fallback | civic.cognition.decision",
                    StringComparison.Ordinal) &&
                manager.CivicCognitionDecisionCount == 1,
                "A fresh F7 session should permit one new deterministic fallback cognition decision");
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
                Assert(
                    hud.CrisisText.Contains("Policy: not selected (neutral)", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Wetland: Strained 60/100", StringComparison.Ordinal),
                    "Game manager HUD refresh should forward the read-only neutral wetland snapshot");

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
                Assert(
                    hud.CrisisText.Contains("Outcome: Stable: all conditions held 2/2 ticks", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Hold: stable 2/2", StringComparison.Ordinal),
                    "Live compact HUD presenter should show the complete terminal causal outcome and hold progress");

                manager.SetScenario("balanced_basin");
                Assert(
                    hud.CrisisText.Contains("Crisis: none", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Policy: not selected (neutral)", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Wetland: Strained 60/100", StringComparison.Ordinal) &&
                    !hud.CrisisText.Contains("Reeds:", StringComparison.Ordinal) &&
                    !hud.CrisisText.Contains("Effect:", StringComparison.Ordinal),
                    "Crisis-absent scenarios should retain the neutral, not-selected wetland reading without a selected consequence");
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

        private void Test_GodotCivicDeterministicLoopSmoke()
        {
            try
            {
                foreach ((PrototypeCivicPolicy policy, int quota, int selectionHealth, int harvestHealth) scenario in new[]
                {
                    (PrototypeCivicPolicy.ProtectWetland, 4, 75, 74),
                    (PrototypeCivicPolicy.DrawDownWetland, 12, 45, 43)
                })
                {
                    PrototypeCatalogBundle bundle = LoadCatalogBundle();
                    PrototypeRuntimeSession session = new(
                        bundle.Scenarios.Resolve("balanced_basin"),
                        bundle.RoleQuotas.Roles,
                        resourceDefinitions: bundle.Resources.Resources);
                    session.Initialize(8.0f);
                    session.Workers[0].Role = PrototypeCitizenRole.Forager;
                    session.Workers[0].Needs.Nutrition = 100.0f;
                    session.Workers[0].Needs.Fatigue = 0.0f;
                    session.Workers[1].Role = PrototypeCitizenRole.Builder;
                    session.Workers[1].Needs.Nutrition = 100.0f;
                    session.Workers[1].Needs.Fatigue = 0.0f;

                    Assert(session.SelectCivicPolicy(new(scenario.policy, ExpectedVersion: 0, IssuedTick: 0)).Succeeded,
                        "Fixed catalog civic selection should succeed at tick zero");
                    Assert(session.CaptureCitizenInterests().Any(interest =>
                        interest.Reason == PrototypeCitizenInterestReason.FutureReedSupply),
                        "The civic loop should retain the forager's structured future-reed reason");
                    Assert(session.CaptureCitizenInterests().Any(interest =>
                        interest.Reason == PrototypeCitizenInterestReason.ImmediateShelterSupply),
                        "The civic loop should retain the builder's structured immediate-shelter reason");
                    Assert(session.Wetland.ReedQuotaLimit == scenario.quota &&
                        session.Wetland.WetlandHealth == scenario.selectionHealth,
                        "Selected policy should expose its bounded wetland consequence");

                    PrototypeResourceSnapshot reeds = session.ResourceSnapshots.First(resource =>
                        resource.ResourceId == PrototypeWetlandCatalog.ReedResourceId && resource.UnitsRemaining > 0);
                    Assert(session.HarvestForPlayer(reeds.SiteId, 1).Succeeded,
                        "A single reed harvest within the selected quota should succeed");
                    Assert(session.Wetland.WetlandHealth == scenario.harvestHealth,
                        "The reed harvest should apply the selected policy's exact health consequence");

                    PrototypeCognitionModule cognition = new();
                    PrototypeCognitionObservation observation = cognition.PublishObservation(session, session.Workers[0].WorkerId);
                    PrototypeCognitionResolution fallback = cognition.Resolve(
                        session,
                        observation,
                        PrototypeCognitionEvidence.Unavailable());
                    int before = session.EventLog.Entries.Count(entry =>
                        entry.EventType == PrototypeEventTypes.CivicCognitionDecision);
                    Assert(fallback.Accepted &&
                        fallback.Source == PrototypeCognitionDecisionSource.DeterministicFallback &&
                        cognition.Apply(session, fallback),
                        "Offline cognition should use the deterministic fallback event path");
                    Assert(!cognition.Apply(session, fallback) &&
                        session.EventLog.Entries.Count(entry =>
                            entry.EventType == PrototypeEventTypes.CivicCognitionDecision) == before + 1,
                        "A cognition resolution must append exactly one event and cannot mutate policy twice");
                    Assert(PrototypeHudTextBuilder.BuildCompactWetlandText(session.Wetland).Contains(
                        scenario.policy == PrototypeCivicPolicy.ProtectWetland ? "Policy: Protect" : "Policy: Drawdown",
                        StringComparison.Ordinal),
                        "The Godot presentation formatter should retain the selected policy consequence");
                }

                Pass(nameof(Test_GodotCivicDeterministicLoopSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_GodotCivicDeterministicLoopSmoke), ex);
            }
        }

        private async Task Test_MainScene_CrisisPersistenceInputSmoke()
        {
            Node? scene = null;
            string outputDirectory = CreateRunOutputDirectory(nameof(Test_MainScene_CrisisPersistenceInputSmoke));

            try
            {
                System.Environment.SetEnvironmentVariable("SOCIETIES_RUN_OUTPUT_DIR", outputDirectory);
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

                manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.Key2 });
                manager.Inventory.AddItem("logs", 3);
                player.GlobalPosition = manager.CentralDepotPosition;
                player.ProcessInteractionInput(701);
                manager.StepSimulationTicks(5);
                manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.F6 });

                string snapshotPath = Path.Combine(outputDirectory, "latest-snapshot.json");
                Assert(File.Exists(snapshotPath), "F6 should persist the crisis snapshot");
                PrototypeRuntimeSnapshot persisted = PrototypePersistenceService.LoadSnapshot(snapshotPath);
                Assert(persisted.SchemaVersion == 9, "Godot save route should emit strict schema v9");
                Assert(persisted.Directive?.DirectiveId == "food_and_fuel", "F6 should persist the input-selected directive");
                Assert(persisted.ContributionCountsByResource.GetValueOrDefault("logs") == 3, "F6 should persist input contributions");
                Assert(persisted.Crisis?.ElapsedTicks == 5, "F6 should persist crisis elapsed ticks");
                Assert(
                    persisted.CivicPolicy?.PolicyId == "neutral" &&
                    persisted.Wetland?.PolicyId == "neutral" &&
                    persisted.Wetland.ReedQuotaLimit == 0 &&
                    persisted.Wetland.ReedQuotaConsumed == 0 &&
                    persisted.Wetland.WetlandHealth == 60 &&
                    persisted.Wetland.WetlandHealthBand == "strained",
                    "F6 should persist the authoritative neutral civic and wetland state in schema v9");
                Assert(hud.CrisisText.Contains("Directive: Food & Fuel", StringComparison.Ordinal), "HUD should show the saved directive");
                Assert(hud.CrisisText.Contains("logs x3", StringComparison.Ordinal), "HUD should show the saved contribution");

                manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.Key3 });
                manager.StepSimulationTicks(2);
                manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.F7 });
                Assert(manager.CurrentDirective == PrototypeSettlementDirective.Neutral, "F7 should reset directive state");
                Assert(manager.SimulationTick == 0, "F7 should reset crisis time");

                manager._UnhandledInput(new InputEventKey { Pressed = true, Keycode = Key.F9 });
                PrototypeRuntimeSnapshot restored = manager.CaptureSnapshot();
                Assert(manager.CurrentDirective == PrototypeSettlementDirective.FoodAndFuel, "F9 should restore the input-selected directive");
                Assert(restored.ContributionCountsByResource.GetValueOrDefault("logs") == 3, "F9 should restore contribution counters");
                Assert(restored.Crisis?.ElapsedTicks == 5, "F9 should restore crisis elapsed ticks");
                Assert(hud.CrisisText.Contains("Directive: Food & Fuel", StringComparison.Ordinal), "HUD should refresh to the restored directive");
                Assert(hud.CrisisText.Contains("logs x3", StringComparison.Ordinal), "HUD should refresh to the restored contribution");

                string liveBeforeCorruptLoad = PrototypePersistenceService.SerializeSnapshot(
                    manager.CaptureSnapshot());
                File.AppendAllText(
                    Path.Combine(outputDirectory, "latest-event-log.json"),
                    " ");
                bool rejectedCorruptGeneration = false;
                try
                {
                    _ = manager.LoadLatestSnapshotFromDisk();
                }
                catch (InvalidDataException)
                {
                    rejectedCorruptGeneration = true;
                }

                Assert(rejectedCorruptGeneration, "GameManager should reject a tampered schema-v9 companion");
                Assert(
                    PrototypePersistenceService.SerializeSnapshot(manager.CaptureSnapshot()) ==
                    liveBeforeCorruptLoad,
                    "Rejected artifact generation should leave live GameManager state unchanged");

                Pass(nameof(Test_MainScene_CrisisPersistenceInputSmoke));
            }
            catch (Exception ex)
            {
                Fail(nameof(Test_MainScene_CrisisPersistenceInputSmoke), ex);
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

        private void Test_VisualCaptureConfigurationAndHudLayout()
        {
            try
            {
                string[] expected = { "arrival", "settlement_overview", "contribution_point", "citizen_inspection", "terminal_crisis" };
                Assert(expected.OrderBy(id => id).SequenceEqual(PrototypeVisualCaptureConfiguration.PresetIds.OrderBy(id => id)), "Capture configuration should define exactly five named presets");
                Assert(PrototypeVisualCaptureConfiguration.ScenarioId == "empty_stores", "Capture scenario should be empty_stores");
                Assert(PrototypeVisualCaptureConfiguration.SimulationSeed == 1701, "Capture seed should be fixed");
                Assert(PrototypeVisualCaptureConfiguration.TerminalCrisisTick == 9777, "Capture terminal-crisis tick should match the observed canonical terminal state");
                Assert(PrototypeVisualCaptureConfiguration.TerminalCrisisEventCount == 8149, "Capture terminal-crisis provenance should retain the schema-v7 10.5 reference event count");
                Assert(PrototypeVisualCaptureConfiguration.TerminalCrisisTraceSha256 == "8a0239837c5f96ac5ef0e470e9e91178d620b7362213cf47eaa2aa20b637eecc", "Capture terminal-crisis provenance should retain the schema-v7 10.5 reference trace hash");
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

                Assert(
                    hud.CrisisText.Contains("Crisis: Empty Stores", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Time:", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Directive: Neutral", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Contributed:", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Stable conditions:", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Hold:", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Policy: not selected (neutral)", StringComparison.Ordinal) &&
                    hud.CrisisText.Contains("Wetland: Strained 60/100", StringComparison.Ordinal),
                    "Normal view should expose bounded crisis and neutral civic-wetland state text");
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
            string runtimeMetricsPath = Path.Combine(outputDirectory, "runtime-batch-metrics-v6.csv");
            string legacyRuntimeMetricsPath = Path.Combine(outputDirectory, "runtime-batch-metrics-v5.csv");
            string olderLegacyRuntimeMetricsPath = Path.Combine(outputDirectory, "runtime-batch-metrics-v4.csv");
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
                File.WriteAllText(legacyRuntimeMetricsPath, "stale legacy runtime metrics");
                File.WriteAllText(olderLegacyRuntimeMetricsPath, "stale older legacy runtime metrics");
                disabledManager.SaveSnapshotToDisk();
                Assert(!File.Exists(runtimeMetricsPath), "A metrics-disabled save should remove a stale runtime metrics artifact");
                Assert(!File.Exists(legacyRuntimeMetricsPath), "A metrics-disabled save should remove the legacy runtime metrics artifact");
                Assert(!File.Exists(olderLegacyRuntimeMetricsPath), "A metrics-disabled save should remove the older legacy runtime metrics artifact");
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

                File.WriteAllText(legacyRuntimeMetricsPath, "preserve until v6 succeeds");
                Directory.CreateDirectory(runtimeMetricsPath);
                manager.SaveSnapshotToDisk();
                Assert(
                    Directory.Exists(runtimeMetricsPath),
                    "An optional metrics export failure should not fail the core save");
                Assert(
                    File.Exists(legacyRuntimeMetricsPath),
                    "A failed v6 metrics write should preserve the legacy artifact");
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
                Assert(afterManualStep[0].Phases.BuildWorkOrdersInputPreparationMilliseconds > 0.0, "Manual batch should measure BuildWorkOrders input preparation");
                Assert(afterManualStep[0].Phases.BuildWorkOrdersNonExtractionMilliseconds > 0.0, "Manual batch should measure non-extraction work-order synthesis");
                Assert(afterManualStep[0].Phases.BuildWorkOrdersReserveExtractionMilliseconds > 0.0, "Manual batch should measure reserve-extraction generation");
                Assert(afterManualStep[0].Phases.BuildWorkOrdersFinalizationMilliseconds > 0.0, "Manual batch should measure BuildWorkOrders finalization");
                Assert(afterManualStep[0].Phases.ReserveExtractionClassPreparationMilliseconds > 0.0, "Manual batch should measure reserve-extraction class preparation");
                Assert(afterManualStep[0].Phases.ReserveExtractionCandidateEnumerationAndBoundSelectionMilliseconds > 0.0, "Manual batch should measure reserve-extraction candidate enumeration and bound selection");
                Assert(afterManualStep[0].Phases.ReserveExtractionActiveFrontierAndClaimEvaluationMilliseconds > 0.0, "Manual batch should measure reserve-extraction frontier and claim evaluation");
                Assert(afterManualStep[0].Phases.ReserveExtractionRetainedMaterializationMilliseconds > 0.0, "Manual batch should measure retained reserve-extraction materialization");
                double reserveExtractionProfileTotal =
                    afterManualStep[0].Phases.ReserveExtractionClassPreparationMilliseconds +
                    afterManualStep[0].Phases.ReserveExtractionCandidateEnumerationAndBoundSelectionMilliseconds +
                    afterManualStep[0].Phases.ReserveExtractionActiveFrontierAndClaimEvaluationMilliseconds +
                    afterManualStep[0].Phases.ReserveExtractionRetainedMaterializationMilliseconds;
                Assert(
                    reserveExtractionProfileTotal <= afterManualStep[0].Phases.BuildWorkOrdersReserveExtractionMilliseconds + 0.001,
                    "Sequential reserve-extraction profile phases must reconcile within the inclusive parent");
                double buildWorkOrdersProfileTotal =
                    afterManualStep[0].Phases.BuildWorkOrdersInputPreparationMilliseconds +
                    afterManualStep[0].Phases.BuildWorkOrdersNonExtractionMilliseconds +
                    afterManualStep[0].Phases.BuildWorkOrdersReserveExtractionMilliseconds +
                    afterManualStep[0].Phases.BuildWorkOrdersFinalizationMilliseconds;
                Assert(
                    buildWorkOrdersProfileTotal <= afterManualStep[0].Phases.BuildWorkOrdersMilliseconds + 0.001,
                    "Sequential BuildWorkOrders profile phases must reconcile within the inclusive parent");
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
                Assert(!File.Exists(legacyRuntimeMetricsPath), "A successful v6 metrics write should remove the legacy artifact");
                Assert(!File.Exists(olderLegacyRuntimeMetricsPath), "A successful v6 metrics write should remove the older legacy artifact");
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
                    "selector_selected_route_reuses_total",
                    "reserve_extraction_class_preparation_ms",
                    "reserve_extraction_candidate_enumeration_and_bound_selection_ms",
                    "reserve_extraction_active_frontier_and_claim_evaluation_ms",
                    "reserve_extraction_retained_materialization_ms"
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
