using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class SettlementDiagnosticsTests
    {
        [Fact]
        public void Diagnostics_TracksWorkOrdersGeneratedEachTick()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            PrototypeSettlementTickResult result = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);

            Assert.True(simulation.Diagnostics.WorkOrdersGenerated > 0, "Should generate work orders on the first tick");
            Assert.True(simulation.Diagnostics.CitizensEvaluated == scenario.InitialCitizens, "Should evaluate all citizens");
        }

        [Fact]
        public void Diagnostics_WorkOrdersClaimedPlusRemainingEqualsGenerated()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            int totalGenerated = 0;
            int totalClaimed = 0;
            int totalRemaining = 0;

            for (int i = 0; i < 60; i++)
            {
                _ = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);
                totalGenerated += simulation.Diagnostics.WorkOrdersGenerated;
                totalClaimed += simulation.Diagnostics.WorkOrdersClaimed;
                totalRemaining += simulation.Diagnostics.WorkOrdersRemaining;

                Assert.True(
                    simulation.Diagnostics.WorkOrdersGenerated == simulation.Diagnostics.WorkOrdersClaimed + simulation.Diagnostics.WorkOrdersRemaining,
                    $"Generated ({simulation.Diagnostics.WorkOrdersGenerated}) must equal claimed ({simulation.Diagnostics.WorkOrdersClaimed}) + remaining ({simulation.Diagnostics.WorkOrdersRemaining}) on tick {i}");
            }

            Assert.True(totalGenerated > 0);
            Assert.True(totalClaimed > 0);
        }

        [Fact]
        public void Diagnostics_PathPlanLookups_IncreaseDuringAssignment()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            PrototypePathSegmentState pathSegment = simulation.PathSegments.First(segment => !segment.IsBuilt);
            PrototypeStructureState pathStructure = simulation.Structures.Single(
                structure => structure.StructureId == pathSegment.StructureId);
            PrototypeBuildQueueEntry pathQueueEntry = simulation.BuildQueue.Single(
                entry => entry.StructureId == pathSegment.StructureId);
            pathQueueEntry.IsPaused = true;

            PrototypeWorkerState builder = simulation.Workers
                .OrderBy(worker => worker.WorkerId, StringComparer.Ordinal)
                .First();
            builder.Phase = PrototypeWorkerPhase.Building;
            builder.CurrentOrderKind = PrototypeWorkOrderKind.BuildPath;
            builder.CurrentOrderId = "diagnostic.path.build";
            builder.TargetStructureId = pathStructure.StructureId;
            builder.TargetPosition = pathStructure.Position;
            builder.TargetLabel = pathStructure.DisplayName;
            builder.PhaseDurationTicks = 1;
            builder.TicksRemaining = 1;

            var collector = new RuntimeMetricsCollector(capacity: 4, new IncrementingTimeProvider());
            long startTick = simulation.TotalTicks;
            collector.BeginBatch(RuntimeMetricsBatchKind.ManualStep, startTick);
            RuntimeMetricsPhaseToken simulationTickPhase = collector.BeginPhase(RuntimeMetricsPhase.SimulationTick);

            simulation.Advance(resources, 8.0f, PrototypeWeather.Clear, collector);
            simulationTickPhase.Complete();

            PrototypeSettlementSimulation.PrototypeSettlementDiagnosticsState diagnostics = simulation.Diagnostics;
            collector.RecordCompletedTick(
                new RuntimeTickDiagnostics(
                    diagnostics.WorkOrdersGenerated,
                    diagnostics.WorkOrdersGeneratedUncapped,
                    diagnostics.WorkOrdersClaimed,
                    diagnostics.WorkOrdersRemaining,
                    diagnostics.PathPlanLookups,
                    diagnostics.PathPlanCacheHits,
                    diagnostics.CitizensEvaluated)
                {
                    PathPlanCacheMisses = diagnostics.PathPlanCacheMisses,
                    PathPlanCacheSize = diagnostics.PathPlanCacheSize,
                    NavigationInvalidations = diagnostics.NavigationInvalidations,
                    WorkerCount = diagnostics.WorkerCount,
                    IdleCitizensConsideringWorkOrders = diagnostics.IdleCitizensConsideringWorkOrders,
                    CandidateOrdersEvaluated = diagnostics.CandidateOrdersEvaluated
                });
            collector.EndBatch(simulation.TotalTicks);

            RuntimeMetricsBatch batch = Assert.Single(collector.SnapshotBatches());

            Assert.True(pathSegment.IsBuilt, "The forced path completion should build its segment");
            Assert.True(diagnostics.PathPlanLookups > 0, "Path plan lookups should occur during citizen assignment");
            Assert.Equal(
                diagnostics.PathPlanLookups,
                diagnostics.PathPlanCacheHits + diagnostics.PathPlanCacheMisses);
            Assert.True(diagnostics.PathPlanCacheSize > 0, "The path-plan cache should retain plans after assignment");
            Assert.Equal(1, diagnostics.NavigationInvalidations);
            Assert.Equal(1, batch.NavigationInvalidationsTotal);
            Assert.Equal(diagnostics.PathPlanCacheSize, batch.PathPlanCacheSizeLast);
            Assert.True(batch.Phases.NavigationRebuildMilliseconds > 0.0, "Runtime invalidation rebuild time should be measured");
        }

        [Fact]
        public void PerformanceProbe_ClearsOnlyDerivedPathCacheAfterNaturalActivity()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            PrototypePerformanceProbeSnapshot startup = simulation.CapturePerformanceProbeState();
            Assert.Equal(0, startup.PathCacheEntryCount);
            Assert.Equal(0, startup.SimulationTick);
            Assert.True(startup.AllPathCacheKeysMatchNavigationRulesVersion);
            Assert.Equal(0, startup.TotalNavigationInvalidations);

            _ = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);
            PrototypePerformanceProbeSnapshot naturallyWarm = simulation.CapturePerformanceProbeState();
            Assert.True(naturallyWarm.PathCacheEntryCount > 0);
            Assert.Equal(1, naturallyWarm.SimulationTick);
            Assert.True(naturallyWarm.AllPathCacheKeysMatchNavigationRulesVersion);
            Assert.Equal(naturallyWarm.NavigationRulesVersion, naturallyWarm.LastPathPlanRulesVersion);

            int clearedEntryCount = simulation.ClearDerivedPathCacheForPerformance();
            PrototypePerformanceProbeSnapshot cleared = simulation.CapturePerformanceProbeState();

            Assert.Equal(naturallyWarm.PathCacheEntryCount, clearedEntryCount);
            Assert.Equal(0, cleared.PathCacheEntryCount);
            Assert.True(cleared.AllPathCacheKeysMatchNavigationRulesVersion);
            Assert.Equal(naturallyWarm.NavigationRulesVersion, cleared.NavigationRulesVersion);
            Assert.Equal(naturallyWarm.TotalNavigationInvalidations, cleared.TotalNavigationInvalidations);
            Assert.Equal(naturallyWarm.LastPathPlanRulesVersion, cleared.LastPathPlanRulesVersion);
        }

        [Fact]
        public void PerformanceProbe_ForcedCompletionMeasuresExactPostChangeLookup()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            _ = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);
            PrototypePerformanceProbeSnapshot naturallyWarm = simulation.CapturePerformanceProbeState();
            Assert.True(naturallyWarm.PathCacheEntryCount > 0);

            Assert.True(simulation.TryPrepareForcedPathCompletionForPerformance(out string structureId));
            PrototypePerformanceProbeSnapshot prepared = simulation.CapturePerformanceProbeState();
            PrototypeForcedInvalidationProbeSnapshot preparedForced = prepared.ForcedInvalidation;
            Assert.True(preparedForced.Prepared);
            Assert.False(preparedForced.Committed);
            Assert.Equal(structureId, preparedForced.PathSegmentStructureId);
            Assert.False(preparedForced.PathSegmentWasBuiltBefore);
            Assert.False(preparedForced.PathSegmentIsBuiltAfter);
            Assert.Equal(prepared.NavigationRulesVersion, preparedForced.PreChangeQueryVersion);
            Assert.Equal(prepared.NavigationRulesVersion, preparedForced.PreChangePlanVersion);
            Assert.NotNull(preparedForced.PreChangePlanCost);
            Assert.True(prepared.PathCacheEntryCount > 0, "Preparation should retain the exact pre-change query in the cache");

            PrototypeSettlementTickResult result = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);
            PrototypePerformanceProbeSnapshot completed = simulation.CapturePerformanceProbeState();
            PrototypeForcedInvalidationProbeSnapshot forced = completed.ForcedInvalidation;

            Assert.True(forced.Prepared);
            Assert.True(forced.Committed);
            Assert.Equal(structureId, forced.PathSegmentStructureId);
            Assert.False(forced.PathSegmentWasBuiltBefore);
            Assert.True(forced.PathSegmentIsBuiltAfter);
            Assert.Equal(naturallyWarm.NavigationRulesVersion, forced.VersionBeforeCommit);
            Assert.Equal(naturallyWarm.NavigationRulesVersion + 1, forced.VersionAfterCommit);
            Assert.Equal(naturallyWarm.TotalNavigationInvalidations, forced.TotalInvalidationsBeforeCommit);
            Assert.Equal(naturallyWarm.TotalNavigationInvalidations + 1, forced.TotalInvalidationsAfterCommit);
            Assert.Equal(naturallyWarm.TotalNavigationInvalidations + 1, completed.TotalNavigationInvalidations);
            Assert.Equal(forced.VersionAfterCommit, completed.NavigationRulesVersion);
            Assert.Equal(forced.VersionAfterCommit, completed.LastPathPlanRulesVersion);
            Assert.True(completed.AllPathCacheKeysMatchNavigationRulesVersion);
            Assert.True(forced.CacheEntriesBeforeRebuild > 0);
            Assert.Equal(0, forced.CacheEntriesImmediatelyAfterRebuild);
            Assert.Equal(simulation.TotalTicks, forced.CompletionTick);
            Assert.True(forced.FirstPostChangeLookupObserved);
            Assert.True(forced.FirstPostChangeLookupWasCacheMiss);
            Assert.True(forced.FirstPostChangeLookupUsedNewVersion);
            Assert.Equal(forced.VersionAfterCommit, forced.PostChangeQueryVersion);
            Assert.Equal(forced.VersionAfterCommit, forced.PostChangePlanVersion);
            Assert.True(forced.ExactEndpointsMatch);
            Assert.True(forced.ChangedCellIncludedInPostChangePlan);
            Assert.NotNull(forced.PreChangePlanCost);
            Assert.NotNull(forced.PostChangePlanCost);
            Assert.True(forced.PostChangePlanCost!.Value < forced.PreChangePlanCost!.Value);
            Assert.True(forced.CommitToFirstLookupMilliseconds >= 0.0);

            Assert.True(simulation.PathSegments.Single(segment => segment.StructureId == structureId).IsBuilt);
            Assert.Contains(result.Events, entry => entry.EventType == PrototypeEventTypes.SettlementBuildCompleted);
            Assert.Contains(result.Events, entry => entry.EventType == PrototypeEventTypes.SettlementPathBuilt);
        }

        [Fact]
        public void Diagnostics_PeakOrdersTracksMaximumAcrossSession()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            for (int i = 0; i < 120; i++)
            {
                _ = simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);

                Assert.True(
                    simulation.Diagnostics.PeakOrdersThisSession >= simulation.Diagnostics.WorkOrdersGenerated,
                    "Peak orders must always be >= current orders");
            }

            Assert.True(simulation.Diagnostics.PeakOrdersThisSession > 0, "Should have observed at least some orders");
            Assert.True(
                simulation.Diagnostics.PeakOrdersThisSession >= simulation.Diagnostics.WorkOrdersGenerated,
                "Peak should still be >= current tick's orders");
        }

        [Fact]
        public void Diagnostics_CitizensEvaluatedMatchesWorkerCount()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
            List<PrototypeResourceSiteState> resources = BuildResourceSites(world);

            simulation.Advance(resources, 8.0f, PrototypeWeather.Clear);

            Assert.True(
                simulation.Workers.Count == simulation.Diagnostics.CitizensEvaluated,
                $"Expected {simulation.Workers.Count} citizens evaluated, got {simulation.Diagnostics.CitizensEvaluated}");
            Assert.Equal(simulation.Workers.Count, simulation.Diagnostics.WorkerCount);
            Assert.InRange(
                simulation.Diagnostics.IdleCitizensConsideringWorkOrders,
                0,
                simulation.Diagnostics.WorkerCount);
            Assert.True(
                simulation.Diagnostics.IdleCitizensConsideringWorkOrders > 0,
                "At least one idle citizen should reach generic work-order scoring on the first tick");
            int expectedCandidateOrdersEvaluated =
                simulation.Diagnostics.WorkOrdersClaimed *
                ((2 * simulation.Diagnostics.WorkOrdersGenerated) - simulation.Diagnostics.WorkOrdersClaimed + 1) /
                2;
            Assert.True(
                simulation.Diagnostics.CandidateOrdersEvaluated >= expectedCandidateOrdersEvaluated,
                "Reachability filtering may leave the same candidate available for later citizens, but every claimed-order scan must still be counted.");
            Assert.InRange(
                simulation.Diagnostics.UnreachableWorkOrderCandidatesSkipped,
                0,
                simulation.Diagnostics.CandidateOrdersEvaluated);
        }

        [Fact]
        public void UnreachableOnlyCitizen_RemainsIdleWithStableNavigationDiagnostic()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            WorldGenerationResult world = PrototypeWorldGenerator.Generate(scenario);
            PrototypeSettlementSimulation simulation = New(scenario, bundle.RoleQuotas.Roles, world);
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
            Assert.True(simulation.Diagnostics.UnreachableWorkOrderCandidatesSkipped > 0);
        }

        private static PrototypeSettlementSimulation New(
            PrototypeScenarioDefinition scenario,
            IReadOnlyList<PrototypeRoleQuotaDefinition> roleQuotas,
            WorldGenerationResult world)
        {
            return new(scenario, roleQuotas, world);
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

        private sealed class IncrementingTimeProvider : TimeProvider
        {
            private long _timestamp;

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp()
            {
                _timestamp += TimeSpan.TicksPerMillisecond;
                return _timestamp;
            }
        }
    }
}
