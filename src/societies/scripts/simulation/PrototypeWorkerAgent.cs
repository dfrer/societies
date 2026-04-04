using Godot;

namespace Societies.Simulation
{
    /// <summary>
    /// Lightweight world-space visual for prototype workers.
    /// </summary>
    public partial class PrototypeWorkerAgent : Node3D
    {
        private MeshInstance3D? _body;
        private Label3D? _label;

        public override void _Ready()
        {
            BuildVisuals();
        }

        public void ApplyState(PrototypeWorkerState state)
        {
            Position = state.Position + new Vector3(0.0f, 0.95f, 0.0f);

            if (_label != null)
            {
                string carryText = state.CarryAmount > 0
                    ? $"{state.CarryItemId} x{state.CarryAmount}"
                    : "empty";
                _label.Text = $"{state.DisplayName}\n{state.Phase}\n{carryText}";
            }

            if (_body?.MaterialOverride is StandardMaterial3D material)
            {
                material.AlbedoColor = state.Phase switch
                {
                    PrototypeWorkerPhase.Harvesting => new Color(0.47f, 0.73f, 0.35f),
                    PrototypeWorkerPhase.Crafting => new Color(0.86f, 0.63f, 0.28f),
                    PrototypeWorkerPhase.Depositing => new Color(0.35f, 0.61f, 0.83f),
                    _ => new Color(0.62f, 0.49f, 0.79f)
                };
            }
        }

        private void BuildVisuals()
        {
            _body = new MeshInstance3D
            {
                Name = "Body",
                Mesh = new CapsuleMesh
                {
                    Radius = 0.32f,
                    Height = 1.54f
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.62f, 0.49f, 0.79f)
                }
            };
            AddChild(_body);

            _label = new Label3D
            {
                Name = "Label",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0.0f, 1.5f, 0.0f),
                FontSize = 22,
                Modulate = new Color(0.94f, 0.95f, 0.88f)
            };
            AddChild(_label);
        }
    }
}
