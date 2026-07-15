using Godot;
using Societies.Simulation;
using System.Collections.Generic;

namespace Societies.UI
{
    /// <summary>
    /// Responsive normal-play HUD for crisis, settlement, inventory, interaction, and inspection state.
    /// </summary>
    public partial class PrototypeHud : CanvasLayer
    {
        private Label? _debugLabel;
        private Label? _inventoryLabel;
        private Label? _craftingLabel;
        private Label? _interactionLabel;
        private Label? _statusLabel;
        private Label? _helpLabel;
        private Label? _settlementLabel;
        private Label? _worldLabel;
        private Label? _inspectorLabel;
        private Label? _crisisLabel;
        private Label? _crosshairLabel;
        private Panel? _inventoryPanel;
        private Panel? _debugPanel;
        private Panel? _interactionPanel;
        private Panel? _statusPanel;
        private Panel? _helpPanel;
        private Panel? _settlementPanel;
        private Panel? _worldPanel;
        private Panel? _inspectorPanel;
        private Panel? _crisisPanel;
        private Control? _root;
        private string _statusText = string.Empty;
        private string _interactionText = string.Empty;
        private PrototypeSettlementDirective _directive = PrototypeSettlementDirective.Neutral;
        private PrototypeSettlementClassification _classification = PrototypeSettlementClassification.Strained;
        private PrototypeCrisisState? _crisis;
        private static readonly Dictionary<(PrototypeHudCue Cue, bool Emphasized), StyleBoxFlat> CardStyleCache = new();

        public string DebugText => _debugLabel?.Text ?? string.Empty;
        public string InventoryText => _inventoryLabel?.Text ?? string.Empty;
        public string CraftingText => _craftingLabel?.Text ?? string.Empty;
        public string StatusText => _statusLabel?.Text ?? string.Empty;
        public string HelpText => _helpLabel?.Text ?? string.Empty;
        public string SettlementText => _settlementLabel?.Text ?? string.Empty;
        public string WorldText => _worldLabel?.Text ?? string.Empty;
        public string InspectorText => _inspectorLabel?.Text ?? string.Empty;
        public string CrisisText => _crisisLabel?.Text ?? string.Empty;
        public bool IsInventoryVisible => _inventoryPanel?.Visible ?? false;
        public bool IsDebugVisible => _debugPanel?.Visible ?? false;
        public PrototypeHudLayout Layout { get; private set; } = PrototypeHudLayout.Calculate(1920.0f, 1080.0f);
        public IReadOnlyDictionary<string, PrototypeHudBounds> LayoutBounds => Layout.Bounds;
        public PrototypeHudPresentationState PresentationState { get; private set; }

        public override void _Ready()
        {
            BuildHud();
            GetViewport().SizeChanged += RefreshLayoutFromViewport;
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
            _interactionText = text;
            if (_interactionLabel != null)
            {
                _interactionLabel.Text = text;
            }

            ApplyPresentationState();
        }

        public void SetStatusText(string text)
        {
            _statusText = text;
            if (_statusLabel != null)
            {
                _statusLabel.Text = text;
            }

            ApplyPresentationState();
        }

        public void SetHelpText(string text)
        {
            if (_helpLabel != null)
            {
                _helpLabel.Text = text;
            }
        }

        public void SetSettlementText(string text)
        {
            if (_settlementLabel != null)
            {
                _settlementLabel.Text = text;
            }
        }

        public void SetWorldText(string text)
        {
            if (_worldLabel != null)
            {
                _worldLabel.Text = text;
            }
        }

        public void SetInspectorText(string text)
        {
            if (_inspectorLabel != null)
            {
                _inspectorLabel.Text = text;
            }
        }

        public void SetCrisisText(string text)
        {
            if (_crisisLabel != null)
            {
                _crisisLabel.Text = text;
            }
        }

        public void SetDebugVisible(bool visible)
        {
            if (_debugPanel != null)
            {
                _debugPanel.Visible = visible;
            }
        }

