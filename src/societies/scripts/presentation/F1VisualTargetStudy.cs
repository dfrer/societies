using Godot;
using System;
using System.Collections.Generic;
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
            [F1StudyDirection.ReedworkFoundry] = new(
                new Color("17262A"), new Color("2A4A4A"), new Color("65714F"), new Color("9A4E31"),
                new Color("E0A34C"), new Color("D8E8DB"), new Color("1B2423"), new Color("405C56")),
            [F1StudyDirection.FloodplainCommons] = new(
                new Color("6B796C"), new Color("63765E"), new Color("7B5837"), new Color("CA5F31"),
                new Color("E9B24D"), new Color("FFF2CE"), new Color("2A3029"), new Color("304A6B")),
            [F1StudyDirection.SluiceObservatory] = new(
                new Color("102731"), new Color("1E5D69"), new Color("D9D7C6"), new Color("798E85"),
                new Color("86D8C8"), new Color("EFF5E8"), new Color("102125"), new Color("3F7378"))
        };

        private Node3D? _worldRoot;
        private Node3D? _mistRoot;
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
        private Button? _advanceButton;
        private Vector2 _lastViewportSize;
        private double _visualTime;
        private F1StudyDirection _direction = F1StudyDirection.ReedworkFoundry;
        private F1StudyResponse _response = F1StudyResponse.None;
        private F1StudyState _state = F1StudyState.Awaiting;
        private bool _reducedMotion;

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

            if (_reducedMotion || _mistRoot == null)
            {
                return;
            }

            _visualTime += delta;
            _mistRoot.Position = new Vector3(Mathf.Sin((float)_visualTime * 0.22f) * 0.25f, 0.0f, 0.0f);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            {
                return;
            }

            switch (key.Keycode)
            {
                case Key.Key1: SetDirection(F1StudyDirection.ReedworkFoundry); break;
                case Key.Key2: SetDirection(F1StudyDirection.FloodplainCommons); break;
                case Key.Key3: SetDirection(F1StudyDirection.SluiceObservatory); break;
                case Key.Q: SelectResponse(F1StudyResponse.OfferLabor); break;
                case Key.W: SelectResponse(F1StudyResponse.AskForEvidence); break;
                case Key.E: SelectResponse(F1StudyResponse.Defer); break;
                case Key.Space: AdvanceStudyState(); break;
                case Key.R: ToggleReducedMotion(); break;
                default: return;
            }

            // Run at the input stage so Space remains reliable even when a Button owns focus.
            // The visible Advance button provides the equivalent pointer/focus activation path.
            GetViewport().SetInputAsHandled();
        }

        private void BuildCameraAndLight()
        {
            WorldEnvironment environment = new()
            {
                Environment = new Godot.Environment
                {
                    BackgroundMode = Godot.Environment.BGMode.Color,
                    BackgroundColor = new Color("17262A"),
                    AmbientLightSource = Godot.Environment.AmbientSource.Color,
                    AmbientLightColor = new Color("A9C2BE"),
                    AmbientLightEnergy = 0.7f,
                    FogEnabled = true,
                    FogLightColor = new Color("A9C2BE"),
                    FogDensity = 0.015f
                }
            };
            AddChild(environment);

            DirectionalLight3D key = new()
            {
                RotationDegrees = new Vector3(-54.0f, -28.0f, 0.0f),
                LightColor = new Color("D7E8D7"),
                LightEnergy = 1.3f,
                ShadowEnabled = true
            };
            AddChild(key);

            Camera3D camera = new()
            {
                Position = new Vector3(0.0f, 4.0f, 10.5f),
                Fov = 66.0f,
                Current = true
            };
            AddChild(camera);
            camera.LookAt(new Vector3(0.0f, 1.2f, -5.8f), Vector3.Up);
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

            Button labor = MakeResponseButton("Labor", "Q  OFFER LABOR", F1StudyResponse.OfferLabor);
            Button evidence = MakeResponseButton("Evidence", "W  ASK FOR EVIDENCE", F1StudyResponse.AskForEvidence);
            Button defer = MakeResponseButton("Defer", "E  DEFER", F1StudyResponse.Defer);
            _advanceButton = MakeResponseButton("Advance", "SPACE  SEE CONSEQUENCE", F1StudyResponse.None);
            _advanceButton.Pressed += AdvanceStudyState;
            _uiRoot.AddChild(labor);
            _uiRoot.AddChild(evidence);
            _uiRoot.AddChild(defer);
            _uiRoot.AddChild(_advanceButton);

            _hintLabel = MakeLabel("Hint", 11, HorizontalAlignment.Right);
            _hintLabel.Text = "1 / 2 / 3 direction   •   R reduced motion";
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
                environment.Environment.FogLightColor = palette.Mist;
            }

            _worldRoot?.QueueFree();
            _worldRoot = new Node3D { Name = $"{_direction}World" };
            AddChild(_worldRoot);
            BuildWetlandMoment(_worldRoot, palette);
            RefreshInterface();
        }

        private void BuildWetlandMoment(Node3D root, DirectionPalette palette)
        {
            AddMesh(root, "Water", new PlaneMesh { Size = new Vector2(50.0f, 34.0f) }, palette.Water, new Vector3(0.0f, -0.05f, -7.0f), emission: palette.Water.Darkened(0.7f));
            AddMesh(root, "CausewayBase", new BoxMesh { Size = new Vector3(5.2f, 0.7f, 17.0f) }, palette.Structure, new Vector3(0.0f, 0.28f, -5.8f));
            AddMesh(root, "CausewaySplit", new BoxMesh { Size = new Vector3(0.34f, 0.1f, 4.8f) }, palette.Accent, new Vector3(-0.6f, 0.66f, -5.7f), new Vector3(0.0f, 0.18f, 0.0f));

            for (int index = 0; index < 28; index++)
            {
                float x = -9.0f + (index % 7) * 3.0f + ((index / 7) % 2) * 0.6f;
                float z = -1.5f - (index / 7) * 4.5f;
                AddReedCluster(root, new Vector3(x, 0.0f, z), palette.Reed, index);
            }

            switch (_direction)
            {
                case F1StudyDirection.ReedworkFoundry:
                    BuildReedworkFoundry(root, palette);
                    break;
                case F1StudyDirection.FloodplainCommons:
                    BuildFloodplainCommons(root, palette);
                    break;
                default:
                    BuildSluiceObservatory(root, palette);
                    break;
            }

            BuildCharacters(root, palette);
            _mistRoot = new Node3D { Name = "AtmosphereMist" };
            root.AddChild(_mistRoot);
            for (int index = 0; index < 5; index++)
            {
                AddMesh(_mistRoot, $"Mist{index}", new QuadMesh { Size = new Vector2(7.0f, 1.6f) }, WithAlpha(palette.Mist, 0.12f), new Vector3(-7.0f + index * 3.4f, 1.0f + (index % 2) * 0.45f, -6.5f - index * 1.1f), new Vector3(0.0f, 0.15f, 0.0f), unshaded: true);
            }
        }

        private static void BuildReedworkFoundry(Node3D root, DirectionPalette palette)
        {
            for (int index = 0; index < 8; index++)
            {
                AddMesh(root, $"ReedRib{index}", new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.13f, Height = 4.7f }, palette.Reed, new Vector3(-2.0f + index * 0.57f, 2.35f, -4.5f), new Vector3(0.0f, 0.0f, -0.22f + index * 0.055f));
            }
            AddMesh(root, "FoundryFrame", new BoxMesh { Size = new Vector3(4.9f, 0.18f, 0.18f) }, palette.Accent, new Vector3(0.0f, 3.9f, -4.5f));
            OmniLight3D lamp = new() { Position = new Vector3(0.0f, 3.35f, -3.9f), LightColor = palette.Accent, LightEnergy = 2.6f, OmniRange = 10.0f };
            root.AddChild(lamp);
        }

        private static void BuildFloodplainCommons(Node3D root, DirectionPalette palette)
        {
            AddMesh(root, "NoticeBoard", new BoxMesh { Size = new Vector3(2.5f, 1.55f, 0.12f) }, new Color("E9DDB8"), new Vector3(-3.8f, 2.0f, -4.8f));
            AddMesh(root, "NoticeStripe", new BoxMesh { Size = new Vector3(2.25f, 0.18f, 0.15f) }, palette.Accent, new Vector3(-3.8f, 2.25f, -4.7f));
            AddMesh(root, "CanvasAwning", new QuadMesh { Size = new Vector2(4.7f, 2.2f) }, new Color(palette.Mist, 0.88f), new Vector3(1.3f, 3.3f, -4.2f), new Vector3(-0.26f, 0.0f, 0.0f));
            for (int index = 0; index < 4; index++)
            {
                AddMesh(root, $"TimberPost{index}", new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.15f, Height = 3.5f }, palette.Structure, new Vector3(-0.7f + index * 1.35f, 1.75f, -4.2f));
            }
        }

        private static void BuildSluiceObservatory(Node3D root, DirectionPalette palette)
        {
            AddMesh(root, "MonolithLeft", new BoxMesh { Size = new Vector3(1.3f, 5.5f, 1.5f) }, palette.Reed, new Vector3(-3.5f, 2.75f, -5.6f));
            AddMesh(root, "MonolithRight", new BoxMesh { Size = new Vector3(1.3f, 5.5f, 1.5f) }, palette.Reed, new Vector3(3.5f, 2.75f, -5.6f));
            AddMesh(root, "GaugeGlass", new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.42f, Height = 2.3f }, palette.Accent, new Vector3(0.0f, 2.0f, -5.3f), emission: palette.Accent);
            AddMesh(root, "GateLine", new BoxMesh { Size = new Vector3(6.2f, 0.2f, 0.22f) }, palette.Structure, new Vector3(0.0f, 4.3f, -5.2f));
            SpotLight3D beam = new() { Position = new Vector3(0.0f, 5.4f, -2.2f), RotationDegrees = new Vector3(-55.0f, 180.0f, 0.0f), LightColor = palette.Accent, LightEnergy = 3.0f, SpotRange = 16.0f, SpotAngle = 30.0f };
            root.AddChild(beam);
        }

        private static void BuildCharacters(Node3D root, DirectionPalette palette)
        {
            AddFigure(root, "Mara_WetlandKeeper", "MARA\nKEEPER", new Vector3(-1.55f, 0.85f, -3.4f), palette.Accent, new Color("E9E3CE"));
            AddFigure(root, "Ivo_Repair", "IVO\nBRACING", new Vector3(0.75f, 0.72f, -4.9f), palette.Structure, new Color("F0D9A0"), -0.34f);
            AddFigure(root, "Sena_Depot", "SENA\nDEPOT", new Vector3(4.8f, 0.82f, -7.6f), palette.Reed, new Color("D1E6D7"));
            AddMesh(root, "Depot", new BoxMesh { Size = new Vector3(2.2f, 1.0f, 1.5f) }, palette.Structure, new Vector3(4.8f, 0.5f, -7.8f));
        }

        private static void AddFigure(Node3D root, string name, string labelText, Vector3 position, Color coat, Color labelColor, float rotation = 0.0f)
        {
            Node3D figure = new() { Name = name, Position = position, Rotation = new Vector3(0.0f, rotation, 0.0f) };
            root.AddChild(figure);
            AddMesh(figure, "Body", new CapsuleMesh { Radius = 0.27f, Height = 1.45f }, coat, new Vector3(0.0f, 0.72f, 0.0f));
            AddMesh(figure, "Head", new SphereMesh { Radius = 0.23f, Height = 0.46f }, new Color("9E7354"), new Vector3(0.0f, 1.55f, 0.0f));
            figure.AddChild(new Label3D { Name = "Identity", Text = labelText, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0.0f, 2.2f, 0.0f), FontSize = 18, Modulate = labelColor, OutlineSize = 5, OutlineModulate = new Color(0.04f, 0.08f, 0.08f) });
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
            StandardMaterial3D material = new()
            {
                AlbedoColor = color,
                Roughness = 0.78f,
                ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
                Transparency = F1VisualTargetStudyModel.ShouldUseAlphaTransparency(color.A)
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

        private Button MakeResponseButton(string name, string text, F1StudyResponse response)
        {
            Button button = new() { Name = name, Text = text, TooltipText = text.Replace("  ", ": "), FocusMode = Control.FocusModeEnum.All };
            button.AddThemeFontSizeOverride("font_size", 12);
            button.Pressed += () =>
            {
                if (response != F1StudyResponse.None)
                {
                    SelectResponse(response);
                }
            };
            return button;
        }

        private void ApplyDirectionControls(F1DirectionTreatment treatment)
        {
            if (_uiRoot == null)
            {
                return;
            }

            ApplyControlCopy(_uiRoot.GetNode<Button>("Labor"), treatment.LaborControl, "Offer labor");
            ApplyControlCopy(_uiRoot.GetNode<Button>("Evidence"), treatment.EvidenceControl, "Ask for evidence");
            ApplyControlCopy(_uiRoot.GetNode<Button>("Defer"), treatment.DeferControl, "Defer a commitment");
        }

        private static void ApplyControlCopy(Button button, string text, string semanticChoice)
        {
            button.Text = text;
            button.TooltipText = $"{semanticChoice}: {text.Replace("  ", ": ")}";
        }

        private void RefreshInterface()
        {
            if (_uiRoot == null || _surface == null || _directionLabel == null || _straplineLabel == null || _placeLabel == null || _interactionHeadingLabel == null || _materialLabel == null || _stateMarkLabel == null || _stateLabel == null || _maraLine == null || _consequenceLine == null || _advanceButton == null)
            {
                return;
            }

            DirectionPalette palette = _palettes[_direction];
            F1DirectionTreatment treatment = F1VisualTargetStudyModel.GetTreatment(_direction);
            F1ResponsePresentation presentation = F1VisualTargetStudyModel.GetPresentation(_response, _state);
            _directionLabel.Text = treatment.Title;
            _straplineLabel.Text = treatment.Strapline;
            _placeLabel.Text = treatment.PlaceLabel;
            _interactionHeadingLabel.Text = treatment.InteractionHeading;
            _materialLabel.Text = treatment.PrimaryMaterial.ToUpperInvariant();
            _stateMarkLabel.Text = presentation.StateMark;
            _stateLabel.Text = presentation.StateLabel;
            _maraLine.Text = presentation.MaraLine;
            _consequenceLine.Text = presentation.ConsequenceLine;
            _advanceButton.Visible = presentation.AllowsAdvance;
            _advanceButton.Text = _state == F1StudyState.Refused || _state == F1StudyState.Consequence
                ? "SPACE  RESET POSITION"
                : "SPACE  SEE CONSEQUENCE";

            ApplyDirectionControls(treatment);

            foreach (Node child in _uiRoot.GetChildren())
            {
                if (child is Panel panel)
                {
                    panel.AddThemeStyleboxOverride("panel", panel == _surface
                        ? MakeInteractionSurfaceStyle(palette, treatment.InteractionStyle)
                        : MakePanelStyle(palette, 6));
                }
                else if (child is Button button)
                {
                    ApplyButtonStyle(button, palette, treatment.Direction, treatment.InteractionStyle);
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
            ApplyLayout(GetViewport().GetVisibleRect().Size);
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

            F1DirectionTreatment treatment = F1VisualTargetStudyModel.GetTreatment(_direction);
            float preferredWidth = treatment.InteractionStyle switch
            {
                F1InteractionSurfaceStyle.PublicNotice => layout.IsCompact ? 520.0f : 660.0f,
                F1InteractionSurfaceStyle.CalibrationRail => layout.IsCompact ? 560.0f : 780.0f,
                _ => layout.SurfaceWidth
            };
            bool usesHorizontalRail = treatment.InteractionStyle != F1InteractionSurfaceStyle.InstrumentStack;
            float surfaceWidth = MathF.Min(preferredWidth, viewport.X - layout.Margin * 2.0f);
            float surfaceHeight = layout.SurfaceHeight + (usesHorizontalRail ? 34.0f : 0.0f);
            _surface.Position = new Vector2(layout.Margin, viewport.Y - layout.Margin - surfaceHeight);
            _surface.Size = new Vector2(surfaceWidth, surfaceHeight);

            _interactionHeadingLabel!.Position = new Vector2(18.0f, 8.0f); _interactionHeadingLabel.Size = new Vector2(_surface.Size.X - 36.0f, 20.0f);
            _stateMarkLabel!.Position = new Vector2(14.0f, 28.0f); _stateMarkLabel.Size = new Vector2(36.0f, 34.0f);
            _stateLabel!.Position = new Vector2(58.0f, 29.0f); _stateLabel.Size = new Vector2(_surface.Size.X - 76.0f, 30.0f);
            _maraLine!.Position = new Vector2(18.0f, 62.0f); _maraLine.Size = new Vector2(_surface.Size.X - 36.0f, usesHorizontalRail ? 42.0f : layout.IsCompact ? 48.0f : 56.0f);
            _consequenceLine!.Position = new Vector2(18.0f, usesHorizontalRail ? 104.0f : layout.IsCompact ? 112.0f : 124.0f); _consequenceLine.Size = new Vector2(_surface.Size.X - 36.0f, usesHorizontalRail ? 28.0f : 38.0f);
            _materialLabel!.Position = new Vector2(18.0f, _surface.Size.Y - 25.0f); _materialLabel.Size = new Vector2(_surface.Size.X - 36.0f, 16.0f);

            Button labor = _uiRoot.GetNode<Button>("Labor"); Button evidence = _uiRoot.GetNode<Button>("Evidence"); Button defer = _uiRoot.GetNode<Button>("Defer");
            Button[] choices = { labor, evidence, defer };
            if (!usesHorizontalRail)
            {
                float buttonX = _surface.Position.X + _surface.Size.X + 12.0f;
                float available = viewport.X - buttonX - layout.Margin;
                bool stackButtons = available < 230.0f;
                float choiceWidth = stackButtons ? MathF.Max(174.0f, _surface.Size.X) : MathF.Min(260.0f, available);
                for (int index = 0; index < choices.Length; index++)
                {
                    choices[index].Position = stackButtons
                        ? new Vector2(_surface.Position.X, _surface.Position.Y - 46.0f * (3 - index))
                        : new Vector2(buttonX, _surface.Position.Y + index * 48.0f);
                    choices[index].Size = new Vector2(choiceWidth, 38.0f);
                }
                _advanceButton!.Position = stackButtons
                    ? new Vector2(_surface.Position.X, _surface.Position.Y - 184.0f)
                    : new Vector2(buttonX, _surface.Position.Y + 154.0f);
                _advanceButton.Size = new Vector2(choiceWidth, 38.0f);
            }
            else
            {
                float gutter = 10.0f;
                float controlWidth = (_surface.Size.X - 36.0f - gutter * 2.0f) / 3.0f;
                float controlY = _surface.Size.Y - 66.0f;
                for (int index = 0; index < choices.Length; index++)
                {
                    choices[index].Position = _surface.Position + new Vector2(18.0f + index * (controlWidth + gutter), controlY);
                    choices[index].Size = new Vector2(controlWidth, 34.0f);
                }
                _advanceButton!.Position = _surface.Position + new Vector2(18.0f, controlY - 40.0f);
                _advanceButton.Size = new Vector2(_surface.Size.X - 36.0f, 32.0f);
            }
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
                F1InteractionSurfaceStyle.InstrumentStack => MakePanelStyle(palette, 16),
                F1InteractionSurfaceStyle.PublicNotice => MakePanelStyle(palette, 0),
                _ => MakePanelStyle(palette, 0)
            };

            switch (style)
            {
                case F1InteractionSurfaceStyle.InstrumentStack:
                    surface.BorderWidthLeft = 5;
                    surface.BorderWidthTop = 2;
                    break;
                case F1InteractionSurfaceStyle.PublicNotice:
                    surface.BorderWidthTop = 4;
                    surface.BorderWidthBottom = 4;
                    // Keep the board noticeably distinct from the other treatments without
                    // sacrificing contrast for the shared light-on-dark study typography.
                    surface.BgColor = WithAlpha(palette.Panel.Lightened(0.10f), 0.98f);
                    break;
                case F1InteractionSurfaceStyle.CalibrationRail:
                    surface.BorderWidthTop = 2;
                    surface.BorderWidthBottom = 2;
                    surface.BorderWidthLeft = 0;
                    surface.BorderWidthRight = 0;
                    break;
            }

            return surface;
        }

        private static void ApplyButtonStyle(Button button, DirectionPalette palette, F1StudyDirection direction, F1InteractionSurfaceStyle style)
        {
            int radius = style == F1InteractionSurfaceStyle.InstrumentStack ? 10 : 0;
            F1PressedControlColors pressedColors = F1VisualTargetStudyModel.GetPressedControlColors(direction);
            StyleBoxFlat normal = MakePanelStyle(palette, radius); normal.BgColor = WithAlpha(palette.Panel, 0.92f);
            StyleBoxFlat hover = MakePanelStyle(palette, radius); hover.BgColor = WithAlpha(palette.Accent, 0.28f);
            StyleBoxFlat pressed = MakePanelStyle(palette, radius); pressed.BgColor = new Color(pressedColors.BackgroundHex);
            pressed.BorderColor = palette.Accent.Lightened(0.20f);
            pressed.BorderWidthTop = 3; pressed.BorderWidthBottom = 3;
            if (style == F1InteractionSurfaceStyle.CalibrationRail)
            {
                normal.BorderWidthLeft = 0; normal.BorderWidthRight = 0;
                hover.BorderWidthLeft = 0; hover.BorderWidthRight = 0;
                pressed.BorderWidthLeft = 0; pressed.BorderWidthRight = 0;
            }
            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("pressed", pressed);
            button.AddThemeColorOverride("font_color", palette.Text);
            button.AddThemeColorOverride("font_hover_color", palette.Text);
            button.AddThemeColorOverride("font_pressed_color", new Color(pressedColors.ForegroundHex));
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
            if (_hintLabel != null)
            {
                _hintLabel.Text = _reducedMotion
                    ? "STATIC-SAFE VIEW   •   R restores subtle motion"
                    : "1 / 2 / 3 direction   •   R reduced motion";
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

        private sealed record DirectionPalette(Color Sky, Color Water, Color Reed, Color Structure, Color Accent, Color Text, Color Panel, Color Mist);
    }
}
