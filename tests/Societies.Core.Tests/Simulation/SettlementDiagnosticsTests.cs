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
            Assert.Equal(expectedCandidateOrdersEvaluated, simulation.Diagnostics.CandidateOrdersEvaluated);
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
