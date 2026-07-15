using Societies.Core;
using Societies.Simulation;
using System.Collections.Generic;
using System.Linq;

namespace Societies.UI
{
    /// <summary>
    /// Pure HUD text composition for the prototype runtime.
    /// </summary>
    public static class PrototypeHudTextBuilder
    {
        public static string BuildHelpText()
        {
            return "WASD move  Shift sprint  Space jump  Mouse look  E harvest\n" +
                   "Tab inventory  1 craft Stone Axe  2 Food & Fuel  3 Shelter  F3 cycle citizen  F4 cycle structure  F5 toggle weather  F6 save snapshot  F7 reset run\n" +
                   "F8 observer  F9 load snapshot  F10 overlays (terrain/routes/depots)  F11 next build  F12 pause build  Esc mouse";
        }

        public static string BuildDebugText(
            int fps,
            int entityCount,
            string timeText,
            string weatherText,
            string sessionMode,
            long simulationTick,
            string? scenarioId = null,
            int? worldSeed = null,
            CameraMode cameraMode = CameraMode.Player,
            TerrainOverlayMode overlayMode = TerrainOverlayMode.None)
        {
            List<string> lines = new()
            {
                "Societies Prototype V2 M3",
                $"FPS: {fps}",
                $"Entities: {entityCount}",
                $"Time: {timeText}",
                $"Weather: {weatherText}",
                $"Mode: {sessionMode}"
            };

            if (!string.IsNullOrWhiteSpace(scenarioId))
            {
                lines.Add($"Scenario: {scenarioId}");
            }

            if (worldSeed.HasValue)
            {
                lines.Add($"World Seed: {worldSeed.Value}");
            }

            lines.Add($"Camera: {cameraMode}");
            lines.Add($"Overlay: {overlayMode}");
            lines.Add($"Tick: {simulationTick}");
            return string.Join('\n', lines);
        }

        public static string BuildWorldText(
            string scenarioId,
            int worldSeed,
            CameraMode cameraMode,
            TerrainOverlayMode overlayMode,
            PrototypeWorldSummary? worldSummary,
            float averageRouteLengthMeters = 0.0f,
            float pathCoverageRatio = 0.0f)
        {
            List<string> lines = new()
            {
                "World",
                $"Scenario: {scenarioId}",
                $"World Seed: {worldSeed}",
                $"Camera: {cameraMode}",
                $"Overlay: {overlayMode}"
            };

            if (worldSummary != null)
            {
                lines.Add($"Terrain: {worldSummary.TerrainMode}");
                int buildablePercent = (int)System.MathF.Round(worldSummary.BuildableCellRatio * 100.0f);
                lines.Add($"Buildable: {buildablePercent} %");
                lines.Add($"Avg Route: {averageRouteLengthMeters:0.0} m");
                lines.Add($"Path Cover: {pathCoverageRatio * 100.0f:0}%");

                if (worldSummary.BiomeCellCounts.Count > 0)
                {
                    string biomeSummary = string.Join(
                        ", ",
                        worldSummary.BiomeCellCounts
                            .OrderBy(pair => pair.Key)
                            .Select(pair => $"{pair.Key} {pair.Value}"));
                    lines.Add($"Biomes: {biomeSummary}");
                }
            }

            return string.Join('\n', lines);
        }

