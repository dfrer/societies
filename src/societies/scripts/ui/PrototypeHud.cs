using Godot;
using Societies.Core;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Societies.UI
{
    /// <summary>
    /// Responsive normal-play HUD for crisis, settlement, inventory, interaction, and inspection state.
    /// </summary>
    public partial class PrototypeHud : CanvasLayer
    {
        /// <summary>
        /// Keeps normal-play text in the dedicated presentation canvas above every world canvas.
        /// This is deliberately independent of the 3D renderer so capture readback cannot mix
        /// placeholder terrain shading into HUD cards.
        /// </summary>
        public const int PresentationCanvasLayer = 100;

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
        private Label? _goalLabel;
        private Label? _decisionRailLabel;
        private Label? _crosshairLabel;
        private Button? _protectWetlandButton;
        private Button? _drawDownWetlandButton;
        private readonly List<Button> _profileButtons = new();
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
        private PrototypeCivicPolicy _selectedCivicPolicy;
        private bool _civicChoiceAvailable;
        private bool _decisionSurfaceOpen;
        private PrototypeHudLoopProgress _loopProgress;
        private bool _diagnosticsVisible;
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
        public string GoalText => _goalLabel?.Text ?? string.Empty;
        public bool IsInventoryVisible => _inventoryPanel?.Visible ?? false;
        public bool IsDebugVisible => _debugPanel?.Visible ?? false;
        public bool IsDiagnosticsVisible => _diagnosticsVisible;
        public int ProfileChoiceCount => _profileButtons.Count;
        public bool HasCivicChoiceSurface => _protectWetlandButton != null && _drawDownWetlandButton != null;
        public bool IsCivicChoiceAvailable => _civicChoiceAvailable;
        public PrototypeHudLayout Layout { get; private set; } = PrototypeHudLayout.Calculate(1920.0f, 1080.0f);
        public IReadOnlyDictionary<string, PrototypeHudBounds> LayoutBounds => Layout.Bounds;
        public PrototypeHudPresentationState PresentationState { get; private set; }

        /// <summary>Intent-only UI event; GameManager remains the command seam.</summary>
        public event Action<PrototypeCivicPolicy>? CivicPolicyRequested;

        /// <summary>Intent-only UI event; selected scenario data remains authoritative.</summary>
        public event Action<string>? ExperienceProfileRequested;

        public override void _Ready()
        {
            Layer = PresentationCanvasLayer;
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

        public void SetGoalText(string text)
        {
            if (_goalLabel != null)
            {
                _goalLabel.Text = text;
            }
        }

        public void SetCivicChoiceState(
            PrototypeCivicPolicy selectedCivicPolicy,
            long totalContributedQuantity,
            bool hasCarriedRawResource)
        {
            bool wasUnresolved = HasUnresolvedCivicChoice;
            _selectedCivicPolicy = selectedCivicPolicy;
            _loopProgress = PrototypeHudLoopProgress.Create(
                hasCarriedRawResource,
                totalContributedQuantity,
                selectedCivicPolicy);
            _civicChoiceAvailable = _loopProgress.HasContributed;
            if (!wasUnresolved && HasUnresolvedCivicChoice)
            {
                _decisionSurfaceOpen = true;
            }
            else if (!HasUnresolvedCivicChoice)
            {
                _decisionSurfaceOpen = false;
            }
            if (_protectWetlandButton != null) _protectWetlandButton.Disabled = !HasUnresolvedCivicChoice;
            if (_drawDownWetlandButton != null) _drawDownWetlandButton.Disabled = !HasUnresolvedCivicChoice;
            ApplyDecisionRailState();
            ApplyDiagnosticsVisibility();
            ApplyPointerMode();
        }

        /// <summary>
        /// Builds the two catalog-owned profile buttons. The buttons contain no profile state;
        /// they only ask the GameManager to recreate the selected scenario.
        /// </summary>
        public void SetExperienceProfiles(
            IReadOnlyList<PrototypeExperienceProfileOption> profiles,
            string? activeScenarioId)
        {
            if (profiles.Count != 2)
            {
                throw new ArgumentException("ER-01 requires exactly two curated experience profiles.", nameof(profiles));
            }

            foreach (Button button in _profileButtons)
            {
                button.QueueFree();
            }
            _profileButtons.Clear();

            if (_helpPanel == null)
            {
                return;
            }

            foreach ((PrototypeExperienceProfileOption profile, int index) in profiles
                .OrderBy(candidate => candidate.DisplayOrder)
                .ThenBy(candidate => candidate.ScenarioId, StringComparer.Ordinal)
                .Select((profile, index) => (profile, index)))
            {
                Button button = new()
                {
                    Name = $"ExperienceProfile_{profile.ProfileId}",
                    Text = $"{profile.Title}: {profile.ResourceApproach}",
                    TooltipText = $"{profile.ImmediatePressure} {profile.WorldCue}",
                    Disabled = string.Equals(profile.ScenarioId, activeScenarioId, StringComparison.OrdinalIgnoreCase),
                    MouseFilter = Control.MouseFilterEnum.Stop,
                    AnchorLeft = index == 0 ? 0.0f : 0.5f,
                    AnchorRight = index == 0 ? 0.5f : 1.0f,
                    AnchorTop = 0.0f,
                    AnchorBottom = 1.0f,
                    OffsetLeft = index == 0 ? 8.0f : 3.0f,
                    OffsetTop = 8.0f,
                    OffsetRight = index == 0 ? -3.0f : -8.0f,
                    OffsetBottom = -8.0f
                };
                button.Pressed += () => ExperienceProfileRequested?.Invoke(profile.ScenarioId);
                _helpPanel.AddChild(button);
                _profileButtons.Add(button);
            }

            ApplyDiagnosticsVisibility();
        }

        public void ToggleDiagnostics() => SetDiagnosticsVisible(!_diagnosticsVisible);

        public void SetDiagnosticsVisible(bool visible)
        {
            _diagnosticsVisible = visible;
            ApplyDiagnosticsVisibility();
            ApplyPointerMode();
        }

        /// <summary>
        /// Handles the explicit close/reopen path for the currently active pointer surface.
        /// Returns false when Escape should retain the player's ordinary mouse-mode behavior.
        /// </summary>
        public bool HandleEscapeForActiveSurface()
        {
            if (_diagnosticsVisible)
            {
                SetDiagnosticsVisible(false);
                return true;
            }

            if (!HasUnresolvedCivicChoice)
            {
                return false;
            }

            _decisionSurfaceOpen = !_decisionSurfaceOpen;
            ApplyDiagnosticsVisibility();
            ApplyPointerMode();
            return true;
        }

        public void RequestCivicPolicy(PrototypeCivicPolicy policy) => CivicPolicyRequested?.Invoke(policy);

        public void RequestExperienceProfile(string scenarioId) => ExperienceProfileRequested?.Invoke(scenarioId);

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
            _helpLabel.Name = "HelpLabel";
            root.AddChild(_helpPanel);
            _crisisPanel = CreateCard("CrisisPanel", 16, out _crisisLabel);
            root.AddChild(_crisisPanel);
            _goalLabel = CreateLabel(17);
            _goalLabel.Name = "SettlementGoal";
            _goalLabel.OffsetBottom = -120.0f;
            _crisisPanel.AddChild(_goalLabel);
            _decisionRailLabel = new Label
            {
                Name = "DecisionRail",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorTop = 1.0f,
                AnchorBottom = 1.0f,
                OffsetLeft = 12.0f,
                OffsetTop = -116.0f,
                OffsetRight = -12.0f,
                OffsetBottom = -94.0f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _decisionRailLabel.AddThemeFontSizeOverride("font_size", 13);
            _crisisPanel.AddChild(_decisionRailLabel);
            _protectWetlandButton = CreateCivicChoiceButton("[4] Protect wetland", PrototypeCivicPolicy.ProtectWetland);
            _protectWetlandButton.AnchorTop = 1.0f;
            _protectWetlandButton.AnchorBottom = 1.0f;
            _protectWetlandButton.OffsetLeft = 12.0f;
            _protectWetlandButton.OffsetTop = -88.0f;
            _protectWetlandButton.OffsetRight = -12.0f;
            _protectWetlandButton.OffsetBottom = -52.0f;
            _crisisPanel.AddChild(_protectWetlandButton);
            _drawDownWetlandButton = CreateCivicChoiceButton("[5] Draw down wetland", PrototypeCivicPolicy.DrawDownWetland);
            _drawDownWetlandButton.AnchorTop = 1.0f;
            _drawDownWetlandButton.AnchorBottom = 1.0f;
            _drawDownWetlandButton.OffsetLeft = 12.0f;
            _drawDownWetlandButton.OffsetTop = -46.0f;
            _drawDownWetlandButton.OffsetRight = -12.0f;
            _drawDownWetlandButton.OffsetBottom = -10.0f;
            _crisisPanel.AddChild(_drawDownWetlandButton);
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
            ApplyCardStyle(_statusPanel, PresentationState.StatusCue, false);
            ApplyCardStyle(_inventoryPanel, PrototypeHudCue.Shelter, false);
            ApplyCardStyle(_inspectorPanel, PresentationState.DirectiveCue, false);
            ApplyCardStyle(_worldPanel, PrototypeHudCue.Neutral, false);
            ApplyCardStyle(_helpPanel, PrototypeHudCue.Neutral, false);
            ApplyCardStyle(_debugPanel, PrototypeHudCue.Neutral, false);
            ApplyDecisionRailState();
            ApplyDiagnosticsVisibility();
        }

        private void ApplyDiagnosticsVisibility()
        {
            bool normalPlay = !_diagnosticsVisible;
            if (_debugPanel != null) _debugPanel.Visible = _diagnosticsVisible;
            if (_inventoryPanel != null) _inventoryPanel.Visible = _diagnosticsVisible;
            if (_settlementPanel != null) _settlementPanel.Visible = _diagnosticsVisible;
            if (_worldPanel != null) _worldPanel.Visible = _diagnosticsVisible;
            if (_inspectorPanel != null) _inspectorPanel.Visible = _diagnosticsVisible;
            if (_helpPanel != null) _helpPanel.Visible = _diagnosticsVisible;
            if (_crisisLabel != null) _crisisLabel.Visible = _diagnosticsVisible;
            if (_goalLabel != null) _goalLabel.Visible = normalPlay;
            if (_decisionRailLabel != null) _decisionRailLabel.Visible = normalPlay;
            if (_protectWetlandButton != null) _protectWetlandButton.Visible = normalPlay && IsDecisionSurfaceActive;
            if (_drawDownWetlandButton != null) _drawDownWetlandButton.Visible = normalPlay && IsDecisionSurfaceActive;
            if (_helpLabel != null) _helpLabel.Visible = _diagnosticsVisible && _profileButtons.Count == 0;
            foreach (Button button in _profileButtons)
            {
                // The two deterministic starts are an opt-in diagnostics surface. The help
                // label is hidden while these controls are visible so the nodes never overlap.
                button.Visible = _diagnosticsVisible;
            }
        }

        /// <summary>
        /// A presentation-only reading of existing inventory, contribution, and policy
        /// projections supplied by the authoritative runtime through the HUD presenter.
        /// </summary>
        private void ApplyDecisionRailState()
        {
            if (_decisionRailLabel == null)
            {
                return;
            }

            _decisionRailLabel.Text = _loopProgress.DecisionRailText;
            _decisionRailLabel.Modulate = _loopProgress.HasSelectedPolicy
                ? new Color(0.36f, 0.82f, 0.73f)
                : _loopProgress.HasContributed
                    ? new Color(0.95f, 0.73f, 0.31f)
                    : new Color(0.76f, 0.70f, 0.56f);
        }

        private bool HasUnresolvedCivicChoice =>
            _civicChoiceAvailable && _selectedCivicPolicy == PrototypeCivicPolicy.Neutral;

        private bool IsDecisionSurfaceActive =>
            HasUnresolvedCivicChoice && _decisionSurfaceOpen;

        private void ApplyPointerMode()
        {
            Input.MouseMode = ResolvePointerMode(_diagnosticsVisible, IsDecisionSurfaceActive);
        }

        internal static Input.MouseModeEnum ResolvePointerMode(bool diagnosticsVisible, bool decisionSurfaceActive) =>
            diagnosticsVisible || decisionSurfaceActive
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;

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

        private Button CreateCivicChoiceButton(string text, PrototypeCivicPolicy policy)
        {
            Button button = new()
            {
                Name = $"CivicChoice_{policy}",
                Text = text,
                TooltipText = policy == PrototypeCivicPolicy.ProtectWetland
                    ? "Reserve reeds now to preserve future wetland supply."
                    : "Allow more reeds now at a shared wetland cost.",
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            button.Pressed += () => CivicPolicyRequested?.Invoke(policy);
            return button;
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
                    // The bounded normal-play palette is intentionally closer to a weathered
                    // field board than a debug overlay: reed-gold for active work, wetland teal
                    // for shared consequence, and rust only when a command cannot proceed.
                    PrototypeHudCue.FoodAndFuel => new Color(0.95f, 0.73f, 0.31f),
                    PrototypeHudCue.Shelter => new Color(0.36f, 0.82f, 0.73f),
                    PrototypeHudCue.Stable => new Color(0.42f, 0.84f, 0.59f),
                    PrototypeHudCue.Collapsed => new Color(0.90f, 0.36f, 0.27f),
                    PrototypeHudCue.BlockedInteraction => new Color(0.91f, 0.39f, 0.27f),
                    PrototypeHudCue.ContributionSuccess => new Color(0.95f, 0.73f, 0.31f),
                    PrototypeHudCue.DepletedInteraction => new Color(0.63f, 0.66f, 0.70f),
                    _ => new Color(0.36f, 0.82f, 0.73f)
                };
                style = new StyleBoxFlat
                {
                    // Capture and normal play need the same unambiguous reading. Opaque cards
                    // prevent dark 3D placeholder geometry from visually bleeding through text.
                    BgColor = new Color(0.075f, 0.065f, 0.047f, 1.0f),
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
