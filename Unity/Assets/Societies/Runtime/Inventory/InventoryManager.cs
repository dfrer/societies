using UnityEngine;

namespace Societies.Runtime.Inventory
{
    /// <summary>
    /// Manages player inventory operations
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        [Header("Inventory Settings")]
        [SerializeField] private int _slotCount = 64;
        [SerializeField] private float _capacityKg = 100f;
        [SerializeField] private int _hotbarSize = 9;

        public InventoryState Inventory { get; private set; }
        public int SelectedSlot { get; private set; }

        public float CurrentWeight => Inventory.CurrentWeightKg;
        public float Capacity => Inventory.CapacityKg;
        public float EncumbrancePercent => Inventory.EncumbrancePercent;

        public event Action<int> OnSlotSelected;
        public event Action OnInventoryChanged;

        private void Awake()
        {
            Inventory = new InventoryState(_slotCount);
            Inventory.CapacityKg = _capacityKg;
            SelectedSlot = 0;
        }

        /// <summary>
        /// Add item to inventory
        /// </summary>
        public bool TryAddItem(int itemId, int quantity)
        {
            bool result = Inventory.TryAddItem(itemId, quantity);
            if (result)
            {
                OnInventoryChanged?.Invoke();
            }
            return result;
        }

        /// <summary>
        /// Remove item from inventory
        /// </summary>
        public bool RemoveItem(int itemId, int quantity)
        {
            bool result = Inventory.RemoveItem(itemId, quantity);
            if (result)
            {
                OnInventoryChanged?.Invoke();
            }
            return result;
        }

        /// <summary>
        /// Get item count in inventory
        /// </summary>
        public int GetItemCount(int itemId)
        {
            return Inventory.GetItemCount(itemId);
        }

        /// <summary>
        /// Check if has item
        /// </summary>
        public bool HasItem(int itemId, int quantity = 1)
        {
            return Inventory.HasItem(itemId, quantity);
        }

        /// <summary>
        /// Select hotbar slot (0-8)
        /// </summary>
        public void SelectSlot(int slot)
        {
            if (slot >= 0 && slot < _hotbarSize)
            {
                SelectedSlot = slot;
                OnSlotSelected?.Invoke(slot);
            }
        }

        /// <summary>
        /// Get item in selected slot
        /// </summary>
        public ItemStack GetSelectedItem()
        {
            if (SelectedSlot >= 0 && SelectedSlot < Inventory.Slots.Length)
            {
                return Inventory.Slots[SelectedSlot].Stack;
            }
            return ItemStack.Empty;
        }

        /// <summary>
        /// Get item in specific slot
        /// </summary>
        public ItemStack GetSlotItem(int slot)
        {
            if (slot >= 0 && slot < Inventory.Slots.Length)
            {
                return Inventory.Slots[slot].Stack;
            }
            return ItemStack.Empty;
        }

        /// <summary>
        /// Cycle to next slot
        /// </summary>
        public void CycleSlot(int direction)
        {
            int newSlot = SelectedSlot + direction;
            if (newSlot < 0) newSlot = _hotbarSize - 1;
            if (newSlot >= _hotbarSize) newSlot = 0;
            SelectSlot(newSlot);
        }
    }
}