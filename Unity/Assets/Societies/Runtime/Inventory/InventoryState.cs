using System;
using UnityEngine;

namespace Societies.Runtime.Inventory
{
    /// <summary>
    /// Single item stack in inventory
    /// </summary>
    [System.Serializable]
    public struct ItemStack
    {
        public int ItemId;
        public int Quantity;
        public float Quality;
        public int Durability;

        public bool IsEmpty => ItemId == 0 || Quantity <= 0;

        public static ItemStack Empty => new() { ItemId = 0, Quantity = 0, Quality = 1f, Durability = 0 };

        public static ItemStack Create(int itemId, int quantity, float quality = 1f, int durability = -1)
        {
            return new ItemStack
            {
                ItemId = itemId,
                Quantity = quantity,
                Quality = quality,
                Durability = durability
            };
        }

        public override string ToString() => $"Item({ItemId}, x{Quantity}, q:{Quality:F2}, d:{Durability})";
    }

    /// <summary>
    /// Inventory slot with item stack
    /// </summary>
    [System.Serializable]
    public class InventorySlot
    {
        public ItemStack Stack;
        public bool IsLocked;

        public InventorySlot()
        {
            Stack = ItemStack.Empty;
            IsLocked = false;
        }

        public bool IsEmpty => Stack.IsEmpty;
    }

    /// <summary>
    /// Inventory state (64 slots for MVP)
    /// </summary>
    public class InventoryState
    {
        public const int DEFAULT_SLOT_COUNT = 64;

        public InventorySlot[] Slots;
        public float CurrentWeightKg { get; private set; }
        public float CapacityKg { get; set; } = 100f;

        public InventoryState(int slotCount = DEFAULT_SLOT_COUNT)
        {
            Slots = new InventorySlot[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                Slots[i] = new InventorySlot();
            }
            CurrentWeightKg = 0f;
        }

        /// <summary>
        /// Get encumbrance percentage (0-100+)
        /// </summary>
        public float EncumbrancePercent => (CurrentWeightKg / CapacityKg) * 100f;

        /// <summary>
        /// Get encumbrance band (0-4)
        /// </summary>
        public int EncumbranceBand
        {
            get
            {
                float percent = EncumbrancePercent;
                if (percent < 25f) return 0;
                if (percent < 50f) return 1;
                if (percent < 75f) return 2;
                if (percent < 100f) return 3;
                return 4;
            }
        }

        /// <summary>
        /// Recalculate total weight
        /// </summary>
        public void RecalculateWeight()
        {
            float total = 0f;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty)
                {
                    total += GetItemWeight(Slots[i].Stack.ItemId) * Slots[i].Stack.Quantity;
                }
            }
            CurrentWeightKg = total;
        }

        /// <summary>
        /// Get weight of an item by ID (simplified MVP version)
        /// </summary>
        public static float GetItemWeight(int itemId)
        {
            // MVP: simplified weights
            return itemId switch
            {
                // Raw resources
                1 => 1.5f,   // Dirt
                2 => 1.5f,   // Grass
                3 => 2.5f,   // Stone
                4 => 1.6f,   // Coal
                5 => 2.7f,   // Copper Ore
                6 => 3.0f,   // Iron Ore
                7 => 0.8f,   // Wood
                8 => 0.5f,   // Leaves
                9 => 1.6f,   // Sand
                10 => 2.0f,  // Clay

                // Processed
                20 => 0.6f,  // Wood Plank
                21 => 2.4f,  // Stone Brick
                30 => 3.5f,  // Copper Ingot
                31 => 4.5f,  // Iron Ingot

                // Default
                _ => 1.0f
            };
        }

        /// <summary>
        /// Try add item to inventory
        /// </summary>
        public bool TryAddItem(int itemId, int quantity, int slot = -1)
        {
            if (quantity <= 0) return false;

            float itemWeight = GetItemWeight(itemId) * quantity;
            if (CurrentWeightKg + itemWeight > CapacityKg && slot < 0)
            {
                return false; // Can't fit
            }

            // Try to stack first
            if (slot < 0)
            {
                for (int i = 0; i < Slots.Length; i++)
                {
                    if (!Slots[i].IsEmpty && Slots[i].Stack.ItemId == itemId)
                    {
                        // Stack exists, add to it
                        if (CurrentWeightKg + itemWeight <= CapacityKg)
                        {
                            Slots[i].Stack.Quantity += quantity;
                            CurrentWeightKg += itemWeight;
                            return true;
                        }
                    }
                }
            }

            // Find empty slot
            int targetSlot = slot >= 0 ? slot : FindEmptySlot();
            if (targetSlot < 0) return false;

            // Check weight for specific slot
            if (CurrentWeightKg + itemWeight > CapacityKg) return false;

            Slots[targetSlot].Stack = ItemStack.Create(itemId, quantity);
            CurrentWeightKg += itemWeight;
            return true;
        }

        /// <summary>
        /// Remove item from inventory
        /// </summary>
        public bool RemoveItem(int itemId, int quantity, int slot = -1)
        {
            if (quantity <= 0) return false;

            int targetSlot = slot >= 0 ? slot : FindSlotWithItem(itemId);
            if (targetSlot < 0) return false;

            var slotItem = Slots[targetSlot].Stack;
            if (slotItem.ItemId != itemId || slotItem.Quantity < quantity)
                return false;

            float itemWeight = GetItemWeight(itemId) * quantity;

            if (slotItem.Quantity == quantity)
            {
                Slots[targetSlot].Stack = ItemStack.Empty;
            }
            else
            {
                Slots[targetSlot].Stack.Quantity -= quantity;
            }

            CurrentWeightKg -= itemWeight;
            CurrentWeightKg = Mathf.Max(0, CurrentWeightKg);
            return true;
        }

        /// <summary>
        /// Get total count of item in inventory
        /// </summary>
        public int GetItemCount(int itemId)
        {
            int total = 0;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty && Slots[i].Stack.ItemId == itemId)
                {
                    total += Slots[i].Stack.Quantity;
                }
            }
            return total;
        }

        /// <summary>
        /// Check if inventory has item
        /// </summary>
        public bool HasItem(int itemId, int quantity = 1)
        {
            return GetItemCount(itemId) >= quantity;
        }

        /// <summary>
        /// Find first empty slot
        /// </summary>
        public int FindEmptySlot()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].IsEmpty) return i;
            }
            return -1;
        }

        /// <summary>
        /// Find slot containing item
        /// </summary>
        public int FindSlotWithItem(int itemId)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsEmpty && Slots[i].Stack.ItemId == itemId)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}