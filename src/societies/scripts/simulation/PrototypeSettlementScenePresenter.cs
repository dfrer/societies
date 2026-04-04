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
                        Position = PrototypeSerializableVector3.FromVector3(spawn.Position)
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
                    Position = PrototypeSerializableVector3.FromVector3(node.Position)
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
                    node.UnitsRemaining))
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
                SpawnResourceNode(snapshot.ResourceId, snapshot.Position.ToVector3(), snapshot.UnitsRemaining, sequence);
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
            IReadOnlyList<PrototypeWorkerState> workers)
        {
            EnsureSettlementHub().ApplyState(stockpile, workers);
        }

        private void SpawnResourceNode(string resourceId, Vector3 position, int unitsRemaining, int sequence)
        {
            ResourceNode node = new()
            {
                Name = $"{resourceId}_{sequence}",
                ResourceId = resourceId,
                UnitsRemaining = unitsRemaining
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
