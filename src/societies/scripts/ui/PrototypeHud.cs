using Godot;

namespace Societies.UI
{
    /// <summary>
    /// Simple HUD for debug data, inventory, crafting, and interaction prompts.
    /// </summary>
    public partial class PrototypeHud : CanvasLayer
    {
        private Label? _debugLabel;
        private Label? _inventoryLabel;
        private Label? _craftingLabel;
        private Label? _interactionLabel;
        private Label? _statusLabel;
        private Label? _helpLabel;
        private Panel? _inventoryPanel;

        public override void _Ready()
        {
            BuildHud();
        }

        public void ToggleInventory()
        {
            if (_inventoryPanel != null)
            {
                _inventoryPanel.Visible = !_inventoryPanel.Visible;
            }
        }

        public void SetDebugText(string text)
        {
            if (_debugLabel != null)
            {
                _debugLabel.Text = text;
            }
        }

        public void SetInventoryText(string text)
        {
            if (_inventoryLabel != null)
            {
                _inventoryLabel.Text = text;
            }
        }

        public void SetCraftingText(string text)
        {
            if (_craftingLabel != null)
            {
                _craftingLabel.Text = text;
            }
        }

        public void SetInteractionText(string text)
        {
            if (_interactionLabel != null)
            {
                _interactionLabel.Text = text;
            }
        }

        public void SetStatusText(string text)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = text;
            }
        }

        public void SetHelpText(string text)
        {
            if (_helpLabel != null)
            {
                _helpLabel.Text = text;
            }
        }

        private void BuildHud()
        {
            Control root = new()
            {
                Name = "HudRoot",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(root);

            _debugLabel = CreateLabel(new Vector2(14.0f, 14.0f), new Vector2(340.0f, 140.0f), 18);
            root.AddChild(_debugLabel);

            _interactionLabel = CreateLabel(new Vector2(14.0f, 160.0f), new Vector2(520.0f, 40.0f), 18);
            root.AddChild(_interactionLabel);

            _statusLabel = CreateLabel(new Vector2(14.0f, 204.0f), new Vector2(520.0f, 32.0f), 18);
            root.AddChild(_statusLabel);

            _helpLabel = CreateLabel(new Vector2(14.0f, 850.0f), new Vector2(760.0f, 120.0f), 16);
            root.AddChild(_helpLabel);

            Label crosshair = CreateLabel(new Vector2(955.0f, 520.0f), new Vector2(24.0f, 24.0f), 24);
            crosshair.Text = "+";
            root.AddChild(crosshair);

            _inventoryPanel = new Panel
            {
                Name = "InventoryPanel",
                Position = new Vector2(1520.0f, 24.0f),
                Size = new Vector2(360.0f, 320.0f),
                Visible = true
            };
            root.AddChild(_inventoryPanel);

            _inventoryLabel = CreateLabel(new Vector2(12.0f, 12.0f), new Vector2(160.0f, 280.0f), 18);
            _inventoryPanel.AddChild(_inventoryLabel);

            _craftingLabel = CreateLabel(new Vector2(176.0f, 12.0f), new Vector2(172.0f, 280.0f), 17);
            _inventoryPanel.AddChild(_craftingLabel);
        }

        private static Label CreateLabel(Vector2 position, Vector2 size, int fontSize)
        {
            Label label = new()
            {
                Position = position,
                Size = size
            };
            label.AddThemeFontSizeOverride("font_size", fontSize);
            return label;
        }
    }
}
