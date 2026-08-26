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
        [Export] public float ContributionRangeMeters { get; set; } = 4.5f;

        public TerrainGenerator? Terrain { get; set; }
        public bool ControlsEnabled => _controlsEnabled;
        public bool IsDepotFocused => _isDepotFocused;
        public Vector3 ContributionDepotPosition { get; set; }

        public event Action<string, int>? HarvestRequested;
        public event Action<Vector3, ulong>? ContributionRequested;
        public event Action<bool>? DepotFocusChanged;

        private Node3D? _cameraPivot;
        private Camera3D? _camera;
        private RayCast3D? _interactionRay;
        private ResourceNode? _focusedResource;
        private bool _isDepotFocused;
        private bool _controlsEnabled = true;

        public override void _Ready()
        {
            BuildVisuals();
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }

        public override void _Input(InputEvent @event)
        {
            if (!_controlsEnabled)
            {
                return;
            }

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
            if (!_controlsEnabled)
            {
                return;
            }

            HandleMovement((float)delta);
            UpdateInteractionTarget();

            if (Input.IsActionJustPressed("interact"))
            {
                ProcessInteractionInput(Engine.GetPhysicsFrames());
            }
        }

        public string GetInteractionText()
        {
            ResourceNode? focusedResource = GetValidFocusedResource();
            if (focusedResource == null)
            {
                return _isDepotFocused
                    ? "Central depot focused — Press E to contribute raw resources"
                    : "Look at a resource node and press E";
            }

            return $"Press E to harvest {focusedResource.DisplayName} ({focusedResource.UnitsRemaining} left)";
        }

        public void ResetForPrototypeRun(Vector3 position)
        {
            Velocity = Vector3.Zero;
            Position = position;
            Rotation = Vector3.Zero;
            ResourceNode? focusedResource = GetValidFocusedResource();
            focusedResource?.SetFocused(false);
            _focusedResource = null;
            SetDepotFocus(false);

            if (_cameraPivot != null)
            {
                _cameraPivot.Rotation = Vector3.Zero;
            }
        }

        public void SetControlEnabled(bool enabled)
        {
            _controlsEnabled = enabled;

            if (_camera != null)
            {
                _camera.Current = enabled;
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
            ResourceNode? previousFocusedResource = GetValidFocusedResource();
            _focusedResource = null;

            if (_interactionRay == null)
            {
                previousFocusedResource?.SetFocused(false);
                SetDepotFocus(IsWithinContributionRange());
                return;
            }

            _interactionRay.ForceRaycastUpdate();
            if (!_interactionRay.IsColliding())
            {
                previousFocusedResource?.SetFocused(false);
                SetDepotFocus(IsWithinContributionRange());
                return;
            }

            GodotObject collider = _interactionRay.GetCollider();
            if (collider is ResourceNode resource)
            {
                _focusedResource = resource;
            }
            else if (collider is Node colliderNode)
            {
                _focusedResource = colliderNode.GetParent() as ResourceNode;
            }

            if (previousFocusedResource != null && previousFocusedResource != _focusedResource &&
                GodotObject.IsInstanceValid(previousFocusedResource))
            {
                previousFocusedResource.SetFocused(false);
            }
            ResourceNode? focusedResource = GetValidFocusedResource();
            focusedResource?.SetFocused(true);
            SetDepotFocus(_focusedResource == null && IsWithinContributionRange());
        }

        private void TryHarvest()
        {
            ResourceNode? focusedResource = GetValidFocusedResource();
            if (focusedResource == null)
            {
                return;
            }

            string siteId = focusedResource.SiteId;
            HarvestRequested?.Invoke(siteId, 1);
            _ = GetValidFocusedResource();
        }

        public bool ApplyCaptureCameraPose(Vector3 cameraPosition, Vector3 lookAt, float fieldOfView)
        {
            if (_camera == null)
            {
                return false;
            }

            _camera.GlobalPosition = cameraPosition;
            _camera.LookAt(lookAt, Vector3.Up);
            _camera.Fov = fieldOfView;
            return true;
        }

        public void ProcessInteractionInput(ulong inputFrame)
        {
            if (GetValidFocusedResource() != null)
            {
                TryHarvest();
                return;
            }

            if (IsWithinContributionRange())
            {
                ContributionRequested?.Invoke(GlobalPosition, inputFrame);
            }
        }

        private ResourceNode? GetValidFocusedResource()
        {
            if (_focusedResource != null && !GodotObject.IsInstanceValid(_focusedResource))
            {
                _focusedResource = null;
            }

            return _focusedResource;
        }

        private bool IsWithinContributionRange()
        {
            return GlobalPosition.DistanceTo(ContributionDepotPosition) <= ContributionRangeMeters;
        }

        private void SetDepotFocus(bool focused)
        {
            if (_isDepotFocused == focused)
            {
                return;
            }

            _isDepotFocused = focused;
            DepotFocusChanged?.Invoke(focused);
        }

        private void ClampToWorld()
        {
            if (Terrain == null)
            {
                return;
            }

            Vector3 position = GlobalPosition;
            float limit = Terrain.WorldHalfSize - 1.0f;
            float terrainHeight = Terrain.SampleHeight(position);

            position.X = Mathf.Clamp(position.X, -limit, limit);
            position.Z = Mathf.Clamp(position.Z, -limit, limit);
            if (position.Y < terrainHeight + 0.95f)
            {
                position.Y = terrainHeight + 0.95f;
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
