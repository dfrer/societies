using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Societies.Core
{
    /// <summary>
    /// Centralizes artifact naming, persistence, and compatibility paths for prototype runs.
    /// Legacy filenames remain authoritative for current smoke coverage while V2 artifacts are emitted in parallel.
    /// </summary>
    public sealed class PrototypeRunArtifactManager
    {
        private const string DefaultRunOutputDirectory = "user://prototype_runs";
        private const string RunOutputDirectoryEnvironmentVariable = "SOCIETIES_RUN_OUTPUT_DIR";

        public PrototypeArtifactPaths GetArtifactPaths()
        {
            string root = GetRunOutputDirectoryPath();
            return new PrototypeArtifactPaths(
                Path.Combine(root, "latest-snapshot.json"),
                Path.Combine(root, "latest-event-log.json"),
                Path.Combine(root, "latest-run-summary.json"),
                Path.Combine(root, "snapshot-v2.json"),
                Path.Combine(root, "event-log-v2.json"),
                Path.Combine(root, "run-summary-v2.json"),
                Path.Combine(root, "metrics-timeseries-v2.csv"),
                Path.Combine(root, "world-summary-v2.json"));
        }

        public string SaveArtifacts(
            PrototypeRuntimeSession session,
            PrototypeRuntimeSnapshot snapshot,
            PrototypeWorldSummary worldSummary)
        {
            return SaveArtifacts(session, snapshot, worldSummary, runtimeMetrics: null);
        }

        public string SaveArtifacts(
            PrototypeRuntimeSession session,
            PrototypeRuntimeSnapshot snapshot,
            PrototypeWorldSummary worldSummary,
            RuntimeMetricsCollector? runtimeMetrics)
        {
            PrototypeArtifactPaths paths = GetArtifactPaths();
            PrototypeRunSummary runSummary = PrototypeRunSummaryBuilder.Build(
                snapshot,
                session.EventLog.Entries,
                session.RunStartHour,
                session.Scenario.Id,
                session.Scenario.DisplayName,
                worldSummary);

            PrototypePersistenceService.SaveSnapshot(paths.LegacySnapshotPath, snapshot);
            PrototypePersistenceService.SaveSnapshot(paths.SnapshotV2Path, snapshot);
            PrototypePersistenceService.SaveEventLog(paths.LegacyEventLogPath, session.EventLog);
            PrototypePersistenceService.SaveEventLog(paths.EventLogV2Path, session.EventLog);
            PrototypePersistenceService.SaveRunSummary(paths.LegacyRunSummaryPath, runSummary);
            PrototypePersistenceService.SaveRunSummary(paths.RunSummaryV2Path, runSummary);
            PrototypePersistenceService.SaveWorldSummary(paths.WorldSummaryV2Path, worldSummary);
            SaveText(paths.MetricsCsvPath, session.MetricsTracker.BuildCsv());
            SaveRuntimeMetricsBestEffort(paths.RuntimeMetricsCsvPath, runtimeMetrics);

            return paths.LegacySnapshotPath;
        }

        public PrototypeLoadedArtifacts? LoadLatestArtifacts()
        {
            PrototypeArtifactPaths paths = GetArtifactPaths();
            if (!File.Exists(paths.LegacySnapshotPath))
            {
                return null;
            }

            PrototypeRuntimeSnapshot snapshot = PrototypePersistenceService.LoadSnapshot(paths.LegacySnapshotPath);
            PrototypeEventRecord[] eventLog = File.Exists(paths.LegacyEventLogPath)
                ? PrototypePersistenceService.LoadEventLog(paths.LegacyEventLogPath).ToArray()
                : Array.Empty<PrototypeEventRecord>();
            PrototypeRunSummary? runSummary = File.Exists(paths.LegacyRunSummaryPath)
                ? PrototypePersistenceService.LoadRunSummary(paths.LegacyRunSummaryPath)
                : null;

            return new PrototypeLoadedArtifacts(snapshot, eventLog, runSummary);
        }

        private static void SaveText(string path, string content)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
        }

        private static void SaveRuntimeMetricsBestEffort(string path, RuntimeMetricsCollector? runtimeMetrics)
        {
            try
            {
                if (runtimeMetrics != null)
                {
                    runtimeMetrics.ExportCsv(path);
                    return;
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(
                    $"WARNING: Optional runtime metrics artifact '{path}' was not updated: {exception.Message}");
            }
        }

        private static string GetRunOutputDirectoryPath()
        {
            string? overrideDirectory = System.Environment.GetEnvironmentVariable(RunOutputDirectoryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overrideDirectory))
            {
                return Path.GetFullPath(overrideDirectory);
            }

            return ProjectSettings.GlobalizePath(DefaultRunOutputDirectory);
        }
    }

    public readonly record struct PrototypeArtifactPaths(
        string LegacySnapshotPath,
        string LegacyEventLogPath,
        string LegacyRunSummaryPath,
        string SnapshotV2Path,
        string EventLogV2Path,
        string RunSummaryV2Path,
        string MetricsCsvPath,
        string WorldSummaryV2Path)
    {
        public string RuntimeMetricsCsvPath => Path.Combine(
            Path.GetDirectoryName(LegacySnapshotPath) ?? string.Empty,
            "runtime-batch-metrics-v3.csv");
    }

    public readonly record struct PrototypeLoadedArtifacts(
        PrototypeRuntimeSnapshot Snapshot,
        IReadOnlyList<PrototypeEventRecord> EventLog,
        PrototypeRunSummary? RunSummary);
}
