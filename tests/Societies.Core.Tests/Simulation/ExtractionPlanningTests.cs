using Godot;
using Societies.Simulation;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Societies.Core.Tests
{
    public sealed class ExtractionPlanningTests
    {
        private readonly ITestOutputHelper _output;

        public ExtractionPlanningTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Constructor_DefaultsToExactBoundedExtractionPlanning()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeSettlementSimulation simulation = new(
                scenario,
                bundle.RoleQuotas.Roles,
                PrototypeWorldGenerator.Generate(scenario));

            Assert.Equal(PrototypeExtractionPlanningMode.ExactBounded, simulation.ExtractionPlanningMode);
        }

        [Fact]
        public void ExtractionPlanningMode_IsIndependentFromGenericOrderSelectionMode()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeSettlementSimulation simulation = new(
                scenario,
                bundle.RoleQuotas.Roles,
                PrototypeWorldGenerator.Generate(scenario),
                orderSelectionMode: PrototypeOrderSelectionMode.ExhaustiveReference,
                extractionPlanningMode: PrototypeExtractionPlanningMode.ExactBounded);

            Assert.Equal(PrototypeOrderSelectionMode.ExhaustiveReference, simulation.OrderSelectionMode);
            Assert.Equal(PrototypeExtractionPlanningMode.ExactBounded, simulation.ExtractionPlanningMode);
        }

        [Fact]
        public void WholeResourceClassOmission_RequiresFrontierStrictlyAbovePriorityUpperBound()
        {
            int[] strictlyHigher = Enumerable.Repeat(941, 50).ToArray();
            int[] equalAtCutoff = strictlyHigher.Take(49).Append(940).ToArray();

            bool omitted = PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                strictlyHigher,
                frontierBudget: 50,
                priorityUpperBound: 940,
                new[] { "extract.berries_1", "extract.berries_2" },
                new HashSet<string>(StringComparer.Ordinal),
                desiredUnits: 2,
                out int omittedCount);
            bool equalOmitted = PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                equalAtCutoff,
                frontierBudget: 50,
                priorityUpperBound: 940,
                new[] { "extract.berries_1" },
                new HashSet<string>(StringComparer.Ordinal),
                desiredUnits: 1,
                out _);

            Assert.True(omitted);
            Assert.Equal(2, omittedCount);
            Assert.False(equalOmitted);
        }

        [Fact]
        public void WholeResourceClassOmission_RejectsUnderfilledExistingFrontier()
        {
            bool omitted = PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                Enumerable.Repeat(1200, 49).ToArray(),
                frontierBudget: 50,
                priorityUpperBound: 640,
                new[] { "extract.logs_1" },
                new HashSet<string>(StringComparer.Ordinal),
                desiredUnits: 1,
                out _);

            Assert.False(omitted);
        }

        [Fact]
        public void ExtractionPriorityUpperBound_IncludesBuiltCorridorBonus()
        {
            Assert.Equal(640, PrototypeExtractionPlanningMath.ComputePriorityUpperBound(640, hasBuiltCorridor: false));
            Assert.Equal(680, PrototypeExtractionPlanningMath.ComputePriorityUpperBound(640, hasBuiltCorridor: true));

            bool omitted = PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                Enumerable.Repeat(680, 50).ToArray(),
                frontierBudget: 50,
                priorityUpperBound: PrototypeExtractionPlanningMath.ComputePriorityUpperBound(640, hasBuiltCorridor: true),
                new[] { "extract.logs_1" },
                new HashSet<string>(StringComparer.Ordinal),
                desiredUnits: 1,
                out _);

            Assert.False(omitted);
        }

        [Fact]
        public void WholeResourceClassOmission_FallsBackForAnyExactClaimedIdCollision()
        {
            bool omitted = PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                Enumerable.Repeat(1200, 50).ToArray(),
                frontierBudget: 50,
                priorityUpperBound: 640,
                new[] { "extract.logs_1", "extract.logs_2" },
                new HashSet<string>(new[] { "extract.logs_2" }, StringComparer.Ordinal),
                desiredUnits: 1,
                out _);

            Assert.False(omitted);
        }

        [Fact]
        public void WholeResourceClassOmission_CountsEligibleDuplicatesAndCapsAtDesiredUnits()
        {
            string[] eligibleOrderIds = { "extract.same", "extract.same", "extract.other" };
            bool omitted = PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                Enumerable.Repeat(1200, 50).ToArray(),
                frontierBudget: 50,
                priorityUpperBound: 640,
                eligibleOrderIds,
                new HashSet<string>(StringComparer.Ordinal),
                desiredUnits: 2,
                out int omittedCount);
            bool allOmitted = PrototypeExtractionPlanningMath.TryComputeWholeResourceClassOmission(
                Enumerable.Repeat(1200, 50).ToArray(),
                frontierBudget: 50,
                priorityUpperBound: 640,
                eligibleOrderIds,
                new HashSet<string>(StringComparer.Ordinal),
                desiredUnits: 5,
                out int allOmittedCount);

            Assert.True(omitted);
            Assert.Equal(2, omittedCount);
            Assert.True(allOmitted);
            Assert.Equal(3, allOmittedCount);
        }

        [Fact]
        public void FrontierLimit_SortsWhenVirtualCountExceedsBudgetEvenIfActualCountEqualsBudget()
        {
            List<PrototypeWorkOrder> orders = Enumerable.Range(0, 50)
                .Select(index => new PrototypeWorkOrder
                {
                    OrderId = $"order.{49 - index:D2}",
                    Priority = index % 2
                })
                .ToList();

            List<PrototypeWorkOrder> limited = PrototypeExtractionPlanningMath.ApplyFrontierLimit(
                orders,
                frontierBudget: 50,
                virtualUncappedCount: 51);

            Assert.Equal(
                orders.OrderByDescending(order => order.Priority).ThenBy(order => order.OrderId, StringComparer.Ordinal).Select(order => order.OrderId),
                limited.Select(order => order.OrderId));
            Assert.NotSame(orders, limited);
        }

        [Fact]
        public void ControlledFrontier_OmitsStrictClassButFallsBackAtEqualityAndProcessesLaterClass()
        {
            (PrototypeSettlementSimulation simulation, WorldGenerationResult world) = NewControlledSimulation();
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);
            PrototypeResourceSiteState logSite = resources.First(site => site.ResourceId == "logs");
            PrototypeResourceSiteState berrySite = resources.First(site => site.ResourceId == "berries");
            List<PrototypeWorkOrder> fixedOrders = FixedOrders(priority: 641);

            ControlledExtractionFrontierProbe probe = PlanExtractionFrontier(
                simulation,
                fixedOrders,
                new[] { logSite, berrySite },
                new[]
                {
                    (ResourceId: "logs", DesiredUnits: 1, BasePriority: 640),
                    (ResourceId: "berries", DesiredUnits: 1, BasePriority: 641)
                });

            Assert.Equal(1, probe.OmittedCount);
            Assert.Equal(52, probe.VirtualUncappedCount);
            Assert.Equal(50, probe.Orders.Count);
            Assert.Contains(probe.Orders, order => order.OrderId == $"extract.{berrySite.NodeName}");
            Assert.DoesNotContain(probe.Orders, order => order.OrderId == $"extract.{logSite.NodeName}");
            Assert.Equal(
                probe.Orders.OrderByDescending(order => order.Priority).ThenBy(order => order.OrderId, StringComparer.Ordinal).Select(order => order.OrderId),
                probe.Orders.Select(order => order.OrderId));
            Assert.True(probe.PathPlanLookups > 0, "The equal-priority later class must execute the legacy route/penalty path.");
        }

        [Fact]
        public void ControlledFrontier_CorridorBonusPreventsUnsafeStrictOmission()
        {
            (PrototypeSettlementSimulation noCorridor, WorldGenerationResult noCorridorWorld) = NewControlledSimulation();
            (PrototypeSettlementSimulation corridor, WorldGenerationResult corridorWorld) = NewControlledSimulation();
            PrototypePathSegmentState corridorSegment = corridor.PathSegments.First(segment => segment.CorridorId == "corridor.logs");
            corridorSegment.IsBuilt = true;
            PrototypeResourceSiteState noCorridorSite = BuildResourceSites(noCorridorWorld).First(site => site.ResourceId == "logs");
            PrototypeResourceSiteState corridorSite = BuildResourceSites(corridorWorld).First(site => site.ResourceId == "logs");
            List<PrototypeWorkOrder> fixedOrders = FixedOrders(priority: 680);
            (string ResourceId, int DesiredUnits, int BasePriority)[] extractionClass =
            {
                ("logs", DesiredUnits: 1, BasePriority: 640)
            };

            ControlledExtractionFrontierProbe omitted = PlanExtractionFrontier(
                noCorridor,
                fixedOrders,
                new[] { noCorridorSite },
                extractionClass);
            ControlledExtractionFrontierProbe retained = PlanExtractionFrontier(
                corridor,
                fixedOrders,
                new[] { corridorSite },
                extractionClass);

            Assert.Equal(1, omitted.OmittedCount);
            Assert.Equal(0, omitted.PathPlanLookups);
            Assert.Equal(0, retained.OmittedCount);
            Assert.True(retained.PathPlanLookups > 0);
        }

        [Fact]
        public void ControlledFrontier_ClaimedCollisionExecutesIdenticalLegacyCacheTraceBeforeFinalRemoval()
        {
            (PrototypeSettlementSimulation unclaimed, WorldGenerationResult unclaimedWorld) = NewControlledSimulation();
            (PrototypeSettlementSimulation claimed, WorldGenerationResult claimedWorld) = NewControlledSimulation();
            PrototypeResourceSiteState unclaimedSite = BuildResourceSites(unclaimedWorld).First(site => site.ResourceId == "logs");
            PrototypeResourceSiteState claimedSite = BuildResourceSites(claimedWorld).First(site => site.ResourceId == "logs");
            claimed.Citizens[0].CurrentOrderId = $"extract.{claimedSite.NodeName}";
            claimed.Citizens[0].Phase = PrototypeWorkerPhase.MovingToResource;
            List<PrototypeWorkOrder> fixedOrders = FixedOrders(priority: 640);
            (string ResourceId, int DesiredUnits, int BasePriority)[] extractionClass =
            {
                ("logs", DesiredUnits: 1, BasePriority: 640)
            };

            ControlledExtractionFrontierProbe unclaimedCold = PlanExtractionFrontier(
                unclaimed,
                fixedOrders,
                new[] { unclaimedSite },
                extractionClass);
            ControlledExtractionFrontierProbe claimedCold = PlanExtractionFrontier(
                claimed,
                fixedOrders,
                new[] { claimedSite },
                extractionClass);
            ControlledExtractionFrontierProbe unclaimedWarm = PlanExtractionFrontier(
                unclaimed,
                fixedOrders,
                new[] { unclaimedSite },
                extractionClass);
            ControlledExtractionFrontierProbe claimedWarm = PlanExtractionFrontier(
                claimed,
                fixedOrders,
                new[] { claimedSite },
                extractionClass);

            Assert.Equal(0, claimedCold.OmittedCount);
            Assert.Equal(unclaimedCold.PathPlanLookups, claimedCold.PathPlanLookups);
            Assert.Equal(unclaimedCold.PathPlanCacheHits, claimedCold.PathPlanCacheHits);
            Assert.Equal(unclaimedCold.PathPlanCacheMisses, claimedCold.PathPlanCacheMisses);
            Assert.Equal(unclaimedCold.CachedRouteDistanceFastPathHits, claimedCold.CachedRouteDistanceFastPathHits);
            Assert.Equal(unclaimedCold.PerformanceProbe.PathCacheEntryCount, claimedCold.PerformanceProbe.PathCacheEntryCount);
            Assert.Equal(unclaimedWarm.PathPlanLookups, claimedWarm.PathPlanLookups);
            Assert.Equal(unclaimedWarm.PathPlanCacheHits, claimedWarm.PathPlanCacheHits);
            Assert.Equal(unclaimedWarm.PathPlanCacheMisses, claimedWarm.PathPlanCacheMisses);
            Assert.Equal(unclaimedWarm.CachedRouteDistanceFastPathHits, claimedWarm.CachedRouteDistanceFastPathHits);
            Assert.DoesNotContain(claimedCold.Orders, order => order.OrderId == claimed.Citizens[0].CurrentOrderId);
        }

        [Fact]
        public void ControlledFrontier_DuplicateCandidatesUseVirtualCountAndDeterministicFallback()
        {
            (PrototypeSettlementSimulation simulation, WorldGenerationResult world) = NewControlledSimulation();
            PrototypeResourceSiteState site = BuildResourceSites(world).First(candidate => candidate.ResourceId == "logs");
            PrototypeResourceSiteState duplicate = site with { Position = site.Position + new Vector3(0.25f, 0.0f, 0.25f) };

            ControlledExtractionFrontierProbe omitted = PlanExtractionFrontier(
                simulation,
                FixedOrders(priority: 641),
                new[] { site, duplicate },
                new[] { (ResourceId: "logs", DesiredUnits: 3, BasePriority: 640) });
            ControlledExtractionFrontierProbe fallback = PlanExtractionFrontier(
                simulation,
                FixedOrders(priority: 640),
                new[] { site, duplicate },
                new[] { (ResourceId: "logs", DesiredUnits: 3, BasePriority: 640) });

            Assert.Equal(2, omitted.OmittedCount);
            Assert.Equal(52, omitted.VirtualUncappedCount);
            Assert.Equal(0, omitted.PathPlanLookups);
            Assert.Equal(0, fallback.OmittedCount);
            Assert.Equal(52, fallback.VirtualUncappedCount);
            Assert.True(fallback.PathPlanLookups >= 2);
            Assert.Equal(2, fallback.Orders.Count(order => order.OrderId == $"extract.{site.NodeName}"));
        }

        [Fact]
        public void ControlledFrontier_UnreachableCandidatePreservesNegativeCacheFallbackTrace()
        {
            (PrototypeSettlementSimulation simulation, WorldGenerationResult world) = NewDisconnectedSimulation();
            TerrainCell unreachableCell = world.WorldMap.GetCell(2, 0);
            PrototypeResourceSiteState unreachable = new(
                "unreachable.logs",
                "logs",
                unreachableCell.WorldPosition,
                UnitsRemaining: 1,
                "unreachable.cluster");
            (string ResourceId, int DesiredUnits, int BasePriority)[] extractionClass =
            {
                ("logs", DesiredUnits: 1, BasePriority: 640)
            };

            ControlledExtractionFrontierProbe cold = PlanExtractionFrontier(
                simulation,
                FixedOrders(priority: 640),
                new[] { unreachable },
                extractionClass);
            ControlledExtractionFrontierProbe warm = PlanExtractionFrontier(
                simulation,
                FixedOrders(priority: 640),
                new[] { unreachable },
                extractionClass);

            Assert.Equal(0, cold.OmittedCount);
            Assert.True(cold.PathPlanCacheMisses > 0);
            Assert.Equal(0, warm.PathPlanCacheMisses);
            Assert.True(warm.PathPlanCacheHits > 0);
            Assert.Equal(51, cold.VirtualUncappedCount);
            Assert.DoesNotContain(cold.Orders, order => order.OrderId == "extract.unreachable.logs");
            Assert.True(warm.PerformanceProbe.AllPathCacheKeysMatchNavigationRulesVersion);
        }

        [Fact]
        public void ExactTopK_AllSelectedUniqueNamesUsesOrdinalOrderWithoutExactQueries()
        {
            PrototypeExtractionCandidate[] candidates =
            {
                Candidate("node.c", lowerBound: 3.0f, originalIndex: 0),
                Candidate("node.a", lowerBound: 1.0f, originalIndex: 1),
                Candidate("node.b", lowerBound: 2.0f, originalIndex: 2)
            };
            int exactQueries = 0;

            IReadOnlyList<PrototypeExtractionCandidate> selected = PrototypeExtractionPlanningMath.SelectExactTopK(
                candidates,
                desiredUnits: candidates.Length,
                candidate =>
                {
                    exactQueries++;
                    return candidate.DistanceLowerBound;
                });

            Assert.Equal(0, exactQueries);
            Assert.Equal(new[] { "node.a", "node.b", "node.c" }, selected.Select(candidate => candidate.Site.NodeName));
        }

        [Fact]
        public void ExactTopK_DuplicateNamesPreserveExactDistanceAndOriginalInputOrder()
        {
            PrototypeExtractionCandidate[] candidates =
            {
                Candidate("same", lowerBound: 1.0f, originalIndex: 0),
                Candidate("same", lowerBound: 1.0f, originalIndex: 1)
            };
            Dictionary<int, float> exactDistances = new() { [0] = 4.0f, [1] = 2.0f };
            int exactQueries = 0;

            IReadOnlyList<PrototypeExtractionCandidate> selected = PrototypeExtractionPlanningMath.SelectExactTopK(
                candidates,
                desiredUnits: candidates.Length,
                candidate =>
                {
                    exactQueries++;
                    return exactDistances[candidate.OriginalIndex];
                });

            Assert.Equal(2, exactQueries);
            Assert.Equal(new[] { 1, 0 }, selected.Select(candidate => candidate.OriginalIndex));
        }

        [Fact]
        public void ExactTopK_StrictCutoffEvaluatesEqualBoundAndHonorsOrdinalTie()
        {
            PrototypeExtractionCandidate[] candidates =
            {
                Candidate("node.z", lowerBound: 1.0f, originalIndex: 0),
                Candidate("node.a", lowerBound: 2.0f, originalIndex: 1),
                Candidate("node.never", lowerBound: 3.0f, originalIndex: 2)
            };
            int exactQueries = 0;

            IReadOnlyList<PrototypeExtractionCandidate> selected = PrototypeExtractionPlanningMath.SelectExactTopK(
                candidates,
                desiredUnits: 1,
                candidate =>
                {
                    exactQueries++;
                    return candidate.OriginalIndex == 2 ? 1.0f : 2.0f;
                });

            Assert.Equal(2, exactQueries);
            Assert.Equal("node.a", Assert.Single(selected).Site.NodeName);
        }

        [Fact]
        public void ExactTopK_UnreachableInfinityCannotPruneLaterReachableCandidate()
        {
            PrototypeExtractionCandidate[] candidates =
            {
                Candidate("unreachable", lowerBound: 1.0f, originalIndex: 0),
                Candidate("reachable", lowerBound: 2.0f, originalIndex: 1)
            };
            int exactQueries = 0;

            IReadOnlyList<PrototypeExtractionCandidate> selected = PrototypeExtractionPlanningMath.SelectExactTopK(
                candidates,
                desiredUnits: 1,
                candidate =>
                {
                    exactQueries++;
                    return candidate.Site.NodeName == "unreachable" ? float.PositiveInfinity : 5.0f;
                });

            Assert.Equal(2, exactQueries);
            Assert.Equal("reachable", Assert.Single(selected).Site.NodeName);
        }

        [Fact]
        public void RemoteDepotPenaltySkipsExactQueryForBuiltDepotAndStrictlyRemoteBound()
        {
            int exactQueries = 0;

            bool builtDepotPenalty = PrototypeExtractionPlanningMath.ShouldApplyRemoteDepotPenalty(
                hasBuiltRemoteDepot: true,
                distanceLowerBound: 100.0f,
                activationDistance: 55.0f,
                () =>
                {
                    exactQueries++;
                    return 100.0f;
                });
            bool strictBoundPenalty = PrototypeExtractionPlanningMath.ShouldApplyRemoteDepotPenalty(
                hasBuiltRemoteDepot: false,
                distanceLowerBound: 55.001f,
                activationDistance: 55.0f,
                () =>
                {
                    exactQueries++;
                    return 55.001f;
                });

            Assert.False(builtDepotPenalty);
            Assert.True(strictBoundPenalty);
            Assert.Equal(0, exactQueries);
        }

        [Fact]
        public void RemoteDepotPenaltyQueriesAtThresholdEquality()
        {
            int exactQueries = 0;

            bool applyPenalty = PrototypeExtractionPlanningMath.ShouldApplyRemoteDepotPenalty(
                hasBuiltRemoteDepot: false,
                distanceLowerBound: 55.0f,
                activationDistance: 55.0f,
                () =>
                {
                    exactQueries++;
                    return 55.25f;
                });

            Assert.True(applyPenalty);
            Assert.Equal(1, exactQueries);
        }

        [Theory]
        [InlineData("balanced_basin")]
        [InlineData("long_haul_quarry")]
        [InlineData("food_poor_highlands")]
        [InlineData("wetland_builder")]
        public void StraightLineBound_CoversEveryNavigableResourceEndpoint(string scenarioId)
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve(scenarioId);
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeNavigationGrid navigation = new(world.WorldMap, Array.Empty<Vector2I>(), rulesVersion: 1);
            Vector3 start = world.SettlementSpawn.AnchorPosition;
            int navigableEndpoints = 0;

            foreach (PrototypeResourceSpawn resource in world.ResourceSpawns)
            {
                if (!navigation.TryFindPath(start, resource.Position, out PrototypePathPlan? plan))
                {
                    continue;
                }

                float lowerBound = PrototypeOrderSelectionMath.ComputeStraightLineDistanceLowerBound(
                    start,
                    resource.Position,
                    world.WorldMap.Cells.Count);
                Assert.True(
                    lowerBound <= plan!.TotalDistanceMeters,
                    $"{scenarioId}/{resource.ResourceId}: bound={lowerBound}, exact={plan.TotalDistanceMeters}");
                navigableEndpoints++;
            }

            Assert.True(navigableEndpoints > 0);
        }

        [Theory]
        [InlineData("balanced_basin")]
        [InlineData("long_haul_quarry")]
        [InlineData("food_poor_highlands")]
        [InlineData("wetland_builder")]
        public void ExactBoundedExtraction_MatchesExhaustiveReferenceForThreeHundredTicks(string scenarioId)
        {
            PrototypeCatalogBundle optimizedBundle = LoadCatalogs();
            PrototypeCatalogBundle exhaustiveBundle = LoadCatalogs();
            PrototypeScenarioDefinition optimizedScenario = optimizedBundle.Scenarios.Resolve(scenarioId);
            PrototypeScenarioDefinition exhaustiveScenario = exhaustiveBundle.Scenarios.Resolve(scenarioId);
            if (scenarioId == "balanced_basin")
            {
                optimizedScenario.InitialCitizens = 16;
                exhaustiveScenario.InitialCitizens = 16;
            }

            WorldGenerationResult optimizedWorld = PrototypeWorldGenerator.Generate(optimizedScenario);
            WorldGenerationResult exhaustiveWorld = PrototypeWorldGenerator.Generate(exhaustiveScenario);
            PrototypeSettlementSimulation optimized = new(
                optimizedScenario,
                optimizedBundle.RoleQuotas.Roles,
                optimizedWorld,
                orderSelectionMode: PrototypeOrderSelectionMode.ExactBranchAndBound,
                extractionPlanningMode: PrototypeExtractionPlanningMode.ExactBounded);
            PrototypeSettlementSimulation exhaustive = new(
                exhaustiveScenario,
                exhaustiveBundle.RoleQuotas.Roles,
                exhaustiveWorld,
                orderSelectionMode: PrototypeOrderSelectionMode.ExactBranchAndBound,
                extractionPlanningMode: PrototypeExtractionPlanningMode.ExhaustiveReference);
            List<PrototypeResourceSiteState> optimizedResources = BuildResourceSites(optimizedWorld);
            List<PrototypeResourceSiteState> exhaustiveResources = BuildResourceSites(exhaustiveWorld);
            long optimizedQueries = 0;
            long exhaustiveQueries = 0;
            long optimizedGeneralMisses = 0;
            long exhaustiveGeneralMisses = 0;
            long optimizedOmittedOrders = 0;
            float currentHour = 8.0f;

            for (int tick = 1; tick <= 300; tick++)
            {
                PrototypeSettlementTickResult optimizedResult = optimized.Advance(
                    optimizedResources,
                    currentHour,
                    PrototypeWeather.Clear);
                PrototypeSettlementTickResult exhaustiveResult = exhaustive.Advance(
                    exhaustiveResources,
                    currentHour,
                    PrototypeWeather.Clear);

                optimizedQueries += optimized.Diagnostics.PathPlanLookups;
                exhaustiveQueries += exhaustive.Diagnostics.PathPlanLookups;
                int optimizedGeneralMissesThisTick = optimized.Diagnostics.PathPlanCacheMisses - optimized.Diagnostics.SelectorPathCacheMisses;
                int exhaustiveGeneralMissesThisTick = exhaustive.Diagnostics.PathPlanCacheMisses - exhaustive.Diagnostics.SelectorPathCacheMisses;
                Assert.True(optimizedGeneralMissesThisTick >= 0);
                Assert.True(exhaustiveGeneralMissesThisTick >= 0);
                optimizedGeneralMisses += optimizedGeneralMissesThisTick;
                exhaustiveGeneralMisses += exhaustiveGeneralMissesThisTick;
                optimizedOmittedOrders += optimized.Diagnostics.ExtractionOrdersOmitted;
                Assert.Equal(0, exhaustive.Diagnostics.ExtractionOrdersOmitted);
                Assert.Equal(exhaustive.Diagnostics.WorkOrdersGenerated, optimized.Diagnostics.WorkOrdersGenerated);
                Assert.Equal(exhaustive.Diagnostics.WorkOrdersGeneratedUncapped, optimized.Diagnostics.WorkOrdersGeneratedUncapped);
                Assert.Equal(exhaustive.Diagnostics.WorkOrdersClaimed, optimized.Diagnostics.WorkOrdersClaimed);
                Assert.Equal(exhaustive.Diagnostics.WorkOrdersRemaining, optimized.Diagnostics.WorkOrdersRemaining);
                Assert.Equal(
                    exhaustive.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal).Select(worker => (worker.WorkerId, worker.CurrentOrderId)),
                    optimized.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal).Select(worker => (worker.WorkerId, worker.CurrentOrderId)));
                Assert.Equal(
                    JsonSerializer.Serialize(exhaustive.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)),
                    JsonSerializer.Serialize(optimized.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)));
                Assert.Equal(exhaustiveResult.Events, optimizedResult.Events);
                Assert.Equal(exhaustiveResult.HarvestRequests, optimizedResult.HarvestRequests);

                ApplyHarvestRequests(exhaustive, exhaustiveResources, exhaustiveResult.HarvestRequests);
                ApplyHarvestRequests(optimized, optimizedResources, optimizedResult.HarvestRequests);
                Assert.Equal(exhaustiveResources, optimizedResources);
                Assert.Equal(
                    JsonSerializer.Serialize(exhaustive.ProducedResources.OrderBy(pair => pair.Key, StringComparer.Ordinal)),
                    JsonSerializer.Serialize(optimized.ProducedResources.OrderBy(pair => pair.Key, StringComparer.Ordinal)));
                Assert.Equal(
                    JsonSerializer.Serialize(exhaustive.ConsumedResources.OrderBy(pair => pair.Key, StringComparer.Ordinal)),
                    JsonSerializer.Serialize(optimized.ConsumedResources.OrderBy(pair => pair.Key, StringComparer.Ordinal)));
                Assert.Equal(
                    JsonSerializer.Serialize(exhaustive.CentralDepot),
                    JsonSerializer.Serialize(optimized.CentralDepot));
                PrototypePerformanceProbeSnapshot optimizedProbe = optimized.CapturePerformanceProbeState();
                PrototypePerformanceProbeSnapshot exhaustiveProbe = exhaustive.CapturePerformanceProbeState();
                Assert.True(optimizedProbe.AllPathCacheKeysMatchNavigationRulesVersion);
                Assert.True(exhaustiveProbe.AllPathCacheKeysMatchNavigationRulesVersion);
                Assert.Equal(exhaustiveProbe.NavigationRulesVersion, optimizedProbe.NavigationRulesVersion);
                Assert.Equal(exhaustiveProbe.TotalNavigationInvalidations, optimizedProbe.TotalNavigationInvalidations);
                Assert.Equal(
                    exhaustive.RouteBacklogTicksByKind.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                    optimized.RouteBacklogTicksByKind.OrderBy(pair => pair.Key, StringComparer.Ordinal));
                if (scenarioId == "balanced_basin" && tick == 218)
                {
                    Assert.Equal(218, optimized.RouteBacklogTicksByKind.GetValueOrDefault("extract"));
                    Assert.Equal(48, optimized.RouteBacklogTicksByKind.GetValueOrDefault("buildpath"));
                }
                currentHour = AdvanceHour(currentHour);
            }

            Assert.Equal(
                JsonSerializer.Serialize(exhaustive.CaptureSnapshot(300)),
                JsonSerializer.Serialize(optimized.CaptureSnapshot(300)));
            Assert.True(
                optimizedQueries < exhaustiveQueries,
                $"Expected extraction planning to reduce queries; optimized={optimizedQueries}, exhaustive={exhaustiveQueries}.");
            Assert.True(
                optimizedGeneralMisses < exhaustiveGeneralMisses,
                $"Expected whole-class omission to reduce general path-cache misses; optimized={optimizedGeneralMisses}, exhaustive={exhaustiveGeneralMisses}.");
            if (scenarioId == "balanced_basin")
            {
                Assert.True(optimizedOmittedOrders > 0, "The actual BuildWorkOrders path must exercise whole-class omission.");
                Assert.True(
                    optimized.CapturePerformanceProbeState().TotalNavigationInvalidations > 0,
                    "The differential must retain exact cache/version behavior across a natural navigation invalidation.");
            }
            _output.WriteLine(
                $"{scenarioId}: optimized queries={optimizedQueries}, exhaustive queries={exhaustiveQueries}, " +
                $"reduction={(1.0 - optimizedQueries / (double)exhaustiveQueries):P2}; " +
                $"general misses optimized={optimizedGeneralMisses}, exhaustive={exhaustiveGeneralMisses}");
        }

        private static PrototypeExtractionCandidate Candidate(string nodeName, float lowerBound, int originalIndex)
        {
            PrototypeResourceSiteState site = new(nodeName, "logs", Vector3.Zero, 1, "cluster");
            return new PrototypeExtractionCandidate(site, Vector3.Zero, lowerBound, originalIndex);
        }

        private static ControlledExtractionFrontierProbe PlanExtractionFrontier(
            PrototypeSettlementSimulation simulation,
            IReadOnlyList<PrototypeWorkOrder> existingOrders,
            IReadOnlyList<PrototypeResourceSiteState> resources,
            IReadOnlyList<(string ResourceId, int DesiredUnits, int BasePriority)> extractionClasses)
        {
            System.Reflection.MethodInfo method = typeof(PrototypeSettlementSimulation).GetMethod(
                "PlanExtractionFrontierForTesting",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            object boxedProbe = method.Invoke(
                simulation,
                new object[] { existingOrders, resources, extractionClasses })!;
            Type probeType = boxedProbe.GetType();

            T Read<T>(string propertyName)
            {
                return (T)probeType.GetProperty(propertyName)!.GetValue(boxedProbe)!;
            }

            return new ControlledExtractionFrontierProbe(
                Read<IReadOnlyList<PrototypeWorkOrder>>("Orders"),
                Read<int>("VirtualUncappedCount"),
                Read<int>("OmittedCount"),
                Read<int>("PathPlanLookups"),
                Read<int>("PathPlanCacheHits"),
                Read<int>("PathPlanCacheMisses"),
                Read<long>("CachedRouteDistanceFastPathHits"),
                Read<PrototypePerformanceProbeSnapshot>("PerformanceProbe"));
        }

        private readonly record struct ControlledExtractionFrontierProbe(
            IReadOnlyList<PrototypeWorkOrder> Orders,
            int VirtualUncappedCount,
            int OmittedCount,
            int PathPlanLookups,
            int PathPlanCacheHits,
            int PathPlanCacheMisses,
            long CachedRouteDistanceFastPathHits,
            PrototypePerformanceProbeSnapshot PerformanceProbe);

        private static (PrototypeSettlementSimulation Simulation, WorldGenerationResult World) NewControlledSimulation()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            scenario.InitialCitizens = 1;
            scenario.RemoteDepotPolicy.ActivationDistanceMeters = 100_000.0f;
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            return (
                new PrototypeSettlementSimulation(
                    scenario,
                    bundle.RoleQuotas.Roles,
                    world,
                    orderSelectionMode: PrototypeOrderSelectionMode.ExactBranchAndBound,
                    extractionPlanningMode: PrototypeExtractionPlanningMode.ExactBounded),
                world);
        }

        private static (PrototypeSettlementSimulation Simulation, WorldGenerationResult World) NewDisconnectedSimulation()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            scenario.InitialCitizens = 1;
            scenario.StartingStructures.Clear();
            scenario.StartingBuildQueue.Clear();
            scenario.RemoteDepotPolicy.ActivationDistanceMeters = 100_000.0f;
            TerrainCell[] cells = Enumerable.Range(0, 3)
                .Select(index => new TerrainCell
                {
                    GridX = index,
                    GridY = 0,
                    WorldPosition = new Vector3(-2.0f + (index * 2.0f), 0.0f, 0.0f),
                    ElevationMeters = 0.0f,
                    SlopeDegrees = 0.0f,
                    MovementCost = 1.0f,
                    IsBuildable = index != 1,
                    Biome = index == 1 ? BiomeType.Wetland : BiomeType.Meadow
                })
                .ToArray();
            WorldMapState worldMap = new(3, 1, cellSizeMeters: 2.0f, worldSize: 6.0f, cells);
            WorldGenerationResult world = new()
            {
                WorldSeed = scenario.SimulationSeed,
                WorldMap = worldMap,
                SettlementSpawn = new SettlementSpawnState
                {
                    AnchorPosition = cells[0].WorldPosition,
                    GridX = 0,
                    GridY = 0
                }
            };
            return (
                new PrototypeSettlementSimulation(
                    scenario,
                    bundle.RoleQuotas.Roles,
                    world,
                    orderSelectionMode: PrototypeOrderSelectionMode.ExactBranchAndBound,
                    extractionPlanningMode: PrototypeExtractionPlanningMode.ExactBounded),
                world);
        }

        private static List<PrototypeWorkOrder> FixedOrders(int priority)
        {
            return Enumerable.Range(0, 50)
                .Select(index => new PrototypeWorkOrder
                {
                    OrderId = $"zz.fixed.{index:D2}",
                    Priority = priority
                })
                .ToList();
        }

        private static void ApplyHarvestRequests(
            PrototypeSettlementSimulation simulation,
            List<PrototypeResourceSiteState> resources,
            IReadOnlyList<PrototypeHarvestRequest> requests)
        {
            foreach (PrototypeHarvestRequest request in requests)
            {
                int index = resources.FindIndex(site => site.NodeName == request.TargetNodeName);
                Assert.True(index >= 0, $"Missing resource node {request.TargetNodeName}");
                PrototypeResourceSiteState site = resources[index];
                if (site.UnitsRemaining < request.Amount)
                {
                    simulation.OnHarvestFailed(request.WorkerId);
                    continue;
                }

                resources[index] = site with { UnitsRemaining = site.UnitsRemaining - request.Amount };
            }
        }

        private static float AdvanceHour(float currentHour)
        {
            float next = currentHour + (24.0f / 600.0f / 20.0f);
            return next >= 24.0f ? next - 24.0f : next;
        }

        private static List<PrototypeResourceSiteState> BuildResourceSites(WorldGenerationResult world)
        {
            return world.ResourceSpawns
                .GroupBy(spawn => spawn.ResourceId)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .SelectMany(group => group.Select((spawn, index) => new PrototypeResourceSiteState(
                    $"{spawn.ResourceId}_{index + 1}",
                    spawn.ResourceId,
                    spawn.Position,
                    spawn.UnitsRemaining,
                    spawn.ClusterId)))
                .ToList();
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            return PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());
        }

        private static string GetCatalogDirectoryPath()
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not find src/societies/data.");
        }
    }
}
