using Godot;
using Societies.Core;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Simulation
{
    /// <summary>
    /// World-space presentation for the prototype settlement stockpile/workstation loop.
    /// </summary>
    public partial class PrototypeSettlementHub : Node3D
    {
        private readonly List<MeshInstance3D> _stockpileCrates = new();
        private MeshInstance3D? _campfireFlame;
        private OmniLight3D? _campfireLight;
        private Label3D? _label;
        private Node3D? _stockpileRoot;
        private Node3D? _workbenchRoot;
        private Node3D? _campfireRoot;
        private double _animationTime;

        public string StatusText => _label?.Text ?? string.Empty;

        public bool IsCampfireLit => _campfireFlame?.Visible ?? false;

        public override void _Ready()
        {
            BuildVisuals();
        }

        public override void _Process(double delta)
        {
            _animationTime += delta;

            if (_campfireFlame?.Visible == true)
            {
                float pulse = 0.9f + Mathf.Sin((float)_animationTime * 5.6f) * 0.08f;
                _campfireFlame.Scale = new Vector3(1.0f, pulse, 1.0f);
            }

            if (_campfireLight != null)
            {
                _campfireLight.LightEnergy = _campfireFlame?.Visible == true
                    ? 1.2f + Mathf.Sin((float)_animationTime * 7.2f) * 0.12f
                    : 0.0f;
            }
        }

        public void ApplyState(IReadOnlyDictionary<string, int> stockpile, IReadOnlyList<PrototypeWorkerState> workers)
        {
            int stockpileTotal = stockpile.Values.Sum();
            for (int i = 0; i < _stockpileCrates.Count; i++)
            {
                _stockpileCrates[i].Visible = stockpileTotal > i * 4;
            }

            bool hasCampfire = stockpile.TryGetValue("campfire", out int campfireCount) && campfireCount > 0;
            if (_campfireFlame != null)
            {
                _campfireFlame.Visible = hasCampfire;
            }

            if (_campfireLight != null)
            {
                _campfireLight.Visible = hasCampfire;
            }

            if (_label != null)
            {
                string stockpileSummary = stockpile.Count == 0
                    ? "empty"
                    : string.Join(", ", stockpile
                        .Where(pair => pair.Value > 0)
                        .OrderByDescending(pair => pair.Value)
                        .ThenBy(pair => pair.Key)
                        .Take(4)
                        .Select(pair => $"{pair.Key} x{pair.Value}"));

                int activeWorkers = workers.Count(worker => worker.Phase != PrototypeWorkerPhase.Idle);
                _label.Text = "Settlement Hub\n" +
                              $"Stockpile: {stockpileSummary}\n" +
                              $"Campfire: {(hasCampfire ? "lit" : "unbuilt")}\n" +
                              $"Workers active: {activeWorkers}/{workers.Count}";
            }
        }

        public void ApplyTerrainProfile(TerrainGenerator terrain, Vector3 settlementAnchorPosition)
        {
            if (_stockpileRoot == null || _workbenchRoot == null || _campfireRoot == null)
            {
                return;
            }

            Vector3 stockpileWorld = PrototypeSettlementLayout.GetStockpileWorldPosition(settlementAnchorPosition);
            Vector3 workbenchWorld = PrototypeSettlementLayout.GetWorkstationWorldPosition(settlementAnchorPosition);
            Vector3 campfireWorld = PrototypeSettlementLayout.GetCampfireWorldPosition(settlementAnchorPosition);

            _stockpileRoot.Position = new Vector3(
                stockpileWorld.X - settlementAnchorPosition.X,
                terrain.SampleHeight(stockpileWorld) - settlementAnchorPosition.Y,
                stockpileWorld.Z - settlementAnchorPosition.Z);
            _workbenchRoot.Position = new Vector3(
                workbenchWorld.X - settlementAnchorPosition.X,
                terrain.SampleHeight(workbenchWorld) - settlementAnchorPosition.Y,
                workbenchWorld.Z - settlementAnchorPosition.Z);
            _campfireRoot.Position = new Vector3(
                campfireWorld.X - settlementAnchorPosition.X,
                terrain.SampleHeight(campfireWorld) - settlementAnchorPosition.Y,
                campfireWorld.Z - settlementAnchorPosition.Z);
        }

        private void BuildVisuals()
        {
            MeshInstance3D plaza = CreateMesh(
                new CylinderMesh
                {
                    TopRadius = 4.8f,
                    BottomRadius = 4.8f,
                    Height = 0.14f
                },
                new Color(0.52f, 0.47f, 0.34f),
                new Vector3(0.0f, 0.07f, 0.0f));
            AddChild(plaza);

            BuildStockpileVisual();
            BuildWorkbenchVisual();
            BuildCampfireVisual();

            _label = new Label3D
            {
                Name = "HubLabel",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0.0f, 2.7f, -1.0f),
                FontSize = 26,
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
                new BoxMesh { Size = new Vector3(2.1f, 0.22f, 1.7f) },
                new Color(0.45f, 0.31f, 0.19f),
                new Vector3(0.0f, 0.11f, 0.0f));
            _stockpileRoot.AddChild(pallet);

            Vector3[] crateOffsets =
            {
                new(-0.55f, 0.42f, -0.25f),
                new(0.12f, 0.42f, 0.2f),
                new(0.58f, 0.42f, -0.18f)
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

        private void BuildWorkbenchVisual()
        {
            _workbenchRoot = new Node3D
            {
                Name = "WorkbenchRoot",
                Position = PrototypeSettlementLayout.GetWorkstationWorldPosition(Vector3.Zero)
            };
            AddChild(_workbenchRoot);

            MeshInstance3D tableTop = CreateMesh(
                new BoxMesh { Size = new Vector3(2.2f, 0.18f, 1.05f) },
                new Color(0.43f, 0.29f, 0.18f),
                new Vector3(0.0f, 0.92f, 0.0f));
            _workbenchRoot.AddChild(tableTop);

            Vector3[] legOffsets =
            {
                new(-0.88f, 0.46f, -0.36f),
                new(0.88f, 0.46f, -0.36f),
                new(-0.88f, 0.46f, 0.36f),
                new(0.88f, 0.46f, 0.36f)
            };

            foreach (Vector3 offset in legOffsets)
            {
                MeshInstance3D leg = CreateMesh(
                    new BoxMesh { Size = new Vector3(0.12f, 0.9f, 0.12f) },
                    new Color(0.36f, 0.24f, 0.15f),
                    offset);
                _workbenchRoot.AddChild(leg);
            }
        }

        private void BuildCampfireVisual()
        {
            _campfireRoot = new Node3D
            {
                Name = "CampfireRoot",
                Position = PrototypeSettlementLayout.GetCampfireWorldPosition(Vector3.Zero)
            };
            AddChild(_campfireRoot);

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
                _campfireRoot.AddChild(stone);
            }

            _campfireFlame = CreateMesh(
                new BoxMesh { Size = new Vector3(0.34f, 0.75f, 0.34f) },
                new Color(0.96f, 0.52f, 0.16f),
                new Vector3(0.0f, 0.56f, 0.0f));
            _campfireFlame.Visible = false;
            _campfireFlame.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.96f, 0.52f, 0.16f),
                EmissionEnabled = true,
                Emission = new Color(0.96f, 0.58f, 0.18f),
                EmissionEnergyMultiplier = 0.9f
            };
            _campfireRoot.AddChild(_campfireFlame);

            _campfireLight = new OmniLight3D
            {
                Name = "CampfireLight",
                Position = new Vector3(0.0f, 1.35f, 0.0f),
                LightColor = new Color(1.0f, 0.76f, 0.44f),
                OmniRange = 8.5f,
                LightEnergy = 0.0f,
                Visible = false
            };
            _campfireRoot.AddChild(_campfireLight);
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
