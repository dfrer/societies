using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Societies.Core
{
    public enum RuntimeMetricsBatchKind
    {
        RenderedFrame,
        ManualStep
    }

    public enum RuntimeMetricsPhase
    {
        SimulationTick,
        SessionAdvance,
        BuildWorkOrders,
        HarvestApply,
        SceneSync,
        UpdateHud
    }

    public readonly record struct RuntimeTickDiagnostics(
        int WorkOrdersGenerated,
        int WorkOrdersGeneratedUncapped,
        int WorkOrdersClaimed,
        int WorkOrdersRemaining,
        int PathPlanLookups,
        int PathPlanCacheHits,
        int CitizensEvaluated);

    public readonly record struct RuntimePhaseTotals(
        double SimulationTickMilliseconds,
        double SessionAdvanceMilliseconds,
        double BuildWorkOrdersMilliseconds,
        double HarvestApplyMilliseconds,
        double SceneSyncMilliseconds,
        double UpdateHudMilliseconds);

    public readonly record struct RuntimeMetricsBatch(
        long Sequence,
        RuntimeMetricsBatchKind Kind,
        long StartSimulationTick,
        long EndSimulationTick,
        int CompletedTicks,
        double WallMilliseconds,
        double MaximumTickMilliseconds,
        RuntimePhaseTotals Phases,
        long WorkOrdersGeneratedTotal,
        long WorkOrdersGeneratedUncappedTotal,
        long WorkOrdersClaimedTotal,
        int? WorkOrdersRemainingLast,
        long PathPlanLookupsTotal,
        long PathPlanCacheHitsTotal,
        long CitizensEvaluatedTotal);

    /// <summary>
    /// Bounded, instance-owned runtime performance telemetry. A null collector represents
    /// disabled metrics so simulation hot paths do not read a clock or allocate diagnostic rows.
    /// </summary>
    public sealed class RuntimeMetricsCollector
    {
        private const string CsvHeader =
            "sequence,batch_kind,start_simulation_tick,end_simulation_tick,completed_ticks,wall_ms,max_tick_ms," +
            "simulation_tick_ms,session_advance_ms,build_work_orders_ms,harvest_apply_ms,scene_sync_ms,update_hud_ms," +
            "work_orders_generated_total,work_orders_generated_uncapped_total,work_orders_claimed_total," +
            "work_orders_remaining_last,path_plan_lookups_total,path_plan_cache_hits_total,citizens_evaluated_total";

        private readonly RuntimeMetricsBatch[] _batches;
        private readonly TimeProvider _clock;
        private readonly long[] _activePhaseTokenIds = new long[Enum.GetValues<RuntimeMetricsPhase>().Length];
        private int _startIndex;
        private int _count;
        private long _nextSequence;
        private long _generation;
        private long _nextBatchId;
        private long _nextPhaseTokenId;
        private long _droppedBatchCount;

        private bool _batchOpen;
        private long _activeBatchId;
        private RuntimeMetricsBatchKind _batchKind;
        private long _batchStartSimulationTick;
        private long _batchStartTimestamp;
        private int _completedTicks;
        private double _maximumTickMilliseconds;
        private double? _pendingCompletedTickMilliseconds;
        private double _simulationTickMilliseconds;
        private double _sessionAdvanceMilliseconds;
        private double _buildWorkOrdersMilliseconds;
        private double _harvestApplyMilliseconds;
        private double _sceneSyncMilliseconds;
        private double _updateHudMilliseconds;
        private long _workOrdersGeneratedTotal;
        private long _workOrdersGeneratedUncappedTotal;
        private long _workOrdersClaimedTotal;
        private int? _workOrdersRemainingLast;
        private long _pathPlanLookupsTotal;
        private long _pathPlanCacheHitsTotal;
        private long _citizensEvaluatedTotal;

        public RuntimeMetricsCollector(int capacity = 4096, TimeProvider? clock = null)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Metrics capacity must be positive.");
            }

            _batches = new RuntimeMetricsBatch[capacity];
            _clock = clock ?? TimeProvider.System;
        }

        public int Count => _count;

        public int Capacity => _batches.Length;

        public long DroppedBatchCount => _droppedBatchCount;

        public void Reset()
        {
            Array.Clear(_batches);
            _startIndex = 0;
            _count = 0;
            _nextSequence = 0;
            _droppedBatchCount = 0;
            _batchOpen = false;
            _generation++;
            ClearActiveBatch();
        }

        public void BeginBatch(RuntimeMetricsBatchKind kind, long startSimulationTick)
        {
            if (_batchOpen)
            {
                throw new InvalidOperationException("A runtime metrics batch is already open.");
            }

            if (startSimulationTick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startSimulationTick));
            }

            if (!Enum.IsDefined(typeof(RuntimeMetricsBatchKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown runtime metrics batch kind.");
            }

            ClearActiveBatch();
            _batchOpen = true;
            _activeBatchId = ++_nextBatchId;
            _batchKind = kind;
            _batchStartSimulationTick = startSimulationTick;
            _batchStartTimestamp = _clock.GetTimestamp();
        }

        public RuntimeMetricsPhaseToken BeginPhase(RuntimeMetricsPhase phase)
        {
            EnsureBatchOpen();
            int phaseIndex = GetPhaseIndex(phase);
            if (_activePhaseTokenIds[phaseIndex] != 0)
            {
                throw new InvalidOperationException($"Runtime metrics phase '{phase}' is already active in this batch.");
            }

            if (_nextPhaseTokenId == long.MaxValue)
            {
                throw new InvalidOperationException("Runtime metrics phase token sequence exhausted.");
            }

            long tokenId = ++_nextPhaseTokenId;
            _activePhaseTokenIds[phaseIndex] = tokenId;
            return new RuntimeMetricsPhaseToken(
                this,
                _generation,
                _activeBatchId,
                tokenId,
                phase,
                _clock.GetTimestamp());
        }

        public void RecordCompletedTick(in RuntimeTickDiagnostics diagnostics)
        {
            EnsureBatchOpen();
            if (!_pendingCompletedTickMilliseconds.HasValue)
            {
                throw new InvalidOperationException(
                    "A SimulationTick phase must complete before recording a completed tick.");
            }

            double elapsedMilliseconds = _pendingCompletedTickMilliseconds.Value;
            _pendingCompletedTickMilliseconds = null;
            _completedTicks++;
            _maximumTickMilliseconds = Math.Max(_maximumTickMilliseconds, elapsedMilliseconds);
            _workOrdersGeneratedTotal += diagnostics.WorkOrdersGenerated;
            _workOrdersGeneratedUncappedTotal += diagnostics.WorkOrdersGeneratedUncapped;
            _workOrdersClaimedTotal += diagnostics.WorkOrdersClaimed;
            _workOrdersRemainingLast = diagnostics.WorkOrdersRemaining;
            _pathPlanLookupsTotal += diagnostics.PathPlanLookups;
            _pathPlanCacheHitsTotal += diagnostics.PathPlanCacheHits;
            _citizensEvaluatedTotal += diagnostics.CitizensEvaluated;
        }

        public void EndBatch(long endSimulationTick)
        {
            EnsureBatchOpen();
            if (HasActivePhaseTokens())
            {
                throw new InvalidOperationException("All runtime metrics phases must complete before ending a batch.");
            }
            if (_pendingCompletedTickMilliseconds.HasValue)
            {
                throw new InvalidOperationException(
                    "The completed SimulationTick phase must be recorded before ending the batch.");
            }

            if (endSimulationTick < _batchStartSimulationTick)
            {
                throw new ArgumentOutOfRangeException(nameof(endSimulationTick));
            }

            if (endSimulationTick - _batchStartSimulationTick != _completedTicks)
            {
                throw new InvalidOperationException(
                    "Completed tick count must equal the simulation tick delta for the batch.");
            }

            double wallMilliseconds = GetElapsedMilliseconds(_batchStartTimestamp);
            var batch = new RuntimeMetricsBatch(
                _nextSequence++,
                _batchKind,
                _batchStartSimulationTick,
                endSimulationTick,
                _completedTicks,
                wallMilliseconds,
                _maximumTickMilliseconds,
                new RuntimePhaseTotals(
                    _simulationTickMilliseconds,
                    _sessionAdvanceMilliseconds,
                    _buildWorkOrdersMilliseconds,
                    _harvestApplyMilliseconds,
                    _sceneSyncMilliseconds,
                    _updateHudMilliseconds),
                _workOrdersGeneratedTotal,
                _workOrdersGeneratedUncappedTotal,
                _workOrdersClaimedTotal,
                _workOrdersRemainingLast,
                _pathPlanLookupsTotal,
                _pathPlanCacheHitsTotal,
                _citizensEvaluatedTotal);

            Store(batch);
            _batchOpen = false;
            ClearActiveBatch();
        }

        public void AbortBatch()
        {
            if (!_batchOpen)
            {
                return;
            }

            _batchOpen = false;
            ClearActiveBatch();
        }

        public RuntimeMetricsBatch[] SnapshotBatches()
        {
            var snapshot = new RuntimeMetricsBatch[_count];
            for (int index = 0; index < _count; index++)
            {
                snapshot[index] = _batches[(_startIndex + index) % Capacity];
            }

            return snapshot;
        }

        public void WriteCsv(TextWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteLine(CsvHeader);

            for (int index = 0; index < _count; index++)
            {
                RuntimeMetricsBatch batch = _batches[(_startIndex + index) % Capacity];
                WriteCsvBatch(writer, in batch);
            }
        }

        public void ExportCsv(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Metrics export path is required.", nameof(path));
            }

            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var writer = new StreamWriter(fullPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            WriteCsv(writer);
        }

        internal double CompletePhase(
            long generation,
            long batchId,
            long tokenId,
            RuntimeMetricsPhase phase,
            long startTimestamp)
        {
            if (!_batchOpen || generation != _generation || batchId != _activeBatchId)
            {
                return 0.0;
            }

            int phaseIndex = GetPhaseIndex(phase);
            if (_activePhaseTokenIds[phaseIndex] != tokenId)
            {
                return 0.0;
            }

            _activePhaseTokenIds[phaseIndex] = 0;

            double elapsedMilliseconds = GetElapsedMilliseconds(startTimestamp);
            switch (phase)
            {
                case RuntimeMetricsPhase.SimulationTick:
                    if (_pendingCompletedTickMilliseconds.HasValue)
                    {
                        throw new InvalidOperationException(
                            "The previous SimulationTick phase must be recorded before another completes.");
                    }
                    _simulationTickMilliseconds += elapsedMilliseconds;
                    _pendingCompletedTickMilliseconds = elapsedMilliseconds;
                    break;
                case RuntimeMetricsPhase.SessionAdvance:
                    _sessionAdvanceMilliseconds += elapsedMilliseconds;
                    break;
                case RuntimeMetricsPhase.BuildWorkOrders:
                    _buildWorkOrdersMilliseconds += elapsedMilliseconds;
                    break;
                case RuntimeMetricsPhase.HarvestApply:
                    _harvestApplyMilliseconds += elapsedMilliseconds;
                    break;
                case RuntimeMetricsPhase.SceneSync:
                    _sceneSyncMilliseconds += elapsedMilliseconds;
                    break;
                case RuntimeMetricsPhase.UpdateHud:
                    _updateHudMilliseconds += elapsedMilliseconds;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown runtime metrics phase.");
            }

            return elapsedMilliseconds;
        }

        private void Store(RuntimeMetricsBatch batch)
        {
            if (_count < Capacity)
            {
                int writeIndex = (_startIndex + _count) % Capacity;
                _batches[writeIndex] = batch;
                _count++;
                return;
            }

            _batches[_startIndex] = batch;
            _startIndex = (_startIndex + 1) % Capacity;
            _droppedBatchCount++;
        }

        private void EnsureBatchOpen()
        {
            if (!_batchOpen)
            {
                throw new InvalidOperationException("No runtime metrics batch is open.");
            }
        }

        private double GetElapsedMilliseconds(long startTimestamp)
        {
            return _clock.GetElapsedTime(startTimestamp, _clock.GetTimestamp()).TotalMilliseconds;
        }

        private void ClearActiveBatch()
        {
            _activeBatchId = 0;
            _batchKind = default;
            _batchStartSimulationTick = 0;
            _batchStartTimestamp = 0;
            _completedTicks = 0;
            _maximumTickMilliseconds = 0.0;
            _pendingCompletedTickMilliseconds = null;
            _simulationTickMilliseconds = 0.0;
            _sessionAdvanceMilliseconds = 0.0;
            _buildWorkOrdersMilliseconds = 0.0;
            _harvestApplyMilliseconds = 0.0;
            _sceneSyncMilliseconds = 0.0;
            _updateHudMilliseconds = 0.0;
            _workOrdersGeneratedTotal = 0;
            _workOrdersGeneratedUncappedTotal = 0;
            _workOrdersClaimedTotal = 0;
            _workOrdersRemainingLast = null;
            _pathPlanLookupsTotal = 0;
            _pathPlanCacheHitsTotal = 0;
            _citizensEvaluatedTotal = 0;
            Array.Clear(_activePhaseTokenIds);
        }

        private static void WriteCsvBatch(TextWriter writer, in RuntimeMetricsBatch batch)
        {
            var values = new[]
            {
                batch.Sequence.ToString(CultureInfo.InvariantCulture),
                FormatBatchKind(batch.Kind),
                batch.StartSimulationTick.ToString(CultureInfo.InvariantCulture),
                batch.EndSimulationTick.ToString(CultureInfo.InvariantCulture),
                batch.CompletedTicks.ToString(CultureInfo.InvariantCulture),
                FormatDouble(batch.WallMilliseconds),
                FormatDouble(batch.MaximumTickMilliseconds),
                FormatDouble(batch.Phases.SimulationTickMilliseconds),
                FormatDouble(batch.Phases.SessionAdvanceMilliseconds),
                FormatDouble(batch.Phases.BuildWorkOrdersMilliseconds),
                FormatDouble(batch.Phases.HarvestApplyMilliseconds),
                FormatDouble(batch.Phases.SceneSyncMilliseconds),
                FormatDouble(batch.Phases.UpdateHudMilliseconds),
                batch.WorkOrdersGeneratedTotal.ToString(CultureInfo.InvariantCulture),
                batch.WorkOrdersGeneratedUncappedTotal.ToString(CultureInfo.InvariantCulture),
                batch.WorkOrdersClaimedTotal.ToString(CultureInfo.InvariantCulture),
                batch.WorkOrdersRemainingLast?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                batch.PathPlanLookupsTotal.ToString(CultureInfo.InvariantCulture),
                batch.PathPlanCacheHitsTotal.ToString(CultureInfo.InvariantCulture),
                batch.CitizensEvaluatedTotal.ToString(CultureInfo.InvariantCulture)
            };

            writer.WriteLine(string.Join(',', values));
        }

        private static string FormatBatchKind(RuntimeMetricsBatchKind kind)
        {
            return kind switch
            {
                RuntimeMetricsBatchKind.RenderedFrame => "rendered_frame",
                RuntimeMetricsBatchKind.ManualStep => "manual_step",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown runtime metrics batch kind.")
            };
        }

        private static string FormatDouble(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static int GetPhaseIndex(RuntimeMetricsPhase phase)
        {
            if (!Enum.IsDefined(typeof(RuntimeMetricsPhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown runtime metrics phase.");
            }

            return (int)phase;
        }

        private bool HasActivePhaseTokens()
        {
            foreach (long tokenId in _activePhaseTokenIds)
            {
                if (tokenId != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public struct RuntimeMetricsPhaseToken : IDisposable
    {
        private RuntimeMetricsCollector? _collector;
        private readonly long _generation;
        private readonly long _batchId;
        private readonly long _tokenId;
        private readonly RuntimeMetricsPhase _phase;
        private readonly long _startTimestamp;

        internal RuntimeMetricsPhaseToken(
            RuntimeMetricsCollector collector,
            long generation,
            long batchId,
            long tokenId,
            RuntimeMetricsPhase phase,
            long startTimestamp)
        {
            _collector = collector;
            _generation = generation;
            _batchId = batchId;
            _tokenId = tokenId;
            _phase = phase;
            _startTimestamp = startTimestamp;
        }

        public double Complete()
        {
            RuntimeMetricsCollector? collector = _collector;
            _collector = null;
            return collector?.CompletePhase(_generation, _batchId, _tokenId, _phase, _startTimestamp) ?? 0.0;
        }

        public void Dispose()
        {
            Complete();
        }
    }
}
