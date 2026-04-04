using Godot;
using Societies.Core;

namespace Societies.Simulation
{
    /// <summary>
    /// World-space visual for prototype workers with readable movement, intent, and carry state.
    /// </summary>
    public partial class PrototypeWorkerAgent : Node3D
    {
        private Node3D? _actorRoot;
        private Node3D? _beaconRoot;
        private MeshInstance3D? _body;
        private MeshInstance3D? _head;
        private MeshInstance3D? _carryMesh;
        private MeshInstance3D? _shadow;
        private MeshInstance3D? _beaconMesh;
        private Label3D? _label;
        private Label3D? _beaconLabel;
        private Vector3 _visualTargetPosition;
        private Vector3 _desiredPosition;
        private PrototypeWorkerPhase _phase;
        private float _animationTime;
        private bool _isMoving;

        public string LabelText => _label?.Text ?? string.Empty;

        public override void _Ready()
        {
            BuildVisuals();
            _desiredPosition = GlobalPosition;
            _visualTargetPosition = GlobalPosition;
        }

        public override void _Process(double delta)
        {
            _animationTime += (float)delta;

            if (GlobalPosition.DistanceTo(_desiredPosition) > 6.0f)
            {
                GlobalPosition = _desiredPosition;
            }
            else
            {
                float smoothFactor = 1.0f - Mathf.Exp(-(float)delta * 10.0f);
                GlobalPosition = GlobalPosition.Lerp(_desiredPosition, smoothFactor);
            }

            UpdateFacing();
            UpdateAnimation();
            UpdateBeacon();
        }

        public void ApplyState(PrototypeWorkerState state)
        {
            _desiredPosition = state.Position;
            _visualTargetPosition = state.TargetPosition;
            _phase = state.Phase;
            _isMoving = state.Phase == PrototypeWorkerPhase.MovingToResource ||
                        state.Phase == PrototypeWorkerPhase.MovingToStockpile ||
                        state.Phase == PrototypeWorkerPhase.MovingToWorkstation;

            if (_label != null)
            {
                string carryText = state.CarryAmount > 0
                    ? $"{InventoryComponent.FormatItemName(state.CarryItemId)} x{state.CarryAmount}"
                    : "empty hands";
                string targetText = string.IsNullOrWhiteSpace(state.TargetLabel)
                    ? string.Empty
                    : $" -> {state.TargetLabel}";
                string progressText = state.Phase == PrototypeWorkerPhase.Idle
                    ? string.Empty
                    : $" ({state.ProgressPercent} %)";
                _label.Text = $"{state.DisplayName}\n{state.ActivityText}{progressText}{targetText}\n{carryText}";
            }

            if (_beaconLabel != null)
            {
                _beaconLabel.Text = state.TargetLabel;
            }

            if (_body?.MaterialOverride is StandardMaterial3D bodyMaterial)
            {
                bodyMaterial.AlbedoColor = GetPhaseColor(state.Phase);
            }

            if (_head?.MaterialOverride is StandardMaterial3D headMaterial)
            {
                headMaterial.AlbedoColor = GetPhaseColor(state.Phase).Lightened(0.18f);
            }

            if (_carryMesh != null)
            {
                _carryMesh.Visible = state.CarryAmount > 0;
                if (_carryMesh.MaterialOverride is StandardMaterial3D carryMaterial)
                {
                    carryMaterial.AlbedoColor = GetCarryColor(state.CarryItemId);
                }
            }

            if (_beaconMesh?.MaterialOverride is StandardMaterial3D beaconMaterial)
            {
                Color color = GetPhaseColor(state.Phase);
                beaconMaterial.AlbedoColor = color;
                beaconMaterial.EmissionEnabled = true;
                beaconMaterial.Emission = color;
                beaconMaterial.EmissionEnergyMultiplier = 0.55f;
            }

            if (GlobalPosition == Vector3.Zero)
            {
                GlobalPosition = _desiredPosition;
            }

            UpdateBeacon();
        }

        private void BuildVisuals()
        {
            _actorRoot = new Node3D { Name = "ActorRoot" };
            AddChild(_actorRoot);

            _shadow = new MeshInstance3D
            {
                Name = "Shadow",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.42f,
                    BottomRadius = 0.42f,
                    Height = 0.03f
                },
                Position = new Vector3(0.0f, 0.04f, 0.0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.09f, 0.09f, 0.09f, 0.65f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                }
            };
            AddChild(_shadow);

            _body = new MeshInstance3D
            {
                Name = "Body",
                Mesh = new CapsuleMesh
                {
                    Radius = 0.3f,
                    Height = 1.2f
                },
                Position = new Vector3(0.0f, 0.92f, 0.0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.62f, 0.49f, 0.79f)
                }
            };
            _actorRoot.AddChild(_body);

