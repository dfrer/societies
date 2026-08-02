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
        private int _lightweightExtractionFrontierActivations;

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
            PrototypeWeather weather,
            RuntimeMetricsCollector? runtimeMetrics)
        {
            Dictionary<string, int> committedCarries;
            HashSet<string> activeClaimedOrderIds;
            RuntimeMetricsPhaseToken inputPreparationPhase = runtimeMetrics?.BeginPhase(
                RuntimeMetricsPhase.BuildWorkOrdersInputPreparation) ?? default;
            try
            {
                _lightweightExtractionFrontierActivations = 0;
                committedCarries = _citizens
                    .Where(citizen => citizen.CarryAmount > 0)
                    .GroupBy(citizen => citizen.CarryItemId)
                    .ToDictionary(group => group.Key, group => group.Sum(citizen => citizen.CarryAmount), StringComparer.Ordinal);
                activeClaimedOrderIds = BuildActiveClaimedOrderIds();
            }
            finally
            {
                inputPreparationPhase.Complete();
            }

            List<PrototypeWorkOrder> orders;
            RuntimeMetricsPhaseToken nonExtractionPhase = runtimeMetrics?.BeginPhase(
                RuntimeMetricsPhase.BuildWorkOrdersNonExtraction) ?? default;
            try
            {
                orders = new List<PrototypeWorkOrder>();
                AddRefuelOrders(orders);
                AddHaulOrdersFromStores(orders);
                AddProductionOrders(orders);
                AddBuildOrders(orders);
                orders = RemoveClaimedOrders(orders, activeClaimedOrderIds);
                AnnotateDirectiveAffinities(orders);
            }
            finally
            {
                nonExtractionPhase.Complete();
            }

            int omittedExtractionOrderCount = 0;
            int unmaterializedExtractionOrderCount = 0;
            bool useLightweightExtractionFrontier =
                _extractionPlanningMode == PrototypeExtractionPlanningMode.ExactBounded && !_uncappedOrders;
            HashSet<string>? generatedExtractionNodeNames = null;
            HashSet<string>? exhaustiveProjectedOmittedOrderIds =
                _extractionPlanningMode == PrototypeExtractionPlanningMode.ExhaustiveReference
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : null;
            int exhaustiveProjectedOmittedOrderCount = 0;
            RuntimeMetricsPhaseToken reserveExtractionPhase = runtimeMetrics?.BeginPhase(
                RuntimeMetricsPhase.BuildWorkOrdersReserveExtraction) ?? default;
            try
            {
                AddReserveExtractionOrders(
                    orders,
                    resources,
                    committedCarries,
                    currentHour,
                    weather,
                    activeClaimedOrderIds,
                    ref omittedExtractionOrderCount,
                    ref unmaterializedExtractionOrderCount,
                    ref useLightweightExtractionFrontier,
                    ref generatedExtractionNodeNames,
                    exhaustiveProjectedOmittedOrderIds,
                    ref exhaustiveProjectedOmittedOrderCount,
                    runtimeMetrics);
            }
            finally
            {
                reserveExtractionPhase.Complete();
            }

            RuntimeMetricsPhaseToken finalizationPhase = runtimeMetrics?.BeginPhase(
                RuntimeMetricsPhase.BuildWorkOrdersFinalization) ?? default;
            try
            {
                orders = RemoveClaimedOrders(orders, activeClaimedOrderIds);
                _workOrdersGeneratedUncappedThisTick = orders.Count +
                    omittedExtractionOrderCount +
                    unmaterializedExtractionOrderCount;
                _extractionOrdersOmittedThisTick = omittedExtractionOrderCount;
                if (_extractionPlanningMode == PrototypeExtractionPlanningMode.ExhaustiveReference)
                {
                    List<PrototypeWorkOrder> projectedOrders = orders
                        .Where(order => !exhaustiveProjectedOmittedOrderIds!.Contains(order.OrderId))
                        .ToList();
                    projectedOrders = ApplyWorkOrderFrontierLimit(
                        projectedOrders,
                        projectedOrders.Count + exhaustiveProjectedOmittedOrderCount);
                    UpdateRouteBacklogMetrics(projectedOrders);
                }
                orders = ApplyWorkOrderFrontierLimit(orders, _workOrdersGeneratedUncappedThisTick);
                return orders;
            }
            finally
            {
                finalizationPhase.Complete();
            }
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
            _lightweightExtractionFrontierActivations = 0;
            HashSet<string> activeClaimedOrderIds = BuildActiveClaimedOrderIds();
            List<PrototypeWorkOrder> orders = RemoveClaimedOrders(existingOrders.ToList(), activeClaimedOrderIds);
            int lookupsBefore = _pathPlanLookupsThisTick;
            int hitsBefore = _pathPlanCacheHitsThisTick;
            int missesBefore = _pathPlanCacheMissesThisTick;
            long fastPathHitsBefore = _cachedRouteDistanceFastPathHits;
            int omittedCount = 0;
            int unmaterializedCount = 0;
            bool useLightweightExtractionFrontier =
                _extractionPlanningMode == PrototypeExtractionPlanningMode.ExactBounded && !_uncappedOrders;
            HashSet<string>? generatedExtractionNodeNames = null;
            int projectedOmittedCount = 0;

            foreach ((string resourceId, int desiredUnits, int basePriority) in extractionClasses)
            {
                AddExtractionOrders(
                    orders,
                    resources,
                    resourceId,
                    desiredUnits,
                    basePriority,
                    activeClaimedOrderIds,
                    ref omittedCount,
                    ref unmaterializedCount,
                    ref useLightweightExtractionFrontier,
                    ref generatedExtractionNodeNames,
                    null,
                    ref projectedOmittedCount,
                    null);
            }

            orders = RemoveClaimedOrders(orders, activeClaimedOrderIds);
            int virtualUncappedCount = orders.Count + omittedCount + unmaterializedCount;
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
            int committedFirewood = _structures.Sum(structure => structure.InputStore.GetCount("firewood")) + HearthFuel;
            int firewoodShortfall = Math.Max(0, GetFirewoodTarget() - (_centralDepot.GetCount("firewood") + woodYard.OutputStore.GetCount("firewood") + committedFirewood));
            int timberNeed = GetPendingConstructionRequirement("timber") - (_centralDepot.GetCount("timber") + woodYard.OutputStore.GetCount("timber"));

            AddStoreSupplyOrders(orders, woodYard, "logs", Math.Max(4, firewoodShortfall + Math.Max(0, timberNeed)));

            if (woodYard.InputStore.GetCount("logs") > 0 && woodYard.OutputStore.AvailableCapacity > 0)
            {
                if (firewoodShortfall > 0)
                {
                    AddWoodYardProcessOrder(orders, woodYard, "firewood", 930, "fuel shortage");
                }

                if (timberNeed > 0)
                {
                    AddWoodYardProcessOrder(orders, woodYard, "timber", 760, "construction lumber");
                }
            }
        }
        private static void AddWoodYardProcessOrder(
            List<PrototypeWorkOrder> orders,
            PrototypeStructureState woodYard,
            string outputId,
            int priority,
            string reason)
        {
            orders.Add(new PrototypeWorkOrder
            {
                OrderId = $"process.{woodYard.StructureId}.{outputId}",
                Kind = PrototypeWorkOrderKind.Process,
                Priority = priority,
                ResourceId = outputId,
                StructureId = woodYard.StructureId,
                Label = woodYard.DisplayName,
                Reason = reason,
                TargetPosition = woodYard.Position,
                Amount = 1
            });
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
            HashSet<string> activeClaimedOrderIds,
            ref int omittedExtractionOrderCount,
            ref int unmaterializedExtractionOrderCount,
            ref bool useLightweightExtractionFrontier,
            ref HashSet<string>? generatedExtractionNodeNames,
            HashSet<string>? exhaustiveProjectedOmittedOrderIds,
            ref int exhaustiveProjectedOmittedOrderCount,
            RuntimeMetricsCollector? runtimeMetrics)
        {
            AddExtractionOrders(orders, resources, "logs", Math.Max(0, GetLogTarget() - GetAccessibleResourceCount("logs", committedCarries)), 640, activeClaimedOrderIds, ref omittedExtractionOrderCount, ref unmaterializedExtractionOrderCount, ref useLightweightExtractionFrontier, ref generatedExtractionNodeNames, exhaustiveProjectedOmittedOrderIds, ref exhaustiveProjectedOmittedOrderCount, runtimeMetrics);
            AddExtractionOrders(orders, resources, "berries", Math.Max(0, GetBerryTarget() - GetAccessibleResourceCount("berries", committedCarries)), 900, activeClaimedOrderIds, ref omittedExtractionOrderCount, ref unmaterializedExtractionOrderCount, ref useLightweightExtractionFrontier, ref generatedExtractionNodeNames, exhaustiveProjectedOmittedOrderIds, ref exhaustiveProjectedOmittedOrderCount, runtimeMetrics);
            AddExtractionOrders(orders, resources, "reeds", Math.Max(0, GetPendingConstructionRequirement("thatch") - GetAccessibleResourceCount("reeds", committedCarries)), 700, activeClaimedOrderIds, ref omittedExtractionOrderCount, ref unmaterializedExtractionOrderCount, ref useLightweightExtractionFrontier, ref generatedExtractionNodeNames, exhaustiveProjectedOmittedOrderIds, ref exhaustiveProjectedOmittedOrderCount, runtimeMetrics);
            AddExtractionOrders(orders, resources, "stone", Math.Max(0, GetPendingConstructionRequirement("stone") - GetAccessibleResourceCount("stone", committedCarries)), 620, activeClaimedOrderIds, ref omittedExtractionOrderCount, ref unmaterializedExtractionOrderCount, ref useLightweightExtractionFrontier, ref generatedExtractionNodeNames, exhaustiveProjectedOmittedOrderIds, ref exhaustiveProjectedOmittedOrderCount, runtimeMetrics);
            AddExtractionOrders(orders, resources, "clay", Math.Max(0, GetPendingConstructionRequirement("clay") - GetAccessibleResourceCount("clay", committedCarries)), 620, activeClaimedOrderIds, ref omittedExtractionOrderCount, ref unmaterializedExtractionOrderCount, ref useLightweightExtractionFrontier, ref generatedExtractionNodeNames, exhaustiveProjectedOmittedOrderIds, ref exhaustiveProjectedOmittedOrderCount, runtimeMetrics);
        }
        private void AddExtractionOrders(
            List<PrototypeWorkOrder> orders,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            string resourceId,
            int desiredUnits,
            int priority,
            HashSet<string> activeClaimedOrderIds,
            ref int omittedExtractionOrderCount,
            ref int unmaterializedExtractionOrderCount,
            ref bool useLightweightExtractionFrontier,
            ref HashSet<string>? generatedExtractionNodeNames,
            HashSet<string>? exhaustiveProjectedOmittedOrderIds,
            ref int exhaustiveProjectedOmittedOrderCount,
            RuntimeMetricsCollector? runtimeMetrics)
        {
            if (desiredUnits <= 0)
            {
                return;
            }

            List<PrototypeResourceSiteState> eligibleSites;
            int priorityUpperBound;
            PrototypeDirectiveAffinity directiveAffinity;
            string directiveCause;
            int frontierBudget;
            bool omitFromExhaustiveDiagnosticProjection;
            RuntimeMetricsPhaseToken classPreparationPhase = runtimeMetrics?.BeginPhase(
                RuntimeMetricsPhase.ReserveExtractionClassPreparation) ?? default;
            try
            {
                eligibleSites = resources
                    .Where(site => site.ResourceId == resourceId && site.UnitsRemaining > 0)
                    .ToList();
                bool hasBuiltCorridor = _pathSegments.Any(segment =>
                    segment.IsBuilt &&
                    string.Equals(segment.CorridorId, $"corridor.{resourceId}", StringComparison.Ordinal));
                priorityUpperBound = PrototypeExtractionPlanningMath.ComputePriorityUpperBound(
                    priority,
                    hasBuiltCorridor);
                (directiveAffinity, directiveCause) = GetDirectiveMetadataForResource(resourceId);
                int effectivePriorityUpperBound = checked(priorityUpperBound +
                    (int)PrototypeSettlementDirectiveCatalog.GetAssignmentScoreBonus(_activeDirective, directiveAffinity));
                frontierBudget = Math.Max(50, _citizens.Count * 5);
                int projectedOmittedCount = 0;
                omitFromExhaustiveDiagnosticProjection =
                    _extractionPlanningMode == PrototypeExtractionPlanningMode.ExhaustiveReference &&
                    !_uncappedOrders &&
                    exhaustiveProjectedOmittedOrderIds != null &&
                    PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                        orders
                            .Where(order => !exhaustiveProjectedOmittedOrderIds.Contains(order.OrderId))
                            .Select(GetDirectiveAdjustedPriority)
                            .ToArray(),
                        frontierBudget,
                        effectivePriorityUpperBound,
                        eligibleSites.Select(site => $"extract.{site.NodeName}").ToArray(),
                        activeClaimedOrderIds,
                        desiredUnits,
                        out projectedOmittedCount);
                if (omitFromExhaustiveDiagnosticProjection)
                {
                    exhaustiveProjectedOmittedOrderCount += projectedOmittedCount;
                }
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
            }
            finally
            {
                classPreparationPhase.Complete();
            }

            int firstGeneratedOrderIndex = orders.Count;
            IReadOnlyList<PrototypeExtractionCandidate> sites;
            RuntimeMetricsPhaseToken candidatePhase = runtimeMetrics?.BeginPhase(
                RuntimeMetricsPhase.ReserveExtractionCandidateEnumerationAndBoundSelection) ?? default;
            try
            {
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

                sites = _extractionPlanningMode == PrototypeExtractionPlanningMode.ExhaustiveReference
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
            }
            finally
            {
                candidatePhase.Complete();
            }

            bool useDepotTopologyBounds;
            RuntimeMetricsPhaseToken frontierPreparationPhase = runtimeMetrics?.BeginPhase(
                RuntimeMetricsPhase.ReserveExtractionActiveFrontierAndClaimEvaluation) ?? default;
            try
            {
                int materializationBudget = checked(frontierBudget + activeClaimedOrderIds.Count);
                int guaranteedAvoidedOrderCount = sites.Count - materializationBudget;
                if (useLightweightExtractionFrontier && guaranteedAvoidedOrderCount >= frontierBudget)
                {
                    generatedExtractionNodeNames ??= new HashSet<string>(StringComparer.Ordinal);
                    foreach (PrototypeExtractionCandidate candidate in sites)
                    {
                        if (!generatedExtractionNodeNames.Add(candidate.Site.NodeName))
                        {
                            useLightweightExtractionFrontier = false;
                        }
                    }
                }

                useDepotTopologyBounds = _extractionPlanningMode == PrototypeExtractionPlanningMode.ExactBounded &&
                    ShouldBuildGeometricDistanceField(
                        _centralDepot.Position,
                        sites.Select(candidate => candidate.InteractionPosition));
            }
            finally
            {
                frontierPreparationPhase.Complete();
            }

            if (TryAddLightweightExtractionFrontier(
                orders,
                sites,
                resourceId,
                priorityUpperBound,
                directiveAffinity,
                directiveCause,
                activeClaimedOrderIds,
                frontierBudget,
                useDepotTopologyBounds,
                ref unmaterializedExtractionOrderCount,
                ref useLightweightExtractionFrontier,
                runtimeMetrics))
            {
                return;
            }

            RuntimeMetricsPhaseToken materializationPhase = runtimeMetrics?.BeginPhase(
                RuntimeMetricsPhase.ReserveExtractionRetainedMaterialization) ?? default;
            try
            {
                foreach (PrototypeExtractionCandidate candidate in sites)
                {
                    int adjustedPriority = ComputeExtractionPriority(
                        candidate,
                        priorityUpperBound,
                        useDepotTopologyBounds);
                    orders.Add(CreateExtractionOrder(
                        candidate,
                        resourceId,
                        adjustedPriority,
                        directiveAffinity,
                        directiveCause));
                }
                if (omitFromExhaustiveDiagnosticProjection)
                {
                    foreach (PrototypeWorkOrder order in orders.Skip(firstGeneratedOrderIndex))
                    {
                        exhaustiveProjectedOmittedOrderIds!.Add(order.OrderId);
                    }
                }
            }
            finally
            {
                materializationPhase.Complete();
            }
        }

        private bool TryAddLightweightExtractionFrontier(
            List<PrototypeWorkOrder> orders,
            IReadOnlyList<PrototypeExtractionCandidate> sites,
            string resourceId,
            int priorityUpperBound,
            PrototypeDirectiveAffinity directiveAffinity,
            string directiveCause,
            HashSet<string> activeClaimedOrderIds,
            int frontierBudget,
            bool useDepotTopologyBounds,
            ref int unmaterializedExtractionOrderCount,
            ref bool useLightweightExtractionFrontier,
            RuntimeMetricsCollector? runtimeMetrics)
        {
            PriorityQueue<PrototypeExtractionFrontierEntry, PrototypeExtractionFrontierPriority> frontier;
            int unclaimedSiteCount = 0;
            RuntimeMetricsPhaseToken evaluationPhase = runtimeMetrics?.BeginPhase(
                RuntimeMetricsPhase.ReserveExtractionActiveFrontierAndClaimEvaluation) ?? default;
            try
            {
                if (!useLightweightExtractionFrontier ||
                    sites.Count == 0)
                {
                    return false;
                }

                int materializationBudget = checked(frontierBudget + activeClaimedOrderIds.Count);
                int guaranteedAvoidedOrderCount = sites.Count - materializationBudget;
                if (guaranteedAvoidedOrderCount < frontierBudget)
                {
                    return false;
                }

                HashSet<string> existingOrderIds = new(StringComparer.Ordinal);
                foreach (PrototypeWorkOrder order in orders)
                {
                    if (!existingOrderIds.Add(order.OrderId))
                    {
                        useLightweightExtractionFrontier = false;
                        return false;
                    }
                }
                foreach (string existingOrderId in existingOrderIds)
                {
                    if (!existingOrderId.StartsWith("extract.", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    foreach (PrototypeExtractionCandidate candidate in sites)
                    {
                        if (ExtractionOrderIdEqualsNodeName(existingOrderId, candidate.Site.NodeName))
                        {
                            useLightweightExtractionFrontier = false;
                            return false;
                        }
                    }
                }

                frontier = new(PrototypeExtractionFrontierPriorityComparer.Instance);

                void Offer(PrototypeExtractionFrontierEntry entry)
                {
                    frontier.Enqueue(
                        entry,
                        new PrototypeExtractionFrontierPriority(
                            entry.EffectivePriority,
                            entry.OrderKey,
                            entry.OrderKeyIsExtractionNodeName));
                    if (frontier.Count > materializationBudget)
                    {
                        frontier.Dequeue();
                    }
                }

                foreach (PrototypeWorkOrder order in orders)
                {
                    Offer(new PrototypeExtractionFrontierEntry(
                        GetDirectiveAdjustedPriority(order),
                        order.OrderId,
                        false,
                        false,
                        null,
                        0));
                }

                for (int index = 0; index < sites.Count; index++)
                {
                    PrototypeExtractionCandidate candidate = sites[index];
                    string nodeName = candidate.Site.NodeName;
                    bool isActiveClaim = HasActiveExtractionClaim(activeClaimedOrderIds, nodeName);
                    if (!isActiveClaim)
                    {
                        unclaimedSiteCount++;
                    }
                    int adjustedPriority = ComputeExtractionPriority(
                        candidate,
                        priorityUpperBound,
                        useDepotTopologyBounds);
                    int effectivePriority = checked(adjustedPriority +
                        (int)PrototypeSettlementDirectiveCatalog.GetAssignmentScoreBonus(_activeDirective, directiveAffinity));
                    PrototypeExtractionFrontierEntry evaluated = new(
                        effectivePriority,
                        nodeName,
                        true,
                        isActiveClaim,
                        candidate,
                        adjustedPriority);
                    Offer(evaluated);
                }
            }
            finally
            {
                evaluationPhase.Complete();
            }

            RuntimeMetricsPhaseToken materializationPhase = runtimeMetrics?.BeginPhase(
                RuntimeMetricsPhase.ReserveExtractionRetainedMaterialization) ?? default;
            try
            {
                int retainedUnclaimedCount = 0;
                foreach (var item in frontier.UnorderedItems)
                {
                    PrototypeExtractionFrontierEntry entry = item.Element;
                    if (entry.Candidate.HasValue &&
                        !entry.IsActiveClaim)
                    {
                        retainedUnclaimedCount++;
                    }
                }
                unmaterializedExtractionOrderCount += unclaimedSiteCount - retainedUnclaimedCount;
                foreach (var item in frontier.UnorderedItems)
                {
                    PrototypeExtractionFrontierEntry retained = item.Element;
                    if (retained.Candidate.HasValue)
                    {
                        orders.Add(CreateExtractionOrder(
                            retained.Candidate.Value,
                            resourceId,
                            retained.AdjustedPriority,
                            directiveAffinity,
                            directiveCause));
                    }
                }
            }
            finally
            {
                materializationPhase.Complete();
            }

            _lightweightExtractionFrontierActivations++;
            return true;
        }

        private int ComputeExtractionPriority(
            PrototypeExtractionCandidate candidate,
            int priorityUpperBound,
            bool useDepotTopologyBounds)
        {
            PrototypeResourceSiteState site = candidate.Site;
            Vector3 interactionPosition = candidate.InteractionPosition;
            bool hasRemoteDepot = GetRemoteDepot(site.ClusterId, requireBuilt: true) != null;
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
            return applyRemoteDepotPenalty
                ? priorityUpperBound - 140
                : priorityUpperBound;
        }

        private static PrototypeWorkOrder CreateExtractionOrder(
            PrototypeExtractionCandidate candidate,
            string resourceId,
            int adjustedPriority,
            PrototypeDirectiveAffinity directiveAffinity,
            string directiveCause)
        {
            PrototypeResourceSiteState site = candidate.Site;
            return new PrototypeWorkOrder
            {
                OrderId = $"extract.{site.NodeName}",
                Kind = PrototypeWorkOrderKind.Extract,
                Priority = adjustedPriority,
                ResourceId = resourceId,
                TargetNodeName = site.NodeName,
                ClusterId = site.ClusterId,
                Label = PrototypeSettlementLayout.GetResourceTargetLabel(resourceId),
                Reason = $"reserve target for {InventoryComponent.FormatItemName(resourceId)}",
                DirectiveAffinity = directiveAffinity,
                DirectiveCause = directiveCause,
                TargetPosition = candidate.InteractionPosition,
                Amount = 1
            };
        }

        private static bool HasActiveExtractionClaim(
            HashSet<string> activeClaimedOrderIds,
            string nodeName)
        {
            foreach (string claimedOrderId in activeClaimedOrderIds)
            {
                if (ExtractionOrderIdEqualsNodeName(claimedOrderId, nodeName))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ExtractionOrderIdEqualsNodeName(string orderId, string nodeName)
        {
            const string extractionOrderIdPrefix = "extract.";
            return orderId.Length == extractionOrderIdPrefix.Length + nodeName.Length &&
                orderId.StartsWith(extractionOrderIdPrefix, StringComparison.Ordinal) &&
                orderId.AsSpan(extractionOrderIdPrefix.Length).SequenceEqual(nodeName.AsSpan());
        }

        private static int CompareExtractionFrontierOrderKeys(
            string leftOrderKey,
            bool leftIsExtractionNodeName,
            string rightOrderKey,
            bool rightIsExtractionNodeName)
        {
            if (leftIsExtractionNodeName)
            {
                return rightIsExtractionNodeName
                    ? StringComparer.Ordinal.Compare(leftOrderKey, rightOrderKey)
                    : CompareExtractionNodeNameToOrderId(leftOrderKey, rightOrderKey);
            }
            if (!rightIsExtractionNodeName)
            {
                return StringComparer.Ordinal.Compare(leftOrderKey, rightOrderKey);
            }

            int comparison = CompareExtractionNodeNameToOrderId(rightOrderKey, leftOrderKey);
            return comparison < 0 ? 1 : comparison > 0 ? -1 : 0;
        }

        private static int CompareExtractionNodeNameToOrderId(string nodeName, string orderId)
        {
            const string extractionOrderIdPrefix = "extract.";
            int sharedPrefixLength = Math.Min(extractionOrderIdPrefix.Length, orderId.Length);
            int prefixComparison = extractionOrderIdPrefix
                .AsSpan(0, sharedPrefixLength)
                .SequenceCompareTo(orderId.AsSpan(0, sharedPrefixLength));
            if (prefixComparison != 0)
            {
                return prefixComparison;
            }
            if (orderId.Length < extractionOrderIdPrefix.Length)
            {
                return 1;
            }
            return nodeName.AsSpan().SequenceCompareTo(orderId.AsSpan(extractionOrderIdPrefix.Length));
        }

        private readonly record struct PrototypeExtractionFrontierEntry(
            int EffectivePriority,
            string OrderKey,
            bool OrderKeyIsExtractionNodeName,
            bool IsActiveClaim,
            PrototypeExtractionCandidate? Candidate,
            int AdjustedPriority);

        private readonly record struct PrototypeExtractionFrontierPriority(
            int EffectivePriority,
            string OrderKey,
            bool OrderKeyIsExtractionNodeName);

        private sealed class PrototypeExtractionFrontierPriorityComparer : IComparer<PrototypeExtractionFrontierPriority>
        {
            public static readonly PrototypeExtractionFrontierPriorityComparer Instance = new();

            public int Compare(PrototypeExtractionFrontierPriority left, PrototypeExtractionFrontierPriority right)
            {
                int priorityComparison = left.EffectivePriority.CompareTo(right.EffectivePriority);
                return priorityComparison != 0
                    ? priorityComparison
                    : CompareExtractionFrontierOrderKeys(
                        right.OrderKey,
                        right.OrderKeyIsExtractionNodeName,
                        left.OrderKey,
                        left.OrderKeyIsExtractionNodeName);
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

        private List<PrototypeWorkOrder> RemoveClaimedOrders(
            List<PrototypeWorkOrder> orders,
            IReadOnlySet<string> claimedOrderIds)
        {
            return orders
                .Where(order => !claimedOrderIds.Contains(order.OrderId) &&
                    CanReserveProcessOrder(order))
                .ToList();
        }

        private bool CanReserveProcessOrder(PrototypeWorkOrder order)
        {
            if (order.Kind != PrototypeWorkOrderKind.Process ||
                !PrototypeProcessingRecipeCatalog.TryResolve(
                    GetStructure(order.StructureId)?.StructureKindId ?? string.Empty,
                    order.ResourceId,
                    out PrototypeProcessingRecipe candidateRecipe))
            {
                return true;
            }

            PrototypeStructureState? structure = GetStructure(order.StructureId);
            if (structure == null)
            {
                return false;
            }

            List<PrototypeProcessingRecipe> activeRecipes = _citizens
                .Where(citizen => citizen.CurrentOrderKind == PrototypeWorkOrderKind.Process &&
                    citizen.Phase != PrototypeWorkerPhase.Idle &&
                    citizen.Phase != PrototypeWorkerPhase.Incapacitated &&
                    string.Equals(citizen.TargetStructureId, structure.StructureId, StringComparison.Ordinal))
                .Select(citizen => PrototypeProcessingRecipeCatalog.TryResolve(structure.StructureKindId, citizen.CarryItemId, out PrototypeProcessingRecipe recipe)
                    ? recipe
                    : default)
                .Where(recipe => recipe.OutputAmount > 0)
                .ToList();

            activeRecipes.Add(candidateRecipe);

            if (activeRecipes.Sum(recipe => recipe.OutputAmount) > structure.OutputStore.AvailableCapacity)
            {
                return false;
            }

            return activeRecipes
                .SelectMany(recipe => recipe.Inputs)
                .GroupBy(input => input.Key, StringComparer.Ordinal)
                .All(group => group.Sum(input => input.Value) <= structure.InputStore.GetCount(group.Key));
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
