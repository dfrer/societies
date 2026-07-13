using Godot;
using Societies.Simulation;
using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Societies.Core.Tests
{
    public sealed class OrderSelectionTests
    {
        private readonly ITestOutputHelper _output;

        public OrderSelectionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Constructor_DefaultsToExactBranchAndBound()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeSettlementSimulation simulation = new(
                scenario,
                bundle.RoleQuotas.Roles,
                PrototypeWorldGenerator.Generate(scenario));

            Assert.Equal(PrototypeOrderSelectionMode.ExactBranchAndBound, simulation.OrderSelectionMode);
        }

        [Fact]
        public void ScoreUpperBound_IsNeverBelowExactNavigableScore()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeNavigationGrid navigation = new(world.WorldMap, Array.Empty<Vector2I>(), rulesVersion: 1);
            Vector3 start = world.SettlementSpawn.AnchorPosition;
            TerrainCell destinationCell = world.WorldMap.Cells
                .Where(cell => cell.Biome != BiomeType.Wetland && cell.SlopeDegrees <= 18.0f)
                .OrderByDescending(cell => HorizontalDistance(start, cell.WorldPosition))
                .First();

            Assert.True(navigation.TryFindPath(start, destinationCell.WorldPosition, out PrototypePathPlan? plan));
            const int priority = 740;
            const float roleBonus = 18.0f;
            float exactScore = priority + roleBonus -
                (PrototypeOrderSelectionMath.ExactDistancePenalty * plan!.TotalDistanceMeters);
            float upperBound = PrototypeOrderSelectionMath.ComputeScoreUpperBound(
                priority,
                roleBonus,
                start,
                destinationCell.WorldPosition,
                world.WorldMap.Cells.Count);

            Assert.True(
                upperBound >= exactScore,
                $"Expected bound {upperBound} to cover exact score {exactScore} for distance {plan.TotalDistanceMeters}.");
        }

        [Fact]
        public void ExactTieBreak_UsesOrdinalOrderIdThenOriginalIndex()
        {
            Assert.True(PrototypeOrderSelectionMath.IsExactCandidatePreferred(
                100.0f, "order.a", 9, 100.0f, "order.b", 0));
            Assert.False(PrototypeOrderSelectionMath.IsExactCandidatePreferred(
                100.0f, "order.b", 0, 100.0f, "order.a", 9));
            Assert.True(PrototypeOrderSelectionMath.IsExactCandidatePreferred(
                100.0f, "same", 2, 100.0f, "same", 3));
            Assert.False(PrototypeOrderSelectionMath.IsExactCandidatePreferred(
                99.0f, "order.a", 0, 100.0f, "order.z", 9));
        }

        [Fact]
        public void GenericSelection_AccountsQueriesAndReusesSelectedRouteExactlyOnce()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);
            PrototypeSettlementSimulation.PrototypeSettlementDiagnosticsState diagnostics = simulation.Diagnostics;

            Assert.Equal(diagnostics.CandidateOrdersEvaluated, diagnostics.SelectorCandidatesBounded);
            Assert.Equal(
                diagnostics.SelectorExactPathQueries,
                diagnostics.SelectorPathCacheHits + diagnostics.SelectorPathCacheMisses);
            Assert.Equal(
                diagnostics.SelectorCandidatesBounded,
                diagnostics.SelectorCandidatesExactScored +
                diagnostics.SelectorCandidatesPruned +
                diagnostics.UnreachableWorkOrderCandidatesSkipped);
            Assert.Equal(diagnostics.WorkOrdersClaimed, diagnostics.SelectorSelectedRouteReuses);
            Assert.True(diagnostics.SelectorCandidatesPruned > 0);
            Assert.True(diagnostics.SelectorSelectedRouteReuses > 0);
        }

        [Fact]
        public void GenericSelection_AllUnreachableCandidatesRemainIdleAndAreAccounted()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = new(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);
            TerrainCell blockedCell = world.WorldMap.Cells.First(cell => cell.Biome == BiomeType.Wetland);

            foreach (PrototypeWorkerState worker in simulation.Workers)
            {
                worker.Phase = PrototypeWorkerPhase.Incapacitated;
            }

            PrototypeWorkerState isolatedCitizen = simulation.Workers
                .OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)
                .First();
            isolatedCitizen.Phase = PrototypeWorkerPhase.Idle;
            isolatedCitizen.Position = blockedCell.WorldPosition;
            isolatedCitizen.HomePosition = blockedCell.WorldPosition;
            isolatedCitizen.Needs.Nutrition = 100.0f;
            isolatedCitizen.Needs.Fatigue = 0.0f;

            simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);

            Assert.Equal(PrototypeWorkerPhase.Idle, isolatedCitizen.Phase);
            Assert.Equal("navigation.unreachable", isolatedCitizen.LastFailureReason);
            Assert.Equal("No reachable work", isolatedCitizen.ActivityText);
            Assert.Equal(0, simulation.Diagnostics.SelectorCandidatesPruned);
            Assert.Equal(0, simulation.Diagnostics.SelectorCandidatesExactScored);
            Assert.Equal(
                simulation.Diagnostics.SelectorCandidatesBounded,
                simulation.Diagnostics.UnreachableWorkOrderCandidatesSkipped);
            Assert.Equal(
                simulation.Diagnostics.SelectorExactPathQueries,
                simulation.Diagnostics.SelectorPathCacheHits + simulation.Diagnostics.SelectorPathCacheMisses);
        }

        [Theory]
        [InlineData("balanced_basin")]
        [InlineData("long_haul_quarry")]
        [InlineData("food_poor_highlands")]
        [InlineData("wetland_builder")]
        public void OptimizedSelector_MatchesExhaustiveReferenceForThreeHundredTicks(string scenarioId)
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
                orderSelectionMode: PrototypeOrderSelectionMode.ExactBranchAndBound);
            PrototypeSettlementSimulation exhaustive = new(
                exhaustiveScenario,
                exhaustiveBundle.RoleQuotas.Roles,
                exhaustiveWorld,
                orderSelectionMode: PrototypeOrderSelectionMode.ExhaustiveReference);
            List<PrototypeResourceSiteState> optimizedResources = BuildResourceSites(optimizedWorld);
            List<PrototypeResourceSiteState> exhaustiveResources = BuildResourceSites(exhaustiveWorld);
            long optimizedQueries = 0;
            long exhaustiveQueries = 0;
            Stopwatch optimizedRuntime = new();
            Stopwatch exhaustiveRuntime = new();
            float currentHour = 8.0f;

            for (int tick = 1; tick <= 300; tick++)
            {
                optimizedRuntime.Start();
                PrototypeSettlementTickResult optimizedResult = optimized.Advance(
                    optimizedResources,
                    currentHour,
                    PrototypeWeather.Clear);
                optimizedRuntime.Stop();
                exhaustiveRuntime.Start();
                PrototypeSettlementTickResult exhaustiveResult = exhaustive.Advance(
                    exhaustiveResources,
                    currentHour,
                    PrototypeWeather.Clear);
                exhaustiveRuntime.Stop();

                optimizedQueries += optimized.Diagnostics.SelectorExactPathQueries;
                exhaustiveQueries += exhaustive.Diagnostics.SelectorExactPathQueries;
                Assert.Equal(
                    exhaustive.Workers
                        .OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)
                        .Select(worker => (worker.WorkerId, worker.CurrentOrderId))
                        .ToArray(),
                    optimized.Workers
                        .OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)
                        .Select(worker => (worker.WorkerId, worker.CurrentOrderId))
                        .ToArray());
                Assert.Equal(
                    JsonSerializer.Serialize(exhaustive.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)),
                    JsonSerializer.Serialize(optimized.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)));
                Assert.Equal(exhaustiveResult.Events.ToArray(), optimizedResult.Events.ToArray());
                Assert.Equal(exhaustiveResult.HarvestRequests.ToArray(), optimizedResult.HarvestRequests.ToArray());

                ApplyHarvestRequests(exhaustive, exhaustiveResources, exhaustiveResult.HarvestRequests);
                ApplyHarvestRequests(optimized, optimizedResources, optimizedResult.HarvestRequests);
                Assert.Equal(exhaustiveResources.ToArray(), optimizedResources.ToArray());
                currentHour = AdvanceHour(currentHour);
            }

            string exhaustiveSnapshot = JsonSerializer.Serialize(exhaustive.CaptureSnapshot(300));
            string optimizedSnapshot = JsonSerializer.Serialize(optimized.CaptureSnapshot(300));
            Assert.Equal(exhaustiveSnapshot, optimizedSnapshot);
            Assert.True(optimizedQueries <= exhaustiveQueries);

            if (scenarioId == "balanced_basin")
            {
                Assert.True(
                    optimizedQueries * 100 <= exhaustiveQueries * 40,
                    $"Expected at least 60% fewer exact queries; optimized={optimizedQueries}, exhaustive={exhaustiveQueries}.");
                Assert.True(
                    optimizedQueries < 2544,
                    $"Expected topology bounds to beat the W1-05c 2,544-query baseline; optimized={optimizedQueries}.");
            }

            _output.WriteLine(
                $"{scenarioId}: optimized queries={optimizedQueries}, exhaustive queries={exhaustiveQueries}, " +
                $"reduction={(1.0 - (optimizedQueries / (double)Math.Max(1, exhaustiveQueries))):P2}, " +
                $"optimized runtime={optimizedRuntime.Elapsed.TotalSeconds:F3}s, exhaustive runtime={exhaustiveRuntime.Elapsed.TotalSeconds:F3}s");
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

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.X - b.X, a.Z - b.Z).Length();
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
