using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Societies.Core
{
    /// <summary>
    /// Snapshot and event-log contracts for prototype validation runs.
    /// </summary>
    public sealed class PrototypeRuntimeSnapshot
    {
        public int SchemaVersion { get; set; } = 9;

        public string ScenarioId { get; set; } = string.Empty;

        public int WorldSeed { get; set; }

        public int WorldGenerationAttempt { get; set; }

        public string WorldHash { get; set; } = string.Empty;

        public int SimulationSeed { get; set; }

        public long SimulationTick { get; set; }

        public float CurrentHour { get; set; }

        public string CurrentWeather { get; set; } = "Clear";

        public float TimeUntilNextWeatherShift { get; set; }

        public uint WeatherRandomState { get; set; }

        public PrototypeSerializableVector3 PlayerPosition { get; set; }

        public PrototypeSerializableVector3 SettlementAnchorPosition { get; set; }

        public Dictionary<string, int> Inventory { get; set; } = new();

        public Dictionary<string, int> Stockpile { get; set; } = new();

        public List<PrototypeWorkerSnapshot> Workers { get; set; } = new();

        public List<PrototypeResourceSnapshot> Resources { get; set; } = new();

        public PrototypeSettlementSnapshot? Settlement { get; set; } = new();

        public PrototypeDirectiveSnapshot? Directive { get; set; } = new();

        public Dictionary<string, long> ContributionCountsByResource { get; set; } = new();

        public PrototypeCrisisStateSnapshot? Crisis { get; set; }

        public PrototypeRuntimeTelemetrySnapshot? Telemetry { get; set; } = new();

        public PrototypeCivicPolicySnapshot? CivicPolicy { get; set; } = new();

        public PrototypeWetlandSnapshot? Wetland { get; set; } = new();

        public string WorldModel { get; set; } = "heightfield_v1";

        public VoxelWorldSnapshot? VoxelWorld { get; set; }

        /// <summary>Schema-v11 authoritative placed-piece state; absent in terrain-only v10 saves.</summary>
        public WorldcraftConstructionSnapshot? Construction { get; set; }
    }

    /// <summary>
    /// Frozen schema-v7 directive payload.
    /// </summary>
    public sealed class PrototypeDirectiveSnapshot
    {
        public string DirectiveId { get; set; } = "neutral";
    }

    /// <summary>
    /// Compact, bounded telemetry required to continue a schema-v7 run exactly.
    /// It records aggregate facts and transition counts, never a per-tick narrative.
    /// </summary>
    public sealed class PrototypeRuntimeTelemetrySnapshot
    {
        public long? FirstDirectiveTick { get; set; }

        public long? FirstContributionTick { get; set; }

        public int DirectiveChanges { get; set; }

        public string FinalDirectiveId { get; set; } = "neutral";

        public bool HasCrisisObservation { get; set; }

        public int PeakIncapacitatedCitizens { get; set; }

        public int MinimumMeals { get; set; }

        public int MinimumHearthFuel { get; set; }

        public int MaximumBedCoveragePercent { get; set; }

        public int FinalCapableCitizens { get; set; }

        public int FinalIncapacitatedCitizens { get; set; }

        public int FinalMeals { get; set; }

        public int FinalHearthFuel { get; set; }

        public int FinalBedCoveragePercent { get; set; }

        public int StabilityHoldEntries { get; set; }

        public int StabilityHoldBreaks { get; set; }

        public int CollapseHoldEntries { get; set; }

        public int CollapseHoldBreaks { get; set; }
    }

    public sealed class PrototypeResourceSnapshot
    {
        public string SiteId { get; set; } = string.Empty;

        public string ResourceId { get; set; } = string.Empty;

        public int UnitsRemaining { get; set; }

        public PrototypeSerializableVector3 Position { get; set; }

        public string ClusterId { get; set; } = string.Empty;
    }

    public sealed class PrototypeWorkerSnapshot
    {
        public string WorkerId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string PreferredResourceId { get; set; } = string.Empty;

        public string RoleId { get; set; } = string.Empty;

        public string Phase { get; set; } = string.Empty;

        public string TargetResourceNodeName { get; set; } = string.Empty;

        public string TargetStructureId { get; set; } = string.Empty;

        public string SourceStoreId { get; set; } = string.Empty;

        public string DestinationStoreId { get; set; } = string.Empty;

        public string CarryItemId { get; set; } = string.Empty;

        public int CarryAmount { get; set; }

        public int TicksRemaining { get; set; }

        public int PhaseDurationTicks { get; set; }

        public PrototypeSerializableVector3 Position { get; set; }

        public PrototypeSerializableVector3 HomePosition { get; set; }

        public PrototypeSerializableVector3 TargetPosition { get; set; }

        public string TargetLabel { get; set; } = string.Empty;

        public string ActivityText { get; set; } = string.Empty;

        public float Nutrition { get; set; } = 100.0f;

        public float Fatigue { get; set; }

        public string LastFailureReason { get; set; } = string.Empty;

        public string CurrentOrderId { get; set; } = string.Empty;

        public string CurrentOrderKind { get; set; } = string.Empty;

        public string CurrentOrderReason { get; set; } = string.Empty;

        public int HomeBedCapacity { get; set; }

        public List<string> RecentEvents { get; set; } = new();

        public int TravelTicksAccumulated { get; set; }

        public int WorkTicksAccumulated { get; set; }

        public float CurrentRouteLengthMeters { get; set; }

        public float CurrentRouteCost { get; set; }

        public int CurrentRouteTravelTicks { get; set; }

        public int CurrentWaypointIndex { get; set; }

        public int CachedRouteVersion { get; set; }

        public int RouteSourceGridX { get; set; }

        public int RouteSourceGridY { get; set; }

        public int RouteDestinationGridX { get; set; }

        public int RouteDestinationGridY { get; set; }

        public List<PrototypeSerializableVector3> RouteWaypoints { get; set; } = new();
    }

    public sealed class PrototypeEventLog
    {
        private readonly List<PrototypeEventRecord> _entries = new();

        public IReadOnlyList<PrototypeEventRecord> Entries => _entries;

        public void Clear()
        {
            _entries.Clear();
        }

        public void Record(long tick, string eventType, string message)
        {
            _entries.Add(new PrototypeEventRecord
            {
                Tick = tick,
                EventType = eventType,
                Message = message
            });
        }

        public void ReplaceEntries(IEnumerable<PrototypeEventRecord> entries)
        {
            _entries.Clear();
            _entries.AddRange(entries);
        }
    }

    public sealed class PrototypeEventRecord
    {
        public long Tick { get; set; }

        public string EventType { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    public sealed class PrototypeRunSummary
    {
        public int SchemaVersion { get; set; } = 9;

        public string ScenarioId { get; set; } = string.Empty;

        public string ScenarioDisplayName { get; set; } = string.Empty;

        public string SettlementClassification { get; set; } = string.Empty;

        public int WorldSeed { get; set; }

        public string TerrainMode { get; set; } = string.Empty;

        public float BuildableCellRatio { get; set; }

        public Dictionary<string, int> BiomeCellCounts { get; set; } = new();

        public int SimulationSeed { get; set; }

        public long SimulationTick { get; set; }

        public float StartHour { get; set; }

        public string StartTimeText { get; set; } = string.Empty;

        public float EndHour { get; set; }

        public string EndTimeText { get; set; } = string.Empty;

        public string FinalWeather { get; set; } = string.Empty;

        public Dictionary<string, int> PlayerInventory { get; set; } = new();

        public Dictionary<string, int> Stockpile { get; set; } = new();

        public Dictionary<string, int> RemainingResourcesByType { get; set; } = new();

        public Dictionary<string, int> WorkersByPhase { get; set; } = new();

        public Dictionary<string, int> CraftedItemCounts { get; set; } = new();

        public Dictionary<string, int> EventCountsByType { get; set; } = new();

        public Dictionary<string, int> ProducedResources { get; set; } = new();

        public Dictionary<string, int> ConsumedResources { get; set; } = new();

        public Dictionary<string, int> BlockedReasonCounts { get; set; } = new();

        public Dictionary<string, int> BuiltStructuresByKind { get; set; } = new();

        public int MealCoveragePercent { get; set; }

        public int BedCoveragePercent { get; set; }

        public int HearthFuel { get; set; }

        public int HearthLitTicks { get; set; }

        public string BuildQueueStatus { get; set; } = string.Empty;

        public string CollapseReason { get; set; } = string.Empty;

        public float AverageRouteLengthMeters { get; set; }

        public float AverageTravelWorkRatio { get; set; }

        public float PathCoverageRatio { get; set; }

        public Dictionary<string, int> DepotThroughputByDepot { get; set; } = new();

        public Dictionary<string, int> RouteBacklogTicksByKind { get; set; } = new();

        public int CrisisElapsedTicks { get; set; }

        public int CrisisDeadlineTicks { get; set; }

        public double CrisisElapsedSeconds { get; set; }

        public int StabilityHoldTicks { get; set; }

        public int CollapseHoldTicks { get; set; }

        public string CrisisOutcome { get; set; } = string.Empty;

        public string CrisisFailureReason { get; set; } = string.Empty;

        public bool TerminalEventEmitted { get; set; }

        public long? FirstDirectiveTick { get; set; }

        public long? FirstContributionTick { get; set; }

        public int DirectiveChanges { get; set; }

        public string FinalDirective { get; set; } = "neutral";

        public Dictionary<string, long> ContributionsByResource { get; set; } = new();

        public int PeakIncapacitatedCitizens { get; set; }

        public int MinimumMeals { get; set; }

        public int MinimumHearthFuel { get; set; }

        public int MaximumBedCoveragePercent { get; set; }

        public int FinalCapableCitizens { get; set; }

        public int FinalIncapacitatedCitizens { get; set; }

        public int FinalMeals { get; set; }

        public int FinalHearthFuel { get; set; }

        public int FinalBedCoveragePercent { get; set; }

        public int StabilityHoldEntries { get; set; }

        public int StabilityHoldBreaks { get; set; }

        public int CollapseHoldEntries { get; set; }

        public int CollapseHoldBreaks { get; set; }

        public PrototypeCivicPolicySnapshot? CivicPolicy { get; set; } = new();

        public PrototypeWetlandSnapshot? Wetland { get; set; } = new();
    }

    public struct PrototypeSerializableVector3
    {
        public float X { get; set; }

        public float Y { get; set; }

        public float Z { get; set; }

        public static PrototypeSerializableVector3 FromVector3(Vector3 value)
        {
            return new PrototypeSerializableVector3
            {
                X = value.X,
                Y = value.Y,
                Z = value.Z
            };
        }

        public Vector3 ToVector3()
        {
            return new Vector3(X, Y, Z);
        }
    }

    public static class PrototypePersistenceService
    {
        private static readonly string[] RequiredSchemaV7SnapshotProperties =
        {
            nameof(PrototypeRuntimeSnapshot.SchemaVersion),
            nameof(PrototypeRuntimeSnapshot.ScenarioId),
            nameof(PrototypeRuntimeSnapshot.WorldSeed),
            nameof(PrototypeRuntimeSnapshot.WorldGenerationAttempt),
            nameof(PrototypeRuntimeSnapshot.WorldHash),
            nameof(PrototypeRuntimeSnapshot.SimulationSeed),
            nameof(PrototypeRuntimeSnapshot.SimulationTick),
            nameof(PrototypeRuntimeSnapshot.CurrentHour),
            nameof(PrototypeRuntimeSnapshot.CurrentWeather),
            nameof(PrototypeRuntimeSnapshot.TimeUntilNextWeatherShift),
            nameof(PrototypeRuntimeSnapshot.WeatherRandomState),
            nameof(PrototypeRuntimeSnapshot.PlayerPosition),
            nameof(PrototypeRuntimeSnapshot.SettlementAnchorPosition),
            nameof(PrototypeRuntimeSnapshot.Inventory),
            nameof(PrototypeRuntimeSnapshot.Stockpile),
            nameof(PrototypeRuntimeSnapshot.Workers),
            nameof(PrototypeRuntimeSnapshot.Resources),
            nameof(PrototypeRuntimeSnapshot.Settlement),
            nameof(PrototypeRuntimeSnapshot.Directive),
            nameof(PrototypeRuntimeSnapshot.ContributionCountsByResource),
            nameof(PrototypeRuntimeSnapshot.Crisis),
            nameof(PrototypeRuntimeSnapshot.Telemetry)
        };
        private static readonly string[] RequiredSchemaV8SnapshotProperties =
        {
            nameof(PrototypeRuntimeSnapshot.SchemaVersion),
            nameof(PrototypeRuntimeSnapshot.ScenarioId),
            nameof(PrototypeRuntimeSnapshot.WorldSeed),
            nameof(PrototypeRuntimeSnapshot.WorldGenerationAttempt),
            nameof(PrototypeRuntimeSnapshot.WorldHash),
            nameof(PrototypeRuntimeSnapshot.SimulationSeed),
            nameof(PrototypeRuntimeSnapshot.SimulationTick),
            nameof(PrototypeRuntimeSnapshot.CurrentHour),
            nameof(PrototypeRuntimeSnapshot.CurrentWeather),
            nameof(PrototypeRuntimeSnapshot.TimeUntilNextWeatherShift),
            nameof(PrototypeRuntimeSnapshot.WeatherRandomState),
            nameof(PrototypeRuntimeSnapshot.PlayerPosition),
            nameof(PrototypeRuntimeSnapshot.SettlementAnchorPosition),
            nameof(PrototypeRuntimeSnapshot.Inventory),
            nameof(PrototypeRuntimeSnapshot.Stockpile),
            nameof(PrototypeRuntimeSnapshot.Workers),
            nameof(PrototypeRuntimeSnapshot.Resources),
            nameof(PrototypeRuntimeSnapshot.Settlement),
            nameof(PrototypeRuntimeSnapshot.Directive),
            nameof(PrototypeRuntimeSnapshot.ContributionCountsByResource),
            nameof(PrototypeRuntimeSnapshot.Crisis),
            nameof(PrototypeRuntimeSnapshot.Telemetry),
            nameof(PrototypeRuntimeSnapshot.CivicPolicy)
        };
        private static readonly string[] RequiredSchemaV9SnapshotProperties =
        {
            nameof(PrototypeRuntimeSnapshot.SchemaVersion),
            nameof(PrototypeRuntimeSnapshot.ScenarioId),
            nameof(PrototypeRuntimeSnapshot.WorldSeed),
            nameof(PrototypeRuntimeSnapshot.WorldGenerationAttempt),
            nameof(PrototypeRuntimeSnapshot.WorldHash),
            nameof(PrototypeRuntimeSnapshot.SimulationSeed),
            nameof(PrototypeRuntimeSnapshot.SimulationTick),
            nameof(PrototypeRuntimeSnapshot.CurrentHour),
            nameof(PrototypeRuntimeSnapshot.CurrentWeather),
            nameof(PrototypeRuntimeSnapshot.TimeUntilNextWeatherShift),
            nameof(PrototypeRuntimeSnapshot.WeatherRandomState),
            nameof(PrototypeRuntimeSnapshot.PlayerPosition),
            nameof(PrototypeRuntimeSnapshot.SettlementAnchorPosition),
            nameof(PrototypeRuntimeSnapshot.Inventory),
            nameof(PrototypeRuntimeSnapshot.Stockpile),
            nameof(PrototypeRuntimeSnapshot.Workers),
            nameof(PrototypeRuntimeSnapshot.Resources),
            nameof(PrototypeRuntimeSnapshot.Settlement),
            nameof(PrototypeRuntimeSnapshot.Directive),
            nameof(PrototypeRuntimeSnapshot.ContributionCountsByResource),
            nameof(PrototypeRuntimeSnapshot.Crisis),
            nameof(PrototypeRuntimeSnapshot.Telemetry),
            nameof(PrototypeRuntimeSnapshot.CivicPolicy),
            nameof(PrototypeRuntimeSnapshot.Wetland)
        };
        private static readonly string[] RequiredSchemaV10SnapshotProperties = RequiredSchemaV9SnapshotProperties
            .Concat(new[] { nameof(PrototypeRuntimeSnapshot.WorldModel), nameof(PrototypeRuntimeSnapshot.VoxelWorld) })
            .ToArray();
        private static readonly string[] RequiredSchemaV11SnapshotProperties = RequiredSchemaV10SnapshotProperties
            .Concat(new[] { nameof(PrototypeRuntimeSnapshot.Construction) })
            .ToArray();
        private static readonly string[] RequiredSchemaV10VoxelWorldProperties =
        {
            nameof(VoxelWorldSnapshot.Schema), nameof(VoxelWorldSnapshot.Generator), nameof(VoxelWorldSnapshot.Materials),
            nameof(VoxelWorldSnapshot.Seed), nameof(VoxelWorldSnapshot.MinX), nameof(VoxelWorldSnapshot.MaxXExclusive),
            nameof(VoxelWorldSnapshot.MinY), nameof(VoxelWorldSnapshot.MaxYExclusive), nameof(VoxelWorldSnapshot.MinZ),
            nameof(VoxelWorldSnapshot.MaxZExclusive), nameof(VoxelWorldSnapshot.WorldRevision),
            nameof(VoxelWorldSnapshot.EventSequence), nameof(VoxelWorldSnapshot.Events), nameof(VoxelWorldSnapshot.Chunks),
            nameof(VoxelWorldSnapshot.WorldIdentity), nameof(VoxelWorldSnapshot.RootHash)
        };
        private static readonly string[] RequiredSchemaV10VoxelChunkProperties =
        {
            nameof(VoxelChunkSnapshot.X), nameof(VoxelChunkSnapshot.Y), nameof(VoxelChunkSnapshot.Z),
            nameof(VoxelChunkSnapshot.PayloadSegments), nameof(VoxelChunkSnapshot.Hash)
        };
        private static readonly string[] RequiredSchemaV10VoxelEventProperties =
        {
            nameof(VoxelChangeEvent.Sequence), nameof(VoxelChangeEvent.Tick), nameof(VoxelChangeEvent.ActorId),
            nameof(VoxelChangeEvent.Kind), nameof(VoxelChangeEvent.Coord), nameof(VoxelChangeEvent.Before),
            nameof(VoxelChangeEvent.After), nameof(VoxelChangeEvent.Revision)
        };
        private static readonly string[] RequiredSchemaV10VoxelCoordProperties =
        {
            nameof(VoxelCoord.X), nameof(VoxelCoord.Y), nameof(VoxelCoord.Z)
        };
        private static readonly string[] RequiredSchemaV11ConstructionProperties =
        {
            nameof(WorldcraftConstructionSnapshot.BaseWorldRevision), nameof(WorldcraftConstructionSnapshot.Revision),
            nameof(WorldcraftConstructionSnapshot.NextPieceSequence), nameof(WorldcraftConstructionSnapshot.EventSequence),
            nameof(WorldcraftConstructionSnapshot.Pieces), nameof(WorldcraftConstructionSnapshot.Events)
        };
        private static readonly string[] RequiredSchemaV11PieceProperties =
        {
            nameof(WorldcraftPieceSnapshot.InstanceId), nameof(WorldcraftPieceSnapshot.PieceId), nameof(WorldcraftPieceSnapshot.Anchor), nameof(WorldcraftPieceSnapshot.RotationQuarterTurns), nameof(WorldcraftPieceSnapshot.PlacedTick)
        };
        private static readonly string[] RequiredSchemaV11ConstructionEventProperties =
        {
            nameof(WorldcraftConstructionEvent.Sequence), nameof(WorldcraftConstructionEvent.Tick), nameof(WorldcraftConstructionEvent.Kind),
            nameof(WorldcraftConstructionEvent.PieceInstanceId), nameof(WorldcraftConstructionEvent.PieceId), nameof(WorldcraftConstructionEvent.ItemId),
            nameof(WorldcraftConstructionEvent.Coord), nameof(WorldcraftConstructionEvent.Anchor), nameof(WorldcraftConstructionEvent.RotationQuarterTurns),
            nameof(WorldcraftConstructionEvent.ConstructionRevision), nameof(WorldcraftConstructionEvent.WorldRevision), nameof(WorldcraftConstructionEvent.InventoryDeltas)
        };
        private static readonly string[] RequiredSchemaV7DirectiveProperties =
        {
            nameof(PrototypeDirectiveSnapshot.DirectiveId)
        };
        private static readonly string[] RequiredSchemaV7TelemetryProperties =
        {
            nameof(PrototypeRuntimeTelemetrySnapshot.FirstDirectiveTick),
            nameof(PrototypeRuntimeTelemetrySnapshot.FirstContributionTick),
            nameof(PrototypeRuntimeTelemetrySnapshot.DirectiveChanges),
            nameof(PrototypeRuntimeTelemetrySnapshot.FinalDirectiveId),
            nameof(PrototypeRuntimeTelemetrySnapshot.HasCrisisObservation),
            nameof(PrototypeRuntimeTelemetrySnapshot.PeakIncapacitatedCitizens),
            nameof(PrototypeRuntimeTelemetrySnapshot.MinimumMeals),
            nameof(PrototypeRuntimeTelemetrySnapshot.MinimumHearthFuel),
            nameof(PrototypeRuntimeTelemetrySnapshot.MaximumBedCoveragePercent),
            nameof(PrototypeRuntimeTelemetrySnapshot.FinalCapableCitizens),
            nameof(PrototypeRuntimeTelemetrySnapshot.FinalIncapacitatedCitizens),
            nameof(PrototypeRuntimeTelemetrySnapshot.FinalMeals),
            nameof(PrototypeRuntimeTelemetrySnapshot.FinalHearthFuel),
            nameof(PrototypeRuntimeTelemetrySnapshot.FinalBedCoveragePercent),
            nameof(PrototypeRuntimeTelemetrySnapshot.StabilityHoldEntries),
            nameof(PrototypeRuntimeTelemetrySnapshot.StabilityHoldBreaks),
            nameof(PrototypeRuntimeTelemetrySnapshot.CollapseHoldEntries),
            nameof(PrototypeRuntimeTelemetrySnapshot.CollapseHoldBreaks)
        };
        private static readonly string[] RequiredSchemaV7CrisisProperties =
        {
            nameof(PrototypeCrisisStateSnapshot.CrisisId),
            nameof(PrototypeCrisisStateSnapshot.TicksPerSecond),
            nameof(PrototypeCrisisStateSnapshot.DeadlineTicks),
            nameof(PrototypeCrisisStateSnapshot.ElapsedTicks),
            nameof(PrototypeCrisisStateSnapshot.StableHoldTicks),
            nameof(PrototypeCrisisStateSnapshot.CollapseHoldTicks),
            nameof(PrototypeCrisisStateSnapshot.Outcome),
            nameof(PrototypeCrisisStateSnapshot.CollapseCause),
            nameof(PrototypeCrisisStateSnapshot.TerminalEventEmitted),
            nameof(PrototypeCrisisStateSnapshot.HasObservation),
            nameof(PrototypeCrisisStateSnapshot.LastObservation)
        };
        private static readonly string[] RequiredSchemaV7CrisisObservationProperties =
        {
            nameof(PrototypeCrisisObservation.TotalCitizens),
            nameof(PrototypeCrisisObservation.CapableCitizens),
            nameof(PrototypeCrisisObservation.Meals),
            nameof(PrototypeCrisisObservation.HearthFuel),
            nameof(PrototypeCrisisObservation.BedCoveragePercent),
            nameof(PrototypeCrisisObservation.IncapacitatedCitizens)
        };
        private static readonly string[] RequiredSchemaV8CivicPolicyProperties =
        {
            nameof(PrototypeCivicPolicySnapshot.PolicyId),
            nameof(PrototypeCivicPolicySnapshot.SelectedTick),
            nameof(PrototypeCivicPolicySnapshot.Version),
            nameof(PrototypeCivicPolicySnapshot.WindowStartTick),
            nameof(PrototypeCivicPolicySnapshot.WindowEndTick)
        };
        private static readonly string[] RequiredSchemaV9WetlandProperties =
        {
            nameof(PrototypeWetlandSnapshot.PolicyId),
            nameof(PrototypeWetlandSnapshot.PolicySelectedTick),
            nameof(PrototypeWetlandSnapshot.PolicyVersion),
            nameof(PrototypeWetlandSnapshot.ReedQuotaLimit),
            nameof(PrototypeWetlandSnapshot.ReedQuotaConsumed),
            nameof(PrototypeWetlandSnapshot.WetlandHealth),
            nameof(PrototypeWetlandSnapshot.WetlandHealthBand)
        };
        private static readonly string[] RequiredSchemaV9RunSummaryProperties = typeof(PrototypeRunSummary)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            MaxDepth = 64
        };

        public static string SerializeSnapshot(PrototypeRuntimeSnapshot snapshot)
        {
            JsonObject root = JsonSerializer.SerializeToNode(snapshot, JsonOptions)!.AsObject();
            if (snapshot.SchemaVersion < 10)
            {
                root.Remove(nameof(PrototypeRuntimeSnapshot.WorldModel));
                root.Remove(nameof(PrototypeRuntimeSnapshot.VoxelWorld));
                root.Remove(nameof(PrototypeRuntimeSnapshot.Construction));
            }
            else if (snapshot.SchemaVersion == 10) root.Remove(nameof(PrototypeRuntimeSnapshot.Construction));
            return root.ToJsonString(JsonOptions);
        }

        public static PrototypeRuntimeSnapshot DeserializeSnapshot(string json)
        {
            byte[] bytes = ValidateJsonPayload(
                json,
                PrototypeRunArtifactManager.MaximumSnapshotBytes,
                PrototypePersistenceBounds.MaximumSnapshotRows,
                PrototypeRunArtifactManager.MaximumDictionaryEntries,
                PrototypeRunArtifactManager.MaximumMessageLength,
                "snapshot");
            using JsonDocument document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(nameof(PrototypeRuntimeSnapshot.SchemaVersion), out JsonElement schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int schemaVersion))
            {
                throw new InvalidDataException("Runtime snapshot is missing an integral SchemaVersion.");
            }

            if (schemaVersion is not (5 or 6 or 7 or 8 or 9 or 10 or 11))
            {
                throw new InvalidDataException($"Unsupported runtime snapshot schema {schemaVersion}; expected 5, 6, 7, 8, 9, 10, or 11.");
            }

            if (schemaVersion is 7 or 8 or 9 or 10 or 11)
            {
                IReadOnlyList<string> requiredSnapshotProperties = schemaVersion switch
                {
                    7 => RequiredSchemaV7SnapshotProperties,
                    8 => RequiredSchemaV8SnapshotProperties,
                    9 => RequiredSchemaV9SnapshotProperties,
                    10 => RequiredSchemaV10SnapshotProperties,
                    _ => RequiredSchemaV11SnapshotProperties
                };
                foreach (string propertyName in requiredSnapshotProperties)
                {
                    if (!document.RootElement.TryGetProperty(propertyName, out _))
                    {
                        throw new InvalidDataException(
                            $"Schema-v{schemaVersion} runtime snapshot is missing required property '{propertyName}'.");
                    }
                }

                JsonElement directive = RequireObjectWithProperties(
                    document.RootElement,
                    nameof(PrototypeRuntimeSnapshot.Directive),
                    RequiredSchemaV7DirectiveProperties,
                    schemaVersion);
                _ = directive;
                JsonElement telemetry = RequireObjectWithProperties(
                    document.RootElement,
                    nameof(PrototypeRuntimeSnapshot.Telemetry),
                    RequiredSchemaV7TelemetryProperties,
                    schemaVersion);
                _ = telemetry;

                JsonElement crisis = document.RootElement.GetProperty(
                    nameof(PrototypeRuntimeSnapshot.Crisis));
                if (crisis.ValueKind != JsonValueKind.Null)
                {
                    RequireProperties(
                        crisis,
                        nameof(PrototypeRuntimeSnapshot.Crisis),
                        RequiredSchemaV7CrisisProperties,
                        schemaVersion);
                    JsonElement hasObservation = crisis.GetProperty(
                        nameof(PrototypeCrisisStateSnapshot.HasObservation));
                    if (hasObservation.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    {
                        throw new InvalidDataException(
                            $"Schema-v{schemaVersion} crisis HasObservation must be a Boolean.");
                    }

                    if (hasObservation.GetBoolean())
                    {
                        _ = RequireObjectWithProperties(
                            crisis,
                            nameof(PrototypeCrisisStateSnapshot.LastObservation),
                            RequiredSchemaV7CrisisObservationProperties,
                            schemaVersion);
                    }
                }

                if (schemaVersion >= 8)
                {
                    _ = RequireObjectWithProperties(
                        document.RootElement,
                        nameof(PrototypeRuntimeSnapshot.CivicPolicy),
                        RequiredSchemaV8CivicPolicyProperties,
                        schemaVersion);
                }

                if (schemaVersion >= 9)
                {
                    _ = RequireObjectWithProperties(
                        document.RootElement,
                        nameof(PrototypeRuntimeSnapshot.Wetland),
                        RequiredSchemaV9WetlandProperties,
                        schemaVersion);
                }

                if (schemaVersion is 10 or 11)
                {
                    JsonElement voxelWorld = RequireObjectWithProperties(
                        document.RootElement,
                        nameof(PrototypeRuntimeSnapshot.VoxelWorld),
                        RequiredSchemaV10VoxelWorldProperties,
                        schemaVersion);
                    ValidateSchemaV10VoxelRows(voxelWorld, schemaVersion);
                    if (schemaVersion == 11)
                    {
                        JsonElement construction = RequireObjectWithProperties(document.RootElement, nameof(PrototypeRuntimeSnapshot.Construction), RequiredSchemaV11ConstructionProperties, schemaVersion);
                        ValidateSchemaV11ConstructionRows(construction, schemaVersion);
                    }
                }
            }

            PrototypeRuntimeSnapshot? snapshot =
                JsonSerializer.Deserialize<PrototypeRuntimeSnapshot>(bytes, JsonOptions);
            if (snapshot == null)
            {
                throw new InvalidDataException("Runtime snapshot payload is null.");
            }
            if (schemaVersion >= 8)
            {
                PrototypeCivicPolicyState civicPolicy =
                    PrototypeCivicPolicyState.PrepareRestore(snapshot.CivicPolicy!);
                if (civicPolicy.SelectedTick > snapshot.SimulationTick)
                {
                    throw new InvalidDataException(
                        "Runtime snapshot civic policy selection tick exceeds the simulation tick.");
                }

                if (schemaVersion >= 9)
                {
                    _ = PrototypeWetlandState.PrepareRestore(snapshot.Wetland!, civicPolicy);
                }
            }

            if (schemaVersion is 10 or 11)
            {
                PrototypeVoxelSnapshotValidator.ValidateCanonicalShell(snapshot);
                try
                {
                    VoxelWorldModule voxelWorld = VoxelWorldModule.Restore(snapshot.VoxelWorld!);
                    if (voxelWorld.Seed != snapshot.WorldSeed || snapshot.SimulationSeed != snapshot.WorldSeed ||
                        snapshot.WorldGenerationAttempt != 0 ||
                        !string.Equals(voxelWorld.WorldIdentity, snapshot.WorldHash, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException($"Schema-v{schemaVersion} voxel identity does not match its outer envelope.");
                    }
                    if (schemaVersion == 11)
                    {
                        _ = WorldcraftConstructionState.Restore(snapshot.Construction!, voxelWorld, snapshot.Inventory, snapshot.SimulationTick);
                    }
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidDataException($"Schema-v{schemaVersion} voxel world payload is invalid.", exception);
                }
            }

            return snapshot;
        }

        private static void ValidateSchemaV10VoxelRows(JsonElement voxelWorld, int schemaVersion)
        {
            JsonElement chunks = voxelWorld.GetProperty(nameof(VoxelWorldSnapshot.Chunks));
            if (chunks.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Schema-v10 voxel chunks must be an array.");
            }
            foreach (JsonElement chunk in chunks.EnumerateArray())
            {
                if (chunk.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException("Schema-v10 voxel chunk must be an object.");
                }
                RequireProperties(chunk, nameof(VoxelWorldSnapshot.Chunks), RequiredSchemaV10VoxelChunkProperties, schemaVersion);
                if (chunk.GetProperty(nameof(VoxelChunkSnapshot.PayloadSegments)).ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("Schema-v10 voxel chunk segments must be an array.");
                }
            }

            JsonElement events = voxelWorld.GetProperty(nameof(VoxelWorldSnapshot.Events));
            if (events.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Schema-v10 voxel events must be an array.");
            }
            foreach (JsonElement voxelEvent in events.EnumerateArray())
            {
                if (voxelEvent.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException("Schema-v10 voxel event must be an object.");
                }
                RequireProperties(voxelEvent, nameof(VoxelWorldSnapshot.Events), RequiredSchemaV10VoxelEventProperties, schemaVersion);
                JsonElement coord = voxelEvent.GetProperty(nameof(VoxelChangeEvent.Coord));
                if (coord.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException("Schema-v10 voxel event coordinate must be an object.");
                }
                RequireProperties(coord, nameof(VoxelChangeEvent.Coord), RequiredSchemaV10VoxelCoordProperties, schemaVersion);
            }
        }

        private static void ValidateSchemaV11ConstructionRows(JsonElement construction, int schemaVersion)
        {
            JsonElement pieces = construction.GetProperty(nameof(WorldcraftConstructionSnapshot.Pieces));
            JsonElement events = construction.GetProperty(nameof(WorldcraftConstructionSnapshot.Events));
            if (pieces.ValueKind != JsonValueKind.Array || events.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Schema-v11 construction pieces and events must be arrays.");
            }
            foreach (JsonElement piece in pieces.EnumerateArray())
            {
                RequireProperties(piece, "construction piece", RequiredSchemaV11PieceProperties, schemaVersion);
                _ = RequireObjectWithProperties(piece, nameof(WorldcraftPieceSnapshot.Anchor), RequiredSchemaV10VoxelCoordProperties, schemaVersion);
            }
            foreach (JsonElement constructionEvent in events.EnumerateArray())
            {
                RequireProperties(constructionEvent, "construction event", RequiredSchemaV11ConstructionEventProperties, schemaVersion);
                _ = RequireObjectWithProperties(constructionEvent, nameof(WorldcraftConstructionEvent.Coord), RequiredSchemaV10VoxelCoordProperties, schemaVersion);
                _ = RequireObjectWithProperties(constructionEvent, nameof(WorldcraftConstructionEvent.Anchor), RequiredSchemaV10VoxelCoordProperties, schemaVersion);
                JsonElement deltas = constructionEvent.GetProperty(nameof(WorldcraftConstructionEvent.InventoryDeltas));
                if (deltas.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException("Schema-v11 construction event inventory deltas must be an object.");
                }
            }
        }

        private static JsonElement RequireObjectWithProperties(
            JsonElement parent,
            string propertyName,
            IReadOnlyList<string> requiredProperties,
            int schemaVersion)
        {
            if (!parent.TryGetProperty(propertyName, out JsonElement payload) ||
                payload.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    $"Schema-v{schemaVersion} runtime snapshot property '{propertyName}' must be an object.");
            }

            RequireProperties(payload, propertyName, requiredProperties, schemaVersion);
            return payload;
        }

        private static void RequireProperties(
            JsonElement payload,
            string payloadName,
            IReadOnlyList<string> requiredProperties,
            int schemaVersion)
        {
            if (payload.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    $"Schema-v{schemaVersion} runtime snapshot property '{payloadName}' must be an object.");
            }

            foreach (string propertyName in requiredProperties)
            {
                if (!payload.TryGetProperty(propertyName, out _))
                {
                    throw new InvalidDataException(
                        $"Schema-v{schemaVersion} runtime snapshot property '{payloadName}' is missing required property '{propertyName}'.");
                }
            }
        }

        public static string SerializeEventLog(PrototypeEventLog eventLog)
        {
            return JsonSerializer.Serialize(eventLog.Entries, JsonOptions);
        }

        public static List<PrototypeEventRecord> DeserializeEventLog(string json)
        {
            byte[] bytes = ValidateJsonPayload(
                json,
                PrototypeRunArtifactManager.MaximumEventLogBytes,
                PrototypeRunArtifactManager.MaximumEventRows,
                maximumObjectProperties: 8,
                PrototypeRunArtifactManager.MaximumMessageLength,
                "event log");
            List<PrototypeEventRecord> eventLog =
                JsonSerializer.Deserialize<List<PrototypeEventRecord>>(bytes, JsonOptions)
                ?? throw new InvalidDataException("Event-log payload is null.");
            PrototypeRunArtifactManager.ValidateStandaloneEventLog(eventLog);
            return eventLog;
        }

        public static string SerializeRunSummary(PrototypeRunSummary summary)
        {
            return JsonSerializer.Serialize(summary, JsonOptions);
        }

        public static PrototypeRunSummary DeserializeRunSummary(string json)
        {
            byte[] bytes = ValidateJsonPayload(
                json,
                PrototypeRunArtifactManager.MaximumRunSummaryBytes,
                PrototypePersistenceBounds.MaximumSnapshotRows,
                PrototypeRunArtifactManager.MaximumDictionaryEntries,
                PrototypeRunArtifactManager.MaximumMessageLength,
                "run summary");
            using JsonDocument document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(nameof(PrototypeRunSummary.SchemaVersion), out JsonElement schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int schemaVersion))
            {
                throw new InvalidDataException("Run summary is missing an integral SchemaVersion.");
            }

            if (schemaVersion >= 9)
            {
                foreach (string propertyName in RequiredSchemaV9RunSummaryProperties)
                {
                    if (!document.RootElement.TryGetProperty(propertyName, out _))
                    {
                        throw new InvalidDataException(
                            $"Schema-v{schemaVersion} run summary is missing required property '{propertyName}'.");
                    }
                }

                _ = RequireObjectWithProperties(
                    document.RootElement,
                    nameof(PrototypeRunSummary.CivicPolicy),
                    RequiredSchemaV8CivicPolicyProperties,
                    schemaVersion);
                _ = RequireObjectWithProperties(
                    document.RootElement,
                    nameof(PrototypeRunSummary.Wetland),
                    RequiredSchemaV9WetlandProperties,
                    schemaVersion);
            }

            PrototypeRunSummary summary =
                JsonSerializer.Deserialize<PrototypeRunSummary>(bytes, JsonOptions)
                ?? throw new InvalidDataException("Run-summary payload is null.");
            PrototypeRunArtifactManager.ValidateStandaloneRunSummary(summary);
            return summary;
        }

        public static void SaveSnapshot(string path, PrototypeRuntimeSnapshot snapshot)
        {
            string json = SerializeSnapshot(snapshot);
            _ = DeserializeSnapshot(json);
            AtomicWriteJson(path, json);
        }

        public static PrototypeRuntimeSnapshot LoadSnapshot(string path)
        {
            byte[] bytes = PrototypeRunArtifactManager.ReadBoundedFile(
                path,
                PrototypeRunArtifactManager.MaximumSnapshotBytes,
                "snapshot");
            return DeserializeSnapshot(Encoding.UTF8.GetString(bytes));
        }

        public static void SaveEventLog(string path, PrototypeEventLog eventLog)
        {
            string json = SerializeEventLog(eventLog);
            _ = DeserializeEventLog(json);
            AtomicWriteJson(path, json);
        }

        public static List<PrototypeEventRecord> LoadEventLog(string path)
        {
            byte[] bytes = PrototypeRunArtifactManager.ReadBoundedFile(
                path,
                PrototypeRunArtifactManager.MaximumEventLogBytes,
                "event log");
            return DeserializeEventLog(Encoding.UTF8.GetString(bytes));
        }

        public static void SaveRunSummary(string path, PrototypeRunSummary summary)
        {
            string json = SerializeRunSummary(summary);
            _ = DeserializeRunSummary(json);
            AtomicWriteJson(path, json);
        }

        public static PrototypeRunSummary LoadRunSummary(string path)
        {
            byte[] bytes = PrototypeRunArtifactManager.ReadBoundedFile(
                path,
                PrototypeRunArtifactManager.MaximumRunSummaryBytes,
                "run summary");
            return DeserializeRunSummary(Encoding.UTF8.GetString(bytes));
        }

        private static byte[] ValidateJsonPayload(
            string json,
            long maximumBytes,
            int maximumArrayItems,
            int maximumObjectProperties,
            int maximumStringBytes,
            string label)
        {
            ArgumentNullException.ThrowIfNull(json);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            PrototypeRunArtifactManager.ValidatePayloadByteLength(
                bytes,
                maximumBytes,
                label);
            PrototypeRunArtifactManager.PreflightJson(
                bytes,
                maximumArrayItems,
                maximumObjectProperties,
                maximumStringBytes,
                label);
            return bytes;
        }

        public static string SerializeWorldSummary(PrototypeWorldSummary summary)
        {
            return JsonSerializer.Serialize(summary, JsonOptions);
        }

        public static PrototypeWorldSummary DeserializeWorldSummary(string json)
        {
            byte[] bytes = ValidateJsonPayload(
                json,
                PrototypeRunArtifactManager.MaximumWorldSummaryBytes,
                PrototypePersistenceBounds.MaximumSnapshotRows,
                PrototypeRunArtifactManager.MaximumDictionaryEntries,
                PrototypeRunArtifactManager.MaximumMessageLength,
                "world summary");
            PrototypeWorldSummary summary =
                JsonSerializer.Deserialize<PrototypeWorldSummary>(bytes, JsonOptions)
                ?? throw new InvalidDataException("World-summary payload is null.");
            PrototypeRunArtifactManager.ValidateStandaloneWorldSummary(summary);
            return summary;
        }

        public static void SaveWorldSummary(string path, PrototypeWorldSummary summary)
        {
            string json = SerializeWorldSummary(summary);
            _ = DeserializeWorldSummary(json);
            AtomicWriteJson(path, json);
        }

        public static PrototypeWorldSummary LoadWorldSummary(string path)
        {
            byte[] bytes = PrototypeRunArtifactManager.ReadBoundedFile(
                path,
                PrototypeRunArtifactManager.MaximumWorldSummaryBytes,
                "world summary");
            return DeserializeWorldSummary(Encoding.UTF8.GetString(bytes));
        }

        public static string? GetLatestFile(string directoryPath, string searchPattern)
        {
            if (!Directory.Exists(directoryPath))
            {
                return null;
            }

            return Directory
                .GetFiles(directoryPath, searchPattern)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static void AtomicWriteJson(string path, string json)
        {
            PrototypeRunArtifactManager.AtomicWrite(
                path,
                Encoding.UTF8.GetBytes(json),
                Guid.NewGuid().ToString("N"));
        }
    }
}
