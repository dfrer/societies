using Godot;
using Societies.Core;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Simulation
{
    /// <summary>
    /// Deterministic worker + stockpile + workstation loop for the prototype validation sandbox.
    /// Workers gather from live resource nodes, deposit into a shared stockpile, and craft a campfire
    /// once the stockpile can afford it.
    /// </summary>
    public sealed class PrototypeSettlementSimulation
    {
        private const int TravelTicks = 12;
        private const int HarvestTicks = 10;
        private const int DepositTicks = 3;
        private const int CraftTicks = 24;
        private const int IdleTicks = 12;

        private static readonly string[] DefaultResourceFocus = { "wood", "stone", "berry" };

        private readonly List<PrototypeWorkerState> _workers = new();
        private readonly InventoryComponent _stockpile;
        private readonly Vector3 _stockpilePosition;

        public PrototypeSettlementSimulation(InventoryComponent stockpile, int workerCount, Vector3 stockpilePosition)
        {
            _stockpile = stockpile;
            _stockpilePosition = stockpilePosition;

            for (int i = 0; i < workerCount; i++)
            {
                string preferredResourceId = DefaultResourceFocus[i % DefaultResourceFocus.Length];
                _workers.Add(new PrototypeWorkerState
                {
                    WorkerId = $"worker_{i + 1}",
                    DisplayName = $"Worker {i + 1}",
                    PreferredResourceId = preferredResourceId,
                    Phase = PrototypeWorkerPhase.Idle,
                    TicksRemaining = IdleTicks,
                    Position = stockpilePosition
                });
            }
        }

        public IReadOnlyList<PrototypeWorkerState> Workers => _workers;

        public PrototypeSettlementTickResult Advance(IReadOnlyList<PrototypeResourceSiteState> resources)
        {
            PrototypeSettlementTickResult result = new();
            Dictionary<string, PrototypeResourceSiteState> resourceByNodeName = resources.ToDictionary(site => site.NodeName);

            foreach (PrototypeWorkerState worker in _workers)
            {
                if (worker.TicksRemaining > 0)
                {
                    worker.TicksRemaining--;
                    continue;
                }

                switch (worker.Phase)
                {
                    case PrototypeWorkerPhase.Idle:
                        QueueNextTask(worker, resourceByNodeName.Values.ToList(), result);
                        break;

                    case PrototypeWorkerPhase.MovingToResource:
                        worker.Phase = PrototypeWorkerPhase.Harvesting;
                        worker.TicksRemaining = HarvestTicks;
                        if (!string.IsNullOrWhiteSpace(worker.TargetResourceNodeName) &&
                            resourceByNodeName.TryGetValue(worker.TargetResourceNodeName, out PrototypeResourceSiteState targetSite))
                        {
                            worker.Position = targetSite.Position;
                        }
                        break;

                    case PrototypeWorkerPhase.Harvesting:
                        CompleteHarvest(worker, resourceByNodeName, result);
                        break;

                    case PrototypeWorkerPhase.MovingToStockpile:
                        worker.Position = _stockpilePosition;
                        worker.Phase = PrototypeWorkerPhase.Depositing;
                        worker.TicksRemaining = DepositTicks;
                        break;

                    case PrototypeWorkerPhase.Depositing:
                        CompleteDeposit(worker, result);
                        QueueNextTask(worker, resourceByNodeName.Values.ToList(), result);
                        break;

                    case PrototypeWorkerPhase.Crafting:
                        CompleteCraft(worker, result);
                        QueueNextTask(worker, resourceByNodeName.Values.ToList(), result);
                        break;
                }
            }

            return result;
        }

        public void OnHarvestFailed(string workerId)
        {
            PrototypeWorkerState? worker = _workers.FirstOrDefault(candidate => candidate.WorkerId == workerId);
            if (worker == null)
            {
                return;
            }

            worker.CarryAmount = 0;
            worker.CarryItemId = string.Empty;
            worker.TargetResourceNodeName = string.Empty;
            worker.Position = _stockpilePosition;
            worker.Phase = PrototypeWorkerPhase.Idle;
            worker.TicksRemaining = IdleTicks;
        }

        public void LoadState(IReadOnlyList<PrototypeWorkerSnapshot> snapshots, Vector3 stockpilePosition)
        {
            _workers.Clear();

            foreach (PrototypeWorkerSnapshot snapshot in snapshots)
            {
                _workers.Add(new PrototypeWorkerState
                {
                    WorkerId = snapshot.WorkerId,
                    DisplayName = snapshot.DisplayName,
                    PreferredResourceId = snapshot.PreferredResourceId,
                    Phase = ParsePhase(snapshot.Phase),
                    TargetResourceNodeName = snapshot.TargetResourceNodeName,
                    CarryItemId = snapshot.CarryItemId,
                    CarryAmount = snapshot.CarryAmount,
                    TicksRemaining = snapshot.TicksRemaining,
                    Position = snapshot.Position.ToVector3()
                });
            }

            if (_workers.Count == 0)
            {
                _workers.Add(new PrototypeWorkerState
                {
                    WorkerId = "worker_1",
                    DisplayName = "Worker 1",
                    PreferredResourceId = "wood",
                    Phase = PrototypeWorkerPhase.Idle,
                    TicksRemaining = IdleTicks,
                    Position = stockpilePosition
                });
            }
        }

        private void QueueNextTask(
            PrototypeWorkerState worker,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            PrototypeSettlementTickResult result)
        {
            if (CanStartCampfireCraft(worker))
            {
                worker.Phase = PrototypeWorkerPhase.Crafting;
                worker.TicksRemaining = CraftTicks;
                worker.Position = _stockpilePosition;
                result.Events.Add(new PrototypeSettlementEvent(PrototypeEventTypes.AiCraftStarted, $"{worker.DisplayName} started crafting a Campfire"));
                return;
            }

            PrototypeResourceSiteState? target = SelectTargetSite(worker, resources);
            if (target == null)
            {
                worker.Phase = PrototypeWorkerPhase.Idle;
                worker.TicksRemaining = IdleTicks;
                worker.TargetResourceNodeName = string.Empty;
                worker.Position = _stockpilePosition;
                return;
            }

            worker.TargetResourceNodeName = target.Value.NodeName;
            worker.Phase = PrototypeWorkerPhase.MovingToResource;
            worker.TicksRemaining = TravelTicks;
            worker.Position = _stockpilePosition;
            result.Events.Add(new PrototypeSettlementEvent(PrototypeEventTypes.AiTaskAssigned, $"{worker.DisplayName} is gathering {target.Value.ResourceId}"));
        }

        private void CompleteHarvest(
            PrototypeWorkerState worker,
            IReadOnlyDictionary<string, PrototypeResourceSiteState> resourceByNodeName,
            PrototypeSettlementTickResult result)
        {
            if (string.IsNullOrWhiteSpace(worker.TargetResourceNodeName) ||
                !resourceByNodeName.TryGetValue(worker.TargetResourceNodeName, out PrototypeResourceSiteState targetSite) ||
                targetSite.UnitsRemaining <= 0)
            {
                worker.Phase = PrototypeWorkerPhase.Idle;
                worker.TicksRemaining = IdleTicks;
                worker.TargetResourceNodeName = string.Empty;
                return;
            }

            worker.CarryItemId = targetSite.ResourceId;
            worker.CarryAmount = 1;
            worker.Phase = PrototypeWorkerPhase.MovingToStockpile;
            worker.TicksRemaining = TravelTicks;
            worker.Position = targetSite.Position;

            result.HarvestRequests.Add(new PrototypeHarvestRequest(
                worker.WorkerId,
                worker.DisplayName,
                targetSite.NodeName,
                targetSite.ResourceId,
                1));
        }

        private void CompleteDeposit(PrototypeWorkerState worker, PrototypeSettlementTickResult result)
        {
            if (worker.CarryAmount <= 0 || string.IsNullOrWhiteSpace(worker.CarryItemId))
            {
                return;
            }

            _stockpile.AddItem(worker.CarryItemId, worker.CarryAmount);
            result.Events.Add(new PrototypeSettlementEvent(
                PrototypeEventTypes.AiDepositCompleted,
                $"{worker.DisplayName} deposited {InventoryComponent.FormatItemName(worker.CarryItemId)} x{worker.CarryAmount}"));

            worker.CarryItemId = string.Empty;
            worker.CarryAmount = 0;
            worker.TargetResourceNodeName = string.Empty;
            worker.Position = _stockpilePosition;
        }

        private void CompleteCraft(PrototypeWorkerState worker, PrototypeSettlementTickResult result)
        {
            if (CraftingSystem.TryCraft("campfire", _stockpile, out CraftingRecipe? recipe))
            {
                result.Events.Add(new PrototypeSettlementEvent(PrototypeEventTypes.AiCraftCompleted, $"{worker.DisplayName} crafted {recipe!.DisplayName}"));
            }
            else
            {
                result.Events.Add(new PrototypeSettlementEvent(PrototypeEventTypes.AiCraftBlocked, $"{worker.DisplayName} could not complete Campfire crafting"));
            }

            worker.Position = _stockpilePosition;
        }

        private bool CanStartCampfireCraft(PrototypeWorkerState worker)
        {
            if (worker.CarryAmount > 0)
            {
                return false;
            }

            if (_stockpile.GetCount("campfire") > 0)
            {
                return false;
            }

            if (_workers.Any(candidate => candidate.WorkerId != worker.WorkerId && candidate.Phase == PrototypeWorkerPhase.Crafting))
            {
                return false;
            }

            return _stockpile.GetCount("wood") >= 3 && _stockpile.GetCount("stone") >= 4;
        }

        private static PrototypeResourceSiteState? SelectTargetSite(
            PrototypeWorkerState worker,
            IReadOnlyList<PrototypeResourceSiteState> resources)
        {
            PrototypeResourceSiteState? preferred = resources
                .Where(site => site.ResourceId == worker.PreferredResourceId && site.UnitsRemaining > 0)
                .OrderBy(site => site.Position.LengthSquared())
                .ThenBy(site => site.NodeName)
                .Cast<PrototypeResourceSiteState?>()
                .FirstOrDefault();

            if (preferred != null)
            {
                return preferred;
            }

            return resources
                .Where(site => site.UnitsRemaining > 0)
                .OrderBy(site => site.Position.LengthSquared())
                .ThenBy(site => site.NodeName)
                .Cast<PrototypeResourceSiteState?>()
                .FirstOrDefault();
        }

        private static PrototypeWorkerPhase ParsePhase(string phase)
        {
            return System.Enum.TryParse(phase, out PrototypeWorkerPhase parsed)
                ? parsed
                : PrototypeWorkerPhase.Idle;
        }
    }

    public sealed class PrototypeSettlementTickResult
    {
        public List<PrototypeHarvestRequest> HarvestRequests { get; } = new();

        public List<PrototypeSettlementEvent> Events { get; } = new();
    }

    public sealed class PrototypeWorkerState
    {
        public string WorkerId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string PreferredResourceId { get; set; } = string.Empty;

        public PrototypeWorkerPhase Phase { get; set; }

        public string TargetResourceNodeName { get; set; } = string.Empty;

        public string CarryItemId { get; set; } = string.Empty;

        public int CarryAmount { get; set; }

        public int TicksRemaining { get; set; }

        public Vector3 Position { get; set; }
    }

    public enum PrototypeWorkerPhase
    {
        Idle,
        MovingToResource,
        Harvesting,
        MovingToStockpile,
        Depositing,
        Crafting
    }

    public readonly record struct PrototypeResourceSiteState(
        string NodeName,
        string ResourceId,
        Vector3 Position,
        int UnitsRemaining);

    public readonly record struct PrototypeHarvestRequest(
        string WorkerId,
        string WorkerDisplayName,
        string TargetNodeName,
        string ResourceId,
        int Amount);

    public readonly record struct PrototypeSettlementEvent(
        string EventType,
        string Message);
}
