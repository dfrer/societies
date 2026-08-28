using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Societies.Core
{
    public static class VoxelWorldcraftCatalog
    {
        public const string SoilItem = "soil";
        public const string StoneItem = "stone";
        public const string WoodItem = "wood";
        public const int HotbarSlots = 8;
        public const int StackLimit = 32;
        public const int BuildRangeCells = 7;
        public static readonly IReadOnlyList<string> HotbarOrder = Array.AsReadOnly(new[] { SoilItem, StoneItem, WoodItem });

        public static string? ItemFor(VoxelMaterialId material) => material switch
        {
            VoxelMaterialId.Soil => SoilItem,
            VoxelMaterialId.Stone => StoneItem,
            VoxelMaterialId.Wood => WoodItem,
            _ => null
        };
        public static bool IsKnownItem(string itemId) => HotbarOrder.Contains(itemId, StringComparer.Ordinal);
        public static WorldcraftPieceDefinition? FindPiece(string pieceId) => Pieces.FirstOrDefault(piece => piece.Id == pieceId);
        public static readonly IReadOnlyList<WorldcraftPieceDefinition> Pieces = Array.AsReadOnly(new[]
        {
            Piece("wood_floor", "Wood floor", true, new[] { new VoxelCoord(0, 0, 0), new VoxelCoord(1, 0, 0) }, 2),
            Piece("wood_wall", "Wood wall", true, new[] { new VoxelCoord(0, 0, 0), new VoxelCoord(0, 1, 0) }, 2),
            Piece("wood_post", "Wood post", false, new[] { new VoxelCoord(0, 0, 0), new VoxelCoord(0, 1, 0), new VoxelCoord(0, 2, 0) }, 1)
        });

        private static WorldcraftPieceDefinition Piece(string id, string name, bool rotates, VoxelCoord[] cells, int wood) =>
            new(id, name, rotates, Array.AsReadOnly(cells), new ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>(StringComparer.Ordinal) { [WoodItem] = wood }));
    }

    public sealed record WorldcraftPieceDefinition(string Id, string DisplayName, bool Rotates,
        IReadOnlyList<VoxelCoord> LocalCells, IReadOnlyDictionary<string, int> Cost);

    public enum WorldcraftRejection
    {
        None, InvalidActor, TickMismatch, StaleRevision, UnknownPiece, InvalidRotation, OutOfRange,
        OutOfBounds, Occupied, Unsupported, InsufficientMaterials, InventoryFull, UnknownPieceInstance,
        EventCapacityReached, PieceCapacityReached, OrphanedSupport
    }

    public sealed class WorldcraftGatherResult
    {
        public bool Accepted { get; init; }
        public WorldcraftRejection Rejection { get; init; }
        public VoxelEditResult? VoxelEdit { get; init; }
        public VoxelEditRejection VoxelRejection { get; init; }
        public string ItemId { get; init; } = string.Empty;
    }

    public sealed class WorldcraftPlacementCommand
    {
        public string ActorId { get; init; } = string.Empty;
        public long Tick { get; init; }
        public long ExpectedConstructionRevision { get; init; }
        public string PieceId { get; init; } = string.Empty;
        public VoxelCoord Anchor { get; init; }
        public int RotationQuarterTurns { get; init; }
        public VoxelCoord ActorCell { get; init; }
    }

    public sealed class WorldcraftDismantleCommand
    {
        public string ActorId { get; init; } = string.Empty;
        public long Tick { get; init; }
        public long ExpectedConstructionRevision { get; init; }
        public string PieceInstanceId { get; init; } = string.Empty;
        public VoxelCoord ActorCell { get; init; }
    }

    public sealed class WorldcraftPieceSnapshot
    {
        public string InstanceId { get; set; } = string.Empty;
        public string PieceId { get; set; } = string.Empty;
        public VoxelCoord Anchor { get; set; }
        public int RotationQuarterTurns { get; set; }
        public long PlacedTick { get; set; }
    }

    public sealed class WorldcraftConstructionSnapshot
    {
        public long BaseWorldRevision { get; set; }
        public long Revision { get; set; }
        public long NextPieceSequence { get; set; }
        public long EventSequence { get; set; }
        public List<WorldcraftPieceSnapshot> Pieces { get; set; } = new();
        public List<WorldcraftConstructionEvent> Events { get; set; } = new();
    }

    public sealed class WorldcraftConstructionEvent
    {
        public long Sequence { get; set; }
        public long Tick { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string PieceInstanceId { get; set; } = string.Empty;
        public string PieceId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public VoxelCoord Coord { get; set; }
        public VoxelCoord Anchor { get; set; }
        public int RotationQuarterTurns { get; set; }
        public long ConstructionRevision { get; set; }
        public long WorldRevision { get; set; }
        public Dictionary<string, int> InventoryDeltas { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class WorldcraftPlacementEvaluation
    {
        public bool IsValid => Rejection == WorldcraftRejection.None;
        public WorldcraftRejection Rejection { get; init; }
        public IReadOnlyList<VoxelCoord> Cells { get; init; } = Array.Empty<VoxelCoord>();
        public WorldcraftPieceDefinition? Definition { get; init; }
        public VoxelCoord Anchor { get; init; }
        public int RotationQuarterTurns { get; init; }
    }

    public sealed class WorldcraftCommandResult
    {
        public bool Accepted { get; init; }
        public WorldcraftRejection Rejection { get; init; }
        public long ConstructionRevision { get; init; }
        public WorldcraftPieceSnapshot? Piece { get; init; }
        public IReadOnlyList<string> ChangedItemIds { get; init; } = Array.Empty<string>();
    }

    internal readonly record struct WorldcraftRestoreReplayWork(long WorldGenerationCount, long AppliedVoxelEventCount);

    internal sealed class WorldcraftConstructionState
    {
        internal const int MaximumPieceCount = 512;
        internal const int MaximumEventCount = PrototypePersistenceBounds.MaximumSnapshotRows;
        private readonly List<WorldcraftPieceSnapshot> _pieces = new();
        private readonly List<WorldcraftConstructionEvent> _events = new();
        public long Revision { get; private set; }
        public long NextPieceSequence { get; private set; }
        public long EventSequence { get; private set; }
        public long BaseWorldRevision { get; private set; }
        public bool CanRecordEvent => EventSequence < MaximumEventCount;
        public bool CanPlacePiece => _pieces.Count < MaximumPieceCount && CanRecordEvent;
        public bool CanTrackVoxelEdit => EventSequence == 0 && Revision == 0 && _pieces.Count == 0 || CanRecordEvent;
        public IReadOnlyList<WorldcraftPieceSnapshot> CapturePieces() => Array.AsReadOnly(_pieces.Select(ClonePiece).ToArray());
        public IReadOnlyList<VoxelCoord> GetCells(WorldcraftPieceSnapshot piece) => CellsFor(
            VoxelWorldcraftCatalog.FindPiece(piece.PieceId)!, piece.Anchor, piece.RotationQuarterTurns);

        public WorldcraftConstructionState(long baseWorldRevision = 0)
        {
            if (baseWorldRevision < 0) throw new ArgumentOutOfRangeException(nameof(baseWorldRevision));
            BaseWorldRevision = baseWorldRevision;
        }

        public WorldcraftPlacementEvaluation Evaluate(WorldcraftPlacementCommand command, VoxelWorldModule world, InventoryComponent inventory)
        {
            if (command == null) return Reject(WorldcraftRejection.InvalidActor);
            WorldcraftPieceDefinition? definition = VoxelWorldcraftCatalog.FindPiece(command.PieceId);
            int normalizedRotation = NormalizeRotation(command.RotationQuarterTurns);
            IReadOnlyList<VoxelCoord> cells = definition == null
                ? Array.Empty<VoxelCoord>()
                : CellsFor(definition, command.Anchor, normalizedRotation);
            if (string.IsNullOrWhiteSpace(command.ActorId)) return Reject(WorldcraftRejection.InvalidActor, definition, cells, command.Anchor, normalizedRotation);
            if (command.ExpectedConstructionRevision != Revision) return Reject(WorldcraftRejection.StaleRevision, definition, cells, command.Anchor, normalizedRotation);
            if (definition == null) return Reject(WorldcraftRejection.UnknownPiece);
            if (!CanRecordEvent) return Reject(WorldcraftRejection.EventCapacityReached, definition, cells, command.Anchor, normalizedRotation);
            if (_pieces.Count >= MaximumPieceCount) return Reject(WorldcraftRejection.PieceCapacityReached, definition, cells, command.Anchor, normalizedRotation);
            if (command.RotationQuarterTurns is < 0 or > 3 || (!definition.Rotates && command.RotationQuarterTurns != 0))
                return Reject(WorldcraftRejection.InvalidRotation, definition, cells, command.Anchor, normalizedRotation);
            if (cells.Any(cell => !world.Contains(cell))) return Reject(WorldcraftRejection.OutOfBounds, definition, cells, command.Anchor, normalizedRotation);
            if (Chebyshev(command.Anchor, command.ActorCell) > VoxelWorldcraftCatalog.BuildRangeCells)
                return Reject(WorldcraftRejection.OutOfRange, definition, cells, command.Anchor, normalizedRotation);
            HashSet<VoxelCoord> occupied = _pieces.SelectMany(GetCells).ToHashSet();
            if (cells.Any(cell => world.GetMaterial(cell) != VoxelMaterialId.Air || occupied.Contains(cell)))
                return Reject(WorldcraftRejection.Occupied, definition, cells, command.Anchor, normalizedRotation);
            if (!cells.Any(cell => IsSupported(cell, world, occupied))) return Reject(WorldcraftRejection.Unsupported, definition, cells, command.Anchor, normalizedRotation);
            if (!inventory.HasItems(definition.Cost)) return Reject(WorldcraftRejection.InsufficientMaterials, definition, cells, command.Anchor, normalizedRotation);
            return new() { Cells = Array.AsReadOnly(cells.ToArray()), Definition = definition,
                Anchor = command.Anchor, RotationQuarterTurns = normalizedRotation };
        }

        public WorldcraftPieceSnapshot Place(WorldcraftPlacementCommand command, long worldRevision)
        {
            if (!CanPlacePiece) throw new InvalidOperationException("Construction capacity was not reserved before placement.");
            NextPieceSequence = checked(NextPieceSequence + 1);
            WorldcraftPieceSnapshot piece = new()
            {
                InstanceId = FormatPieceId(NextPieceSequence), PieceId = command.PieceId, Anchor = command.Anchor,
                RotationQuarterTurns = command.RotationQuarterTurns, PlacedTick = command.Tick
            };
            _pieces.Add(piece);
            Revision = checked(Revision + 1);
            WorldcraftPieceDefinition definition = VoxelWorldcraftCatalog.FindPiece(piece.PieceId)!;
            AppendEvent(new()
            {
                Tick = command.Tick, Kind = "place", PieceInstanceId = piece.InstanceId, PieceId = piece.PieceId,
                Anchor = piece.Anchor, RotationQuarterTurns = piece.RotationQuarterTurns,
                ConstructionRevision = Revision, WorldRevision = worldRevision,
                InventoryDeltas = definition.Cost.ToDictionary(pair => pair.Key, pair => -pair.Value, StringComparer.Ordinal)
            });
            return ClonePiece(piece);
        }

        public bool TryGet(string instanceId, out WorldcraftPieceSnapshot? piece)
        {
            WorldcraftPieceSnapshot? stored = _pieces.FirstOrDefault(candidate => candidate.InstanceId == instanceId);
            piece = stored == null ? null : ClonePiece(stored);
            return piece != null;
        }

        public bool WouldOrphanAfterVoxelRemoval(VoxelWorldModule world, VoxelCoord coord) => !AllPiecesSupported(world, coord, null);
        public bool WouldOrphanAfterDismantle(VoxelWorldModule world, string instanceId) => !AllPiecesSupported(world, null, instanceId);
        public bool IsOccupiedByPiece(VoxelCoord coord) => _pieces.SelectMany(GetCells).Contains(coord);

        public void Dismantle(WorldcraftPieceSnapshot piece, long tick, long worldRevision)
        {
            if (!CanRecordEvent) throw new InvalidOperationException("Construction event capacity was not reserved before dismantle.");
            int index = _pieces.FindIndex(candidate => candidate.InstanceId == piece.InstanceId);
            if (index < 0) throw new InvalidOperationException("Construction piece vanished before dismantle commit.");
            WorldcraftPieceSnapshot stored = _pieces[index];
            _pieces.RemoveAt(index);
            Revision = checked(Revision + 1);
            WorldcraftPieceDefinition definition = VoxelWorldcraftCatalog.FindPiece(stored.PieceId)!;
            AppendEvent(new()
            {
                Tick = tick, Kind = "dismantle", PieceInstanceId = stored.InstanceId, PieceId = stored.PieceId,
                Anchor = stored.Anchor, RotationQuarterTurns = stored.RotationQuarterTurns,
                ConstructionRevision = Revision, WorldRevision = worldRevision,
                InventoryDeltas = definition.Cost.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            });
        }

        public void RecordGather(long tick, VoxelCoord coord, string itemId, long worldRevision)
        {
            if (!CanRecordEvent) throw new InvalidOperationException("Construction event capacity was not reserved before gather.");
            AppendEvent(new()
            {
                Tick = tick, Kind = "gather", ItemId = itemId, Coord = coord,
                ConstructionRevision = Revision, WorldRevision = worldRevision,
                InventoryDeltas = new(StringComparer.Ordinal) { [itemId] = 1 }
            });
        }

        public void RecordVoxelEdit(VoxelChangeEvent change)
        {
            if (EventSequence == 0 && Revision == 0 && _pieces.Count == 0)
            {
                if (change.Revision != BaseWorldRevision + 1) throw new InvalidOperationException("Pristine construction world cursor is not contiguous.");
                BaseWorldRevision = change.Revision;
                return;
            }
            if (!CanRecordEvent) throw new InvalidOperationException("Construction event capacity was not reserved before voxel edit.");
            AppendEvent(new()
            {
                Tick = change.Tick, Kind = "voxel_edit", Coord = change.Coord,
                ConstructionRevision = Revision, WorldRevision = change.Revision,
                InventoryDeltas = new(StringComparer.Ordinal)
            });
        }

        public WorldcraftConstructionSnapshot CaptureSnapshot() => new()
        {
            BaseWorldRevision = BaseWorldRevision, Revision = Revision, NextPieceSequence = NextPieceSequence, EventSequence = EventSequence,
            Pieces = _pieces.Select(ClonePiece).ToList(), Events = _events.Select(CloneEvent).ToList()
        };

        public static WorldcraftConstructionState Restore(WorldcraftConstructionSnapshot snapshot, VoxelWorldModule world,
            IReadOnlyDictionary<string, int> finalInventory, long simulationTick) =>
            Restore(snapshot, world, finalInventory, simulationTick, out _);

        internal static WorldcraftConstructionState Restore(WorldcraftConstructionSnapshot snapshot, VoxelWorldModule world,
            IReadOnlyDictionary<string, int> finalInventory, long simulationTick, out WorldcraftRestoreReplayWork replayWork)
        {
            replayWork = default;
            if (snapshot == null || snapshot.BaseWorldRevision < 0 || snapshot.BaseWorldRevision > world.WorldRevision ||
                snapshot.Revision < 0 || snapshot.NextPieceSequence < 0 || snapshot.EventSequence < 0 ||
                snapshot.Pieces == null || snapshot.Events == null || snapshot.Pieces.Count > MaximumPieceCount ||
                snapshot.EventSequence > MaximumEventCount || snapshot.Events.Count != snapshot.EventSequence || simulationTick < 0)
                throw new InvalidOperationException("Construction snapshot metadata is invalid.");

            Dictionary<string, WorldcraftPieceSnapshot> active = new(StringComparer.Ordinal);
            Dictionary<string, int> inventory = new(StringComparer.Ordinal);
            HashSet<long> gatherRevisions = new();
            long pieceSequence = 0, constructionRevision = 0, previousTick = -1;
            long expectedWorldRevision = snapshot.BaseWorldRevision;
            VoxelWorldModule.ReplayCursor replayCursor = world.CreateReplayCursor(expectedWorldRevision);
            for (int index = 0; index < snapshot.Events.Count; index++)
            {
                WorldcraftConstructionEvent value = snapshot.Events[index] ?? throw new InvalidOperationException("Construction event is null.");
                if (value.Sequence != index + 1L || value.Tick < previousTick || value.Tick > simulationTick ||
                    value.WorldRevision < expectedWorldRevision || value.WorldRevision > world.WorldRevision || value.InventoryDeltas == null)
                    throw new InvalidOperationException("Construction event ordering is invalid.");
                previousTick = value.Tick;
                if (value.Kind == "gather")
                {
                    expectedWorldRevision = checked(expectedWorldRevision + 1);
                    if (value.WorldRevision != expectedWorldRevision) throw new InvalidOperationException("Gather world revision is not canonical.");
                    ValidateGather(value, world, constructionRevision, gatherRevisions);
                    replayCursor.AdvanceTo(expectedWorldRevision);
                    if (!AllReplayPiecesSupported(active.Values, replayCursor.World))
                        throw new InvalidOperationException("Historical gather orphaned construction.");
                }
                else if (value.Kind == "voxel_edit")
                {
                    expectedWorldRevision = checked(expectedWorldRevision + 1);
                    if (value.WorldRevision != expectedWorldRevision) throw new InvalidOperationException("Voxel edit world revision is not canonical.");
                    ValidateVoxelEdit(value, world, constructionRevision, active);
                    replayCursor.AdvanceTo(expectedWorldRevision);
                    if (!AllReplayPiecesSupported(active.Values, replayCursor.World))
                        throw new InvalidOperationException("Historical raw voxel edit orphaned construction.");
                }
                else if (value.Kind == "place")
                {
                    if (value.WorldRevision != expectedWorldRevision) throw new InvalidOperationException("Placement world revision is not canonical.");
                    pieceSequence = checked(pieceSequence + 1); constructionRevision = checked(constructionRevision + 1);
                    ValidatePieceEvent(value, "place", FormatPieceId(pieceSequence), constructionRevision, active);
                    ValidateHistoricalPlacement(value, replayCursor.World, active.Values);
                    active.Add(value.PieceInstanceId, new() { InstanceId = value.PieceInstanceId, PieceId = value.PieceId,
                        Anchor = value.Anchor, RotationQuarterTurns = value.RotationQuarterTurns, PlacedTick = value.Tick });
                }
                else if (value.Kind == "dismantle")
                {
                    if (value.WorldRevision != expectedWorldRevision) throw new InvalidOperationException("Dismantle world revision is not canonical.");
                    constructionRevision = checked(constructionRevision + 1);
                    ValidatePieceEvent(value, "dismantle", value.PieceInstanceId, constructionRevision, active);
                    WorldcraftPieceSnapshot placed = active[value.PieceInstanceId];
                    if (placed.PieceId != value.PieceId || placed.Anchor != value.Anchor || placed.RotationQuarterTurns != value.RotationQuarterTurns)
                        throw new InvalidOperationException("Dismantle metadata differs from placement.");
                    active.Remove(value.PieceInstanceId);
                    if (!AllReplayPiecesSupported(active.Values, replayCursor.World))
                        throw new InvalidOperationException("Historical dismantle orphaned construction.");
                }
                else throw new InvalidOperationException("Construction event kind is invalid.");
                if (value.InventoryDeltas.Count > 0) ApplyInventoryDeltas(inventory, value.InventoryDeltas);
            }
            if (snapshot.NextPieceSequence != pieceSequence || snapshot.Revision != constructionRevision ||
                expectedWorldRevision != world.WorldRevision || replayCursor.World.WorldRevision != world.WorldRevision ||
                !string.Equals(replayCursor.World.RootHash, world.RootHash, StringComparison.Ordinal) ||
                !CountMapsEqual(inventory, finalInventory))
                throw new InvalidOperationException("Construction counters or inventory do not replay.");

            Dictionary<string, WorldcraftPieceSnapshot> declared = new(StringComparer.Ordinal);
            HashSet<VoxelCoord> occupied = new();
            foreach (WorldcraftPieceSnapshot piece in snapshot.Pieces.OrderBy(piece => piece.InstanceId, StringComparer.Ordinal))
            {
                ValidateFinalPiece(piece, world, occupied);
                if (!declared.TryAdd(piece.InstanceId, ClonePiece(piece))) throw new InvalidOperationException("Duplicate piece identity.");
                foreach (VoxelCoord cell in CellsFor(VoxelWorldcraftCatalog.FindPiece(piece.PieceId)!, piece.Anchor, piece.RotationQuarterTurns)) occupied.Add(cell);
            }
            if (declared.Count != active.Count || declared.Any(pair => !active.TryGetValue(pair.Key, out WorldcraftPieceSnapshot? replayed) || !PiecesEqual(pair.Value, replayed)))
                throw new InvalidOperationException("Construction events do not reproduce final pieces.");

            WorldcraftConstructionState state = new(snapshot.BaseWorldRevision)
                { Revision = constructionRevision, NextPieceSequence = pieceSequence, EventSequence = snapshot.EventSequence };
            state._pieces.AddRange(snapshot.Pieces.Select(ClonePiece));
            state._events.AddRange(snapshot.Events.Select(CloneEvent));
            if (!state.AllPiecesSupported(world, null, null)) throw new InvalidOperationException("Construction contains orphaned pieces.");
            replayWork = new(replayCursor.WorldGenerationCount, replayCursor.AppliedEventCount);
            return state;
        }

        private static void ValidateGather(WorldcraftConstructionEvent value, VoxelWorldModule world,
            long constructionRevision, HashSet<long> gatherRevisions)
        {
            if (value.ConstructionRevision != constructionRevision || !VoxelWorldcraftCatalog.IsKnownItem(value.ItemId) ||
                value.PieceInstanceId.Length != 0 || value.PieceId.Length != 0 || value.RotationQuarterTurns != 0 ||
                value.Anchor != default ||
                !ExactDeltas(value.InventoryDeltas, new Dictionary<string, int> { [value.ItemId] = 1 }) ||
                value.WorldRevision <= 0 || !gatherRevisions.Add(value.WorldRevision))
                throw new InvalidOperationException("Gather history is invalid.");
            VoxelChangeEvent voxelEvent = world.GetEventAtRevision(value.WorldRevision);
            if (voxelEvent.Kind != VoxelEditKind.Remove || voxelEvent.Coord != value.Coord || voxelEvent.Tick != value.Tick ||
                VoxelWorldcraftCatalog.ItemFor(voxelEvent.Before) != value.ItemId || voxelEvent.After != VoxelMaterialId.Air)
                throw new InvalidOperationException("Gather does not match voxel removal.");
        }

        private static void ValidateVoxelEdit(WorldcraftConstructionEvent value, VoxelWorldModule world,
            long constructionRevision, IReadOnlyDictionary<string, WorldcraftPieceSnapshot> active)
        {
            if (value.ConstructionRevision != constructionRevision || value.ItemId.Length != 0 ||
                value.PieceInstanceId.Length != 0 || value.PieceId.Length != 0 || value.RotationQuarterTurns != 0 ||
                value.Anchor != default ||
                value.InventoryDeltas.Count != 0)
                throw new InvalidOperationException("Raw voxel edit marker metadata is invalid.");
            VoxelChangeEvent voxelEvent = world.GetEventAtRevision(value.WorldRevision);
            if (voxelEvent.Coord != value.Coord || voxelEvent.Tick != value.Tick ||
                (voxelEvent.Kind == VoxelEditKind.Place && active.Values.SelectMany(piece =>
                    CellsFor(VoxelWorldcraftCatalog.FindPiece(piece.PieceId)!, piece.Anchor, piece.RotationQuarterTurns)).Contains(voxelEvent.Coord)))
                throw new InvalidOperationException("Raw voxel edit marker does not match safe voxel history.");
        }

        private static void ValidateHistoricalPlacement(WorldcraftConstructionEvent value, VoxelWorldModule historicalWorld,
            IEnumerable<WorldcraftPieceSnapshot> active)
        {
            WorldcraftPieceDefinition definition = VoxelWorldcraftCatalog.FindPiece(value.PieceId)!;
            IReadOnlyList<VoxelCoord> cells = CellsFor(definition, value.Anchor, value.RotationQuarterTurns);
            HashSet<VoxelCoord> occupied = active.SelectMany(piece =>
                CellsFor(VoxelWorldcraftCatalog.FindPiece(piece.PieceId)!, piece.Anchor, piece.RotationQuarterTurns)).ToHashSet();
            if (cells.Any(cell => !historicalWorld.Contains(cell) || historicalWorld.GetMaterial(cell) != VoxelMaterialId.Air || occupied.Contains(cell)) ||
                !cells.Any(cell => IsSupported(cell, historicalWorld, occupied)))
                throw new InvalidOperationException("Historical placement geometry is impossible at its declared world revision.");
        }

        private static bool AllReplayPiecesSupported(IEnumerable<WorldcraftPieceSnapshot> pieces, VoxelWorldModule world)
        {
            List<WorldcraftPieceSnapshot> remaining = pieces.OrderBy(piece => piece.InstanceId, StringComparer.Ordinal).ToList();
            HashSet<VoxelCoord> anchored = new();
            bool changed;
            do
            {
                changed = false;
                foreach (WorldcraftPieceSnapshot piece in remaining.ToArray())
                {
                    IReadOnlyList<VoxelCoord> cells = CellsFor(VoxelWorldcraftCatalog.FindPiece(piece.PieceId)!, piece.Anchor, piece.RotationQuarterTurns);
                    if (!cells.Any(cell =>
                    {
                        VoxelCoord below = new(cell.X, cell.Y - 1, cell.Z);
                        return anchored.Contains(below) || world.Contains(below) && world.GetMaterial(below) != VoxelMaterialId.Air;
                    })) continue;
                    foreach (VoxelCoord cell in cells) anchored.Add(cell);
                    remaining.Remove(piece);
                    changed = true;
                }
            } while (changed);
            return remaining.Count == 0;
        }

        private static void ValidatePieceEvent(WorldcraftConstructionEvent value, string kind, string expectedId,
            long expectedRevision, IReadOnlyDictionary<string, WorldcraftPieceSnapshot> active)
        {
            WorldcraftPieceDefinition definition = VoxelWorldcraftCatalog.FindPiece(value.PieceId)
                ?? throw new InvalidOperationException("Unknown piece in event.");
            bool lifecycle = kind == "place" ? !active.ContainsKey(value.PieceInstanceId) : active.ContainsKey(value.PieceInstanceId);
            IReadOnlyDictionary<string, int> deltas = definition.Cost.ToDictionary(pair => pair.Key,
                pair => kind == "place" ? -pair.Value : pair.Value, StringComparer.Ordinal);
            if (value.PieceInstanceId != expectedId || value.ConstructionRevision != expectedRevision || value.ItemId.Length != 0 ||
                value.Coord != default ||
                value.RotationQuarterTurns is < 0 or > 3 || (!definition.Rotates && value.RotationQuarterTurns != 0) ||
                !lifecycle || !ExactDeltas(value.InventoryDeltas, deltas))
                throw new InvalidOperationException("Piece event lifecycle or cost is invalid.");
        }

        private static void ValidateFinalPiece(WorldcraftPieceSnapshot piece, VoxelWorldModule world, HashSet<VoxelCoord> occupied)
        {
            WorldcraftPieceDefinition definition = VoxelWorldcraftCatalog.FindPiece(piece.PieceId)
                ?? throw new InvalidOperationException("Unknown final piece.");
            if (string.IsNullOrWhiteSpace(piece.InstanceId) || piece.PlacedTick < 0 || piece.RotationQuarterTurns is < 0 or > 3 ||
                (!definition.Rotates && piece.RotationQuarterTurns != 0)) throw new InvalidOperationException("Final piece metadata is invalid.");
            if (CellsFor(definition, piece.Anchor, piece.RotationQuarterTurns).Any(cell =>
                !world.Contains(cell) || world.GetMaterial(cell) != VoxelMaterialId.Air || occupied.Contains(cell)))
                throw new InvalidOperationException("Final pieces collide.");
        }

        private bool AllPiecesSupported(VoxelWorldModule world, VoxelCoord? removedVoxel, string? excludedId)
        {
            List<WorldcraftPieceSnapshot> remaining = _pieces.Where(piece => piece.InstanceId != excludedId)
                .OrderBy(piece => piece.InstanceId, StringComparer.Ordinal).ToList();
            HashSet<VoxelCoord> anchored = new();
            bool changed;
            do
            {
                changed = false;
                foreach (WorldcraftPieceSnapshot piece in remaining.ToArray())
                {
                    IReadOnlyList<VoxelCoord> cells = GetCells(piece);
                    if (!cells.Any(cell =>
                    {
                        VoxelCoord below = new(cell.X, cell.Y - 1, cell.Z);
                        return anchored.Contains(below) || (world.Contains(below) && below != removedVoxel && world.GetMaterial(below) != VoxelMaterialId.Air);
                    })) continue;
                    foreach (VoxelCoord cell in cells) anchored.Add(cell);
                    remaining.Remove(piece); changed = true;
                }
            } while (changed);
            return remaining.Count == 0;
        }

        private void AppendEvent(WorldcraftConstructionEvent value)
        {
            value.Sequence = checked(EventSequence + 1); EventSequence = value.Sequence; _events.Add(CloneEvent(value));
        }

        private static void ApplyInventoryDeltas(Dictionary<string, int> inventory, IReadOnlyDictionary<string, int> deltas)
        {
            if (deltas.Count == 0 || deltas.Any(pair => !VoxelWorldcraftCatalog.IsKnownItem(pair.Key) || pair.Value == 0))
                throw new InvalidOperationException("Inventory delta is invalid.");
            foreach ((string itemId, int delta) in deltas)
            {
                int next = checked((inventory.TryGetValue(itemId, out int current) ? current : 0) + delta);
                if (next < 0) throw new InvalidOperationException("Inventory replay underflowed.");
                if (next == 0) inventory.Remove(itemId); else inventory[itemId] = next;
            }
            InventoryComponent bounded = new();
            bounded.ReplaceContentsAndConfigureBoundedStorage(inventory, VoxelWorldcraftCatalog.HotbarSlots,
                VoxelWorldcraftCatalog.StackLimit, VoxelWorldcraftCatalog.HotbarOrder);
        }

        private static bool ExactDeltas(IReadOnlyDictionary<string, int> actual, IReadOnlyDictionary<string, int> expected) =>
            actual.Count == expected.Count && actual.All(pair => expected.TryGetValue(pair.Key, out int amount) && amount == pair.Value);
        private static bool CountMapsEqual(IReadOnlyDictionary<string, int> left, IReadOnlyDictionary<string, int> right) =>
            left.Where(pair => pair.Value != 0).OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .SequenceEqual(right.Where(pair => pair.Value != 0).OrderBy(pair => pair.Key, StringComparer.Ordinal));
        private static bool PiecesEqual(WorldcraftPieceSnapshot left, WorldcraftPieceSnapshot right) => left.InstanceId == right.InstanceId &&
            left.PieceId == right.PieceId && left.Anchor == right.Anchor && left.RotationQuarterTurns == right.RotationQuarterTurns && left.PlacedTick == right.PlacedTick;
        private static WorldcraftPieceSnapshot ClonePiece(WorldcraftPieceSnapshot piece) => new()
        { InstanceId = piece.InstanceId, PieceId = piece.PieceId, Anchor = piece.Anchor, RotationQuarterTurns = piece.RotationQuarterTurns, PlacedTick = piece.PlacedTick };
        private static WorldcraftConstructionEvent CloneEvent(WorldcraftConstructionEvent value) => new()
        {
            Sequence = value.Sequence, Tick = value.Tick, Kind = value.Kind, PieceInstanceId = value.PieceInstanceId,
            PieceId = value.PieceId, ItemId = value.ItemId, Coord = value.Coord, Anchor = value.Anchor,
            RotationQuarterTurns = value.RotationQuarterTurns, ConstructionRevision = value.ConstructionRevision,
            WorldRevision = value.WorldRevision, InventoryDeltas = new(value.InventoryDeltas, StringComparer.Ordinal)
        };
        private static WorldcraftPlacementEvaluation Reject(WorldcraftRejection rejection, WorldcraftPieceDefinition? definition = null,
            IReadOnlyList<VoxelCoord>? cells = null, VoxelCoord anchor = default, int rotationQuarterTurns = 0) => new()
            {
                Rejection = rejection, Definition = definition, Anchor = anchor, RotationQuarterTurns = NormalizeRotation(rotationQuarterTurns),
                Cells = Array.AsReadOnly((cells ?? Array.Empty<VoxelCoord>()).ToArray())
            };
        private static int NormalizeRotation(int rotationQuarterTurns) => ((rotationQuarterTurns % 4) + 4) % 4;
        private static int Chebyshev(VoxelCoord a, VoxelCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Max(Math.Abs(a.Y - b.Y), Math.Abs(a.Z - b.Z)));
        private static bool IsSupported(VoxelCoord cell, VoxelWorldModule world, HashSet<VoxelCoord> pieces)
        { VoxelCoord below = new(cell.X, cell.Y - 1, cell.Z); return world.Contains(below) && world.GetMaterial(below) != VoxelMaterialId.Air || pieces.Contains(below); }
        internal static IReadOnlyList<VoxelCoord> CellsFor(WorldcraftPieceDefinition definition, VoxelCoord anchor, int rotation) =>
            Array.AsReadOnly(definition.LocalCells.Select(local =>
            {
                (int x, int z) = rotation switch { 0 => (local.X, local.Z), 1 => (-local.Z, local.X), 2 => (-local.X, -local.Z), _ => (local.Z, -local.X) };
                return new VoxelCoord(anchor.X + x, anchor.Y + local.Y, anchor.Z + z);
            }).OrderBy(cell => cell.X).ThenBy(cell => cell.Y).ThenBy(cell => cell.Z).ToArray());
        private static string FormatPieceId(long sequence) => $"piece-{sequence:D6}";
    }
}
