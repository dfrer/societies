using System;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Core
{
    /// <summary>
    /// Simple inventory storage for the playable prototype.
    /// </summary>
    public sealed class InventoryComponent
    {
        private readonly Dictionary<string, int> _items = new();
        private int? _slotLimit;
        private int _stackLimit = int.MaxValue;
        private HashSet<string>? _allowedItemIds;

        public event Action? Changed;

        public IReadOnlyDictionary<string, int> Items => _items;

        public int SlotLimit => _slotLimit ?? int.MaxValue;

        public int StackLimit => _stackLimit;

        public int UsedSlots => CalculateUsedSlots(_items, _stackLimit);

        /// <summary>Optional bounded mode used by voxel worldcraft; default legacy storage remains unbounded.</summary>
        public void ConfigureBoundedStorage(int slotLimit, int stackLimit, IEnumerable<string> allowedItemIds)
        {
            if (slotLimit <= 0 || stackLimit <= 0 || allowedItemIds == null)
            {
                throw new ArgumentOutOfRangeException(nameof(slotLimit));
            }
            HashSet<string> allowed = allowedItemIds.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
            if (allowed.Count == 0) throw new ArgumentException("At least one item id is required.", nameof(allowedItemIds));
            if (!IsWithinCapacity(_items, slotLimit, stackLimit, allowed))
            {
                throw new InvalidOperationException("Existing inventory exceeds the requested bounded storage.");
            }

            // Commit the policy only after every input and the existing contents validate. A failed
            // reconfiguration must leave both the previous policy and contents untouched.
            _slotLimit = slotLimit;
            _stackLimit = stackLimit;
            _allowedItemIds = allowed;
        }

        /// <summary>Atomically replaces contents and installs a bounded policy for snapshot restore.</summary>
        public void ReplaceContentsAndConfigureBoundedStorage(
            IReadOnlyDictionary<string, int> items,
            int slotLimit,
            int stackLimit,
            IEnumerable<string> allowedItemIds)
        {
            if (items == null || slotLimit <= 0 || stackLimit <= 0 || allowedItemIds == null)
            {
                throw new ArgumentOutOfRangeException(nameof(slotLimit));
            }

            HashSet<string> allowed = allowedItemIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            if (allowed.Count == 0 || !IsWithinCapacity(items, slotLimit, stackLimit, allowed))
            {
                throw new InvalidOperationException("Inventory contents exceed the requested bounded storage.");
            }

            Dictionary<string, int> replacement = items
                .Where(pair => pair.Value > 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            _slotLimit = slotLimit;
            _stackLimit = stackLimit;
            _allowedItemIds = allowed;
            _items.Clear();
            foreach ((string itemId, int amount) in replacement)
            {
                _items.Add(itemId, amount);
            }
            Changed?.Invoke();
        }

        public void AddItem(string itemId, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (_slotLimit.HasValue && !CanAddItem(itemId, amount))
            {
                throw new InvalidOperationException("Bounded inventory cannot accept this item or amount.");
            }

            _items[itemId] = GetCount(itemId) + amount;
            Changed?.Invoke();
        }

        public bool CanAddItem(string itemId, int amount)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(itemId) || (_allowedItemIds != null && !_allowedItemIds.Contains(itemId))) return false;
            Dictionary<string, int> candidate = new(_items, StringComparer.Ordinal) { [itemId] = checked(GetCount(itemId) + amount) };
            return IsWithinConfiguredCapacity(candidate);
        }

        public bool TryAddItem(string itemId, int amount)
        {
            if (!CanAddItem(itemId, amount)) return false;
            _items[itemId] = checked(GetCount(itemId) + amount);
            Changed?.Invoke();
            return true;
        }

        public bool CanAddItems(IReadOnlyDictionary<string, int> additions)
        {
            if (additions == null || additions.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0))
            {
                return false;
            }
            Dictionary<string, int> candidate = new(_items, StringComparer.Ordinal);
            try
            {
                foreach ((string itemId, int amount) in additions)
                {
                    candidate[itemId] = checked((candidate.TryGetValue(itemId, out int current) ? current : 0) + amount);
                }
            }
            catch (OverflowException)
            {
                return false;
            }
            return IsWithinConfiguredCapacity(candidate);
        }

        public bool TryAddItems(IReadOnlyDictionary<string, int> additions)
        {
            if (!CanAddItems(additions)) return false;
            foreach ((string itemId, int amount) in additions)
            {
                _items[itemId] = checked(GetCount(itemId) + amount);
            }
            Changed?.Invoke();
            return true;
        }

        public bool TryRemoveItems(IReadOnlyDictionary<string, int> removals)
        {
            if (removals == null || removals.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0) ||
                !HasItems(removals))
            {
                return false;
            }
            foreach ((string itemId, int amount) in removals)
            {
                int remaining = _items[itemId] - amount;
                if (remaining == 0) _items.Remove(itemId);
                else _items[itemId] = remaining;
            }
            Changed?.Invoke();
            return true;
        }

        public bool RemoveItem(string itemId, int amount)
        {
            if (amount <= 0 || !_items.TryGetValue(itemId, out int current) || current < amount)
            {
                return false;
            }

            int remaining = current - amount;
            if (remaining == 0)
            {
                _items.Remove(itemId);
            }
            else
            {
                _items[itemId] = remaining;
            }

            Changed?.Invoke();
            return true;
        }

        public int GetCount(string itemId)
        {
            return _items.TryGetValue(itemId, out int amount) ? amount : 0;
        }

        public bool HasItems(IReadOnlyDictionary<string, int> cost)
        {
            foreach ((string itemId, int amount) in cost)
            {
                if (GetCount(itemId) < amount)
                {
                    return false;
                }
            }

            return true;
        }

        public void ReplaceContents(IReadOnlyDictionary<string, int> items)
        {
            if (items == null || !IsWithinConfiguredCapacity(items))
            {
                throw new InvalidOperationException("Inventory contents exceed configured capacity or use an unknown item.");
            }
            _items.Clear();

            foreach ((string itemId, int amount) in items)
            {
                if (amount > 0)
                {
                    _items[itemId] = amount;
                }
            }

            Changed?.Invoke();
        }

        private bool IsWithinConfiguredCapacity(IReadOnlyDictionary<string, int> items)
        {
            return IsWithinCapacity(_items == items ? _items : items, _slotLimit, _stackLimit, _allowedItemIds);
        }

        private static bool IsWithinCapacity(
            IReadOnlyDictionary<string, int> items,
            int? slotLimit,
            int stackLimit,
            IReadOnlySet<string>? allowedItemIds)
        {
            long slots = 0;
            foreach ((string itemId, int amount) in items)
            {
                if (string.IsNullOrWhiteSpace(itemId) || amount < 0 ||
                    (allowedItemIds != null && !allowedItemIds.Contains(itemId)))
                {
                    return false;
                }
                slots += (amount + (long)stackLimit - 1) / stackLimit;
                if (slotLimit.HasValue && slots > slotLimit.Value) return false;
            }
            return true;
        }

        private static int CalculateUsedSlots(IReadOnlyDictionary<string, int> items, int stackLimit)
        {
            long slots = items.Values.Where(amount => amount > 0)
                .Sum(amount => (amount + (long)stackLimit - 1) / stackLimit);
            return checked((int)slots);
        }

        public string GetSummaryText()
        {
            if (_items.Count == 0)
            {
                return "Inventory\nEmpty";
            }

            List<string> lines = new() { "Inventory" };
            foreach ((string itemId, int amount) in _items.OrderBy(pair => pair.Key))
            {
                lines.Add($"{FormatItemName(itemId)}: {amount}");
            }

            return string.Join('\n', lines);
        }

        public static string FormatItemName(string itemId)
        {
            return itemId.Replace('_', ' ');
        }
    }
}
