using Godot;
using Societies.Core;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Simulation
{
    /// <summary>
    /// World-space presentation for the M2 settlement center, hearth, and structure markers.
    /// </summary>
    public partial class PrototypeSettlementHub : Node3D
    {
        private readonly List<MeshInstance3D> _stockpileCrates = new();
        private readonly Dictionary<string, Node3D> _structureMarkers = new();
        private MeshInstance3D? _hearthFlame;
        private OmniLight3D? _hearthLight;
        private Label3D? _label;
        private Node3D? _stockpileRoot;
        private Node3D? _woodYardRoot;
        private Node3D? _hearthRoot;
        private Node3D? _structureRoot;
        private double _animationTime;

        public string StatusText => _label?.Text ?? string.Empty;

        public bool IsHearthLit => _hearthFlame?.Visible == true;

        public bool IsCampfireLit => IsHearthLit;

        public override void _Ready()
        {
            BuildVisuals();
        }

        public override void _Process(double delta)
        {
            _animationTime += delta;

            if (_hearthFlame?.Visible == true)
            {
                float pulse = 0.9f + Mathf.Sin((float)_animationTime * 5.6f) * 0.08f;
                _hearthFlame.Scale = new Vector3(1.0f, pulse, 1.0f);
            }

            if (_hearthLight != null)
            {
                _hearthLight.LightEnergy = _hearthFlame?.Visible == true
                    ? 1.35f + Mathf.Sin((float)_animationTime * 7.2f) * 0.15f
                    : 0.0f;
            }

            foreach (Node3D marker in _structureMarkers.Values)
            {
                if (marker.GetMeta("pulse").AsBool())
                {
                    float scale = 1.0f + Mathf.Sin((float)_animationTime * 4.4f) * 0.04f;
                    marker.Scale = new Vector3(scale, scale, scale);
                }
                else
                {
                    marker.Scale = Vector3.One;
                }
            }
        }

        public void ApplyState(
            IReadOnlyDictionary<string, int> stockpile,
            IReadOnlyList<PrototypeWorkerState> citizens,
            IReadOnlyList<PrototypeStructureState> structures,
            PrototypeSettlementClassification classification,
            string buildQueueStatusText,
            int mealCoveragePercent,
            int bedCoveragePercent,
            int hearthFuel)
        {
            int stockpileTotal = stockpile.Values.Sum();
            for (int i = 0; i < _stockpileCrates.Count; i++)
            {
                _stockpileCrates[i].Visible = stockpileTotal > i * 6;
            }

            bool hasBuiltHearth = structures.Any(structure => structure.StructureKindId == "central_hearth" && structure.IsBuilt);
            bool hearthLit = hasBuiltHearth && hearthFuel > 0;
            if (_hearthFlame != null)
            {
                _hearthFlame.Visible = hearthLit;
            }

            if (_hearthLight != null)
            {
                _hearthLight.Visible = hearthLit;
            }

            UpdateStructureMarkers(structures);

            if (_label != null)
            {
                string stockpileSummary = stockpile.Count == 0
                    ? "empty"
                    : string.Join(", ", stockpile
                        .Where(pair => pair.Value > 0)
                        .OrderByDescending(pair => pair.Value)
                        .ThenBy(pair => pair.Key)
                        .Take(5)
                        .Select(pair => $"{InventoryComponent.FormatItemName(pair.Key)} x{pair.Value}"));

                int activeCitizens = citizens.Count(citizen => citizen.Phase != PrototypeWorkerPhase.Idle);
                _label.Text = "Settlement Hub\n" +
                              $"State: {classification}\n" +
                              $"Citizens: {activeCitizens}/{citizens.Count} active\n" +
                              $"Meals {mealCoveragePercent}%  Beds {bedCoveragePercent}%  Hearth {hearthFuel}\n" +
                              $"{buildQueueStatusText}\n" +
                              $"Stockpile: {stockpileSummary}";
            }
        }

        public void ApplyTerrainProfile(TerrainGenerator terrain, Vector3 settlementAnchorPosition)
        {
            if (_stockpileRoot == null || _woodYardRoot == null || _hearthRoot == null)
            {
                return;
            }

            Vector3 stockpileWorld = PrototypeSettlementLayout.GetStockpileWorldPosition(settlementAnchorPosition);
            Vector3 workbenchWorld = PrototypeSettlementLayout.GetWorkstationWorldPosition(settlementAnchorPosition);
            Vector3 hearthWorld = PrototypeSettlementLayout.GetCampfireWorldPosition(settlementAnchorPosition);

            _stockpileRoot.Position = new Vector3(
                stockpileWorld.X - settlementAnchorPosition.X,
                terrain.SampleHeight(stockpileWorld) - settlementAnchorPosition.Y,
                stockpileWorld.Z - settlementAnchorPosition.Z);
            _woodYardRoot.Position = new Vector3(
                workbenchWorld.X - settlementAnchorPosition.X,
                terrain.SampleHeight(workbenchWorld) - settlementAnchorPosition.Y,
                workbenchWorld.Z - settlementAnchorPosition.Z);
            _hearthRoot.Position = new Vector3(
                hearthWorld.X - settlementAnchorPosition.X,
                terrain.SampleHeight(hearthWorld) - settlementAnchorPosition.Y,
                hearthWorld.Z - settlementAnchorPosition.Z);
        }

        private void BuildVisuals()
        {
            MeshInstance3D plaza = CreateMesh(
                new CylinderMesh
                {
                    TopRadius = 5.3f,
                    BottomRadius = 5.3f,
                    Height = 0.14f
                },
                new Color(0.52f, 0.47f, 0.34f),
                new Vector3(0.0f, 0.07f, 0.0f));
            AddChild(plaza);

            _structureRoot = new Node3D { Name = "StructureMarkers" };
            AddChild(_structureRoot);

            BuildStockpileVisual();
            BuildWoodYardVisual();
            BuildHearthVisual();

            _label = new Label3D
            {
                Name = "HubLabel",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0.0f, 3.3f, -1.1f),
                FontSize = 24,
                Modulate = new Color(0.96f, 0.95f, 0.88f),
                Text = "Settlement Hub"
            };
            AddChild(_label);
        }

        private void BuildStockpileVisual()
        {
            _stockpileRoot = new Node3D
            {
                Name = "StockpileRoot",
                Position = PrototypeSettlementLayout.GetStockpileWorldPosition(Vector3.Zero)
            };
            AddChild(_stockpileRoot);

            MeshInstance3D pallet = CreateMesh(
                new BoxMesh { Size = new Vector3(2.3f, 0.22f, 1.9f) },
                new Color(0.45f, 0.31f, 0.19f),
                new Vector3(0.0f, 0.11f, 0.0f));
            _stockpileRoot.AddChild(pallet);

            Vector3[] crateOffsets =
            {
                new(-0.65f, 0.42f, -0.25f),
                new(0.02f, 0.42f, 0.24f),
                new(0.62f, 0.42f, -0.16f)
            };

            foreach (Vector3 offset in crateOffsets)
            {
                MeshInstance3D crate = CreateMesh(
                    new BoxMesh { Size = new Vector3(0.72f, 0.62f, 0.72f) },
                    new Color(0.67f, 0.5f, 0.28f),
                    offset);
                crate.Visible = false;
                _stockpileRoot.AddChild(crate);
                _stockpileCrates.Add(crate);
            }
        }

        private void BuildWoodYardVisual()
        {
            _woodYardRoot = new Node3D
            {
                Name = "WoodYardRoot",
                Position = PrototypeSettlementLayout.GetWorkstationWorldPosition(Vector3.Zero)
            };
            AddChild(_woodYardRoot);

            MeshInstance3D choppingBlock = CreateMesh(
                new CylinderMesh { TopRadius = 0.46f, BottomRadius = 0.52f, Height = 0.72f },
                new Color(0.48f, 0.32f, 0.19f),
                new Vector3(0.0f, 0.36f, 0.0f));
            _woodYardRoot.AddChild(choppingBlock);

            MeshInstance3D logPile = CreateMesh(
                new BoxMesh { Size = new Vector3(1.8f, 0.48f, 0.9f) },
                new Color(0.55f, 0.37f, 0.22f),
                new Vector3(0.8f, 0.26f, -0.45f));
            _woodYardRoot.AddChild(logPile);
        }

        private void BuildHearthVisual()
        {
            _hearthRoot = new Node3D
            {
                Name = "HearthRoot",
                Position = PrototypeSettlementLayout.GetCampfireWorldPosition(Vector3.Zero)
            };
            AddChild(_hearthRoot);

            Vector3[] stoneRing =
            {
                new(-0.34f, 0.11f, -0.22f),
                new(0.31f, 0.11f, -0.18f),
                new(-0.18f, 0.11f, 0.31f),
                new(0.26f, 0.11f, 0.29f),
                new(0.0f, 0.11f, -0.38f)
            };

            foreach (Vector3 offset in stoneRing)
            {
                MeshInstance3D stone = CreateMesh(
                    new BoxMesh { Size = new Vector3(0.26f, 0.22f, 0.26f) },
                    new Color(0.46f, 0.48f, 0.52f),
                    offset);
                _hearthRoot.AddChild(stone);
            }

            _hearthFlame = CreateMesh(
                new BoxMesh { Size = new Vector3(0.34f, 0.75f, 0.34f) },
                new Color(0.96f, 0.52f, 0.16f),
                new Vector3(0.0f, 0.56f, 0.0f));
            _hearthFlame.Visible = false;
            _hearthFlame.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.96f, 0.52f, 0.16f),
                EmissionEnabled = true,
                Emission = new Color(0.96f, 0.58f, 0.18f),
                EmissionEnergyMultiplier = 1.0f
            };
            _hearthRoot.AddChild(_hearthFlame);

            _hearthLight = new OmniLight3D
            {
                Name = "HearthLight",
                Position = new Vector3(0.0f, 1.35f, 0.0f),
                LightColor = new Color(1.0f, 0.76f, 0.44f),
                OmniRange = 9.0f,
                LightEnergy = 0.0f,
                Visible = false
            };
            _hearthRoot.AddChild(_hearthLight);
        }

        private void UpdateStructureMarkers(IReadOnlyList<PrototypeStructureState> structures)
        {
            if (_structureRoot == null)
            {
                return;
            }

            IReadOnlyList<PrototypeStructureState> visibleStructures = structures
                .Where(structure => structure.StructureKindId != "path_segment")
                .ToList();

            HashSet<string> activeIds = visibleStructures.Select(structure => structure.StructureId).ToHashSet();
            foreach ((string structureId, Node3D marker) in _structureMarkers.ToList())
            {
                if (activeIds.Contains(structureId))
                {
                    continue;
                }

                if (GodotObject.IsInstanceValid(marker))
                {
                    marker.QueueFree();
                }

                _structureMarkers.Remove(structureId);
            }

            foreach (PrototypeStructureState structure in visibleStructures)
            {
                if (!_structureMarkers.TryGetValue(structure.StructureId, out Node3D? marker) || !GodotObject.IsInstanceValid(marker))
                {
                    marker = CreateStructureMarker(structure);
                    _structureRoot.AddChild(marker);
                    _structureMarkers[structure.StructureId] = marker;
                }

                marker.Position = ToLocal(structure.Position) + new Vector3(0.0f, 0.25f, 0.0f);
                marker.SetMeta("pulse", structure.IsBuilt && !structure.IsBlocked && structure.ActiveTicks > 0);

                MeshInstance3D body = marker.GetNode<MeshInstance3D>("Body");
                body.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = GetStructureColor(structure),
                    Roughness = 0.9f,
                    Transparency = structure.IsBuilt ? BaseMaterial3D.TransparencyEnum.Disabled : BaseMaterial3D.TransparencyEnum.Alpha
                };
                body.Transparency = structure.IsBuilt ? 0.0f : 0.35f;

                Label3D label = marker.GetNode<Label3D>("Label");
                label.Text = structure.IsBlocked
                    ? $"{structure.DisplayName}\n{structure.BlockedReason}"
                    : structure.IsBuilt
                        ? structure.DisplayName
                        : $"{structure.DisplayName}\nplanned";
            }
        }

        private static Color GetStructureColor(PrototypeStructureState structure)
        {
            if (structure.IsBlocked)
            {
                return new Color(0.79f, 0.25f, 0.23f);
            }

            if (!structure.IsBuilt)
            {
                return new Color(0.64f, 0.63f, 0.6f);
            }

            return structure.StructureKindId switch
            {
                "hut" => new Color(0.77f, 0.65f, 0.41f),
                "storehouse" => new Color(0.59f, 0.43f, 0.24f),
                "kiln" => new Color(0.58f, 0.38f, 0.31f),
                "drying_rack" => new Color(0.72f, 0.68f, 0.34f),
                "central_hearth" => new Color(0.86f, 0.53f, 0.19f),
                "cookfire" => new Color(0.77f, 0.45f, 0.21f),
                "wood_yard" => new Color(0.52f, 0.34f, 0.19f),
                "central_depot" => new Color(0.42f, 0.44f, 0.56f),
                _ => new Color(0.62f, 0.62f, 0.62f)
            };
        }

        private static Node3D CreateStructureMarker(PrototypeStructureState structure)
        {
            Node3D root = new()
            {
                Name = structure.StructureId
            };

            MeshInstance3D body = new()
            {
                Name = "Body",
                Mesh = CreateStructureMesh(structure.StructureKindId)
            };
            root.AddChild(body);

            Label3D label = new()
            {
                Name = "Label",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0.0f, 1.6f, 0.0f),
                FontSize = 20,
                Modulate = new Color(0.94f, 0.92f, 0.86f),
                Text = structure.DisplayName
            };
            root.AddChild(label);

            return root;
        }

        private static PrimitiveMesh CreateStructureMesh(string structureKindId)
        {
            return structureKindId switch
            {
                "hut" => new BoxMesh { Size = new Vector3(1.6f, 1.0f, 1.5f) },
                "storehouse" => new BoxMesh { Size = new Vector3(1.9f, 1.2f, 1.7f) },
                "kiln" => new CylinderMesh { TopRadius = 0.65f, BottomRadius = 0.82f, Height = 1.0f },
                "drying_rack" => new BoxMesh { Size = new Vector3(1.8f, 1.1f, 0.28f) },
                "central_hearth" => new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.62f, Height = 0.35f },
                "cookfire" => new CylinderMesh { TopRadius = 0.38f, BottomRadius = 0.48f, Height = 0.32f },
                "wood_yard" => new BoxMesh { Size = new Vector3(1.8f, 0.7f, 0.9f) },
                "central_depot" => new BoxMesh { Size = new Vector3(2.0f, 0.8f, 1.3f) },
                _ => new BoxMesh { Size = Vector3.One }
            };
        }

        private static MeshInstance3D CreateMesh(PrimitiveMesh mesh, Color color, Vector3 position)
        {
            return new MeshInstance3D
            {
                Mesh = mesh,
                Position = position,
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    Roughness = 0.92f
                }
            };
        }
    }
}
