using Godot;
using Societies.Core;
using Societies.Simulation;
using Societies.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Societies.Tests
{
    /// <summary>
    /// Deterministic scene-level SG-VX-01 diagnostic. It exercises the dedicated scene rather than a
    /// synthetic physics fixture and writes four review frames through Godot's viewport API.
    /// </summary>
    public partial class VoxelSceneDiagnosticRunner : Node
    {
        private const int CaptureWidth = 960;
        private const int CaptureHeight = 540;
        private const int LandingFrames = 180;
        private readonly List<string> _failures = new();
        private readonly Dictionary<string, string> _captureHashes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _cameraPoses = new(StringComparer.Ordinal);
        private string _isolationMarker = string.Empty;
        private string _desktopName = string.Empty;
        private string _activeDesktopName = string.Empty;

        public override void _Ready()
        {
            RunAsync();
        }

        private async void RunAsync()
        {
            try
            {
                string outputDirectory = ResolveRequiredArgument("--output-dir");
                Directory.CreateDirectory(outputDirectory);
                RequireAlternateDesktopIsolation(outputDirectory);
                ConfigureCaptureWindow();
                await RunDiagnosticAsync(outputDirectory);
                WriteIsolationEvidence(outputDirectory);
            }
            catch (Exception exception)
            {
                _failures.Add(exception.ToString());
            }

            if (_failures.Count == 0)
            {
                GD.Print("SG_VX_SCENE_DIAGNOSTIC PASS");
                GetTree().Quit(0);
                return;
            }

            foreach (string failure in _failures)
            {
                GD.PrintErr($"SG_VX_SCENE_DIAGNOSTIC FAIL: {failure}");
            }

            GetTree().Quit(1);
        }

        private async Task RunDiagnosticAsync(string outputDirectory)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            PackedScene dedicatedScene = GD.Load<PackedScene>("res://scenes/snow_globe_voxel_foundation.tscn")
                ?? throw new InvalidOperationException("Dedicated SG-VX-01 scene failed to load.");
            GameManager manager = dedicatedScene.Instantiate<GameManager>();
            AddChild(manager);
            // Rendered diagnostics must not take over the operator's desktop. PlayerCharacter
            // captures the mouse in _Ready for normal play, so immediately return it and disable
            // live window input before yielding a frame. Synthetic InputEventAction state still
            // exercises the production movement path later in this diagnostic.
            PlayerCharacter diagnosticPlayer = manager.GetNode<PlayerCharacter>("World/Players/LocalPlayer");
            diagnosticPlayer.SetProcessInput(false);
            diagnosticPlayer.SetProcessUnhandledInput(false);
            manager.SetProcessInput(false);
            manager.SetProcessUnhandledInput(false);
            Input.MouseMode = Input.MouseModeEnum.Visible;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            try
            {
                Require(manager.CurrentScenarioId == "snow_globe_voxel" && manager.UsesVoxelWorld,
                    "Dedicated scene did not select the voxel runtime authority.");
                VoxelWorldPresenter presenter = manager.GetNode<VoxelWorldPresenter>("World/VoxelWorldPresenter");
                PlayerCharacter player = diagnosticPlayer;
                RequireCaptureIsolation(player);
                GD.Print($"SG_VX_SCENE_DIAGNOSTIC collision initial bodies={presenter.GetChildren().OfType<StaticBody3D>().Count()} shapes={presenter.GetChildren().OfType<StaticBody3D>().SelectMany(body => body.GetChildren().OfType<CollisionShape3D>()).Count()}");
                PrototypeRuntimeSnapshot snapshot = manager.CaptureSnapshot();
                VoxelWorldModule world = VoxelWorldModule.Restore(snapshot.VoxelWorld
                    ?? throw new InvalidOperationException("Dedicated scene has no voxel snapshot."));
                Vector3 spawnColumn = new(player.GlobalPosition.X, 0.0f, player.GlobalPosition.Z);
                float spawnSurfaceY = GetSurfaceY(world, spawnColumn);
                float maximumSurfaceY = GetMaximumSurfaceY(world);
                Require(GetPlayerFootY(player) >= spawnSurfaceY + 0.5f,
                    $"Spawn lacks clearance: playerFootY={GetPlayerFootY(player):F3}, surfaceY={spawnSurfaceY:F3}.");
                RequireSafeSpawnNeighborhood(world, spawnColumn, spawnSurfaceY);
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                RequireRayHitsVoxelSurface(presenter, player, spawnColumn, spawnSurfaceY, "spawn");
                RequirePlayerCameraDownwardRay(player, presenter, spawnSurfaceY);
                RecordCameraPose("launch-player-view", player.GetNode<Camera3D>("CameraPivot/Camera3D"));
                await CaptureAsync(outputDirectory, "launch-player-view.png");
                Camera3D camera = CreateDiagnosticCamera(manager, "VoxelDiagnosticCamera");
                CreateSpawnDiagnosticMarker(manager, player, spawnSurfaceY, maximumSurfaceY);
                CreateOutsideAirDiagnosticMarker(manager, player, maximumSurfaceY);
                SetCamera(camera, "spawn", new Vector3(player.GlobalPosition.X + 12.0f, maximumSurfaceY + 50.0f, player.GlobalPosition.Z + 14.0f), player.GlobalPosition + new Vector3(0.0f, 1.2f, 0.0f));
                await CaptureAsync(outputDirectory, "spawn.png");

                RequireNoLegacySettlementPresentation(manager);
                SetCamera(camera, "settlement-terrain-wide", new Vector3(player.GlobalPosition.X + 32.0f, maximumSurfaceY + 70.0f, player.GlobalPosition.Z + 36.0f), player.GlobalPosition + new Vector3(0.0f, 1.0f, 0.0f));
                await CaptureAsync(outputDirectory, "settlement-terrain-wide.png");

                SetCamera(
                    camera,
                    "side-surface-diagnostic",
                    new Vector3(VoxelWorldModule.MinX - 8.0f, 12.0f, player.GlobalPosition.Z - 12.0f),
                    new Vector3((VoxelWorldModule.MinX - 3.0f + player.GlobalPosition.X) * 0.5f, maximumSurfaceY * 0.5f, player.GlobalPosition.Z - 3.0f));
                await CaptureAsync(outputDirectory, "side-surface-diagnostic.png");

                await AssertLandingAsync(player, world, spawnColumn, spawnSurfaceY, "spawn landing", LandingFrames);
                await TraverseAndAssertAsync(player, world, presenter, spawnColumn);
                VoxelCoord editableTop = FindExposedTopVoxel(world, spawnColumn);
                PositionAbove(player, editableTop, GetSurfaceY(world, ColumnCenter(editableTop)));
                Require(manager.ApplyVoxelPlayerIntent(VoxelEditKind.Remove, editableTop).Accepted,
                    "Authoritative remove at the tested traversal column was rejected.");
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                GD.Print($"SG_VX_SCENE_DIAGNOSTIC collision after-edit bodies={presenter.GetChildren().OfType<StaticBody3D>().Count()} shapes={presenter.GetChildren().OfType<StaticBody3D>().SelectMany(body => body.GetChildren().OfType<CollisionShape3D>()).Count()}");
                VoxelWorldModule removedWorld = VoxelWorldModule.Restore(manager.CaptureSnapshot().VoxelWorld!);
                float removedSurfaceY = GetSurfaceY(removedWorld, ColumnCenter(editableTop));
                Require(removedSurfaceY < GetSurfaceY(world, ColumnCenter(editableTop)),
                    "Removing the exposed top voxel did not lower the authoritative terrain surface.");
                    RequireRayHitsVoxelSurface(presenter, player, ColumnCenter(editableTop), removedSurfaceY, "after remove");
                await AssertLandingAsync(player, removedWorld, ColumnCenter(editableTop), removedSurfaceY, "edited-column landing", LandingFrames);

                SetCamera(camera, "after-physics-traversal", player.GlobalPosition + new Vector3(13.0f, 22.0f, 16.0f), player.GlobalPosition);
                await CaptureAsync(outputDirectory, "after-physics-traversal.png");
                GD.Print($"SG_VX_SCENE_DIAGNOSTIC state playerY={player.GlobalPosition.Y:F3} spawnSurfaceY={spawnSurfaceY:F3} removedSurfaceY={removedSurfaceY:F3}");
            }
            finally
            {
                manager.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        private async Task TraverseAndAssertAsync(PlayerCharacter player, VoxelWorldModule world, VoxelWorldPresenter presenter, Vector3 spawnColumn)
        {
            player.SetControlEnabled(false);
            int spawnX = Mathf.FloorToInt(spawnColumn.X);
            int spawnZ = Mathf.FloorToInt(spawnColumn.Z);
            player.GlobalPosition = new Vector3(spawnX + 0.5f, GetSurfaceY(world, spawnColumn) + 1.2f, spawnZ + 0.5f);
            player.Velocity = Vector3.Zero;
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            player.SetControlEnabled(true);

            // Exercise PlayerCharacter._PhysicsProcess -> HandleMovement -> MoveAndSlide rather
            // than translating the body from the diagnostic.  Each bounded leg stays within the
            // generated 5x5 clearing while proving that player input produces real collision-aware
            // travel in both axes.
            string[] actions = { "move_right", "move_forward", "move_left", "move_backward" };
            try
            {
                for (int step = 0; step < actions.Length; step++)
                {
                    await WalkControllerLegAsync(player, world, presenter, spawnX, spawnZ, actions[step], step + 1);
                }
            }
            finally
            {
                foreach (string action in actions)
                {
                    SendAction(action, false);
                }
            }
        }

        private async Task WalkControllerLegAsync(PlayerCharacter player, VoxelWorldModule world, VoxelWorldPresenter presenter, int spawnX, int spawnZ, string action, int step)
        {
            Vector3 start = player.GlobalPosition;
            SendAction(action, true);
            for (int frame = 0; frame < 10; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            SendAction(action, false);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

            float horizontalTravel = new Vector2(player.GlobalPosition.X - start.X, player.GlobalPosition.Z - start.Z).Length();
            float surfaceY = GetSurfaceY(world, player.GlobalPosition);
            Require(horizontalTravel >= 0.35f,
                $"Player controller did not move during bounded traversal step {step} ({action}); travelled={horizontalTravel:F3}.");
            Require(Mathf.Abs(player.GlobalPosition.X - (spawnX + 0.5f)) <= 2.1f && Mathf.Abs(player.GlobalPosition.Z - (spawnZ + 0.5f)) <= 2.1f,
                $"Player controller left the deterministic 5x5 spawn clearing during traversal step {step}: position={player.GlobalPosition}.");
            Require(GetPlayerFootY(player) >= surfaceY - 0.05f,
                $"Player crossed below authoritative surface during controller traversal step {step}: playerFootY={GetPlayerFootY(player):F3}, surfaceY={surfaceY:F3}.");
            RequireRayHitsVoxelSurface(presenter, player, player.GlobalPosition, surfaceY, $"controller-step-{step}");
        }

        private static void SendAction(string action, bool pressed)
        {
            Input.ParseInputEvent(new InputEventAction
            {
                Action = action,
                Pressed = pressed,
                Strength = pressed ? 1.0f : 0.0f
            });
        }

        private async Task AssertLandingAsync(PlayerCharacter player, VoxelWorldModule world, Vector3 column, float surfaceY, string phase, int frames)
        {
            player.SetControlEnabled(true);
            for (int frame = 0; frame < frames; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            Require(GetPlayerFootY(player) >= surfaceY - 0.05f,
                $"Player crossed below authoritative surface during {phase}: playerFootY={GetPlayerFootY(player):F3}, surfaceY={surfaceY:F3}.");
            Require(player.IsOnFloor(), $"Player did not report a grounded floor contact during {phase}.");
            Require(Mathf.Abs(GetPlayerFootY(player) - surfaceY) <= 0.05f,
                $"Player foot did not settle on the authoritative surface during {phase}: playerFootY={GetPlayerFootY(player):F3}, surfaceY={surfaceY:F3}.");
            Require(Mathf.Abs(player.Velocity.Y) <= 0.05f,
                $"Player retained vertical velocity after {phase}: velocityY={player.Velocity.Y:F3}.");
        }

        private void RequireRayHitsVoxelSurface(VoxelWorldPresenter presenter, PlayerCharacter player, Vector3 column, float authoritativeSurfaceY, string phase)
        {
            Vector3 origin = new(column.X, VoxelWorldModule.MaxYExclusive + 4.0f, column.Z);
            Vector3 target = new(column.X, VoxelWorldModule.MinY - 2.0f, column.Z);
            PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(origin, target);
            query.Exclude = new Godot.Collections.Array<Rid> { player.GetRid() };
            Godot.Collections.Dictionary hit = presenter.GetWorld3D().DirectSpaceState.IntersectRay(query);
            Require(hit.Count > 0, $"No collision ray hit for {phase} authoritative surface.");
            Node collider = hit["collider"].AsGodotObject() as Node
                ?? throw new InvalidOperationException($"Collision ray collider missing for {phase}.");
            Require(collider.Name.ToString().StartsWith("VoxelCollision_", StringComparison.Ordinal),
                $"{phase} collision ray hit non-voxel collider '{collider.Name}'.");
            Vector3 position = hit["position"].AsVector3();
            Require(Mathf.Abs(position.Y - authoritativeSurfaceY) <= 0.05f,
                $"Collision/render surface mismatch during {phase}: hitY={position.Y:F3}, authoritativeSurfaceY={authoritativeSurfaceY:F3}.");
        }

        private void RequirePlayerCameraDownwardRay(PlayerCharacter player, VoxelWorldPresenter presenter, float authoritativeSurfaceY)
        {
            Camera3D camera = player.GetNode<Camera3D>("CameraPivot/Camera3D");
            PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
                camera.GlobalPosition,
                camera.GlobalPosition + new Vector3(0.0f, VoxelWorldModule.MinY - VoxelWorldModule.MaxYExclusive - 4.0f, 0.0f));
            query.Exclude = new Godot.Collections.Array<Rid> { player.GetRid() };
            Godot.Collections.Dictionary hit = presenter.GetWorld3D().DirectSpaceState.IntersectRay(query);
            Require(hit.Count > 0 && hit["collider"].AsGodotObject() is Node node && node.Name.ToString().StartsWith("VoxelCollision_", StringComparison.Ordinal),
                "Player launch camera has no downward voxel-surface collision view.");
            if (hit.Count > 0)
            {
                Require(Mathf.Abs(hit["position"].AsVector3().Y - authoritativeSurfaceY) <= 0.05f,
                    "Player launch camera downward ray disagrees with the authoritative spawn surface.");
            }
        }

        private void RequireSafeSpawnNeighborhood(VoxelWorldModule world, Vector3 column, float surfaceY)
        {
            int x = Mathf.FloorToInt(column.X);
            int z = Mathf.FloorToInt(column.Z);
            foreach (int offsetX in Enumerable.Range(-2, 5))
            {
                foreach (int offsetZ in Enumerable.Range(-2, 5))
                {
                    float neighborSurfaceY = GetSurfaceY(world, new Vector3(x + offsetX + 0.5f, 0.0f, z + offsetZ + 0.5f));
                    Require(Mathf.Abs(neighborSurfaceY - surfaceY) <= 1.0f,
                        $"Spawn neighborhood is not visually clear at {x + offsetX},{z + offsetZ}: neighborY={neighborSurfaceY:F3}, spawnY={surfaceY:F3}.");
                }
            }
        }

        private void RequireNoLegacySettlementPresentation(GameManager manager)
        {
            Node3D agents = manager.GetNode<Node3D>("World/Agents");
            Node3D entities = manager.GetNode<Node3D>("World/Entities");
            Node3D environment = manager.GetNode<Node3D>("World/Environment");
            TerrainGenerator terrain = manager.GetNode<TerrainGenerator>("World/Systems/Terrain");
            PrototypeHud hud = manager.GetNode<PrototypeHud>("UI");
            IEnumerable<Node> legacyRoots = environment.GetChildren().Where(node => node is PrototypeSettlementHub ||
                node.Name.ToString() is "SettlementWorldCues" or "SettlementOverlays");
            Require(agents.GetChildCount() == 0, "Voxel scene retained legacy agent presentation.");
            Require(entities.GetChildCount() == 0, "Voxel scene retained legacy resource/world-object presentation.");
            Require(!legacyRoots.Any(), "Voxel scene retained a legacy settlement/world presenter under voxel terrain.");
            Require(terrain.GetChildCount() == 0, "Voxel scene retained heightfield terrain meshes, collision, landmarks, or spawn props.");
            Require(!Descendants(manager.GetNode<Node>("World")).Any(node => node is ResourceNode or PrototypeWorkerAgent or PrototypeSettlementHub),
                "Voxel scene retained a legacy resource, worker, depot, or settlement visual.");
            Require(hud.IsVoxelFoundationMode && !hud.HasVisibleLegacySettlementPanels,
                "Voxel scene retained legacy settlement HUD panels.");
            string visibleCopy = string.Join("\n", hud.HelpText, hud.StatusText, hud.InteractionText, hud.WorldText);
            Require(!visibleCopy.Contains("resource node", StringComparison.OrdinalIgnoreCase) &&
                !visibleCopy.Contains("central depot", StringComparison.OrdinalIgnoreCase) &&
                !visibleCopy.Contains("citizen", StringComparison.OrdinalIgnoreCase),
                "Voxel scene retained misleading legacy resource or settlement HUD guidance.");
        }

        private static IEnumerable<Node> Descendants(Node root) => root.GetChildren().SelectMany(child => new[] { child }.Concat(Descendants(child)));

        private static Camera3D CreateDiagnosticCamera(GameManager manager, string name)
        {
            Camera3D camera = new() { Name = name, Current = true, Fov = 62.0f, Near = 0.05f, Far = 256.0f };
            manager.GetNode<Node3D>("World").AddChild(camera);
            return camera;
        }

        private void SetCamera(Camera3D camera, string poseId, Vector3 position, Vector3 lookAt)
        {
            camera.GlobalPosition = position;
            camera.LookAt(lookAt, Vector3.Up);
            Require(camera.GlobalPosition.IsEqualApprox(position), $"Diagnostic camera pose '{poseId}' did not apply deterministically.");
            RecordCameraPose(poseId, camera);
        }

        private static void CreateSpawnDiagnosticMarker(GameManager manager, PlayerCharacter player, float surfaceY, float maximumSurfaceY)
        {
            Node3D marker = new() { Name = "VoxelDiagnosticSpawnMarker" };
            marker.AddChild(new MeshInstance3D
            {
                Name = "SurfaceMarker",
                Mesh = new CylinderMesh { TopRadius = 1.1f, BottomRadius = 1.1f, Height = 0.12f },
                Position = new Vector3(player.GlobalPosition.X, surfaceY + 0.06f, player.GlobalPosition.Z),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.18f, 0.95f, 0.98f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }
            });
            marker.AddChild(new MeshInstance3D
            {
                Name = "PlayerClearanceMarker",
                Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = (maximumSurfaceY - surfaceY) + 8.0f },
                Position = new Vector3(player.GlobalPosition.X, (surfaceY + maximumSurfaceY + 8.0f) * 0.5f, player.GlobalPosition.Z),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1.0f, 0.78f, 0.12f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }
            });
            marker.AddChild(new Label3D
            {
                Name = "DiagnosticLabel",
                Text = $"PLAYER SPAWN\nSURFACE Y {surfaceY:F0}",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 64,
                OutlineSize = 4,
                PixelSize = 0.02f,
                Position = new Vector3(player.GlobalPosition.X, maximumSurfaceY + 8.5f, player.GlobalPosition.Z),
                Modulate = new Color(0.98f, 0.95f, 0.72f)
            });
            manager.GetNode<Node3D>("World").AddChild(marker);
        }

        private static void CreateOutsideAirDiagnosticMarker(GameManager manager, PlayerCharacter player, float maximumSurfaceY)
        {
            float x = VoxelWorldModule.MinX - 3.0f;
            float z = player.GlobalPosition.Z - 6.0f;
            Node3D marker = new() { Name = "VoxelDiagnosticOutsideAirMarker" };
            marker.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.16f, Height = maximumSurfaceY + 12.0f },
                Position = new Vector3(x, (maximumSurfaceY + 12.0f) * 0.5f, z),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.18f, 0.84f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded }
            });
            marker.AddChild(new Label3D
            {
                Text = "OUTSIDE AIR\nVOXEL EDGE",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 56,
                PixelSize = 0.02f,
                OutlineSize = 4,
                Position = new Vector3(x, maximumSurfaceY + 9.0f, z),
                Modulate = new Color(1.0f, 0.72f, 0.96f)
            });
            manager.GetNode<Node3D>("World").AddChild(marker);
        }

        private async Task CaptureAsync(string outputDirectory, string fileName)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.Singleton.ForceSync();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Image image = GetViewport().GetTexture().GetImage();
            Require(image.GetWidth() == CaptureWidth && image.GetHeight() == CaptureHeight,
                $"Capture '{fileName}' is {image.GetWidth()}x{image.GetHeight()}, expected {CaptureWidth}x{CaptureHeight}.");
            byte[] pixels = image.GetData();
            Require(pixels.Any(value => value != 0) && pixels.Distinct().Take(2).Count() == 2,
                $"Capture '{fileName}' contains no nonempty rendered pixel variation.");
            string path = Path.Combine(outputDirectory, fileName);
            if (image.SavePng(path) != Error.Ok)
            {
                throw new InvalidOperationException($"Could not save capture '{path}'.");
            }
            _captureHashes[fileName] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        }

        private static VoxelCoord FindExposedTopVoxel(VoxelWorldModule world, Vector3 spawnColumn)
        {
            int centerX = Mathf.FloorToInt(spawnColumn.X);
            int centerZ = Mathf.FloorToInt(spawnColumn.Z);
            for (int z = centerZ - 2; z <= centerZ + 2; z++)
            {
                for (int x = centerX - 2; x <= centerX + 2; x++)
                {
                    Vector3 center = new(x + 0.5f, 0.0f, z + 0.5f);
                    int y = Mathf.FloorToInt(GetSurfaceY(world, center)) - 1;
                    VoxelCoord candidate = new(x, y, z);
                    if (world.GetMaterial(candidate) is VoxelMaterialId.Soil or VoxelMaterialId.Stone or VoxelMaterialId.Wood)
                    {
                        return candidate;
                    }
                }
            }

            throw new InvalidOperationException("No exposed editable voxel found for diagnostic.");
        }

        private static float GetSurfaceY(VoxelWorldModule world, Vector3 position)
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

            throw new InvalidOperationException($"Voxel column {x},{z} has no solid support.");
        }

        private static float GetMaximumSurfaceY(VoxelWorldModule world) => Enumerable.Range(VoxelWorldModule.MinX, VoxelWorldModule.MaxXExclusive - VoxelWorldModule.MinX)
            .SelectMany(x => Enumerable.Range(VoxelWorldModule.MinZ, VoxelWorldModule.MaxZExclusive - VoxelWorldModule.MinZ)
                .Select(z => GetSurfaceY(world, new Vector3(x + 0.5f, 0.0f, z + 0.5f))))
            .Max();

        private static Vector3 ColumnCenter(VoxelCoord voxel) => new(voxel.X + 0.5f, 0.0f, voxel.Z + 0.5f);

        private static void PositionAbove(PlayerCharacter player, VoxelCoord voxel, float surfaceY)
        {
            player.GlobalPosition = ColumnCenter(voxel) + new Vector3(0.0f, surfaceY + 2.0f, 0.0f);
            player.Velocity = Vector3.Zero;
        }

        private static float GetPlayerFootY(PlayerCharacter player)
        {
            CapsuleShape3D capsule = player.GetNode<CollisionShape3D>("Collision").Shape as CapsuleShape3D
                ?? throw new InvalidOperationException("Diagnostic player capsule is missing.");
            return player.GlobalPosition.Y - (capsule.Height * 0.5f);
        }

        private static void ConfigureCaptureWindow()
        {
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.MousePassthrough, true);
            DisplayServer.WindowSetSize(new Vector2I(CaptureWidth, CaptureHeight));
            DisplayServer.WindowSetPosition(new Vector2I(-CaptureWidth - 64, -CaptureHeight - 64));
        }

        private void RequireCaptureIsolation(PlayerCharacter player)
        {
            Require(DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.NoFocus),
                "Rendered diagnostic window can still take desktop focus.");
            Require(DisplayServer.WindowGetFlag(DisplayServer.WindowFlags.MousePassthrough),
                "Rendered diagnostic window can still intercept pointer clicks.");
            // A window may be focused within its isolated desktop. The fail-closed desktop
            // identity check above proves that focus is not on the operator's input desktop.
            Require(Input.MouseMode == Input.MouseModeEnum.Visible,
                "Rendered diagnostic retained captured mouse mode.");
            Require(!player.IsProcessingInput() && !player.IsProcessingUnhandledInput(),
                "Rendered diagnostic player still accepts live desktop input.");
        }

        private void RequireAlternateDesktopIsolation(string outputDirectory)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Rendered voxel diagnostic requires Windows alternate-desktop isolation.");
            }

            _isolationMarker = ResolveRequiredArgument("--isolation-marker");
            string markerPath = Path.GetFullPath(ResolveRequiredArgument("--isolation-marker-file"));
            string expectedDesktop = ResolveRequiredArgument("--isolation-desktop");
            string expectedActiveDesktop = ResolveRequiredArgument("--active-desktop");
            string outputRoot = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            EnsureIsolation(markerPath.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase),
                "Isolation marker must be inside the diagnostic output directory.");
            EnsureIsolation(File.Exists(markerPath) && string.Equals(File.ReadAllText(markerPath).Trim(), _isolationMarker, StringComparison.Ordinal),
                "Isolation marker is missing or does not match the launcher token.");
            File.Delete(markerPath);

            _desktopName = WindowsDesktopIdentity.GetCurrentThreadDesktopName();
            _activeDesktopName = WindowsDesktopIdentity.GetInputDesktopName();
            EnsureIsolation(string.Equals(_desktopName, expectedDesktop, StringComparison.Ordinal),
                $"Diagnostic process desktop '{_desktopName}' does not match launcher desktop '{expectedDesktop}'.");
            EnsureIsolation(string.Equals(_activeDesktopName, expectedActiveDesktop, StringComparison.Ordinal),
                $"Input desktop changed from launcher observation '{expectedActiveDesktop}' to '{_activeDesktopName}'.");
            EnsureIsolation(!string.Equals(_desktopName, _activeDesktopName, StringComparison.OrdinalIgnoreCase),
                "Rendered diagnostic targeted the active input desktop.");
        }

        private static void EnsureIsolation(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private void RecordCameraPose(string poseId, Camera3D camera)
        {
            Vector3 p = camera.GlobalPosition;
            Quaternion q = camera.GlobalTransform.Basis.GetRotationQuaternion();
            _cameraPoses[poseId] = FormattableString.Invariant(
                $"position={p.X:R},{p.Y:R},{p.Z:R};rotation={q.X:R},{q.Y:R},{q.Z:R},{q.W:R}");
        }

        private void WriteIsolationEvidence(string outputDirectory)
        {
            string json = JsonSerializer.Serialize(new
            {
                schema = "societies_sg_vx_hidden_desktop_evidence/v1",
                processId = System.Environment.ProcessId,
                isolationMarker = _isolationMarker,
                processDesktop = _desktopName,
                activeInputDesktop = _activeDesktopName,
                activeDesktopTargeted = false,
                liveInputDisabled = true,
                captureWidth = CaptureWidth,
                captureHeight = CaptureHeight,
                captures = _captureHashes,
                cameraPoses = _cameraPoses
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(outputDirectory, "isolation-evidence.json"), json, new UTF8Encoding(false));
        }

        private static class WindowsDesktopIdentity
        {
            private const int UoiName = 2;

            [DllImport("kernel32.dll")]
            private static extern uint GetCurrentThreadId();

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr GetThreadDesktop(uint threadId);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool CloseDesktop(IntPtr desktop);

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder? information, uint length, out uint needed);

            internal static string GetCurrentThreadDesktopName() => GetName(GetThreadDesktop(GetCurrentThreadId()));

            internal static string GetInputDesktopName()
            {
                IntPtr desktop = OpenInputDesktop(0, false, 0x0001);
                if (desktop == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"OpenInputDesktop failed with Win32 error {Marshal.GetLastWin32Error()}.");
                }
                try { return GetName(desktop); }
                finally { _ = CloseDesktop(desktop); }
            }

            private static string GetName(IntPtr handle)
            {
                _ = GetUserObjectInformation(handle, UoiName, null, 0, out uint needed);
                StringBuilder value = new((int)(needed / 2));
                if (!GetUserObjectInformation(handle, UoiName, value, needed, out _))
                {
                    throw new InvalidOperationException($"GetUserObjectInformation failed with Win32 error {Marshal.GetLastWin32Error()}.");
                }
                return value.ToString();
            }
        }

        private static string ResolveRequiredArgument(string argument)
        {
            string[] arguments = OS.GetCmdlineUserArgs();
            int index = Array.IndexOf(arguments, argument);
            return index >= 0 && index < arguments.Length - 1 && !string.IsNullOrWhiteSpace(arguments[index + 1])
                ? arguments[index + 1]
                : throw new InvalidOperationException($"Required argument {argument} is missing.");
        }

        private void Require(bool condition, string message)
        {
            if (!condition)
            {
                _failures.Add(message);
            }
        }
    }
}
