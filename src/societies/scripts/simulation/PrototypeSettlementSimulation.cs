using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Simulation
{
    /// <summary>
    /// Deterministic worker + stockpile + workstation loop for the prototype validation sandbox.
    /// Workers physically travel through the world, deposit into a shared stockpile, and craft
    /// a campfire when the settlement can afford it.
    /// </summary>
    public sealed class PrototypeSettlementSimulation
    {
        private const float WorkerTravelUnitsPerTick = 0.80f;
        private const int MinimumTravelTicks = 4;
        private const int HarvestTicks = 10;
        private const int DepositTicks = 4;
        private const int CraftTicks = 26;
        private const int IdleTicks = 18;
        private const int BerryReserveTarget = 4;
        private const int WoodReserveTargetAfterCampfire = 6;
        private const int StoneReserveTargetAfterCampfire = 3;

        private static readonly string[] DefaultResourceFocus = { "wood", "stone", "berry" };

        private readonly List<PrototypeWorkerState> _workers = new();
        private readonly InventoryComponent _stockpile;
        private readonly Vector3 _settlementAnchorPosition;
        private readonly Vector3 _stockpilePosition;
        private readonly Vector3 _workstationPosition;
        private readonly WorldMapState? _worldMap;

        public PrototypeSettlementSimulation(InventoryComponent stockpile, int workerCount, Vector3 settlementAnchorPosition, WorldMapState? worldMap = null)
        {
            _stockpile = stockpile;
            _worldMap = worldMap;
            _settlementAnchorPosition = settlementAnchorPosition;
            _stockpilePosition = ProjectToSurface(PrototypeSettlementLayout.GetStockpileWorldPosition(settlementAnchorPosition));
            _workstationPosition = ProjectToSurface(PrototypeSettlementLayout.GetWorkstationWorldPosition(settlementAnchorPosition));

            for (int i = 0; i < workerCount; i++)
            {
                string preferredResourceId = DefaultResourceFocus[i % DefaultResourceFocus.Length];
                Vector3 homePosition = ProjectToSurface(PrototypeSettlementLayout.GetWorkerHomeWorldPosition(settlementAnchorPosition, i, workerCount));
                _workers.Add(new PrototypeWorkerState
                {
                    WorkerId = $"worker_{i + 1}",
                    DisplayName = $"Worker {i + 1}",
                    PreferredResourceId = preferredResourceId,
                    Phase = PrototypeWorkerPhase.Idle,
                    TicksRemaining = IdleTicks,
                    PhaseDurationTicks = IdleTicks,
                    Position = homePosition,
                    HomePosition = homePosition,
                    TargetPosition = homePosition,
                    TargetLabel = "Settlement",
                    ActivityText = "Waiting for work"
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
                switch (worker.Phase)
                {
                    case PrototypeWorkerPhase.Idle:
                        if (!AdvanceStationaryPhase(worker, worker.HomePosition))
                        {
                            break;
                        }

                        QueueNextTask(worker, resources, result);
                        break;

                    case PrototypeWorkerPhase.MovingToResource:
                        if (!AdvanceTravelPhase(worker))
                        {
                            break;
                        }

                        BeginHarvestPhase(worker);
                        break;

                    case PrototypeWorkerPhase.Harvesting:
                        if (!AdvanceStationaryPhase(worker, worker.TargetPosition))
                        {
                            break;
                        }

                        CompleteHarvest(worker, resourceByNodeName, result);
                        break;

                    case PrototypeWorkerPhase.MovingToStockpile:
                        if (!AdvanceTravelPhase(worker))
                        {
                            break;
                        }

                        BeginDepositPhase(worker);
                        break;

                    case PrototypeWorkerPhase.Depositing:
                        if (!AdvanceStationaryPhase(worker, worker.HomePosition))
                        {
                            break;
                        }

                        CompleteDeposit(worker, result);
                        QueueNextTask(worker, resources, result);
                        break;

                    case PrototypeWorkerPhase.MovingToWorkstation:
                        if (!AdvanceTravelPhase(worker))
                        {
                            break;
                        }

                        BeginCraftPhase(worker, result);
                        break;

                    case PrototypeWorkerPhase.Crafting:
                        if (!AdvanceStationaryPhase(worker, _workstationPosition))
                        {
                            break;
                        }

                        CompleteCraft(worker, result);
                        QueueNextTask(worker, resources, result);
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
            BeginTravel(
                worker,
                PrototypeWorkerPhase.MovingToStockpile,
                worker.HomePosition,
                "Settlement",
                "Returning empty-handed");
        }

        public void LoadState(IReadOnlyList<PrototypeWorkerSnapshot> snapshots, Vector3 settlementAnchorPosition, WorldMapState? worldMap = null)
        {
            _workers.Clear();

            for (int i = 0; i < snapshots.Count; i++)
            {
                PrototypeWorkerSnapshot snapshot = snapshots[i];
                Vector3 fallbackHomePosition = ProjectToSurface(PrototypeSettlementLayout.GetWorkerHomeWorldPosition(settlementAnchorPosition, i, snapshots.Count));
                Vector3 homePosition = snapshot.HomePosition.ToVector3();
                if (homePosition == Vector3.Zero)
                {
                    homePosition = fallbackHomePosition;
                }

                Vector3 targetPosition = snapshot.TargetPosition.ToVector3();
                if (targetPosition == Vector3.Zero)
                {
                    targetPosition = homePosition;
                }

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
                    PhaseDurationTicks = snapshot.PhaseDurationTicks <= 0 ? snapshot.TicksRemaining : snapshot.PhaseDurationTicks,
                    Position = snapshot.Position.ToVector3(),
                    HomePosition = homePosition,
                    TargetPosition = targetPosition,
                    TargetLabel = string.IsNullOrWhiteSpace(snapshot.TargetLabel) ? "Settlement" : snapshot.TargetLabel,
                    ActivityText = string.IsNullOrWhiteSpace(snapshot.ActivityText) ? "Waiting for work" : snapshot.ActivityText
                });
            }

            if (_workers.Count == 0)
            {
                Vector3 homePosition = ProjectToSurface(PrototypeSettlementLayout.GetWorkerHomeWorldPosition(settlementAnchorPosition, 0, 1));
                _workers.Add(new PrototypeWorkerState
                {
                    WorkerId = "worker_1",
                    DisplayName = "Worker 1",
                    PreferredResourceId = "wood",
                    Phase = PrototypeWorkerPhase.Idle,
                    TicksRemaining = IdleTicks,
                    PhaseDurationTicks = IdleTicks,
                    Position = homePosition,
                    HomePosition = homePosition,
                    TargetPosition = homePosition,
                    TargetLabel = "Settlement",
                    ActivityText = "Waiting for work"
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
                BeginTravel(
                    worker,
                    PrototypeWorkerPhase.MovingToWorkstation,
                    _workstationPosition,
                    "Workbench",
                    "Walking to workstation");
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.AiTaskAssigned,
                    $"{worker.DisplayName} is heading to the workstation to craft a Campfire"));
                return;
            }

            PrototypeTaskSelection? taskSelection = SelectTargetSite(worker, resources);
            if (taskSelection == null)
            {
                BeginIdle(worker, "Waiting for work");
                return;
            }

            BeginTravel(
                worker,
                PrototypeWorkerPhase.MovingToResource,
                taskSelection.Value.TargetSite.Position,
                taskSelection.Value.TargetLabel,
                taskSelection.Value.ActivityText);
            worker.TargetResourceNodeName = taskSelection.Value.TargetSite.NodeName;

            result.Events.Add(new PrototypeSettlementEvent(
                PrototypeEventTypes.AiTaskAssigned,
                $"{worker.DisplayName} is gathering {taskSelection.Value.TargetSite.ResourceId} because {taskSelection.Value.ReasonText.ToLowerInvariant()}"));
        }

        private void BeginHarvestPhase(PrototypeWorkerState worker)
        {
            BeginStationaryPhase(
                worker,
                PrototypeWorkerPhase.Harvesting,
                HarvestTicks,
                worker.TargetPosition,
                worker.TargetLabel,
                $"Harvesting {worker.TargetLabel}");
        }

        private void BeginDepositPhase(PrototypeWorkerState worker)
        {
            string carriedItemName = string.IsNullOrWhiteSpace(worker.CarryItemId)
                ? "goods"
                : InventoryComponent.FormatItemName(worker.CarryItemId);
            BeginStationaryPhase(
                worker,
                PrototypeWorkerPhase.Depositing,
                DepositTicks,
                worker.HomePosition,
                "Settlement",
                $"Depositing {carriedItemName}");
        }

        private void BeginCraftPhase(PrototypeWorkerState worker, PrototypeSettlementTickResult result)
        {
            BeginStationaryPhase(
                worker,
                PrototypeWorkerPhase.Crafting,
                CraftTicks,
                _workstationPosition,
                "Workbench",
                "Crafting campfire");
            result.Events.Add(new PrototypeSettlementEvent(
                PrototypeEventTypes.AiCraftStarted,
                $"{worker.DisplayName} started crafting a Campfire"));
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
                BeginTravel(
                    worker,
                    PrototypeWorkerPhase.MovingToStockpile,
                    worker.HomePosition,
                    "Settlement",
                    "Returning empty-handed");
                return;
            }

            worker.CarryItemId = targetSite.ResourceId;
            worker.CarryAmount = 1;

            BeginTravel(
                worker,
                PrototypeWorkerPhase.MovingToStockpile,
                worker.HomePosition,
                "Settlement",
                $"Returning with {InventoryComponent.FormatItemName(targetSite.ResourceId)}");

            result.HarvestRequests.Add(new PrototypeHarvestRequest(
                worker.WorkerId,
                worker.DisplayName,
                targetSite.NodeName,
                targetSite.ResourceId,
                1));
        }

        private void CompleteDeposit(PrototypeWorkerState worker, PrototypeSettlementTickResult result)
        {
            if (worker.CarryAmount > 0 && !string.IsNullOrWhiteSpace(worker.CarryItemId))
            {
                _stockpile.AddItem(worker.CarryItemId, worker.CarryAmount);
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.AiDepositCompleted,
                    $"{worker.DisplayName} deposited {InventoryComponent.FormatItemName(worker.CarryItemId)} x{worker.CarryAmount}"));
            }

            worker.CarryItemId = string.Empty;
            worker.CarryAmount = 0;
            worker.TargetResourceNodeName = string.Empty;
            worker.Position = worker.HomePosition;
        }

        private void CompleteCraft(PrototypeWorkerState worker, PrototypeSettlementTickResult result)
        {
            if (CraftingSystem.TryCraft("campfire", _stockpile, out CraftingRecipe? recipe))
            {
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.AiCraftCompleted,
                    $"{worker.DisplayName} crafted {recipe!.DisplayName}"));
            }
            else
            {
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.AiCraftBlocked,
                    $"{worker.DisplayName} could not complete Campfire crafting"));
            }

            worker.Position = _workstationPosition;
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

            if (_workers.Any(candidate =>
                candidate.WorkerId != worker.WorkerId &&
                (candidate.Phase == PrototypeWorkerPhase.Crafting || candidate.Phase == PrototypeWorkerPhase.MovingToWorkstation)))
            {
                return false;
            }

            return _stockpile.GetCount("wood") >= 3 && _stockpile.GetCount("stone") >= 4;
        }

        private PrototypeTaskSelection? SelectTargetSite(
            PrototypeWorkerState worker,
            IReadOnlyList<PrototypeResourceSiteState> resources)
        {
            Dictionary<string, int> committed = BuildCommittedResourceCounts(worker.WorkerId);
            bool hasCampfire = _stockpile.GetCount("campfire") > 0;
            List<PrototypeResourceDemand> demands = BuildResourceDemandList(worker, committed, hasCampfire, resources);

            foreach (PrototypeResourceDemand demand in demands)
            {
                PrototypeResourceSiteState candidate = resources
                    .Where(site => site.ResourceId == demand.ResourceId && site.UnitsRemaining > 0)
                    .OrderBy(site => GetHorizontalDistance(site.Position, worker.Position))
                    .ThenBy(site => site.NodeName)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(candidate.NodeName))
                {
                    return new PrototypeTaskSelection(
                        candidate,
                        PrototypeSettlementLayout.GetResourceTargetLabel(candidate.ResourceId),
                        demand.ActivityText,
                        demand.ReasonText);
                }
            }

            return null;
        }

        private Dictionary<string, int> BuildCommittedResourceCounts(string excludingWorkerId)
        {
            Dictionary<string, int> committed = new(StringComparer.Ordinal);

            foreach (PrototypeWorkerState worker in _workers.Where(candidate => candidate.WorkerId != excludingWorkerId))
            {
                if (!string.IsNullOrWhiteSpace(worker.CarryItemId) && worker.CarryAmount > 0)
                {
                    committed[worker.CarryItemId] = committed.GetValueOrDefault(worker.CarryItemId) + worker.CarryAmount;
                }

                if (!string.IsNullOrWhiteSpace(worker.TargetResourceNodeName) &&
                    !string.IsNullOrWhiteSpace(worker.TargetLabel) &&
                    worker.Phase == PrototypeWorkerPhase.MovingToResource)
                {
                    string resourceId = InferTargetResourceId(worker.TargetLabel, worker.PreferredResourceId);
                    committed[resourceId] = committed.GetValueOrDefault(resourceId) + 1;
                }
            }

            return committed;
        }

        private List<PrototypeResourceDemand> BuildResourceDemandList(
            PrototypeWorkerState worker,
            IReadOnlyDictionary<string, int> committed,
            bool hasCampfire,
            IReadOnlyList<PrototypeResourceSiteState> resources)
        {
            List<PrototypeResourceDemand> demands = new();

            int woodTarget = hasCampfire ? WoodReserveTargetAfterCampfire : 3;
            int stoneTarget = hasCampfire ? StoneReserveTargetAfterCampfire : 4;

            AddDemand(
                demands,
                worker,
                resources,
                "wood",
                woodTarget,
                committed.GetValueOrDefault("wood"),
                hasCampfire ? "fuel and building reserve" : "the first campfire",
                hasCampfire ? "Gathering wood for settlement reserve" : "Gathering wood for the campfire");
            AddDemand(
                demands,
                worker,
                resources,
                "stone",
                stoneTarget,
                committed.GetValueOrDefault("stone"),
                hasCampfire ? "stone reserve" : "the first campfire",
                hasCampfire ? "Gathering stone for settlement reserve" : "Gathering stone for the campfire");
            AddDemand(
                demands,
                worker,
                resources,
                "berry",
                BerryReserveTarget,
                committed.GetValueOrDefault("berry"),
                "food reserve",
                "Gathering berries for the food reserve");

            return demands
                .OrderByDescending(demand => demand.Priority)
                .ThenByDescending(demand => demand.ResourceId == worker.PreferredResourceId)
                .ThenBy(demand => demand.ResourceId)
                .ToList();
        }

        private void AddDemand(
            List<PrototypeResourceDemand> demands,
            PrototypeWorkerState worker,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            string resourceId,
            int targetAmount,
            int committedAmount,
            string reasonText,
            string activityText)
        {
            if (!resources.Any(site => site.ResourceId == resourceId && site.UnitsRemaining > 0))
            {
                return;
            }

            int effectiveAmount = _stockpile.GetCount(resourceId) + committedAmount;
            int deficit = Math.Max(0, targetAmount - effectiveAmount);
            if (deficit == 0)
            {
                return;
            }

            int priority = deficit * 10;
            if (worker.PreferredResourceId == resourceId)
            {
                priority += 2;
            }

            demands.Add(new PrototypeResourceDemand(resourceId, priority, reasonText, activityText));
        }

        private void BeginIdle(PrototypeWorkerState worker, string activityText)
        {
            BeginStationaryPhase(
                worker,
                PrototypeWorkerPhase.Idle,
                IdleTicks,
                worker.HomePosition,
                "Settlement",
                activityText);
        }

        private void BeginTravel(
            PrototypeWorkerState worker,
            PrototypeWorkerPhase phase,
            Vector3 targetPosition,
            string targetLabel,
            string activityText)
        {
            worker.Phase = phase;
            worker.TargetPosition = targetPosition;
            worker.TargetLabel = targetLabel;
            worker.ActivityText = activityText;
            worker.PhaseDurationTicks = CalculateTravelTicks(worker.Position, targetPosition);
            worker.TicksRemaining = worker.PhaseDurationTicks;
        }

        private void BeginStationaryPhase(
            PrototypeWorkerState worker,
            PrototypeWorkerPhase phase,
            int durationTicks,
            Vector3 position,
            string targetLabel,
            string activityText)
        {
            worker.Phase = phase;
            worker.Position = position;
            worker.TargetPosition = position;
            worker.TargetLabel = targetLabel;
            worker.ActivityText = activityText;
            worker.PhaseDurationTicks = durationTicks;
            worker.TicksRemaining = durationTicks;
        }

        private static bool AdvanceStationaryPhase(PrototypeWorkerState worker, Vector3 position)
        {
            worker.Position = position;
            if (worker.TicksRemaining > 0)
            {
                worker.TicksRemaining--;
            }

            return worker.TicksRemaining <= 0;
        }

        private static bool AdvanceTravelPhase(PrototypeWorkerState worker)
        {
            Vector3 nextHorizontalPosition = new Vector3(worker.Position.X, 0.0f, worker.Position.Z)
                .MoveToward(new Vector3(worker.TargetPosition.X, 0.0f, worker.TargetPosition.Z), WorkerTravelUnitsPerTick);
            worker.Position = new Vector3(
                nextHorizontalPosition.X,
                Mathf.Lerp(worker.Position.Y, worker.TargetPosition.Y, 0.35f),
                nextHorizontalPosition.Z);
            if (worker.TicksRemaining > 0)
            {
                worker.TicksRemaining--;
            }

            if (worker.TicksRemaining <= 0 || GetHorizontalDistance(worker.Position, worker.TargetPosition) <= 0.01f)
            {
                worker.Position = worker.TargetPosition;
                worker.TicksRemaining = 0;
                return true;
            }

            return false;
        }

        private static int CalculateTravelTicks(Vector3 start, Vector3 destination)
        {
            return Math.Max(MinimumTravelTicks, Mathf.CeilToInt(GetHorizontalDistance(start, destination) / WorkerTravelUnitsPerTick));
        }

        private static float GetHorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.X, a.Z).DistanceTo(new Vector2(b.X, b.Z));
        }

        private static string InferTargetResourceId(string targetLabel, string fallbackResourceId)
        {
            return targetLabel switch
            {
                "Tree" => "wood",
                "Rock" => "stone",
                "Berry Bush" => "berry",
                _ => fallbackResourceId
            };
        }

        private static PrototypeWorkerPhase ParsePhase(string phase)
        {
            return System.Enum.TryParse(phase, out PrototypeWorkerPhase parsed)
                ? parsed
                : PrototypeWorkerPhase.Idle;
        }

        private Vector3 ProjectToSurface(Vector3 position)
        {
            return _worldMap?.ProjectToSurface(position) ?? position;
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

        public int PhaseDurationTicks { get; set; }

        public Vector3 Position { get; set; }

        public Vector3 HomePosition { get; set; }

        public Vector3 TargetPosition { get; set; }

        public string TargetLabel { get; set; } = string.Empty;

        public string ActivityText { get; set; } = string.Empty;

        public int ProgressPercent
        {
            get
            {
                if (PhaseDurationTicks <= 0)
                {
                    return 100;
                }

                float completedRatio = 1.0f - (TicksRemaining / (float)PhaseDurationTicks);
                return Mathf.Clamp(Mathf.RoundToInt(completedRatio * 100.0f), 0, 100);
            }
        }
    }

    public enum PrototypeWorkerPhase
    {
        Idle,
        MovingToResource,
        Harvesting,
        MovingToStockpile,
        Depositing,
        MovingToWorkstation,
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

    internal readonly record struct PrototypeResourceDemand(
        string ResourceId,
        int Priority,
        string ReasonText,
        string ActivityText);

    internal readonly record struct PrototypeTaskSelection(
        PrototypeResourceSiteState TargetSite,
        string TargetLabel,
        string ActivityText,
        string ReasonText);
}
