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
            string interactionText,
            string? scenarioId = null)
        {
            hud.SetDebugText(
                PrototypeHudTextBuilder.BuildDebugText(
                    fps,
                    entityCount,
                    timeText,
                    weatherText,
                    sessionMode,
                    simulationTick,
                    scenarioId));
            hud.SetInventoryText(inventory.GetSummaryText());
            hud.SetCraftingText(CraftingSystem.GetRecipeSummary(inventory));
            hud.SetSettlementText(PrototypeHudTextBuilder.BuildSettlementText(stockpile, workers));
            hud.SetInteractionText(interactionText);
        }
    }
}
