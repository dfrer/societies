using System.Collections.Generic;
using System.Linq;
using Societies.Simulation;

namespace Societies.Core
{
    /// <summary>
    /// Builds compact machine-readable summaries for prototype validation runs.
    /// </summary>
    public static class PrototypeRunSummaryBuilder
    {
        public static PrototypeRunSummary Build(
            PrototypeRuntimeSnapshot snapshot,
            IReadOnlyList<PrototypeEventRecord> eventRecords,
            float startHour)
        {
            Dictionary<string, int> craftedItemCounts = BuildCraftedItemCounts(snapshot);

            return new PrototypeRunSummary
            {
                SimulationSeed = snapshot.SimulationSeed,
                SimulationTick = snapshot.SimulationTick,
                StartHour = startHour,
                StartTimeText = PrototypeClockService.FormatTime(startHour),
                EndHour = snapshot.CurrentHour,
                EndTimeText = PrototypeClockService.FormatTime(snapshot.CurrentHour),
                FinalWeather = snapshot.CurrentWeather,
                PlayerInventory = OrderCounts(snapshot.Inventory),
                Stockpile = OrderCounts(snapshot.Stockpile),
                RemainingResourcesByType = OrderCounts(
                    snapshot.Resources
                        .GroupBy(resource => resource.ResourceId)
                        .ToDictionary(group => group.Key, group => group.Sum(resource => resource.UnitsRemaining))),
                WorkersByPhase = OrderCounts(
                    snapshot.Workers
                        .GroupBy(worker => worker.Phase)
                        .ToDictionary(group => group.Key, group => group.Count())),
                CraftedItemCounts = craftedItemCounts,
                EventCountsByType = OrderCounts(
                    eventRecords
                        .GroupBy(record => record.EventType)
                        .ToDictionary(group => group.Key, group => group.Count()))
            };
        }

        private static Dictionary<string, int> BuildCraftedItemCounts(PrototypeRuntimeSnapshot snapshot)
        {
            Dictionary<string, int> combinedInventory = snapshot.Inventory
                .Concat(snapshot.Stockpile)
                .GroupBy(pair => pair.Key)
                .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value));

            return OrderCounts(
                CraftingSystem
                    .GetCraftedItemIds()
                    .Where(itemId => combinedInventory.TryGetValue(itemId, out int count) && count > 0)
                    .ToDictionary(itemId => itemId, itemId => combinedInventory[itemId]));
        }

        private static Dictionary<string, int> OrderCounts(IReadOnlyDictionary<string, int> counts)
        {
            return counts
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }
}
