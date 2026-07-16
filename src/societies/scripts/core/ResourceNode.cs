using Godot;

namespace Societies.Core
{
    /// <summary>
    /// Harvestable world node for settlement-economy resources.
    /// </summary>
    public partial class ResourceNode : Entity
    {
        [Export] public string ResourceId { get; set; } = "logs";
        [Export] public string SiteId { get; set; } = string.Empty;
        [Export] public int UnitsRemaining { get; set; } = 5;
        [Export] public string ClusterId { get; set; } = string.Empty;

        private Node3D? _visualRoot;
        private Label3D? _stateLabel;

        public override void _Ready()
        {
            EntityType = "resource";
            DisplayName = ResourceId switch
            {
                "wood" => "Tree",
                "logs" => "Tree",
                "stone" => "Rock",
                "berry" => "Berry Bush",
                "berries" => "Berry Bush",
                "clay" => "Clay Deposit",
                "reeds" => "Reed Bed",
                _ => "Resource"
            };

            BuildNode();
            base._Ready();
        }

        public void ApplyProjection(int unitsRemaining)
        {
            UnitsRemaining = Mathf.Max(0, unitsRemaining);
            UpdateVisualState();
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
                case "logs":
                    CreateTreeVisual();
                    break;
                case "stone":
                    CreateRockVisual();
                    break;
                case "berry":
                case "berries":
                    CreateBerryVisual();
                    break;
                case "clay":
                    CreateClayVisual();
                    break;
                case "reeds":
                    CreateReedVisual();
                    break;
            }

            _stateLabel = new Label3D
            {
                Name = "ResourceLabel",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0.0f, 3.55f, 0.0f),
                FontSize = 14,
                Modulate = new Color(0.95f, 0.94f, 0.84f)
            };
            AddChild(_stateLabel);
            UpdateVisualState();
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

        private void CreateClayVisual()
        {
            MeshInstance3D pit = CreateMesh(
                new BoxMesh { Size = new Vector3(1.6f, 0.45f, 1.4f) },
                new Color(0.58f, 0.42f, 0.28f),
                new Vector3(0.0f, 0.22f, 0.0f)
            );
            _visualRoot?.AddChild(pit);
        }

        private void CreateReedVisual()
        {
            for (int i = 0; i < 4; i++)
            {
                float x = -0.35f + (i * 0.22f);
                MeshInstance3D stalk = CreateMesh(
                    new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.06f, Height = 1.4f },
                    new Color(0.62f, 0.71f, 0.34f),
                    new Vector3(x, 0.7f, 0.0f)
                );
                _visualRoot?.AddChild(stalk);
            }
        }

        private void UpdateVisualState()
        {
            if (_visualRoot == null)
            {
                return;
            }

            float normalized = Mathf.Clamp(UnitsRemaining / 6.0f, 0.2f, 1.0f);
            _visualRoot.Scale = new Vector3(1.0f, normalized, 1.0f);
            if (_stateLabel != null)
            {
                _stateLabel.Text = $"{DisplayName}\n{UnitsRemaining} available";
            }
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