        public void SetPresentationState(
            PrototypeSettlementDirective directive,
            PrototypeSettlementClassification classification,
            PrototypeCrisisState? crisis)
        {
            _directive = directive;
            _classification = classification;
            _crisis = crisis;
            ApplyPresentationState();
        }

        /// <summary>Updates live control bounds from a viewport size; also exposed for headless assertions.</summary>
        public void ApplyResponsiveLayout(Vector2 viewportSize)
        {
            Layout = PrototypeHudLayout.Calculate(viewportSize.X, viewportSize.Y);
            ApplyBounds(_crisisPanel, PrototypeHudLayout.Crisis);
            ApplyBounds(_inspectorPanel, PrototypeHudLayout.Inspector);
            ApplyBounds(_worldPanel, PrototypeHudLayout.World);
            ApplyBounds(_inventoryPanel, PrototypeHudLayout.Inventory);
            ApplyBounds(_settlementPanel, PrototypeHudLayout.Settlement);
            ApplyBounds(_interactionPanel, PrototypeHudLayout.Interaction);
            ApplyBounds(_statusPanel, PrototypeHudLayout.Status);
            ApplyBounds(_helpPanel, PrototypeHudLayout.Help);
            ApplyBounds(_debugPanel, PrototypeHudLayout.Debug);
            ApplyBounds(_crosshairLabel, PrototypeHudLayout.Crosshair);
        }

