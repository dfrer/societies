using Godot;
using Societies.Core;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Simulation
{
    public enum PrototypeCitizenRole
    {
        Logger,
        Mason,
        Forager,
        Hauler,
        Processor,
        Builder,
        Generalist
    }

    public enum PrototypeWorkerPhase
    {
        Idle,
        MovingToResource,
        Harvesting,
        MovingToStockpile,
        MovingToCache,
        Depositing,
        DepositingToCache,
        MovingToDepot,
        DepositingToDepot,
        MovingToWorkstation,
        MovingToStructure,
        DepositingToStructure,
        Crafting,
        Processing,
        Building,
        Refueling,
        Eating,
        Sleeping,
        Incapacitated
    }

    public enum PrototypeWorkOrderKind
    {
        Extract,
        HaulToCache,
        HaulToDepot,
        HaulToRemoteDepot,
        HaulFromRemoteDepot,
        HaulToStructure,
        Process,
        Build,
        BuildPath,
        EstablishRemoteDepot,
        Eat,
        Sleep,
        RefuelHearth,
        Repath
    }

    public enum PrototypeSettlementDirective
    {
        Neutral,
        FoodAndFuel,
        Shelter
    }

    public enum PrototypeDirectiveAffinity
    {
        None,
        FoodAndFuel,
        Shelter
    }

    public readonly record struct PrototypeDirectiveChangeResult(
        PrototypeSettlementDirective PreviousDirective,
        PrototypeSettlementDirective CurrentDirective,
        bool Succeeded,
        bool Changed,
        string FailureReason);

    public static class PrototypeSettlementDirectiveCatalog
    {
        public const float AssignmentScoreBonus = 240.0f;

        public static string GetId(PrototypeSettlementDirective directive)
        {
            return directive switch
            {
                PrototypeSettlementDirective.Neutral => "neutral",
                PrototypeSettlementDirective.FoodAndFuel => "food_and_fuel",
                PrototypeSettlementDirective.Shelter => "shelter",
                _ => throw new System.ArgumentOutOfRangeException(nameof(directive))
            };
        }

        public static string GetDisplayName(PrototypeSettlementDirective directive)
        {
            return directive switch
            {
                PrototypeSettlementDirective.Neutral => "Neutral",
                PrototypeSettlementDirective.FoodAndFuel => "Food & Fuel",
                PrototypeSettlementDirective.Shelter => "Shelter",
                _ => throw new System.ArgumentOutOfRangeException(nameof(directive))
            };
        }

        public static float GetAssignmentScoreBonus(
            PrototypeSettlementDirective directive,
            PrototypeWorkOrder order)
        {
            bool matches = directive switch
            {
                PrototypeSettlementDirective.FoodAndFuel => order.DirectiveAffinity == PrototypeDirectiveAffinity.FoodAndFuel,
                PrototypeSettlementDirective.Shelter => order.DirectiveAffinity == PrototypeDirectiveAffinity.Shelter,
                _ => false
            };
            return matches ? AssignmentScoreBonus : 0.0f;
        }
    }

    public enum PrototypeSettlementClassification
    {
        Stable,
        Strained,
        Collapsed
    }

    /// <summary>
    /// Non-persistent runtime choice used to prove the optimized selector against
    /// the exhaustive reference without changing the settlement snapshot schema.
    /// </summary>
    public enum PrototypeOrderSelectionMode
    {
        ExactBranchAndBound,
        ExhaustiveReference
    }

    /// <summary>
    /// Non-persistent runtime choice used to compare bounded extraction planning
    /// with the original exhaustive route ordering.
    /// </summary>
    public enum PrototypeExtractionPlanningMode
    {
        ExactBounded,
        ExhaustiveReference
    }

    /// <summary>
    /// Non-persistent runtime choice used to compare cached distance-only replay
    /// with full path-plan rematerialization.
    /// </summary>
    public enum PrototypeRouteDistanceMode
    {
        CachedDistanceOnly,
        FullMaterializationReference
    }

    public readonly record struct PrototypeExtractionCandidate(
        PrototypeResourceSiteState Site,
        Vector3 InteractionPosition,
        float DistanceLowerBound,
        int OriginalIndex);

    public static class PrototypeExtractionPlanningMath
    {
        public const int BuiltCorridorPriorityBonus = 40;

        public static int ComputePriorityUpperBound(int basePriority, bool hasBuiltCorridor)
        {
            return basePriority + (hasBuiltCorridor ? BuiltCorridorPriorityBonus : 0);
        }

        public static bool TryComputeWholeResourceClassOmission(
            IReadOnlyList<int> existingUnclaimedPriorities,
            int frontierBudget,
            int priorityUpperBound,
            IReadOnlyList<string> eligibleOrderIds,
            IReadOnlySet<string> activeClaimedOrderIds,
            int desiredUnits,
            out int omittedCount)
        {
            omittedCount = 0;
            if (desiredUnits <= 0 ||
                eligibleOrderIds.Count == 0 ||
                existingUnclaimedPriorities.Count < frontierBudget ||
                eligibleOrderIds.Any(activeClaimedOrderIds.Contains))
            {
                return false;
            }

            int strictlyHigherCount = 0;
            foreach (int existingPriority in existingUnclaimedPriorities)
            {
                if (existingPriority <= priorityUpperBound)
                {
                    continue;
                }

                strictlyHigherCount++;
                if (strictlyHigherCount == frontierBudget)
                {
                    omittedCount = System.Math.Min(desiredUnits, eligibleOrderIds.Count);
                    return true;
                }
            }

            return false;
        }

        public static List<PrototypeWorkOrder> ApplyFrontierLimit(
            List<PrototypeWorkOrder> orders,
            int frontierBudget,
            int virtualUncappedCount)
        {
            if (virtualUncappedCount <= frontierBudget)
            {
                return orders;
            }

            return orders
                .OrderByDescending(order => order.Priority)
                .ThenBy(order => order.OrderId, System.StringComparer.Ordinal)
                .Take(frontierBudget)
                .ToList();
        }

        public static IReadOnlyList<PrototypeExtractionCandidate> SelectExactTopK(
            IReadOnlyList<PrototypeExtractionCandidate> candidates,
            int desiredUnits,
            System.Func<PrototypeExtractionCandidate, float> computeExactDistance)
        {
            if (desiredUnits <= 0 || candidates.Count == 0)
            {
                return System.Array.Empty<PrototypeExtractionCandidate>();
            }

            int selectedCount = System.Math.Min(desiredUnits, candidates.Count);
            if (selectedCount == candidates.Count &&
                candidates.Select(candidate => candidate.Site.NodeName).Distinct(System.StringComparer.Ordinal).Count() == candidates.Count)
            {
                // When every candidate is emitted, route order is not observable: extraction
                // order IDs are unique and every downstream tie/frontier is ordered by ID.
                // Duplicate IDs fall through to the exhaustive ordering path below.
                return candidates
                    .OrderBy(candidate => candidate.Site.NodeName, System.StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.OriginalIndex)
                    .ToArray();
            }

            List<(PrototypeExtractionCandidate Candidate, float ExactDistance)> exactCandidates = new(selectedCount);
            foreach (PrototypeExtractionCandidate candidate in candidates
                .OrderBy(candidate => candidate.DistanceLowerBound)
                .ThenBy(candidate => candidate.Site.NodeName, System.StringComparer.Ordinal)
                .ThenBy(candidate => candidate.OriginalIndex))
            {
                if (exactCandidates.Count == selectedCount &&
                    candidate.DistanceLowerBound > exactCandidates[^1].ExactDistance)
                {
                    break;
                }

                float exactDistance = computeExactDistance(candidate);
                exactCandidates.Add((candidate, exactDistance));
                exactCandidates.Sort((left, right) =>
                {
                    int distanceComparison = left.ExactDistance.CompareTo(right.ExactDistance);
                    if (distanceComparison != 0)
                    {
                        return distanceComparison;
                    }

                    int nameComparison = System.StringComparer.Ordinal.Compare(
                        left.Candidate.Site.NodeName,
                        right.Candidate.Site.NodeName);
                    return nameComparison != 0
                        ? nameComparison
                        : left.Candidate.OriginalIndex.CompareTo(right.Candidate.OriginalIndex);
                });

                if (exactCandidates.Count > selectedCount)
                {
                    exactCandidates.RemoveAt(exactCandidates.Count - 1);
                }
            }

            return exactCandidates
                .Select(candidate => candidate.Candidate)
                .ToArray();
        }

        public static bool ShouldApplyRemoteDepotPenalty(
            bool hasBuiltRemoteDepot,
            float distanceLowerBound,
            float activationDistance,
            System.Func<float> computeExactDistance)
        {
            if (hasBuiltRemoteDepot)
            {
                return false;
            }

            if (distanceLowerBound > activationDistance)
            {
                return true;
            }

            return computeExactDistance() > activationDistance;
        }
    }

    public static class PrototypeOrderSelectionMath
    {
        public const float ExactDistancePenalty = 0.75f;

        // A simple route visits no more cells than the world contains. Reserving one
        // millimetre of float error per possible segment (plus both exact endpoint
        // segments) keeps this straight-line value a conservative distance lower bound.
        public static float ComputeStraightLineDistanceLowerBound(
            Vector3 startPosition,
            Vector3 destinationPosition,
            int worldCellCount)
        {
            int maximumPathSegmentCount = System.Math.Max(1, worldCellCount + 2);
            float floatGuardMeters = maximumPathSegmentCount * 0.001f;
            float straightLineDistance = new Vector2(
                destinationPosition.X - startPosition.X,
                destinationPosition.Z - startPosition.Z).Length();
            return System.Math.Max(0.0f, straightLineDistance - floatGuardMeters);
        }

        public static float ComputeScoreUpperBound(
            int priority,
            float roleBonus,
            Vector3 startPosition,
            Vector3 destinationPosition,
            int worldCellCount)
        {
            float distanceLowerBound = ComputeStraightLineDistanceLowerBound(
                startPosition,
                destinationPosition,
                worldCellCount);
            return priority + roleBonus - (ExactDistancePenalty * distanceLowerBound);
        }

        public static bool IsExactCandidatePreferred(
            float candidateScore,
            string candidateOrderId,
            int candidateOriginalIndex,
            float currentBestScore,
            string currentBestOrderId,
            int currentBestOriginalIndex)
        {
            if (candidateScore != currentBestScore)
            {
                return candidateScore > currentBestScore;
            }

            int orderIdComparison = System.StringComparer.Ordinal.Compare(candidateOrderId, currentBestOrderId);
            return orderIdComparison < 0 ||
                (orderIdComparison == 0 && candidateOriginalIndex < currentBestOriginalIndex);
        }
    }

    public sealed class PrototypeNeedState
    {
        public float Nutrition { get; set; } = 100.0f;

        public float Fatigue { get; set; }

        public bool IsNutritionCritical => Nutrition <= 12.0f;

        public bool NeedsFood => Nutrition <= 45.0f;

        public bool IsExhausted => Fatigue >= 90.0f;

        public bool NeedsSleep => Fatigue >= 62.0f;
    }

    public sealed class PrototypeResourceStoreState
    {
        public string StoreId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public int Capacity { get; set; } = 24;

        public Vector3 Position { get; set; }

        public string LinkedClusterId { get; set; } = string.Empty;

        public HashSet<string> AllowedResourceIds { get; } = new();

        public Dictionary<string, int> Items { get; } = new();

        public int Occupied => Items.Values.Sum();

        public int AvailableCapacity => Capacity <= 0 ? int.MaxValue : Capacity - Occupied;

        public int GetCount(string resourceId)
        {
            return Items.TryGetValue(resourceId, out int amount) ? amount : 0;
        }

        public bool CanAccept(string resourceId)
        {
            return AllowedResourceIds.Count == 0 || AllowedResourceIds.Contains(resourceId);
        }

        public bool Add(string resourceId, int amount)
        {
            if (amount <= 0 || !CanAccept(resourceId) || AvailableCapacity < amount)
            {
                return false;
            }

            Items[resourceId] = GetCount(resourceId) + amount;
            return true;
        }

        public bool Remove(string resourceId, int amount)
        {
            if (amount <= 0 || GetCount(resourceId) < amount)
            {
                return false;
            }

            int remaining = GetCount(resourceId) - amount;
            if (remaining == 0)
            {
                Items.Remove(resourceId);
            }
            else
            {
                Items[resourceId] = remaining;
            }

            return true;
        }
    }

    public sealed class PrototypeStructureState
    {
        public string StructureId { get; set; } = string.Empty;

        public string StructureKindId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public Vector3 Position { get; set; }

        public int GridX { get; set; }

        public int GridY { get; set; }

        public string CorridorId { get; set; } = string.Empty;

        public string LinkedClusterId { get; set; } = string.Empty;

        public bool IsBuilt { get; set; }

        public bool IsBlocked { get; set; }

        public string BlockedReason { get; set; } = string.Empty;

        public int AssignedBeds { get; set; }

        public int BedCapacity { get; set; }

        public float Progress { get; set; }

        public int ActiveTicks { get; set; }

        public int BlockedTicks { get; set; }

        public PrototypeResourceStoreState InputStore { get; set; } = new();

        public PrototypeResourceStoreState OutputStore { get; set; } = new();

        public int HearthFuel { get; set; }
    }

    public sealed class PrototypeNavigationGridCell
    {
        public int GridX { get; init; }

        public int GridY { get; init; }

        public Vector3 WorldPosition { get; init; }

        public float MovementCost { get; init; }

        public bool IsWalkable { get; init; }

        public bool HasBuiltPath { get; init; }
    }

    public readonly record struct PrototypePathQuery(
        int StartGridX,
        int StartGridY,
        int EndGridX,
        int EndGridY,
        int RulesVersion);

    public readonly record struct PrototypePathCacheKey(
        int StartGridX,
        int StartGridY,
        int EndGridX,
        int EndGridY,
        int RulesVersion);

    public sealed class PrototypePathCacheEntry
    {
        public PrototypePathQuery Query { get; init; }

        public bool IsReachable { get; init; }

        public IReadOnlyList<Vector2I> Cells { get; init; } = new List<Vector2I>();
    }

    public sealed class PrototypePathPlan
    {
        public PrototypePathQuery Query { get; init; }

        public IReadOnlyList<Vector2I> Cells { get; init; } = new List<Vector2I>();

        public IReadOnlyList<Vector3> Waypoints { get; init; } = new List<Vector3>();

        public float TotalCost { get; init; }

        public float TotalDistanceMeters { get; init; }

        public int EstimatedTravelTicks { get; init; }
    }

    public sealed class PrototypeCitizenNavigationState
    {
        public int CurrentWaypointIndex { get; set; }

        public float CurrentRouteLengthMeters { get; set; }

        public float CurrentRouteCost { get; set; }

        public int CurrentRouteTravelTicks { get; set; }

        public int CachedRouteVersion { get; set; }

        public int SourceGridX { get; set; }

        public int SourceGridY { get; set; }

        public int DestinationGridX { get; set; }

        public int DestinationGridY { get; set; }

        public List<PrototypeSerializableVector3> RouteWaypoints { get; set; } = new();
    }

    public sealed class PrototypePathSegmentState
    {
        public string StructureId { get; set; } = string.Empty;

        public string CorridorId { get; set; } = string.Empty;

        public int GridX { get; set; }

        public int GridY { get; set; }

        public Vector3 Position { get; set; }

        public bool IsBuilt { get; set; }

        public int UtilizationCount { get; set; }
    }

    public sealed class PrototypeRemoteDepotState
    {
        public string StructureId { get; set; } = string.Empty;

        public string ClusterId { get; set; } = string.Empty;

        public string ResourceId { get; set; } = string.Empty;

        public Vector3 Position { get; set; }

        public int GridX { get; set; }

        public int GridY { get; set; }

        public bool IsBuilt { get; set; }

        public float DistanceToCentralDepot { get; set; }

        public int ThroughputCount { get; set; }
    }

    public sealed class PrototypeRouteHeatCellState
    {
        public int GridX { get; set; }

        public int GridY { get; set; }

        public Vector3 Position { get; set; }

        public int UsageCount { get; set; }
    }

    public sealed class PrototypeLogisticsMetricsState
    {
        public int CompletedRouteCount { get; set; }

        public float TotalCompletedRouteDistanceMeters { get; set; }

        public int TotalCompletedRouteTicks { get; set; }

        public int TravelTicksAccumulated { get; set; }

        public int WorkTicksAccumulated { get; set; }

        public float PathCoverageRatio { get; set; }

        public Dictionary<string, int> DepotThroughputByDepot { get; set; } = new();

        public Dictionary<string, int> RouteBacklogTicksByKind { get; set; } = new();
    }

    public sealed class PrototypeBuildQueueEntry
    {
        public string EntryId { get; set; } = string.Empty;

        public string StructureKindId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public bool IsPaused { get; set; }

        public bool IsCompleted { get; set; }

        public int Priority { get; set; }

        public string StructureId { get; set; } = string.Empty;
    }

    public sealed class PrototypeWorkOrder
    {
        public string OrderId { get; set; } = string.Empty;

        public PrototypeWorkOrderKind Kind { get; set; }

        public int Priority { get; set; }

        public string ResourceId { get; set; } = string.Empty;

        public string SourceStoreId { get; set; } = string.Empty;

        public string DestinationStoreId { get; set; } = string.Empty;

        public string StructureId { get; set; } = string.Empty;

        public string TargetNodeName { get; set; } = string.Empty;

        public string ClusterId { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public PrototypeDirectiveAffinity DirectiveAffinity { get; set; }

        public string DirectiveCause { get; set; } = string.Empty;

        public Vector3 TargetPosition { get; set; }

        public int Amount { get; set; } = 1;
    }

    internal readonly record struct PrototypeExtractionFrontierProbe(
        IReadOnlyList<PrototypeWorkOrder> Orders,
        int VirtualUncappedCount,
        int OmittedCount,
        int PathPlanLookups,
        int PathPlanCacheHits,
        int PathPlanCacheMisses,
        long CachedRouteDistanceFastPathHits,
        PrototypePerformanceProbeSnapshot PerformanceProbe);

    internal readonly record struct PrototypeDirectiveSelectionProbe(
        string OrderId,
        string CurrentOrderReason);

    public sealed class PrototypeWorkerState
    {
        public string WorkerId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public PrototypeCitizenRole Role { get; set; }

        public PrototypeNeedState Needs { get; set; } = new();

        public string PreferredResourceId { get; set; } = string.Empty;

        public PrototypeWorkerPhase Phase { get; set; }

        public string TargetResourceNodeName { get; set; } = string.Empty;

        public string TargetStructureId { get; set; } = string.Empty;

        public string SourceStoreId { get; set; } = string.Empty;

        public string DestinationStoreId { get; set; } = string.Empty;

        public string CarryItemId { get; set; } = string.Empty;

        public int CarryAmount { get; set; }

        public int TicksRemaining { get; set; }

        public int PhaseDurationTicks { get; set; }

        public Vector3 Position { get; set; }

        public Vector3 HomePosition { get; set; }

        public Vector3 TargetPosition { get; set; }

        public string TargetLabel { get; set; } = string.Empty;

        public string ActivityText { get; set; } = string.Empty;

        public string LastFailureReason { get; set; } = string.Empty;

        public string CurrentOrderId { get; set; } = string.Empty;

        public PrototypeWorkOrderKind? CurrentOrderKind { get; set; }

        public string CurrentOrderReason { get; set; } = string.Empty;

        public List<string> RecentEvents { get; set; } = new();

        public int HomeBedCapacity { get; set; }

        public PrototypeCitizenNavigationState Navigation { get; set; } = new();

        public int TravelTicksAccumulated { get; set; }

        public int WorkTicksAccumulated { get; set; }

        public float TravelWorkRatio => WorkTicksAccumulated <= 0
            ? TravelTicksAccumulated
            : TravelTicksAccumulated / (float)WorkTicksAccumulated;

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

    public readonly record struct PrototypeResourceSiteState(
        string NodeName,
        string ResourceId,
        Vector3 Position,
        int UnitsRemaining,
        string ClusterId);

    public readonly record struct PrototypeHarvestRequest(
        string WorkerId,
        string WorkerDisplayName,
        string TargetNodeName,
        string ResourceId,
        int Amount,
        string ClusterId);

    public readonly record struct PrototypeHarvestCommand(
        string ActorId,
        string SiteId,
        string ResourceId,
        int RequestedQuantity);

    public readonly record struct PrototypeHarvestResult(
        string ActorId,
        string SiteId,
        string ResourceId,
        int RequestedQuantity,
        int AppliedQuantity,
        bool Succeeded,
        string FailureReason);

    public readonly record struct PrototypeContributionResult(
        string ResourceId,
        int RequestedQuantity,
        int AppliedQuantity,
        bool Succeeded,
        string FailureReason);

    public sealed class PrototypeContributionBatchResult
    {
        public PrototypeContributionBatchResult(
            IReadOnlyList<PrototypeContributionResult> results,
            bool succeeded,
            string failureReason)
        {
            Results = results;
            Succeeded = succeeded;
            FailureReason = failureReason;
        }

        public IReadOnlyList<PrototypeContributionResult> Results { get; }

        public bool Succeeded { get; }

        public string FailureReason { get; }

        public int AppliedQuantity => Results.Sum(result => result.AppliedQuantity);
    }

    public readonly record struct PrototypeSettlementEvent(
        string EventType,
        string Message);

    public sealed class PrototypeSettlementTickResult
    {
        public List<PrototypeHarvestRequest> HarvestRequests { get; } = new();

        public List<PrototypeSettlementEvent> Events { get; } = new();
    }

    public sealed class PrototypeResourceStoreSnapshot
    {
        public string StoreId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public int Capacity { get; set; }

        public PrototypeSerializableVector3 Position { get; set; }

        public string LinkedClusterId { get; set; } = string.Empty;

        public Dictionary<string, int> Items { get; set; } = new();
    }

    public sealed class PrototypeStructureSnapshot
    {
        public string StructureId { get; set; } = string.Empty;

        public string StructureKindId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public PrototypeSerializableVector3 Position { get; set; }

        public int GridX { get; set; }

        public int GridY { get; set; }

        public string CorridorId { get; set; } = string.Empty;

        public string LinkedClusterId { get; set; } = string.Empty;

        public bool IsBuilt { get; set; }

        public bool IsBlocked { get; set; }

        public string BlockedReason { get; set; } = string.Empty;

        public int AssignedBeds { get; set; }

        public int BedCapacity { get; set; }

        public float Progress { get; set; }

        public int ActiveTicks { get; set; }

        public int BlockedTicks { get; set; }

        public PrototypeResourceStoreSnapshot InputStore { get; set; } = new();

        public PrototypeResourceStoreSnapshot OutputStore { get; set; } = new();

        public int HearthFuel { get; set; }
    }

    public sealed class PrototypePathSegmentSnapshot
    {
        public string StructureId { get; set; } = string.Empty;

        public string CorridorId { get; set; } = string.Empty;

        public int GridX { get; set; }

        public int GridY { get; set; }

        public PrototypeSerializableVector3 Position { get; set; }

        public bool IsBuilt { get; set; }

        public int UtilizationCount { get; set; }
    }

    public sealed class PrototypeRemoteDepotSnapshot
    {
        public string StructureId { get; set; } = string.Empty;

        public string ClusterId { get; set; } = string.Empty;

        public string ResourceId { get; set; } = string.Empty;

        public PrototypeSerializableVector3 Position { get; set; }

        public int GridX { get; set; }

        public int GridY { get; set; }

        public bool IsBuilt { get; set; }

        public float DistanceToCentralDepot { get; set; }

        public int ThroughputCount { get; set; }
    }

    public sealed class PrototypeRouteHeatCellSnapshot
    {
        public int GridX { get; set; }

        public int GridY { get; set; }

        public PrototypeSerializableVector3 Position { get; set; }

        public int UsageCount { get; set; }
    }

    public sealed class PrototypeBuildQueueEntrySnapshot
    {
        public string EntryId { get; set; } = string.Empty;

        public string StructureKindId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public bool IsPaused { get; set; }

        public bool IsCompleted { get; set; }

        public int Priority { get; set; }

        public string StructureId { get; set; } = string.Empty;
    }

    public sealed class PrototypeSettlementSnapshot
    {
        public PrototypeResourceStoreSnapshot CentralDepot { get; set; } = new();

        public List<PrototypeResourceStoreSnapshot> SiteCaches { get; set; } = new();

        public List<PrototypeStructureSnapshot> Structures { get; set; } = new();

        public List<PrototypeWorkerSnapshot> Citizens { get; set; } = new();

        public List<PrototypePathSegmentSnapshot> PathSegments { get; set; } = new();

        public List<PrototypeRemoteDepotSnapshot> RemoteDepots { get; set; } = new();

        public List<PrototypeRouteHeatCellSnapshot> RouteHeatCells { get; set; } = new();

        public List<PrototypeBuildQueueEntrySnapshot> BuildQueue { get; set; } = new();

        public Dictionary<string, int> ProducedResources { get; set; } = new();

        public Dictionary<string, int> ConsumedResources { get; set; } = new();

        public Dictionary<string, int> BlockedReasonCounts { get; set; } = new();

        public Dictionary<string, long> StructureCompletionTicks { get; set; } = new();

        public int SelectedBuildQueueIndex { get; set; }

        public int HearthLitTicks { get; set; }

        public int TotalTicks { get; set; }

        public int NavigationRulesVersion { get; set; } = 1;

        public string Classification { get; set; } = string.Empty;

        public PrototypeLogisticsMetricsState LogisticsMetrics { get; set; } = new();
    }
}
