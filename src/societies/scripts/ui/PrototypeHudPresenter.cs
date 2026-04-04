using Godot;
using Societies.Core;
using Societies.Simulation;
using System.Collections.Generic;

namespace Societies.UI
{
    /// <summary>
    /// Applies pure HUD text builders to the live Godot HUD.
    /// </summary>
    public static class PrototypeHudPresenter
    {
        public static void Initialize(PrototypeHud hud)
        {
            hud.SetHelpText(PrototypeHudTextBuilder.BuildHelpText());
        }

        public static void Apply(
            PrototypeHud hud,
            int fps,
            int entityCount,
            string timeText,
            string weatherText,
            string sessionMode,
            long simulationTick,
            InventoryComponent inventory,
            IReadOnlyDictionary<string, int> stockpile,
            IReadOnlyList<PrototypeWorkerState> workers,
            IReadOnlyList<PrototypeStructureState> structures,
            PrototypeSettlementClassification settlementClassification,
            string buildQueueStatusText,
            int mealCoveragePercent,
            int bedCoveragePercent,
            int hearthFuel,
            float averageRouteLengthMeters,
            float averageTravelWorkRatio,
            float pathCoverageRatio,
            IReadOnlyDictionary<string, int> routeBacklogTicksByKind,
            string interactionText,
            PrototypeWorkerState? selectedCitizen = null,
            PrototypeStructureState? selectedStructure = null,
            string? scenarioId = null,
            int? worldSeed = null,
            CameraMode cameraMode = CameraMode.Player,
            TerrainOverlayMode overlayMode = TerrainOverlayMode.None,
            PrototypeWorldSummary? worldSummary = null)
        {
            hud.SetDebugText(
                PrototypeHudTextBuilder.BuildDebugText(
                    fps,
                    entityCount,
                    timeText,
                    weatherText,
                    sessionMode,
                    simulationTick,
                    scenarioId,
                    worldSeed,
                    cameraMode,
                    overlayMode));
            hud.SetInventoryText(inventory.GetSummaryText());
            hud.SetCraftingText(CraftingSystem.GetRecipeSummary(inventory));
            hud.SetSettlementText(
                PrototypeHudTextBuilder.BuildSettlementText(
                    stockpile,
                    workers,
                    settlementClassification,
                    buildQueueStatusText,
                    mealCoveragePercent,
                    bedCoveragePercent,
                    hearthFuel,
                    structures,
                    averageTravelWorkRatio,
                    routeBacklogTicksByKind));
            hud.SetWorldText(
                PrototypeHudTextBuilder.BuildWorldText(
                    scenarioId ?? "unknown",
                    worldSeed ?? 0,
                    cameraMode,
                    overlayMode,
                    worldSummary,
                    averageRouteLengthMeters,
                    pathCoverageRatio));
            hud.SetInspectorText(PrototypeHudTextBuilder.BuildInspectorText(selectedCitizen, selectedStructure));
            hud.SetInteractionText(interactionText);
        }
    }
}
