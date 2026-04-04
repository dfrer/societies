using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Societies.Core
{
    /// <summary>
    /// Data-driven catalogs that define the current prototype runtime shell and V2-facing content.
    /// </summary>
    public sealed class PrototypeCatalogBundle
    {
        public PrototypeScenarioCatalog Scenarios { get; init; } = new();

        public PrototypeResourceCatalog Resources { get; init; } = new();

        public PrototypeStructureCatalog Structures { get; init; } = new();

        public PrototypeRoleQuotaCatalog RoleQuotas { get; init; } = new();
    }

    public static class PrototypeCatalogLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static PrototypeCatalogBundle LoadFromDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("Catalog directory path is required.", nameof(directoryPath));
            }

            PrototypeCatalogBundle bundle = new()
            {
                Scenarios = LoadFile<PrototypeScenarioCatalog>(directoryPath, "prototype-scenarios.json"),
                Resources = LoadFile<PrototypeResourceCatalog>(directoryPath, "prototype-resources.json"),
                Structures = LoadFile<PrototypeStructureCatalog>(directoryPath, "prototype-structures.json"),
                RoleQuotas = LoadFile<PrototypeRoleQuotaCatalog>(directoryPath, "prototype-role-quotas.json")
            };

            bundle.Scenarios.Validate();
            bundle.Resources.Validate();
            bundle.Structures.Validate();
            bundle.RoleQuotas.Validate();

            return bundle;
        }

        private static T LoadFile<T>(string directoryPath, string fileName) where T : new()
        {
            string fullPath = Path.Combine(directoryPath, fileName);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Missing prototype catalog file '{fileName}'.", fullPath);
            }

            T? value = JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath), JsonOptions);
            return value ?? new T();
        }
    }

    public sealed class PrototypeScenarioCatalog
    {
        public string DefaultScenarioId { get; set; } = string.Empty;

        public List<PrototypeScenarioDefinition> Scenarios { get; set; } = new();

        public PrototypeScenarioDefinition Resolve(string scenarioId)
        {
            PrototypeScenarioDefinition? scenario = Scenarios.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, scenarioId, StringComparison.OrdinalIgnoreCase));

            if (scenario == null)
            {
                throw new InvalidOperationException($"Unknown prototype scenario '{scenarioId}'.");
            }

            return scenario;
        }

        public PrototypeScenarioDefinition ResolveDefault()
        {
            if (string.IsNullOrWhiteSpace(DefaultScenarioId))
            {
                throw new InvalidOperationException("Prototype scenario catalog is missing a default scenario id.");
            }

            return Resolve(DefaultScenarioId);
        }

        public void Validate()
        {
            PrototypeCatalogValidation.ValidateUniqueIds(
                Scenarios.Select(candidate => candidate.Id),
                "scenario");

            if (Scenarios.Count == 0)
            {
                throw new InvalidOperationException("Prototype scenario catalog must contain at least one scenario.");
            }

            if (string.IsNullOrWhiteSpace(DefaultScenarioId))
            {
                throw new InvalidOperationException("Prototype scenario catalog must define a default scenario id.");
            }

            _ = Resolve(DefaultScenarioId);

            foreach (PrototypeScenarioDefinition scenario in Scenarios)
            {
                if (string.IsNullOrWhiteSpace(scenario.DisplayName))
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' is missing a display name.");
                }

                if (scenario.InitialTrees < 0 || scenario.InitialRocks < 0 || scenario.InitialBerryBushes < 0 || scenario.InitialWorkers <= 0)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' contains invalid starting counts.");
                }

                if (scenario.InitialClayDeposits < 0 || scenario.InitialReedBeds < 0)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' contains invalid clay or reed counts.");
                }

                if (scenario.WorldSize <= 0.0f)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' must define a positive world size.");
                }

                if (scenario.WorldGen == null)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' is missing world-generation settings.");
                }

                if (scenario.ResourceClusters == null)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' is missing resource-cluster settings.");
                }

                if (scenario.PathBuildPolicy == null)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' is missing path-build policy settings.");
                }

                if (scenario.RemoteDepotPolicy == null)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' is missing remote-depot policy settings.");
                }

                if (scenario.WorldGen.CellSizeMeters <= 0.0f)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' must define a positive cell size.");
                }

                if (scenario.WorldGen.ForestCoverage <= 0.0f || scenario.WorldGen.ForestCoverage >= 0.95f)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' forest coverage must be between 0 and 0.95.");
                }

                if (scenario.WorldGen.RockyCoverage <= 0.0f || scenario.WorldGen.RockyCoverage >= 0.95f)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' rocky coverage must be between 0 and 0.95.");
                }

                if (scenario.WorldGen.MaxSettlementPlacementAttempts <= 0)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' must allow at least one settlement placement attempt.");
                }

                if (scenario.ResourceClusters.WoodClusters <= 0 ||
                    scenario.ResourceClusters.StoneClusters <= 0 ||
                    scenario.ResourceClusters.BerryClusters <= 0 ||
                    scenario.ResourceClusters.ClayClusters <= 0 ||
                    scenario.ResourceClusters.ReedClusters <= 0)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' resource cluster counts must be positive.");
                }

                if (scenario.StartingStructures.Count == 0)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' must define at least one starting structure.");
                }

                if (scenario.StartingBuildQueue.Count == 0)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' must define an initial build queue.");
                }

                if (scenario.StartingStock.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0))
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' contains invalid starting stock values.");
                }

                if (scenario.PathBuildPolicy.CorridorBudget <= 0)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' must define a positive corridor budget.");
                }

                if (scenario.RemoteDepotPolicy.ActivationDistanceMeters <= 0.0f ||
                    scenario.RemoteDepotPolicy.PlacementRadiusMeters <= 0.0f)
                {
                    throw new InvalidOperationException($"Scenario '{scenario.Id}' must define positive remote depot policy distances.");
                }
            }
        }
    }

    public sealed class PrototypeScenarioDefinition
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string ExpectedOutcome { get; set; } = "stable";

        public int SimulationSeed { get; set; } = 1337;

        public int InitialTrees { get; set; } = 36;

        public int InitialRocks { get; set; } = 24;

        public int InitialBerryBushes { get; set; } = 14;

        public int InitialWorkers { get; set; } = 3;

        public int InitialCitizens
        {
            get => InitialWorkers;
            set => InitialWorkers = value;
        }

        public int InitialClayDeposits { get; set; } = 10;

        public int InitialReedBeds { get; set; } = 10;

        public float WorldSize { get; set; } = 500.0f;

        public WorldGenerationDefinition WorldGen { get; set; } = new();

        public ResourceClusterDefinition ResourceClusters { get; set; } = new();

        public PathBuildPolicyDefinition PathBuildPolicy { get; set; } = new();

        public RemoteDepotPolicyDefinition RemoteDepotPolicy { get; set; } = new();

        public Dictionary<string, int> StartingStock { get; set; } = new();

        public List<string> StartingStructures { get; set; } = new();

        public List<string> StartingBuildQueue { get; set; } = new();

        public int StressPopulationOverride { get; set; }
    }

    public sealed class PathBuildPolicyDefinition
    {
        public int CorridorBudget { get; set; } = 3;

        public bool PauseDuringCriticalShortage { get; set; } = true;
    }

    public sealed class RemoteDepotPolicyDefinition
    {
        public float ActivationDistanceMeters { get; set; } = 55.0f;

        public float PlacementRadiusMeters { get; set; } = 12.0f;
    }

    public sealed class PrototypeResourceCatalog
    {
        public List<PrototypeResourceDefinition> Resources { get; set; } = new();

        public void Validate()
        {
            PrototypeCatalogValidation.ValidateUniqueIds(Resources.Select(candidate => candidate.Id), "resource");

            if (Resources.Count == 0)
            {
                throw new InvalidOperationException("Prototype resource catalog must contain at least one resource.");
            }

            foreach (PrototypeResourceDefinition resource in Resources)
            {
                if (string.IsNullOrWhiteSpace(resource.DisplayName))
                {
                    throw new InvalidOperationException($"Resource '{resource.Id}' is missing a display name.");
                }
            }
        }
    }

    public sealed class PrototypeResourceDefinition
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;
    }

    public sealed class PrototypeStructureCatalog
    {
        public List<PrototypeStructureDefinition> Structures { get; set; } = new();

        public void Validate()
        {
            PrototypeCatalogValidation.ValidateUniqueIds(Structures.Select(candidate => candidate.Id), "structure");

            if (Structures.Count == 0)
            {
                throw new InvalidOperationException("Prototype structure catalog must contain at least one structure.");
            }

            foreach (PrototypeStructureDefinition structure in Structures)
            {
                if (string.IsNullOrWhiteSpace(structure.DisplayName))
                {
                    throw new InvalidOperationException($"Structure '{structure.Id}' is missing a display name.");
                }
            }
        }
    }

    public sealed class PrototypeStructureDefinition
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;
    }

    public sealed class PrototypeRoleQuotaCatalog
    {
        public List<PrototypeRoleQuotaDefinition> Roles { get; set; } = new();

        public void Validate()
        {
            PrototypeCatalogValidation.ValidateUniqueIds(Roles.Select(candidate => candidate.RoleId), "role quota");

            if (Roles.Count == 0)
            {
                throw new InvalidOperationException("Prototype role quota catalog must contain at least one role.");
            }

            double totalShare = Roles.Sum(role => role.Share);
            if (Math.Abs(totalShare - 1.0d) > 0.001d)
            {
                throw new InvalidOperationException($"Prototype role quota catalog must sum to 1.0 but was {totalShare:0.###}.");
            }

            foreach (PrototypeRoleQuotaDefinition role in Roles)
            {
                if (role.Share <= 0.0d)
                {
                    throw new InvalidOperationException($"Role quota '{role.RoleId}' must be positive.");
                }
            }
        }
    }

    public sealed class PrototypeRoleQuotaDefinition
    {
        public string RoleId { get; set; } = string.Empty;

        public double Share { get; set; }
    }

    internal static class PrototypeCatalogValidation
    {
        public static void ValidateUniqueIds(IEnumerable<string> ids, string itemType)
        {
            string[] rawIds = ids.ToArray();
            if (rawIds.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException($"Prototype {itemType} catalog contains a blank id.");
            }

            string[] normalizedIds = rawIds
                .Select(id => id.Trim())
                .ToArray();

            if (normalizedIds.Length == 0)
            {
                throw new InvalidOperationException($"Prototype {itemType} catalog cannot be empty.");
            }

            string[] duplicates = normalizedIds
                .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            if (duplicates.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Prototype {itemType} catalog contains duplicate ids: {string.Join(", ", duplicates)}");
            }
        }
    }
}
