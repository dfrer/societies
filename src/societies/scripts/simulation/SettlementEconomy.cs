using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Societies.Simulation
{
    public sealed partial class PrototypeSettlementSimulation
    {

        private void SeedStartingStock()
        {
            foreach ((string itemId, int amount) in _scenario.StartingStock)
            {
                if (amount > 0)
                {
                    _centralDepot.Add(itemId, amount);
                }
            }
        }
        private void InitializeSiteCaches()
        {
            foreach (ResourceClusterState cluster in _world.ResourceClusters)
            {
                Vector3 cachePosition = TryResolveWalkableInteractionPosition(cluster.CenterPosition, out Vector3 resolvedCachePosition)
                    ? resolvedCachePosition
                    : cluster.CenterPosition;
                PrototypeResourceStoreState cache = CreateStore(
                    $"cache.{cluster.ClusterId}",
                    $"{InventoryComponent.FormatItemName(cluster.ResourceId)} Cache",
                    18,
                    cachePosition,
                    cluster.ResourceId);
                cache.LinkedClusterId = cluster.ClusterId;
                _siteCaches[cache.StoreId] = cache;
            }
        }
        private void ApplyEnvironmentalUpkeep(float currentHour, PrototypeWeather weather, PrototypeSettlementTickResult result)
        {
            PrototypeStructureState? hearth = GetStructure("central_hearth_1");
            if (hearth == null)
            {
                return;
            }

            if (_totalTicks % HearthBurnIntervalTicks == 0 && hearth.HearthFuel > 0)
            {
                hearth.HearthFuel = Math.Max(0, hearth.HearthFuel - 1);
                IncrementCount(_consumedResources, "firewood", 1);
            }

            if (hearth.HearthFuel > 0)
            {
                _hearthLitTicks++;
            }

            if ((weather == PrototypeWeather.Rain || IsNight(currentHour)) && hearth.HearthFuel <= 0)
            {
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.SettlementShortage,
                    "Central hearth is unfueled during adverse conditions"));
            }
        }
        private List<PrototypeWorkOrder> BuildWorkOrders(
            IReadOnlyList<PrototypeResourceSiteState> resources,
            float currentHour,
            PrototypeWeather weather)
        {
            Dictionary<string, int> committedCarries = _citizens
                .Where(citizen => citizen.CarryAmount > 0)
                .GroupBy(citizen => citizen.CarryItemId)
                .ToDictionary(group => group.Key, group => group.Sum(citizen => citizen.CarryAmount), StringComparer.Ordinal);

            HashSet<string> activeClaimedOrderIds = BuildActiveClaimedOrderIds();
            List<PrototypeWorkOrder> orders = new();
            AddRefuelOrders(orders);
            AddHaulOrdersFromStores(orders);
            AddProductionOrders(orders);
            AddBuildOrders(orders);
            orders = RemoveClaimedOrders(orders, activeClaimedOrderIds);
            AnnotateDirectiveAffinities(orders);

            int omittedExtractionOrderCount = 0;
            AddReserveExtractionOrders(
                orders,
                resources,
                committedCarries,
                currentHour,
                weather,
                activeClaimedOrderIds,
                ref omittedExtractionOrderCount);
            orders = RemoveClaimedOrders(orders, activeClaimedOrderIds);
            _workOrdersGeneratedUncappedThisTick = orders.Count + omittedExtractionOrderCount;
            _extractionOrdersOmittedThisTick = omittedExtractionOrderCount;
            orders = ApplyWorkOrderFrontierLimit(orders, _workOrdersGeneratedUncappedThisTick);
            return orders;
        }

        private void AnnotateDirectiveAffinities(IEnumerable<PrototypeWorkOrder> orders)
        {
            foreach (PrototypeWorkOrder order in orders)
            {
                (order.DirectiveAffinity, order.DirectiveCause) = order.Kind switch
                {
                    PrototypeWorkOrderKind.RefuelHearth =>
                        (PrototypeDirectiveAffinity.FoodAndFuel, "hearth refueling"),
                    PrototypeWorkOrderKind.Build when IsHutOrder(order) =>
                        (PrototypeDirectiveAffinity.Shelter, "hut construction"),
                    _ => GetDirectiveMetadataForResource(order.ResourceId)
                };
            }
        }

        private static (PrototypeDirectiveAffinity Affinity, string Cause) GetDirectiveMetadataForResource(string resourceId)
        {
            return resourceId switch
            {
                "berries" => (PrototypeDirectiveAffinity.FoodAndFuel, "berry reserves"),
                "meals" => (PrototypeDirectiveAffinity.FoodAndFuel, "meal production"),
                "firewood" => (PrototypeDirectiveAffinity.FoodAndFuel, "fuel supply"),
                "logs" or "timber" => (PrototypeDirectiveAffinity.Shelter, "construction lumber"),
                "reeds" or "thatch" => (PrototypeDirectiveAffinity.Shelter, "shelter thatch"),
                _ => (PrototypeDirectiveAffinity.None, string.Empty)
            };
        }

        private int GetDirectiveAdjustedPriority(PrototypeWorkOrder order)
        {
            return checked(order.Priority + (int)PrototypeSettlementDirectiveCatalog.GetAssignmentScoreBonus(
                _activeDirective,
                order));
        }

        private bool IsHutOrder(PrototypeWorkOrder order)
        {
            return !string.IsNullOrWhiteSpace(order.StructureId) &&
                string.Equals(GetStructure(order.StructureId)?.StructureKindId, "hut", StringComparison.Ordinal);
        }

        internal PrototypeExtractionFrontierProbe PlanExtractionFrontierForTesting(
            IReadOnlyList<PrototypeWorkOrder> existingOrders,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            IReadOnlyList<(string ResourceId, int DesiredUnits, int BasePriority)> extractionClasses)
        {
            HashSet<string> activeClaimedOrderIds = BuildActiveClaimedOrderIds();
            List<PrototypeWorkOrder> orders = RemoveClaimedOrders(existingOrders.ToList(), activeClaimedOrderIds);
            int lookupsBefore = _pathPlanLookupsThisTick;
            int hitsBefore = _pathPlanCacheHitsThisTick;
            int missesBefore = _pathPlanCacheMissesThisTick;
            long fastPathHitsBefore = _cachedRouteDistanceFastPathHits;
            int omittedCount = 0;

            foreach ((string resourceId, int desiredUnits, int basePriority) in extractionClasses)
            {
                AddExtractionOrders(
                    orders,
                    resources,
                    resourceId,
                    desiredUnits,
                    basePriority,
                    activeClaimedOrderIds,
                    ref omittedCount);
            }

            orders = RemoveClaimedOrders(orders, activeClaimedOrderIds);
            int virtualUncappedCount = orders.Count + omittedCount;
            orders = ApplyWorkOrderFrontierLimit(orders, virtualUncappedCount);
            return new PrototypeExtractionFrontierProbe(
                orders.ToArray(),
                virtualUncappedCount,
                omittedCount,
                _pathPlanLookupsThisTick - lookupsBefore,
                _pathPlanCacheHitsThisTick - hitsBefore,
                _pathPlanCacheMissesThisTick - missesBefore,
                _cachedRouteDistanceFastPathHits - fastPathHitsBefore,
                CapturePerformanceProbeState());
        }

        private List<PrototypeWorkOrder> ApplyWorkOrderFrontierLimit(
            List<PrototypeWorkOrder> orders,
            int virtualUncappedCount)
        {
            if (_uncappedOrders)
            {
                return orders;
            }
            int frontierBudget = Math.Max(50, _citizens.Count * 5);
            return PrototypeExtractionPlanningMath.ApplyFrontierLimit(
                orders,
                frontierBudget,
                virtualUncappedCount,
                order => GetDirectiveAdjustedPriority(order));
        }
        private void AddRefuelOrders(List<PrototypeWorkOrder> orders)
        {
            PrototypeStructureState? hearth = GetStructure("central_hearth_1");
            if (hearth == null)
            {
                return;
            }

            int desiredFuel = Math.Max(4, _citizens.Count / 2);
            int deficit = Math.Max(0, desiredFuel - hearth.HearthFuel);
            int depotFirewood = _centralDepot.GetCount("firewood");

            for (int index = 0; index < Math.Min(deficit, depotFirewood); index++)
            {
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"refuel_{index + 1}",
                    Kind = PrototypeWorkOrderKind.RefuelHearth,
                    Priority = 1200,
                    ResourceId = "firewood",
                    SourceStoreId = _centralDepot.StoreId,
                    StructureId = hearth.StructureId,
                    Label = hearth.DisplayName,
                    Reason = "hearth fuel reserve",
                    TargetPosition = _centralDepot.Position,
                    Amount = 1
                });
            }
        }
        private void AddHaulOrdersFromStores(List<PrototypeWorkOrder> orders)
        {
            foreach (PrototypeResourceStoreState cache in _siteCaches.Values.OrderBy(store => store.StoreId, StringComparer.Ordinal))
            {
                PrototypeStructureState? remoteDepot = GetRemoteDepotStructure(cache.LinkedClusterId, requireBuilt: true);

                foreach ((string itemId, int amount) in cache.Items.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    for (int index = 0; index < amount; index++)
                    {
                        orders.Add(new PrototypeWorkOrder
                        {
                            OrderId = $"haul.cache.{cache.StoreId}.{itemId}.{index}",
                            Kind = remoteDepot == null ? PrototypeWorkOrderKind.HaulToDepot : PrototypeWorkOrderKind.HaulToRemoteDepot,
                            Priority = GetHaulPriority(itemId),
                            ResourceId = itemId,
                            SourceStoreId = cache.StoreId,
                            DestinationStoreId = remoteDepot?.OutputStore.StoreId ?? _centralDepot.StoreId,
                            StructureId = remoteDepot?.StructureId ?? string.Empty,
                            Label = remoteDepot?.DisplayName ?? "Central Depot",
                            Reason = remoteDepot == null ? "remote resource delivery" : "consolidate at remote depot",
                            TargetPosition = cache.Position,
                            Amount = 1
                        });
                    }
                }
            }

            foreach (PrototypeRemoteDepotState depot in _remoteDepots.Where(candidate => candidate.IsBuilt).OrderBy(candidate => candidate.StructureId, StringComparer.Ordinal))
            {
                PrototypeStructureState? structure = GetStructure(depot.StructureId);
                if (structure == null)
                {
                    continue;
                }

                foreach ((string itemId, int amount) in structure.OutputStore.Items.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    for (int index = 0; index < amount; index++)
                    {
                        orders.Add(new PrototypeWorkOrder
                        {
                            OrderId = $"haul.remote.{structure.StructureId}.{itemId}.{index}",
                            Kind = PrototypeWorkOrderKind.HaulFromRemoteDepot,
                            Priority = GetHaulPriority(itemId) + 10,
                            ResourceId = itemId,
                            SourceStoreId = structure.OutputStore.StoreId,
                            DestinationStoreId = _centralDepot.StoreId,
                            StructureId = structure.StructureId,
                            Label = "Central Depot",
                            Reason = "remote depot transfer",
                            TargetPosition = structure.Position,
                            Amount = 1
                        });
                    }
                }
            }

            foreach (PrototypeStructureState structure in _structures.Where(structure => structure.IsBuilt))
            {
                if (structure.StructureKindId == "remote_stockpile")
                {
                    continue;
                }

                foreach ((string itemId, int amount) in structure.OutputStore.Items.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    for (int index = 0; index < amount; index++)
                    {
                        orders.Add(new PrototypeWorkOrder
                        {
                            OrderId = $"haul.output.{structure.StructureId}.{itemId}.{index}",
                            Kind = PrototypeWorkOrderKind.HaulToDepot,
                            Priority = GetHaulPriority(itemId) + 20,
                            ResourceId = itemId,
                            SourceStoreId = structure.OutputStore.StoreId,
                            DestinationStoreId = _centralDepot.StoreId,
                            StructureId = structure.StructureId,
                            Label = "Central Depot",
                            Reason = $"collect {structure.DisplayName} output",
                            TargetPosition = structure.Position,
                            Amount = 1
                        });
                    }
                }
            }
        }
        private void AddProductionOrders(List<PrototypeWorkOrder> orders)
        {
            PrototypeStructureState? woodYard = _structures.FirstOrDefault(structure => structure.StructureKindId == "wood_yard" && structure.IsBuilt);
            if (woodYard != null)
            {
                AddWoodYardOrders(orders, woodYard);
            }

            PrototypeStructureState? cookfire = _structures.FirstOrDefault(structure => structure.StructureKindId == "cookfire" && structure.IsBuilt);
            if (cookfire != null)
            {
                AddCookfireOrders(orders, cookfire);
            }

            PrototypeStructureState? dryingRack = _structures.FirstOrDefault(structure => structure.StructureKindId == "drying_rack" && structure.IsBuilt);
            if (dryingRack != null)
            {
                AddProcessingOrders(orders, dryingRack, "reeds", 2, "thatch", 1, 780, "Turn reeds into thatch");
            }

            PrototypeStructureState? kiln = _structures.FirstOrDefault(structure => structure.StructureKindId == "kiln" && structure.IsBuilt);
            if (kiln != null)
            {
                AddKilnOrders(orders, kiln);
            }
        }
        private void AddWoodYardOrders(List<PrototypeWorkOrder> orders, PrototypeStructureState woodYard)
        {
            int firewoodShortfall = Math.Max(0, GetFirewoodTarget() - (_centralDepot.GetCount("firewood") + woodYard.OutputStore.GetCount("firewood")));
            int timberNeed = GetPendingConstructionRequirement("timber") - (_centralDepot.GetCount("timber") + woodYard.OutputStore.GetCount("timber"));

            AddStoreSupplyOrders(orders, woodYard, "logs", Math.Max(4, firewoodShortfall + Math.Max(0, timberNeed)));

            if (woodYard.InputStore.GetCount("logs") > 0 && woodYard.OutputStore.AvailableCapacity > 0)
            {
                string outputId = firewoodShortfall > 0 ? "firewood" : "timber";
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"process.{woodYard.StructureId}.{outputId}",
                    Kind = PrototypeWorkOrderKind.Process,
                    Priority = firewoodShortfall > 0 ? 930 : 760,
                    ResourceId = outputId,
                    StructureId = woodYard.StructureId,
                    Label = woodYard.DisplayName,
                    Reason = firewoodShortfall > 0 ? "fuel shortage" : "construction lumber",
                    TargetPosition = woodYard.Position,
                    Amount = 1
                });
            }
        }
        private void AddCookfireOrders(List<PrototypeWorkOrder> orders, PrototypeStructureState cookfire)
        {
            int mealShortfall = Math.Max(0, GetMealTarget() - (_centralDepot.GetCount("meals") + cookfire.OutputStore.GetCount("meals")));
            AddStoreSupplyOrders(orders, cookfire, "berries", Math.Max(2, mealShortfall * 2));
            AddStoreSupplyOrders(orders, cookfire, "firewood", Math.Max(1, mealShortfall));

            if (cookfire.InputStore.GetCount("berries") >= 2 &&
                cookfire.InputStore.GetCount("firewood") >= 1 &&
                cookfire.OutputStore.AvailableCapacity >= 2)
            {
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"process.{cookfire.StructureId}.meals",
                    Kind = PrototypeWorkOrderKind.Process,
                    Priority = 980,
                    ResourceId = "meals",
                    StructureId = cookfire.StructureId,
                    Label = cookfire.DisplayName,
                    Reason = "meal shortage",
                    TargetPosition = cookfire.Position,
                    Amount = 1
                });
            }
        }
        private void AddProcessingOrders(List<PrototypeWorkOrder> orders, PrototypeStructureState structure, string inputId, int inputAmount, string outputId, int outputAmount, int priority, string reason)
        {
            AddStoreSupplyOrders(orders, structure, inputId, Math.Max(inputAmount, GetPendingConstructionRequirement(outputId)));

            if (structure.InputStore.GetCount(inputId) >= inputAmount && structure.OutputStore.AvailableCapacity >= outputAmount)
            {
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"process.{structure.StructureId}.{outputId}",
                    Kind = PrototypeWorkOrderKind.Process,
                    Priority = priority,
                    ResourceId = outputId,
                    StructureId = structure.StructureId,
                    Label = structure.DisplayName,
                    Reason = reason,
                    TargetPosition = structure.Position,
                    Amount = 1
                });
            }
        }
        private void AddKilnOrders(List<PrototypeWorkOrder> orders, PrototypeStructureState kiln)
        {
            int brickNeed = Math.Max(0, GetPendingConstructionRequirement("brick") - (_centralDepot.GetCount("brick") + kiln.OutputStore.GetCount("brick")));
            if (brickNeed <= 0)
            {
                return;
            }

            AddStoreSupplyOrders(orders, kiln, "stone", brickNeed);
            AddStoreSupplyOrders(orders, kiln, "clay", brickNeed);
            AddStoreSupplyOrders(orders, kiln, "firewood", brickNeed);

            if (kiln.InputStore.GetCount("stone") >= 1 &&
                kiln.InputStore.GetCount("clay") >= 1 &&
                kiln.InputStore.GetCount("firewood") >= 1 &&
                kiln.OutputStore.AvailableCapacity >= 1)
            {
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"process.{kiln.StructureId}.brick",
                    Kind = PrototypeWorkOrderKind.Process,
                    Priority = 740,
                    ResourceId = "brick",
                    StructureId = kiln.StructureId,
                    Label = kiln.DisplayName,
                    Reason = "construction brick",
                    TargetPosition = kiln.Position,
                    Amount = 1
                });
            }
        }
        private void AddBuildOrders(List<PrototypeWorkOrder> orders)
        {
            foreach (PrototypeBuildQueueEntry entry in _buildQueue.Where(candidate => !candidate.IsPaused && !candidate.IsCompleted).OrderBy(candidate => candidate.Priority))
            {
                PrototypeStructureState? structure = GetStructure(entry.StructureId);
                if (structure == null)
                {
                    continue;
                }

                IReadOnlyDictionary<string, int> cost = GetConstructionCost(structure.StructureKindId);
                foreach ((string itemId, int amount) in cost)
                {
                    int shortfall = Math.Max(0, amount - structure.InputStore.GetCount(itemId));
                    for (int index = 0; index < shortfall && _centralDepot.GetCount(itemId) > 0; index++)
                    {
                        orders.Add(new PrototypeWorkOrder
                        {
                            OrderId = $"supply.{structure.StructureId}.{itemId}.{index}",
                            Kind = PrototypeWorkOrderKind.HaulToStructure,
                            Priority = structure.StructureKindId == "hut" ? 860 : 700,
                            ResourceId = itemId,
                            SourceStoreId = _centralDepot.StoreId,
                            DestinationStoreId = structure.InputStore.StoreId,
                            StructureId = structure.StructureId,
                            Label = structure.DisplayName,
                            Reason = $"construction of {structure.DisplayName}",
                            TargetPosition = _centralDepot.Position,
                            Amount = 1
                        });
                    }
                }

                if (cost.All(pair => structure.InputStore.GetCount(pair.Key) >= pair.Value))
                {
                    if (structure.StructureKindId == "path_segment" && ShouldPausePathBuildsDuringCriticalShortage() && HasCriticalShortage())
                    {
                        continue;
                    }

                    PrototypeWorkOrderKind buildKind = structure.StructureKindId switch
                    {
                        "path_segment" => PrototypeWorkOrderKind.BuildPath,
                        "remote_stockpile" => PrototypeWorkOrderKind.EstablishRemoteDepot,
                        _ => PrototypeWorkOrderKind.Build
                    };

                    orders.Add(new PrototypeWorkOrder
                    {
                        OrderId = $"build.{structure.StructureId}",
                        Kind = buildKind,
                        Priority = structure.StructureKindId switch
                        {
                            "hut" => 880,
                            "remote_stockpile" => 760,
                            "path_segment" => 610,
                            _ => 720
                        },
                        StructureId = structure.StructureId,
                        Label = structure.DisplayName,
                        Reason = structure.StructureKindId switch
                        {
                            "remote_stockpile" => "remote depot ready",
                            "path_segment" => "path corridor ready",
                            _ => "construction ready"
                        },
                        TargetPosition = structure.Position,
                        Amount = 1
                    });
                }
            }
        }
        private void AddReserveExtractionOrders(
            List<PrototypeWorkOrder> orders,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            IReadOnlyDictionary<string, int> committedCarries,
            float currentHour,
            PrototypeWeather weather,
            IReadOnlySet<string> activeClaimedOrderIds,
            ref int omittedExtractionOrderCount)
        {
            AddExtractionOrders(orders, resources, "logs", Math.Max(0, GetLogTarget() - GetAccessibleResourceCount("logs", committedCarries)), 640, activeClaimedOrderIds, ref omittedExtractionOrderCount);
            AddExtractionOrders(orders, resources, "berries", Math.Max(0, GetBerryTarget() - GetAccessibleResourceCount("berries", committedCarries)), 900, activeClaimedOrderIds, ref omittedExtractionOrderCount);
            AddExtractionOrders(orders, resources, "reeds", Math.Max(0, GetPendingConstructionRequirement("thatch") - GetAccessibleResourceCount("reeds", committedCarries)), 700, activeClaimedOrderIds, ref omittedExtractionOrderCount);
            AddExtractionOrders(orders, resources, "stone", Math.Max(0, GetPendingConstructionRequirement("stone") - GetAccessibleResourceCount("stone", committedCarries)), 620, activeClaimedOrderIds, ref omittedExtractionOrderCount);
            AddExtractionOrders(orders, resources, "clay", Math.Max(0, GetPendingConstructionRequirement("clay") - GetAccessibleResourceCount("clay", committedCarries)), 620, activeClaimedOrderIds, ref omittedExtractionOrderCount);
        }
        private void AddExtractionOrders(
            List<PrototypeWorkOrder> orders,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            string resourceId,
            int desiredUnits,
            int priority,
            IReadOnlySet<string> activeClaimedOrderIds,
            ref int omittedExtractionOrderCount)
        {
            if (desiredUnits <= 0)
            {
                return;
            }

            List<PrototypeResourceSiteState> eligibleSites = resources
                .Where(site => site.ResourceId == resourceId && site.UnitsRemaining > 0)
                .ToList();
            bool hasBuiltCorridor = _pathSegments.Any(segment =>
                segment.IsBuilt &&
                string.Equals(segment.CorridorId, $"corridor.{resourceId}", StringComparison.Ordinal));
            int priorityUpperBound = PrototypeExtractionPlanningMath.ComputePriorityUpperBound(
                priority,
                hasBuiltCorridor);
            (PrototypeDirectiveAffinity directiveAffinity, string directiveCause) = GetDirectiveMetadataForResource(resourceId);
            int effectivePriorityUpperBound = checked(priorityUpperBound +
                (int)PrototypeSettlementDirectiveCatalog.GetAssignmentScoreBonus(_activeDirective, directiveAffinity));
            int frontierBudget = Math.Max(50, _citizens.Count * 5);
            if (_extractionPlanningMode == PrototypeExtractionPlanningMode.ExactBounded &&
                !_uncappedOrders &&
                PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                    orders.Select(GetDirectiveAdjustedPriority).ToArray(),
                    frontierBudget,
                    effectivePriorityUpperBound,
                    eligibleSites.Select(site => $"extract.{site.NodeName}").ToArray(),
                    activeClaimedOrderIds,
                    desiredUnits,
                    out int omittedCount))
            {
                omittedExtractionOrderCount += omittedCount;
                return;
            }

            List<PrototypeExtractionCandidate> candidates = eligibleSites
                .Select((site, originalIndex) =>
                {
                    Vector3 interactionPosition = TryResolveWalkableInteractionPosition(site.Position, out Vector3 resolvedPosition)
                        ? resolvedPosition
                        : site.Position;
                    float distanceLowerBound = PrototypeOrderSelectionMath.ComputeStraightLineDistanceLowerBound(
                        _world.SettlementSpawn.AnchorPosition,
                        interactionPosition,
                        _world.WorldMap.Cells.Count);
                    return new PrototypeExtractionCandidate(site, interactionPosition, distanceLowerBound, originalIndex);
                })
                .ToList();

            if (_extractionPlanningMode == PrototypeExtractionPlanningMode.ExactBounded &&
                ShouldBuildGeometricDistanceField(
                    _world.SettlementSpawn.AnchorPosition,
                    candidates.Select(candidate => candidate.InteractionPosition)))
            {
                candidates = candidates
                    .Select(candidate => candidate with
                    {
                        DistanceLowerBound = ComputeRouteDistanceLowerBound(
                            _world.SettlementSpawn.AnchorPosition,
                            candidate.InteractionPosition)
                    })
                    .ToList();
            }

            IReadOnlyList<PrototypeExtractionCandidate> sites = _extractionPlanningMode == PrototypeExtractionPlanningMode.ExhaustiveReference
                ? candidates
                    .OrderBy(candidate => ComputeRouteDistance(_world.SettlementSpawn.AnchorPosition, candidate.InteractionPosition))
                    .ThenBy(candidate => candidate.Site.NodeName, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.OriginalIndex)
                    .Take(desiredUnits)
                    .ToArray()
                : PrototypeExtractionPlanningMath.SelectExactTopK(
                    candidates,
                    desiredUnits,
                    candidate => ComputeRouteDistance(_world.SettlementSpawn.AnchorPosition, candidate.InteractionPosition));

            bool useDepotTopologyBounds = _extractionPlanningMode == PrototypeExtractionPlanningMode.ExactBounded &&
                ShouldBuildGeometricDistanceField(
                    _centralDepot.Position,
                    sites.Select(candidate => candidate.InteractionPosition));
            foreach (PrototypeExtractionCandidate candidate in sites)
            {
                PrototypeResourceSiteState site = candidate.Site;
                string orderId = $"extract.{site.NodeName}";
                Vector3 interactionPosition = candidate.InteractionPosition;
                bool hasRemoteDepot = GetRemoteDepot(site.ClusterId, requireBuilt: true) != null;
                int adjustedPriority = priorityUpperBound;
                float activationDistance = GetRemoteDepotActivationDistance();
                float depotDistanceLowerBound = useDepotTopologyBounds
                    ? ComputeRouteDistanceLowerBound(_centralDepot.Position, interactionPosition)
                    : PrototypeOrderSelectionMath.ComputeStraightLineDistanceLowerBound(
                        _centralDepot.Position,
                        interactionPosition,
                        _world.WorldMap.Cells.Count);
                bool applyRemoteDepotPenalty = _extractionPlanningMode == PrototypeExtractionPlanningMode.ExhaustiveReference
                    ? ComputeRouteDistance(_centralDepot.Position, interactionPosition) > activationDistance && !hasRemoteDepot
                    : PrototypeExtractionPlanningMath.ShouldApplyRemoteDepotPenalty(
                        hasRemoteDepot,
                        depotDistanceLowerBound,
                        activationDistance,
                        () => ComputeRouteDistance(_centralDepot.Position, interactionPosition));
                if (applyRemoteDepotPenalty)
                {
                    adjustedPriority -= 140;
                }

                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = orderId,
                    Kind = PrototypeWorkOrderKind.Extract,
                    Priority = adjustedPriority,
                    ResourceId = resourceId,
                    TargetNodeName = site.NodeName,
                    ClusterId = site.ClusterId,
                    Label = PrototypeSettlementLayout.GetResourceTargetLabel(resourceId),
                    Reason = $"reserve target for {InventoryComponent.FormatItemName(resourceId)}",
                    DirectiveAffinity = directiveAffinity,
                    DirectiveCause = directiveCause,
                    TargetPosition = interactionPosition,
                    Amount = 1
                });
            }
        }
        private bool TryResolveWalkableInteractionPosition(Vector3 resourcePosition, out Vector3 interactionPosition)
        {
            TerrainCell resourceCell = _world.WorldMap.GetNearestCell(resourcePosition);
            if (IsWalkableTerrainCell(resourceCell))
            {
                interactionPosition = resourcePosition;
                return true;
            }

            Vector2I cacheKey = new(resourceCell.GridX, resourceCell.GridY);
            if (!_walkableInteractionPositions.TryGetValue(cacheKey, out Vector3? cachedPosition))
            {
                TerrainCell? interactionCell = _world.WorldMap.Cells
                    .Where(IsWalkableTerrainCell)
                    .OrderBy(candidate => GetHorizontalDistance(candidate.WorldPosition, resourcePosition))
                    .ThenBy(candidate => candidate.GridY)
                    .ThenBy(candidate => candidate.GridX)
                    .FirstOrDefault();
                cachedPosition = interactionCell?.WorldPosition;
                _walkableInteractionPositions[cacheKey] = cachedPosition;
            }

            if (cachedPosition.HasValue)
            {
                interactionPosition = cachedPosition.Value;
                return true;
            }

            interactionPosition = resourcePosition;
            return false;
        }
        private static bool IsWalkableTerrainCell(TerrainCell? cell)
        {
            return cell != null && cell.Biome != BiomeType.Wetland && cell.SlopeDegrees <= 18.0f;
        }
        private void AddStoreSupplyOrders(List<PrototypeWorkOrder> orders, PrototypeStructureState structure, string resourceId, int desiredAmount)
        {
            int available = _centralDepot.GetCount(resourceId);
            int shortfall = Math.Max(0, desiredAmount - structure.InputStore.GetCount(resourceId));
            int count = Math.Min(shortfall, available);

            for (int index = 0; index < count; index++)
            {
                orders.Add(new PrototypeWorkOrder
                {
                    OrderId = $"supply.{structure.StructureId}.{resourceId}.op.{index}",
                    Kind = PrototypeWorkOrderKind.HaulToStructure,
                    Priority = GetSupplyPriority(structure.StructureKindId, resourceId),
                    ResourceId = resourceId,
                    SourceStoreId = _centralDepot.StoreId,
                    DestinationStoreId = structure.InputStore.StoreId,
                    StructureId = structure.StructureId,
                    Label = structure.DisplayName,
                    Reason = $"supply {structure.DisplayName}",
                    TargetPosition = _centralDepot.Position,
                    Amount = 1
                });
            }
        }
        private HashSet<string> BuildActiveClaimedOrderIds()
        {
            return _citizens
                .Where(citizen => !string.IsNullOrWhiteSpace(citizen.CurrentOrderId) && citizen.Phase != PrototypeWorkerPhase.Idle && citizen.Phase != PrototypeWorkerPhase.Incapacitated)
                .Select(citizen => citizen.CurrentOrderId)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static List<PrototypeWorkOrder> RemoveClaimedOrders(
            List<PrototypeWorkOrder> orders,
            IReadOnlySet<string> claimedOrderIds)
        {
            return orders
                .Where(order => !claimedOrderIds.Contains(order.OrderId))
                .ToList();
        }
        private Dictionary<string, int> BuildSettlementSummary()
        {
            Dictionary<string, int> summary = new(StringComparer.Ordinal);

            foreach ((string itemId, int amount) in _centralDepot.Items)
            {
                summary[itemId] = amount;
            }

            summary["beds"] = BedCapacity;
            summary["hearth_fuel"] = HearthFuel;
            summary["huts"] = _structures.Count(structure => structure.StructureKindId == "hut" && structure.IsBuilt);
            summary["storehouses"] = _structures.Count(structure => structure.StructureKindId == "storehouse" && structure.IsBuilt);
            summary["remote_depots"] = _remoteDepots.Count(depot => depot.IsBuilt);
            summary["path_segments"] = _pathSegments.Count(segment => segment.IsBuilt);
            return summary;
        }
        private PrototypeResourceStoreState CreateStore(string id, string displayName, int capacity, Vector3 position, params string[] allowedItems)
        {
            PrototypeResourceStoreState store = new()
            {
                StoreId = id,
                DisplayName = displayName,
                Capacity = capacity,
                Position = position
            };

            foreach (string allowedItem in allowedItems)
            {
                store.AllowedResourceIds.Add(allowedItem);
            }

            return store;
        }
        private void UpdateRouteBacklogMetrics(IReadOnlyList<PrototypeWorkOrder> backlog)
        {
            Dictionary<string, int> currentBacklog = backlog
                .GroupBy(order => order.Kind)
                .ToDictionary(group => group.Key.ToString().ToLowerInvariant(), group => group.Count(), StringComparer.Ordinal);

            foreach (string key in _routeBacklogTicksByKind.Keys.Concat(currentBacklog.Keys).Distinct(StringComparer.Ordinal).ToList())
            {
                _routeBacklogTicksByKind[key] = currentBacklog.ContainsKey(key)
                    ? _routeBacklogTicksByKind.GetValueOrDefault(key) + 1
                    : 0;
            }
        }
        private bool HasCriticalShortage()
        {
            return MealCoveragePercent <= 20 || HearthFuel <= 0;
        }
        private int GetAccessibleResourceCount(string resourceId, IReadOnlyDictionary<string, int> committedCarries) =>
            _siteCaches.Values.Sum(store => store.GetCount(resourceId)) +
            _centralDepot.GetCount(resourceId) +
            _structures.Sum(structure => structure.OutputStore.GetCount(resourceId)) +
            committedCarries.GetValueOrDefault(resourceId);
        private int GetMealTarget() => Math.Max(8, _citizens.Count * 3);
        private int GetFirewoodTarget() => Math.Max(6, _citizens.Count * 2);
        public int GetActiveOrderCount() =>
            _citizens.Count(citizen =>
                citizen.Phase != PrototypeWorkerPhase.Idle &&
                citizen.Phase != PrototypeWorkerPhase.Incapacitated);
        private int GetLogTarget() => Math.Max(8, GetPendingConstructionRequirement("timber") + GetPendingConstructionRequirement("firewood") + 4);
        private int GetBerryTarget() => Math.Max(6, _citizens.Count * 2);
        private int GetPendingConstructionRequirement(string resourceId)
        {
            int total = 0;
            foreach (PrototypeBuildQueueEntry entry in _buildQueue.Where(candidate => !candidate.IsPaused && !candidate.IsCompleted))
            {
                IReadOnlyDictionary<string, int> cost = GetConstructionCost(entry.StructureKindId);
                total += cost.GetValueOrDefault(resourceId);
                if (entry.StructureKindId == "kiln" && resourceId is "firewood" or "clay" or "stone")
                {
                    total += 4;
                }
            }

            return total;
        }
        private static int GetHaulPriority(string itemId) => itemId switch
        {
            "meals" => 1020,
            "firewood" => 980,
            "berries" => 920,
            "timber" => 760,
            "thatch" => 740,
            "brick" => 720,
            _ => 680
        };
        private static int GetSupplyPriority(string structureKindId, string resourceId) => structureKindId switch
        {
            "cookfire" => resourceId == "firewood" ? 950 : 940,
            "wood_yard" => 700,
            "drying_rack" => 760,
            "kiln" => 720,
            "hut" => 880,
            "remote_stockpile" => 730,
            _ => 700
        };
        private static int GetProcessingTicks(string structureId, string outputId) => outputId switch
        {
            "firewood" => 18,
            "timber" => 20,
            "meals" => 18,
            "thatch" => 20,
            "brick" => 26,
            _ => 20
        };
        private PrototypeResourceStoreState? GetStore(string storeId)
        {
            if (string.Equals(storeId, _centralDepot.StoreId, StringComparison.Ordinal))
            {
                return _centralDepot;
            }

            if (_siteCaches.TryGetValue(storeId, out PrototypeResourceStoreState? cache))
            {
                return cache;
            }

            foreach (PrototypeStructureState structure in _structures)
            {
                if (string.Equals(structure.InputStore.StoreId, storeId, StringComparison.Ordinal))
                {
                    return structure.InputStore;
                }

                if (string.Equals(structure.OutputStore.StoreId, storeId, StringComparison.Ordinal))
                {
                    return structure.OutputStore;
                }
            }

            return null;
        }
        private Vector3 GetStorePosition(string storeId) => GetStore(storeId)?.Position ?? _world.SettlementSpawn.AnchorPosition;
        private string GetStoreLabel(string storeId) => GetStore(storeId)?.DisplayName ?? "Store";

    }
}
