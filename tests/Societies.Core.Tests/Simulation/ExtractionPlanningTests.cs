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
                Assert.Equal(
                    exhaustive.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal).Select(worker => (worker.WorkerId, worker.CurrentOrderId)),
                    optimized.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal).Select(worker => (worker.WorkerId, worker.CurrentOrderId)));
                Assert.Equal(exhaustiveResult.Events, optimizedResult.Events);
                Assert.Equal(exhaustiveResult.HarvestRequests, optimizedResult.HarvestRequests);

                ApplyHarvestRequests(exhaustive, exhaustiveResources, exhaustiveResult.HarvestRequests);
                ApplyHarvestRequests(optimized, optimizedResources, optimizedResult.HarvestRequests);
                Assert.Equal(exhaustiveResources, optimizedResources);
                currentHour = AdvanceHour(currentHour);
            }

            Assert.Equal(
                JsonSerializer.Serialize(exhaustive.CaptureSnapshot(300)),
                JsonSerializer.Serialize(optimized.CaptureSnapshot(300)));
            Assert.True(
                optimizedQueries < exhaustiveQueries,
                $"Expected extraction planning to reduce queries; optimized={optimizedQueries}, exhaustive={exhaustiveQueries}.");
            _output.WriteLine(
                $"{scenarioId}: optimized queries={optimizedQueries}, exhaustive queries={exhaustiveQueries}, " +
                $"reduction={(1.0 - optimizedQueries / (double)exhaustiveQueries):P2}");
        }

        private static PrototypeExtractionCandidate Candidate(string nodeName, float lowerBound, int originalIndex)
        {
            PrototypeResourceSiteState site = new(nodeName, "logs", Vector3.Zero, 1, "cluster");
            return new PrototypeExtractionCandidate(site, Vector3.Zero, lowerBound, originalIndex);
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
