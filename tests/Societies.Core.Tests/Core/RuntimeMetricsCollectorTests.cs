using Societies.Core;
using System.Globalization;
using Xunit;

namespace Societies.Core.Tests
{
    public class RuntimeMetricsCollectorTests
    {
        [Fact]
        public void Capacity_RetainsNewestBatchesInChronologicalOrder()
        {
            var clock = new ManualTimeProvider();
            var collector = new RuntimeMetricsCollector(capacity: 2, clock);

            for (int tick = 0; tick < 4; tick++)
            {
                collector.BeginBatch(RuntimeMetricsBatchKind.RenderedFrame, tick);
                RuntimeMetricsPhaseToken tickPhase = collector.BeginPhase(RuntimeMetricsPhase.SimulationTick);
                clock.AdvanceMilliseconds(1);
                tickPhase.Complete();
                collector.RecordCompletedTick(default);
                collector.EndBatch(tick + 1);
            }

            RuntimeMetricsBatch[] batches = collector.SnapshotBatches();
            Assert.Equal(2, collector.Count);
            Assert.Equal(2, collector.DroppedBatchCount);
            Assert.Equal(new long[] { 2, 3 }, batches.Select(batch => batch.Sequence));
            Assert.Equal(new long[] { 2, 3 }, batches.Select(batch => batch.StartSimulationTick));
        }

        [Fact]
        public void Reset_ClearsStateAndInvalidatesOpenTokens()
        {
            var clock = new ManualTimeProvider();
            var collector = new RuntimeMetricsCollector(capacity: 2, clock);
            collector.BeginBatch(RuntimeMetricsBatchKind.RenderedFrame, 10);
            RuntimeMetricsPhaseToken tick = collector.BeginPhase(RuntimeMetricsPhase.SimulationTick);
            RuntimeMetricsPhaseToken navigation = collector.BeginPhase(RuntimeMetricsPhase.NavigationRebuild);
            clock.AdvanceMilliseconds(2);
            navigation.Complete();
            tick.Complete();
            collector.RecordCompletedTick(
                new RuntimeTickDiagnostics(0, 0, 0, 0, 0, 0, 0)
                {
                    PathPlanCacheMisses = 5,
                    PathPlanCacheSize = 10,
                    NavigationInvalidations = 3,
                    WorkerCount = 4,
                    IdleCitizensConsideringWorkOrders = 2,
                    CandidateOrdersEvaluated = 12,
                    SelectorCandidatesBounded = 12,
                    SelectorCandidatesExactScored = 4,
                    SelectorCandidatesPruned = 8,
                    SelectorExactPathQueries = 6,
                    SelectorPathCacheHits = 4,
                    SelectorPathCacheMisses = 2,
                    SelectorSelectedRouteReuses = 1
                });
            RuntimeMetricsPhaseToken staleToken = collector.BeginPhase(RuntimeMetricsPhase.SessionAdvance);
            clock.AdvanceMilliseconds(5);

            collector.Reset();
            Assert.Equal(0.0, staleToken.Complete());

            collector.BeginBatch(RuntimeMetricsBatchKind.ManualStep, 0);
            clock.AdvanceMilliseconds(1);
            collector.EndBatch(0);

            RuntimeMetricsBatch batch = Assert.Single(collector.SnapshotBatches());
            Assert.Equal(0, batch.Sequence);
            Assert.Equal(0, collector.DroppedBatchCount);
            Assert.Equal(0.0, batch.Phases.SessionAdvanceMilliseconds);
            Assert.Equal(0.0, batch.Phases.NavigationRebuildMilliseconds);
            Assert.Equal(0.0, batch.Phases.RouteSelectionMilliseconds);
            Assert.Equal(0, batch.PathPlanCacheMissesTotal);
            Assert.Null(batch.PathPlanCacheSizeLast);
            Assert.Equal(0, batch.NavigationInvalidationsTotal);
            Assert.Null(batch.WorkerCountLast);
            Assert.Equal(0, batch.IdleCitizensConsideringWorkOrdersTotal);
            Assert.Equal(0, batch.CandidateOrdersEvaluatedTotal);
            Assert.Null(batch.CandidateOrdersPerIdleCitizen);
            Assert.Equal(0, batch.SelectorExactPathQueriesTotal);
            Assert.Equal(0, batch.SelectorSelectedRouteReusesTotal);
        }