        public static string BuildSettlementText(
            IReadOnlyDictionary<string, int> stockpile,
            IReadOnlyList<PrototypeWorkerState> workers,
            PrototypeSettlementClassification classification = PrototypeSettlementClassification.Strained,
            string? buildQueueStatusText = null,
            int mealCoveragePercent = 0,
            int bedCoveragePercent = 0,
            int hearthFuel = 0,
            IReadOnlyList<PrototypeStructureState>? structures = null,
            float averageTravelWorkRatio = 0.0f,
            IReadOnlyDictionary<string, int>? routeBacklogTicksByKind = null,
            PrototypeSettlementDirective directive = PrototypeSettlementDirective.Neutral)
        {
            List<string> lines = new() { "Settlement" };

            int activeCitizens = workers.Count(worker => worker.Phase != PrototypeWorkerPhase.Idle);
            lines.Add($"State: {classification}");
            lines.Add($"Citizens: {activeCitizens}/{workers.Count} active");
            lines.Add($"Directive: {PrototypeSettlementDirectiveCatalog.GetDisplayName(directive)}");
            lines.Add($"Meals: {mealCoveragePercent} %  Beds: {bedCoveragePercent} %  Hearth Fuel: {hearthFuel}");
            lines.Add($"Travel/Work: {averageTravelWorkRatio:0.00}");

            if (!string.IsNullOrWhiteSpace(buildQueueStatusText))
            {
                lines.Add(buildQueueStatusText);
            }

            if (routeBacklogTicksByKind is { Count: > 0 })
            {
                string backlogSummary = string.Join(
                    ", ",
                    routeBacklogTicksByKind
                        .Where(pair => pair.Value > 0)
                        .OrderByDescending(pair => pair.Value)
                        .Take(3)
                        .Select(pair => $"{pair.Key} {pair.Value}"));
                if (!string.IsNullOrWhiteSpace(backlogSummary))
                {
                    lines.Add($"Backlog: {backlogSummary}");
                }
            }

            if (stockpile.Count == 0)
            {
                lines.Add("Stockpile: empty");
            }
            else
            {
                string stockpileSummary = string.Join(
                    ", ",
                    stockpile.OrderBy(pair => pair.Key).Select(pair => $"{InventoryComponent.FormatItemName(pair.Key)} x{pair.Value}"));
                lines.Add($"Stockpile: {stockpileSummary}");
            }

            if (structures is { Count: > 0 })
            {
                string structureSummary = string.Join(
                    ", ",
                    structures
                        .GroupBy(structure => structure.StructureKindId)
                        .OrderBy(group => group.Key)
                        .Select(group =>
                        {
                            int built = group.Count(structure => structure.IsBuilt);
                            int total = group.Count();
                            int blocked = group.Count(structure => structure.IsBlocked);
                            string status = blocked > 0 ? $" !{blocked}" : string.Empty;
                            return $"{InventoryComponent.FormatItemName(group.Key)} {built}/{total}{status}";
                        }));
                lines.Add($"Structures: {structureSummary}");
            }

            if (workers.Count == 0)
            {
                lines.Add("Citizens: none");
            }
            else
            {
                lines.Add("Citizens:");
                lines.AddRange(workers.Select(worker =>
                {
                    string carry = worker.CarryAmount > 0
                        ? $" [{InventoryComponent.FormatItemName(worker.CarryItemId)} x{worker.CarryAmount}]"
                        : string.Empty;
                    string target = string.IsNullOrWhiteSpace(worker.TargetLabel)
                        ? string.Empty
                        : $" -> {worker.TargetLabel}";
                    string activity = string.IsNullOrWhiteSpace(worker.ActivityText)
                        ? worker.Phase.ToString()
                        : worker.ActivityText;
                    string progress = worker.Phase == PrototypeWorkerPhase.Idle
                        ? string.Empty
                        : $" ({worker.ProgressPercent} %)";
                    return $"{worker.DisplayName} [{worker.Role} N{worker.Needs.Nutrition:0} F{worker.Needs.Fatigue:0}]: {activity}{progress}{target}{carry}";
                }));
            }

            return string.Join('\n', lines);
        }

        public static string BuildInspectorText(
            PrototypeWorkerState? selectedCitizen,
            PrototypeStructureState? selectedStructure)
        {
            List<string> lines = new() { "Inspector" };

            if (selectedCitizen == null)
            {
                lines.Add("Citizen: none");
            }
            else
            {
                string carry = selectedCitizen.CarryAmount > 0
                    ? $"{InventoryComponent.FormatItemName(selectedCitizen.CarryItemId)} x{selectedCitizen.CarryAmount}"
                    : "empty";
                string order = string.IsNullOrWhiteSpace(selectedCitizen.CurrentOrderKind?.ToString())
                    ? selectedCitizen.Phase.ToString()
                    : $"{selectedCitizen.CurrentOrderKind}: {selectedCitizen.CurrentOrderReason}";

                lines.Add($"Citizen: {selectedCitizen.DisplayName} [{selectedCitizen.Role}]");
                lines.Add($"Needs: nutrition {selectedCitizen.Needs.Nutrition:0}  fatigue {selectedCitizen.Needs.Fatigue:0}");
                lines.Add($"Order: {order}");
                lines.Add($"Carry: {carry}");
                lines.Add($"Route: {selectedCitizen.Navigation.CurrentRouteLengthMeters:0.0} m  {selectedCitizen.Navigation.CurrentRouteTravelTicks} ticks  T/W {selectedCitizen.TravelWorkRatio:0.00}");
                if (!string.IsNullOrWhiteSpace(selectedCitizen.LastFailureReason))
                {
                    lines.Add($"Failure: {selectedCitizen.LastFailureReason}");
                }
            }

            if (selectedStructure == null)
            {
                lines.Add("Structure: none");
            }
            else
            {
                string status = selectedStructure.IsBuilt
                    ? selectedStructure.IsBlocked ? $"blocked ({selectedStructure.BlockedReason})" : "built"
                    : "planned";

                lines.Add($"Structure: {selectedStructure.DisplayName}");
                lines.Add($"Status: {status}");
                lines.Add($"Input: {FormatStoreSummary(selectedStructure.InputStore)}");
                lines.Add($"Output: {FormatStoreSummary(selectedStructure.OutputStore)}");
            }

            return string.Join('\n', lines);
        }

        private static string FormatStoreSummary(PrototypeResourceStoreState store)
        {
            if (store.Items.Count == 0)
            {
                return "empty";
            }

            return string.Join(
                ", ",
                store.Items
                    .OrderBy(pair => pair.Key)
                    .Select(pair => $"{InventoryComponent.FormatItemName(pair.Key)} x{pair.Value}"));
        }
    }
}
