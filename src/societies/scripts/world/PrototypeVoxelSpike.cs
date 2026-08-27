using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Societies.Core
{
    // Persisted ids; never reorder.
    public enum VoxelMaterialId : byte { Air = 0, Soil = 1, Stone = 2, Wood = 3, Bedrock = 4, WaterBlocked = Bedrock }
    public readonly record struct VoxelChunkCoord(int X, int Y, int Z) : IComparable<VoxelChunkCoord>
    { public int CompareTo(VoxelChunkCoord other) => X != other.X ? X.CompareTo(other.X) : Z != other.Z ? Z.CompareTo(other.Z) : Y.CompareTo(other.Y); public override string ToString() => FormattableString.Invariant($"{X}:{Y}:{Z}"); }
    public readonly record struct VoxelCoord(int X, int Y, int Z);
    public enum VoxelEditKind { Remove, Place }
    public enum VoxelEditRejection { None, StaleRevision, OutOfBounds, InvalidActor, InvalidTick, InvalidEditKind, InvalidMaterial, CellMismatch, ImmutableBedrock, RemoveAir, PlaceNonAir, NoOp, EventCapacityReached, TickMismatch, NonSurfaceEdit, UnsupportedPlacement, OrphanedConstruction, PieceOccupied }
    public sealed class VoxelEditCommand { public string ActorId { get; init; } = string.Empty; public long Tick { get; init; } public long ExpectedWorldRevision { get; init; } public VoxelEditKind Kind { get; init; } public VoxelCoord Coord { get; init; } public VoxelMaterialId ExpectedBefore { get; init; } public VoxelMaterialId After { get; init; } }
    public sealed record VoxelChangeEvent(long Sequence, long Tick, string ActorId, VoxelEditKind Kind, VoxelCoord Coord, VoxelMaterialId Before, VoxelMaterialId After, long Revision);
    public sealed class VoxelEditResult { public bool Accepted { get; init; } public VoxelEditRejection Rejection { get; init; } public long WorldRevision { get; init; } public VoxelChangeEvent? Change { get; init; } public IReadOnlyList<VoxelChunkCoord> DirtyChunks { get; init; } = Array.Empty<VoxelChunkCoord>(); }

    public sealed class VoxelChunkSnapshot { public int X { get; set; } public int Y { get; set; } public int Z { get; set; } public List<string> PayloadSegments { get; set; } = new(); public string Hash { get; set; } = string.Empty; }
    public sealed class VoxelWorldSnapshot
    {
        public string Schema { get; set; } = VoxelWorldModule.SnapshotSchema; public string Generator { get; set; } = VoxelWorldModule.GeneratorIdentity; public string Materials { get; set; } = VoxelWorldModule.MaterialIdentity;
        public int Seed { get; set; } public int MinX { get; set; } public int MaxXExclusive { get; set; } public int MinY { get; set; } public int MaxYExclusive { get; set; } public int MinZ { get; set; } public int MaxZExclusive { get; set; }
        public long WorldRevision { get; set; } public long EventSequence { get; set; } public List<VoxelChangeEvent> Events { get; set; } = new(); public List<VoxelChunkSnapshot> Chunks { get; set; } = new(); public string WorldIdentity { get; set; } = string.Empty; public string RootHash { get; set; } = string.Empty;
    }
    public readonly record struct VoxelVertex(float X, float Y, float Z, VoxelMaterialId Material);
    public readonly record struct VoxelColumnSurfaceProjection(int X, int Z, int SurfaceY);
    public readonly record struct VoxelVerticalRunProjection(int X, int Z, int MinYInclusive, int MaxYExclusive);
    public enum VoxelSafeSpawnContract { StrictFiveByFiveClearing, MinimumReliefFallback }
    public readonly record struct VoxelSafeSpawn(int X, int Z, int SurfaceY, VoxelSafeSpawnContract Contract, int NeighborhoodRelief, int MaximumNeighborRise, int CameraClearanceCells);
    public sealed class VoxelChunkGeometryProjection { public VoxelChunkGeometryProjection(VoxelChunkCoord coord, IReadOnlyList<VoxelVertex> vertices, IReadOnlyList<int> indices, IReadOnlyList<VoxelColumnSurfaceProjection> surfaces, IReadOnlyList<VoxelVerticalRunProjection> occupiedRuns) { Coord = coord; Vertices = vertices; Indices = indices; Surfaces = surfaces; OccupiedRuns = occupiedRuns; } public VoxelChunkCoord Coord { get; } public IReadOnlyList<VoxelVertex> Vertices { get; } public IReadOnlyList<int> Indices { get; } public IReadOnlyList<VoxelColumnSurfaceProjection> Surfaces { get; } public IReadOnlyList<VoxelVerticalRunProjection> OccupiedRuns { get; } public int QuadCount => Indices.Count / 6; }
    public readonly record struct VoxelWalkableSpan(int X, int Z, int SupportY);
    public sealed class VoxelWorldProjection { public VoxelWorldProjection(long revision, IReadOnlyList<VoxelChunkGeometryProjection> chunks, IReadOnlyList<VoxelWalkableSpan> walkable) { Revision = revision; Chunks = chunks; Walkable = walkable; } public long Revision { get; } public IReadOnlyList<VoxelChunkGeometryProjection> Chunks { get; } public IReadOnlyList<VoxelWalkableSpan> Walkable { get; } }

    /// <summary>Finite eager deterministic authority. It exposes immutable projections only.</summary>
    public sealed class VoxelWorldModule
    {
        public const int ChunkWidth = 16, ChunkHeight = 32, ChunkDepth = 16, ChunkVoxelCount = 8192, ChunkCount = 64;
        public const int MinX = -64, MaxXExclusive = 64, MinY = 0, MaxYExclusive = 32, MinZ = -64, MaxZExclusive = 64;
        public const long MaximumEventCount = PrototypePersistenceBounds.MaximumSnapshotRows;
        public const string SnapshotSchema = "societies_voxel_world_snapshot/v1", GeneratorIdentity = "societies_voxel_generator/v2", LegacyGeneratorIdentity = "societies_voxel_generator/v1", MaterialIdentity = "air0-soil1-stone2-wood3-bedrock4";
        public const int SpawnClearingRadius = 4, SpawnClearingTerrainHeight = 12;
        private readonly Dictionary<VoxelChunkCoord, byte[]> _chunks = new(); private readonly List<VoxelChangeEvent> _events = new(); private readonly string _generatorIdentity;
        public VoxelWorldModule(int seed) : this(seed, GeneratorIdentity) { }
        private VoxelWorldModule(int seed, string generatorIdentity) { Seed = seed; _generatorIdentity = generatorIdentity; foreach (VoxelChunkCoord coord in ExpectedChunkCoords()) _chunks.Add(coord, GenerateChunk(seed, coord, generatorIdentity)); }
        private VoxelWorldModule(int seed, Dictionary<VoxelChunkCoord, byte[]> chunks, long revision, long sequence, IEnumerable<VoxelChangeEvent> events, string generatorIdentity) { Seed = seed; _generatorIdentity = generatorIdentity; _chunks = chunks; WorldRevision = revision; EventSequence = sequence; _events.AddRange(events); }
        public int Seed { get; } public long WorldRevision { get; private set; } public long EventSequence { get; private set; } public IReadOnlyList<VoxelChangeEvent> Events => new ReadOnlyCollection<VoxelChangeEvent>(_events); public string WorldIdentity => GetWorldIdentity(Seed, _generatorIdentity); public string RootHash => ComputeRootHash();
        public bool Contains(VoxelCoord c) => c.X >= MinX && c.X < MaxXExclusive && c.Y >= MinY && c.Y < MaxYExclusive && c.Z >= MinZ && c.Z < MaxZExclusive;
        public VoxelMaterialId GetMaterial(VoxelCoord c) { if (!Contains(c)) return VoxelMaterialId.Air; (VoxelChunkCoord chunk, int index) = Locate(c); return (VoxelMaterialId)_chunks[chunk][index]; }
        public VoxelEditResult Execute(VoxelEditCommand command) => ExecuteValidated(command, allowLegacyShape: false);
        private VoxelEditResult ExecuteValidated(VoxelEditCommand command, bool allowLegacyShape)
        {
            VoxelEditRejection rejection = Validate(command, allowLegacyShape); if (rejection != VoxelEditRejection.None) return new() { Rejection = rejection, WorldRevision = WorldRevision };
            if (WorldRevision != EventSequence || WorldRevision >= MaximumEventCount) return new() { Rejection = VoxelEditRejection.EventCapacityReached, WorldRevision = WorldRevision };
            (VoxelChunkCoord chunk, int index) = Locate(command.Coord); byte before = _chunks[chunk][index]; _chunks[chunk][index] = (byte)command.After; WorldRevision++;
            VoxelChangeEvent change = new(++EventSequence, command.Tick, command.ActorId, command.Kind, command.Coord, (VoxelMaterialId)before, command.After, WorldRevision); _events.Add(change);
            return new() { Accepted = true, WorldRevision = WorldRevision, Change = change, DirtyChunks = DirtyChunksFor(command.Coord) };
        }
        public IReadOnlyList<VoxelChunkCoord> DirtyChunksFor(VoxelCoord c)
        {
            if (!Contains(c)) return Array.Empty<VoxelChunkCoord>(); (VoxelChunkCoord chunk, _) = Locate(c); List<VoxelChunkCoord> result = new() { chunk };
            void Add(VoxelChunkCoord candidate) { if (_chunks.ContainsKey(candidate)) result.Add(candidate); }
            if (Mod(c.X, ChunkWidth) == 0) Add(chunk with { X = chunk.X - 1 }); if (Mod(c.X, ChunkWidth) == ChunkWidth - 1) Add(chunk with { X = chunk.X + 1 }); if (Mod(c.Z, ChunkDepth) == 0) Add(chunk with { Z = chunk.Z - 1 }); if (Mod(c.Z, ChunkDepth) == ChunkDepth - 1) Add(chunk with { Z = chunk.Z + 1 });
            return result.OrderBy(value => value).ToArray();
        }
        public VoxelWorldProjection CaptureProjection(IEnumerable<VoxelChunkCoord>? scope = null)
        { IReadOnlyList<VoxelChunkCoord> selected = (scope ?? _chunks.Keys).Where(_chunks.ContainsKey).Distinct().OrderBy(value => value).ToArray(); return new(WorldRevision, selected.Select(BuildGeometry).ToArray(), BuildWalkableSpans(selected)); }
        public IReadOnlyList<VoxelWalkableSpan> CaptureWalkableSpans() => BuildWalkableSpans(_chunks.Keys.OrderBy(value => value).ToArray());
        internal sealed class ReplayCursor
        {
            private readonly VoxelWorldModule _source;

            internal ReplayCursor(VoxelWorldModule source, long revision)
            {
                if (revision < 0 || revision > source.WorldRevision) throw new ArgumentOutOfRangeException(nameof(revision));
                _source = source;
                World = new VoxelWorldModule(source.Seed, source._generatorIdentity);
                AdvanceTo(revision);
            }

            internal VoxelWorldModule World { get; }
            internal long WorldGenerationCount => 1;
            internal long AppliedEventCount { get; private set; }

            internal void AdvanceTo(long revision)
            {
                if (revision < World.WorldRevision || revision > _source.WorldRevision)
                    throw new ArgumentOutOfRangeException(nameof(revision));
                while (World.WorldRevision < revision)
                {
                    VoxelChangeEvent value = _source._events[checked((int)World.WorldRevision)];
                    VoxelEditResult result = World.ExecuteValidated(new VoxelEditCommand
                    {
                        ActorId = value.ActorId, Tick = value.Tick, ExpectedWorldRevision = value.Sequence - 1,
                        Kind = value.Kind, Coord = value.Coord, ExpectedBefore = value.Before, After = value.After
                    }, _source._generatorIdentity == LegacyGeneratorIdentity);
                    if (!result.Accepted || result.Change != value)
                        throw new InvalidOperationException("Voxel history could not replay to requested revision.");
                    AppliedEventCount = checked(AppliedEventCount + 1);
                }
            }
        }
        internal ReplayCursor CreateReplayCursor(long revision) => new(this, revision);
        internal VoxelChangeEvent GetEventAtRevision(long revision)
        {
            if (revision <= 0 || revision > WorldRevision) throw new ArgumentOutOfRangeException(nameof(revision));
            VoxelChangeEvent value = _events[checked((int)revision - 1)];
            if (value.Revision != revision) throw new InvalidOperationException("Voxel history revision index is invalid.");
            return value;
        }
        public VoxelSafeSpawn FindSafePlayerSpawn()
        {
            if (_generatorIdentity == GeneratorIdentity)
            {
                foreach ((int x, int z) in Enumerable.Range(MinX + 2, MaxXExclusive - MinX - 4)
                .SelectMany(x => Enumerable.Range(MinZ + 2, MaxZExclusive - MinZ - 4).Select(z => (x, z)))
                .OrderBy(candidate => (candidate.x * candidate.x) + (candidate.z * candidate.z))
                .ThenBy(candidate => candidate.z)
                .ThenBy(candidate => candidate.x))
                {
                    (int surfaceY, int relief, int maximumRise, int clearance) = EvaluateSpawnCandidate(x, z);
                    if (relief <= 1 && maximumRise <= 1 && clearance >= 2)
                    {
                        return new VoxelSafeSpawn(x, z, surfaceY, VoxelSafeSpawnContract.StrictFiveByFiveClearing, relief, maximumRise, clearance);
                    }
                }
                throw new InvalidOperationException("Current voxel generator has no strict five-by-five player clearing.");
            }

            var fallback = Enumerable.Range(MinX + 2, MaxXExclusive - MinX - 4)
                .SelectMany(x => Enumerable.Range(MinZ + 2, MaxZExclusive - MinZ - 4).Select(z =>
                {
                    (int surfaceY, int relief, int maximumRise, int clearance) = EvaluateSpawnCandidate(x, z);
                    return (x, z, surfaceY, relief, maximumRise, clearance);
                }))
                .Where(candidate => candidate.maximumRise <= 1 && candidate.clearance >= 2)
                .OrderBy(candidate => candidate.relief)
                .ThenBy(candidate => (candidate.x * candidate.x) + (candidate.z * candidate.z))
                .ThenBy(candidate => candidate.z)
                .ThenBy(candidate => candidate.x)
                .FirstOrDefault();
            if (fallback.clearance < 2)
            {
                throw new InvalidOperationException("Legacy voxel world has no deterministic locally clear player spawn.");
            }
            return new VoxelSafeSpawn(fallback.x, fallback.z, fallback.surfaceY, VoxelSafeSpawnContract.MinimumReliefFallback, fallback.relief, fallback.maximumRise, fallback.clearance);
        }
        public VoxelWorldSnapshot CaptureSnapshot()
        { List<VoxelChunkSnapshot> chunks = ExpectedChunkCoords().Select(c => new VoxelChunkSnapshot { X = c.X, Y = c.Y, Z = c.Z, PayloadSegments = EncodeSegments(_chunks[c]), Hash = Hash(_chunks[c]) }).ToList(); return new() { Schema = SnapshotSchema, Generator = _generatorIdentity, Materials = MaterialIdentity, Seed = Seed, MinX = MinX, MaxXExclusive = MaxXExclusive, MinY = MinY, MaxYExclusive = MaxYExclusive, MinZ = MinZ, MaxZExclusive = MaxZExclusive, WorldRevision = WorldRevision, EventSequence = EventSequence, Events = _events.Select(value => new VoxelChangeEvent(value.Sequence, value.Tick, value.ActorId, value.Kind, value.Coord, value.Before, value.After, value.Revision)).ToList(), Chunks = chunks, WorldIdentity = WorldIdentity, RootHash = RootHash }; }
        public static VoxelWorldModule Restore(VoxelWorldSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Schema != SnapshotSchema || (snapshot.Generator != GeneratorIdentity && snapshot.Generator != LegacyGeneratorIdentity) || snapshot.Materials != MaterialIdentity || snapshot.MinX != MinX || snapshot.MaxXExclusive != MaxXExclusive || snapshot.MinY != MinY || snapshot.MaxYExclusive != MaxYExclusive || snapshot.MinZ != MinZ || snapshot.MaxZExclusive != MaxZExclusive || snapshot.WorldRevision < 0 || snapshot.EventSequence != snapshot.WorldRevision || snapshot.EventSequence > MaximumEventCount || snapshot.Events == null || snapshot.Events.Count != snapshot.EventSequence || snapshot.Chunks == null || snapshot.Chunks.Count != ChunkCount) throw new InvalidOperationException("Voxel snapshot metadata is invalid.");
            ValidateEventHistory(snapshot.Events);
            VoxelChunkCoord[] expected = ExpectedChunkCoords().ToArray(); Dictionary<VoxelChunkCoord, byte[]> chunks = new();
            for (int i = 0; i < expected.Length; i++) { VoxelChunkSnapshot row = snapshot.Chunks[i] ?? throw new InvalidOperationException("Voxel snapshot chunk is null."); VoxelChunkCoord coord = new(row.X, row.Y, row.Z); if (coord != expected[i] || !chunks.TryAdd(coord, DecodeChunk(row))) throw new InvalidOperationException("Voxel snapshot chunk order or identity is invalid."); }
            VoxelWorldModule candidate = new(snapshot.Seed, chunks, snapshot.WorldRevision, snapshot.EventSequence, snapshot.Events, snapshot.Generator);
            VoxelWorldModule replayed = new(snapshot.Seed, snapshot.Generator);
            foreach (VoxelChangeEvent value in snapshot.Events)
            {
                VoxelEditResult replay = replayed.ExecuteValidated(new VoxelEditCommand { ActorId = value.ActorId, Tick = value.Tick, ExpectedWorldRevision = value.Sequence - 1, Kind = value.Kind, Coord = value.Coord, ExpectedBefore = value.Before, After = value.After }, snapshot.Generator == LegacyGeneratorIdentity);
                if (!replay.Accepted || replay.Change != value) throw new InvalidOperationException("Voxel snapshot event history does not replay to its declared changes.");
            }
            if (!FixedEquals(candidate.WorldIdentity, snapshot.WorldIdentity) || !FixedEquals(candidate.RootHash, snapshot.RootHash) || !FixedEquals(replayed.RootHash, snapshot.RootHash)) throw new InvalidOperationException("Voxel snapshot identity, event replay, or root hash does not match payload."); return candidate;
        }
        private VoxelEditRejection Validate(VoxelEditCommand command, bool allowLegacyShape)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.ActorId) || command.ActorId.Length > 64) return VoxelEditRejection.InvalidActor; if (command.Tick < 0) return VoxelEditRejection.InvalidTick; if (!Enum.IsDefined(typeof(VoxelEditKind), command.Kind)) return VoxelEditRejection.InvalidEditKind; if (command.ExpectedWorldRevision != WorldRevision) return VoxelEditRejection.StaleRevision; if (!Contains(command.Coord)) return VoxelEditRejection.OutOfBounds; if (!Known(command.ExpectedBefore) || !Known(command.After)) return VoxelEditRejection.InvalidMaterial;
            VoxelMaterialId current = GetMaterial(command.Coord); if (current != command.ExpectedBefore) return VoxelEditRejection.CellMismatch; if (current == VoxelMaterialId.Bedrock) return VoxelEditRejection.ImmutableBedrock;
            if (command.Kind == VoxelEditKind.Remove && command.After != VoxelMaterialId.Air) return VoxelEditRejection.NoOp; if (command.Kind == VoxelEditKind.Place && command.After is not (VoxelMaterialId.Soil or VoxelMaterialId.Stone or VoxelMaterialId.Wood)) return VoxelEditRejection.InvalidMaterial; if (command.Kind == VoxelEditKind.Remove && current == VoxelMaterialId.Air) return VoxelEditRejection.RemoveAir; if (command.Kind == VoxelEditKind.Place && current != VoxelMaterialId.Air) return VoxelEditRejection.PlaceNonAir;
            int surfaceY = FindSurfaceY(command.Coord.X, command.Coord.Z);
            if (!allowLegacyShape && command.Kind == VoxelEditKind.Remove && command.Coord.Y != surfaceY - 1) return VoxelEditRejection.NonSurfaceEdit;
            if (!allowLegacyShape && command.Kind == VoxelEditKind.Place && command.Coord.Y != surfaceY) return VoxelEditRejection.UnsupportedPlacement;
            return current == command.After ? VoxelEditRejection.NoOp : VoxelEditRejection.None;
        }
        private VoxelChunkGeometryProjection BuildGeometry(VoxelChunkCoord chunk)
        {
            List<VoxelVertex> vertices = new(); List<int> indices = new();
            for (int y = 0; y < ChunkHeight; y++) for (int z = 0; z < ChunkDepth; z++) for (int x = 0; x < ChunkWidth; x++) { VoxelCoord c = new(chunk.X * ChunkWidth + x, y, chunk.Z * ChunkDepth + z); VoxelMaterialId material = GetMaterial(c); if (material == VoxelMaterialId.Air) continue; foreach ((int dx, int dy, int dz) in Faces) if (GetMaterial(new(c.X + dx, c.Y + dy, c.Z + dz)) == VoxelMaterialId.Air) AddFace(vertices, indices, c, material, dx, dy, dz); }
            IReadOnlyList<VoxelColumnSurfaceProjection> surfaces = Enumerable.Range(0, ChunkDepth).SelectMany(z => Enumerable.Range(0, ChunkWidth).Select(x =>
            {
                int worldX = (chunk.X * ChunkWidth) + x;
                int worldZ = (chunk.Z * ChunkDepth) + z;
                return new VoxelColumnSurfaceProjection(worldX, worldZ, FindSurfaceY(worldX, worldZ));
            })).ToArray();
            List<VoxelVerticalRunProjection> occupiedRuns = new();
            for (int z = 0; z < ChunkDepth; z++) for (int x = 0; x < ChunkWidth; x++)
            {
                int worldX = (chunk.X * ChunkWidth) + x, worldZ = (chunk.Z * ChunkDepth) + z, runStart = -1;
                for (int y = MinY; y <= MaxYExclusive; y++)
                {
                    bool occupied = y < MaxYExclusive && GetMaterial(new VoxelCoord(worldX, y, worldZ)) != VoxelMaterialId.Air;
                    if (occupied && runStart < 0) runStart = y;
                    else if (!occupied && runStart >= 0) { occupiedRuns.Add(new VoxelVerticalRunProjection(worldX, worldZ, runStart, y)); runStart = -1; }
                }
            }
            return new(chunk, vertices.AsReadOnly(), indices.AsReadOnly(), surfaces, occupiedRuns.AsReadOnly());
        }
        private int FindSurfaceY(int x, int z) { for (int y = MaxYExclusive - 1; y >= MinY; y--) if (GetMaterial(new VoxelCoord(x, y, z)) != VoxelMaterialId.Air) return y + 1; return MinY; }
        private (int SurfaceY, int Relief, int MaximumRise, int CameraClearance) EvaluateSpawnCandidate(int x, int z)
        {
            int surfaceY = FindSurfaceY(x, z);
            int[] neighborhood = Enumerable.Range(-2, 5).SelectMany(offsetX => Enumerable.Range(-2, 5).Select(offsetZ => FindSurfaceY(x + offsetX, z + offsetZ))).ToArray();
            int clearance = 0; for (int y = surfaceY; y < MaxYExclusive && GetMaterial(new VoxelCoord(x, y, z)) == VoxelMaterialId.Air; y++) clearance++;
            return (surfaceY, neighborhood.Max() - neighborhood.Min(), neighborhood.Max() - surfaceY, clearance);
        }
        private IReadOnlyList<VoxelWalkableSpan> BuildWalkableSpans(IReadOnlyList<VoxelChunkCoord> selected)
        {
            if (selected.Count == 0) return Array.Empty<VoxelWalkableSpan>();
            HashSet<(int X, int Z)> outputColumns = selected.SelectMany(chunk => Enumerable.Range(0, ChunkDepth).SelectMany(z => Enumerable.Range(0, ChunkWidth).Select(x => (X: chunk.X * ChunkWidth + x, Z: chunk.Z * ChunkDepth + z)))).ToHashSet();
            int minX = Math.Max(MinX, outputColumns.Min(column => column.X) - 1), maxX = Math.Min(MaxXExclusive - 1, outputColumns.Max(column => column.X) + 1), minZ = Math.Max(MinZ, outputColumns.Min(column => column.Z) - 1), maxZ = Math.Min(MaxZExclusive - 1, outputColumns.Max(column => column.Z) + 1);
            Dictionary<(int X, int Z), int> supports = new(); for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++) for (int y = MaxYExclusive - 3; y >= MinY; y--) if (GetMaterial(new(x, y, z)) != VoxelMaterialId.Air && GetMaterial(new(x, y + 1, z)) == VoxelMaterialId.Air && GetMaterial(new(x, y + 2, z)) == VoxelMaterialId.Air) { supports[(x, z)] = y; break; }
            return supports.Where(pair => outputColumns.Contains(pair.Key) && new[] { (pair.Key.X - 1, pair.Key.Z), (pair.Key.X + 1, pair.Key.Z), (pair.Key.X, pair.Key.Z - 1), (pair.Key.X, pair.Key.Z + 1) }.Any(n => supports.TryGetValue(n, out int y) && Math.Abs(y - pair.Value) <= 1)).OrderBy(pair => pair.Key.Z).ThenBy(pair => pair.Key.X).Select(pair => new VoxelWalkableSpan(pair.Key.X, pair.Key.Z, pair.Value)).ToArray();
        }
        private static readonly (int, int, int)[] Faces = { (1,0,0), (-1,0,0), (0,1,0), (0,-1,0), (0,0,1), (0,0,-1) };
        internal static void AddFace(List<VoxelVertex> vs, List<int> ix, VoxelCoord c, VoxelMaterialId material, int dx, int dy, int dz)
        {
            VoxelVertex[] face = (dx, dy, dz) switch
            {
                (1, 0, 0) => new[] { new VoxelVertex(c.X + 1, c.Y, c.Z, material), new(c.X + 1, c.Y + 1, c.Z, material), new(c.X + 1, c.Y + 1, c.Z + 1, material), new(c.X + 1, c.Y, c.Z + 1, material) },
                (-1, 0, 0) => new[] { new VoxelVertex(c.X, c.Y, c.Z + 1, material), new(c.X, c.Y + 1, c.Z + 1, material), new(c.X, c.Y + 1, c.Z, material), new(c.X, c.Y, c.Z, material) },
                (0, 1, 0) => new[] { new VoxelVertex(c.X, c.Y + 1, c.Z, material), new(c.X, c.Y + 1, c.Z + 1, material), new(c.X + 1, c.Y + 1, c.Z + 1, material), new(c.X + 1, c.Y + 1, c.Z, material) },
                (0, -1, 0) => new[] { new VoxelVertex(c.X, c.Y, c.Z + 1, material), new(c.X, c.Y, c.Z, material), new(c.X + 1, c.Y, c.Z, material), new(c.X + 1, c.Y, c.Z + 1, material) },
                (0, 0, 1) => new[] { new VoxelVertex(c.X, c.Y, c.Z + 1, material), new(c.X + 1, c.Y, c.Z + 1, material), new(c.X + 1, c.Y + 1, c.Z + 1, material), new(c.X, c.Y + 1, c.Z + 1, material) },
                (0, 0, -1) => new[] { new VoxelVertex(c.X + 1, c.Y, c.Z, material), new(c.X, c.Y, c.Z, material), new(c.X, c.Y + 1, c.Z, material), new(c.X + 1, c.Y + 1, c.Z, material) },
                _ => throw new ArgumentOutOfRangeException(nameof(dx), "Voxel face direction must be axis aligned.")
            };
            int start = vs.Count; vs.AddRange(face); ix.Add(start); ix.Add(start + 2); ix.Add(start + 1); ix.Add(start); ix.Add(start + 3); ix.Add(start + 2);
        }
        private static IEnumerable<VoxelChunkCoord> ExpectedChunkCoords() { for (int x = -4; x < 4; x++) for (int z = -4; z < 4; z++) yield return new(x,0,z); }
        private (VoxelChunkCoord Chunk, int Index) Locate(VoxelCoord c) { VoxelChunkCoord chunk = new(FloorDiv(c.X,ChunkWidth),0,FloorDiv(c.Z,ChunkDepth)); return (chunk,(c.Y*ChunkWidth*ChunkDepth)+(Mod(c.Z,ChunkDepth)*ChunkWidth)+Mod(c.X,ChunkWidth)); }
        private static byte[] GenerateChunk(int seed, VoxelChunkCoord chunk, string generatorIdentity) { byte[] cells = new byte[ChunkVoxelCount]; for (int z=0;z<ChunkDepth;z++) for(int x=0;x<ChunkWidth;x++) { int wx=chunk.X*ChunkWidth+x,wz=chunk.Z*ChunkDepth+z; bool clearing=generatorIdentity==GeneratorIdentity&&Math.Abs(wx)<=SpawnClearingRadius&&Math.Abs(wz)<=SpawnClearingRadius; int terrain=clearing?SpawnClearingTerrainHeight:8+(int)(Hash32(seed,wx,wz,1)%7); for(int y=0;y<ChunkHeight;y++) cells[(y*ChunkWidth*ChunkDepth)+(z*ChunkWidth)+x]=(byte)(y==0?VoxelMaterialId.Bedrock:y<terrain-3?VoxelMaterialId.Stone:y<=terrain?VoxelMaterialId.Soil:VoxelMaterialId.Air); if(!clearing&&Hash32(seed,wx,wz,2)%113==0&&terrain+2<ChunkHeight) for(int y=terrain+1;y<=terrain+2;y++) cells[(y*ChunkWidth*ChunkDepth)+(z*ChunkWidth)+x]=(byte)VoxelMaterialId.Wood; } return cells; }
        private string ComputeRootHash() { using IncrementalHash hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256); hash.AppendData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{WorldIdentity}|{WorldRevision}|{EventSequence}|"))); foreach (VoxelChangeEvent value in _events) hash.AppendData(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{value.Sequence}|{value.Tick}|{value.ActorId.Length}:{value.ActorId}|{(int)value.Kind}|{value.Coord.X}:{value.Coord.Y}:{value.Coord.Z}|{(byte)value.Before}|{(byte)value.After}|{value.Revision};"))); foreach(VoxelChunkCoord c in ExpectedChunkCoords()){hash.AppendData(Encoding.UTF8.GetBytes(c.ToString()));hash.AppendData(_chunks[c]);} return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(); }
        public static string GetWorldIdentity(int seed) => GetWorldIdentity(seed, GeneratorIdentity);
        private static string GetWorldIdentity(int seed, string generatorIdentity) => Hash(Encoding.UTF8.GetBytes(FormattableString.Invariant($"{generatorIdentity}|{MaterialIdentity}|{seed}|{MinX}:{MaxXExclusive}|{MinY}:{MaxYExclusive}|{MinZ}:{MaxZExclusive}")));
        private static List<string> EncodeSegments(byte[] cells) { const int segmentBytes = 720; List<string> segments = new(); for (int offset = 0; offset < cells.Length; offset += segmentBytes) segments.Add(Convert.ToBase64String(cells, offset, Math.Min(segmentBytes, cells.Length - offset))); return segments; }
        private static byte[] DecodeChunk(VoxelChunkSnapshot row) { if(row.PayloadSegments == null || row.PayloadSegments.Count == 0 || row.PayloadSegments.Count > 16 || row.PayloadSegments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment.Length > 1024) || string.IsNullOrWhiteSpace(row.Hash)) throw new InvalidOperationException("Voxel snapshot chunk payload is missing or oversized."); List<byte> decoded = new(ChunkVoxelCount); try { foreach(string segment in row.PayloadSegments) decoded.AddRange(Convert.FromBase64String(segment)); } catch(FormatException e) { throw new InvalidOperationException("Voxel snapshot payload is not base64.",e); } byte[] cells=decoded.ToArray(); if(cells.Length!=ChunkVoxelCount||cells.Any(value=>!Known((VoxelMaterialId)value))||!FixedEquals(Hash(cells),row.Hash)) throw new InvalidOperationException("Voxel snapshot chunk payload is invalid."); return cells; }
        private static void ValidateEventHistory(IReadOnlyList<VoxelChangeEvent> events)
        {
            for (int index = 0; index < events.Count; index++)
            {
                VoxelChangeEvent value = events[index] ?? throw new InvalidOperationException("Voxel snapshot event is null."); long expected = index + 1L;
                bool coordValid = value.Coord.X >= MinX && value.Coord.X < MaxXExclusive && value.Coord.Y >= MinY && value.Coord.Y < MaxYExclusive && value.Coord.Z >= MinZ && value.Coord.Z < MaxZExclusive;
                bool transitionValid = value.Kind switch { VoxelEditKind.Remove => value.Before is (VoxelMaterialId.Soil or VoxelMaterialId.Stone or VoxelMaterialId.Wood) && value.After == VoxelMaterialId.Air, VoxelEditKind.Place => value.Before == VoxelMaterialId.Air && value.After is (VoxelMaterialId.Soil or VoxelMaterialId.Stone or VoxelMaterialId.Wood), _ => false };
                if (value.Sequence != expected || value.Revision != expected || value.Tick < 0 || string.IsNullOrWhiteSpace(value.ActorId) || value.ActorId.Length > 64 || !coordValid || !Known(value.Before) || !Known(value.After) || !transitionValid) throw new InvalidOperationException("Voxel snapshot event history is invalid.");
            }
        }
        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); private static bool FixedEquals(string left,string right) => left != null && right != null && left.Length==64 && right.Length==64 && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left),Encoding.ASCII.GetBytes(right)); private static bool Known(VoxelMaterialId material) => (byte)material <= (byte)VoxelMaterialId.Bedrock;
        private static int FloorDiv(int value,int divisor) => value >= 0 ? value/divisor : ((value+1)/divisor)-1; private static int Mod(int value,int divisor) { int result=value%divisor; return result<0?result+divisor:result; } private static uint Hash32(int seed,int x,int z,int salt) { unchecked { uint v=(uint)seed^((uint)x*0x9E3779B9u)^((uint)z*0x85EBCA6Bu)^((uint)salt*0xC2B2AE35u);v^=v>>16;v*=0x7FEB352Du;v^=v>>15;v*=0x846CA68Bu;return v^(v>>16); } }
    }
}
