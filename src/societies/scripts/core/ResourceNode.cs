using Godot;

namespace Societies.Core
{
    /// <summary>
    /// Harvestable world node for trees, rocks, and berry bushes.
    /// </summary>
    public partial class ResourceNode : Entity
    {
        [Export] public string ResourceId { get; set; } = "wood";
        [Export] public int UnitsRemaining { get; set; } = 5;

        private Node3D? _visualRoot;

        public override void _Ready()
        {
            EntityType = "resource";
            DisplayName = ResourceId switch
            {
                "wood" => "Tree",
                "stone" => "Rock",
                "berry" => "Berry Bush",
                _ => "Resource"
            };

            BuildNode();
            base._Ready();
        }

        public bool TryHarvest(int amount, out string itemId, out int harvestedAmount)
        {
            itemId = ResourceId;
            harvestedAmount = 0;

            if (UnitsRemaining <= 0 || amount <= 0)
            {
                return false;
            }

            harvestedAmount = Mathf.Min(amount, UnitsRemaining);
            UnitsRemaining -= harvestedAmount;
            UpdateVisualState();

            if (UnitsRemaining <= 0)
            {
                QueueFree();
            }

            return true;
        }

        private void BuildNode()
        {
            _visualRoot = new Node3D { Name = "VisualRoot" };
            AddChild(_visualRoot);

            StaticBody3D hitbox = new()
            {
                Name = "Hitbox"
            };
            CollisionShape3D collision = new()
            {
                Shape = new CylinderShape3D
                {
                    Radius = ResourceId == "stone" ? 1.2f : 0.9f,
                    Height = ResourceId == "wood" ? 4.0f : 2.2f
                },
                Position = new Vector3(0.0f, ResourceId == "wood" ? 2.0f : 1.1f, 0.0f)
            };
            hitbox.AddChild(collision);
            AddChild(hitbox);

            switch (ResourceId)
            {
                case "wood":
                    CreateTreeVisual();
                    break;
                case "stone":
                    CreateRockVisual();
                    break;
                case "berry":
                    CreateBerryVisual();
                    break;
            }
        }

        private void CreateTreeVisual()
        {
            MeshInstance3D trunk = CreateMesh(
                new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.28f, Height = 2.2f },
                new Color(0.42f, 0.28f, 0.16f),
                new Vector3(0.0f, 1.1f, 0.0f)
            );
            _visualRoot?.AddChild(trunk);

            MeshInstance3D canopy = CreateMesh(
                new BoxMesh { Size = new Vector3(1.8f, 1.4f, 1.8f) },
                new Color(0.18f, 0.48f, 0.22f),
                new Vector3(0.0f, 2.7f, 0.0f)
            );
            _visualRoot?.AddChild(canopy);
        }

        private void CreateRockVisual()
        {
            MeshInstance3D rock = CreateMesh(
                new BoxMesh { Size = new Vector3(1.9f, 1.2f, 1.6f) },
                new Color(0.48f, 0.5f, 0.53f),
                new Vector3(0.0f, 0.6f, 0.0f)
            );
            rock.Rotation = new Vector3(0.2f, 0.35f, 0.1f);
            _visualRoot?.AddChild(rock);
        }

        private void CreateBerryVisual()
        {
            MeshInstance3D stem = CreateMesh(
                new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.12f, Height = 1.1f },
                new Color(0.35f, 0.24f, 0.15f),
                new Vector3(0.0f, 0.55f, 0.0f)
            );
            _visualRoot?.AddChild(stem);

            MeshInstance3D bush = CreateMesh(
                new BoxMesh { Size = new Vector3(1.3f, 1.0f, 1.3f) },
                new Color(0.26f, 0.54f, 0.22f),
                new Vector3(0.0f, 1.35f, 0.0f)
            );
            _visualRoot?.AddChild(bush);

            MeshInstance3D berries = CreateMesh(
                new BoxMesh { Size = new Vector3(0.28f, 0.28f, 0.28f) },
                new Color(0.69f, 0.16f, 0.24f),
                new Vector3(0.38f, 1.15f, 0.08f)
            );
            _visualRoot?.AddChild(berries);
        }

        private void UpdateVisualState()
        {
            if (_visualRoot == null)
            {
                return;
            }

            float normalized = Mathf.Clamp(UnitsRemaining / 6.0f, 0.2f, 1.0f);
            _visualRoot.Scale = new Vector3(1.0f, normalized, 1.0f);
        }

        private static MeshInstance3D CreateMesh(PrimitiveMesh mesh, Color color, Vector3 position)
        {
            return new MeshInstance3D
            {
                Mesh = mesh,
                Position = position,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = color
                }
            };
        }
    }
}
