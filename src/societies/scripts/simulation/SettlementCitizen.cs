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

        private void InitializeCitizens(IReadOnlyList<PrototypeRoleQuotaDefinition> roleQuotas)
        {
            List<PrototypeCitizenRole> seededRoles = BuildRolePlan(roleQuotas, _scenario.InitialCitizens);

            for (int index = 0; index < _scenario.InitialCitizens; index++)
            {
                Vector3 homePosition = ProjectToSurface(GetCitizenHomePosition(index, _scenario.InitialCitizens));
                PrototypeCitizenRole role = index < seededRoles.Count ? seededRoles[index] : PrototypeCitizenRole.Generalist;
                _citizens.Add(new PrototypeWorkerState
                {
                    WorkerId = $"citizen_{index + 1}",
                    DisplayName = $"Citizen {index + 1}",
                    Role = role,
                    Phase = PrototypeWorkerPhase.Idle,
                    TicksRemaining = 8,
                    PhaseDurationTicks = 8,
                    Position = homePosition,
                    HomePosition = homePosition,
                    TargetPosition = homePosition,
                    TargetLabel = "Settlement",
                    ActivityText = "Waiting for work",
                    HomeBedCapacity = 0,
                    Needs = new PrototypeNeedState
                    {
                        Nutrition = 72.0f + (index % 4) * 4.0f,
                        Fatigue = 12.0f + (index % 3) * 3.0f
                    }
                });
            }
        }
        private void AdvanceCitizenNeeds(
            PrototypeWorkerState citizen,
            float currentHour,
            PrototypeWeather weather,
            PrototypeSettlementTickResult result)
        {
            if (citizen.Phase == PrototypeWorkerPhase.Incapacitated)
            {
                citizen.Needs.Nutrition = Mathf.Max(0.0f, citizen.Needs.Nutrition - 0.06f);
                citizen.Needs.Fatigue = Mathf.Min(100.0f, citizen.Needs.Fatigue + 0.02f);
                return;
            }

            citizen.Needs.Nutrition = Mathf.Max(0.0f, citizen.Needs.Nutrition - GetNutritionDecay(citizen.Phase));
            citizen.Needs.Fatigue = Mathf.Clamp(citizen.Needs.Fatigue + GetFatigueDelta(citizen.Phase, currentHour, weather), 0.0f, 100.0f);

            if (citizen.Needs.IsNutritionCritical && _centralDepot.GetCount("meals") == 0 && _centralDepot.GetCount("berries") == 0)
            {
                citizen.Phase = PrototypeWorkerPhase.Incapacitated;
                citizen.ActivityText = "Starving";
                citizen.LastFailureReason = "food.shortage";
                AddRecentEvent(citizen, "Food critical");
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.NeedCritical,
                    $"{citizen.DisplayName} became nonproductive because no food was available"));
            }
        }
        private void AdvanceCitizen(
            PrototypeWorkerState citizen,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            float currentHour,
            PrototypeWeather weather,
            PrototypeSettlementTickResult result,
            List<PrototypeWorkOrder> availableOrders)
        {
            _citizensEvaluatedThisTick++;
            TrackCitizenPhaseTick(citizen);

            switch (citizen.Phase)
            {
                case PrototypeWorkerPhase.MovingToResource:
                case PrototypeWorkerPhase.MovingToCache:
                case PrototypeWorkerPhase.MovingToDepot:
                case PrototypeWorkerPhase.MovingToStructure:
                    if (!AdvanceTravelPhase(citizen))
                    {
                        return;
                    }

                    ResolveMovementArrival(citizen, result);
                    return;

                case PrototypeWorkerPhase.Harvesting:
                case PrototypeWorkerPhase.DepositingToCache:
                case PrototypeWorkerPhase.DepositingToDepot:
                case PrototypeWorkerPhase.DepositingToStructure:
                case PrototypeWorkerPhase.Processing:
                case PrototypeWorkerPhase.Building:
                case PrototypeWorkerPhase.Refueling:
                case PrototypeWorkerPhase.Eating:
                case PrototypeWorkerPhase.Sleeping:
                    if (!AdvanceStationaryPhase(citizen, citizen.TargetPosition))
                    {
                        return;
                    }

                    ResolveStationaryCompletion(citizen, result);
                    return;

                case PrototypeWorkerPhase.Incapacitated:
                    citizen.TargetPosition = citizen.HomePosition;
                    citizen.Position = citizen.Position.MoveToward(citizen.HomePosition, CitizenTravelUnitsPerTick * 0.5f);
                    return;

                case PrototypeWorkerPhase.Idle:
                default:
                    citizen.Position = citizen.Position.MoveToward(citizen.HomePosition, CitizenTravelUnitsPerTick * 0.25f);
                    break;
            }

            if (TryAssignNeedDrivenOrder(citizen, currentHour, weather, result))
            {
                return;
            }

            PrototypeWorkOrder? order = availableOrders
                .OrderByDescending(candidate => ScoreOrder(citizen, candidate))
                .ThenBy(candidate => candidate.OrderId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (order == null)
            {
                BeginIdle(citizen, "Waiting for work");
                return;
            }

            availableOrders.Remove(order);
            BeginOrder(citizen, order, result);
        }
        private void ResolveMovementArrival(PrototypeWorkerState citizen, PrototypeSettlementTickResult result)
        {
            if (citizen.Navigation.CurrentRouteLengthMeters > 0.0f)
            {
                _completedRouteCount++;
                _completedRouteDistanceMeters += citizen.Navigation.CurrentRouteLengthMeters;
                _completedRouteTravelTicks += citizen.Navigation.CurrentRouteTravelTicks;
            }

            switch (citizen.CurrentOrderKind)
            {
                case PrototypeWorkOrderKind.Extract:
                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Harvesting,
                        HarvestTicks,
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        $"Harvesting {citizen.TargetLabel}");
                    return;

                case PrototypeWorkOrderKind.HaulToDepot:
                case PrototypeWorkOrderKind.HaulFromRemoteDepot:
                    if (citizen.CarryAmount == 0)
                    {
                        if (!TryPickupFromStore(citizen, citizen.SourceStoreId))
                        {
                            FailCitizenOrder(citizen, "haul.source.empty", result, $"{citizen.DisplayName} could not pick up goods for depot hauling");
                            return;
                        }

                        BeginTravel(
                            citizen,
                            PrototypeWorkerPhase.MovingToDepot,
                            _centralDepot.Position,
                            "Central Depot",
                            $"Delivering {InventoryComponent.FormatItemName(citizen.CarryItemId)}");
                        return;
                    }

                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.DepositingToDepot,
                        DepositTicks,
                        _centralDepot.Position,
                        "Central Depot",
                        $"Depositing {InventoryComponent.FormatItemName(citizen.CarryItemId)}");
                    return;

                case PrototypeWorkOrderKind.HaulToRemoteDepot:
                    if (citizen.CarryAmount == 0)
                    {
                        if (!TryPickupFromStore(citizen, citizen.SourceStoreId))
                        {
                            FailCitizenOrder(citizen, "haul.source.empty", result, $"{citizen.DisplayName} could not pick up goods for remote hauling");
                            return;
                        }

                        Vector3 destinationPosition = GetStorePosition(citizen.DestinationStoreId);
                        BeginTravel(
                            citizen,
                            PrototypeWorkerPhase.MovingToStructure,
                            destinationPosition,
                            citizen.TargetLabel,
                            $"Consolidating {InventoryComponent.FormatItemName(citizen.CarryItemId)}");
                        return;
                    }

                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.DepositingToStructure,
                        DepositTicks,
                        GetStorePosition(citizen.DestinationStoreId),
                        citizen.TargetLabel,
                        $"Stocking {citizen.TargetLabel}");
                    return;

                case PrototypeWorkOrderKind.HaulToStructure:
                    if (citizen.CarryAmount == 0)
                    {
                        if (!TryPickupFromStore(citizen, citizen.SourceStoreId))
                        {
                            FailCitizenOrder(citizen, "haul.source.empty", result, $"{citizen.DisplayName} could not pick up goods for structure hauling");
                            return;
                        }

                        PrototypeStructureState? destinationStructure = GetStructure(citizen.TargetStructureId);
                        if (destinationStructure == null)
                        {
                            FailCitizenOrder(citizen, "haul.structure.missing", result, $"{citizen.DisplayName} could not find the destination structure");
                            return;
                        }

                        BeginTravel(
                            citizen,
                            PrototypeWorkerPhase.MovingToStructure,
                            destinationStructure.Position,
                            destinationStructure.DisplayName,
                            $"Carrying {InventoryComponent.FormatItemName(citizen.CarryItemId)}");
                        return;
                    }

                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.DepositingToStructure,
                        DepositTicks,
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        $"Supplying {citizen.TargetLabel}");
                    return;

                case PrototypeWorkOrderKind.Process:
                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Processing,
                        GetProcessingTicks(citizen.TargetStructureId, citizen.CarryItemId),
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        $"Working at {citizen.TargetLabel}");
                    return;

                case PrototypeWorkOrderKind.Build:
                case PrototypeWorkOrderKind.BuildPath:
                case PrototypeWorkOrderKind.EstablishRemoteDepot:
                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Building,
                        GetBuildTicks(citizen.TargetStructureId),
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        $"Building {citizen.TargetLabel}");
                    return;

                case PrototypeWorkOrderKind.RefuelHearth:
                    if (citizen.CarryAmount == 0)
                    {
                        if (!TryPickupFromStore(citizen, citizen.SourceStoreId))
                        {
                            FailCitizenOrder(citizen, "fuel.source.empty", result, $"{citizen.DisplayName} could not collect firewood for the hearth");
                            return;
                        }

                        PrototypeStructureState? hearth = GetStructure(citizen.TargetStructureId);
                        if (hearth == null)
                        {
                            FailCitizenOrder(citizen, "fuel.hearth.missing", result, $"{citizen.DisplayName} could not find the hearth");
                            return;
                        }

                        BeginTravel(
                            citizen,
                            PrototypeWorkerPhase.MovingToStructure,
                            hearth.Position,
                            hearth.DisplayName,
                            "Refueling the hearth");
                        return;
                    }

                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Refueling,
                        DepositTicks,
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        "Refueling the hearth");
                    return;

                case PrototypeWorkOrderKind.Eat:
                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Eating,
                        EatTicks,
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        "Eating");
                    return;

                case PrototypeWorkOrderKind.Sleep:
                    BeginStationaryPhase(
                        citizen,
                        PrototypeWorkerPhase.Sleeping,
                        SleepTicks,
                        citizen.TargetPosition,
                        citizen.TargetLabel,
                        "Sleeping");
                    return;
            }
        }
        private void ResolveStationaryCompletion(PrototypeWorkerState citizen, PrototypeSettlementTickResult result)
        {
            switch (citizen.CurrentOrderKind)
            {
                case PrototypeWorkOrderKind.Extract when citizen.Phase == PrototypeWorkerPhase.Harvesting:
                    citizen.CarryItemId = InferHarvestItemFromNode(citizen.TargetResourceNodeName);
                    citizen.CarryAmount = 1;
                    result.HarvestRequests.Add(new PrototypeHarvestRequest(
                        citizen.WorkerId,
                        citizen.DisplayName,
                        citizen.TargetResourceNodeName,
                        citizen.CarryItemId,
                        1,
                        ExtractClusterId(citizen.TargetResourceNodeName)));
                    AddRecentEvent(citizen, $"Harvested {citizen.CarryItemId}");

                    PrototypeResourceStoreState? cache = ResolveCacheForCitizen(citizen);
                    if (cache == null)
                    {
                        FailCitizenOrder(citizen, "cache.missing", result, $"{citizen.DisplayName} could not find a site cache");
                        return;
                    }

                    BeginTravel(
                        citizen,
                        PrototypeWorkerPhase.MovingToCache,
                        cache.Position,
                        cache.DisplayName,
                        $"Carrying {InventoryComponent.FormatItemName(citizen.CarryItemId)}");
                    citizen.Phase = PrototypeWorkerPhase.MovingToCache;
                    return;

                case PrototypeWorkOrderKind.Extract:
                    PrototypeResourceStoreState? destinationCache = ResolveCacheForCitizen(citizen);
                    if (destinationCache == null || !destinationCache.Add(citizen.CarryItemId, citizen.CarryAmount))
                    {
                        FailCitizenOrder(citizen, "cache.full", result, $"{citizen.DisplayName} could not deposit into the site cache");
                        return;
                    }

                    IncrementCount(_producedResources, citizen.CarryItemId, citizen.CarryAmount);
                    result.Events.Add(new PrototypeSettlementEvent(
                        PrototypeEventTypes.SettlementCacheDeposit,
                        $"{citizen.DisplayName} cached {InventoryComponent.FormatItemName(citizen.CarryItemId)} x{citizen.CarryAmount}"));
                    ClearCitizenCarry(citizen);
                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.HaulToDepot:
                case PrototypeWorkOrderKind.HaulFromRemoteDepot:
                    if (_centralDepot.Add(citizen.CarryItemId, citizen.CarryAmount))
                    {
                        if (!string.IsNullOrWhiteSpace(citizen.SourceStoreId) && citizen.SourceStoreId.Contains("remote_stockpile", StringComparison.Ordinal))
                        {
                            IncrementCount(_depotThroughputByDepot, citizen.SourceStoreId, citizen.CarryAmount);
                            PrototypeRemoteDepotState? sourceDepot = _remoteDepots.FirstOrDefault(candidate => string.Equals(candidate.StructureId, citizen.TargetStructureId, StringComparison.Ordinal));
                            if (sourceDepot != null)
                            {
                                sourceDepot.ThroughputCount += citizen.CarryAmount;
                            }
                        }

                        result.Events.Add(new PrototypeSettlementEvent(
                            PrototypeEventTypes.SettlementHaulCompleted,
                            $"{citizen.DisplayName} delivered {InventoryComponent.FormatItemName(citizen.CarryItemId)} x{citizen.CarryAmount} to the depot"));
                        ClearCitizenCarry(citizen);
                        BeginIdle(citizen, "Ready for work");
                        return;
                    }

                    FailCitizenOrder(citizen, "depot.full", result, $"{citizen.DisplayName} could not unload to the depot");
                    return;

                case PrototypeWorkOrderKind.HaulToRemoteDepot:
                    PrototypeResourceStoreState? remoteDestination = GetStore(citizen.DestinationStoreId);
                    if (remoteDestination == null || !remoteDestination.Add(citizen.CarryItemId, citizen.CarryAmount))
                    {
                        FailCitizenOrder(citizen, "remote.depot.full", result, $"{citizen.DisplayName} could not supply the remote depot");
                        return;
                    }

                    IncrementCount(_depotThroughputByDepot, citizen.DestinationStoreId, citizen.CarryAmount);
                    PrototypeRemoteDepotState? destinationDepot = _remoteDepots.FirstOrDefault(candidate => string.Equals(candidate.StructureId, citizen.TargetStructureId, StringComparison.Ordinal));
                    if (destinationDepot != null)
                    {
                        destinationDepot.ThroughputCount += citizen.CarryAmount;
                    }
                    result.Events.Add(new PrototypeSettlementEvent(
                        PrototypeEventTypes.SettlementHaulCompleted,
                        $"{citizen.DisplayName} stocked {citizen.TargetLabel} with {InventoryComponent.FormatItemName(citizen.CarryItemId)} x{citizen.CarryAmount}"));
                    ClearCitizenCarry(citizen);
                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.HaulToStructure:
                    PrototypeStructureState? structure = GetStructure(citizen.TargetStructureId);
                    if (structure == null || !structure.InputStore.Add(citizen.CarryItemId, citizen.CarryAmount))
                    {
                        FailCitizenOrder(citizen, "structure.input.full", result, $"{citizen.DisplayName} could not supply {citizen.TargetLabel}");
                        return;
                    }

                    result.Events.Add(new PrototypeSettlementEvent(
                        PrototypeEventTypes.SettlementStructureSupplied,
                        $"{citizen.DisplayName} supplied {citizen.TargetLabel} with {InventoryComponent.FormatItemName(citizen.CarryItemId)} x{citizen.CarryAmount}"));
                    ClearCitizenCarry(citizen);
                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.Process:
                    if (!CompleteProcessing(citizen, result))
                    {
                        return;
                    }

                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.Build:
                case PrototypeWorkOrderKind.BuildPath:
                case PrototypeWorkOrderKind.EstablishRemoteDepot:
                    if (!CompleteBuild(citizen, result))
                    {
                        return;
                    }

                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.RefuelHearth:
                    PrototypeStructureState? hearth = GetStructure(citizen.TargetStructureId);
                    if (hearth == null)
                    {
                        FailCitizenOrder(citizen, "fuel.hearth.missing", result, $"{citizen.DisplayName} could not refuel the hearth");
                        return;
                    }

                    hearth.HearthFuel += citizen.CarryAmount;
                    result.Events.Add(new PrototypeSettlementEvent(
                        PrototypeEventTypes.SettlementHearthRefueled,
                        $"{citizen.DisplayName} refueled the hearth with firewood x{citizen.CarryAmount}"));
                    ClearCitizenCarry(citizen);
                    BeginIdle(citizen, "Ready for work");
                    return;

                case PrototypeWorkOrderKind.Eat:
                    CompleteEating(citizen, result);
                    BeginIdle(citizen, "Fed");
                    return;

                case PrototypeWorkOrderKind.Sleep:
                    CompleteSleeping(citizen);
                    BeginIdle(citizen, "Rested");
                    return;
            }
        }
        private bool TryAssignNeedDrivenOrder(
            PrototypeWorkerState citizen,
            float currentHour,
            PrototypeWeather weather,
            PrototypeSettlementTickResult result)
        {
            if (citizen.Needs.NeedsFood)
            {
                string? foodId = _centralDepot.GetCount("meals") > 0
                    ? "meals"
                    : _centralDepot.GetCount("berries") > 0 ? "berries" : null;
                if (foodId != null)
                {
                    BeginOrder(citizen, new PrototypeWorkOrder
                    {
                        OrderId = $"eat.{citizen.WorkerId}.{_totalTicks}",
                        Kind = PrototypeWorkOrderKind.Eat,
                        Priority = 1400,
                        ResourceId = foodId,
                        Label = "Central Hearth",
                        Reason = citizen.Needs.IsNutritionCritical ? "critical nutrition" : "food need",
                        TargetPosition = GetStructure("central_hearth_1")?.Position ?? _world.SettlementSpawn.AnchorPosition,
                        Amount = 1
                    }, result);
                    return true;
                }
            }

            if (citizen.Needs.NeedsSleep || (IsNight(currentHour) && citizen.Needs.Fatigue >= 48.0f))
            {
                Vector3 sleepTarget = GetSleepPosition(citizen);
                string label = citizen.HomeBedCapacity > 0 ? "Hut" : "Hearthside Bedroll";
                BeginOrder(citizen, new PrototypeWorkOrder
                {
                    OrderId = $"sleep.{citizen.WorkerId}.{_totalTicks}",
                    Kind = PrototypeWorkOrderKind.Sleep,
                    Priority = 1300,
                    Label = label,
                    Reason = citizen.Needs.IsExhausted ? "critical fatigue" : "rest cycle",
                    TargetPosition = sleepTarget,
                    Amount = 1
                }, result);
                return true;
            }

            return false;
        }
        private void BeginOrder(PrototypeWorkerState citizen, PrototypeWorkOrder order, PrototypeSettlementTickResult result)
        {
            citizen.CurrentOrderId = order.OrderId;
            citizen.CurrentOrderKind = order.Kind;
            citizen.CurrentOrderReason = order.Reason;
            citizen.TargetStructureId = order.StructureId;
            citizen.TargetResourceNodeName = order.TargetNodeName;
            citizen.SourceStoreId = order.SourceStoreId;
            citizen.DestinationStoreId = order.DestinationStoreId;
            citizen.TargetLabel = order.Label;
            citizen.TargetPosition = order.TargetPosition;
            citizen.CarryItemId = citizen.CarryAmount > 0 ? citizen.CarryItemId : order.ResourceId;

            switch (order.Kind)
            {
                case PrototypeWorkOrderKind.Extract:
                    BeginTravel(citizen, PrototypeWorkerPhase.MovingToResource, order.TargetPosition, order.Label, $"Heading to {order.Label}");
                    break;
                case PrototypeWorkOrderKind.HaulToDepot:
                case PrototypeWorkOrderKind.HaulFromRemoteDepot:
                case PrototypeWorkOrderKind.HaulToRemoteDepot:
                case PrototypeWorkOrderKind.HaulToStructure:
                    BeginTravel(citizen, GetSourceTravelPhase(order.SourceStoreId), GetStorePosition(order.SourceStoreId), GetStoreLabel(order.SourceStoreId), $"Collecting {InventoryComponent.FormatItemName(order.ResourceId)}");
                    break;
                case PrototypeWorkOrderKind.Process:
                case PrototypeWorkOrderKind.Build:
                case PrototypeWorkOrderKind.BuildPath:
                case PrototypeWorkOrderKind.EstablishRemoteDepot:
                case PrototypeWorkOrderKind.Eat:
                case PrototypeWorkOrderKind.Sleep:
                    BeginTravel(citizen, PrototypeWorkerPhase.MovingToStructure, order.TargetPosition, order.Label, $"Heading to {order.Label}");
                    break;
                case PrototypeWorkOrderKind.RefuelHearth:
                    BeginTravel(citizen, PrototypeWorkerPhase.MovingToDepot, GetStorePosition(order.SourceStoreId), GetStoreLabel(order.SourceStoreId), "Collecting firewood");
                    break;
                case PrototypeWorkOrderKind.Repath:
                    BeginTravel(citizen, PrototypeWorkerPhase.MovingToStructure, order.TargetPosition, order.Label, "Repathing");
                    break;
            }

            result.Events.Add(new PrototypeSettlementEvent(
                PrototypeEventTypes.SettlementWorkAssigned,
                $"{citizen.DisplayName} accepted {order.Kind} for {order.Reason}"));
        }
        private bool CompleteProcessing(PrototypeWorkerState citizen, PrototypeSettlementTickResult result)
        {
            PrototypeStructureState? structure = GetStructure(citizen.TargetStructureId);
            if (structure == null)
            {
                FailCitizenOrder(citizen, "process.structure.missing", result, $"{citizen.DisplayName} could not find {citizen.TargetLabel}");
                return false;
            }

            string outputId = citizen.CarryItemId;
            switch (structure.StructureKindId)
            {
                case "wood_yard":
                    if (!structure.InputStore.Remove("logs", 1))
                    {
                        FailBlockedStructure(structure, citizen, result, "process.logs.missing", "Wood yard lacks logs");
                        return false;
                    }

                    if (outputId == "firewood")
                    {
                        structure.OutputStore.Add("firewood", 2);
                        IncrementCount(_producedResources, "firewood", 2);
                    }
                    else
                    {
                        structure.OutputStore.Add("timber", 1);
                        IncrementCount(_producedResources, "timber", 1);
                    }

                    IncrementCount(_consumedResources, "logs", 1);
                    break;

                case "cookfire":
                    if (!structure.InputStore.Remove("berries", 2) || !structure.InputStore.Remove("firewood", 1))
                    {
                        FailBlockedStructure(structure, citizen, result, "process.meals.blocked", "Cookfire lacks berries or firewood");
                        return false;
                    }

                    structure.OutputStore.Add("meals", 2);
                    IncrementCount(_producedResources, "meals", 2);
                    IncrementCount(_consumedResources, "berries", 2);
                    IncrementCount(_consumedResources, "firewood", 1);
                    break;

                case "drying_rack":
                    if (!structure.InputStore.Remove("reeds", 2))
                    {
                        FailBlockedStructure(structure, citizen, result, "process.reeds.missing", "Drying rack lacks reeds");
                        return false;
                    }

                    structure.OutputStore.Add("thatch", 1);
                    IncrementCount(_producedResources, "thatch", 1);
                    IncrementCount(_consumedResources, "reeds", 2);
                    break;

                case "kiln":
                    if (!structure.InputStore.Remove("stone", 1) ||
                        !structure.InputStore.Remove("clay", 1) ||
                        !structure.InputStore.Remove("firewood", 1))
                    {
                        FailBlockedStructure(structure, citizen, result, "process.brick.blocked", "Kiln lacks stone, clay, or firewood");
                        return false;
                    }

                    structure.OutputStore.Add("brick", 1);
                    IncrementCount(_producedResources, "brick", 1);
                    IncrementCount(_consumedResources, "stone", 1);
                    IncrementCount(_consumedResources, "clay", 1);
                    IncrementCount(_consumedResources, "firewood", 1);
                    break;
            }

            structure.ActiveTicks++;
            result.Events.Add(new PrototypeSettlementEvent(
                PrototypeEventTypes.SettlementProcessCompleted,
                $"{citizen.DisplayName} completed work at {structure.DisplayName}"));
            return true;
        }
        private void CompleteEating(PrototypeWorkerState citizen, PrototypeSettlementTickResult result)
        {
            if (_centralDepot.Remove("meals", 1))
            {
                citizen.Needs.Nutrition = Mathf.Min(100.0f, citizen.Needs.Nutrition + 42.0f);
                IncrementCount(_consumedResources, "meals", 1);
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.SettlementNeedRecovered,
                    $"{citizen.DisplayName} ate a meal"));
            }
            else if (_centralDepot.Remove("berries", 1))
            {
                citizen.Needs.Nutrition = Mathf.Min(100.0f, citizen.Needs.Nutrition + 22.0f);
                IncrementCount(_consumedResources, "berries", 1);
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.SettlementNeedRecovered,
                    $"{citizen.DisplayName} survived on raw berries"));
            }

            if (citizen.Phase == PrototypeWorkerPhase.Incapacitated && citizen.Needs.Nutrition > 18.0f)
            {
                citizen.Phase = PrototypeWorkerPhase.Idle;
            }
        }
        private void CompleteSleeping(PrototypeWorkerState citizen)
        {
            float restBonus = citizen.HomeBedCapacity > 0 ? 44.0f : 24.0f;
            citizen.Needs.Fatigue = Mathf.Max(0.0f, citizen.Needs.Fatigue - restBonus);
            AddRecentEvent(citizen, citizen.HomeBedCapacity > 0 ? "Slept in hut" : "Slept by hearth");
        }
        private void AssignBeds()
        {
            int remainingBeds = _structures.Where(structure => structure.IsBuilt && structure.StructureKindId == "hut").Sum(structure => structure.BedCapacity);

            foreach (PrototypeWorkerState citizen in _citizens.OrderBy(citizen => citizen.WorkerId, StringComparer.Ordinal))
            {
                citizen.HomeBedCapacity = remainingBeds > 0 ? 1 : 0;
                if (remainingBeds > 0)
                {
                    remainingBeds--;
                }
            }
        }
        private void TrackCitizenPhaseTick(PrototypeWorkerState citizen)
        {
            if (citizen.Phase is PrototypeWorkerPhase.MovingToResource or
                PrototypeWorkerPhase.MovingToCache or
                PrototypeWorkerPhase.MovingToDepot or
                PrototypeWorkerPhase.MovingToStructure)
            {
                citizen.TravelTicksAccumulated++;
                _travelTicksAccumulated++;
                return;
            }

            if (citizen.Phase is PrototypeWorkerPhase.Harvesting or
                PrototypeWorkerPhase.DepositingToCache or
                PrototypeWorkerPhase.DepositingToDepot or
                PrototypeWorkerPhase.DepositingToStructure or
                PrototypeWorkerPhase.Processing or
                PrototypeWorkerPhase.Building or
                PrototypeWorkerPhase.Refueling)
            {
                citizen.WorkTicksAccumulated++;
                _workTicksAccumulated++;
            }
        }
        private PrototypeWorkerSnapshot CaptureCitizen(PrototypeWorkerState citizen)
        {
            return new PrototypeWorkerSnapshot
            {
                WorkerId = citizen.WorkerId,
                DisplayName = citizen.DisplayName,
                RoleId = citizen.Role.ToString(),
                Phase = citizen.Phase.ToString(),
                TargetResourceNodeName = citizen.TargetResourceNodeName,
                TargetStructureId = citizen.TargetStructureId,
                SourceStoreId = citizen.SourceStoreId,
                DestinationStoreId = citizen.DestinationStoreId,
                CarryItemId = citizen.CarryItemId,
                CarryAmount = citizen.CarryAmount,
                TicksRemaining = citizen.TicksRemaining,
                PhaseDurationTicks = citizen.PhaseDurationTicks,
                Position = PrototypeSerializableVector3.FromVector3(citizen.Position),
                HomePosition = PrototypeSerializableVector3.FromVector3(citizen.HomePosition),
                TargetPosition = PrototypeSerializableVector3.FromVector3(citizen.TargetPosition),
                TargetLabel = citizen.TargetLabel,
                ActivityText = citizen.ActivityText,
                Nutrition = citizen.Needs.Nutrition,
                Fatigue = citizen.Needs.Fatigue,
                LastFailureReason = citizen.LastFailureReason,
                CurrentOrderId = citizen.CurrentOrderId,
                CurrentOrderKind = citizen.CurrentOrderKind?.ToString() ?? string.Empty,
                CurrentOrderReason = citizen.CurrentOrderReason,
                HomeBedCapacity = citizen.HomeBedCapacity,
                RecentEvents = citizen.RecentEvents.ToList(),
                TravelTicksAccumulated = citizen.TravelTicksAccumulated,
                WorkTicksAccumulated = citizen.WorkTicksAccumulated,
                CurrentRouteLengthMeters = citizen.Navigation.CurrentRouteLengthMeters,
                CurrentRouteCost = citizen.Navigation.CurrentRouteCost,
                CurrentRouteTravelTicks = citizen.Navigation.CurrentRouteTravelTicks,
                CurrentWaypointIndex = citizen.Navigation.CurrentWaypointIndex,
                CachedRouteVersion = citizen.Navigation.CachedRouteVersion,
                RouteSourceGridX = citizen.Navigation.SourceGridX,
                RouteSourceGridY = citizen.Navigation.SourceGridY,
                RouteDestinationGridX = citizen.Navigation.DestinationGridX,
                RouteDestinationGridY = citizen.Navigation.DestinationGridY,
                RouteWaypoints = citizen.Navigation.RouteWaypoints.ToList()
            };
        }
        private static PrototypeResourceStoreState RestoreStoreSnapshot(PrototypeResourceStoreSnapshot snapshot)
        {
            PrototypeResourceStoreState store = new()
            {
                StoreId = snapshot.StoreId,
                DisplayName = snapshot.DisplayName,
                Capacity = snapshot.Capacity,
                Position = snapshot.Position.ToVector3(),
                LinkedClusterId = snapshot.LinkedClusterId
            };

            foreach ((string itemId, int amount) in snapshot.Items)
            {
                store.Items[itemId] = amount;
            }

            return store;
        }
        private static void RestoreStore(PrototypeResourceStoreState store, PrototypeResourceStoreSnapshot snapshot)
        {
            store.DisplayName = snapshot.DisplayName;
            store.Capacity = snapshot.Capacity;
            store.Position = snapshot.Position.ToVector3();
            store.LinkedClusterId = snapshot.LinkedClusterId;
            ReplaceCounts(store.Items, snapshot.Items);
        }
        private static PrototypeWorkerState RestoreCitizen(PrototypeWorkerSnapshot snapshot)
        {
            PrototypeCitizenRole role = Enum.TryParse(snapshot.RoleId, true, out PrototypeCitizenRole parsedRole)
                ? parsedRole
                : PrototypeCitizenRole.Generalist;
            PrototypeWorkerPhase phase = Enum.TryParse(snapshot.Phase, true, out PrototypeWorkerPhase parsedPhase)
                ? parsedPhase
                : PrototypeWorkerPhase.Idle;
            PrototypeWorkOrderKind? orderKind = Enum.TryParse(snapshot.CurrentOrderKind, true, out PrototypeWorkOrderKind parsedOrder)
                ? parsedOrder
                : null;

            return new PrototypeWorkerState
            {
                WorkerId = snapshot.WorkerId,
                DisplayName = snapshot.DisplayName,
                Role = role,
                Phase = phase,
                TargetResourceNodeName = snapshot.TargetResourceNodeName,
                TargetStructureId = snapshot.TargetStructureId,
                SourceStoreId = snapshot.SourceStoreId,
                DestinationStoreId = snapshot.DestinationStoreId,
                CarryItemId = snapshot.CarryItemId,
                CarryAmount = snapshot.CarryAmount,
                TicksRemaining = snapshot.TicksRemaining,
                PhaseDurationTicks = snapshot.PhaseDurationTicks,
                Position = snapshot.Position.ToVector3(),
                HomePosition = snapshot.HomePosition.ToVector3(),
                TargetPosition = snapshot.TargetPosition.ToVector3(),
                TargetLabel = snapshot.TargetLabel,
                ActivityText = snapshot.ActivityText,
                Needs = new PrototypeNeedState
                {
                    Nutrition = snapshot.Nutrition,
                    Fatigue = snapshot.Fatigue
                },
                LastFailureReason = snapshot.LastFailureReason,
                CurrentOrderId = snapshot.CurrentOrderId,
                CurrentOrderKind = orderKind,
                CurrentOrderReason = snapshot.CurrentOrderReason,
                HomeBedCapacity = snapshot.HomeBedCapacity,
                RecentEvents = snapshot.RecentEvents.ToList(),
                TravelTicksAccumulated = snapshot.TravelTicksAccumulated,
                WorkTicksAccumulated = snapshot.WorkTicksAccumulated,
                Navigation = new PrototypeCitizenNavigationState
                {
                    CurrentWaypointIndex = snapshot.CurrentWaypointIndex,
                    CurrentRouteLengthMeters = snapshot.CurrentRouteLengthMeters,
                    CurrentRouteCost = snapshot.CurrentRouteCost,
                    CurrentRouteTravelTicks = snapshot.CurrentRouteTravelTicks,
                    CachedRouteVersion = snapshot.CachedRouteVersion,
                    SourceGridX = snapshot.RouteSourceGridX,
                    SourceGridY = snapshot.RouteSourceGridY,
                    DestinationGridX = snapshot.RouteDestinationGridX,
                    DestinationGridY = snapshot.RouteDestinationGridY,
                    RouteWaypoints = snapshot.RouteWaypoints.ToList()
                }
            };
        }
        private void BeginIdle(PrototypeWorkerState citizen, string activityText)
        {
            citizen.CurrentOrderId = string.Empty;
            citizen.CurrentOrderKind = null;
            citizen.CurrentOrderReason = string.Empty;
            citizen.TargetResourceNodeName = string.Empty;
            citizen.TargetStructureId = string.Empty;
            citizen.SourceStoreId = string.Empty;
            citizen.DestinationStoreId = string.Empty;
            citizen.Navigation = new PrototypeCitizenNavigationState();
            BeginStationaryPhase(citizen, PrototypeWorkerPhase.Idle, 6, citizen.HomePosition, "Settlement", activityText);
        }
        private void BeginTravel(PrototypeWorkerState citizen, PrototypeWorkerPhase phase, Vector3 targetPosition, string targetLabel, string activityText)
        {
            PrototypePathPlan plan = FindPathPlan(citizen.Position, targetPosition);
            citizen.Phase = phase;
            citizen.TargetPosition = targetPosition;
            citizen.TargetLabel = targetLabel;
            citizen.ActivityText = activityText;
            TerrainCell sourceCell = _world.WorldMap.GetNearestCell(citizen.Position);
            TerrainCell destinationCell = _world.WorldMap.GetNearestCell(targetPosition);
            citizen.Navigation = new PrototypeCitizenNavigationState
            {
                CurrentWaypointIndex = plan.Waypoints.Count > 1 ? 1 : 0,
                CurrentRouteLengthMeters = plan.TotalDistanceMeters,
                CurrentRouteCost = plan.TotalCost,
                CurrentRouteTravelTicks = plan.EstimatedTravelTicks,
                CachedRouteVersion = _navigationRulesVersion,
                SourceGridX = sourceCell.GridX,
                SourceGridY = sourceCell.GridY,
                DestinationGridX = destinationCell.GridX,
                DestinationGridY = destinationCell.GridY,
                RouteWaypoints = plan.Waypoints
                    .Select(PrototypeSerializableVector3.FromVector3)
                    .ToList()
            };
            citizen.PhaseDurationTicks = CalculateTravelTicks(plan);
            citizen.TicksRemaining = citizen.PhaseDurationTicks;
            RegisterPathUsage(plan);
        }
        private void BeginStationaryPhase(PrototypeWorkerState citizen, PrototypeWorkerPhase phase, int durationTicks, Vector3 position, string targetLabel, string activityText)
        {
            citizen.Phase = phase;
            citizen.Position = position;
            citizen.TargetPosition = position;
            citizen.TargetLabel = targetLabel;
            citizen.ActivityText = activityText;
            citizen.PhaseDurationTicks = durationTicks;
            citizen.TicksRemaining = durationTicks;
        }
        private static bool AdvanceStationaryPhase(PrototypeWorkerState citizen, Vector3 position)
        {
            citizen.Position = position;
            if (citizen.TicksRemaining > 0)
            {
                citizen.TicksRemaining--;
            }

            return citizen.TicksRemaining <= 0;
        }
        private static bool AdvanceTravelPhase(PrototypeWorkerState citizen)
        {
            List<Vector3> route = citizen.Navigation.RouteWaypoints
                .Select(waypoint => waypoint.ToVector3())
                .ToList();
            Vector3 waypointTarget = citizen.TargetPosition;
            if (route.Count > 0 && citizen.Navigation.CurrentWaypointIndex < route.Count)
            {
                waypointTarget = route[citizen.Navigation.CurrentWaypointIndex];
            }

            float routeCostMultiplier = citizen.Navigation.CurrentRouteLengthMeters <= 0.01f
                ? 1.0f
                : Mathf.Clamp(citizen.Navigation.CurrentRouteCost / citizen.Navigation.CurrentRouteLengthMeters, 0.45f, 2.4f);
            float step = CitizenTravelUnitsPerTick / routeCostMultiplier;
            Vector3 nextHorizontalPosition = new Vector3(citizen.Position.X, 0.0f, citizen.Position.Z)
                .MoveToward(new Vector3(waypointTarget.X, 0.0f, waypointTarget.Z), step);
            citizen.Position = new Vector3(nextHorizontalPosition.X, Mathf.Lerp(citizen.Position.Y, waypointTarget.Y, 0.35f), nextHorizontalPosition.Z);
            if (citizen.TicksRemaining > 0)
            {
                citizen.TicksRemaining--;
            }

            if (GetHorizontalDistance(citizen.Position, waypointTarget) <= 0.15f && route.Count > 0 && citizen.Navigation.CurrentWaypointIndex < route.Count - 1)
            {
                citizen.Navigation.CurrentWaypointIndex++;
            }

            if (citizen.TicksRemaining <= 0 || GetHorizontalDistance(citizen.Position, citizen.TargetPosition) <= 0.15f)
            {
                citizen.Position = citizen.TargetPosition;
                citizen.TicksRemaining = 0;
                return true;
            }

            return false;
        }
        private static int CalculateTravelTicks(PrototypePathPlan plan)
        {
            return Math.Max(MinimumTravelTicks, plan.EstimatedTravelTicks);
        }
        private static float GetHorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.X, a.Z).DistanceTo(new Vector2(b.X, b.Z));
        }
        private static float GetNutritionDecay(PrototypeWorkerPhase phase)
        {
            return phase switch
            {
                PrototypeWorkerPhase.MovingToResource or PrototypeWorkerPhase.MovingToCache or PrototypeWorkerPhase.MovingToDepot or PrototypeWorkerPhase.MovingToStructure => 0.10f,
                PrototypeWorkerPhase.Harvesting or PrototypeWorkerPhase.Building or PrototypeWorkerPhase.Processing => 0.12f,
                PrototypeWorkerPhase.Sleeping => 0.04f,
                _ => 0.08f
            };
        }
        private float GetFatigueDelta(PrototypeWorkerPhase phase, float currentHour, PrototypeWeather weather)
        {
            if (phase == PrototypeWorkerPhase.Sleeping)
            {
                float recovery = -1.55f;
                if ((weather == PrototypeWeather.Rain || IsNight(currentHour)) && HearthFuel <= 0)
                {
                    recovery = -0.85f;
                }

                return recovery;
            }

            if (phase == PrototypeWorkerPhase.Idle || phase == PrototypeWorkerPhase.Eating)
            {
                return 0.04f;
            }

            if (phase == PrototypeWorkerPhase.Incapacitated)
            {
                return 0.02f;
            }

            return 0.12f;
        }
        private static bool IsNight(float currentHour) => currentHour >= 20.0f || currentHour < 6.0f;
        private float ScoreOrder(PrototypeWorkerState citizen, PrototypeWorkOrder order)
        {
            float distancePenalty = ComputeRouteDistance(citizen.Position, order.TargetPosition) * 0.75f;
            float roleBonus = GetRoleBonus(citizen.Role, order);
            return order.Priority + roleBonus - distancePenalty;
        }
        private static float GetRoleBonus(PrototypeCitizenRole role, PrototypeWorkOrder order)
        {
            return role switch
            {
                PrototypeCitizenRole.Logger when order.ResourceId is "logs" or "firewood" or "timber" => 18.0f,
                PrototypeCitizenRole.Mason when order.ResourceId is "stone" or "clay" or "brick" => 18.0f,
                PrototypeCitizenRole.Forager when order.ResourceId is "berries" or "reeds" or "meals" => 18.0f,
                PrototypeCitizenRole.Hauler when order.Kind is PrototypeWorkOrderKind.HaulToDepot or PrototypeWorkOrderKind.HaulToRemoteDepot or PrototypeWorkOrderKind.HaulFromRemoteDepot or PrototypeWorkOrderKind.HaulToStructure or PrototypeWorkOrderKind.RefuelHearth => 22.0f,
                PrototypeCitizenRole.Processor when order.Kind == PrototypeWorkOrderKind.Process => 22.0f,
                PrototypeCitizenRole.Builder when order.Kind is PrototypeWorkOrderKind.Build or PrototypeWorkOrderKind.BuildPath or PrototypeWorkOrderKind.EstablishRemoteDepot => 22.0f,
                PrototypeCitizenRole.Generalist => 8.0f,
                _ => 0.0f
            };
        }
        private PrototypeResourceStoreState? ResolveCacheForCitizen(PrototypeWorkerState citizen)
        {
            string clusterId = ExtractClusterId(citizen.TargetResourceNodeName);
            return string.IsNullOrWhiteSpace(clusterId) ? null : GetStore($"cache.{clusterId}");
        }
        private string ExtractClusterId(string nodeName) => _resourceNodeClusterMap.TryGetValue(nodeName, out string? clusterId) ? clusterId : string.Empty;
        private bool TryPickupFromStore(PrototypeWorkerState citizen, string sourceStoreId)
        {
            PrototypeResourceStoreState? source = GetStore(sourceStoreId);
            if (source == null || string.IsNullOrWhiteSpace(citizen.CarryItemId))
            {
                return false;
            }

            if (!source.Remove(citizen.CarryItemId, 1))
            {
                return false;
            }

            citizen.CarryAmount = 1;
            return true;
        }
        private static string InferHarvestItemFromNode(string nodeName) => nodeName.Split('_')[0] switch
        {
            "logs" => "logs",
            "stone" => "stone",
            "berries" => "berries",
            "clay" => "clay",
            "reeds" => "reeds",
            _ => "logs"
        };
        private static void ClearCitizenCarry(PrototypeWorkerState citizen)
        {
            citizen.CarryItemId = string.Empty;
            citizen.CarryAmount = 0;
        }
        private PrototypeWorkerPhase GetSourceTravelPhase(string sourceStoreId) =>
            string.Equals(sourceStoreId, _centralDepot.StoreId, StringComparison.Ordinal)
                ? PrototypeWorkerPhase.MovingToDepot
                : sourceStoreId.StartsWith("cache.", StringComparison.Ordinal)
                    ? PrototypeWorkerPhase.MovingToCache
                    : PrototypeWorkerPhase.MovingToStructure;
        private void FailCitizenOrder(PrototypeWorkerState citizen, string blockedReason, PrototypeSettlementTickResult result, string message)
        {
            citizen.LastFailureReason = blockedReason;
            AddRecentEvent(citizen, blockedReason);
            IncrementCount(_blockedReasonCounts, blockedReason, 1);
            result.Events.Add(new PrototypeSettlementEvent(PrototypeEventTypes.SettlementBlocked, message));
            ClearCitizenCarry(citizen);
            BeginIdle(citizen, "Blocked");
        }
        private void FailBlockedStructure(PrototypeStructureState structure, PrototypeWorkerState citizen, PrototypeSettlementTickResult result, string blockedReason, string message)
        {
            structure.IsBlocked = true;
            structure.BlockedReason = blockedReason;
            structure.BlockedTicks++;
            FailCitizenOrder(citizen, blockedReason, result, message);
        }
        private static void AddRecentEvent(PrototypeWorkerState citizen, string text)
        {
            citizen.RecentEvents.Add(text);
            if (citizen.RecentEvents.Count > 4)
            {
                citizen.RecentEvents.RemoveAt(0);
            }
        }
        private Vector3 GetSleepPosition(PrototypeWorkerState citizen) =>
            citizen.HomeBedCapacity > 0
                ? citizen.HomePosition
                : ProjectToSurface(_world.SettlementSpawn.AnchorPosition + new Vector3(0.0f, 0.0f, 2.6f));
        private Vector3 GetCitizenHomePosition(int citizenIndex, int citizenCount)
        {
            float angle = (Mathf.Tau * citizenIndex / Math.Max(citizenCount, 1)) - (Mathf.Pi * 0.5f);
            return _world.SettlementSpawn.AnchorPosition + new Vector3(Mathf.Cos(angle) * 3.8f, 0.0f, Mathf.Sin(angle) * 3.8f);
        }
        private Vector3 ProjectToSurface(Vector3 position) => _world.WorldMap.ProjectToSurface(position);
        private static List<PrototypeCitizenRole> BuildRolePlan(IReadOnlyList<PrototypeRoleQuotaDefinition> roleQuotas, int citizenCount)
        {
            List<PrototypeCitizenRole> roles = new(citizenCount);
            foreach ((PrototypeCitizenRole role, int count) in roleQuotas.Select(role => (ParseRole(role.RoleId), Math.Max(1, (int)Math.Round(role.Share * citizenCount)))))
            {
                for (int index = 0; index < count && roles.Count < citizenCount; index++)
                {
                    roles.Add(role);
                }
            }

            while (roles.Count < citizenCount)
            {
                roles.Add(PrototypeCitizenRole.Generalist);
            }

            return roles;
        }
        private static PrototypeCitizenRole ParseRole(string roleId) => roleId.ToLowerInvariant() switch
        {
            "logger" => PrototypeCitizenRole.Logger,
            "mason" => PrototypeCitizenRole.Mason,
            "forager" => PrototypeCitizenRole.Forager,
            "hauler" => PrototypeCitizenRole.Hauler,
            "processor" => PrototypeCitizenRole.Processor,
            "builder" => PrototypeCitizenRole.Builder,
            _ => PrototypeCitizenRole.Generalist
        };

    }
}
