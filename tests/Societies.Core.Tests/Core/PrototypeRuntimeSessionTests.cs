using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeRuntimeSessionTests
    {
        [Fact]
        public void RouteDistanceMode_FreshRuntimeExecutesConfiguredImplementation()
        {
            PrototypeCatalogBundle optimizedBundle = LoadCatalogs();
            PrototypeCatalogBundle referenceBundle = LoadCatalogs();
            PrototypeScenarioDefinition optimizedScenario = optimizedBundle.Scenarios.Resolve("balanced_basin");
            PrototypeScenarioDefinition referenceScenario = referenceBundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession optimized = new(
                optimizedScenario,
                optimizedBundle.RoleQuotas.Roles,
                routeDistanceMode: PrototypeRouteDistanceMode.CachedDistanceOnly);
            PrototypeRuntimeSession reference = new(
                referenceScenario,
                referenceBundle.RoleQuotas.Roles,
                routeDistanceMode: PrototypeRouteDistanceMode.FullMaterializationReference);
            optimized.Initialize(8.0f);
            reference.Initialize(8.0f);

            AdvanceTwice(optimized, BuildResourceSites(optimized.World!));
            AdvanceTwice(reference, BuildResourceSites(reference.World!));

            Assert.Equal(PrototypeRouteDistanceMode.CachedDistanceOnly, optimized.RouteDistanceMode);
            Assert.Equal(PrototypeRouteDistanceMode.FullMaterializationReference, reference.RouteDistanceMode);
            Assert.True(optimized.CachedRouteDistanceFastPathHits > 0);
            Assert.Equal(0, reference.CachedRouteDistanceFastPathHits);
        }

        [Fact]
        public void RouteDistanceMode_SnapshotRestoreExecutesConfiguredImplementation()
        {
            PrototypeCatalogBundle sourceBundle = LoadCatalogs();
            PrototypeScenarioDefinition sourceScenario = sourceBundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession source = new(sourceScenario, sourceBundle.RoleQuotas.Roles);
            source.Initialize(8.0f);
            List<PrototypeResourceSiteState> sourceResources = BuildResourceSites(source.World!);
            AdvanceTwice(source, sourceResources);
            PrototypeRuntimeSnapshot snapshot = source.CaptureSnapshot(
                Vector3.Zero,
                sourceResources.Select(resource => new PrototypeResourceSnapshot
                {
                    ResourceId = resource.ResourceId,
                    UnitsRemaining = resource.UnitsRemaining,
                    Position = PrototypeSerializableVector3.FromVector3(resource.Position),
                    ClusterId = resource.ClusterId
                }).ToList());

            PrototypeCatalogBundle optimizedBundle = LoadCatalogs();
            PrototypeCatalogBundle referenceBundle = LoadCatalogs();
            PrototypeRuntimeSession optimized = new(
                optimizedBundle.Scenarios.Resolve("balanced_basin"),
                optimizedBundle.RoleQuotas.Roles,
                routeDistanceMode: PrototypeRouteDistanceMode.CachedDistanceOnly);
            PrototypeRuntimeSession reference = new(
                referenceBundle.Scenarios.Resolve("balanced_basin"),
                referenceBundle.RoleQuotas.Roles,
                routeDistanceMode: PrototypeRouteDistanceMode.FullMaterializationReference);
            optimized.ApplySnapshot(snapshot);
            reference.ApplySnapshot(snapshot);

            AdvanceTwice(optimized, BuildResourceSites(optimized.World!));
            AdvanceTwice(reference, BuildResourceSites(reference.World!));

            Assert.True(optimized.CachedRouteDistanceFastPathHits > 0);
            Assert.Equal(0, reference.CachedRouteDistanceFastPathHits);
        }

        [Fact]
        public void PerformanceProbe_RuntimeSessionForwardsCacheAndForcedInvalidationControls()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession session = new(scenario, bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(session.World!);

            PrototypePerformanceProbeSnapshot startup = session.CapturePerformanceProbeState();
            Assert.Equal(0, startup.SimulationTick);
            Assert.Equal(0, startup.PathCacheEntryCount);

            _ = session.Advance(1.0f / 20.0f, 600.0f, resources);
            PrototypePerformanceProbeSnapshot naturallyWarm = session.CapturePerformanceProbeState();
            Assert.Equal(1, naturallyWarm.SimulationTick);
            Assert.True(naturallyWarm.PathCacheEntryCount > 0);

            Assert.Equal(naturallyWarm.PathCacheEntryCount, session.ClearDerivedPathCacheForPerformance());
            Assert.Equal(0, session.CapturePerformanceProbeState().PathCacheEntryCount);
            Assert.True(session.TryPrepareForcedPathCompletionForPerformance(out string structureId));

            PrototypeRuntimeTickResult result = session.Advance(1.0f / 20.0f, 600.0f, resources);
            PrototypePerformanceProbeSnapshot completed = session.CapturePerformanceProbeState();
            Assert.Equal(2, completed.SimulationTick);
            Assert.True(completed.ForcedInvalidation.Prepared);
            Assert.True(completed.ForcedInvalidation.Committed);
            Assert.Equal(structureId, completed.ForcedInvalidation.PathSegmentStructureId);
            Assert.True(completed.ForcedInvalidation.PathSegmentIsBuiltAfter);
            Assert.True(completed.ForcedInvalidation.FirstPostChangeLookupUsedNewVersion);
            Assert.Contains(result.SettlementResult.Events, entry => entry.EventType == PrototypeEventTypes.SettlementPathBuilt);
        }

        [Fact]
        public void BalancedBasin_ReachesEconomyStateWithMealsFuelAndBeds()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession session = new(scenario, bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);

            List<PrototypeResourceSiteState> resources = session.World!.ResourceSpawns
                .GroupBy(spawn => spawn.ResourceId)
                .OrderBy(group => group.Key)
                .SelectMany(group => group.Select((spawn, index) => new PrototypeResourceSiteState(
                    $"{spawn.ResourceId}_{index + 1}",
                    spawn.ResourceId,
                    spawn.Position,
                    spawn.UnitsRemaining,
                    spawn.ClusterId)))
                .ToList();

            for (int tick = 0; tick < 1400; tick++)
            {
                PrototypeRuntimeTickResult result = session.Advance(1.0f / 20.0f, 600.0f, resources);
                foreach (PrototypeHarvestRequest request in result.SettlementResult.HarvestRequests)
                {
                    int index = resources.FindIndex(site => site.NodeName == request.TargetNodeName);
                    Assert.True(index >= 0, $"Missing resource node {request.TargetNodeName}");

                    PrototypeResourceSiteState site = resources[index];
                    if (site.UnitsRemaining < request.Amount)
                    {
                        session.OnHarvestFailed(request.WorkerId, request.WorkerDisplayName, request.ResourceId);
                        continue;
                    }

                    resources[index] = site with
                    {
                        UnitsRemaining = site.UnitsRemaining - request.Amount
                    };
                    session.RecordAiHarvestSucceeded(request.WorkerDisplayName, request.ResourceId, request.Amount);
                }

                session.RecordSettlementEvents(result.SettlementResult.Events);
            }

            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero, resources.Select(resource => new PrototypeResourceSnapshot
            {
                ResourceId = resource.ResourceId,
                UnitsRemaining = resource.UnitsRemaining,
                Position = PrototypeSerializableVector3.FromVector3(resource.Position),
                ClusterId = resource.ClusterId
            }).ToList());

            Assert.True(
                session.Stockpile.GetCount("meals") > 0 ||
                session.Stockpile.GetCount("berries") > 0 ||
                snapshot.Settlement!.ProducedResources.GetValueOrDefault("meals") > 0);
            Assert.True(
                session.Stockpile.GetCount("firewood") > 0 ||
                session.Stockpile.GetCount("hearth_fuel") > 0 ||
                snapshot.Settlement!.ProducedResources.GetValueOrDefault("firewood") > 0 ||
                snapshot.Settlement.HearthLitTicks > 0);
            Assert.True(session.Stockpile.GetCount("beds") >= 2 || snapshot.Settlement!.Structures.Any(structure => structure.StructureKindId == "hut" && structure.IsBuilt));
            Assert.NotNull(snapshot.Settlement);
            Assert.True(snapshot.Settlement!.Citizens.Count == session.Workers.Count);
            Assert.True(snapshot.Settlement.PathSegments.Count > 0);
            Assert.True(snapshot.Settlement.LogisticsMetrics.TotalCompletedRouteDistanceMeters >= 0.0f);
        }

        private static List<PrototypeResourceSiteState> BuildResourceSites(WorldGenerationResult world)
        {
            return world.ResourceSpawns
                .GroupBy(spawn => spawn.ResourceId)
                .OrderBy(group => group.Key)
                .SelectMany(group => group.Select((spawn, index) => new PrototypeResourceSiteState(
                    $"{spawn.ResourceId}_{index + 1}",
                    spawn.ResourceId,
                    spawn.Position,
                    spawn.UnitsRemaining,
                    spawn.ClusterId)))
                .ToList();
        }

        private static void AdvanceTwice(
            PrototypeRuntimeSession session,
            IReadOnlyList<PrototypeResourceSiteState> resources)
        {
            _ = session.Advance(1.0f / 20.0f, 600.0f, resources);
            _ = session.Advance(1.0f / 20.0f, 600.0f, resources);
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            return PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());
        }

        private static string GetCatalogDirectoryPath()
        {
            string baseDirectory = AppContext.BaseDirectory;
            string? current = baseDirectory;

            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                DirectoryInfo? parent = Directory.GetParent(current);
                current = parent?.FullName;
            }

            throw new DirectoryNotFoundException($"Could not find src/societies/data from '{baseDirectory}'.");
        }
    }
}
