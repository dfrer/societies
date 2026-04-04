using System.Collections.Generic;
using Xunit;

namespace Societies.Core.Tests
{
    public class InventoryComponentTests
    {
        [Fact]
        public void AddItem_ValidAmount_UpdatesCountAndRaisesChanged()
        {
            InventoryComponent inventory = new();
            int changedCount = 0;
            inventory.Changed += () => changedCount++;

            inventory.AddItem("wood", 3);

            Assert.Equal(3, inventory.GetCount("wood"));
            Assert.Equal(1, changedCount);
        }

        [Fact]
        public void RemoveItem_WhenSufficientAmount_RemovesItemsAndRaisesChanged()
        {
            InventoryComponent inventory = new();
            int changedCount = 0;
            inventory.Changed += () => changedCount++;
            inventory.AddItem("stone", 4);

            bool removed = inventory.RemoveItem("stone", 3);

            Assert.True(removed);
            Assert.Equal(1, inventory.GetCount("stone"));
            Assert.Equal(2, changedCount);
        }

        [Fact]
        public void ReplaceContents_ReplacesExistingInventoryInSingleMutation()
        {
            InventoryComponent inventory = new();
            int changedCount = 0;
            inventory.Changed += () => changedCount++;
            inventory.AddItem("wood", 5);

            inventory.ReplaceContents(new Dictionary<string, int>
            {
                ["berry"] = 2,
                ["stone"] = 3,
                ["wood"] = 0
            });

            Assert.Equal(0, inventory.GetCount("wood"));
            Assert.Equal(2, inventory.GetCount("berry"));
            Assert.Equal(3, inventory.GetCount("stone"));
            Assert.Equal(2, changedCount);
        }

        [Fact]
        public void HasItems_ReturnsFalseWhenCostCannotBePaid()
        {
            InventoryComponent inventory = new();
            inventory.AddItem("wood", 1);

            bool hasItems = inventory.HasItems(new Dictionary<string, int>
            {
                ["wood"] = 1,
                ["stone"] = 1
            });

            Assert.False(hasItems);
        }
    }
}
