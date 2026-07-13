using Godot;
using Societies.Core;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class RouteDistanceModeDifferentialTests
    {
        private static readonly JsonSerializerOptions FieldJsonOptions = new() { IncludeFields = true };

        [Theory]
        [InlineData("balanced_basin")]
        [InlineData("long_haul_quarry")]
        [InlineData("food_poor_highlands")]
        [InlineData("wetland_builder")]
        public void CachedDistanceOnly_MatchesFullMaterializationReferenceForThreeHundredTicks(string scenarioId)
        {
            PrototypeCatalogBundle optimizedBundle = LoadCatalogs();
            PrototypeCatalogBundle referenceBundle = LoadCatalogs();
            PrototypeScenarioDefinition optimizedScenario = optimizedBundle.Scenarios.Resolve(scenarioId);
            PrototypeScenarioDefinition referenceScenario = referenceBundle.Scenarios.Resolve(scenarioId);
            if (scenarioId == "balanced_basin")
            {
                optimizedScenario.InitialCitizens = 16;
                referenceScenario.InitialCitizens = 16;
            }

            WorldGenerationResult optimizedWorld = PrototypeWorldGenerator.Generate(optimizedScenario);
            WorldGenerationResult referenceWorld = PrototypeWorldGenerator.Generate(referenceScenario);
            PrototypeSettlementSimulation optimized = new(
                optimizedScenario,
                optimizedBundle.RoleQuotas.Roles,
                optimizedWorld,
                routeDistanceMode: PrototypeRouteDistanceMode.CachedDistanceOnly);
            PrototypeSettlementSimulation reference = new(
                referenceScenario,
                referenceBundle.RoleQuotas.Roles,
                referenceWorld,
                routeDistanceMode: PrototypeRouteDistanceMode.FullMaterializationReference);
            List<PrototypeResourceSiteState> optimizedResources = BuildResourceSites(optimizedWorld);
            List<PrototypeResourceSiteState> referenceResources = BuildResourceSites(referenceWorld);
            float currentHour = 8.0f;

            for (int tick = 1; tick <= 300; tick++)
            {
                PrototypeSettlementTickResult optimizedResult = optimized.Advance(
                    optimizedResources,
                    currentHour,
                    PrototypeWeather.Clear);
                PrototypeSettlementTickResult referenceResult = reference.Advance(
                    referenceResources,
                    currentHour,
                    PrototypeWeather.Clear);

                Assert.Equal(
                    JsonSerializer.Serialize(reference.Diagnostics, FieldJsonOptions),
                    JsonSerializer.Serialize(optimized.Diagnostics, FieldJsonOptions));
                Assert.Equal(
                    JsonSerializer.Serialize(reference.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)),
                    JsonSerializer.Serialize(optimized.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)));
                Assert.Equal(CaptureWorkerRouteBits(reference.Workers), CaptureWorkerRouteBits(optimized.Workers));
                Assert.Equal(reference.CapturePerformanceProbeState(), optimized.CapturePerformanceProbeState());
                Assert.Equal(referenceResult.Events.ToArray(), optimizedResult.Events.ToArray());
                Assert.Equal(referenceResult.HarvestRequests.ToArray(), optimizedResult.HarvestRequests.ToArray());

                ApplyHarvestRequests(reference, referenceResources, referenceResult.HarvestRequests);
                ApplyHarvestRequests(optimized, optimizedResources, optimizedResult.HarvestRequests);
                Assert.Equal(referenceResources.ToArray(), optimizedResources.ToArray());
                currentHour = AdvanceHour(currentHour);
            }

            Assert.Equal(
                JsonSerializer.Serialize(reference.CaptureSnapshot(300)),
                JsonSerializer.Serialize(optimized.CaptureSnapshot(300)));
            Assert.True(
                optimized.CachedRouteDistanceFastPathHits > 0,
                $"Expected cached distance replay to be exercised for {scenarioId}.");
            Assert.Equal(0, reference.CachedRouteDistanceFastPathHits);
        }

        private static string CaptureWorkerRouteBits(IReadOnlyList<PrototypeWorkerState> workers)
        {
            WorkerRouteBits[] routes = workers
                .OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)
                .Select(worker => new WorkerRouteBits(
                    worker.WorkerId,
                    Bits(worker.Position.X),
                    Bits(worker.Position.Y),
                    Bits(worker.Position.Z),
                    Bits(worker.TargetPosition.X),
                    Bits(worker.TargetPosition.Y),
                    Bits(worker.TargetPosition.Z),
                    worker.Navigation.CurrentWaypointIndex,
                    Bits(worker.Navigation.CurrentRouteLengthMeters),
                    Bits(worker.Navigation.CurrentRouteCost),
                    worker.Navigation.CurrentRouteTravelTicks,
                    worker.Navigation.CachedRouteVersion,
                    worker.Navigation.SourceGridX,
                    worker.Navigation.SourceGridY,
                    worker.Navigation.DestinationGridX,
                    worker.Navigation.DestinationGridY,
                    worker.Navigation.RouteWaypoints
                        .Select(waypoint => new WaypointBits(Bits(waypoint.X), Bits(waypoint.Y), Bits(waypoint.Z)))
                        .ToArray()))
                .ToArray();
            return JsonSerializer.Serialize(routes);
        }

        private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

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

        private sealed record WorkerRouteBits(
            string WorkerId,
            int PositionX,
            int PositionY,
            int PositionZ,
            int TargetX,
            int TargetY,
            int TargetZ,
            int CurrentWaypointIndex,
            int CurrentRouteLength,
            int CurrentRouteCost,
            int CurrentRouteTravelTicks,
            int CachedRouteVersion,
            int SourceGridX,
            int SourceGridY,
            int DestinationGridX,
            int DestinationGridY,
            WaypointBits[] Waypoints);

        private sealed record WaypointBits(int X, int Y, int Z);
    }
}
