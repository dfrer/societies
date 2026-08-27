using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Societies.Presentation;

namespace Societies.Presentation
{
    /// <summary>
    /// A self-contained visual comparison surface for the F1 wetland causeway moment.
    /// All world objects and response states are local study props; this node never reads
    /// or writes the prototype simulation.
    /// </summary>
    public partial class F1VisualTargetStudy : Node3D
    {
        private readonly Dictionary<F1StudyDirection, DirectionPalette> _palettes = new()
        {
            [F1StudyDirection.HearthwoodCauseway] = new(
                new Color("463226"), new Color("6D918F"), new Color("718158"), new Color("6A3B25"),
                new Color("B85B37"), new Color("F7E5C5"), new Color("352119"), new Color("B8CFB0"),
                new Color("8A5A35"), new Color("C96A43"), new Color("D49A62"), new Color("8A6752")),
            [F1StudyDirection.ReedKilnWetlands] = new(
                new Color("3C3930"), new Color("627D78"), new Color("9D9D63"), new Color("4A342A"),
                new Color("D27743"), new Color("FFF0CF"), new Color("30251F"), new Color("AFC9AD"),
                new Color("554235"), new Color("9A6545"), new Color("A8643C"), new Color("6D7D58")),
            [F1StudyDirection.PaintedSluiceToyworks] = new(
                new Color("243B46"), new Color("5C9DA6"), new Color("779A83"), new Color("315666"),
                new Color("E7B34D"), new Color("F4F7E9"), new Color("16303A"), new Color("B9D8CD"),
                new Color("35515B"), new Color("D86A4B"), new Color("66B8BC"), new Color("5C78A1"))
        };

        private Node3D? _worldRoot;
        private Node3D? _waterAccentRoot;
        private Camera3D? _tabletopCamera;
        private Control? _uiRoot;
        private Label? _directionLabel;
        private Label? _straplineLabel;
        private Label? _placeLabel;
        private Label? _interactionHeadingLabel;
        private Label? _materialLabel;
        private Label? _stateMarkLabel;
        private Label? _stateLabel;
        private Label? _maraLine;
        private Label? _consequenceLine;
        private Label? _hintLabel;
        private Panel? _surface;
        private readonly Dictionary<string, PhysicalControlPiece> _physicalControls = new();
        private string? _hoveredPhysicalControl;
        private Vector2 _lastViewportSize;
        private double _visualTime;
        private F1StudyDirection _direction = F1StudyDirection.HearthwoodCauseway;
        private F1StudyResponse _response = F1StudyResponse.None;
        private F1StudyState _state = F1StudyState.Awaiting;
        private bool _reducedMotion;
        private bool _diagnosticsVisible;

        public F1StudyState CurrentStudyState => _state;
        public bool IsReducedMotion => _reducedMotion;
        public bool DiagnosticsVisible => _diagnosticsVisible;
        public IReadOnlyCollection<string> PhysicalControlIds => _physicalControls.Keys;
        public bool HasPointerControlSurface => _tabletopCamera != null && _physicalControls.Count == 7;

        /// <summary>Engine-safe deterministic activation seam for the Godot-hosted study regression.</summary>
        public bool ActivatePhysicalControlForTest(string controlId) => TryActivatePhysicalControl(controlId);

        /// <summary>Engine-safe reduced-motion seam for the Godot-hosted study regression.</summary>
        public void SetReducedMotionForTest(bool reducedMotion)
        {
            _reducedMotion = reducedMotion;
            UpdateReducedMotionHint();
        }

        /// <summary>Engine-safe diagnostics toggle seam; it never reaches simulation or provider state.</summary>
        public void ToggleDiagnosticsForTest() => ToggleDiagnostics();

        /// <summary>Actual visible Canvas occluder rects, used with camera projection in the Godot-hosted regression.</summary>
        public IReadOnlyList<Rect2> GetVisibleCanvasOccluderRectsForTest()
        {
            List<Rect2> rects = new();
            if (_uiRoot == null)
            {
                return rects;
            }
            foreach (string nodeName in new[] { "Header", "ResponseSurface", "Hint" })
            {
                if (_uiRoot.GetNodeOrNull<Control>(nodeName) is Control control && control.Visible)
                {
                    rects.Add(control.GetGlobalRect());
                }
            }
            return rects;
        }

