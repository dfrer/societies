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
                   "Tab inventory  1 craft Stone Axe  2 craft Campfire  F5 toggle weather  F6 save snapshot  F7 reset run  F8 observer  F9 load snapshot  F10 overlays  Esc mouse";
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
                "Societies Prototype V2 M1",
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
            PrototypeWorldSummary? worldSummary)
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
            IReadOnlyList<PrototypeWorkerState> workers)
        {
            List<string> lines = new() { "Settlement" };

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

            if (workers.Count == 0)
            {
                lines.Add("Workers: none");
            }
            else
            {
                lines.Add("Workers:");
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
                    return $"{worker.DisplayName}: {activity}{progress}{target}{carry}";
                }));
            }

            return string.Join('\n', lines);
        }
    }
}
