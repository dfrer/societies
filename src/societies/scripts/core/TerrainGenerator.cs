using Godot;
using System.Collections.Generic;

namespace Societies.Core
{
    /// <summary>
    /// Renders the current prototype terrain from pure world data.
    /// Falls back to a flat plane when no generated world has been applied.
    /// </summary>
    public partial class TerrainGenerator : Node3D
    {
        [Export] public float WorldSize { get; set; } = 500.0f;
        [Export] public float GroundHeight { get; set; } = 0.0f;

        private WorldMapState? _currentWorld;
        private TerrainOverlayMode _overlayMode;

        public float WorldHalfSize => WorldSize * 0.5f;

        public override void _Ready()
        {
            if (GetChildCount() == 0)
            {
                RebuildTerrain();
            }
        }

        public void ApplyWorld(WorldMapState world, TerrainOverlayMode overlayMode)
        {
            _currentWorld = world;
            WorldSize = world.WorldSize;
            _overlayMode = overlayMode;
            RebuildTerrain();
        }

        public void SetOverlayMode(TerrainOverlayMode mode)
        {
            _overlayMode = mode;
            RebuildTerrain();
        }

        public void RebuildTerrain()
        {
            foreach (Node child in GetChildren())
            {
                child.Free();
            }

            if (_currentWorld == null)
            {
                BuildFlatGround();
            }
            else
            {
                BuildHeightfieldGround(_currentWorld, _overlayMode);
            }

            BuildLandmarks();
        }

        public Vector3 GetPlayerSpawnPoint()
        {
            return GetPlayerSpawnPoint(Vector3.Zero);
        }

        public Vector3 GetPlayerSpawnPoint(Vector3 desiredHorizontalPosition)
        {
            Vector3 spawnPosition = new(desiredHorizontalPosition.X, 0.0f, desiredHorizontalPosition.Z);
            float height = SampleHeight(spawnPosition);
            return new Vector3(spawnPosition.X, height + 2.0f, spawnPosition.Z);
        }

        public PrototypeSpawnBounds GetSpawnBounds()
        {
            return new PrototypeSpawnBounds(WorldHalfSize, GroundHeight);
        }

        public float SampleHeight(Vector3 worldPosition)
        {
            return _currentWorld?.SampleHeight(worldPosition) ?? GroundHeight;
        }

        private void BuildFlatGround()
        {
            MeshInstance3D groundMesh = new()
            {
                Name = "Ground"
            };
            groundMesh.Mesh = new PlaneMesh
            {
                Size = new Vector2(WorldSize, WorldSize),
                SubdivideDepth = 8,
                SubdivideWidth = 8
            };
            groundMesh.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.24f, 0.47f, 0.23f),
                Roughness = 1.0f
            };
            AddChild(groundMesh);

