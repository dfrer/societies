using Godot;

namespace Societies.Core
{
    /// <summary>
    /// Read-only free camera for observing prototype settlement behavior.
    /// </summary>
    public partial class ObserverCameraRig : Node3D
    {
        [Export] public float BaseMoveSpeed { get; set; } = 28.0f;
        [Export] public float VerticalMoveSpeed { get; set; } = 18.0f;
        [Export] public float MouseSensitivity { get; set; } = 0.0022f;
        [Export] public float SpeedMultiplierStep { get; set; } = 0.25f;

        public bool ControlsEnabled => _controlsEnabled;

        private Camera3D? _camera;
        private float _speedMultiplier = 1.0f;
        private bool _controlsEnabled;
        private float _pitch;

        public override void _Ready()
        {
            BuildVisuals();
            SetControlEnabled(false);
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
                _pitch = Mathf.Clamp(_pitch - (motion.Relative.Y * MouseSensitivity), -1.35f, 1.35f);
                Rotation = new Vector3(_pitch, Rotation.Y, Rotation.Z);
            }

            if (@event.IsActionPressed("escape"))
            {
                Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                    ? Input.MouseModeEnum.Visible
                    : Input.MouseModeEnum.Captured;
            }

            if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed)
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    _speedMultiplier = Mathf.Clamp(_speedMultiplier + SpeedMultiplierStep, 0.5f, 4.0f);
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    _speedMultiplier = Mathf.Clamp(_speedMultiplier - SpeedMultiplierStep, 0.5f, 4.0f);
                }
            }
        }

        public override void _Process(double delta)
        {
            if (!_controlsEnabled)
            {
                return;
            }

            Vector2 horizontalInput = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
            Vector3 direction = (-GlobalTransform.Basis.Z * horizontalInput.Y) + (GlobalTransform.Basis.X * horizontalInput.X);
            direction.Y = 0.0f;
            direction = direction.Normalized();

            float vertical = 0.0f;
            if (Input.IsActionPressed("jump"))
            {
                vertical += 1.0f;
            }

            if (Input.IsKeyPressed(Key.Ctrl))
            {
                vertical -= 1.0f;
            }

            float horizontalSpeed = BaseMoveSpeed * _speedMultiplier * (float)delta;
            float verticalSpeed = VerticalMoveSpeed * _speedMultiplier * (float)delta;

            GlobalPosition += (direction * horizontalSpeed) + (Vector3.Up * vertical * verticalSpeed);
        }

        public void FocusOn(Vector3 focusPosition)
        {
            GlobalPosition = focusPosition + new Vector3(-18.0f, 22.0f, 18.0f);
            LookAt(focusPosition + new Vector3(0.0f, 3.5f, 0.0f), Vector3.Up);
            _pitch = Rotation.X;
        }

        public void SetControlEnabled(bool enabled)
        {
            _controlsEnabled = enabled;

            if (_camera != null)
            {
                _camera.Current = enabled;
            }

            if (enabled && Input.MouseMode == Input.MouseModeEnum.Visible)
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }

        private void BuildVisuals()
        {
            _camera ??= GetNodeOrNull<Camera3D>("Camera3D");
            if (_camera != null)
            {
                return;
            }

            _camera = new Camera3D
            {
                Name = "Camera3D",
                Current = false,
                Fov = 75.0f
            };
            AddChild(_camera);
        }
    }
}
