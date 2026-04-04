using Xunit;

namespace Societies.Core.Tests
{
    public class CraftingSystemTests
    {
        [Fact]
        public void TryCraft_WhenInputsExist_ConsumesInputsAndCreatesOutput()
        {
            InventoryComponent inventory = new();
            inventory.AddItem("logs", 2);
            inventory.AddItem("stone", 3);

            bool crafted = CraftingSystem.TryCraft("stone_axe", inventory, out CraftingRecipe? recipe);

            Assert.True(crafted);
            Assert.NotNull(recipe);
            Assert.Equal(0, inventory.GetCount("logs"));
            Assert.Equal(0, inventory.GetCount("stone"));
            Assert.Equal(1, inventory.GetCount("stone_axe"));
        }

        [Fact]
        public void TryCraft_WhenInputsMissing_ReturnsFalseWithoutMutatingInventory()
        {
            InventoryComponent inventory = new();
            inventory.AddItem("logs", 1);

            bool crafted = CraftingSystem.TryCraft("stone_axe", inventory, out CraftingRecipe? recipe);

            Assert.False(crafted);
            Assert.NotNull(recipe);
            Assert.Equal(1, inventory.GetCount("logs"));
            Assert.Equal(0, inventory.GetCount("stone_axe"));
        }

        [Fact]
        public void GetFailureText_ListsMissingIngredients()
        {
            InventoryComponent inventory = new();
            inventory.AddItem("logs", 1);

            string failureText = CraftingSystem.GetFailureText("stone_axe", inventory);

            Assert.Contains("logs x1", failureText);
            Assert.Contains("stone x3", failureText);
        }

        [Fact]
        public void GetRecipeSummary_ReflectsAffordability()
        {
            InventoryComponent inventory = new();
            inventory.AddItem("logs", 2);
            inventory.AddItem("stone", 3);

            string summary = CraftingSystem.GetRecipeSummary(inventory);

            Assert.Contains("Stone Axe [ready]", summary);
            Assert.DoesNotContain("Campfire", summary);
        }
    }
}
