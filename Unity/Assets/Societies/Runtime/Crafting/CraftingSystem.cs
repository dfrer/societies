using System;
using System.Collections.Generic;
using Societies.Runtime.Inventory;
using UnityEngine;

namespace Societies.Runtime.Crafting
{
    public class CraftingSystem : MonoBehaviour
    {
        [Serializable]
        public struct Recipe
        {
            public int Id;
            public string Name;
            public int[] InputItemIds;
            public int[] InputQuantities;
            public int OutputItemId;
            public int OutputQuantity;
            public float CraftTime;
        }

        public List<Recipe> Recipes = new();

        private void Start()
        {
            Recipes.Clear();

            Recipes.Add(new Recipe
            {
                Id = 1,
                Name = "Wood Planks",
                InputItemIds = new[] { 7 },
                InputQuantities = new[] { 4 },
                OutputItemId = 20,
                OutputQuantity = 4,
                CraftTime = 1f
            });

            Recipes.Add(new Recipe
            {
                Id = 2,
                Name = "Stone Bricks",
                InputItemIds = new[] { 3 },
                InputQuantities = new[] { 2 },
                OutputItemId = 21,
                OutputQuantity = 2,
                CraftTime = 1.5f
            });

            Recipes.Add(new Recipe
            {
                Id = 3,
                Name = "Workbench",
                InputItemIds = new[] { 7, 3 },
                InputQuantities = new[] { 4, 2 },
                OutputItemId = 40,
                OutputQuantity = 1,
                CraftTime = 3f
            });

            // Requires furnace.
            Recipes.Add(new Recipe
            {
                Id = 4,
                Name = "Copper Ingot",
                InputItemIds = new[] { 5 },
                InputQuantities = new[] { 1 },
                OutputItemId = 30,
                OutputQuantity = 1,
                CraftTime = 2f
            });

            // Requires furnace.
            Recipes.Add(new Recipe
            {
                Id = 5,
                Name = "Iron Ingot",
                InputItemIds = new[] { 6 },
                InputQuantities = new[] { 1 },
                OutputItemId = 31,
                OutputQuantity = 1,
                CraftTime = 2.5f
            });

            Recipes.Add(new Recipe
            {
                Id = 6,
                Name = "Stone Pickaxe",
                InputItemIds = new[] { 7, 3 },
                InputQuantities = new[] { 2, 1 },
                OutputItemId = 50,
                OutputQuantity = 1,
                CraftTime = 2f
            });

            Recipes.Add(new Recipe
            {
                Id = 7,
                Name = "Iron Pickaxe",
                InputItemIds = new[] { 7, 31 },
                InputQuantities = new[] { 2, 1 },
                OutputItemId = 51,
                OutputQuantity = 1,
                CraftTime = 2.5f
            });

            Recipes.Add(new Recipe
            {
                Id = 8,
                Name = "Stone Axe",
                InputItemIds = new[] { 7, 3 },
                InputQuantities = new[] { 2, 1 },
                OutputItemId = 52,
                OutputQuantity = 1,
                CraftTime = 2f
            });
        }

        public bool CanCraft(InventoryManager inventoryManager, Recipe recipe)
        {
            if (inventoryManager == null)
            {
                return false;
            }

            if (recipe.InputItemIds == null || recipe.InputQuantities == null)
            {
                return false;
            }

            if (recipe.InputItemIds.Length != recipe.InputQuantities.Length)
            {
                return false;
            }

            for (int i = 0; i < recipe.InputItemIds.Length; i++)
            {
                if (!inventoryManager.HasItem(recipe.InputItemIds[i], recipe.InputQuantities[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryCraft(InventoryManager inventoryManager, Recipe recipe)
        {
            if (!CanCraft(inventoryManager, recipe))
            {
                return false;
            }

            for (int i = 0; i < recipe.InputItemIds.Length; i++)
            {
                if (!inventoryManager.RemoveItem(recipe.InputItemIds[i], recipe.InputQuantities[i]))
                {
                    return false;
                }
            }

            return inventoryManager.TryAddItem(recipe.OutputItemId, recipe.OutputQuantity);
        }
    }
}
