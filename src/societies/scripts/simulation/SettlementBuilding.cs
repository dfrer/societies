using Godot;
using Societies.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Societies.Simulation
{
    public sealed partial class PrototypeSettlementSimulation
    {

        private void InitializeStructures()
        {
            int hutIndex = 0;

            foreach (string structureKindId in _scenario.StartingStructures)
            {
                PrototypeStructureState structure = CreateStructure(structureKindId, structureKindId == "hut" ? hutIndex++ : 0, isBuilt: true);
                _structures.Add(structure);
            }

            foreach ((string structureKindId, int queueIndex) in _scenario.StartingBuildQueue.Select((value, index) => (value, index)))
            {
                int structureIndex = structureKindId == "hut"
                    ? hutIndex++
                    : _structures.Count(structure => structure.StructureKindId == structureKindId);

                PrototypeStructureState structure = CreateStructure(structureKindId, structureIndex, isBuilt: false);
                _structures.Add(structure);
                _buildQueue.Add(new PrototypeBuildQueueEntry
                {
                    EntryId = $"build_{queueIndex + 1}",
                    StructureKindId = structureKindId,
                    DisplayName = structure.DisplayName,
                    Priority = queueIndex,
                    StructureId = structure.StructureId
                });
            }
        }
        private void UpdateStructureStates()
        {
            foreach (PrototypeStructureState structure in _structures)
            {
                structure.IsBlocked = false;
                structure.BlockedReason = string.Empty;

                if (structure.StructureKindId == "remote_stockpile" && structure.IsBuilt)
                {
                    structure.OutputStore.Capacity = 64;
                }
            }

            if (GetStructure("storehouse_1")?.IsBuilt == true)
            {
                _centralDepot.Capacity = 220;
            }
        }
        private bool CompleteBuild(PrototypeWorkerState citizen, PrototypeSettlementTickResult result)
        {
            PrototypeStructureState? structure = GetStructure(citizen.TargetStructureId);
            if (structure == null)
            {
                FailCitizenOrder(citizen, "build.structure.missing", result, $"{citizen.DisplayName} could not find the build site");
                return false;
            }

            IReadOnlyDictionary<string, int> cost = GetConstructionCost(structure.StructureKindId);
            foreach ((string itemId, int amount) in cost)
            {
                if (!structure.InputStore.Remove(itemId, amount))
                {
                    FailBlockedStructure(structure, citizen, result, "build.inputs.missing", $"{structure.DisplayName} lacks construction materials");
                    return false;
                }

                IncrementCount(_consumedResources, itemId, amount);
            }

            structure.IsBuilt = true;
            structure.Progress = 1.0f;
            structure.IsBlocked = false;
            structure.BlockedReason = string.Empty;
            _structuresCompletedThisTick++;

            if (structure.StructureKindId == "hut")
            {
                structure.BedCapacity = 2;
                AssignBeds();
            }

            if (structure.StructureKindId == "path_segment")
            {
                PrototypePathSegmentState? segment = _pathSegments.FirstOrDefault(candidate => string.Equals(candidate.StructureId, structure.StructureId, StringComparison.Ordinal));
                if (segment != null)
                {
                    segment.IsBuilt = true;
                }

                _pathSegmentCompletedThisTick = true;
                InvalidateNavigation();
            }

            if (structure.StructureKindId == "remote_stockpile")
            {
                PrototypeRemoteDepotState? depot = _remoteDepots.FirstOrDefault(candidate => string.Equals(candidate.StructureId, structure.StructureId, StringComparison.Ordinal));
                if (depot != null)
                {
                    depot.IsBuilt = true;
                }
            }

            PrototypeBuildQueueEntry? entry = _buildQueue.FirstOrDefault(candidate => candidate.StructureId == structure.StructureId);
            if (entry != null)
            {
                entry.IsCompleted = true;
            }

            _structureCompletionTicks[structure.StructureId] = _totalTicks;
            result.Events.Add(new PrototypeSettlementEvent(
                PrototypeEventTypes.SettlementBuildCompleted,
                $"{citizen.DisplayName} completed {structure.DisplayName}"));
            if (structure.StructureKindId == "path_segment")
            {
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.SettlementPathBuilt,
                    $"{citizen.DisplayName} completed a path segment for {structure.CorridorId}"));
            }

            if (structure.StructureKindId == "remote_stockpile")
            {
                result.Events.Add(new PrototypeSettlementEvent(
                    PrototypeEventTypes.SettlementRemoteDepotEstablished,
                    $"{citizen.DisplayName} established {structure.DisplayName}"));
            }
            return true;
        }
        private PrototypeStructureState CreateStructure(string structureKindId, int structureIndex, bool isBuilt)
        {
            string displayName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(InventoryComponent.FormatItemName(structureKindId));
            string structureId = $"{structureKindId}_{structureIndex + 1}";
            Vector3 position = GetStructurePosition(structureKindId, structureIndex);

            PrototypeStructureState structure = new()
            {
                StructureId = structureId,
                StructureKindId = structureKindId,
                DisplayName = displayName,
                Position = position,
                GridX = _world.WorldMap.GetNearestCell(position).GridX,
                GridY = _world.WorldMap.GetNearestCell(position).GridY,
                IsBuilt = isBuilt,
                BedCapacity = structureKindId == "hut" && isBuilt ? 2 : 0,
                InputStore = CreateStore($"{structureId}.input", $"{displayName} Input", 24, position),
                OutputStore = CreateStore($"{structureId}.output", $"{displayName} Output", 24, position)
            };

            if (structureKindId == "central_hearth")
            {
                structure.HearthFuel = 2;
            }

            return structure;
        }
        private int GetBuildTicks(string structureId) => GetStructure(structureId)?.StructureKindId switch
        {
            "hut" => 40,
            "drying_rack" => 34,
            "storehouse" => 44,
            "kiln" => 42,
            "remote_stockpile" => 36,
            "path_segment" => PathBuildTicks,
            _ => 36
        };
        private static IReadOnlyDictionary<string, int> GetConstructionCost(string structureKindId) => structureKindId switch
        {
            "hut" => HutCost,
            "storehouse" => StorehouseCost,
            "drying_rack" => DryingRackCost,
            "kiln" => KilnCost,
            "remote_stockpile" => RemoteDepotCost,
            _ => new Dictionary<string, int>()
        };
        private PrototypeStructureState? GetStructure(string structureId) => _structures.FirstOrDefault(structure => string.Equals(structure.StructureId, structureId, StringComparison.Ordinal));
        private Vector3 GetStructurePosition(string structureKindId, int structureIndex)
        {
            Vector3 anchor = _world.SettlementSpawn.AnchorPosition;
            Vector3 offset = structureKindId switch
            {
                "central_hearth" => new Vector3(0.0f, 0.0f, 0.85f),
                "central_depot" => new Vector3(-2.2f, 0.0f, 0.8f),
                "cookfire" => new Vector3(1.8f, 0.0f, -1.2f),
                "wood_yard" => new Vector3(2.9f, 0.0f, 1.4f),
                "drying_rack" => new Vector3(-4.4f, 0.0f, 2.2f),
                "kiln" => new Vector3(4.8f, 0.0f, 2.6f),
                "storehouse" => new Vector3(-4.2f, 0.0f, -2.6f),
                "hut" => GetHutOffset(structureIndex),
                _ => Vector3.Zero
            };

            return ProjectToSurface(anchor + offset);
        }
        private static Vector3 GetHutOffset(int structureIndex)
        {
            float angle = (-Mathf.Pi * 0.40f) + (structureIndex * 0.85f);
            float radius = 5.4f + (structureIndex * 0.2f);
            return new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
        }

    }
}
