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
            clock.AdvanceMilliseconds(2);
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
                new RuntimeTickDiagnostics(10, 14, 2, 8, 100, 80, 4));
            RuntimeMetricsPhaseToken secondTickPhase = collector.BeginPhase(RuntimeMetricsPhase.SimulationTick);
            clock.AdvanceMilliseconds(2);
            secondTickPhase.Complete();
            collector.RecordCompletedTick(
                new RuntimeTickDiagnostics(20, 27, 3, 5, 150, 140, 6));
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
                clock.AdvanceMilliseconds(1.5);
                tickPhase.Complete();
                collector.RecordCompletedTick(default);
                collector.EndBatch(1);

                using var writer = new StringWriter();
                collector.WriteCsv(writer);
                string[] lines = writer.ToString().ReplaceLineEndings("\n")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries);

                Assert.Equal(3, lines.Length);
                Assert.Equal(20, lines[0].Split(',').Length);
                Assert.Equal(20, lines[1].Split(',').Length);
                Assert.Equal(string.Empty, lines[1].Split(',')[16]);
                Assert.Equal(20, lines[2].Split(',').Length);
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