            _head = new MeshInstance3D
            {
                Name = "Head",
                Mesh = new SphereMesh
                {
                    Radius = 0.22f,
                    Height = 0.44f
                },
                Position = new Vector3(0.0f, 1.82f, 0.0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.76f, 0.67f, 0.56f)
                }
            };
            _actorRoot.AddChild(_head);

            _carryMesh = new MeshInstance3D
            {
                Name = "Carry",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(0.28f, 0.28f, 0.28f)
                },
                Position = new Vector3(0.38f, 1.08f, 0.0f),
                Visible = false,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.54f, 0.41f, 0.2f)
                }
            };
            _actorRoot.AddChild(_carryMesh);

            _label = new Label3D
            {
                Name = "Label",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0.0f, 2.45f, 0.0f),
                FontSize = 20,
                Modulate = new Color(0.94f, 0.95f, 0.88f)
            };
            AddChild(_label);

            _beaconRoot = new Node3D { Name = "BeaconRoot" };
            AddChild(_beaconRoot);

            _beaconMesh = new MeshInstance3D
            {
                Name = "Beacon",
                Mesh = new CylinderMesh
                {
                    TopRadius = 0.24f,
                    BottomRadius = 0.24f,
                    Height = 0.08f
                },
                Position = new Vector3(0.0f, 0.05f, 0.0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.35f, 0.61f, 0.83f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                }
            };
            _beaconRoot.AddChild(_beaconMesh);

            _beaconLabel = new Label3D
            {
                Name = "BeaconLabel",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0.0f, 0.45f, 0.0f),
                FontSize = 16,
                Modulate = new Color(0.89f, 0.94f, 0.97f)
            };
            _beaconRoot.AddChild(_beaconLabel);
        }

        private void UpdateFacing()
        {
            if (_actorRoot == null)
            {
                return;
            }

            Vector3 facingTarget = _isMoving ? _visualTargetPosition : _desiredPosition;
            Vector3 flatDirection = new(
                facingTarget.X - GlobalPosition.X,
                0.0f,
                facingTarget.Z - GlobalPosition.Z);

            if (flatDirection.LengthSquared() < 0.04f)
            {
                return;
            }

            _actorRoot.LookAt(GlobalPosition + flatDirection.Normalized(), Vector3.Up, true);
        }

        private void UpdateAnimation()
        {
            if (_actorRoot == null || _shadow == null)
            {
                return;
            }

            float bob = 0.0f;
            if (_isMoving)
            {
                bob = Mathf.Sin(_animationTime * 10.0f) * 0.08f;
            }
            else if (_phase == PrototypeWorkerPhase.Harvesting || _phase == PrototypeWorkerPhase.Crafting)
            {
                bob = Mathf.Sin(_animationTime * 7.0f) * 0.04f;
            }

            _actorRoot.Position = new Vector3(0.0f, bob, 0.0f);
            _shadow.Scale = _isMoving
                ? new Vector3(0.92f, 1.0f, 0.92f)
                : new Vector3(1.0f, 1.0f, 1.0f);
        }

        private void UpdateBeacon()
        {
            if (_beaconRoot == null)
            {
                return;
            }

            Vector3 toTarget = _visualTargetPosition - GlobalPosition;
            bool showBeacon = toTarget.LengthSquared() > 0.36f && !string.IsNullOrWhiteSpace(_beaconLabel?.Text);
            _beaconRoot.Visible = showBeacon;
            if (!showBeacon)
            {
                return;
            }

            _beaconRoot.Position = new Vector3(toTarget.X, 0.0f, toTarget.Z);
        }

        private static Color GetPhaseColor(PrototypeWorkerPhase phase)
        {
            return phase switch
            {
                PrototypeWorkerPhase.MovingToResource => new Color(0.35f, 0.61f, 0.83f),
                PrototypeWorkerPhase.Harvesting => new Color(0.47f, 0.73f, 0.35f),
                PrototypeWorkerPhase.MovingToStockpile => new Color(0.86f, 0.63f, 0.28f),
                PrototypeWorkerPhase.Depositing => new Color(0.93f, 0.79f, 0.35f),
                PrototypeWorkerPhase.MovingToWorkstation => new Color(0.82f, 0.48f, 0.29f),
                PrototypeWorkerPhase.Crafting => new Color(0.91f, 0.43f, 0.22f),
                _ => new Color(0.62f, 0.49f, 0.79f)
            };
        }

        private static Color GetCarryColor(string itemId)
        {
            return itemId switch
            {
                "wood" => new Color(0.54f, 0.38f, 0.2f),
                "stone" => new Color(0.55f, 0.58f, 0.61f),
                "berry" => new Color(0.74f, 0.21f, 0.31f),
                _ => new Color(0.78f, 0.74f, 0.58f)
            };
        }
    }
}