        /// <summary>Screen-space bounds of the actual world hit pieces under the active orthographic camera.</summary>
        public IReadOnlyList<Rect2> GetPhysicalControlProjectedRectsForTest()
        {
            List<Rect2> rects = new();
            if (_tabletopCamera == null)
            {
                return rects;
            }
            foreach (PhysicalControlPiece piece in _physicalControls.Values)
            {
                Vector3 halfSize = piece.HitSize * 0.5f;
                Vector2 minimum = new(float.MaxValue, float.MaxValue);
                Vector2 maximum = new(float.MinValue, float.MinValue);
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 worldPoint = piece.HitTarget.GlobalTransform * new Vector3(halfSize.X * x, halfSize.Y * y, halfSize.Z * z);
                            Vector2 screenPoint = _tabletopCamera.UnprojectPosition(worldPoint);
                            minimum = minimum.Min(screenPoint);
                            maximum = maximum.Max(screenPoint);
                        }
                    }
                }
                rects.Add(new Rect2(minimum, maximum - minimum).Grow(14.0f));
            }
            return rects;
        }

        public bool ArePhysicalControlsUnobscuredByVisibleCanvasForTest()
        {
            IReadOnlyList<Rect2> controlRects = GetPhysicalControlProjectedRectsForTest();
            IReadOnlyList<Rect2> canvasRects = GetVisibleCanvasOccluderRectsForTest();
            return controlRects.Count == 7 && controlRects.All(control => control.Size.X > 0.0f && control.Size.Y > 0.0f) &&
                controlRects.All(control => canvasRects.All(canvas => !canvas.Intersects(control)));
        }

        public override void _Ready()
        {
            BuildCameraAndLight();
            BuildInterface();
            ApplyDirection();
        }

        public override void _Process(double delta)
        {
            Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
            if (viewportSize != _lastViewportSize)
            {
                _lastViewportSize = viewportSize;
                ApplyLayout(viewportSize);
            }

            if (_reducedMotion || _waterAccentRoot == null)
            {
                return;
            }

            _visualTime += delta;
            // A small tabletop-water shift, not atmosphere or cinematic motion.
            _waterAccentRoot.Position = new Vector3(Mathf.Sin((float)_visualTime * 0.42f) * 0.07f, 0.0f, 0.0f);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventMouseMotion motion)
            {
                SetHoveredPhysicalControl(ResolvePhysicalControlAt(motion.Position));
                return;
            }

            if (@event is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left && mouse.Pressed)
            {
                if (TryActivatePhysicalControl(ResolvePhysicalControlAt(mouse.Position)))
                {
                    GetViewport().SetInputAsHandled();
                }
                return;
            }

            if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            {
                return;
            }

            switch (key.Keycode)
            {
                case Key.Key1: SetDirection(F1StudyDirection.HearthwoodCauseway); break;
                case Key.Key2: SetDirection(F1StudyDirection.ReedKilnWetlands); break;
                case Key.Key3: SetDirection(F1StudyDirection.PaintedSluiceToyworks); break;
                case Key.Q: SelectResponse(F1StudyResponse.OfferLabor); break;
                case Key.W: SelectResponse(F1StudyResponse.AskForEvidence); break;
                case Key.E: SelectResponse(F1StudyResponse.Defer); break;
                case Key.Space: AdvanceStudyState(); break;
                case Key.R: ToggleReducedMotion(); break;
                case Key.D: ToggleDiagnostics(); break;
                default: return;
            }

            // Run at the input stage so Space remains reliable alongside the raycast-driven physical pieces.
            GetViewport().SetInputAsHandled();
        }

        private void BuildCameraAndLight()
        {
            WorldEnvironment environment = new()
            {
                Name = "WorldEnvironment",
                Environment = new Godot.Environment
                {
                    BackgroundMode = Godot.Environment.BGMode.Color,
                    BackgroundColor = new Color("17262A"),
                    AmbientLightSource = Godot.Environment.AmbientSource.Color,
                    AmbientLightColor = new Color("F2D9B0"),
                    AmbientLightEnergy = 0.88f,
                    FogEnabled = false
                }
            };
            AddChild(environment);

            DirectionalLight3D key = new()
            {
                RotationDegrees = new Vector3(-58.0f, -28.0f, 0.0f),
                LightColor = new Color("FFE5BB"),
                LightEnergy = 1.15f,
                ShadowEnabled = false
            };
            AddChild(key);

            _tabletopCamera = new Camera3D
            {
                Name = "TabletopCamera",
                Position = new Vector3(0.0f, 11.0f, 11.5f),
                Projection = Camera3D.ProjectionType.Orthogonal,
                Size = 16.0f,
                Current = true
            };
            AddChild(_tabletopCamera);
            _tabletopCamera.LookAt(new Vector3(0.0f, 0.0f, -5.8f), Vector3.Up);
        }

        private void BuildInterface()
        {
            CanvasLayer canvas = new() { Layer = 10 };
            AddChild(canvas);
            _uiRoot = new Control { Name = "StudyInterface", MouseFilter = Control.MouseFilterEnum.Pass };
            _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            canvas.AddChild(_uiRoot);

            Panel header = MakePanel("Header");
            _uiRoot.AddChild(header);
            _directionLabel = MakeLabel("Direction", 21, HorizontalAlignment.Left);
            _straplineLabel = MakeLabel("Strapline", 11, HorizontalAlignment.Left);
            _placeLabel = MakeLabel("Place", 12, HorizontalAlignment.Right);
            header.AddChild(_directionLabel);
            header.AddChild(_straplineLabel);
            header.AddChild(_placeLabel);

            _surface = MakePanel("ResponseSurface");
            _surface.Visible = false;
            _uiRoot.AddChild(_surface);
            _interactionHeadingLabel = MakeLabel("InteractionHeading", 11, HorizontalAlignment.Left);
            _stateMarkLabel = MakeLabel("StateMark", 30, HorizontalAlignment.Center);
            _stateLabel = MakeLabel("State", 12, HorizontalAlignment.Left);
            _maraLine = MakeLabel("MaraLine", 16, HorizontalAlignment.Left);
            _consequenceLine = MakeLabel("Consequence", 13, HorizontalAlignment.Left);
            _materialLabel = MakeLabel("Material", 11, HorizontalAlignment.Left);
            _surface.AddChild(_interactionHeadingLabel);
            _surface.AddChild(_stateMarkLabel);
            _surface.AddChild(_stateLabel);
            _surface.AddChild(_maraLine);
            _surface.AddChild(_consequenceLine);
            _surface.AddChild(_materialLabel);

            _hintLabel = MakeLabel("Hint", 11, HorizontalAlignment.Right);
            _hintLabel.Text = "D  DETAILS";
            _hintLabel.Visible = false;
            _uiRoot.AddChild(_hintLabel);
            ApplyLayout(GetViewport().GetVisibleRect().Size);
        }

        private void ApplyDirection()
        {
            DirectionPalette palette = _palettes[_direction];
            if (GetNodeOrNull<WorldEnvironment>("WorldEnvironment") is WorldEnvironment environment && environment.Environment != null)
            {
                environment.Environment.BackgroundColor = palette.Sky;
                environment.Environment.AmbientLightColor = palette.Mist;
            }

            _worldRoot?.QueueFree();
            _worldRoot = new Node3D { Name = $"{_direction}World" };
            AddChild(_worldRoot);
            _physicalControls.Clear();
            _hoveredPhysicalControl = null;
            BuildWetlandMoment(_worldRoot, palette);
            RefreshInterface();
        }

        private void BuildWetlandMoment(Node3D root, DirectionPalette palette)
        {
            AddMesh(root, "TabletopPlinth", new BoxMesh { Size = new Vector3(18.0f, 0.72f, 15.0f) }, palette.Table, new Vector3(0.0f, -0.38f, -5.4f));
            AddMesh(root, "MatteWaterInset", new PlaneMesh { Size = new Vector2(16.6f, 13.6f) }, palette.Water, new Vector3(0.0f, 0.01f, -5.4f));
            AddMesh(root, "CausewayBase", new BoxMesh { Size = new Vector3(5.5f, 0.74f, 15.2f) }, palette.Structure, new Vector3(0.0f, 0.38f, -5.5f));
            AddMesh(root, "CausewaySplit", new BoxMesh { Size = new Vector3(0.44f, 0.13f, 4.5f) }, palette.Accent, new Vector3(-0.55f, 0.82f, -5.45f), new Vector3(0.0f, 0.14f, 0.0f));

            for (int index = 0; index < 20; index++)
            {
                float x = -9.0f + (index % 7) * 3.0f + ((index / 7) % 2) * 0.6f;
                float z = -1.5f - (index / 7) * 4.5f;
                AddReedCluster(root, new Vector3(x, 0.0f, z), palette.Reed, index);
            }

            switch (_direction)
            {
                case F1StudyDirection.HearthwoodCauseway:
                    BuildHearthwoodCauseway(root, palette);
                    break;
                case F1StudyDirection.ReedKilnWetlands:
                    BuildReedKilnWetlands(root, palette);
                    break;
                default:
                    BuildPaintedSluiceToyworks(root, palette);
                    break;
            }

            BuildCharacters(root, palette);
            BuildPhysicalControls(root, palette);
            BuildWorldStateFeedback(root, palette);
            _waterAccentRoot = new Node3D { Name = "TabletopWaterAccents" };
            root.AddChild(_waterAccentRoot);
            for (int index = 0; index < 6; index++)
            {
                float radius = 0.34f + (index % 3) * 0.14f;
                AddMesh(_waterAccentRoot, $"WaterRing{index}", new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.018f }, WithAlpha(palette.Mist, 0.24f), new Vector3(-6.4f + index * 2.35f, 0.035f, -2.2f - (index % 3) * 3.8f), unshaded: true);
            }
        }

        private static void BuildHearthwoodCauseway(Node3D root, DirectionPalette palette)
        {
            for (int index = 0; index < 7; index++)
            {
                float z = -1.3f - index * 1.95f;
                AddMesh(root, $"HandCarvedPlank{index}", new BoxMesh { Size = new Vector3(4.9f, 0.28f, 1.55f) }, palette.Wood, new Vector3(0.0f, 0.9f, z), new Vector3(0.0f, (index % 2 == 0 ? 0.025f : -0.025f), 0.0f));
                AddMesh(root, $"ClayPegLeft{index}", new CylinderMesh { TopRadius = 0.17f, BottomRadius = 0.2f, Height = 0.34f }, palette.Clay, new Vector3(-1.85f, 1.06f, z));
                AddMesh(root, $"ClayPegRight{index}", new CylinderMesh { TopRadius = 0.17f, BottomRadius = 0.2f, Height = 0.34f }, palette.Clay, new Vector3(1.85f, 1.06f, z));
            }
            AddMesh(root, "WoolFeltWorkMat", new BoxMesh { Size = new Vector3(3.8f, 0.08f, 2.3f) }, palette.Felt, new Vector3(-5.0f, 0.18f, -3.8f));
            AddMesh(root, "ChunkyWoodBrace", new BoxMesh { Size = new Vector3(0.42f, 1.9f, 5.0f) }, palette.Wood.Darkened(0.18f), new Vector3(-0.9f, 1.6f, -5.5f), new Vector3(0.0f, 0.0f, 0.22f));
            AddMesh(root, "TerracottaRepairMarker", new CylinderMesh { TopRadius = 0.62f, BottomRadius = 0.72f, Height = 0.42f }, palette.Clay, new Vector3(2.8f, 0.5f, -5.2f));
        }

        private static void BuildReedKilnWetlands(Node3D root, DirectionPalette palette)
        {
            for (int index = 0; index < 6; index++)
            {
                float x = -4.8f + index * 1.82f;
                float z = -3.5f - (index % 2) * 2.8f;
                float radius = 0.72f + (index % 3) * 0.14f;
                AddMesh(root, $"EarthenwareMound{index}", new SphereMesh { Radius = radius, Height = radius * 1.25f }, palette.Clay, new Vector3(x, radius * 0.42f, z), new Vector3(0.0f, index * 0.23f, 0.0f));
                AddMesh(root, $"ReedMat{index}", new BoxMesh { Size = new Vector3(1.42f, 0.07f, 1.76f) }, palette.Reed, new Vector3(x + 0.48f, 0.12f, z + 0.6f), new Vector3(0.0f, index * 0.15f, 0.0f));
            }
            AddMesh(root, "ScorchedWoodBrace", new BoxMesh { Size = new Vector3(0.46f, 1.7f, 5.3f) }, palette.Wood.Darkened(0.38f), new Vector3(0.95f, 1.64f, -5.45f), new Vector3(0.0f, 0.0f, -0.26f));
            AddMesh(root, "KilnWitnessTile", new CylinderMesh { TopRadius = 0.82f, BottomRadius = 0.94f, Height = 0.22f }, palette.Accent, new Vector3(-2.9f, 0.34f, -5.4f));
        }

        private static void BuildPaintedSluiceToyworks(Node3D root, DirectionPalette palette)
        {
            for (int index = 0; index < 6; index++)
            {
                Color blockColor = index % 2 == 0 ? palette.Wood : palette.Accent;
                AddMesh(root, $"PaintedSluiceBlock{index}", new BoxMesh { Size = new Vector3(1.18f, 1.05f + (index % 2) * 0.42f, 1.42f) }, blockColor, new Vector3(-3.1f + index * 1.22f, 0.96f, -5.1f + (index % 2) * 0.62f));
            }
            AddMesh(root, "GlazedClayChannel", new BoxMesh { Size = new Vector3(0.64f, 0.28f, 6.5f) }, palette.Clay, new Vector3(2.75f, 0.72f, -5.25f));
            AddMesh(root, "GlazedGauge", new CylinderMesh { TopRadius = 0.72f, BottomRadius = 0.72f, Height = 0.24f }, palette.Accent, new Vector3(3.85f, 0.46f, -3.4f));
            AddMesh(root, "CauseEffectWheel", new CylinderMesh { TopRadius = 1.04f, BottomRadius = 1.04f, Height = 0.3f }, palette.Wood, new Vector3(-3.25f, 1.35f, -6.5f), new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f));
            AddMesh(root, "WheelPointer", new BoxMesh { Size = new Vector3(0.16f, 1.25f, 0.22f) }, palette.Accent, new Vector3(-3.25f, 1.35f, -5.97f), new Vector3(0.0f, 0.0f, -0.58f));
        }

        private static void BuildCharacters(Node3D root, DirectionPalette palette)
        {
            AddCitizenToken(root, "Mara_WetlandKeeper", "MARA\nKEEPER", new Vector3(-1.55f, 0.95f, -3.4f), palette.Accent, new Color("FFF1D1"));
            AddCitizenToken(root, "Ivo_Repair", "IVO\nBRACING", new Vector3(0.75f, 0.87f, -4.9f), palette.Wood, new Color("FFF1D1"), -0.34f);
            AddCitizenToken(root, "Sena_Depot", "SENA\nDEPOT", new Vector3(4.8f, 0.94f, -7.6f), palette.Reed, new Color("FFF1D1"));
            AddMesh(root, "DepotBlock", new BoxMesh { Size = new Vector3(2.4f, 1.15f, 1.65f) }, palette.Structure, new Vector3(4.8f, 0.58f, -7.8f));
        }

        private static void AddCitizenToken(Node3D root, string name, string labelText, Vector3 position, Color coat, Color labelColor, float rotation = 0.0f)
        {
            Node3D token = new() { Name = name, Position = position, Rotation = new Vector3(0.0f, rotation, 0.0f) };
            root.AddChild(token);
            AddMesh(token, "PawnBase", new CylinderMesh { TopRadius = 0.38f, BottomRadius = 0.48f, Height = 0.24f }, coat.Darkened(0.12f), new Vector3(0.0f, 0.12f, 0.0f));
            AddMesh(token, "ChunkyBody", new BoxMesh { Size = new Vector3(0.58f, 0.76f, 0.48f) }, coat, new Vector3(0.0f, 0.6f, 0.0f));
            AddMesh(token, "PaintedHeadBlock", new BoxMesh { Size = new Vector3(0.42f, 0.34f, 0.38f) }, labelColor.Darkened(0.08f), new Vector3(0.0f, 1.13f, 0.0f));
            token.AddChild(new Label3D { Name = "Identity", Text = labelText, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0.0f, 1.64f, 0.0f), FontSize = 17, Modulate = labelColor, OutlineSize = 4, OutlineModulate = new Color(0.10f, 0.07f, 0.05f) });
        }

        private static void AddReedCluster(Node3D root, Vector3 position, Color color, int index)
        {
            for (int stem = 0; stem < 5; stem++)
            {
                float x = (stem - 2) * 0.12f;
                float height = 1.1f + ((index + stem) % 4) * 0.24f;
                AddMesh(root, $"Reed{index}_{stem}", new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.04f, Height = height }, color, position + new Vector3(x, height * 0.5f, (stem % 2) * 0.07f), new Vector3(0.0f, 0.0f, x * 0.55f));
            }
        }

        private static void AddMesh(Node parent, string name, Mesh mesh, Color color, Vector3 position, Vector3 rotation = default, Color? emission = null, bool unshaded = false)
        {
            StandardMaterial3D material = CreateStudyMaterial(color, emission, unshaded);
            parent.AddChild(new MeshInstance3D { Name = name, Mesh = mesh, Position = position, Rotation = rotation, MaterialOverride = material });
        }

        /// <summary>Material factory used by the study's code-native world props and headless-safe tests.</summary>
        public static StandardMaterial3D CreateStudyMaterial(Color color, Color? emission = null, bool unshaded = false)
        {
            F1StudyMaterialProfile profile = F1VisualTargetStudyModel.GetMaterialProfile(color.A);
            StandardMaterial3D material = new()
            {
                AlbedoColor = color,
                Metallic = profile.Metallic,
                Roughness = profile.Roughness,
                ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
                Transparency = profile.UsesAlphaTransparency
                    ? BaseMaterial3D.TransparencyEnum.Alpha
                    : BaseMaterial3D.TransparencyEnum.Disabled
            };
            if (emission.HasValue)
            {
                material.EmissionEnabled = true;
                material.Emission = emission.Value;
                material.EmissionEnergyMultiplier = 0.65f;
            }
            return material;
        }

        private Panel MakePanel(string name)
        {
            Panel panel = new() { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
            panel.AddThemeStyleboxOverride("panel", MakePanelStyle(_palettes[_direction], 8));
            return panel;
        }

        private Label MakeLabel(string name, int size, HorizontalAlignment alignment)
        {
            Label label = new()
            {
                Name = name,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = alignment,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            label.AddThemeFontSizeOverride("font_size", size);
            return label;
        }

        private void RefreshInterface()
        {
            if (_uiRoot == null || _surface == null || _directionLabel == null || _straplineLabel == null || _placeLabel == null || _interactionHeadingLabel == null || _materialLabel == null || _stateMarkLabel == null || _stateLabel == null || _maraLine == null || _consequenceLine == null)
            {
                return;
            }

            DirectionPalette palette = _palettes[_direction];
            F1DirectionTreatment treatment = F1VisualTargetStudyModel.GetTreatment(_direction);
            F1ResponsePresentation presentation = F1VisualTargetStudyModel.GetPresentation(_response, _state);
            _directionLabel.Text = treatment.Title;
            _straplineLabel.Text = $"{treatment.Strapline}   •   D DETAILS";
            _placeLabel.Text = treatment.PlaceLabel;
            _interactionHeadingLabel.Text = treatment.InteractionHeading;
            _materialLabel.Text = treatment.PrimaryMaterial.ToUpperInvariant();
            _stateMarkLabel.Text = presentation.StateMark;
            _stateLabel.Text = presentation.StateLabel;
            _maraLine.Text = presentation.MaraLine;
            _consequenceLine.Text = presentation.ConsequenceLine;
            foreach (Node child in _uiRoot.GetChildren())
            {
                if (child is Panel panel)
                {
                    panel.AddThemeStyleboxOverride("panel", panel == _surface
                        ? MakeInteractionSurfaceStyle(palette, treatment.InteractionStyle)
                        : MakePanelStyle(palette, 6));
                }
            }
            _directionLabel.Modulate = palette.Text;
            _straplineLabel.Modulate = palette.Accent;
            _placeLabel.Modulate = palette.Text;
            _interactionHeadingLabel.Modulate = palette.Accent;
            _stateMarkLabel.Modulate = GetStateColor(palette, presentation.State);
            _stateLabel.Modulate = GetStateColor(palette, presentation.State);
            _maraLine.Modulate = palette.Text;
            _consequenceLine.Modulate = palette.Text.Lightened(0.12f);
            _materialLabel.Modulate = palette.Accent;
            ApplyDiagnosticsVisibility();
            ApplyLayout(GetViewport().GetVisibleRect().Size);
            UpdateWorldStateFeedback(palette, presentation);
            UpdatePhysicalControlVisuals(palette);
        }

        private void ApplyLayout(Vector2 viewport)
        {
            if (_uiRoot == null || _surface == null || viewport.X <= 0 || viewport.Y <= 0)
            {
                return;
            }
            F1StudyLayout layout = F1VisualTargetStudyModel.CalculateLayout(viewport.X, viewport.Y);
            Panel header = _uiRoot.GetNode<Panel>("Header");
            header.Position = new Vector2(layout.Margin, layout.Margin);
            header.Size = new Vector2(viewport.X - layout.Margin * 2.0f, layout.HeaderHeight);
            _directionLabel!.Position = new Vector2(18.0f, 6.0f); _directionLabel.Size = new Vector2(header.Size.X * 0.54f, 30.0f);
            _straplineLabel!.Position = new Vector2(18.0f, layout.IsCompact ? 28.0f : 38.0f); _straplineLabel.Size = new Vector2(header.Size.X * 0.62f, 22.0f);
            _placeLabel!.Position = new Vector2(header.Size.X * 0.55f, 10.0f); _placeLabel.Size = new Vector2(header.Size.X * 0.42f, 42.0f);

            F1DirectionSurfaceLayout directionLayout = F1VisualTargetStudyModel.CalculateDirectionSurfaceLayout(_direction, viewport.X, viewport.Y);
            bool usesHorizontalRail = directionLayout.UsesHorizontalPhysicalRail;
            float surfaceWidth = directionLayout.SurfaceWidth;
            float surfaceHeight = directionLayout.SurfaceHeight;
            _surface.Position = new Vector2(layout.Margin, viewport.Y - layout.Margin - surfaceHeight);
            _surface.Size = new Vector2(surfaceWidth, surfaceHeight);

            _interactionHeadingLabel!.Position = new Vector2(18.0f, 8.0f); _interactionHeadingLabel.Size = new Vector2(_surface.Size.X - 36.0f, 20.0f);
            _stateMarkLabel!.Position = new Vector2(14.0f, 28.0f); _stateMarkLabel.Size = new Vector2(36.0f, 34.0f);
            _stateLabel!.Position = new Vector2(58.0f, 29.0f); _stateLabel.Size = new Vector2(_surface.Size.X - 76.0f, 30.0f);
            _maraLine!.Position = new Vector2(18.0f, 62.0f); _maraLine.Size = new Vector2(_surface.Size.X - 36.0f, usesHorizontalRail ? 42.0f : layout.IsCompact ? 48.0f : 56.0f);
            _consequenceLine!.Position = new Vector2(18.0f, usesHorizontalRail ? 104.0f : layout.IsCompact ? 112.0f : 124.0f); _consequenceLine.Size = new Vector2(_surface.Size.X - 36.0f, usesHorizontalRail ? 28.0f : 38.0f);
            _materialLabel!.Position = new Vector2(18.0f, _surface.Size.Y - 25.0f); _materialLabel.Size = new Vector2(_surface.Size.X - 36.0f, 16.0f);
            _hintLabel!.Position = new Vector2(viewport.X - layout.Margin - 360.0f, viewport.Y - layout.Margin - 18.0f);
            _hintLabel.Size = new Vector2(360.0f, 18.0f);
        }

        private static StyleBoxFlat MakePanelStyle(DirectionPalette palette, int cornerRadius)
        {
            return new StyleBoxFlat
            {
                BgColor = WithAlpha(palette.Panel, 0.91f),
                BorderColor = new Color(palette.Accent, 0.72f),
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                CornerRadiusTopLeft = cornerRadius, CornerRadiusTopRight = cornerRadius,
                CornerRadiusBottomLeft = cornerRadius, CornerRadiusBottomRight = cornerRadius,
                ContentMarginLeft = 10.0f, ContentMarginRight = 10.0f
            };
        }

        private static StyleBoxFlat MakeInteractionSurfaceStyle(DirectionPalette palette, F1InteractionSurfaceStyle style)
        {
            StyleBoxFlat surface = style switch
            {
                F1InteractionSurfaceStyle.HandboundLedger => MakePanelStyle(palette, 16),
                F1InteractionSurfaceStyle.KilnTileNotice => MakePanelStyle(palette, 4),
                _ => MakePanelStyle(palette, 0)
            };

            switch (style)
            {
                case F1InteractionSurfaceStyle.HandboundLedger:
                    surface.BorderWidthLeft = 5;
                    surface.BorderWidthTop = 2;
                    break;
                case F1InteractionSurfaceStyle.KilnTileNotice:
                    surface.BorderWidthTop = 4;
                    surface.BorderWidthBottom = 4;
                    // Keep the board noticeably distinct from the other treatments without
                    // sacrificing contrast for the shared light-on-dark study typography.
                    surface.BgColor = WithAlpha(palette.Panel.Lightened(0.10f), 0.98f);
                    break;
                case F1InteractionSurfaceStyle.PaintedControlRail:
                    surface.BorderWidthTop = 2;
                    surface.BorderWidthBottom = 2;
                    surface.BorderWidthLeft = 0;
                    surface.BorderWidthRight = 0;
                    break;
            }

            return surface;
        }

        private void BuildPhysicalControls(Node3D root, DirectionPalette palette)
        {
            F1DirectionTreatment treatment = F1VisualTargetStudyModel.GetTreatment(_direction);
            for (int index = 0; index < F1VisualTargetStudyModel.OrderedDirections.Count; index++)
            {
                F1StudyDirection direction = F1VisualTargetStudyModel.OrderedDirections[index];
                string label = direction switch
                {
                    F1StudyDirection.HearthwoodCauseway => "1  HEARTHWOOD",
                    F1StudyDirection.ReedKilnWetlands => "2  REED-KILN",
                    _ => "3  TOYWORKS"
                };
                CreatePhysicalControl(
                    root,
                    $"direction:{direction}",
                    "F1DirectionHitTarget",
                    label,
                    new BoxMesh { Size = new Vector3(2.45f, 0.32f, 0.86f) },
                    new Vector3(-4.2f + index * 4.2f, 0.34f, 1.0f),
                    new Vector3(2.55f, 0.55f, 1.05f));
            }

            switch (_direction)
            {
                case F1StudyDirection.HearthwoodCauseway:
                    CreatePhysicalControl(root, "response:OfferLabor", "F1ResponseHitTarget", treatment.LaborControl, new BoxMesh { Size = new Vector3(2.35f, 0.40f, 0.92f) }, new Vector3(-5.4f, 0.38f, -1.0f), new Vector3(2.55f, 0.65f, 1.12f));
                    CreatePhysicalControl(root, "response:AskForEvidence", "F1ResponseHitTarget", treatment.EvidenceControl, new BoxMesh { Size = new Vector3(2.35f, 0.40f, 0.92f) }, new Vector3(-5.4f, 0.38f, -2.35f), new Vector3(2.55f, 0.65f, 1.12f));
                    CreatePhysicalControl(root, "response:Defer", "F1ResponseHitTarget", treatment.DeferControl, new BoxMesh { Size = new Vector3(2.35f, 0.40f, 0.92f) }, new Vector3(-5.4f, 0.38f, -3.70f), new Vector3(2.55f, 0.65f, 1.12f));
                    break;
                case F1StudyDirection.ReedKilnWetlands:
                    CreatePhysicalControl(root, "response:OfferLabor", "F1ResponseHitTarget", treatment.LaborControl, new CylinderMesh { TopRadius = 0.92f, BottomRadius = 1.04f, Height = 0.30f }, new Vector3(-4.7f, 0.30f, -1.8f), new Vector3(2.25f, 0.55f, 2.25f));
                    CreatePhysicalControl(root, "response:AskForEvidence", "F1ResponseHitTarget", treatment.EvidenceControl, new CylinderMesh { TopRadius = 0.92f, BottomRadius = 1.04f, Height = 0.30f }, new Vector3(-2.45f, 0.30f, -2.85f), new Vector3(2.25f, 0.55f, 2.25f));
                    CreatePhysicalControl(root, "response:Defer", "F1ResponseHitTarget", treatment.DeferControl, new CylinderMesh { TopRadius = 0.92f, BottomRadius = 1.04f, Height = 0.30f }, new Vector3(-0.20f, 0.30f, -1.8f), new Vector3(2.25f, 0.55f, 2.25f));
                    break;
                default:
                    CreatePhysicalControl(root, "response:OfferLabor", "F1ResponseHitTarget", treatment.LaborControl, new BoxMesh { Size = new Vector3(2.18f, 0.36f, 0.86f) }, new Vector3(-4.3f, 0.34f, -1.6f), new Vector3(2.38f, 0.55f, 1.06f));
                    CreatePhysicalControl(root, "response:AskForEvidence", "F1ResponseHitTarget", treatment.EvidenceControl, new BoxMesh { Size = new Vector3(2.18f, 0.36f, 0.86f) }, new Vector3(-1.95f, 0.34f, -1.6f), new Vector3(2.38f, 0.55f, 1.06f));
                    CreatePhysicalControl(root, "response:Defer", "F1ResponseHitTarget", treatment.DeferControl, new BoxMesh { Size = new Vector3(2.18f, 0.36f, 0.86f) }, new Vector3(0.40f, 0.34f, -1.6f), new Vector3(2.38f, 0.55f, 1.06f));
                    break;
            }

            CreatePhysicalControl(root, "advance", "F1ResponseHitTarget", "SPACE  TURN THE RESULT TILE", new BoxMesh { Size = new Vector3(3.65f, 0.34f, 0.84f) }, new Vector3(4.25f, 0.34f, -1.7f), new Vector3(3.9f, 0.55f, 1.04f));
        }

        private void CreatePhysicalControl(Node3D root, string controlId, string groupName, string labelText, Mesh mesh, Vector3 position, Vector3 hitSize)
        {
            StaticBody3D hitTarget = new()
            {
                Name = $"{controlId.Replace(':', '_')}HitTarget",
                Position = position
            };
            hitTarget.AddToGroup("F1PhysicalControl");
            hitTarget.AddToGroup(groupName);
            hitTarget.SetMeta("f1_physical_control_id", controlId);
            root.AddChild(hitTarget);

            MeshInstance3D visiblePiece = new()
            {
                Name = $"{controlId.Replace(':', '_')}Piece",
                Mesh = mesh
            };
            hitTarget.AddChild(visiblePiece);
            hitTarget.AddChild(new CollisionShape3D
            {
                Name = "PointerRaycastShape",
                Shape = new BoxShape3D { Size = hitSize }
            });
            Label3D label = new()
            {
                Name = "ControlLabel",
                Text = labelText,
                Position = new Vector3(0.0f, hitSize.Y * 0.58f, 0.0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 24,
                OutlineSize = 4,
                OutlineModulate = new Color("1B1510")
            };
            hitTarget.AddChild(label);
            _physicalControls[controlId] = new PhysicalControlPiece(hitTarget, visiblePiece, label, hitSize);
        }

        private static void BuildWorldStateFeedback(Node3D root, DirectionPalette palette)
        {
            MeshInstance3D feedbackTile = new()
            {
                Name = "WorldStateFeedbackTile",
                Mesh = new BoxMesh { Size = new Vector3(3.7f, 0.24f, 0.95f) },
                Position = new Vector3(4.25f, 0.48f, -3.05f),
                MaterialOverride = CreateStudyMaterial(palette.Panel)
            };
            root.AddChild(feedbackTile);
            root.AddChild(new Label3D
            {
                Name = "WorldStateFeedback",
                Text = "◇  POSITION OPEN",
                Position = new Vector3(4.25f, 0.92f, -3.05f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 27,
                OutlineSize = 4,
                OutlineModulate = new Color("1B1510"),
                Modulate = palette.Text
            });
        }

        private void UpdateWorldStateFeedback(DirectionPalette palette, F1ResponsePresentation presentation)
        {
            if (_worldRoot?.GetNodeOrNull<Label3D>("WorldStateFeedback") is Label3D label)
            {
                label.Text = $"{presentation.StateMark}  {presentation.StateLabel}";
                label.Modulate = GetStateColor(palette, presentation.State);
            }
            if (_worldRoot?.GetNodeOrNull<MeshInstance3D>("WorldStateFeedbackTile") is MeshInstance3D tile)
            {
                tile.MaterialOverride = CreateStudyMaterial(GetStateColor(palette, presentation.State).Darkened(0.55f));
            }
        }

        private void UpdatePhysicalControlVisuals(DirectionPalette palette)
        {
            F1PhysicalControlColors colors = F1VisualTargetStudyModel.GetPhysicalControlColors(_direction);
            foreach ((string controlId, PhysicalControlPiece piece) in _physicalControls)
            {
                bool selectedDirection = controlId == $"direction:{_direction}";
                bool selectedResponse = controlId == $"response:{_response}" && _response != F1StudyResponse.None;
                bool advancesResult = controlId == "advance" && _state != F1StudyState.Awaiting;
                bool pressed = selectedDirection || selectedResponse || advancesResult;
                bool hovered = controlId == _hoveredPhysicalControl && !pressed;
                Color background = new(pressed ? colors.PressedBackgroundHex : hovered ? colors.HoverBackgroundHex : colors.NormalBackgroundHex);
                Color foreground = new(pressed ? colors.PressedForegroundHex : hovered ? colors.HoverForegroundHex : colors.NormalForegroundHex);
                piece.Mesh.MaterialOverride = CreateStudyMaterial(background);
                piece.Label.Modulate = foreground;
            }
        }

        private void SetHoveredPhysicalControl(string? controlId)
        {
            if (_hoveredPhysicalControl == controlId)
            {
                return;
            }
            _hoveredPhysicalControl = controlId;
            UpdatePhysicalControlVisuals(_palettes[_direction]);
        }

        private string? ResolvePhysicalControlAt(Vector2 screenPosition)
        {
            if (_tabletopCamera == null || !HasPointerControlSurface)
            {
                return null;
            }
            Vector3 origin = _tabletopCamera.ProjectRayOrigin(screenPosition);
            Vector3 end = origin + _tabletopCamera.ProjectRayNormal(screenPosition) * 100.0f;
            Godot.Collections.Dictionary result = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(origin, end));
            if (!result.ContainsKey("collider") || result["collider"].AsGodotObject() is not Node hitTarget)
            {
                return null;
            }
            return hitTarget.HasMeta("f1_physical_control_id")
                ? hitTarget.GetMeta("f1_physical_control_id").AsString()
                : null;
        }

        private bool TryActivatePhysicalControl(string? controlId)
        {
            if (string.IsNullOrEmpty(controlId))
            {
                return false;
            }
            foreach (F1StudyDirection direction in F1VisualTargetStudyModel.OrderedDirections)
            {
                if (controlId == $"direction:{direction}")
                {
                    SetDirection(direction);
                    return true;
                }
            }
            if (controlId == "response:OfferLabor") { SelectResponse(F1StudyResponse.OfferLabor); return true; }
            if (controlId == "response:AskForEvidence") { SelectResponse(F1StudyResponse.AskForEvidence); return true; }
            if (controlId == "response:Defer") { SelectResponse(F1StudyResponse.Defer); return true; }
            if (controlId == "advance") { AdvanceStudyState(); return true; }
            return false;
        }

        private void SetDirection(F1StudyDirection direction)
        {
            _direction = direction;
            ApplyDirection();
        }

        private void SelectResponse(F1StudyResponse response)
        {
            _response = response;
            _state = F1VisualTargetStudyModel.NextState(_response, F1StudyState.Awaiting);
            RefreshInterface();
        }

        private void AdvanceStudyState()
        {
            _state = F1VisualTargetStudyModel.NextState(_response, _state);
            if (_state == F1StudyState.Awaiting)
            {
                _response = F1StudyResponse.None;
            }
            RefreshInterface();
        }

        private void ToggleReducedMotion()
        {
            _reducedMotion = !_reducedMotion;
            UpdateReducedMotionHint();
        }

        private void UpdateReducedMotionHint()
        {
            if (_hintLabel != null)
            {
                _hintLabel.Text = _reducedMotion
                    ? "STATIC-SAFE VIEW   •   R restores subtle motion"
                    : "1 / 2 / 3 direction   •   R reduced motion";
            }
        }

        private void ToggleDiagnostics()
        {
            _diagnosticsVisible = !_diagnosticsVisible;
            ApplyDiagnosticsVisibility();
        }

        private void ApplyDiagnosticsVisibility()
        {
            if (_surface != null)
            {
                _surface.Visible = _diagnosticsVisible;
            }
            // The top header carries the compact treatment identity. Essential choice and state
            // feedback remain on the tabletop pieces, so this optional detail layer never owns input.
            if (_hintLabel != null)
            {
                _hintLabel.Visible = false;
            }
        }

        private static Color GetStateColor(DirectionPalette palette, F1StudyState state) => state switch
        {
            F1StudyState.Pending => palette.Accent,
            F1StudyState.Refused => new Color("F08A5D"),
            F1StudyState.Consequence => new Color("9EE3B6"),
            _ => palette.Text
        };

        private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

        private sealed record DirectionPalette(
            Color Sky,
            Color Water,
            Color Reed,
            Color Structure,
            Color Accent,
            Color Text,
            Color Panel,
            Color Mist,
            Color Table,
            Color Wood,
            Color Clay,
            Color Felt);

        private sealed record PhysicalControlPiece(StaticBody3D HitTarget, MeshInstance3D Mesh, Label3D Label, Vector3 HitSize);
    }
}