        [Fact]
        public void NestedPhases_AccumulateIndependentInclusiveDurations()
        {
            var clock = new ManualTimeProvider();
            var collector = new RuntimeMetricsCollector(clock: clock);
            collector.BeginBatch(RuntimeMetricsBatchKind.RenderedFrame, 0);

            RuntimeMetricsPhaseToken outer = collector.BeginPhase(RuntimeMetricsPhase.SimulationTick);
            clock.AdvanceMilliseconds(1);
            RuntimeMetricsPhaseToken inner = collector.BeginPhase(RuntimeMetricsPhase.SessionAdvance);
            RuntimeMetricsPhaseToken copiedInner = inner;
            RuntimeMetricsPhaseToken navigation = collector.BeginPhase(RuntimeMetricsPhase.NavigationRebuild);
            RuntimeMetricsPhaseToken selection = collector.BeginPhase(RuntimeMetricsPhase.RouteSelection);
            clock.AdvanceMilliseconds(2);
            Assert.Equal(2.0, navigation.Complete());
            Assert.Equal(2.0, selection.Complete());
            Assert.Equal(2.0, inner.Complete());
            Assert.Equal(0.0, copiedInner.Complete());
            clock.AdvanceMilliseconds(1);
            Assert.Equal(4.0, outer.Complete());
            collector.RecordCompletedTick(default);
            collector.EndBatch(1);

            RuntimeMetricsBatch batch = Assert.Single(collector.SnapshotBatches());
            Assert.Equal(4.0, batch.WallMilliseconds);
            Assert.Equal(4.0, batch.Phases.SimulationTickMilliseconds);
            Assert.Equal(2.0, batch.Phases.SessionAdvanceMilliseconds);
            Assert.Equal(2.0, batch.Phases.NavigationRebuildMilliseconds);
            Assert.Equal(2.0, batch.Phases.RouteSelectionMilliseconds);
        }

        [Fact]
        public void MultipleTicks_AccumulateCountersAndKeepLastGaugeAndMaximum()
        {
            var clock = new ManualTimeProvider();
            var collector = new RuntimeMetricsCollector(clock: clock);
            collector.BeginBatch(RuntimeMetricsBatchKind.ManualStep, 20);

            RuntimeMetricsPhaseToken firstTickPhase = collector.BeginPhase(RuntimeMetricsPhase.SimulationTick);
            clock.AdvanceMilliseconds(1);
            firstTickPhase.Complete();
            collector.RecordCompletedTick(
                new RuntimeTickDiagnostics(10, 14, 2, 8, 100, 80, 4)
                {
                    PathPlanCacheMisses = 20,
                    PathPlanCacheSize = 7,
                    NavigationInvalidations = 2,
                    WorkerCount = 3,
                    IdleCitizensConsideringWorkOrders = 4,
                    CandidateOrdersEvaluated = 40,
                    SelectorCandidatesBounded = 40,
                    SelectorCandidatesExactScored = 10,
                    SelectorCandidatesPruned = 30,
                    SelectorExactPathQueries = 30,
                    SelectorPathCacheHits = 20,
                    SelectorPathCacheMisses = 10,
                    SelectorSelectedRouteReuses = 2
                });
            RuntimeMetricsPhaseToken secondTickPhase = collector.BeginPhase(RuntimeMetricsPhase.SimulationTick);
            clock.AdvanceMilliseconds(2);
            secondTickPhase.Complete();
            collector.RecordCompletedTick(
                new RuntimeTickDiagnostics(20, 27, 3, 5, 150, 140, 6)
                {
                    PathPlanCacheMisses = 10,
                    PathPlanCacheSize = 9,
                    NavigationInvalidations = 1,
                    WorkerCount = 5,
                    IdleCitizensConsideringWorkOrders = 6,
                    CandidateOrdersEvaluated = 90,
                    SelectorCandidatesBounded = 90,
                    SelectorCandidatesExactScored = 20,
                    SelectorCandidatesPruned = 70,
                    SelectorExactPathQueries = 50,
                    SelectorPathCacheHits = 35,
                    SelectorPathCacheMisses = 15,
                    SelectorSelectedRouteReuses = 3
                });
            collector.EndBatch(22);

            RuntimeMetricsBatch batch = Assert.Single(collector.SnapshotBatches());
            Assert.Equal(RuntimeMetricsBatchKind.ManualStep, batch.Kind);
            Assert.Equal(2, batch.CompletedTicks);
            Assert.Equal(2.0, batch.MaximumTickMilliseconds);
            Assert.Equal(3.0, batch.Phases.SimulationTickMilliseconds);
            Assert.Equal(30, batch.WorkOrdersGeneratedTotal);
            Assert.Equal(41, batch.WorkOrdersGeneratedUncappedTotal);
            Assert.Equal(5, batch.WorkOrdersClaimedTotal);
            Assert.Equal(5, batch.WorkOrdersRemainingLast);
            Assert.Equal(250, batch.PathPlanLookupsTotal);
            Assert.Equal(220, batch.PathPlanCacheHitsTotal);
            Assert.Equal(10, batch.CitizensEvaluatedTotal);
            Assert.Equal(30, batch.PathPlanCacheMissesTotal);
            Assert.Equal(9, batch.PathPlanCacheSizeLast);
            Assert.Equal(3, batch.NavigationInvalidationsTotal);
            Assert.Equal(5, batch.WorkerCountLast);
            Assert.Equal(10, batch.IdleCitizensConsideringWorkOrdersTotal);
            Assert.Equal(130, batch.CandidateOrdersEvaluatedTotal);
            Assert.Equal(13.0, batch.CandidateOrdersPerIdleCitizen);
            Assert.Equal(130, batch.SelectorCandidatesBoundedTotal);
            Assert.Equal(30, batch.SelectorCandidatesExactScoredTotal);
            Assert.Equal(100, batch.SelectorCandidatesPrunedTotal);
            Assert.Equal(80, batch.SelectorExactPathQueriesTotal);
            Assert.Equal(55, batch.SelectorPathCacheHitsTotal);
            Assert.Equal(25, batch.SelectorPathCacheMissesTotal);
            Assert.Equal(5, batch.SelectorSelectedRouteReusesTotal);
        }

