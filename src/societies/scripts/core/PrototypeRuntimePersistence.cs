using Godot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Societies.Core
{
    /// <summary>
    /// Snapshot and event-log contracts for prototype validation runs.
    /// </summary>
    public sealed class PrototypeRuntimeSnapshot
    {
        public int SchemaVersion { get; set; } = 3;

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
    }

    public sealed class PrototypeResourceSnapshot
    {
        public string ResourceId { get; set; } = string.Empty;

        public int UnitsRemaining { get; set; }

        public PrototypeSerializableVector3 Position { get; set; }
    }

    public sealed class PrototypeWorkerSnapshot
    {
        public string WorkerId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string PreferredResourceId { get; set; } = string.Empty;

        public string Phase { get; set; } = string.Empty;

        public string TargetResourceNodeName { get; set; } = string.Empty;

        public string CarryItemId { get; set; } = string.Empty;

        public int CarryAmount { get; set; }

        public int TicksRemaining { get; set; }

        public int PhaseDurationTicks { get; set; }

        public PrototypeSerializableVector3 Position { get; set; }

        public PrototypeSerializableVector3 HomePosition { get; set; }

        public PrototypeSerializableVector3 TargetPosition { get; set; }

        public string TargetLabel { get; set; } = string.Empty;

        public string ActivityText { get; set; } = string.Empty;
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
        public int SchemaVersion { get; set; } = 3;

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
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static string SerializeSnapshot(PrototypeRuntimeSnapshot snapshot)
        {
            return JsonSerializer.Serialize(snapshot, JsonOptions);
        }

        public static PrototypeRuntimeSnapshot DeserializeSnapshot(string json)
        {
            PrototypeRuntimeSnapshot? snapshot = JsonSerializer.Deserialize<PrototypeRuntimeSnapshot>(json, JsonOptions);
            return snapshot ?? new PrototypeRuntimeSnapshot();
        }

        public static string SerializeEventLog(PrototypeEventLog eventLog)
        {
            return JsonSerializer.Serialize(eventLog.Entries, JsonOptions);
        }

        public static List<PrototypeEventRecord> DeserializeEventLog(string json)
        {
            return JsonSerializer.Deserialize<List<PrototypeEventRecord>>(json, JsonOptions) ?? new List<PrototypeEventRecord>();
        }

        public static string SerializeRunSummary(PrototypeRunSummary summary)
        {
            return JsonSerializer.Serialize(summary, JsonOptions);
        }

        public static PrototypeRunSummary DeserializeRunSummary(string json)
        {
            PrototypeRunSummary? summary = JsonSerializer.Deserialize<PrototypeRunSummary>(json, JsonOptions);
            return summary ?? new PrototypeRunSummary();
        }

        public static void SaveSnapshot(string path, PrototypeRuntimeSnapshot snapshot)
        {
            EnsureDirectory(path);
            File.WriteAllText(path, SerializeSnapshot(snapshot));
        }

        public static PrototypeRuntimeSnapshot LoadSnapshot(string path)
        {
            return DeserializeSnapshot(File.ReadAllText(path));
        }

        public static void SaveEventLog(string path, PrototypeEventLog eventLog)
        {
            EnsureDirectory(path);
            File.WriteAllText(path, SerializeEventLog(eventLog));
        }

        public static List<PrototypeEventRecord> LoadEventLog(string path)
        {
            return DeserializeEventLog(File.ReadAllText(path));
        }

        public static void SaveRunSummary(string path, PrototypeRunSummary summary)
        {
            EnsureDirectory(path);
            File.WriteAllText(path, SerializeRunSummary(summary));
        }

        public static PrototypeRunSummary LoadRunSummary(string path)
        {
            return DeserializeRunSummary(File.ReadAllText(path));
        }

        public static string SerializeWorldSummary(PrototypeWorldSummary summary)
        {
            return JsonSerializer.Serialize(summary, JsonOptions);
        }

        public static PrototypeWorldSummary DeserializeWorldSummary(string json)
        {
            PrototypeWorldSummary? summary = JsonSerializer.Deserialize<PrototypeWorldSummary>(json, JsonOptions);
            return summary ?? new PrototypeWorldSummary();
        }

        public static void SaveWorldSummary(string path, PrototypeWorldSummary summary)
        {
            EnsureDirectory(path);
            File.WriteAllText(path, SerializeWorldSummary(summary));
        }

        public static PrototypeWorldSummary LoadWorldSummary(string path)
        {
            return DeserializeWorldSummary(File.ReadAllText(path));
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

        private static void EnsureDirectory(string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
