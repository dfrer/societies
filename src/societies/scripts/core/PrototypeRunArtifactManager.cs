using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Societies.Simulation;

namespace Societies.Core
{
    internal static class PrototypePersistenceBounds
    {
        internal const int MaximumSnapshotRows = 10_000;
    }

    /// <summary>
    /// Centralizes artifact naming, persistence, and compatibility paths for prototype runs.
    /// Legacy filenames remain authoritative for current smoke coverage while V2 artifacts are emitted in parallel.
    /// </summary>
    public sealed class PrototypeRunArtifactManager
    {
        private const string DefaultRunOutputDirectory = "user://prototype_runs";
        private const string RunOutputDirectoryEnvironmentVariable = "SOCIETIES_RUN_OUTPUT_DIR";
        private const int GenerationManifestSchemaVersion = 1;
        internal const long MaximumManifestBytes = 64 * 1024;
        internal const long MaximumSnapshotBytes = 16 * 1024 * 1024;
        internal const long MaximumEventLogBytes = 16 * 1024 * 1024;
        internal const long MaximumRunSummaryBytes = 2 * 1024 * 1024;
        internal const long MaximumWorldSummaryBytes = 2 * 1024 * 1024;
        private const long MaximumMetricsCsvBytes = 16 * 1024 * 1024;
        internal const int MaximumEventRows = 50_000;
        internal const int MaximumDictionaryEntries = 4_096;
        internal const int MaximumIdentifierLength = 128;
        internal const int MaximumMessageLength = 1_024;
        private static readonly JsonSerializerOptions ManifestJsonOptions = new()
        {
            WriteIndented = true,
            MaxDepth = 16
        };
        private static readonly string[] RequiredSchemaV8ManifestProperties =
        {
            nameof(PrototypeArtifactGenerationManifest.SchemaVersion),
            nameof(PrototypeArtifactGenerationManifest.GenerationId),
            nameof(PrototypeArtifactGenerationManifest.RuntimeSchemaVersion),
            nameof(PrototypeArtifactGenerationManifest.ScenarioId),
            nameof(PrototypeArtifactGenerationManifest.SimulationTick),
            nameof(PrototypeArtifactGenerationManifest.EventCount),
            nameof(PrototypeArtifactGenerationManifest.Snapshot),
            nameof(PrototypeArtifactGenerationManifest.EventLog),
            nameof(PrototypeArtifactGenerationManifest.RunSummary)
        };
        private static readonly string[] RequiredSchemaV8BindingProperties =
        {
            nameof(PrototypeArtifactFileBinding.FileName),
            nameof(PrototypeArtifactFileBinding.ByteLength),
            nameof(PrototypeArtifactFileBinding.Sha256)
        };

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

            string generationId = Guid.NewGuid().ToString("N");
            byte[] snapshotBytes = Encoding.UTF8.GetBytes(
                PrototypePersistenceService.SerializeSnapshot(snapshot));
            byte[] eventLogBytes = Encoding.UTF8.GetBytes(
                PrototypePersistenceService.SerializeEventLog(session.EventLog));
            byte[] runSummaryBytes = Encoding.UTF8.GetBytes(
                PrototypePersistenceService.SerializeRunSummary(runSummary));
            byte[] worldSummaryBytes = Encoding.UTF8.GetBytes(
                PrototypePersistenceService.SerializeWorldSummary(worldSummary));
            byte[] metricsCsvBytes = Encoding.UTF8.GetBytes(
                session.MetricsTracker.BuildCsv());
            byte[]? runtimeMetricsCsvBytes = BuildRuntimeMetricsCsv(runtimeMetrics);
            PrototypeArtifactGenerationManifest? manifest = null;
            byte[]? manifestBytes = null;

            ValidatePayloadByteLength(snapshotBytes, MaximumSnapshotBytes, "snapshot");
            ValidatePayloadByteLength(eventLogBytes, MaximumEventLogBytes, "event log");
            ValidatePayloadByteLength(runSummaryBytes, MaximumRunSummaryBytes, "run summary");
            ValidatePayloadByteLength(worldSummaryBytes, MaximumWorldSummaryBytes, "world summary");
            ValidatePayloadByteLength(metricsCsvBytes, MaximumMetricsCsvBytes, "metrics CSV");
            if (runtimeMetricsCsvBytes != null)
            {
                ValidatePayloadByteLength(
                    runtimeMetricsCsvBytes,
                    MaximumMetricsCsvBytes,
                    "runtime metrics CSV");
            }

            PreflightJson(
                worldSummaryBytes,
                PrototypePersistenceBounds.MaximumSnapshotRows,
                MaximumDictionaryEntries,
                MaximumMessageLength,
                "world summary");
            _ = PrototypePersistenceService.DeserializeWorldSummary(
                Encoding.UTF8.GetString(worldSummaryBytes));
            if (snapshot.SchemaVersion is 7 or 8 or 9 or 10 or 11 or 12)
            {
                PrototypeRuntimeSnapshot validatedSnapshot = DeserializeAndValidateSnapshotPayload(
                    snapshotBytes);
                PrototypeEventRecord[] validatedEventLog = DeserializeAndValidateEventLogPayload(
                    eventLogBytes,
                    validatedSnapshot.SimulationTick);
                PrototypeRunSummary validatedRunSummary = DeserializeAndValidateRunSummaryPayload(
                    runSummaryBytes);
                ValidateRunSummary(validatedRunSummary, validatedSnapshot, validatedEventLog);

                manifest = new PrototypeArtifactGenerationManifest
                {
                    SchemaVersion = GenerationManifestSchemaVersion,
                    GenerationId = generationId,
                    RuntimeSchemaVersion = validatedSnapshot.SchemaVersion,
                    ScenarioId = validatedSnapshot.ScenarioId,
                    SimulationTick = validatedSnapshot.SimulationTick,
                    EventCount = validatedEventLog.Length,
                    Snapshot = BuildBinding(paths.LegacySnapshotPath, snapshotBytes),
                    EventLog = BuildBinding(paths.LegacyEventLogPath, eventLogBytes),
                    RunSummary = BuildBinding(paths.LegacyRunSummaryPath, runSummaryBytes)
                };
                ValidateManifest(manifest, paths, validatedSnapshot);
                manifestBytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(manifest, ManifestJsonOptions));
                ValidatePayloadByteLength(
                    manifestBytes,
                    MaximumManifestBytes,
                    "generation manifest");
                PreflightJson(
                    manifestBytes,
                    maximumArrayItems: 32,
                    maximumObjectProperties: 32,
                    maximumStringBytes: MaximumMessageLength,
                    "generation manifest");
            }

            AtomicWrite(paths.LegacySnapshotPath, snapshotBytes, generationId);
            AtomicWrite(paths.SnapshotV2Path, snapshotBytes, generationId);
            AtomicWrite(paths.LegacyEventLogPath, eventLogBytes, generationId);
            AtomicWrite(paths.EventLogV2Path, eventLogBytes, generationId);
            AtomicWrite(paths.LegacyRunSummaryPath, runSummaryBytes, generationId);
            AtomicWrite(paths.RunSummaryV2Path, runSummaryBytes, generationId);
            AtomicWrite(paths.WorldSummaryV2Path, worldSummaryBytes, generationId);
            AtomicWrite(paths.MetricsCsvPath, metricsCsvBytes, generationId);
            SaveRuntimeMetricsBestEffort(
                paths.RuntimeMetricsCsvPath,
                paths.LegacyRuntimeMetricsCsvPath,
                paths.OlderLegacyRuntimeMetricsCsvPath,
                runtimeMetricsCsvBytes,
                generationId);

