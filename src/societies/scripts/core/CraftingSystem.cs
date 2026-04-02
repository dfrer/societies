using System.Collections.Generic;
using System.Linq;

namespace Societies.Core
{
    /// <summary>
    /// Minimal recipe table for Prototype 1.
    /// </summary>
    public static class CraftingSystem
    {
        private static readonly IReadOnlyList<CraftingRecipe> Recipes = new List<CraftingRecipe>
        {
            new(
                "stone_axe",
                "Stone Axe",
                new Dictionary<string, int> { ["wood"] = 2, ["stone"] = 3 },
                new Dictionary<string, int> { ["stone_axe"] = 1 }
            ),
            new(
                "campfire",
                "Campfire",
                new Dictionary<string, int> { ["wood"] = 3, ["stone"] = 4 },
                new Dictionary<string, int> { ["campfire"] = 1 }
            )
        };

        public static bool TryCraft(string recipeId, InventoryComponent inventory, out CraftingRecipe? recipe)
        {
            recipe = Recipes.FirstOrDefault(candidate => candidate.Id == recipeId);
            if (recipe == null || !inventory.HasItems(recipe.Inputs))
            {
                return false;
            }

            foreach ((string itemId, int amount) in recipe.Inputs)
            {
                inventory.RemoveItem(itemId, amount);
            }

            foreach ((string itemId, int amount) in recipe.Outputs)
            {
                inventory.AddItem(itemId, amount);
            }

            return true;
        }

        public static string GetFailureText(string recipeId, InventoryComponent inventory)
        {
            CraftingRecipe? recipe = Recipes.FirstOrDefault(candidate => candidate.Id == recipeId);
            if (recipe == null)
            {
                return "Unknown recipe";
            }

            List<string> missing = new();
            foreach ((string itemId, int amount) in recipe.Inputs)
            {
                int shortfall = amount - inventory.GetCount(itemId);
                if (shortfall > 0)
                {
                    missing.Add($"{InventoryComponent.FormatItemName(itemId)} x{shortfall}");
                }
            }

            return missing.Count == 0
                ? $"Cannot craft {recipe.DisplayName}"
                : $"Missing {string.Join(", ", missing)}";
        }

        public static string GetRecipeSummary(InventoryComponent inventory)
        {
            List<string> lines = new() { "Crafting" };

            foreach ((CraftingRecipe recipe, int index) in Recipes.Select((recipe, index) => (recipe, index)))
            {
                string affordability = inventory.HasItems(recipe.Inputs) ? "ready" : "missing";
                string ingredients = string.Join(", ", recipe.Inputs.Select(pair => $"{InventoryComponent.FormatItemName(pair.Key)} x{pair.Value}"));
                lines.Add($"{index + 1}. {recipe.DisplayName} [{affordability}]");
                lines.Add($"   {ingredients}");
            }

            return string.Join('\n', lines);
        }
    }

    public sealed class CraftingRecipe
    {
        public CraftingRecipe(
            string id,
            string displayName,
            IReadOnlyDictionary<string, int> inputs,
            IReadOnlyDictionary<string, int> outputs)
        {
            Id = id;
            DisplayName = displayName;
            Inputs = inputs;
            Outputs = outputs;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyDictionary<string, int> Inputs { get; }
        public IReadOnlyDictionary<string, int> Outputs { get; }
    }
}
