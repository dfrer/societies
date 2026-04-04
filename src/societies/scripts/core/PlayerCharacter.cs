using Godot;
using System;

namespace Societies.Core
{
    /// <summary>
    /// First-person controller for the Prototype 1 player.
    /// </summary>
    public partial class PlayerCharacter : CharacterBody3D
    {
        [Export] public float WalkSpeed { get; set; } = 6.5f;
        [Export] public float SprintSpeed { get; set; } = 10.5f;
        [Export] public float JumpVelocity { get; set; } = 5.5f;
        [Export] public float Gravity { get; set; } = 18.0f;
        [Export] public float MouseSensitivity { get; set; } = 0.0025f;

        public InventoryComponent? Inventory { get; set; }
        public TerrainGenerator? Terrain { get; set; }

        public event Action<string, int>? Harvested;

        private Node3D? _cameraPivot;
        private Camera3D? _camera;
        private RayCast3D? _interactionRay;
        private ResourceNode? _focusedResource;

        public override void _Ready()
        {
            BuildVisuals();
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                RotateY(-motion.Relative.X * MouseSensitivity);

                if (_cameraPivot != null)
                {
                    Vector3 rotation = _cameraPivot.Rotation;
                    rotation.X = Mathf.Clamp(rotation.X - motion.Relative.Y * MouseSensitivity, -1.25f, 1.25f);
                    _cameraPivot.Rotation = rotation;
                }
            }

            if (@event.IsActionPressed("escape"))
            {
                Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                    ? Input.MouseModeEnum.Visible
                    : Input.MouseModeEnum.Captured;
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            HandleMovement((float)delta);
            UpdateInteractionTarget();

            if (Input.IsActionJustPressed("interact"))
            {
                TryHarvest();
            }
        }

        public string GetInteractionText()
        {
            if (_focusedResource == null)
            {
                return "Look at a tree, rock, or berry bush and press E";
            }

            return $"Press E to harvest {_focusedResource.DisplayName} ({_focusedResource.UnitsRemaining} left)";
        }

        public void ResetForPrototypeRun(Vector3 position)
        {
            Velocity = Vector3.Zero;
            Position = position;
            Rotation = Vector3.Zero;
            _focusedResource = null;

            if (_cameraPivot != null)
            {
                _cameraPivot.Rotation = Vector3.Zero;
            }
        }

        private void HandleMovement(float delta)
        {
            Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
            Vector3 forward = -Transform.Basis.Z;
            Vector3 right = Transform.Basis.X;
            Vector3 direction = (right * input.X) + (forward * input.Y);
            direction.Y = 0.0f;
            direction = direction.Normalized();

            float speed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;
            Vector3 velocity = Velocity;

            if (!IsOnFloor())
            {
                velocity.Y -= Gravity * delta;
            }
            else if (velocity.Y < 0.0f)
            {
                velocity.Y = 0.0f;
            }

            if (Input.IsActionJustPressed("jump") && IsOnFloor())
            {
                velocity.Y = JumpVelocity;
            }

            if (direction != Vector3.Zero)
            {
                velocity.X = direction.X * speed;
                velocity.Z = direction.Z * speed;
            }
            else
            {
                velocity.X = Mathf.MoveToward(velocity.X, 0.0f, speed);
                velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, speed);
            }

            Velocity = velocity;
            MoveAndSlide();
            ClampToWorld();
        }

        private void UpdateInteractionTarget()
        {
            _focusedResource = null;

            if (_interactionRay == null)
            {
                return;
            }

            _interactionRay.ForceRaycastUpdate();
            if (!_interactionRay.IsColliding())
            {
                return;
            }

            GodotObject collider = _interactionRay.GetCollider();
            if (collider is ResourceNode resource)
            {
                _focusedResource = resource;
                return;
            }

            if (collider is Node colliderNode)
            {
                _focusedResource = colliderNode.GetParent() as ResourceNode;
            }
        }

        private void TryHarvest()
        {
            if (_focusedResource == null || Inventory == null)
            {
                return;
            }

            if (_focusedResource.TryHarvest(1, out string itemId, out int amount))
            {
                Inventory.AddItem(itemId, amount);
                Harvested?.Invoke(itemId, amount);
            }
        }

        private void ClampToWorld()
        {
            if (Terrain == null)
            {
                return;
            }

            Vector3 position = GlobalPosition;
            float limit = Terrain.WorldHalfSize - 1.0f;

            position.X = Mathf.Clamp(position.X, -limit, limit);
            position.Z = Mathf.Clamp(position.Z, -limit, limit);
            if (position.Y < Terrain.GroundHeight + 0.95f)
            {
                position.Y = Terrain.GroundHeight + 0.95f;
            }

            GlobalPosition = position;
        }

        private void BuildVisuals()
        {
            CollisionShape3D collision = new()
            {
                Name = "Collision"
            };
            collision.Shape = new CapsuleShape3D
            {
                Radius = 0.4f,
                Height = 1.1f
            };
            AddChild(collision);

            MeshInstance3D body = new()
            {
                Name = "Body",
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            body.Mesh = new CylinderMesh
            {
                TopRadius = 0.35f,
                BottomRadius = 0.35f,
                Height = 1.1f
            };
            body.Position = new Vector3(0.0f, 0.95f, 0.0f);
            body.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.27f, 0.36f, 0.58f)
            };
            AddChild(body);

            _cameraPivot = new Node3D
            {
                Name = "CameraPivot",
                Position = new Vector3(0.0f, 1.6f, 0.0f)
            };
            AddChild(_cameraPivot);

            _camera = new Camera3D
            {
                Name = "Camera3D",
                Current = true,
                Fov = 75.0f
            };
            _cameraPivot.AddChild(_camera);

            _interactionRay = new RayCast3D
            {
                Name = "InteractionRay",
                TargetPosition = new Vector3(0.0f, 0.0f, -4.5f),
                Enabled = true
            };
            _camera.AddChild(_interactionRay);
        }
    }
}
