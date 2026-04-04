using Godot;
using Societies.Core;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Simulation
{
    /// <summary>
    /// Owns scene-side presentation for prototype settlement resources, workers, and hub visuals.
    /// </summary>
    public sealed class PrototypeSettlementScenePresenter
    {
        private readonly Node3D _agentsRoot;
        private readonly Node3D _entitiesRoot;
        private readonly Node3D _environmentRoot;
        private readonly Dictionary<string, PrototypeWorkerAgent> _workerNodes = new();
        private Vector3 _settlementAnchorPosition;
        private TerrainGenerator _terrain;
        private PrototypeSettlementHub? _settlementHub;
        private Node3D? _overlayRoot;
        private TerrainOverlayMode _lastOverlayMode;
        private int _lastOverlaySignature;

        public PrototypeSettlementScenePresenter(
            Node3D agentsRoot,
            Node3D entitiesRoot,
            Node3D environmentRoot,
            TerrainGenerator terrain)
        {
            _agentsRoot = agentsRoot;
            _entitiesRoot = entitiesRoot;
            _environmentRoot = environmentRoot;
            _terrain = terrain;
            _settlementAnchorPosition = Vector3.Zero;
        }

        public bool HasAnyResources => _entitiesRoot.GetChildren().OfType<ResourceNode>().Any();

        public PrototypeSettlementHub EnsureSettlementHub()
        {
            _settlementHub ??= _environmentRoot.GetNodeOrNull<PrototypeSettlementHub>("SettlementHub");
            if (_settlementHub == null)
            {
                _settlementHub = new PrototypeSettlementHub { Name = "SettlementHub" };
                _environmentRoot.AddChild(_settlementHub);
            }

            _settlementHub.Position = _settlementAnchorPosition;
            _settlementHub.ApplyTerrainProfile(_terrain, _settlementAnchorPosition);
            return _settlementHub;
        }

        public void UpdateTerrain(TerrainGenerator terrain)
        {
            _terrain = terrain;
            _settlementHub?.ApplyTerrainProfile(_terrain, _settlementAnchorPosition);
        }

        public void ResetDynamicNodes()
        {
            ClearChildren(_entitiesRoot);
            ClearChildren(_agentsRoot);
            _overlayRoot?.QueueFree();
            _overlayRoot = null;
            _workerNodes.Clear();
        }

        public void ApplyWorld(WorldGenerationResult result)
        {
            _settlementAnchorPosition = result.SettlementSpawn.AnchorPosition;
            EnsureSettlementHub();
            ReplaceResourceNodes(
                result.ResourceSpawns
                    .Select(spawn => new PrototypeResourceSnapshot
                    {
                        ResourceId = spawn.ResourceId,
                        UnitsRemaining = spawn.UnitsRemaining,
                        Position = PrototypeSerializableVector3.FromVector3(spawn.Position),
                        ClusterId = spawn.ClusterId
                    })
                    .ToList());
        }

        public IReadOnlyList<PrototypeResourceSnapshot> CaptureResourceSnapshots()
        {
            return _entitiesRoot
                .GetChildren()
                .OfType<ResourceNode>()
                .OrderBy(node => node.Name.ToString())
                .Select(node => new PrototypeResourceSnapshot
                {
                    ResourceId = node.ResourceId,
                    UnitsRemaining = node.UnitsRemaining,
                    Position = PrototypeSerializableVector3.FromVector3(node.Position),
                    ClusterId = node.ClusterId
                })
                .ToList();
        }

        public IReadOnlyList<PrototypeResourceSiteState> CaptureResourceSites()
        {
            return _entitiesRoot
                .GetChildren()
                .OfType<ResourceNode>()
                .OrderBy(node => node.Name.ToString())
                .Select(node => new PrototypeResourceSiteState(
                    node.Name.ToString(),
                    node.ResourceId,
                    node.Position,
                    node.UnitsRemaining,
                    node.ClusterId))
                .ToList();
        }

        public bool ApplyHarvestRequest(PrototypeHarvestRequest request, out string itemId, out int harvestedAmount)
        {
            itemId = string.Empty;
            harvestedAmount = 0;

            ResourceNode? node = _entitiesRoot
                .GetChildren()
                .OfType<ResourceNode>()
                .FirstOrDefault(candidate => candidate.Name.ToString() == request.TargetNodeName);

            return node != null && node.TryHarvest(request.Amount, out itemId, out harvestedAmount);
        }

        public void ReplaceResourceNodes(IReadOnlyList<PrototypeResourceSnapshot> resourceSnapshots)
        {
            ClearChildren(_entitiesRoot);

            Dictionary<string, int> counters = new();
            foreach (PrototypeResourceSnapshot snapshot in resourceSnapshots)
            {
                int sequence = counters.TryGetValue(snapshot.ResourceId, out int current) ? current + 1 : 1;
                counters[snapshot.ResourceId] = sequence;
                SpawnResourceNode(snapshot.ResourceId, snapshot.Position.ToVector3(), snapshot.UnitsRemaining, sequence, snapshot.ClusterId);
            }
        }

        public void SyncWorkers(IReadOnlyList<PrototypeWorkerState> workers)
        {
            HashSet<string> activeWorkerIds = workers.Select(worker => worker.WorkerId).ToHashSet();

            foreach ((string workerId, PrototypeWorkerAgent node) in _workerNodes.ToList())
            {
                if (activeWorkerIds.Contains(workerId))
                {
                    continue;
                }

                if (GodotObject.IsInstanceValid(node))
                {
                    node.Free();
                }

                _workerNodes.Remove(workerId);
            }

            foreach (PrototypeWorkerState worker in workers.OrderBy(candidate => candidate.WorkerId))
            {
                if (!_workerNodes.TryGetValue(worker.WorkerId, out PrototypeWorkerAgent? node) || !GodotObject.IsInstanceValid(node))
                {
                    node = new PrototypeWorkerAgent
                    {
                        Name = worker.WorkerId
                    };
                    _agentsRoot.AddChild(node);
                    _workerNodes[worker.WorkerId] = node;
                }

                node.ApplyState(worker);
            }
        }

        public void UpdateSettlementPresentation(
            IReadOnlyDictionary<string, int> stockpile,
            IReadOnlyList<PrototypeWorkerState> workers,
            IReadOnlyList<PrototypeStructureState> structures,
            PrototypeSettlementClassification classification,
            string buildQueueStatusText,
            int mealCoveragePercent,
            int bedCoveragePercent,
            int hearthFuel,
            TerrainOverlayMode overlayMode = TerrainOverlayMode.None,
            IReadOnlyList<PrototypePathSegmentState>? pathSegments = null,
            IReadOnlyList<PrototypeRemoteDepotState>? remoteDepots = null,
            IReadOnlyList<PrototypeRouteHeatCellState>? routeHeatCells = null)
        {
            EnsureSettlementHub().ApplyState(
                stockpile,
                workers,
                structures,
                classification,
                buildQueueStatusText,
                mealCoveragePercent,
                bedCoveragePercent,
                hearthFuel);

            UpdateNavigationOverlays(overlayMode, pathSegments ?? new List<PrototypePathSegmentState>(), remoteDepots ?? new List<PrototypeRemoteDepotState>(), routeHeatCells ?? new List<PrototypeRouteHeatCellState>());
        }

        private void UpdateNavigationOverlays(
            TerrainOverlayMode overlayMode,
            IReadOnlyList<PrototypePathSegmentState> pathSegments,
            IReadOnlyList<PrototypeRemoteDepotState> remoteDepots,
            IReadOnlyList<PrototypeRouteHeatCellState> routeHeatCells)
        {
            int signature =
                (pathSegments.Count(segment => segment.IsBuilt) * 1000000) +
                (remoteDepots.Count(depot => depot.IsBuilt) * 1000) +
                routeHeatCells.Sum(cell => cell.UsageCount);
            if (_overlayRoot != null && _lastOverlayMode == overlayMode && _lastOverlaySignature == signature)
            {
                return;
            }

            _lastOverlayMode = overlayMode;
            _lastOverlaySignature = signature;

            _overlayRoot ??= new Node3D { Name = "SettlementOverlays" };
            if (_overlayRoot.GetParent() == null)
            {
                _environmentRoot.AddChild(_overlayRoot);
            }

            ClearChildren(_overlayRoot);

            switch (overlayMode)
            {
                case TerrainOverlayMode.BuiltPaths:
                    foreach (PrototypePathSegmentState segment in pathSegments.Where(segment => segment.IsBuilt))
                    {
                        _overlayRoot.AddChild(CreatePathMarker(segment.Position));
                    }

                    break;

                case TerrainOverlayMode.RemoteDepots:
                    foreach (PrototypeRemoteDepotState depot in remoteDepots)
                    {
                        _overlayRoot.AddChild(CreateDepotMarker(depot.Position, depot.IsBuilt));
                    }

                    break;

                case TerrainOverlayMode.RouteHeat:
                    foreach (PrototypeRouteHeatCellState cell in routeHeatCells
                        .OrderByDescending(cell => cell.UsageCount)
                        .Take(96))
                    {
                        _overlayRoot.AddChild(CreateHeatMarker(cell.Position, cell.UsageCount));
                    }

                    break;
            }
        }

        private static MeshInstance3D CreatePathMarker(Vector3 position)
        {
            return new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.12f, 1.6f) },
                Position = position + new Vector3(0.0f, 0.06f, 0.0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.86f, 0.72f, 0.28f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                }
            };
        }

        private static MeshInstance3D CreateDepotMarker(Vector3 position, bool isBuilt)
        {
            return new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 1.1f, BottomRadius = 1.1f, Height = 1.8f },
                Position = position + new Vector3(0.0f, 0.9f, 0.0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = isBuilt ? new Color(0.31f, 0.67f, 0.84f) : new Color(0.54f, 0.58f, 0.63f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                }
            };
        }

        private static MeshInstance3D CreateHeatMarker(Vector3 position, int usageCount)
        {
            float intensity = Mathf.Clamp(usageCount / 24.0f, 0.15f, 1.0f);
            return new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.14f, 1.8f) },
                Position = position + new Vector3(0.0f, 0.08f + (intensity * 0.04f), 0.0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.92f, Mathf.Lerp(0.76f, 0.18f, intensity), 0.20f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha
                }
            };
        }

        private void SpawnResourceNode(string resourceId, Vector3 position, int unitsRemaining, int sequence, string clusterId)
        {
            ResourceNode node = new()
            {
                Name = $"{resourceId}_{sequence}",
                ResourceId = resourceId,
                UnitsRemaining = unitsRemaining,
                ClusterId = clusterId
            };

            _entitiesRoot.AddChild(node);
            node.Position = position;
        }

        private static void ClearChildren(Node parent)
        {
            foreach (Node child in parent.GetChildren())
            {
                child.Free();
            }
        }
    }
}