        private void BuildHud()
        {
            Control root = new()
            {
                Name = "HudRoot",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f
            };
            AddChild(root);
            _root = root;
            root.Resized += RefreshLayoutFromViewport;

            _debugPanel = CreateCard("DebugPanel", 15, out _debugLabel);
            _debugPanel.Visible = false;
            root.AddChild(_debugPanel);
            _interactionPanel = CreateCard("InteractionPanel", 18, out _interactionLabel);
            root.AddChild(_interactionPanel);
            _statusPanel = CreateCard("StatusPanel", 18, out _statusLabel);
            root.AddChild(_statusPanel);
            _helpPanel = CreateCard("HelpPanel", 15, out _helpLabel);
            root.AddChild(_helpPanel);
            _crisisPanel = CreateCard("CrisisPanel", 16, out _crisisLabel);
            root.AddChild(_crisisPanel);
            _inspectorPanel = CreateCard("InspectorPanel", 16, out _inspectorLabel);
            root.AddChild(_inspectorPanel);
            _worldPanel = CreateCard("WorldPanel", 15, out _worldLabel);
            root.AddChild(_worldPanel);
            _settlementPanel = CreateCard("SettlementPanel", 15, out _settlementLabel);
            root.AddChild(_settlementPanel);

            _inventoryPanel = CreateCard("InventoryPanel", 16, out _inventoryLabel);
            root.AddChild(_inventoryPanel);
            _craftingLabel = CreateLabel(15);
            _craftingLabel.AnchorLeft = 0.5f;
            _craftingLabel.AnchorRight = 1.0f;
            _craftingLabel.OffsetLeft = 6.0f;
            _craftingLabel.OffsetTop = 12.0f;
            _craftingLabel.OffsetRight = -12.0f;
            _craftingLabel.OffsetBottom = -12.0f;
            _inventoryLabel.AnchorRight = 0.5f;
            _inventoryLabel.OffsetRight = -6.0f;
            _inventoryPanel.AddChild(_craftingLabel);

            _crosshairLabel = new Label
            {
                Name = "Crosshair",
                Text = "+",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _crosshairLabel.AddThemeFontSizeOverride("font_size", 24);
            root.AddChild(_crosshairLabel);

            ApplyResponsiveLayout(GetViewport().GetVisibleRect().Size);
            ApplyPresentationState();
        }

        private void RefreshLayoutFromViewport()
        {
            if (_root != null)
            {
                ApplyResponsiveLayout(GetViewport().GetVisibleRect().Size);
            }
        }

        private void ApplyPresentationState()
        {
            PresentationState = PrototypeHudPresentationState.Create(
                _directive,
                _classification,
                _crisis,
                _statusText,
                _interactionText);
            ApplyCardStyle(_crisisPanel, PresentationState.SettlementCue, true);
            ApplyCardStyle(_settlementPanel, PresentationState.SettlementCue, true);
            ApplyCardStyle(_interactionPanel, PresentationState.InteractionCue, true);
            ApplyCardStyle(_statusPanel, PresentationState.InteractionCue, false);
            ApplyCardStyle(_inventoryPanel, PrototypeHudCue.Shelter, false);
            ApplyCardStyle(_inspectorPanel, PresentationState.DirectiveCue, false);
            ApplyCardStyle(_worldPanel, PrototypeHudCue.Neutral, false);
            ApplyCardStyle(_helpPanel, PrototypeHudCue.Neutral, false);
            ApplyCardStyle(_debugPanel, PrototypeHudCue.Neutral, false);
        }

        private void ApplyBounds(Control? control, string key)
        {
            if (control == null)
            {
                return;
            }

            PrototypeHudBounds bounds = Layout[key];
            control.Position = new Vector2(bounds.X, bounds.Y);
            control.Size = new Vector2(bounds.Width, bounds.Height);
        }

        private static Panel CreateCard(string name, int fontSize, out Label label)
        {
            Panel panel = new()
            {
                Name = name,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label = CreateLabel(fontSize);
            panel.AddChild(label);
            return panel;
        }

        private static Label CreateLabel(int fontSize)
        {
            Label label = new()
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorRight = 1.0f,
                AnchorBottom = 1.0f,
                OffsetLeft = 12.0f,
                OffsetTop = 10.0f,
                OffsetRight = -12.0f,
                OffsetBottom = -10.0f,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ClipText = true,
                VerticalAlignment = VerticalAlignment.Top
            };
            label.AddThemeFontSizeOverride("font_size", fontSize);
            return label;
        }

        private static void ApplyCardStyle(Panel? panel, PrototypeHudCue cue, bool emphasized)
        {
            if (panel == null)
            {
                return;
            }

            (PrototypeHudCue Cue, bool Emphasized) key = (cue, emphasized);
            if (!CardStyleCache.TryGetValue(key, out StyleBoxFlat? style))
            {
                Color accent = cue switch
                {
                    PrototypeHudCue.FoodAndFuel => new Color(0.94f, 0.63f, 0.22f),
                    PrototypeHudCue.Shelter => new Color(0.33f, 0.70f, 0.92f),
                    PrototypeHudCue.Stable => new Color(0.34f, 0.82f, 0.48f),
                    PrototypeHudCue.Collapsed => new Color(0.92f, 0.29f, 0.24f),
                    PrototypeHudCue.BlockedInteraction => new Color(0.96f, 0.39f, 0.22f),
                    PrototypeHudCue.ContributionSuccess => new Color(0.42f, 0.88f, 0.55f),
                    _ => new Color(0.42f, 0.76f, 0.72f)
                };
                style = new StyleBoxFlat
                {
                    BgColor = new Color(0.035f, 0.065f, 0.075f, emphasized ? 0.91f : 0.78f),
                    BorderColor = accent,
                    CornerRadiusTopLeft = 6,
                    CornerRadiusTopRight = 6,
                    CornerRadiusBottomRight = 6,
                    CornerRadiusBottomLeft = 6,
                    BorderWidthLeft = emphasized ? 3 : 1,
                    BorderWidthTop = emphasized ? 3 : 1,
                    BorderWidthRight = emphasized ? 3 : 1,
                    BorderWidthBottom = emphasized ? 3 : 1
                };
                CardStyleCache[key] = style;
            }
            panel.AddThemeStyleboxOverride("panel", style);
        }
    }
}
