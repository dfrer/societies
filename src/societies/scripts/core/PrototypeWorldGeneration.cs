using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Societies.Core
{
    public enum BiomeType
    {
        Meadow,
        Forest,
        RockyUpland,
        Wetland
    }

    public enum TerrainOverlayMode
    {
        None,
        Biome,
        Buildability,
        MovementCost
    }

    public enum CameraMode
    {
        Player,
        Observer
    }

    public sealed class WorldGenerationDefinition
    {
        public float CellSizeMeters { get; set; } = 2.0f;

        public float HeightAmplitude { get; set; } = 10.0f;

        public float RidgeStrength { get; set; } = 0.2f;

        public float WetnessBias { get; set; }

        public float ForestCoverage { get; set; } = 0.30f;

        public float RockyCoverage { get; set; } = 0.14f;

        public int MaxSettlementPlacementAttempts { get; set; } = 12;
    }

    public sealed class ResourceClusterDefinition
    {
        public int WoodClusters { get; set; } = 7;

        public int StoneClusters { get; set; } = 5;

        public int BerryClusters { get; set; } = 4;
    }

    public sealed class TerrainCell
    {
        public int GridX { get; init; }

        public int GridY { get; init; }

        public Vector3 WorldPosition { get; set; }

        public float ElevationMeters { get; set; }

        public float SlopeDegrees { get; set; }

        public float Wetness { get; set; }

        public float Fertility { get; set; }

        public float MovementCost { get; set; }

        public bool IsBuildable { get; set; }

        public BiomeType Biome { get; set; }
    }

    public sealed class SettlementSpawnState
    {
        public Vector3 AnchorPosition { get; init; }

        public int GridX { get; init; }

        public int GridY { get; init; }
    }

    public sealed class ResourceClusterState
    {
        public string ResourceId { get; init; } = string.Empty;

        public int ClusterIndex { get; init; }

        public Vector3 CenterPosition { get; init; }

        public int CellX { get; init; }

        public int CellY { get; init; }

        public bool IsStarterCluster { get; init; }

        public float DistanceFromSettlement { get; init; }
    }

    public sealed class WorldMapState
    {
        private readonly TerrainCell[] _cells;

        public WorldMapState(int gridWidth, int gridHeight, float cellSizeMeters, float worldSize, TerrainCell[] cells)
        {
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            CellSizeMeters = cellSizeMeters;
            WorldSize = worldSize;
            _cells = cells;
            OriginX = -(worldSize * 0.5f) + (cellSizeMeters * 0.5f);
            OriginZ = -(worldSize * 0.5f) + (cellSizeMeters * 0.5f);
        }

        public int GridWidth { get; }

        public int GridHeight { get; }

        public float CellSizeMeters { get; }

        public float WorldSize { get; }

        public float WorldHalfSize => WorldSize * 0.5f;

        public float OriginX { get; }

        public float OriginZ { get; }

        public IReadOnlyList<TerrainCell> Cells => _cells;

        public TerrainCell GetCell(int x, int y)
        {
            return _cells[(y * GridWidth) + x];
        }

        public TerrainCell? TryGetCell(int x, int y)
        {
            return x < 0 || y < 0 || x >= GridWidth || y >= GridHeight
                ? null
                : GetCell(x, y);
        }

        public TerrainCell GetNearestCell(Vector3 worldPosition)
        {
            int cellX = Mathf.Clamp(Mathf.RoundToInt((worldPosition.X - OriginX) / CellSizeMeters), 0, GridWidth - 1);
            int cellY = Mathf.Clamp(Mathf.RoundToInt((worldPosition.Z - OriginZ) / CellSizeMeters), 0, GridHeight - 1);
            return GetCell(cellX, cellY);
        }

        public float SampleHeight(Vector3 worldPosition)
        {
            float normalizedX = Mathf.Clamp((worldPosition.X - OriginX) / CellSizeMeters, 0.0f, GridWidth - 1.0f);
            float normalizedY = Mathf.Clamp((worldPosition.Z - OriginZ) / CellSizeMeters, 0.0f, GridHeight - 1.0f);

            int x0 = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, GridWidth - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(normalizedY), 0, GridHeight - 1);
            int x1 = Mathf.Min(x0 + 1, GridWidth - 1);
            int y1 = Mathf.Min(y0 + 1, GridHeight - 1);

            float tx = normalizedX - x0;
            float ty = normalizedY - y0;

            float h00 = GetCell(x0, y0).ElevationMeters;
            float h10 = GetCell(x1, y0).ElevationMeters;
            float h01 = GetCell(x0, y1).ElevationMeters;
            float h11 = GetCell(x1, y1).ElevationMeters;

            float hx0 = Mathf.Lerp(h00, h10, tx);
            float hx1 = Mathf.Lerp(h01, h11, tx);
            return Mathf.Lerp(hx0, hx1, ty);
        }

        public Vector3 ProjectToSurface(Vector3 worldPosition)
        {
            return new Vector3(worldPosition.X, SampleHeight(worldPosition), worldPosition.Z);
        }

        public bool HasAdjacentBiome(int x, int y, BiomeType biome)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    TerrainCell? candidate = TryGetCell(x + offsetX, y + offsetY);
                    if (candidate != null && candidate.Biome == biome)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public sealed class WorldGenerationResult
    {
        public int WorldSeed { get; init; }

        public int WorldGenerationAttempt { get; init; }

        public WorldMapState WorldMap { get; init; } = null!;

        public SettlementSpawnState SettlementSpawn { get; init; } = null!;

        public List<ResourceClusterState> ResourceClusters { get; init; } = new();

        public List<PrototypeResourceSpawn> ResourceSpawns { get; init; } = new();

        public Dictionary<string, float> StarterResourceDistances { get; init; } = new();

        public Dictionary<string, float> AverageClusterDistances { get; init; } = new();

        public string WorldHash { get; init; } = string.Empty;
    }

    public static class PrototypeSeedDerivation
    {
        public static int Derive(int baseSeed, string channel, int salt = 0)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash ^= (uint)baseSeed;
                hash *= 16777619u;

                for (int index = 0; index < channel.Length; index++)
                {
                    hash ^= channel[index];
                    hash *= 16777619u;
                }

                hash ^= (uint)salt;
                hash *= 16777619u;

                return hash == 0 ? 0x6D2B79F5 : (int)hash;
            }
        }
    }

    public static class PrototypeWorldGenerator
    {
        private const float WoodStarterMaxDistance = 40.0f;
        private const float BerryStarterMaxDistance = 50.0f;
        private const float StoneStarterMaxDistance = 65.0f;

        public static WorldGenerationResult Generate(PrototypeScenarioDefinition scenario)
        {
            if (scenario.WorldGen == null || scenario.ResourceClusters == null)
            {
                throw new InvalidOperationException($"Scenario '{scenario.Id}' is missing world-generation configuration.");
            }

            int maxAttempts = Mathf.Max(1, scenario.WorldGen.MaxSettlementPlacementAttempts);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int worldSeed = PrototypeSeedDerivation.Derive(scenario.SimulationSeed, "world.root", attempt);
                WorldMapState worldMap = GenerateWorldMap(scenario, worldSeed);
                if (!TryChooseSettlementSpawn(worldMap, out SettlementSpawnState? settlementSpawn))
                {
                    continue;
                }

                List<ResourceClusterState> resourceClusters = GenerateResourceClusters(worldMap, settlementSpawn!, scenario, worldSeed);
                if (!TryBuildStarterDistances(resourceClusters, out Dictionary<string, float>? starterDistances))
                {
                    continue;
                }

                if (starterDistances!["wood"] > WoodStarterMaxDistance ||
                    starterDistances["berry"] > BerryStarterMaxDistance ||
                    starterDistances["stone"] > StoneStarterMaxDistance)
                {
                    continue;
                }

                List<PrototypeResourceSpawn> spawns = GenerateResourceSpawns(worldMap, resourceClusters, scenario, worldSeed);
                return new WorldGenerationResult
                {
                    WorldSeed = worldSeed,
                    WorldGenerationAttempt = attempt,
                    WorldMap = worldMap,
                    SettlementSpawn = settlementSpawn!,
                    ResourceClusters = resourceClusters,
                    ResourceSpawns = spawns,
                    StarterResourceDistances = starterDistances,
                    AverageClusterDistances = resourceClusters
                        .GroupBy(cluster => cluster.ResourceId)
                        .OrderBy(group => group.Key)
                        .ToDictionary(group => group.Key, group => group.Average(cluster => cluster.DistanceFromSettlement)),
                    WorldHash = ComputeWorldHash(worldMap, settlementSpawn!, resourceClusters, worldSeed)
                };
            }

            throw new InvalidOperationException($"Failed to generate a valid world for scenario '{scenario.Id}' after {maxAttempts} attempts.");
        }

        public static WorldGenerationResult Regenerate(PrototypeScenarioDefinition scenario, int worldSeed, int worldGenerationAttempt)
        {
            if (scenario.WorldGen == null || scenario.ResourceClusters == null)
            {
                throw new InvalidOperationException($"Scenario '{scenario.Id}' is missing world-generation configuration.");
            }

            WorldMapState worldMap = GenerateWorldMap(scenario, worldSeed);
            if (!TryChooseSettlementSpawn(worldMap, out SettlementSpawnState? settlementSpawn))
            {
                throw new InvalidOperationException($"Failed to regenerate world for scenario '{scenario.Id}' using persisted world seed {worldSeed}.");
            }

            List<ResourceClusterState> resourceClusters = GenerateResourceClusters(worldMap, settlementSpawn!, scenario, worldSeed);
            TryBuildStarterDistances(resourceClusters, out Dictionary<string, float>? starterDistances);
            List<PrototypeResourceSpawn> spawns = GenerateResourceSpawns(worldMap, resourceClusters, scenario, worldSeed);

            return new WorldGenerationResult
            {
                WorldSeed = worldSeed,
                WorldGenerationAttempt = worldGenerationAttempt,
                WorldMap = worldMap,
                SettlementSpawn = settlementSpawn!,
                ResourceClusters = resourceClusters,
                ResourceSpawns = spawns,
                StarterResourceDistances = starterDistances ?? new Dictionary<string, float>(),
                AverageClusterDistances = resourceClusters
                    .GroupBy(cluster => cluster.ResourceId)
                    .OrderBy(group => group.Key)
                    .ToDictionary(group => group.Key, group => group.Average(cluster => cluster.DistanceFromSettlement)),
                WorldHash = ComputeWorldHash(worldMap, settlementSpawn!, resourceClusters, worldSeed)
            };
        }

        private static WorldMapState GenerateWorldMap(PrototypeScenarioDefinition scenario, int worldSeed)
        {
            int gridWidth = Mathf.RoundToInt(scenario.WorldSize / scenario.WorldGen.CellSizeMeters);
            int gridHeight = gridWidth;
            float cellSize = scenario.WorldGen.CellSizeMeters;
            float origin = -(scenario.WorldSize * 0.5f) + (cellSize * 0.5f);

            TerrainCell[] cells = new TerrainCell[gridWidth * gridHeight];
            int broadSeed = PrototypeSeedDerivation.Derive(worldSeed, "terrain.broad");
            int ridgeSeed = PrototypeSeedDerivation.Derive(worldSeed, "terrain.ridge");
            int wetSeed = PrototypeSeedDerivation.Derive(worldSeed, "terrain.wet");
            int fertileSeed = PrototypeSeedDerivation.Derive(worldSeed, "terrain.fertile");

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    float worldX = origin + (x * cellSize);
                    float worldZ = origin + (y * cellSize);
                    float normalizedX = worldX / (scenario.WorldSize * 0.5f);
                    float normalizedZ = worldZ / (scenario.WorldSize * 0.5f);
                    float distanceFromCenter = Mathf.Clamp(Mathf.Sqrt((normalizedX * normalizedX) + (normalizedZ * normalizedZ)), 0.0f, 1.0f);

                    float broadNoise = FractalNoise(normalizedX * 1.8f, normalizedZ * 1.8f, broadSeed, 4, 2.0f, 0.5f);
                    float ridgeNoise = 1.0f - Mathf.Abs(FractalNoise(normalizedX * 3.0f, normalizedZ * 3.0f, ridgeSeed, 3, 2.05f, 0.55f));
                    float wetNoise = FractalNoise(normalizedX * 2.4f, normalizedZ * 2.4f, wetSeed, 3, 2.0f, 0.5f);
                    float fertileNoise = FractalNoise(normalizedX * 2.1f, normalizedZ * 2.1f, fertileSeed, 3, 2.1f, 0.55f);

                    float basinTerm = Mathf.Max(0.0f, 1.0f - (distanceFromCenter * distanceFromCenter));
                    float elevation = (broadNoise * scenario.WorldGen.HeightAmplitude * 0.55f) +
                                      (((ridgeNoise * 2.0f) - 1.0f) * scenario.WorldGen.HeightAmplitude * scenario.WorldGen.RidgeStrength * 0.45f) -
                                      (basinTerm * scenario.WorldGen.HeightAmplitude * 0.32f);

                    float wetness = Mathf.Clamp(
                        0.5f +
                        (wetNoise * 0.35f) +
                        scenario.WorldGen.WetnessBias -
                        (elevation / Mathf.Max(6.0f, scenario.WorldGen.HeightAmplitude * 2.5f)),
                        0.0f,
                        1.0f);
                    float fertility = Mathf.Clamp(
                        0.55f +
                        (fertileNoise * 0.20f) +
                        (wetness * 0.24f),
                        0.0f,
                        1.0f);

                    cells[(y * gridWidth) + x] = new TerrainCell
                    {
                        GridX = x,
                        GridY = y,
                        WorldPosition = new Vector3(worldX, elevation, worldZ),
                        ElevationMeters = elevation,
                        Wetness = wetness,
                        Fertility = fertility
                    };
                }
            }

            WorldMapState worldMap = new(gridWidth, gridHeight, cellSize, scenario.WorldSize, cells);
            ApplySlopeAndBiomes(worldMap, scenario);
            return worldMap;
        }

        private static void ApplySlopeAndBiomes(WorldMapState worldMap, PrototypeScenarioDefinition scenario)
        {
            float minElevation = worldMap.Cells.Min(cell => cell.ElevationMeters);
            float maxElevation = worldMap.Cells.Max(cell => cell.ElevationMeters);
            float elevationRange = Mathf.Max(0.001f, maxElevation - minElevation);
            float roughnessMultiplier = 1.0f +
                                        (scenario.WorldGen.RidgeStrength * 4.5f) +
                                        (scenario.WorldGen.HeightAmplitude / 12.0f);
            roughnessMultiplier *= 1.0f + scenario.WorldGen.RidgeStrength;

            foreach (TerrainCell cell in worldMap.Cells)
            {
                TerrainCell left = worldMap.TryGetCell(cell.GridX - 1, cell.GridY) ?? cell;
                TerrainCell right = worldMap.TryGetCell(cell.GridX + 1, cell.GridY) ?? cell;
                TerrainCell up = worldMap.TryGetCell(cell.GridX, cell.GridY - 1) ?? cell;
                TerrainCell down = worldMap.TryGetCell(cell.GridX, cell.GridY + 1) ?? cell;

                float gradientX = (right.ElevationMeters - left.ElevationMeters) / (2.0f * worldMap.CellSizeMeters);
                float gradientY = (down.ElevationMeters - up.ElevationMeters) / (2.0f * worldMap.CellSizeMeters);
                cell.SlopeDegrees = Mathf.RadToDeg(
                    Mathf.Atan(Mathf.Sqrt((gradientX * gradientX) + (gradientY * gradientY)) * roughnessMultiplier));
            }

            int wetlandTargetCount = Mathf.RoundToInt(worldMap.Cells.Count * ComputeWetlandTargetRatio(scenario.WorldGen));
            int rockyTargetCount = Mathf.RoundToInt(worldMap.Cells.Count * scenario.WorldGen.RockyCoverage);
            int forestTargetCount = Mathf.RoundToInt(worldMap.Cells.Count * scenario.WorldGen.ForestCoverage);

            HashSet<TerrainCell> wetlandCells = worldMap.Cells
                .OrderByDescending(cell => (cell.Wetness * 0.7f) + (((maxElevation - cell.ElevationMeters) / elevationRange) * 0.3f))
                .Take(wetlandTargetCount)
                .ToHashSet();

            HashSet<TerrainCell> rockyCells = worldMap.Cells
                .Except(wetlandCells)
                .OrderByDescending(cell =>
                {
                    float elevationNorm = (cell.ElevationMeters - minElevation) / elevationRange;
                    float slopeNorm = Mathf.Clamp(cell.SlopeDegrees / 24.0f, 0.0f, 1.0f);
                    return (slopeNorm * 0.55f) + (elevationNorm * 0.35f) + ((1.0f - cell.Fertility) * 0.10f);
                })
                .Take(rockyTargetCount)
                .ToHashSet();

            HashSet<TerrainCell> forestCells = worldMap.Cells
                .Except(wetlandCells)
                .Except(rockyCells)
                .OrderByDescending(cell =>
                {
                    float slopeNorm = Mathf.Clamp(cell.SlopeDegrees / 18.0f, 0.0f, 1.0f);
                    float wetnessBalance = 1.0f - Mathf.Abs(cell.Wetness - 0.55f);
                    return (cell.Fertility * 0.55f) + (wetnessBalance * 0.25f) + ((1.0f - slopeNorm) * 0.20f);
                })
                .Take(forestTargetCount)
                .ToHashSet();

            foreach (TerrainCell cell in worldMap.Cells)
            {
                if (wetlandCells.Contains(cell))
                {
                    cell.Biome = BiomeType.Wetland;
                    cell.MovementCost = 2.2f;
                }
                else if (rockyCells.Contains(cell))
                {
                    cell.Biome = BiomeType.RockyUpland;
                    cell.MovementCost = 1.5f;
                }
                else if (forestCells.Contains(cell))
                {
                    cell.Biome = BiomeType.Forest;
                    cell.MovementCost = 1.25f;
                }
                else
                {
                    cell.Biome = BiomeType.Meadow;
                    cell.MovementCost = 1.0f;
                }

                cell.IsBuildable = cell.Biome != BiomeType.Wetland && cell.SlopeDegrees <= 12.0f;
            }
        }

        private static bool TryChooseSettlementSpawn(WorldMapState worldMap, out SettlementSpawnState? settlementSpawn)
        {
            settlementSpawn = null;

            List<TerrainCell> candidates = worldMap.Cells
                .Where(cell => cell.Biome == BiomeType.Meadow && cell.IsBuildable)
                .OrderBy(cell =>
                {
                    float centerDistance = new Vector2(cell.WorldPosition.X, cell.WorldPosition.Z).Length();
                    return centerDistance + (cell.SlopeDegrees * 2.0f) + MathF.Abs(cell.ElevationMeters);
                })
                .ToList();

            foreach (TerrainCell candidate in candidates)
            {
                if (!HasEligibleResourceCell(worldMap, candidate, BiomeType.Forest, WoodStarterMaxDistance))
                {
                    continue;
                }

                if (!HasEligibleBerryCell(worldMap, candidate, BerryStarterMaxDistance))
                {
                    continue;
                }

                if (!HasEligibleResourceCell(worldMap, candidate, BiomeType.RockyUpland, StoneStarterMaxDistance))
                {
                    continue;
                }

                settlementSpawn = new SettlementSpawnState
                {
                    AnchorPosition = candidate.WorldPosition,
                    GridX = candidate.GridX,
                    GridY = candidate.GridY
                };
                return true;
            }

            return false;
        }

        private static List<ResourceClusterState> GenerateResourceClusters(
            WorldMapState worldMap,
            SettlementSpawnState settlementSpawn,
            PrototypeScenarioDefinition scenario,
            int worldSeed)
        {
            List<ResourceClusterState> result = new();
            result.AddRange(CreateResourceClustersForType("wood", worldMap, settlementSpawn, scenario, scenario.ResourceClusters.WoodClusters, BiomeType.Forest, worldSeed));
            result.AddRange(CreateResourceClustersForType("stone", worldMap, settlementSpawn, scenario, scenario.ResourceClusters.StoneClusters, BiomeType.RockyUpland, worldSeed));
            result.AddRange(CreateBerryClusters(worldMap, settlementSpawn, scenario, scenario.ResourceClusters.BerryClusters, worldSeed));
            return result;
        }

        private static List<ResourceClusterState> CreateResourceClustersForType(
            string resourceId,
            WorldMapState worldMap,
            SettlementSpawnState settlementSpawn,
            PrototypeScenarioDefinition scenario,
            int clusterCount,
            BiomeType biome,
            int worldSeed)
        {
            List<TerrainCell> eligibleCells = worldMap.Cells.Where(cell => cell.Biome == biome).ToList();
            if (eligibleCells.Count == 0)
            {
                throw new InvalidOperationException($"World generation produced no eligible {biome} cells for resource '{resourceId}'.");
            }

            TerrainCell starterCell = SelectNearestCandidate(
                eligibleCells,
                settlementSpawn.AnchorPosition,
                GetStarterMaxDistance(resourceId),
                GetIdealStarterDistance(resourceId),
                0.0f);

            List<ResourceClusterState> clusters = new()
            {
                BuildClusterState(resourceId, 0, starterCell, settlementSpawn.AnchorPosition, true)
            };

            float distancePreference = GetClusterDistancePreference(resourceId, scenario);
            float minimumSpacing = GetClusterSpacing(resourceId, worldMap.CellSizeMeters);
            DeterministicRandom rng = new(PrototypeSeedDerivation.Derive(worldSeed, $"clusters.{resourceId}"));

            for (int clusterIndex = 1; clusterIndex < clusterCount; clusterIndex++)
            {
                TerrainCell nextCell = SelectAdditionalClusterCell(
                    eligibleCells,
                    clusters,
                    settlementSpawn.AnchorPosition,
                    distancePreference,
                    minimumSpacing,
                    rng);
                clusters.Add(BuildClusterState(resourceId, clusterIndex, nextCell, settlementSpawn.AnchorPosition, false));
            }

            return clusters;
        }

        private static List<ResourceClusterState> CreateBerryClusters(
            WorldMapState worldMap,
            SettlementSpawnState settlementSpawn,
            PrototypeScenarioDefinition scenario,
            int clusterCount,
            int worldSeed)
        {
            List<TerrainCell> eligibleCells = worldMap.Cells
                .Where(cell => cell.Biome == BiomeType.Meadow && worldMap.HasAdjacentBiome(cell.GridX, cell.GridY, BiomeType.Forest))
                .ToList();

            if (eligibleCells.Count == 0)
            {
                throw new InvalidOperationException("World generation produced no eligible berry cells.");
            }

            TerrainCell starterCell = SelectNearestCandidate(
                eligibleCells,
                settlementSpawn.AnchorPosition,
                BerryStarterMaxDistance,
                GetIdealStarterDistance("berry"),
                0.0f);

            List<ResourceClusterState> clusters = new()
            {
                BuildClusterState("berry", 0, starterCell, settlementSpawn.AnchorPosition, true)
            };

            float distancePreference = GetClusterDistancePreference("berry", scenario);
            float minimumSpacing = GetClusterSpacing("berry", worldMap.CellSizeMeters);
            DeterministicRandom rng = new(PrototypeSeedDerivation.Derive(worldSeed, "clusters.berry"));

            for (int clusterIndex = 1; clusterIndex < clusterCount; clusterIndex++)
            {
                TerrainCell nextCell = SelectAdditionalClusterCell(
                    eligibleCells,
                    clusters,
                    settlementSpawn.AnchorPosition,
                    distancePreference,
                    minimumSpacing,
                    rng);
                clusters.Add(BuildClusterState("berry", clusterIndex, nextCell, settlementSpawn.AnchorPosition, false));
            }

            return clusters;
        }

        private static List<PrototypeResourceSpawn> GenerateResourceSpawns(
            WorldMapState worldMap,
            IReadOnlyList<ResourceClusterState> clusters,
            PrototypeScenarioDefinition scenario,
            int worldSeed)
        {
            HashSet<long> occupiedCells = new();
            List<PrototypeResourceSpawn> spawns = new();

            AppendResourceSpawns("wood", scenario.InitialTrees, clusters.Where(cluster => cluster.ResourceId == "wood").ToList(), worldMap, worldSeed, occupiedCells, spawns);
            AppendResourceSpawns("stone", scenario.InitialRocks, clusters.Where(cluster => cluster.ResourceId == "stone").ToList(), worldMap, worldSeed, occupiedCells, spawns);
            AppendResourceSpawns("berry", scenario.InitialBerryBushes, clusters.Where(cluster => cluster.ResourceId == "berry").ToList(), worldMap, worldSeed, occupiedCells, spawns);

            return spawns
                .OrderBy(spawn => spawn.ResourceId, StringComparer.Ordinal)
                .ThenBy(spawn => spawn.Position.X)
                .ThenBy(spawn => spawn.Position.Z)
                .ToList();
        }

        private static void AppendResourceSpawns(
            string resourceId,
            int nodeCount,
            IReadOnlyList<ResourceClusterState> clusters,
            WorldMapState worldMap,
            int worldSeed,
            HashSet<long> occupiedCells,
            List<PrototypeResourceSpawn> output)
        {
            int[] perClusterCounts = PartitionNodeCounts(nodeCount, clusters.Count, PrototypeSeedDerivation.Derive(worldSeed, $"partition.{resourceId}"));
            DeterministicRandom rng = new(PrototypeSeedDerivation.Derive(worldSeed, $"spawns.{resourceId}"));

            for (int clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
            {
                ResourceClusterState cluster = clusters[clusterIndex];
                List<TerrainCell> nearbyCells = GatherNearbySpawnCells(worldMap, resourceId, cluster);
                int placed = 0;

                foreach (TerrainCell cell in nearbyCells)
                {
                    if (placed >= perClusterCounts[clusterIndex])
                    {
                        break;
                    }

                    long cellKey = GetCellKey(cell.GridX, cell.GridY);
                    if (!occupiedCells.Add(cellKey))
                    {
                        continue;
                    }

                    output.Add(new PrototypeResourceSpawn(
                        resourceId,
                        cell.WorldPosition,
                        resourceId == "berry"
                            ? rng.NextIntInclusive(3, 5)
                            : rng.NextIntInclusive(4, 7)));
                    placed++;
                }

                foreach (TerrainCell cell in worldMap.Cells.Where(cell => IsEligibleSpawnCell(worldMap, resourceId, cell)))
                {
                    if (placed >= perClusterCounts[clusterIndex])
                    {
                        break;
                    }

                    long cellKey = GetCellKey(cell.GridX, cell.GridY);
                    if (!occupiedCells.Add(cellKey))
                    {
                        continue;
                    }

                    output.Add(new PrototypeResourceSpawn(
                        resourceId,
                        cell.WorldPosition,
                        resourceId == "berry"
                            ? rng.NextIntInclusive(3, 5)
                            : rng.NextIntInclusive(4, 7)));
                    placed++;
                }
            }
        }

        private static List<TerrainCell> GatherNearbySpawnCells(WorldMapState worldMap, string resourceId, ResourceClusterState cluster)
        {
            float clusterRadius = resourceId switch
            {
                "wood" => 14.0f,
                "stone" => 18.0f,
                _ => 10.0f
            };

            return worldMap.Cells
                .Where(cell => IsEligibleSpawnCell(worldMap, resourceId, cell))
                .OrderBy(cell => cell.WorldPosition.DistanceTo(cluster.CenterPosition))
                .Where(cell => cell.WorldPosition.DistanceTo(cluster.CenterPosition) <= clusterRadius)
                .ToList();
        }

        private static bool IsEligibleSpawnCell(WorldMapState worldMap, string resourceId, TerrainCell cell)
        {
            return resourceId switch
            {
                "wood" => cell.Biome == BiomeType.Forest,
                "stone" => cell.Biome == BiomeType.RockyUpland,
                "berry" => cell.Biome == BiomeType.Meadow && worldMap.HasAdjacentBiome(cell.GridX, cell.GridY, BiomeType.Forest),
                _ => false
            };
        }

        private static int[] PartitionNodeCounts(int totalNodes, int clusterCount, int partitionSeed)
        {
            int safeClusterCount = Math.Max(clusterCount, 1);
            int[] counts = Enumerable.Repeat(totalNodes / safeClusterCount, safeClusterCount).ToArray();
            int remainder = totalNodes % safeClusterCount;

            DeterministicRandom rng = new(partitionSeed);
            List<int> indices = Enumerable.Range(0, safeClusterCount)
                .OrderBy(_ => rng.NextIntInclusive(0, 10_000))
                .ToList();

            for (int index = 0; index < remainder; index++)
            {
                counts[indices[index]]++;
            }

            return counts;
        }

        private static TerrainCell SelectNearestCandidate(
            IEnumerable<TerrainCell> candidates,
            Vector3 anchorPosition,
            float maxDistance,
            float idealDistance,
            float spacingPenalty)
        {
            return candidates
                .Select(candidate => new
                {
                    Cell = candidate,
                    Distance = candidate.WorldPosition.DistanceTo(anchorPosition)
                })
                .Where(candidate => candidate.Distance <= maxDistance)
                .OrderBy(candidate => MathF.Abs(candidate.Distance - idealDistance) + spacingPenalty)
                .ThenBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Cell)
                .First();
        }

        private static TerrainCell SelectAdditionalClusterCell(
            IReadOnlyList<TerrainCell> candidates,
            IReadOnlyList<ResourceClusterState> existingClusters,
            Vector3 anchorPosition,
            float distancePreference,
            float minimumSpacing,
            DeterministicRandom rng)
        {
            TerrainCell? bestCell = null;
            float bestScore = float.MinValue;
            float furthestReach = candidates.Max(cell => cell.WorldPosition.DistanceTo(anchorPosition));
            float safeReach = MathF.Max(1.0f, furthestReach);

            foreach (TerrainCell candidate in candidates)
            {
                float distanceFromAnchor = candidate.WorldPosition.DistanceTo(anchorPosition);
                float distanceNorm = distanceFromAnchor / safeReach;
                float spacing = existingClusters.Min(cluster => cluster.CenterPosition.DistanceTo(candidate.WorldPosition));
                if (spacing < minimumSpacing)
                {
                    continue;
                }

                float spacingScore = MathF.Min(1.0f, spacing / (minimumSpacing * 1.8f));
                float preferenceScore = 1.0f - MathF.Abs(distanceNorm - distancePreference);
                float score = (spacingScore * 0.55f) + (preferenceScore * 0.44f) + rng.NextFloat(0.0f, 0.01f);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCell = candidate;
                }
            }

            return bestCell ?? candidates[0];
        }

        private static ResourceClusterState BuildClusterState(
            string resourceId,
            int clusterIndex,
            TerrainCell cell,
            Vector3 settlementAnchor,
            bool isStarterCluster)
        {
            return new ResourceClusterState
            {
                ResourceId = resourceId,
                ClusterIndex = clusterIndex,
                CenterPosition = cell.WorldPosition,
                CellX = cell.GridX,
                CellY = cell.GridY,
                IsStarterCluster = isStarterCluster,
                DistanceFromSettlement = cell.WorldPosition.DistanceTo(settlementAnchor)
            };
        }

        private static float GetStarterMaxDistance(string resourceId)
        {
            return resourceId switch
            {
                "wood" => WoodStarterMaxDistance,
                "stone" => StoneStarterMaxDistance,
                _ => BerryStarterMaxDistance
            };
        }

        private static float GetIdealStarterDistance(string resourceId)
        {
            return resourceId switch
            {
                "wood" => 12.0f,
                "stone" => 12.0f,
                _ => 12.0f
            };
        }

        private static float GetClusterSpacing(string resourceId, float cellSizeMeters)
        {
            return resourceId switch
            {
                "wood" => 18.0f + (cellSizeMeters * 2.0f),
                "stone" => 22.0f + (cellSizeMeters * 2.0f),
                _ => 14.0f + (cellSizeMeters * 2.0f)
            };
        }

        private static float GetClusterDistancePreference(string resourceId, PrototypeScenarioDefinition scenario)
        {
            return resourceId switch
            {
                "stone" => Mathf.Clamp(
                    0.34f +
                    ((scenario.WorldGen.RockyCoverage - 0.14f) * 2.2f) +
                    (scenario.WorldGen.RidgeStrength * 0.25f) +
                    ((0.30f - scenario.WorldGen.ForestCoverage) * 0.40f),
                    0.30f,
                    0.92f),
                "wood" => Mathf.Clamp(
                    0.28f +
                    ((0.30f - scenario.WorldGen.ForestCoverage) * 0.5f),
                    0.18f,
                    0.55f),
                _ => Mathf.Clamp(
                    0.24f +
                    MathF.Max(0.0f, scenario.WorldGen.WetnessBias) * 0.10f,
                    0.18f,
                    0.42f)
            };
        }

        private static float ComputeWetlandTargetRatio(WorldGenerationDefinition worldGen)
        {
            return Mathf.Clamp(
                0.08f +
                (worldGen.WetnessBias * 0.35f) +
                ((10.0f - worldGen.HeightAmplitude) / 25.0f),
                0.02f,
                0.36f);
        }

        private static bool HasEligibleResourceCell(WorldMapState worldMap, TerrainCell anchorCell, BiomeType biome, float maxDistance)
        {
            return worldMap.Cells.Any(cell =>
                cell.Biome == biome &&
                cell.WorldPosition.DistanceTo(anchorCell.WorldPosition) <= maxDistance);
        }

        private static bool HasEligibleBerryCell(WorldMapState worldMap, TerrainCell anchorCell, float maxDistance)
        {
            return worldMap.Cells.Any(cell =>
                cell.Biome == BiomeType.Meadow &&
                worldMap.HasAdjacentBiome(cell.GridX, cell.GridY, BiomeType.Forest) &&
                cell.WorldPosition.DistanceTo(anchorCell.WorldPosition) <= maxDistance);
        }

        private static bool TryBuildStarterDistances(
            IReadOnlyList<ResourceClusterState> resourceClusters,
            out Dictionary<string, float>? starterDistances)
        {
            starterDistances = resourceClusters
                .Where(cluster => cluster.IsStarterCluster)
                .OrderBy(cluster => cluster.ResourceId)
                .ToDictionary(cluster => cluster.ResourceId, cluster => cluster.DistanceFromSettlement);

            return starterDistances.ContainsKey("wood") &&
                   starterDistances.ContainsKey("stone") &&
                   starterDistances.ContainsKey("berry");
        }

        private static float FractalNoise(float x, float y, int seed, int octaves, float lacunarity, float gain)
        {
            float amplitude = 1.0f;
            float frequency = 1.0f;
            float sum = 0.0f;
            float maxAmplitude = 0.0f;

            for (int octave = 0; octave < octaves; octave++)
            {
                sum += ValueNoise(x * frequency, y * frequency, seed + (octave * 31)) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return maxAmplitude <= 0.0f ? 0.0f : sum / maxAmplitude;
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float tx = SmoothStep(x - x0);
            float ty = SmoothStep(y - y0);

            float n00 = HashToSigned(seed, x0, y0);
            float n10 = HashToSigned(seed, x1, y0);
            float n01 = HashToSigned(seed, x0, y1);
            float n11 = HashToSigned(seed, x1, y1);

            float nx0 = Mathf.Lerp(n00, n10, tx);
            float nx1 = Mathf.Lerp(n01, n11, tx);
            return Mathf.Lerp(nx0, nx1, ty);
        }

        private static float HashToSigned(int seed, int x, int y)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash ^= (uint)seed;
                hash *= 16777619u;
                hash ^= (uint)x;
                hash *= 16777619u;
                hash ^= (uint)y;
                hash *= 16777619u;
                return ((hash & 0x00FFFFFFu) / 8388607.5f) - 1.0f;
            }
        }

        private static float SmoothStep(float value)
        {
            return value * value * (3.0f - (2.0f * value));
        }

        private static string ComputeWorldHash(
            WorldMapState worldMap,
            SettlementSpawnState settlementSpawn,
            IReadOnlyList<ResourceClusterState> resourceClusters,
            int worldSeed)
        {
            StringBuilder builder = new();
            builder.Append(worldSeed.ToString(CultureInfo.InvariantCulture));
            builder.Append('|');
            builder.Append(settlementSpawn.GridX.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(settlementSpawn.GridY.ToString(CultureInfo.InvariantCulture));
            builder.Append('|');

            foreach (TerrainCell cell in worldMap.Cells)
            {
                builder.Append((int)cell.Biome);
                builder.Append(':');
                builder.Append(MathF.Round(cell.ElevationMeters, 2).ToString(CultureInfo.InvariantCulture));
                builder.Append(';');
            }

            builder.Append('|');
            foreach (ResourceClusterState cluster in resourceClusters.OrderBy(cluster => cluster.ResourceId).ThenBy(cluster => cluster.ClusterIndex))
            {
                builder.Append(cluster.ResourceId);
                builder.Append('@');
                builder.Append(cluster.CellX.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(cluster.CellY.ToString(CultureInfo.InvariantCulture));
                builder.Append(';');
            }

            unchecked
            {
                ulong hash = 1469598103934665603UL;
                string text = builder.ToString();
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 1099511628211UL;
                }

                return hash.ToString("X16", CultureInfo.InvariantCulture);
            }
        }

        private static long GetCellKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }
    }
}