            StaticBody3D collider = new()
            {
                Name = "GroundCollider"
            };
            CollisionShape3D collisionShape = new()
            {
                Shape = new BoxShape3D
                {
                    Size = new Vector3(WorldSize, 1.0f, WorldSize)
                },
                Position = new Vector3(0.0f, -0.5f, 0.0f)
            };
            collider.AddChild(collisionShape);
            AddChild(collider);
        }

        private void BuildHeightfieldGround(WorldMapState world, TerrainOverlayMode overlayMode)
        {
            SurfaceTool surfaceTool = new();
            surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

            for (int y = 0; y < world.GridHeight - 1; y++)
            {
                for (int x = 0; x < world.GridWidth - 1; x++)
                {
                    TerrainCell a = world.GetCell(x, y);
                    TerrainCell b = world.GetCell(x + 1, y);
                    TerrainCell c = world.GetCell(x, y + 1);
                    TerrainCell d = world.GetCell(x + 1, y + 1);

                    AddVertex(surfaceTool, world, a, overlayMode);
                    AddVertex(surfaceTool, world, b, overlayMode);
                    AddVertex(surfaceTool, world, c, overlayMode);

                    AddVertex(surfaceTool, world, c, overlayMode);
                    AddVertex(surfaceTool, world, b, overlayMode);
                    AddVertex(surfaceTool, world, d, overlayMode);
                }
            }

            ArrayMesh mesh = surfaceTool.Commit();
            MeshInstance3D meshInstance = new()
            {
                Name = "Ground",
                Mesh = mesh,
                MaterialOverride = new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true,
                    Roughness = 1.0f
                }
            };
            AddChild(meshInstance);

            HeightMapShape3D heightMapShape = new()
            {
                MapWidth = world.GridWidth,
                MapDepth = world.GridHeight,
                MapData = BuildHeightMapData(world)
            };

            StaticBody3D collider = new()
            {
                Name = "GroundCollider",
                Position = new Vector3(world.OriginX, 0.0f, world.OriginZ),
                Scale = new Vector3(world.CellSizeMeters, 1.0f, world.CellSizeMeters)
            };
            CollisionShape3D collisionShape = new()
            {
                Shape = heightMapShape
            };
            collider.AddChild(collisionShape);
            AddChild(collider);
        }

        private void BuildLandmarks()
        {
            CreateBoundaryPost(new Vector3(-WorldHalfSize + 6.0f, 0.0f, -WorldHalfSize + 6.0f));
            CreateBoundaryPost(new Vector3(-WorldHalfSize + 6.0f, 0.0f, WorldHalfSize - 6.0f));
            CreateBoundaryPost(new Vector3(WorldHalfSize - 6.0f, 0.0f, -WorldHalfSize + 6.0f));
            CreateBoundaryPost(new Vector3(WorldHalfSize - 6.0f, 0.0f, WorldHalfSize - 6.0f));

            Vector3 spawnSurface = Vector3.Zero;
            float spawnHeight = SampleHeight(spawnSurface);
            MeshInstance3D spawnPad = new()
            {
                Name = "SpawnPad",
                Mesh = new CylinderMesh
                {
                    TopRadius = 3.6f,
                    BottomRadius = 3.6f,
                    Height = 0.4f
                },
                Position = new Vector3(0.0f, spawnHeight + 0.2f, 0.0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.72f, 0.69f, 0.52f)
                }
            };
            AddChild(spawnPad);
        }

        private void CreateBoundaryPost(Vector3 position)
        {
            float height = SampleHeight(position);
            MeshInstance3D marker = new()
            {
                Mesh = new BoxMesh
                {
                    Size = new Vector3(2.0f, 4.0f, 2.0f)
                },
                Position = new Vector3(position.X, height + 2.0f, position.Z),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.31f, 0.24f, 0.18f)
                }
            };
            AddChild(marker);
        }

        private static void AddVertex(SurfaceTool surfaceTool, WorldMapState world, TerrainCell cell, TerrainOverlayMode overlayMode)
        {
            surfaceTool.SetColor(GetCellColor(cell, overlayMode));
            surfaceTool.SetNormal(BuildNormal(world, cell.GridX, cell.GridY));
            surfaceTool.AddVertex(cell.WorldPosition);
        }

        private static Vector3 BuildNormal(WorldMapState world, int x, int y)
        {
            TerrainCell center = world.GetCell(x, y);
            TerrainCell left = world.TryGetCell(x - 1, y) ?? center;
            TerrainCell right = world.TryGetCell(x + 1, y) ?? center;
            TerrainCell up = world.TryGetCell(x, y - 1) ?? center;
            TerrainCell down = world.TryGetCell(x, y + 1) ?? center;

            Vector3 tangentX = new Vector3(world.CellSizeMeters * 2.0f, right.ElevationMeters - left.ElevationMeters, 0.0f);
            Vector3 tangentZ = new Vector3(0.0f, down.ElevationMeters - up.ElevationMeters, world.CellSizeMeters * 2.0f);
            return tangentZ.Cross(tangentX).Normalized();
        }

        private static float[] BuildHeightMapData(WorldMapState world)
        {
            float[] mapData = new float[world.GridWidth * world.GridHeight];
            for (int y = 0; y < world.GridHeight; y++)
            {
                for (int x = 0; x < world.GridWidth; x++)
                {
                    mapData[(y * world.GridWidth) + x] = world.GetCell(x, y).ElevationMeters;
                }
            }

            return mapData;
        }

        private static Color GetCellColor(TerrainCell cell, TerrainOverlayMode overlayMode)
        {
            return overlayMode switch
            {
                TerrainOverlayMode.Biome => GetBiomeOverlayColor(cell.Biome),
                TerrainOverlayMode.Buildability => cell.IsBuildable
                    ? new Color(0.34f, 0.72f, 0.33f)
                    : new Color(0.78f, 0.25f, 0.23f),
                TerrainOverlayMode.MovementCost => GetMovementOverlayColor(cell.MovementCost),
                _ => GetBiomeBaseColor(cell.Biome)
            };
        }

        private static Color GetBiomeBaseColor(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Forest => new Color(0.19f, 0.43f, 0.21f),
                BiomeType.RockyUpland => new Color(0.46f, 0.46f, 0.43f),
                BiomeType.Wetland => new Color(0.20f, 0.38f, 0.28f),
                _ => new Color(0.41f, 0.55f, 0.29f)
            };
        }

        private static Color GetBiomeOverlayColor(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Forest => new Color(0.14f, 0.55f, 0.18f),
                BiomeType.RockyUpland => new Color(0.62f, 0.60f, 0.55f),
                BiomeType.Wetland => new Color(0.17f, 0.56f, 0.46f),
                _ => new Color(0.71f, 0.78f, 0.33f)
            };
        }

        private static Color GetMovementOverlayColor(float movementCost)
        {
            float normalized = Mathf.Clamp((movementCost - 1.0f) / 1.2f, 0.0f, 1.0f);
            return new Color(
                Mathf.Lerp(0.28f, 0.86f, normalized),
                Mathf.Lerp(0.72f, 0.32f, normalized),
                Mathf.Lerp(0.30f, 0.20f, normalized));
        }
    }
}
