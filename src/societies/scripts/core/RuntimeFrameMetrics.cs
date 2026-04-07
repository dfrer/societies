using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Societies.Core
{
    /// <summary>
    /// Lightweight phase-level timing accumulator for the authoritative
    /// runtime. Designed to sit behind a toggle so overhead is zero when off.
    /// Enable via SOCIETIES_PERF_METRICS=1 env var or programmatically.
    /// </summary>
    public sealed class RuntimeFrameMetrics
    {
        private static RuntimeFrameMetrics? _instance;
        public static RuntimeFrameMetrics Instance => _instance ??= new RuntimeFrameMetrics();

        public bool IsEnabled { get; set; } = System.Environment.GetEnvironmentVariable("SOCIETIES_PERF_METRICS") == "1";

        // Per-frame accumulators (reset each rendered frame).
        public int TicksProcessedInFrame { get; private set; }
        public double FrameWallMs { get; private set; }
        public double ProcessSimulationTickTotalMs { get; private set; }
        public double SessionAdvanceTotalMs { get; private set; }
        public double BuildWorkOrdersTotalMs { get; internal set; }
        public double SceneSyncTotalMs { get; internal set; }
        public double UpdateHudTotalMs { get; internal set; }
        public double SaveArtifactTotalMs { get; internal set; }
        public double HarvestApplyTotalMs { get; internal set; }

        // Diagnostics forwarded from settlement simulation each tick.
        public int PathPlanLookupsLastTick { get; set; }
        public int PathPlanCacheHitsLastTick { get; set; }
        public int WorkOrdersGeneratedLastTick { get; set; }
        public int WorkOrdersRemainingLastTick { get; set; }

        // Running peaks (across all measured frames/ticks).
        private int _peakTicksPerFrame;
        public int PeakTicksPerFrame => _peakTicksPerFrame;
        private double _peakFrameMs;
        public double PeakFrameMs => _peakFrameMs;
        private double _peakTickMs;
        public double PeakTickMs => _peakTickMs;

        // Ring buffer of recent per-tick durations (last N ticks).
        private readonly double[] _tickDurations;
        private int _tickRingIndex;
        private int _tickRingCount;

        /// <summary>
        /// Number of tick durations to retain in the ring buffer. Keep small.
        /// </summary>
        public int RingBufferSize => _tickDurations.Length;

        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private double _phaseStart;
        private double _frameStart;

        /// <summary>
        /// Per-frame timing categories that can be measured independently.
        /// </summary>
        public enum Phase
        {
            // Must stay in sync with _phaseLabels below.
            FrameTotal,
            SimulationTick,
            SessionAdvance,
            BuildWorkOrders,
            SceneSync,
            UpdateHud,
            SaveArtifact,
            HarvestApply,
        }

        private static readonly string[] _phaseLabels = new[]
        {
            "frame_total",
            "process_simulation_tick",
            "session_advance",
            "build_work_orders",
            "scene_sync",
            "update_hud",
            "save_artifact",
            "harvest_apply",
        };

        private RuntimeFrameMetrics(int ringBufferSize = 128)
        {
            _tickDurations = new double[ringBufferSize];
            _tickRingIndex = 0;
            _tickRingCount = 0;
        }

        // ------------------------------------------------------------------
        // Public helpers called from GameManager and related components.
        // ------------------------------------------------------------------

        public void BeginFrame()
        {
            if (!IsEnabled) return;
            TicksProcessedInFrame = 0;
            ProcessSimulationTickTotalMs = 0;
            SessionAdvanceTotalMs = 0;
            BuildWorkOrdersTotalMs = 0;
            SceneSyncTotalMs = 0;
            UpdateHudTotalMs = 0;
            SaveArtifactTotalMs = 0;
            HarvestApplyTotalMs = 0;
            PathPlanLookupsLastTick = 0;
            PathPlanCacheHitsLastTick = 0;
            WorkOrdersGeneratedLastTick = 0;
            WorkOrdersRemainingLastTick = 0;
            _frameStart = _sw.Elapsed.TotalMilliseconds;
        }

        public void EndFrame()
        {
            if (!IsEnabled) return;
            FrameWallMs = _sw.Elapsed.TotalMilliseconds - _frameStart;
            if (TicksProcessedInFrame > _peakTicksPerFrame) _peakTicksPerFrame = TicksProcessedInFrame;
            if (FrameWallMs > _peakFrameMs) _peakFrameMs = FrameWallMs;
        }

        /// <summary>
        /// Record that one simulation tick was processed within this frame.
        /// </summary>
        public void RecordTickProcessed()
        {
            if (!IsEnabled) return;
            TicksProcessedInFrame++;
        }

        /// <summary>
        /// Push the most recent tick's duration into the ring buffer.
        /// </summary>
        public void RecordTickDurationMs(double ms)
        {
            if (!IsEnabled) return;
            _tickDurations[_tickRingIndex] = ms;
            _tickRingIndex = (_tickRingIndex + 1) % _tickDurations.Length;
            if (_tickRingCount < _tickDurations.Length) _tickRingCount++;
            if (ms > _peakTickMs) _peakTickMs = ms;
        }

        public double GetAverageTickMs()
        {
            double sum = 0;
            for (int i = 0; i < _tickRingCount; i++) sum += _tickDurations[i];
            return _tickRingCount > 0 ? sum / _tickRingCount : 0.0;
        }

        public double GetMaxRecentTickMs()
        {
            double max = 0;
            for (int i = 0; i < _tickRingCount; i++)
                if (_tickDurations[i] > max) max = _tickDurations[i];
            return max;
        }

        /// <summary>
        /// Measure a phase with a Stopwatch-like pattern.
        /// Usage: m.BeginPhase(Phase.SessionAdvance); ...; m.EndPhase(Phase.SessionAdvance);
        /// </summary>
        public void BeginPhase(Phase phase)
        {
            if (!IsEnabled) return;
            _phaseStart = _sw.Elapsed.TotalMilliseconds;
        }

        public void EndPhase(Phase phase)
        {
            if (!IsEnabled) return;
            double elapsed = _sw.Elapsed.TotalMilliseconds - _phaseStart;
            switch (phase)
            {
                case Phase.FrameTotal: break; // tracked by Begin/EndFrame instead
                case Phase.SimulationTick: ProcessSimulationTickTotalMs += elapsed; break;
                case Phase.SessionAdvance: SessionAdvanceTotalMs += elapsed; break;
                case Phase.BuildWorkOrders: BuildWorkOrdersTotalMs += elapsed; break;
                case Phase.SceneSync: SceneSyncTotalMs += elapsed; break;
                case Phase.UpdateHud: UpdateHudTotalMs += elapsed; break;
                case Phase.SaveArtifact: SaveArtifactTotalMs += elapsed; break;
                case Phase.HarvestApply: HarvestApplyTotalMs += elapsed; break;
            }
        }

        /// <summary>
        /// Build a concise human-readable summary of the last frame.
        /// </summary>
        public string BuildFrameSummary()
        {
            if (!IsEnabled) return "<metrics disabled>";
            StringBuilder sb = new();
            sb.AppendLine("--- Per-Frame Summary ---");
            sb.AppendLine($"  Frame wall:              {FrameWallMs:F2} ms");
            sb.AppendLine($"  Ticks in frame:          {TicksProcessedInFrame}");
            sb.AppendLine($"  ProcessSimulationTick:   {ProcessSimulationTickTotalMs:F2} ms");
            sb.AppendLine($"  SessionAdvance:          {SessionAdvanceTotalMs:F2} ms");
            sb.AppendLine($"  BuildWorkOrders:         {BuildWorkOrdersTotalMs:F2} ms");
            sb.AppendLine($"  SceneSync:               {SceneSyncTotalMs:F2} ms");
            sb.AppendLine($"  UpdateHud:               {UpdateHudTotalMs:F2} ms");
            sb.AppendLine($"  SaveArtifact:            {SaveArtifactTotalMs:F2} ms");
            sb.AppendLine($"  HarvestApply:            {HarvestApplyTotalMs:F2} ms");
            sb.AppendLine($"  Path lookups/Hits:       {PathPlanLookupsLastTick}/{PathPlanCacheHitsLastTick}");
            sb.AppendLine($"  Orders gen/remain:       {WorkOrdersGeneratedLastTick}/{WorkOrdersRemainingLastTick}");
            sb.AppendLine("--- Running Peaks ---");
            sb.AppendLine($"  Peak ticks/frame:        {_peakTicksPerFrame}");
            sb.AppendLine($"  Peak frame ms:           {_peakFrameMs:F2} ms");
            sb.AppendLine($"  Peak tick ms:            {_peakTickMs:F2} ms");
            sb.AppendLine($"  Avg recent tick ms:      {GetAverageTickMs():F2} ms");
            sb.AppendLine($"  Max recent tick ms:      {GetMaxRecentTickMs():F2} ms");
            return sb.ToString();
        }

        /// <summary>
        /// CSV header for per-frame export.
        /// </summary>
        public static string CsvHeader =>
            "frame_num,frame_ms,ticks_in_frame,sim_tick_ms,advance_ms,bwo_ms,sync_ms,hud_ms,save_ms,harvest_ms,path_lookups,path_hits,orders_gen,orders_remain";

        /// <summary>
        /// CSV row for a single frame.
        /// </summary>
        public string ToCsvRow(int frameNum)
        {
            if (!IsEnabled) return "";
            return $"{frameNum},{FrameWallMs:F2},{TicksProcessedInFrame},{ProcessSimulationTickTotalMs:F2},{SessionAdvanceTotalMs:F2},{BuildWorkOrdersTotalMs:F2},{SceneSyncTotalMs:F2},{UpdateHudTotalMs:F2},{SaveArtifactTotalMs:F2},{HarvestApplyTotalMs:F2},{PathPlanLookupsLastTick},{PathPlanCacheHitsLastTick},{WorkOrdersGeneratedLastTick},{WorkOrdersRemainingLastTick}";
        }

        // ---- Session-level accumulators ----
        private int _totalFramesMeasured;
        private int _totalTicksProcessed;
        private double _cumulativeFrameMs;

        /// <summary>
        /// Call at end of each frame when metrics are enabled. Accumulates session-level stats.
        /// </summary>
        public void AccumulateSessionStats()
        {
            if (!IsEnabled) return;
            _totalFramesMeasured++;
            _totalTicksProcessed += TicksProcessedInFrame;
            _cumulativeFrameMs += FrameWallMs;
        }

        /// <summary>
        /// Call after each individual simulation tick when not going through the frame
        /// loop (e.g., StepSimulationTicks, headless characterization).
        /// Records the same tick data that _Process would have accumulated.
        /// </summary>
        public void AccumulateSingleTick(double tickDurationMs)
        {
            if (!IsEnabled) return;
            _totalTicksProcessed++;
            // Record the tick in the ring buffer (already done by RecordTickDurationMs)
            // Also update the peak
            if (tickDurationMs > _peakTickMs) _peakTickMs = tickDurationMs;
        }

        /// <summary>
        /// Export all accumulated CSV rows to a file.
        /// </summary>
        private readonly System.Collections.Generic.List<string> _csvRows = new();

        public void WriteCsvRow(string row)
        {
            if (!IsEnabled) return;
            _csvRows.Add(row);
        }

        public void ExportToCsv(string path)
        {
            if (!IsEnabled) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(CsvHeader);
            foreach (var row in _csvRows)
            {
                sb.AppendLine(row);
            }

            string? dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(path, sb.ToString());
        }

        /// <summary>
        /// Build an end-of-session summary with aggregate stats.
        /// </summary>
        public string BuildSessionSummary()
        {
            if (!IsEnabled) return "<metrics disabled>";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("====== Runtime Perf Session Summary ======");
            sb.AppendLine($"  Total frames measured:      {_totalFramesMeasured}");
            sb.AppendLine($"  Total ticks processed:       {_totalTicksProcessed}");
            sb.AppendLine($"  Cumulative frame time:       {_cumulativeFrameMs:F2} ms");
            sb.AppendLine($"  Avg frame wall:              {(_totalFramesMeasured > 0 ? _cumulativeFrameMs / _totalFramesMeasured : 0):F2} ms");
            sb.AppendLine($"  Peak ticks/frame:            {PeakTicksPerFrame}");
            sb.AppendLine($"  Peak frame wall:             {PeakFrameMs:F2} ms");
            sb.AppendLine($"  Peak single tick:            {PeakTickMs:F2} ms");
            sb.AppendLine($"  Avg recent tick (ring):      {GetAverageTickMs():F2} ms");
            sb.AppendLine($"  Max recent tick (ring):      {GetMaxRecentTickMs():F2} ms");
            sb.AppendLine("====== End Session Summary ======");
            return sb.ToString();
        }
    }
}
