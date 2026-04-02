using Godot;

namespace Societies.Core
{
    /// <summary>
    /// Generates a lightweight prototype terrain for a 500x500 unit world.
    /// </summary>
    public partial class TerrainGenerator : Node3D
    {
        [Export] public float WorldSize { get; set; } = 500.0f;
        [Export] public float GroundHeight { get; set; } = 0.0f;

        public float WorldHalfSize => WorldSize * 0.5f;

        public override void _Ready()
        {
            if (GetChildCount() == 0)
            {
                BuildGround();
                BuildLandmarks();
            }
        }

        public Vector3 GetPlayerSpawnPoint()
        {
            return new Vector3(0.0f, GroundHeight + 2.0f, 0.0f);
        }

        public Vector3 GetRandomResourcePoint(RandomNumberGenerator rng)
        {
            float safeMargin = 24.0f;
            float minDistanceFromSpawn = 18.0f;

            while (true)
            {
                float x = rng.RandfRange(-WorldHalfSize + safeMargin, WorldHalfSize - safeMargin);
                float z = rng.RandfRange(-WorldHalfSize + safeMargin, WorldHalfSize - safeMargin);
                Vector2 flat = new Vector2(x, z);

                if (flat.Length() >= minDistanceFromSpawn)
                {
                    return new Vector3(x, GroundHeight, z);
                }
            }
        }

        private void BuildGround()
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

        private void BuildLandmarks()
        {
            CreateBoundaryPost(new Vector3(-WorldHalfSize + 6.0f, 2.0f, -WorldHalfSize + 6.0f));
            CreateBoundaryPost(new Vector3(-WorldHalfSize + 6.0f, 2.0f, WorldHalfSize - 6.0f));
            CreateBoundaryPost(new Vector3(WorldHalfSize - 6.0f, 2.0f, -WorldHalfSize + 6.0f));
            CreateBoundaryPost(new Vector3(WorldHalfSize - 6.0f, 2.0f, WorldHalfSize - 6.0f));

            MeshInstance3D spawnPad = new()
            {
                Name = "SpawnPad",
                Mesh = new CylinderMesh
                {
                    TopRadius = 3.6f,
                    BottomRadius = 3.6f,
                    Height = 0.4f
                },
                Position = new Vector3(0.0f, 0.2f, 0.0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.72f, 0.69f, 0.52f)
                }
            };
            AddChild(spawnPad);
        }

        private void CreateBoundaryPost(Vector3 position)
        {
            MeshInstance3D marker = new()
            {
                Mesh = new BoxMesh
                {
                    Size = new Vector3(2.0f, 4.0f, 2.0f)
                },
                Position = position,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.31f, 0.24f, 0.18f)
                }
            };
            AddChild(marker);
        }
    }
}
