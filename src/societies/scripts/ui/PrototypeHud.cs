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
        private Label? _crosshairLabel;
        private Label? _voxelHotbarLabel;
        private Label? _voxelBuildLabel;
        private Label? _voxelPromptLabel;
        private Label? _voxelToastLabel;
        private Label? _voxelInventoryCapacityLabel;
        private Label? _voxelInventoryCloseLabel;
        private Panel? _voxelHotbarPanel;
        private Panel? _voxelBuildPanel;
        private Panel? _voxelInventoryGridPanel;
        private readonly List<Panel> _voxelHotbarSlots = new();
        private readonly List<Panel> _voxelInventorySlots = new();
        private readonly Dictionary<string, Button> _voxelBuildCards = new(StringComparer.Ordinal);
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
        private bool _heightfieldInventoryVisible;
        private bool _heightfieldSettlementVisible;
        private bool _heightfieldInspectorVisible;
        private bool _heightfieldCrisisVisible;
        private bool _heightfieldWorldVisible;
        private bool _heightfieldHelpVisible;
        private static readonly Dictionary<(PrototypeHudCue Cue, bool Emphasized), StyleBoxFlat> CardStyleCache = new();
        private static readonly Color FieldKitInk = new("2d2922");

        public string DebugText => _debugLabel?.Text ?? string.Empty;
        public string InventoryText => _inventoryLabel?.Text ?? string.Empty;
        public string CraftingText => _craftingLabel?.Text ?? string.Empty;
        public string StatusText => _statusLabel?.Text ?? string.Empty;
        public string InteractionText => _interactionLabel?.Text ?? string.Empty;
        public string HelpText => _helpLabel?.Text ?? string.Empty;
        public string SettlementText => _settlementLabel?.Text ?? string.Empty;
        public string WorldText => _worldLabel?.Text ?? string.Empty;
        public string InspectorText => _inspectorLabel?.Text ?? string.Empty;
        public string CrisisText => _crisisLabel?.Text ?? string.Empty;
        public bool IsInventoryVisible => _inventoryPanel?.Visible ?? false;
        public bool IsDebugVisible => _debugPanel?.Visible ?? false;
        public bool IsVoxelFoundationMode { get; private set; }
        public string VoxelHotbarText => _voxelHotbarLabel?.Text ?? string.Empty;
        public string VoxelBuildText => _voxelBuildLabel?.Text ?? string.Empty;
        public int VoxelHotbarSlotCount => _voxelHotbarSlots.Count;
        public int VoxelBuildCardCount => _voxelBuildCards.Count;
        public int VoxelInventoryVisualSlotCount => _voxelInventorySlots.Count;
        public string VoxelInventoryCapacityText => _voxelInventoryCapacityLabel?.Text ?? string.Empty;
        public string VoxelPlacementStateText => _voxelPromptLabel?.Text ?? string.Empty;
        public bool IsVoxelBuildTrayVisible => _voxelBuildPanel?.Visible == true;
        public event Action<string>? VoxelPieceRequested;
        public event Action<bool>? VoxelFieldPackShortcutRequested;
        public bool HasVisibleLegacySettlementPanels => (_inventoryPanel?.Visible ?? false) ||
            (_settlementPanel?.Visible ?? false) || (_inspectorPanel?.Visible ?? false) || (_crisisPanel?.Visible ?? false);
        public bool HasVisibleLegacyVoxelCards => (_settlementPanel?.Visible ?? false) || (_inspectorPanel?.Visible ?? false) ||
            (_crisisPanel?.Visible ?? false) || (_worldPanel?.Visible ?? false) || (_helpPanel?.Visible ?? false);
        public int VoxelInventorySlotLines => IsVoxelFoundationMode && IsInventoryVisible ? _voxelInventorySlots.Count : 0;
        public PrototypeHudLayout Layout { get; private set; } = PrototypeHudLayout.Calculate(1920.0f, 1080.0f);
        public IReadOnlyDictionary<string, PrototypeHudBounds> LayoutBounds => Layout.Bounds;
        public PrototypeHudPresentationState PresentationState { get; private set; }

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

        public void SetDebugVisible(bool visible)
        {
            if (_debugPanel != null)
            {
                _debugPanel.Visible = visible;
            }
        }

        /// <summary>Keeps the voxel-only foundation free of unavailable settlement/resource guidance.</summary>
        public void SetVoxelFoundationMode(bool enabled)
        {
            if (enabled == IsVoxelFoundationMode)
            {
                return;
            }

            if (enabled)
            {
                _heightfieldSettlementVisible = _settlementPanel?.Visible ?? false;
                _heightfieldInspectorVisible = _inspectorPanel?.Visible ?? false;
                _heightfieldCrisisVisible = _crisisPanel?.Visible ?? false;
                _heightfieldInventoryVisible = _inventoryPanel?.Visible ?? false;
                _heightfieldWorldVisible = _worldPanel?.Visible ?? false;
                _heightfieldHelpVisible = _helpPanel?.Visible ?? false;
                IsVoxelFoundationMode = true;
                if (_settlementPanel != null) _settlementPanel.Visible = false;
                if (_inspectorPanel != null) _inspectorPanel.Visible = false;
                if (_crisisPanel != null) _crisisPanel.Visible = false;
                if (_inventoryPanel != null) _inventoryPanel.Visible = false;
                if (_worldPanel != null) _worldPanel.Visible = false;
                if (_helpPanel != null) _helpPanel.Visible = false;
                if (_craftingLabel != null) _craftingLabel.Visible = false;
                if (_inventoryLabel != null) _inventoryLabel.Visible = false;
                if (_voxelInventoryGridPanel != null) _voxelInventoryGridPanel.Visible = true;
                if (_voxelInventoryCapacityLabel != null) _voxelInventoryCapacityLabel.Visible = true;
                if (_voxelInventoryCloseLabel != null) _voxelInventoryCloseLabel.Visible = true;
                if (_voxelHotbarPanel != null) _voxelHotbarPanel.Visible = true;
                if (_voxelBuildPanel != null) _voxelBuildPanel.Visible = true;
                if (_interactionPanel != null) _interactionPanel.Visible = true;
                if (_statusPanel != null) _statusPanel.Visible = true;
                ConfigureVoxelMessageLabel(_interactionLabel);
                ConfigureVoxelMessageLabel(_statusLabel);
                ApplyResponsiveLayout(GetViewport().GetVisibleRect().Size);
                SetHelpText(string.Empty);
                SetInteractionText("Aim at an exposed block to gather · right-click places your selected piece");
                SetStatusText("Founder field kit ready");
                SetWorldText("Founder worldcraft · tactile field workshop");
                return;
            }

            IsVoxelFoundationMode = false;
            if (_settlementPanel != null) _settlementPanel.Visible = _heightfieldSettlementVisible;
            if (_inspectorPanel != null) _inspectorPanel.Visible = _heightfieldInspectorVisible;
            if (_crisisPanel != null) _crisisPanel.Visible = _heightfieldCrisisVisible;
            if (_inventoryPanel != null) _inventoryPanel.Visible = _heightfieldInventoryVisible;
            if (_worldPanel != null) _worldPanel.Visible = _heightfieldWorldVisible;
            if (_helpPanel != null) _helpPanel.Visible = _heightfieldHelpVisible;
            if (_craftingLabel != null) _craftingLabel.Visible = true;
            if (_inventoryLabel != null) _inventoryLabel.Visible = true;
            if (_voxelInventoryGridPanel != null) _voxelInventoryGridPanel.Visible = false;
            if (_voxelInventoryCapacityLabel != null) _voxelInventoryCapacityLabel.Visible = false;
            if (_voxelInventoryCloseLabel != null) _voxelInventoryCloseLabel.Visible = false;
            if (_voxelHotbarPanel != null) _voxelHotbarPanel.Visible = false;
            if (_voxelBuildPanel != null) _voxelBuildPanel.Visible = false;
            ApplyResponsiveLayout(GetViewport().GetVisibleRect().Size);
        }

        public void SetVoxelWorldcraftState(InventoryComponent inventory, bool buildMode, string pieceId, int rotation, long constructionRevision)
        {
            if (!IsVoxelFoundationMode) return;
            string[] slots = new string[VoxelWorldcraftCatalog.HotbarSlots];
            List<(string ItemId, int Count)> visibleSlots = new();
            int slot = 0;
            foreach (string itemId in VoxelWorldcraftCatalog.HotbarOrder)
            {
                int remaining = inventory.GetCount(itemId);
                while (remaining > 0 && slot < slots.Length)
                {
                    int stack = Math.Min(remaining, VoxelWorldcraftCatalog.StackLimit);
                    slots[slot++] = $"[{slot}] {InventoryComponent.FormatItemName(itemId),-8} {stack,2}/{VoxelWorldcraftCatalog.StackLimit}";
                    visibleSlots.Add((itemId, stack));
                    remaining -= stack;
                }
            }
            while (slot < slots.Length) { slots[slot] = $"[{slot + 1}] Empty"; slot++; }
            SetInventoryText("FIELD PACK · 8 STACK SLOTS\n" + string.Join("\n", slots));
            if (_voxelHotbarLabel != null)
            {
                _voxelHotbarLabel.Text = $"FIELD PACK  ·  {inventory.UsedSlots}/{inventory.SlotLimit} stacks  ·  " +
                    string.Join("  ", VoxelWorldcraftCatalog.HotbarOrder.Select(itemId =>
                        $"{Capitalize(InventoryComponent.FormatItemName(itemId))} {inventory.GetCount(itemId)}"));
            }
            if (_voxelBuildLabel != null)
            {
                WorldcraftPieceDefinition definition = VoxelWorldcraftCatalog.FindPiece(pieceId) ?? VoxelWorldcraftCatalog.Pieces[0];
                string cost = string.Join(" + ", definition.Cost.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Value} {Capitalize(InventoryComponent.FormatItemName(pair.Key))}"));
                string affordable = inventory.HasItems(definition.Cost) ? "READY" : "NEED MATERIALS";
                string selector = string.Join("   ", VoxelWorldcraftCatalog.Pieces.Select((piece, index) =>
                    $"{(piece.Id == pieceId ? "▶" : " ")}[{index + 1}] {Capitalize(InventoryComponent.FormatItemName(piece.Id.Replace("wood_", string.Empty, StringComparison.Ordinal)))}"));
                _voxelBuildLabel.Text = buildMode
                    ? $"BUILD  {selector}  ·  {rotation * 90}°  ·  COST {cost}  ·  {affordable}"
                    : $"GATHER  {selector}  ·  SELECTED COST {cost}  ·  {affordable}  ·  B to build  ·  X dismantles targeted piece";
                UpdateFieldKitSlots(visibleSlots, inventory, definition);
                UpdateBuildCards(inventory, definition, rotation, buildMode);
            }
        }

        /// <summary>Projects the latest authoritative preview result; it never evaluates or retains world state.</summary>
        public void SetVoxelPlacementEvaluation(WorldcraftPlacementEvaluation? evaluation, bool buildMode)
        {
            if (!IsVoxelFoundationMode || _voxelPromptLabel == null)
            {
                return;
            }

            if (!buildMode)
            {
                _voxelPromptLabel.Text = "GATHER · Left-click exposed soil, stone, or wood";
                _crosshairLabel?.AddThemeColorOverride("font_color", new Color("d9caa5"));
                return;
            }

            if (evaluation == null)
            {
                _voxelPromptLabel.Text = "AIM · Find a clear surface for your selected piece";
                _crosshairLabel?.AddThemeColorOverride("font_color", new Color("d9caa5"));
                return;
            }

            WorldcraftRejection rejection = evaluation.Rejection;
            _voxelPromptLabel.Text = rejection == WorldcraftRejection.None
                ? "PLACE · Right-click to set the piece"
                : $"CANNOT PLACE · {DescribeWorldcraftRejection(rejection)}";
            _crosshairLabel?.AddThemeColorOverride("font_color", rejection == WorldcraftRejection.None
                ? new Color("7fbd73") : new Color("c85c47"));
        }

        /// <summary>Projects a raycast-derived target label without retaining a target or editing the world.</summary>
        public void SetVoxelGatherTargetFocus(string prompt)
        {
            if (!IsVoxelFoundationMode || _voxelPromptLabel == null || _voxelInventoryOpen())
            {
                return;
            }

            _voxelPromptLabel.Text = prompt;
            _crosshairLabel?.AddThemeColorOverride("font_color", new Color("d9caa5"));
        }

        private bool _voxelInventoryOpen() => _inventoryPanel?.Visible == true;

        public void SetVoxelInventoryVisible(bool visible)
        {
            if (!IsVoxelFoundationMode) return;
            if (_inventoryPanel != null) _inventoryPanel.Visible = visible;
            if (_crosshairLabel != null) _crosshairLabel.Visible = !visible;
            if (_interactionPanel != null) _interactionPanel.Visible = !visible;
            if (_statusPanel != null) _statusPanel.Visible = !visible;
            if (_voxelBuildPanel != null) _voxelBuildPanel.Visible = !visible;
            if (_voxelHotbarPanel != null) _voxelHotbarPanel.Visible = !visible;
            foreach (Button card in _voxelBuildCards.Values) card.Disabled = visible;
            ApplyResponsiveLayout(GetViewport().GetVisibleRect().Size);
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
            if (_voxelHotbarPanel != null) { _voxelHotbarPanel.Position = new Vector2(Mathf.Max(20.0f, (viewportSize.X - 760.0f) * 0.5f), viewportSize.Y - 124.0f); _voxelHotbarPanel.Size = new Vector2(Mathf.Min(760.0f, viewportSize.X - 40.0f), 96.0f); }
            if (_voxelBuildPanel != null) { _voxelBuildPanel.Position = new Vector2(Mathf.Max(20.0f, (viewportSize.X - 720.0f) * 0.5f), viewportSize.Y - 252.0f); _voxelBuildPanel.Size = new Vector2(Mathf.Min(720.0f, viewportSize.X - 40.0f), 110.0f); }
            if (IsVoxelFoundationMode && _inventoryPanel != null)
            {
                Vector2 modalSize = new(Mathf.Min(640.0f, viewportSize.X - 40.0f), Mathf.Min(430.0f, viewportSize.Y - 60.0f));
                _inventoryPanel.Size = modalSize;
                _inventoryPanel.Position = (viewportSize - modalSize) * 0.5f;
            }
            if (IsVoxelFoundationMode && _interactionPanel != null && _statusPanel != null)
            {
                float buildTop = viewportSize.Y - 252.0f;
                const float messageHeight = 54.0f;
                float promptY = Mathf.Clamp(viewportSize.Y * 0.55f, 100.0f, buildTop - (messageHeight * 2.0f) - 12.0f);
                _interactionPanel.Position = new Vector2(Mathf.Max(20.0f, (viewportSize.X - 480.0f) * 0.5f), promptY);
                _interactionPanel.Size = new Vector2(Mathf.Min(480.0f, viewportSize.X - 40.0f), messageHeight);
                _statusPanel.Position = _interactionPanel.Position + new Vector2(0.0f, messageHeight + 6.0f);
                _statusPanel.Size = _interactionPanel.Size;
            }
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

            BuildVoxelFieldKit(root);

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
            ApplyCardStyle(_voxelHotbarPanel, PrototypeHudCue.Shelter, true);
            ApplyCardStyle(_voxelBuildPanel, PrototypeHudCue.Neutral, false);
            if (IsVoxelFoundationMode)
            {
                ApplyFieldKitStyle(_voxelHotbarPanel, new Color("b99555"), new Color("eee4ca"));
                ApplyFieldKitStyle(_voxelBuildPanel, new Color("af533e"), new Color("eee4ca"));
                ApplyFieldKitStyle(_inventoryPanel, new Color("b99555"), new Color("eee4ca"));
                ApplyFieldKitStyle(_interactionPanel, new Color("7fbd73"), new Color("f4edda"));
                ApplyFieldKitStyle(_statusPanel, new Color("b99555"), new Color("f4edda"));
            }
        }

        private void BuildVoxelFieldKit(Control root)
        {
            if (_inventoryPanel == null || _inventoryLabel == null || _craftingLabel == null || _interactionLabel == null || _statusLabel == null)
            {
                throw new InvalidOperationException("Field kit requires the base HUD controls.");
            }
            _inventoryLabel!.Visible = false;
            _craftingLabel!.Visible = false;

            _voxelBuildPanel = new Panel { Name = "VoxelBuildTray", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
            _voxelBuildLabel = new Label { Name = "BuildTraySummary", Position = new Vector2(12, 5), Size = new Vector2(696, 22), ClipText = true, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            _voxelBuildLabel.AddThemeFontSizeOverride("font_size", 12);
            _voxelBuildPanel.AddChild(_voxelBuildLabel);
            HBoxContainer cards = new() { Name = "BuildCards", Position = new Vector2(10, 31), Size = new Vector2(700, 73), MouseFilter = Control.MouseFilterEnum.Stop };
            cards.AddThemeConstantOverride("separation", 8);
            foreach (WorldcraftPieceDefinition piece in VoxelWorldcraftCatalog.Pieces)
            {
                Button card = new()
                {
                    Name = $"BuildCard_{piece.Id}", CustomMinimumSize = new Vector2(224, 70),
                    Text = piece.DisplayName, TooltipText = $"Select {piece.DisplayName}", FocusMode = Control.FocusModeEnum.All,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                string requestedId = piece.Id;
                card.Pressed += () => VoxelPieceRequested?.Invoke(requestedId);
                card.GuiInput += HandleVoxelFieldPackGuiInput;
                cards.AddChild(card); _voxelBuildCards.Add(piece.Id, card);
            }
            _voxelBuildPanel.AddChild(cards); root.AddChild(_voxelBuildPanel);

            _voxelHotbarPanel = new Panel { Name = "VoxelToolBelt", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            _voxelHotbarLabel = new Label { Name = "ToolBeltCapacity", Position = new Vector2(13, 5), Size = new Vector2(734, 19), ClipText = true, MouseFilter = Control.MouseFilterEnum.Ignore };
            _voxelHotbarLabel.AddThemeFontSizeOverride("font_size", 13);
            _voxelHotbarPanel.AddChild(_voxelHotbarLabel);
            HBoxContainer belt = new() { Name = "EightSlotToolBelt", Position = new Vector2(12, 27), Size = new Vector2(736, 62), MouseFilter = Control.MouseFilterEnum.Ignore };
            belt.AddThemeConstantOverride("separation", 5);
            for (int index = 0; index < VoxelWorldcraftCatalog.HotbarSlots; index++)
            {
                Panel slot = new() { Name = $"ToolBeltSlot{index + 1:D2}", CustomMinimumSize = new Vector2(87, 58), MouseFilter = Control.MouseFilterEnum.Ignore };
                Label slotLabel = new() { Name = "Contents", Position = new Vector2(7, 5), Size = new Vector2(74, 48), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
                slotLabel.AddThemeFontSizeOverride("font_size", 12); slot.AddChild(slotLabel); belt.AddChild(slot); _voxelHotbarSlots.Add(slot);
            }
            _voxelHotbarPanel.AddChild(belt); root.AddChild(_voxelHotbarPanel);

            _voxelInventoryCapacityLabel = new Label { Name = "FieldPackHeading", Position = new Vector2(24, 18), Size = new Vector2(345, 28), ClipText = true, MouseFilter = Control.MouseFilterEnum.Ignore };
            _voxelInventoryCapacityLabel.AddThemeFontSizeOverride("font_size", 18); _inventoryPanel.AddChild(_voxelInventoryCapacityLabel);
            _voxelInventoryCloseLabel = new Label { Name = "FieldPackClose", Text = "TAB / ESC · close pack", Position = new Vector2(390, 22), Size = new Vector2(226, 20), ClipText = true, HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Ignore };
            _voxelInventoryCloseLabel.AddThemeFontSizeOverride("font_size", 13); _inventoryPanel.AddChild(_voxelInventoryCloseLabel);
            _voxelInventoryGridPanel = new Panel { Name = "EightSlotFieldPack", Position = new Vector2(24, 62), Size = new Vector2(592, 330), MouseFilter = Control.MouseFilterEnum.Ignore };
            GridContainer grid = new() { Name = "InventorySlots", Columns = 4, Position = new Vector2(8, 8), Size = new Vector2(576, 314), MouseFilter = Control.MouseFilterEnum.Ignore };
            grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8);
            for (int index = 0; index < VoxelWorldcraftCatalog.HotbarSlots; index++)
            {
                Panel slot = new() { Name = $"FieldPackSlot{index + 1:D2}", CustomMinimumSize = new Vector2(136, 145), MouseFilter = Control.MouseFilterEnum.Ignore };
                Label label = new() { Name = "Contents", Position = new Vector2(12, 12), Size = new Vector2(112, 120), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
                label.AddThemeFontSizeOverride("font_size", 15); slot.AddChild(label);
                Button focus = new() { Name = "SlotFocus", Position = Vector2.Zero, Size = new Vector2(136, 145), Text = string.Empty, TooltipText = "Field pack slot", FocusMode = Control.FocusModeEnum.All };
                focus.GuiInput += HandleVoxelFieldPackGuiInput;
                slot.AddChild(focus); grid.AddChild(slot); _voxelInventorySlots.Add(slot);
            }
            _voxelInventoryGridPanel.AddChild(grid); _inventoryPanel.AddChild(_voxelInventoryGridPanel);
            _voxelPromptLabel = _interactionLabel;
            _voxelToastLabel = _statusLabel;
        }

        private void HandleVoxelFieldPackGuiInput(InputEvent @event)
        {
            if (!IsVoxelFoundationMode || @event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
            {
                return;
            }

            if (keyEvent.Keycode == Key.Tab || keyEvent.PhysicalKeycode == Key.Tab)
            {
                VoxelFieldPackShortcutRequested?.Invoke(true);
                GetViewport().SetInputAsHandled();
            }
            else if (keyEvent.Keycode == Key.Escape)
            {
                VoxelFieldPackShortcutRequested?.Invoke(false);
                GetViewport().SetInputAsHandled();
            }
        }

        private void UpdateFieldKitSlots(IReadOnlyList<(string ItemId, int Count)> contents, InventoryComponent inventory, WorldcraftPieceDefinition selected)
        {
            string selectedItem = selected.Cost.Keys.FirstOrDefault() ?? string.Empty;
            for (int index = 0; index < VoxelWorldcraftCatalog.HotbarSlots; index++)
            {
                bool occupied = index < contents.Count;
                string item = occupied ? contents[index].ItemId : string.Empty;
                int count = occupied ? contents[index].Count : 0;
                string silhouette = item switch { "soil" => "▰", "stone" => "◆", "wood" => "▥", _ => "·" };
                string text = occupied ? $"{index + 1}\n{silhouette} {Capitalize(item)}\n{count}/{VoxelWorldcraftCatalog.StackLimit}" : $"{index + 1}\n·\nEMPTY";
                UpdateSlot(_voxelHotbarSlots[index], text, item == selectedItem, occupied, item == selectedItem && inventory.HasItems(selected.Cost));
                UpdateSlot(_voxelInventorySlots[index], occupied ? $"SLOT {index + 1}\n\n{silhouette}\n{Capitalize(item)} ×{count}\nStack {count}/{VoxelWorldcraftCatalog.StackLimit}" : $"SLOT {index + 1}\n\n·\nEMPTY\nAvailable", item == selectedItem, occupied, item == selectedItem && inventory.HasItems(selected.Cost));
            }
            if (_voxelInventoryCapacityLabel != null) _voxelInventoryCapacityLabel.Text = $"FIELD PACK · {inventory.UsedSlots}/{inventory.SlotLimit} stacks";
        }

        private void UpdateBuildCards(InventoryComponent inventory, WorldcraftPieceDefinition selected, int rotation, bool buildMode)
        {
            foreach (WorldcraftPieceDefinition piece in VoxelWorldcraftCatalog.Pieces)
            {
                Button card = _voxelBuildCards[piece.Id]; bool selectedCard = piece.Id == selected.Id; bool affordable = inventory.HasItems(piece.Cost);
                string cost = string.Join(" + ", piece.Cost.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Value} {Capitalize(pair.Key)}"));
                string silhouette = piece.Id switch { "wood_floor" => "▰", "wood_wall" => "▥", _ => "┃" };
                string rotationText = selectedCard && piece.Rotates ? $"R · {rotation * 90}°" : piece.Rotates ? "Rotates" : "Fixed";
                card.Text = $"{silhouette}  {piece.DisplayName.ToUpperInvariant()}\n{cost} · {(affordable ? "READY" : "NEEDS")}\n{rotationText}";
                ApplyFieldKitButtonStyle(card, selectedCard, affordable);
            }
            if (_voxelBuildLabel != null)
            {
                _voxelBuildLabel.Text = buildMode
                    ? $"[1] Floor  [2] Wall  [3] Post · {selected.DisplayName} · COST {string.Join(" + ", selected.Cost.Select(pair => $"{pair.Value} {Capitalize(pair.Key)}"))}"
                    : $"[1] Floor  [2] Wall  [3] Post · COST {string.Join(" + ", selected.Cost.Select(pair => $"{pair.Value} {Capitalize(pair.Key)}"))} · Tab opens pack";
            }
        }

        private static string DescribeWorldcraftRejection(WorldcraftRejection rejection) => rejection switch
        {
            WorldcraftRejection.Occupied => "space already occupied",
            WorldcraftRejection.Unsupported => "needs a supporting surface",
            WorldcraftRejection.OutOfRange => "move closer to the target",
            WorldcraftRejection.InsufficientMaterials => "not enough materials",
            WorldcraftRejection.StaleRevision or WorldcraftRejection.TickMismatch => "world changed; refresh target",
            WorldcraftRejection.InventoryFull => "field pack is full",
            _ => rejection == WorldcraftRejection.None ? "ready" : "that placement is unavailable"
        };

        private static void UpdateSlot(Panel slot, string text, bool selected, bool occupied, bool affordable)
        {
            slot.GetNode<Label>("Contents").Text = text;
            Color accent = selected ? (affordable ? new Color("7fbd73") : new Color("c85c47")) : occupied ? new Color("b99555") : new Color("756f60");
            ApplyFieldKitStyle(slot, accent, occupied ? new Color("e9dfc7") : new Color("b8b0a0"));
            Button? focus = slot.GetNodeOrNull<Button>("SlotFocus");
            if (focus != null) ApplyInventorySlotAffordance(focus, accent);
        }

        private static void ApplyFieldKitButtonStyle(Button button, bool selected, bool affordable)
        {
            Color accent = selected ? (affordable ? new Color("7fbd73") : new Color("c85c47")) : new Color("b99555");
            StyleBoxFlat style = CreateFieldKitStyle(accent);
            button.AddThemeStyleboxOverride("normal", style);
            button.AddThemeStyleboxOverride("hover", CreateFieldKitStyle(new Color("d6ad66")));
            button.AddThemeStyleboxOverride("focus", CreateFieldKitStyle(accent, 3));
            button.AddThemeColorOverride("font_color", FieldKitInk);
            button.AddThemeColorOverride("font_hover_color", FieldKitInk);
            button.AddThemeColorOverride("font_focus_color", FieldKitInk);
            button.AddThemeColorOverride("font_pressed_color", FieldKitInk);
            button.AddThemeColorOverride("font_disabled_color", new Color("51493c"));
        }

        private static void ApplyInventorySlotAffordance(Button button, Color accent)
        {
            button.AddThemeStyleboxOverride("normal", TransparentFieldKitStyle());
            button.AddThemeStyleboxOverride("hover", TransparentFieldKitStyle(new Color("d6ad66"), 3));
            button.AddThemeStyleboxOverride("focus", TransparentFieldKitStyle(accent, 3));
            button.AddThemeStyleboxOverride("pressed", TransparentFieldKitStyle(accent, 4));
        }

        private static void ApplyFieldKitStyle(Control? control, Color accent, Color text)
        {
            if (control == null) return;
            control.AddThemeStyleboxOverride("panel", CreateFieldKitStyle(accent));
            if (control is Panel panel)
            {
                foreach (Label label in panel.GetChildren().OfType<Label>())
                {
                    label.AddThemeColorOverride("font_color", FieldKitInk);
                    label.AddThemeColorOverride("font_outline_color", new Color("f3ead8"));
                    label.AddThemeConstantOverride("outline_size", 0);
                }
            }
        }

        private static void ConfigureVoxelMessageLabel(Label? label)
        {
            if (label == null) return;
            label.OffsetLeft = 14.0f;
            label.OffsetRight = -14.0f;
            label.OffsetTop = 4.0f;
            label.OffsetBottom = -4.0f;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.ClipText = true;
            label.AddThemeFontSizeOverride("font_size", 16);
            label.AddThemeColorOverride("font_color", FieldKitInk);
        }

        private static StyleBoxFlat CreateFieldKitStyle(Color accent, int border = 2) => new()
        {
            BgColor = new Color("e7dcc4"), BorderColor = accent,
            BorderWidthLeft = border, BorderWidthTop = border, BorderWidthRight = border, BorderWidthBottom = border,
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
            ShadowColor = new Color(0.15f, 0.12f, 0.08f, 0.32f), ShadowSize = 3, ContentMarginLeft = 4, ContentMarginTop = 3
        };

        private static StyleBoxFlat TransparentFieldKitStyle(Color? accent = null, int border = 0) => new()
        {
            BgColor = new Color(0.0f, 0.0f, 0.0f, 0.0f), BorderColor = accent ?? new Color(0.0f, 0.0f, 0.0f, 0.0f),
            BorderWidthLeft = border, BorderWidthTop = border, BorderWidthRight = border, BorderWidthBottom = border,
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5
        };

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

        private static string Capitalize(string value) => string.IsNullOrEmpty(value)
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];

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
                    // Capture and normal play need the same unambiguous reading. Opaque cards
                    // prevent dark 3D placeholder geometry from visually bleeding through text.
                    BgColor = new Color(0.035f, 0.065f, 0.075f, 1.0f),
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
