using UnityEngine;
using Societies.Runtime.Inventory;

namespace Societies.Runtime.World
{
    public class StorageContainer : MonoBehaviour
    {
        public string ContainerId { get; private set; }
        public InventoryState Inventory { get; private set; }

        public int SlotCount = 27;
        public float CapacityKg = 100f;

        private void Awake()
        {
            ContainerId = System.Guid.NewGuid().ToString();
            Inventory = new InventoryState(SlotCount);
            Inventory.CapacityKg = CapacityKg;
        }

        // Allow player to access
        public bool CanOpen(string playerId)
        {
            return true; // MVP: anyone can open
        }

        // Add item to storage
        public bool AddItem(int itemId, int quantity)
        {
            return Inventory.TryAddItem(itemId, quantity);
        }

        // Take item from storage
        public bool TakeItem(int itemId, int quantity)
        {
            return Inventory.RemoveItem(itemId, quantity);
        }
    }
}