            if (manifestBytes != null)
            {
                AtomicWrite(
                    paths.GenerationManifestPath,
                    manifestBytes,
                    generationId);
            }

            return paths.LegacySnapshotPath;
        }

        public PrototypeLoadedArtifacts? LoadLatestArtifacts()
        {
            PrototypeArtifactPaths paths = GetArtifactPaths();
            if (!File.Exists(paths.LegacySnapshotPath))
            {
                return null;
            }

            byte[] snapshotBytes = ReadBoundedFile(paths.LegacySnapshotPath, MaximumSnapshotBytes, "snapshot");
            PrototypeRuntimeSnapshot snapshot = DeserializeAndValidateSnapshotPayload(snapshotBytes);

            if (snapshot.SchemaVersion is 7 or 8 or 9 or 10 or 11 or 12)
            {
                return LoadCommittedArtifacts(paths, snapshot, snapshotBytes);
            }

            PrototypeEventRecord[] eventLog = Array.Empty<PrototypeEventRecord>();
            if (File.Exists(paths.LegacyEventLogPath))
            {
                byte[] eventLogBytes = ReadBoundedFile(
                    paths.LegacyEventLogPath,
                    MaximumEventLogBytes,
                    "event log");
                eventLog = DeserializeAndValidateEventLogPayload(
                    eventLogBytes,
                    snapshot.SimulationTick);
            }

            PrototypeRunSummary? runSummary = null;
            if (File.Exists(paths.LegacyRunSummaryPath))
            {
                byte[] runSummaryBytes = ReadBoundedFile(
                    paths.LegacyRunSummaryPath,
                    MaximumRunSummaryBytes,
                    "run summary");
                runSummary = DeserializeAndValidateRunSummaryPayload(runSummaryBytes);
            }

            return new PrototypeLoadedArtifacts(snapshot, eventLog, runSummary);
        }

        private static PrototypeRuntimeSnapshot DeserializeAndValidateSnapshotPayload(
            byte[] snapshotBytes)
        {
            ValidatePayloadByteLength(snapshotBytes, MaximumSnapshotBytes, "snapshot");
            PreflightJson(
                snapshotBytes,
                PrototypePersistenceBounds.MaximumSnapshotRows,
                MaximumDictionaryEntries,
                MaximumMessageLength,
                "snapshot");
            PrototypeRuntimeSnapshot snapshot;
            try
            {
                snapshot = PrototypePersistenceService.DeserializeSnapshot(
                    Encoding.UTF8.GetString(snapshotBytes));
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Snapshot JSON is malformed.", exception);
            }

            ValidateSnapshotBounds(snapshot);
            return snapshot;
        }

        private static PrototypeEventRecord[] DeserializeAndValidateEventLogPayload(
            byte[] eventLogBytes,
            long snapshotTick)
        {
            ValidatePayloadByteLength(eventLogBytes, MaximumEventLogBytes, "event log");
            PreflightJson(
                eventLogBytes,
                MaximumEventRows,
                maximumObjectProperties: 8,
                maximumStringBytes: MaximumMessageLength,
                "event log");
            List<PrototypeEventRecord> eventLog;
            try
            {
                eventLog = PrototypePersistenceService.DeserializeEventLog(
                    Encoding.UTF8.GetString(eventLogBytes));
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Event-log JSON is malformed.", exception);
            }

            ValidateEventLog(eventLog, snapshotTick);
            return eventLog.ToArray();
        }

        private static PrototypeRunSummary DeserializeAndValidateRunSummaryPayload(
            byte[] runSummaryBytes)
        {
            ValidatePayloadByteLength(runSummaryBytes, MaximumRunSummaryBytes, "run summary");
            PreflightJson(
                runSummaryBytes,
                maximumArrayItems: PrototypePersistenceBounds.MaximumSnapshotRows,
                maximumObjectProperties: MaximumDictionaryEntries,
                maximumStringBytes: MaximumMessageLength,
                "run summary");
            PrototypeRunSummary runSummary;
            try
            {
                runSummary = PrototypePersistenceService.DeserializeRunSummary(
                    Encoding.UTF8.GetString(runSummaryBytes));
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Run-summary JSON is malformed.", exception);
            }

            ValidateSummaryBounds(runSummary);
            return runSummary;
        }

        private static PrototypeLoadedArtifacts LoadCommittedArtifacts(
            PrototypeArtifactPaths paths,
            PrototypeRuntimeSnapshot snapshot,
            byte[] snapshotBytes)
        {
            if (!File.Exists(paths.GenerationManifestPath))
            {
                throw new InvalidDataException(
                    $"Schema-v{snapshot.SchemaVersion} artifacts are incomplete because the generation manifest is missing.");
            }

            byte[] manifestBytes = ReadBoundedFile(
                paths.GenerationManifestPath,
                MaximumManifestBytes,
                "generation manifest");
            PreflightJson(
                manifestBytes,
                maximumArrayItems: 32,
                maximumObjectProperties: 32,
                maximumStringBytes: MaximumMessageLength,
                "generation manifest");
            if (snapshot.SchemaVersion >= 8)
            {
                ValidateSchemaV8ManifestShape(manifestBytes);
            }
            PrototypeArtifactGenerationManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PrototypeArtifactGenerationManifest>(
                    manifestBytes,
                    ManifestJsonOptions)
                    ?? throw new InvalidDataException("Generation manifest payload is null.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Generation manifest JSON is malformed.", exception);
            }

            ValidateManifest(manifest, paths, snapshot);
            ValidateBinding(manifest.Snapshot!, paths.LegacySnapshotPath, snapshotBytes, "snapshot");

            byte[] eventLogBytes = ReadBoundedFile(
                paths.LegacyEventLogPath,
                MaximumEventLogBytes,
                "event log");
            ValidateBinding(manifest.EventLog!, paths.LegacyEventLogPath, eventLogBytes, "event log");
            PrototypeEventRecord[] eventLog = DeserializeAndValidateEventLogPayload(
                eventLogBytes,
                snapshot.SimulationTick);

            byte[] runSummaryBytes = ReadBoundedFile(
                paths.LegacyRunSummaryPath,
                MaximumRunSummaryBytes,
                "run summary");
            ValidateBinding(manifest.RunSummary!, paths.LegacyRunSummaryPath, runSummaryBytes, "run summary");
            PrototypeRunSummary runSummary = DeserializeAndValidateRunSummaryPayload(
                runSummaryBytes);

            ValidateRunSummary(runSummary, snapshot, eventLog);
            if (manifest.EventCount != eventLog.Length)
            {
                throw new InvalidDataException(
                    "Generation manifest event count does not match the event log.");
            }

            return new PrototypeLoadedArtifacts(snapshot, eventLog, runSummary);
        }

        private static void ValidateManifest(
            PrototypeArtifactGenerationManifest manifest,
            PrototypeArtifactPaths paths,
            PrototypeRuntimeSnapshot snapshot)
        {
            if (manifest.SchemaVersion != GenerationManifestSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported generation manifest schema {manifest.SchemaVersion}; expected {GenerationManifestSchemaVersion}.");
            }

            if (!Guid.TryParseExact(manifest.GenerationId, "N", out _) ||
                manifest.RuntimeSchemaVersion != snapshot.SchemaVersion ||
                manifest.RuntimeSchemaVersion is not (7 or 8 or 9 or 10 or 11 or 12) ||
                !string.Equals(manifest.ScenarioId, snapshot.ScenarioId, StringComparison.Ordinal) ||
                manifest.SimulationTick != snapshot.SimulationTick ||
                manifest.EventCount < 0 ||
                manifest.EventCount > MaximumEventRows ||
                manifest.Snapshot == null ||
                manifest.EventLog == null ||
                manifest.RunSummary == null)
            {
                throw new InvalidDataException("Generation manifest metadata is invalid or does not match the snapshot.");
            }

            ValidateManifestBindingMetadata(manifest.Snapshot, paths.LegacySnapshotPath, MaximumSnapshotBytes);
            ValidateManifestBindingMetadata(manifest.EventLog, paths.LegacyEventLogPath, MaximumEventLogBytes);
            ValidateManifestBindingMetadata(manifest.RunSummary, paths.LegacyRunSummaryPath, MaximumRunSummaryBytes);
        }

        private static void ValidateSchemaV8ManifestShape(byte[] manifestBytes)
        {
            using JsonDocument document = JsonDocument.Parse(manifestBytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Schema-v8 generation manifest must be an object.");
            }

            foreach (string propertyName in RequiredSchemaV8ManifestProperties)
            {
                if (!document.RootElement.TryGetProperty(propertyName, out _))
                {
                    throw new InvalidDataException(
                        $"Schema-v8 generation manifest is missing required property '{propertyName}'.");
                }
            }

            foreach (string bindingName in new[]
            {
                nameof(PrototypeArtifactGenerationManifest.Snapshot),
                nameof(PrototypeArtifactGenerationManifest.EventLog),
                nameof(PrototypeArtifactGenerationManifest.RunSummary)
            })
            {
                JsonElement binding = document.RootElement.GetProperty(bindingName);
                if (binding.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException(
                        $"Schema-v8 generation manifest binding '{bindingName}' must be an object.");
                }

                foreach (string propertyName in RequiredSchemaV8BindingProperties)
                {
                    if (!binding.TryGetProperty(propertyName, out _))
                    {
                        throw new InvalidDataException(
                            $"Schema-v8 generation manifest binding '{bindingName}' is missing required property '{propertyName}'.");
                    }
                }
            }
        }

        private static void ValidateManifestBindingMetadata(
            PrototypeArtifactFileBinding binding,
            string expectedPath,
            long maximumBytes)
        {
            if (!string.Equals(binding.FileName, Path.GetFileName(expectedPath), StringComparison.Ordinal) ||
                binding.ByteLength < 0 ||
                binding.ByteLength > maximumBytes ||
                binding.Sha256 == null ||
                binding.Sha256.Length != 64 ||
                binding.Sha256.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new InvalidDataException(
                    $"Generation manifest binding for '{Path.GetFileName(expectedPath)}' is invalid.");
            }
        }

        private static void ValidateBinding(
            PrototypeArtifactFileBinding binding,
            string expectedPath,
            byte[] bytes,
            string label)
        {
            string actualHash = ComputeSha256(bytes);
            if (binding.ByteLength != bytes.LongLength ||
                !string.Equals(binding.Sha256, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Committed {label} does not belong to the artifact generation.");
            }
        }

        private static void ValidateEventLog(
            IReadOnlyList<PrototypeEventRecord> eventLog,
            long? snapshotTick)
        {
            if (eventLog.Count > MaximumEventRows)
            {
                throw new InvalidDataException($"Event log exceeds the {MaximumEventRows} row limit.");
            }

            long priorTick = -1;
            foreach (PrototypeEventRecord? entry in eventLog)
            {
                if (entry == null)
                {
                    throw new InvalidDataException("Event log contains a null row.");
                }

                if (entry.Tick < 0 ||
                    entry.Tick < priorTick ||
                    (snapshotTick.HasValue && entry.Tick > snapshotTick.Value) ||
                    !IsBoundedNonBlank(entry.EventType, MaximumIdentifierLength) ||
                    !IsBoundedNonBlank(entry.Message, MaximumMessageLength))
                {
                    throw new InvalidDataException("Event log contains an invalid or out-of-order row.");
                }

                priorTick = entry.Tick;
            }
        }

        private static void ValidateRunSummary(
            PrototypeRunSummary summary,
            PrototypeRuntimeSnapshot snapshot,
            IReadOnlyList<PrototypeEventRecord> eventLog)
        {
            ValidateSummaryBounds(summary);
            if (summary.SchemaVersion != snapshot.SchemaVersion ||
                !string.Equals(summary.ScenarioId, snapshot.ScenarioId, StringComparison.Ordinal) ||
                summary.SimulationTick != snapshot.SimulationTick ||
                summary.WorldSeed != snapshot.WorldSeed ||
                summary.SimulationSeed != snapshot.SimulationSeed ||
                !string.Equals(summary.FinalDirective, snapshot.Directive!.DirectiveId, StringComparison.Ordinal) ||
                !DictionaryEqual(summary.ContributionsByResource, snapshot.ContributionCountsByResource) ||
                (snapshot.SchemaVersion >= 8 &&
                    !CivicPoliciesEqual(summary.CivicPolicy, snapshot.CivicPolicy)) ||
                (snapshot.SchemaVersion >= 9 &&
                    !WetlandsEqual(summary.Wetland, snapshot.Wetland)) ||
                (snapshot.SchemaVersion >= 12 &&
                    !PrototypeCausewayState.SnapshotsEqual(summary.Causeway, snapshot.Causeway)))
            {
                throw new InvalidDataException(
                    $"Run summary does not match the schema-v{snapshot.SchemaVersion} snapshot.");
            }

            Dictionary<string, int> actualEventCounts = eventLog
                .GroupBy(entry => entry.EventType, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            if (!DictionaryEqual(summary.EventCountsByType, actualEventCounts))
            {
                throw new InvalidDataException("Run-summary event counts do not match the event log.");
            }

            ValidateCrisisConsistency(snapshot.Crisis, summary, actualEventCounts);
            if (snapshot.SchemaVersion >= 8)
            {
                ValidateCivicPolicyConsistency(snapshot.CivicPolicy!, snapshot.Workers, eventLog);
            }
            if (snapshot.SchemaVersion >= 9)
            {
                ValidateWetlandConsistency(snapshot.CivicPolicy!, snapshot.Wetland!, eventLog);
            }
            if (snapshot.SchemaVersion >= 12)
            {
                IReadOnlyList<PrototypeEventRecord> causewayEvents = eventLog
                    .Where(entry => PrototypeCausewayState.IsCausewayEventType(entry.EventType))
                    .ToArray();
                if (!EventRecordsEqual(summary.CausewayEvents, causewayEvents))
                {
                    throw new InvalidDataException("Run-summary causeway events do not exactly match the authoritative event log.");
                }
                PrototypeCausewayState restoredCauseway =
                    PrototypeCausewayState.PrepareRestore(
                        PrototypeCausewayDefinitionContract.PrepareFromSnapshot(snapshot.Causeway!.Definition),
                        snapshot.Causeway);
                restoredCauseway.ValidateCurrentHour(snapshot.CurrentHour);
                PrototypeCausewayState.ValidateEventCoherence(snapshot.Causeway!, causewayEvents);
            }
        }

        private static void ValidateCrisisConsistency(
            PrototypeCrisisStateSnapshot? crisis,
            PrototypeRunSummary summary,
            IReadOnlyDictionary<string, int> eventCounts)
        {
            int stabilizedEvents = eventCounts.GetValueOrDefault(PrototypeEventTypes.CrisisStabilized, 0);
            int collapsedEvents = eventCounts.GetValueOrDefault(PrototypeEventTypes.CrisisCollapsed, 0);
            if (crisis == null)
            {
                if (summary.CrisisElapsedTicks != 0 ||
                    summary.CrisisDeadlineTicks != 0 ||
                    summary.StabilityHoldTicks != 0 ||
                    summary.CollapseHoldTicks != 0 ||
                    !string.IsNullOrEmpty(summary.CrisisOutcome) ||
                    !string.IsNullOrEmpty(summary.CrisisFailureReason) ||
                    summary.TerminalEventEmitted ||
                    stabilizedEvents != 0 ||
                    collapsedEvents != 0)
                {
                    throw new InvalidDataException("Non-crisis artifacts contain crisis terminal state.");
                }

                return;
            }

            string expectedOutcome = crisis.Outcome.ToString().ToLowerInvariant();
            string expectedFailureReason = crisis.CollapseCause switch
            {
                PrototypeCrisisCollapseCause.None => string.Empty,
                PrototypeCrisisCollapseCause.IncapacitatedHold => "incapacitated_hold",
                PrototypeCrisisCollapseCause.Deadline => "deadline",
                _ => crisis.CollapseCause.ToString().ToLowerInvariant()
            };
            if (summary.CrisisElapsedTicks != crisis.ElapsedTicks ||
                summary.CrisisDeadlineTicks != crisis.DeadlineTicks ||
                summary.StabilityHoldTicks != crisis.StableHoldTicks ||
                summary.CollapseHoldTicks != crisis.CollapseHoldTicks ||
                !string.Equals(summary.CrisisOutcome, expectedOutcome, StringComparison.Ordinal) ||
                !string.Equals(summary.CrisisFailureReason, expectedFailureReason, StringComparison.Ordinal) ||
                summary.TerminalEventEmitted != crisis.TerminalEventEmitted)
            {
                throw new InvalidDataException("Run summary crisis state does not match the snapshot.");
            }

            int expectedStableEvents = crisis.Outcome == PrototypeCrisisOutcome.Stable &&
                crisis.TerminalEventEmitted ? 1 : 0;
            int expectedCollapsedEvents = crisis.Outcome == PrototypeCrisisOutcome.Collapsed &&
                crisis.TerminalEventEmitted ? 1 : 0;
            if (stabilizedEvents != expectedStableEvents || collapsedEvents != expectedCollapsedEvents)
            {
                throw new InvalidDataException(
                    "Crisis terminal event presence does not match the snapshot terminal state.");
            }
        }

        private static void ValidateSnapshotBounds(PrototypeRuntimeSnapshot snapshot)
        {
            if (!IsBoundedNonBlank(snapshot.ScenarioId, MaximumIdentifierLength) ||
                snapshot.WorldHash == null ||
                snapshot.WorldHash.Length > MaximumMessageLength ||
                snapshot.CurrentWeather == null ||
                snapshot.CurrentWeather.Length > MaximumIdentifierLength ||
                snapshot.Inventory == null ||
                snapshot.Stockpile == null ||
                snapshot.Workers == null ||
                snapshot.Resources == null ||
                snapshot.Settlement == null ||
                snapshot.Directive == null ||
                snapshot.ContributionCountsByResource == null ||
                snapshot.Telemetry == null ||
                (snapshot.SchemaVersion >= 8 && snapshot.CivicPolicy == null) ||
                (snapshot.SchemaVersion >= 9 && snapshot.Wetland == null) ||
                (snapshot.SchemaVersion is 10 or 11 or 12 &&
                    (!string.Equals(snapshot.WorldModel, PrototypeWorldModels.Voxel, StringComparison.Ordinal) ||
                     snapshot.VoxelWorld == null)) ||
                (snapshot.SchemaVersion == 12 && snapshot.Causeway == null) ||
                snapshot.Workers.Count > PrototypePersistenceBounds.MaximumSnapshotRows ||
                snapshot.Resources.Count > PrototypePersistenceBounds.MaximumSnapshotRows)
            {
                throw new InvalidDataException("Runtime snapshot exceeds bounded artifact limits.");
            }

            ValidateDictionaryBounds(snapshot.Inventory);
            ValidateDictionaryBounds(snapshot.Stockpile);
            ValidateDictionaryBounds(snapshot.ContributionCountsByResource);

            if (snapshot.SchemaVersion is 10 or 11 or 12)
            {
                PrototypeVoxelSnapshotValidator.ValidateCanonicalShell(snapshot);
                try
                {
                    VoxelWorldModule voxelWorld = VoxelWorldModule.Restore(snapshot.VoxelWorld!);
                    if (voxelWorld.Seed != snapshot.WorldSeed || snapshot.SimulationSeed != snapshot.WorldSeed ||
                        snapshot.WorldGenerationAttempt != 0 ||
                        !string.Equals(voxelWorld.WorldIdentity, snapshot.WorldHash, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("Runtime snapshot voxel identity does not match its outer envelope.");
                    }
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidDataException("Runtime snapshot voxel payload is invalid.", exception);
                }
            }
        }

        private static void ValidateSummaryBounds(PrototypeRunSummary summary)
        {
            string[] strings =
            {
                summary.ScenarioId,
                summary.ScenarioDisplayName,
                summary.SettlementClassification,
                summary.TerrainMode,
                summary.StartTimeText,
                summary.EndTimeText,
                summary.FinalWeather,
                summary.BuildQueueStatus,
                summary.CollapseReason,
                summary.CrisisOutcome,
                summary.CrisisFailureReason,
                summary.FinalDirective,
                summary.CivicPolicy?.PolicyId ?? string.Empty,
                summary.Wetland?.PolicyId ?? string.Empty,
                summary.Wetland?.WetlandHealthBand ?? string.Empty
            };
            if (strings.Any(value => value == null || value.Length > MaximumMessageLength))
            {
                throw new InvalidDataException("Run summary contains an oversized or null string.");
            }

            ValidateDictionaryBounds(summary.BiomeCellCounts);
            ValidateDictionaryBounds(summary.PlayerInventory);
            ValidateDictionaryBounds(summary.Stockpile);
            ValidateDictionaryBounds(summary.RemainingResourcesByType);
            ValidateDictionaryBounds(summary.WorkersByPhase);
            ValidateDictionaryBounds(summary.CraftedItemCounts);
            ValidateDictionaryBounds(summary.EventCountsByType);
            ValidateDictionaryBounds(summary.ProducedResources);
            ValidateDictionaryBounds(summary.ConsumedResources);
            ValidateDictionaryBounds(summary.BlockedReasonCounts);
            ValidateDictionaryBounds(summary.BuiltStructuresByKind);
            ValidateDictionaryBounds(summary.DepotThroughputByDepot);
            ValidateDictionaryBounds(summary.RouteBacklogTicksByKind);
            ValidateDictionaryBounds(summary.ContributionsByResource);
            if (summary.CausewayEvents == null || summary.CausewayEvents.Count > 64)
            {
                throw new InvalidDataException("Run summary contains an invalid causeway event sequence.");
            }
            ValidateEventLog(summary.CausewayEvents, summary.SimulationTick);
        }

        internal static void ValidateStandaloneEventLog(
            IReadOnlyList<PrototypeEventRecord> eventLog)
        {
            ValidateEventLog(eventLog, snapshotTick: null);
        }

        internal static void ValidateStandaloneRunSummary(PrototypeRunSummary summary)
        {
            ValidateSummaryBounds(summary);
            if (summary.SchemaVersion is not (5 or 6 or 7 or 8 or 9 or 10 or 11 or 12) ||
                summary.SimulationTick < 0)
            {
                throw new InvalidDataException(
                    "Run summary has an unsupported schema or negative simulation tick.");
            }

            if (summary.SchemaVersion >= 8)
            {
                PrototypeCivicPolicyState civicPolicy =
                    PrototypeCivicPolicyState.PrepareRestore(summary.CivicPolicy!);
                if (civicPolicy.SelectedTick > summary.SimulationTick)
                {
                    throw new InvalidDataException(
                        "Run-summary civic policy selection tick exceeds the simulation tick.");
                }

                if (summary.SchemaVersion >= 9)
                {
                    _ = PrototypeWetlandState.PrepareRestore(summary.Wetland!, civicPolicy);
                }
            }

            if (summary.SchemaVersion == 12)
            {
                if (summary.Causeway == null)
                {
                    throw new InvalidDataException("Schema-v12 run summary is missing causeway state.");
                }
                PrototypeCausewayState restoredCauseway =
                    PrototypeCausewayState.PrepareRestore(
                        PrototypeCausewayDefinitionContract.PrepareFromSnapshot(summary.Causeway.Definition),
                        summary.Causeway);
                restoredCauseway.ValidateCurrentHour(summary.EndHour);
                PrototypeCausewayState.ValidateEventCoherence(summary.Causeway, summary.CausewayEvents);
            }
            else if (summary.Causeway != null || summary.CausewayEvents.Count != 0)
            {
                throw new InvalidDataException("Legacy run summary contains causeway state.");
            }
        }

        private static bool EventRecordsEqual(
            IReadOnlyList<PrototypeEventRecord> first,
            IReadOnlyList<PrototypeEventRecord> second)
        {
            if (first.Count != second.Count) return false;
            for (int index = 0; index < first.Count; index++)
            {
                if (first[index].Tick != second[index].Tick ||
                    !string.Equals(first[index].EventType, second[index].EventType, StringComparison.Ordinal) ||
                    !string.Equals(first[index].Message, second[index].Message, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool CivicPoliciesEqual(
            PrototypeCivicPolicySnapshot? first,
            PrototypeCivicPolicySnapshot? second)
        {
            return first != null && second != null &&
                string.Equals(first.PolicyId, second.PolicyId, StringComparison.Ordinal) &&
                first.SelectedTick == second.SelectedTick &&
                first.Version == second.Version &&
                first.WindowStartTick == second.WindowStartTick &&
                first.WindowEndTick == second.WindowEndTick;
        }

        private static bool WetlandsEqual(
            PrototypeWetlandSnapshot? first,
            PrototypeWetlandSnapshot? second)
        {
            return first != null && second != null &&
                string.Equals(first.PolicyId, second.PolicyId, StringComparison.Ordinal) &&
                first.PolicySelectedTick == second.PolicySelectedTick &&
                first.PolicyVersion == second.PolicyVersion &&
                first.ReedQuotaLimit == second.ReedQuotaLimit &&
                first.ReedQuotaConsumed == second.ReedQuotaConsumed &&
                first.WetlandHealth == second.WetlandHealth &&
                string.Equals(first.WetlandHealthBand, second.WetlandHealthBand, StringComparison.Ordinal);
        }

        private static void ValidateCivicPolicyConsistency(
            PrototypeCivicPolicySnapshot civicPolicy,
            IReadOnlyList<PrototypeWorkerSnapshot> workers,
            IReadOnlyList<PrototypeEventRecord> eventLog)
        {
            PrototypeCivicPolicyState restored = PrototypeCivicPolicyState.PrepareRestore(civicPolicy);
            PrototypeEventRecord[] selectionEvents = eventLog
                .Where(entry => string.Equals(
                    entry.EventType,
                    PrototypeEventTypes.CivicPolicySelected,
                    StringComparison.Ordinal))
                .ToArray();
            int expectedCount = restored.Policy == PrototypeCivicPolicy.Neutral ? 0 : 1;
            if (selectionEvents.Length != expectedCount)
            {
                throw new InvalidDataException(
                    "Civic policy selection event presence does not match the snapshot state.");
            }

            PrototypeEventRecord[] preferenceSummaryEvents = eventLog
                .Where(entry => string.Equals(
                    entry.EventType,
                    PrototypeEventTypes.CivicPreferenceSummary,
                    StringComparison.Ordinal))
                .ToArray();
            if (expectedCount == 0 && preferenceSummaryEvents.Length != 0 || preferenceSummaryEvents.Length > 1)
            {
                throw new InvalidDataException(
                    "Civic preference summary presence does not match the snapshot state.");
            }

            int selectionIndex = -1;
            if (expectedCount == 1)
            {
                PrototypeEventRecord selection = selectionEvents[0];
                if (selection.Tick != restored.SelectedTick ||
                    !string.Equals(
                        selection.Message,
                        PrototypeCivicPolicyCatalog.BuildSelectionMessage(restored.Policy),
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Civic policy selection event does not match the snapshot state.");
                }

                for (int index = 0; index < eventLog.Count; index++)
                {
                    if (ReferenceEquals(eventLog[index], selection))
                    {
                        selectionIndex = index;
                        break;
                    }
                }
            }

            if (expectedCount == 1 && preferenceSummaryEvents.Length == 1)
            {
                PrototypeEventRecord selection = selectionEvents[0];
                PrototypeEventRecord preferenceSummary = preferenceSummaryEvents[0];
                if (selectionIndex < 0 || selectionIndex + 1 >= eventLog.Count ||
                    !ReferenceEquals(eventLog[selectionIndex + 1], preferenceSummary) ||
                    preferenceSummary.Tick != selection.Tick)
                {
                    throw new InvalidDataException(
                        "Civic policy selection event is missing its ordered preference summary companion.");
                }

                ValidateCanonicalPreferenceSummary(preferenceSummary.Message, workers.Count);
            }
        }

        private static void ValidateWetlandConsistency(
            PrototypeCivicPolicySnapshot civicPolicy,
            PrototypeWetlandSnapshot wetland,
            IReadOnlyList<PrototypeEventRecord> eventLog)
        {
            PrototypeCivicPolicyState restoredCivic = PrototypeCivicPolicyState.PrepareRestore(civicPolicy);
            _ = PrototypeWetlandState.PrepareRestore(wetland, restoredCivic);
            List<int> wetlandEventIndexes = new();
            for (int index = 0; index < eventLog.Count; index++)
            {
                string eventType = eventLog[index].EventType;
                if (eventType == PrototypeEventTypes.CivicWetlandQuotaApplied ||
                    eventType == PrototypeEventTypes.CivicWetlandQuotaConsumed ||
                    eventType == PrototypeEventTypes.CivicWetlandTransition)
                {
                    wetlandEventIndexes.Add(index);
                }
            }

            if (restoredCivic.Policy == PrototypeCivicPolicy.Neutral)
            {
                if (wetlandEventIndexes.Count != 0)
                {
                    throw new InvalidDataException("Neutral civic artifacts cannot contain wetland consequence events.");
                }

                return;
            }

            PrototypeWetlandState reconstructed = PrototypeWetlandState.CreateForSelection(
                restoredCivic.Policy,
                restoredCivic.SelectedTick!.Value,
                restoredCivic.Version);
            PrototypeWetlandSnapshot selectedWetland = reconstructed.CaptureSnapshot();
            int selectionIndex = FindSingleEventIndex(eventLog, PrototypeEventTypes.CivicPolicySelected);
            int selectionSequenceEndIndex = -1;
            int expectedHarvestTransitionIndex = -1;
            int quotaAppliedCount = eventLog.Count(entry =>
                entry.EventType == PrototypeEventTypes.CivicWetlandQuotaApplied);
            if (quotaAppliedCount > 1)
            {
                throw new InvalidDataException("Wetland quota application event is duplicated.");
            }

            if (quotaAppliedCount == 1)
            {
                int quotaAppliedIndex = FindSingleEventIndex(
                    eventLog,
                    PrototypeEventTypes.CivicWetlandQuotaApplied);
                if (selectionIndex < 0 || selectionIndex + 3 >= eventLog.Count ||
                    eventLog[selectionIndex + 1].EventType != PrototypeEventTypes.CivicPreferenceSummary ||
                    quotaAppliedIndex != selectionIndex + 2 ||
                    eventLog[selectionIndex + 3].EventType != PrototypeEventTypes.CivicWetlandTransition)
                {
                    throw new InvalidDataException(
                        "Wetland selection events are not in canonical policy, preference, quota, transition order.");
                }

                PrototypeEventRecord quotaApplied = eventLog[quotaAppliedIndex];
                PrototypeEventRecord selectionTransition = eventLog[selectionIndex + 3];
                if (quotaApplied.Tick != restoredCivic.SelectedTick ||
                    selectionTransition.Tick != restoredCivic.SelectedTick ||
                    quotaApplied.Message != PrototypeWetlandCatalog.BuildQuotaAppliedMessage(selectedWetland) ||
                    selectionTransition.Message != PrototypeWetlandCatalog.BuildTransitionMessage(
                        "policy_selection",
                        PrototypeWetlandCatalog.NeutralHealth,
                        PrototypeWetlandHealthBand.Strained,
                        selectedWetland))
                {
                    throw new InvalidDataException("Wetland selection events do not match the selected policy state.");
                }

                selectionSequenceEndIndex = selectionIndex + 3;
            }
            else
            {
                selectionSequenceEndIndex = selectionIndex;
                if (selectionIndex + 1 < eventLog.Count &&
                    eventLog[selectionIndex + 1].EventType == PrototypeEventTypes.CivicPreferenceSummary)
                {
                    selectionSequenceEndIndex++;
                }
            }

            for (int index = 0; index < eventLog.Count; index++)
            {
                PrototypeEventRecord entry = eventLog[index];
                if (entry.EventType == PrototypeEventTypes.CivicWetlandQuotaApplied)
                {
                    continue;
                }

                if (entry.EventType == PrototypeEventTypes.CivicWetlandTransition)
                {
                    if (index != selectionSequenceEndIndex && index != expectedHarvestTransitionIndex)
                    {
                        throw new InvalidDataException("Wetland transition event is not paired with its canonical cause.");
                    }

                    continue;
                }

                if (entry.EventType != PrototypeEventTypes.CivicWetlandQuotaConsumed)
                {
                    continue;
                }

                if (selectionSequenceEndIndex < 0 || index <= selectionSequenceEndIndex ||
                    index == 0 || entry.Tick != eventLog[index - 1].Tick ||
                    !TryParseReedHarvestSuccess(eventLog[index - 1], out int amount) ||
                    !reconstructed.CanApplyHarvest(PrototypeWetlandCatalog.ReedResourceId, amount))
                {
                    throw new InvalidDataException(
                        "Wetland quota consumption is not paired with a successful bounded reed harvest.");
                }

                PrototypeWetlandTransition transition = reconstructed.CommitSuccessfulReedHarvest(amount);
                PrototypeWetlandSnapshot current = reconstructed.CaptureSnapshot();
                if (entry.Message != PrototypeWetlandCatalog.BuildQuotaConsumedMessage(current, amount))
                {
                    throw new InvalidDataException("Wetland quota consumption event is not in canonical form.");
                }

                if (transition.BandChanged)
                {
                    if (index + 1 >= eventLog.Count ||
                        eventLog[index + 1].EventType != PrototypeEventTypes.CivicWetlandTransition ||
                        eventLog[index + 1].Tick != entry.Tick ||
                        eventLog[index + 1].Message != PrototypeWetlandCatalog.BuildTransitionMessage(
                            "reed_harvest",
                            transition.PreviousHealth,
                            transition.PreviousBand,
                            current))
                    {
                        throw new InvalidDataException(
                            "Wetland band change is missing its canonical transition event.");
                    }

                    expectedHarvestTransitionIndex = index + 1;
                }
            }

            if (!WetlandsEqual(reconstructed.CaptureSnapshot(), wetland))
            {
                throw new InvalidDataException(
                    "Wetland consequence events do not reconstruct the snapshot state.");
            }
        }

        private static int FindSingleEventIndex(
            IReadOnlyList<PrototypeEventRecord> eventLog,
            string eventType)
        {
            int found = -1;
            for (int index = 0; index < eventLog.Count; index++)
            {
                if (eventLog[index].EventType != eventType)
                {
                    continue;
                }

                if (found >= 0)
                {
                    throw new InvalidDataException($"Event type '{eventType}' is duplicated.");
                }

                found = index;
            }

            return found;
        }

        private static bool TryParseReedHarvestSuccess(
            PrototypeEventRecord harvest,
            out int amount)
        {
            amount = 0;
            string amountText;
            if (harvest.EventType == PrototypeEventTypes.PlayerHarvestSucceeded)
            {
                string prefix = $"Harvested {InventoryComponent.FormatItemName(PrototypeWetlandCatalog.ReedResourceId)} x";
                if (!harvest.Message.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return false;
                }

                amountText = harvest.Message[prefix.Length..];
            }
            else if (harvest.EventType == PrototypeEventTypes.AiHarvestSucceeded)
            {
                string marker = $" harvested {PrototypeWetlandCatalog.ReedResourceId} x";
                int markerIndex = harvest.Message.LastIndexOf(marker, StringComparison.Ordinal);
                if (markerIndex <= 0)
                {
                    return false;
                }

                amountText = harvest.Message[(markerIndex + marker.Length)..];
            }
            else
            {
                return false;
            }

            return amountText.Length > 0 &&
                (amountText.Length == 1 || amountText[0] != '0') &&
                int.TryParse(amountText, NumberStyles.None, CultureInfo.InvariantCulture, out amount) &&
                amount > 0;
        }

        private static void ValidateCanonicalPreferenceSummary(string message, int workerCount)
        {
            string[] prefixes =
            {
                "Civic preferences: protect=",
                "; draw_down=",
                "; reasons=critical_nutrition=",
                ",critical_fatigue=",
                ",food_security=",
                ",recovery_need=",
                ",future_reed_supply=",
                ",balanced_long_term_supply=",
                ",immediate_shelter_supply=",
                ",immediate_material_supply=",
                ",material_throughput="
            };
            int[] values = new int[prefixes.Length];
            int index = 0;
            for (int valueIndex = 0; valueIndex < prefixes.Length; valueIndex++)
            {
                string prefix = prefixes[valueIndex];
                if (!message.AsSpan(index).StartsWith(prefix, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Civic preference summary is not in the canonical format.");
                }

                index += prefix.Length;
                int numberStart = index;
                while (index < message.Length && message[index] is >= '0' and <= '9')
                {
                    index++;
                }

                if (numberStart == index ||
                    (index - numberStart > 1 && message[numberStart] == '0') ||
                    !int.TryParse(
                        message.AsSpan(numberStart, index - numberStart),
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out values[valueIndex]))
                {
                    throw new InvalidDataException("Civic preference summary contains an invalid count.");
                }
            }

            if (index != message.Length)
            {
                throw new InvalidDataException("Civic preference summary is not in the canonical format.");
            }

            long protectCount = values[0];
            long drawDownCount = values[1];
            long reasonCount = 0;
            for (int valueIndex = 2; valueIndex < values.Length; valueIndex++)
            {
                reasonCount += values[valueIndex];
            }

            long expectedWorkerCount = workerCount;
            long protectReasonCount = values[6] + (long)values[7];
            long drawDownReasonCount = reasonCount - protectReasonCount;
            if (protectCount + drawDownCount != expectedWorkerCount ||
                reasonCount != expectedWorkerCount ||
                protectCount != protectReasonCount ||
                drawDownCount != drawDownReasonCount)
            {
                throw new InvalidDataException("Civic preference summary counts do not match the snapshot worker count.");
            }
        }

        internal static void ValidateStandaloneWorldSummary(PrototypeWorldSummary summary)
        {
            string[] strings =
            {
                summary.ScenarioId,
                summary.ScenarioDisplayName,
                summary.TerrainMode,
                summary.WorldHash
            };
            if (summary.SchemaVersion is not (1 or 2 or 3) ||
                summary.SimulationTick < 0 ||
                strings.Any(value => value == null || value.Length > MaximumMessageLength) ||
                !float.IsFinite(summary.WorldSize) ||
                summary.WorldSize < 0.0f ||
                !float.IsFinite(summary.GroundHeight) ||
                !float.IsFinite(summary.CellSizeMeters) ||
                summary.CellSizeMeters < 0.0f ||
                !float.IsFinite(summary.BuildableCellRatio) ||
                summary.BuildableCellRatio < 0.0f ||
                summary.BuildableCellRatio > 1.0f ||
                !float.IsFinite(summary.MeanElevation) ||
                !float.IsFinite(summary.MaxElevation) ||
                !float.IsFinite(summary.AverageMovementCost) ||
                summary.AverageMovementCost < 0.0f ||
                summary.GridWidth < 0 ||
                summary.GridHeight < 0 ||
                summary.BuildableCellCount < 0 ||
                summary.WorkerCount < 0)
            {
                throw new InvalidDataException("World summary contains invalid bounded state.");
            }

            ValidateDictionaryBounds(summary.BiomeCellCounts);
            ValidateDictionaryBounds(summary.StarterResourceDistances);
            ValidateDictionaryBounds(summary.AverageClusterDistances);
            ValidateDictionaryBounds(summary.ResourceNodeCounts);
            ValidateDictionaryBounds(summary.RemainingResourceUnits);
            if (summary.BiomeCellCounts.Values.Any(value => value < 0) ||
                summary.StarterResourceDistances.Values.Any(value =>
                    !float.IsFinite(value) || value < 0.0f) ||
                summary.AverageClusterDistances.Values.Any(value =>
                    !float.IsFinite(value) || value < 0.0f) ||
                summary.ResourceNodeCounts.Values.Any(value => value < 0) ||
                summary.RemainingResourceUnits.Values.Any(value => value < 0))
            {
                throw new InvalidDataException("World summary dictionaries contain invalid values.");
            }
        }

        private static void ValidateDictionaryBounds<T>(IReadOnlyDictionary<string, T> values)
        {
            if (values == null ||
                values.Count > MaximumDictionaryEntries ||
                values.Keys.Any(key => !IsBoundedNonBlank(key, MaximumIdentifierLength)))
            {
                throw new InvalidDataException("Artifact dictionary exceeds bounded key or row limits.");
            }
        }

        private static bool DictionaryEqual<T>(
            IReadOnlyDictionary<string, T> left,
            IReadOnlyDictionary<string, T> right)
            where T : IEquatable<T>
        {
            return left != null &&
                right != null &&
                left.Count == right.Count &&
                left.All(pair => right.TryGetValue(pair.Key, out T? value) && pair.Value.Equals(value));
        }

        private static bool IsBoundedNonBlank(string value, int maximumLength)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
        }

        private static PrototypeArtifactFileBinding BuildBinding(string path, byte[] bytes)
        {
            return new PrototypeArtifactFileBinding
            {
                FileName = Path.GetFileName(path),
                ByteLength = bytes.LongLength,
                Sha256 = ComputeSha256(bytes)
            };
        }

        private static string ComputeSha256(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        internal static void ValidatePayloadByteLength(
            byte[] bytes,
            long maximumBytes,
            string label)
        {
            if (bytes.LongLength > maximumBytes)
            {
                throw new InvalidDataException(
                    $"{label} exceeds the {maximumBytes} byte limit.");
            }
        }

        internal static byte[] ReadBoundedFile(string path, long maximumBytes, string label)
        {
            if (!File.Exists(path))
            {
                throw new InvalidDataException($"{label} artifact is missing.");
            }

            using FileStream stream = new(
                path,
                FileMode.Open,
                System.IO.FileAccess.Read,
                FileShare.Read);
            if (stream.Length > maximumBytes)
            {
                throw new InvalidDataException(
                    $"{label} exceeds the {maximumBytes} byte limit.");
            }

            int boundedCapacity = checked((int)maximumBytes + 1);
            byte[] boundedBuffer = new byte[boundedCapacity];
            int totalRead = 0;
            while (totalRead < boundedBuffer.Length)
            {
                int read = stream.Read(
                    boundedBuffer,
                    totalRead,
                    boundedBuffer.Length - totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (totalRead > maximumBytes || stream.ReadByte() != -1)
            {
                throw new InvalidDataException(
                    $"{label} exceeds the {maximumBytes} byte limit.");
            }

            return boundedBuffer.AsSpan(0, totalRead).ToArray();
        }

        internal static void PreflightJson(
            byte[] bytes,
            int maximumArrayItems,
            int maximumObjectProperties,
            int maximumStringBytes,
            string label)
        {
            Stack<JsonContainerCounter> containers = new();
            try
            {
                Utf8JsonReader reader = new(
                    bytes,
                    new JsonReaderOptions
                    {
                        MaxDepth = 64,
                        CommentHandling = JsonCommentHandling.Disallow
                    });
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartArray:
                            CountParentArrayItem(containers, maximumArrayItems, label);
                            containers.Push(new JsonContainerCounter(isArray: true));
                            break;
                        case JsonTokenType.StartObject:
                            CountParentArrayItem(containers, maximumArrayItems, label);
                            containers.Push(new JsonContainerCounter(isArray: false));
                            break;
                        case JsonTokenType.EndArray:
                        case JsonTokenType.EndObject:
                            if (containers.Count == 0)
                            {
                                throw new InvalidDataException($"{label} JSON containers are unbalanced.");
                            }

                            containers.Pop();
                            break;
                        case JsonTokenType.PropertyName:
                            if (containers.Count == 0 || containers.Peek().IsArray)
                            {
                                throw new InvalidDataException($"{label} JSON property is outside an object.");
                            }

                            JsonContainerCounter objectCounter = containers.Pop();
                            objectCounter.Count++;
                            if (objectCounter.Count > maximumObjectProperties)
                            {
                                throw new InvalidDataException(
                                    $"{label} JSON object exceeds the {maximumObjectProperties} property limit.");
                            }
                            containers.Push(objectCounter);
                            ValidateJsonStringLength(reader, maximumStringBytes, label);
                            break;
                        case JsonTokenType.String:
                            CountParentArrayItem(containers, maximumArrayItems, label);
                            ValidateJsonStringLength(reader, maximumStringBytes, label);
                            break;
                        case JsonTokenType.Number:
                        case JsonTokenType.True:
                        case JsonTokenType.False:
                        case JsonTokenType.Null:
                            CountParentArrayItem(containers, maximumArrayItems, label);
                            break;
                    }
                }

                if (containers.Count != 0)
                {
                    throw new InvalidDataException($"{label} JSON containers are unbalanced.");
                }
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"{label} JSON is malformed or too deeply nested.", exception);
            }
        }

        private static void CountParentArrayItem(
            Stack<JsonContainerCounter> containers,
            int maximumArrayItems,
            string label)
        {
            if (containers.Count == 0 || !containers.Peek().IsArray)
            {
                return;
            }

            JsonContainerCounter arrayCounter = containers.Pop();
            arrayCounter.Count++;
            if (arrayCounter.Count > maximumArrayItems)
            {
                throw new InvalidDataException(
                    $"{label} JSON array exceeds the {maximumArrayItems} item limit.");
            }
            containers.Push(arrayCounter);
        }

        private static void ValidateJsonStringLength(
            Utf8JsonReader reader,
            int maximumStringBytes,
            string label)
        {
            long byteLength = reader.HasValueSequence
                ? reader.ValueSequence.Length
                : reader.ValueSpan.Length;
            if (byteLength > maximumStringBytes)
            {
                throw new InvalidDataException(
                    $"{label} JSON string exceeds the {maximumStringBytes} byte limit.");
            }
        }

        internal static void AtomicWrite(string path, byte[] bytes, string generationId)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = $"{path}.{generationId}.tmp";
            try
            {
                File.WriteAllBytes(temporaryPath, bytes);
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static byte[]? BuildRuntimeMetricsCsv(RuntimeMetricsCollector? runtimeMetrics)
        {
            if (runtimeMetrics == null)
            {
                return null;
            }

            using StringWriter writer = new(CultureInfo.InvariantCulture);
            runtimeMetrics.WriteCsv(writer);
            return Encoding.UTF8.GetBytes(writer.ToString());
        }

        private static void SaveRuntimeMetricsBestEffort(
            string path,
            string legacyPath,
            string olderLegacyPath,
            byte[]? runtimeMetricsCsvBytes,
            string generationId)
        {
            try
            {
                if (runtimeMetricsCsvBytes != null)
                {
                    AtomicWrite(path, runtimeMetricsCsvBytes, generationId);
                    DeleteFileBestEffort(legacyPath);
                    DeleteFileBestEffort(olderLegacyPath);
                    return;
                }

                DeleteFileBestEffort(path);
                DeleteFileBestEffort(legacyPath);
                DeleteFileBestEffort(olderLegacyPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(
                    $"WARNING: Optional runtime metrics artifact '{path}' was not updated: {exception.Message}");
            }
        }

        private static void DeleteFileBestEffort(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(
                    $"WARNING: Optional runtime metrics artifact '{path}' was not removed: {exception.Message}");
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
        public string GenerationManifestPath => Path.Combine(
            Path.GetDirectoryName(LegacySnapshotPath) ?? string.Empty,
            "artifact-generation-v1.json");

        public string RuntimeMetricsCsvPath => Path.Combine(
            Path.GetDirectoryName(LegacySnapshotPath) ?? string.Empty,
            "runtime-batch-metrics-v6.csv");

        public string LegacyRuntimeMetricsCsvPath => Path.Combine(
            Path.GetDirectoryName(LegacySnapshotPath) ?? string.Empty,
            "runtime-batch-metrics-v5.csv");

        public string OlderLegacyRuntimeMetricsCsvPath => Path.Combine(
            Path.GetDirectoryName(LegacySnapshotPath) ?? string.Empty,
            "runtime-batch-metrics-v4.csv");
    }

    public readonly record struct PrototypeLoadedArtifacts(
        PrototypeRuntimeSnapshot Snapshot,
        IReadOnlyList<PrototypeEventRecord> EventLog,
        PrototypeRunSummary? RunSummary);

    internal sealed class PrototypeArtifactGenerationManifest
    {
        public int SchemaVersion { get; set; }

        public string GenerationId { get; set; } = string.Empty;

        public int RuntimeSchemaVersion { get; set; }

        public string ScenarioId { get; set; } = string.Empty;

        public long SimulationTick { get; set; }

        public int EventCount { get; set; }

        public PrototypeArtifactFileBinding? Snapshot { get; set; }

        public PrototypeArtifactFileBinding? EventLog { get; set; }

        public PrototypeArtifactFileBinding? RunSummary { get; set; }
    }

    internal sealed class PrototypeArtifactFileBinding
    {
        public string FileName { get; set; } = string.Empty;

        public long ByteLength { get; set; }

        public string Sha256 { get; set; } = string.Empty;
    }

    internal struct JsonContainerCounter
    {
        public JsonContainerCounter(bool isArray)
        {
            IsArray = isArray;
            Count = 0;
        }

        public bool IsArray { get; }

        public int Count { get; set; }
    }
}