        [Fact]
        public void AbortBatch_DiscardsPartialStateAndInvalidatesTokens()
        {
            var clock = new ManualTimeProvider();
            var collector = new RuntimeMetricsCollector(clock: clock);
            collector.BeginBatch(RuntimeMetricsBatchKind.RenderedFrame, 5);
            RuntimeMetricsPhaseToken token = collector.BeginPhase(RuntimeMetricsPhase.UpdateHud);
            clock.AdvanceMilliseconds(3);
            Assert.Throws<InvalidOperationException>(() => collector.RecordCompletedTick(default));

            Assert.Throws<InvalidOperationException>(() => collector.EndBatch(5));
            collector.AbortBatch();
            Assert.Equal(0.0, token.Complete());
            Assert.Empty(collector.SnapshotBatches());

            collector.BeginBatch(RuntimeMetricsBatchKind.RenderedFrame, 5);
            collector.EndBatch(5);
            Assert.Single(collector.SnapshotBatches());
        }

        [Fact]
        public void Csv_IsStableChronologicalAndInvariantAcrossCultures()
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo french = CultureInfo.GetCultureInfo("fr-FR");
                CultureInfo.CurrentCulture = french;
                CultureInfo.CurrentUICulture = french;

                var clock = new ManualTimeProvider();
                var collector = new RuntimeMetricsCollector(clock: clock);
                collector.BeginBatch(RuntimeMetricsBatchKind.RenderedFrame, 0);
                clock.AdvanceMilliseconds(0.5);
                collector.EndBatch(0);

                collector.BeginBatch(RuntimeMetricsBatchKind.RenderedFrame, 0);
                RuntimeMetricsPhaseToken tickPhase = collector.BeginPhase(RuntimeMetricsPhase.SimulationTick);
                RuntimeMetricsPhaseToken navigation = collector.BeginPhase(RuntimeMetricsPhase.NavigationRebuild);
                clock.AdvanceMilliseconds(0.25);
                navigation.Complete();
                clock.AdvanceMilliseconds(1.25);
                tickPhase.Complete();
                collector.RecordCompletedTick(
                    new RuntimeTickDiagnostics(0, 0, 0, 0, 0, 0, 0)
                    {
                        PathPlanCacheMisses = 2,
                        PathPlanCacheSize = 7,
                        NavigationInvalidations = 1,
                        WorkerCount = 4,
                        IdleCitizensConsideringWorkOrders = 2,
                        CandidateOrdersEvaluated = 3,
                        SelectorCandidatesBounded = 3,
                        SelectorCandidatesExactScored = 2,
                        SelectorCandidatesPruned = 1,
                        SelectorExactPathQueries = 4,
                        SelectorPathCacheHits = 3,
                        SelectorPathCacheMisses = 1,
                        SelectorSelectedRouteReuses = 1
                    });
                collector.EndBatch(1);

                using var writer = new StringWriter();
                collector.WriteCsv(writer);
                string[] lines = writer.ToString().ReplaceLineEndings("\n")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries);

                Assert.Equal(3, lines.Length);
                Assert.Equal(36, lines[0].Split(',').Length);
                Assert.Equal(36, lines[1].Split(',').Length);
                Assert.Equal(string.Empty, lines[1].Split(',')[16]);
                Assert.Equal(string.Empty, lines[1].Split(',')[21]);
                Assert.Equal(string.Empty, lines[1].Split(',')[23]);
                Assert.Equal(string.Empty, lines[1].Split(',')[26]);
                Assert.Equal(36, lines[2].Split(',').Length);
                Assert.Equal("path_plan_cache_misses_total", lines[0].Split(',')[20]);
                Assert.Equal("navigation_rebuild_ms", lines[0].Split(',')[27]);
                Assert.Equal("route_selection_ms", lines[0].Split(',')[28]);
                Assert.Equal("selector_exact_path_queries_total", lines[0].Split(',')[32]);
                Assert.Equal("selector_selected_route_reuses_total", lines[0].Split(',')[35]);
                Assert.Equal("1.5", lines[2].Split(',')[26]);
                Assert.Equal("0.25", lines[2].Split(',')[27]);
                Assert.Equal("4", lines[2].Split(',')[32]);
                Assert.Equal("1", lines[2].Split(',')[35]);
                Assert.Contains("rendered_frame", lines[2]);
                Assert.Contains("1.5", lines[2]);
                Assert.DoesNotContain("1,5", lines[2]);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        private sealed class ManualTimeProvider : TimeProvider
        {
            private long _timestamp;

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp() => _timestamp;

            public void AdvanceMilliseconds(double milliseconds)
            {
                _timestamp += TimeSpan.FromMilliseconds(milliseconds).Ticks;
            }
        }
    }
}
