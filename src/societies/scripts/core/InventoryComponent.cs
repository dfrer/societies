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

        public event Action? Changed;

        public IReadOnlyDictionary<string, int> Items => _items;

        public void AddItem(string itemId, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _items[itemId] = GetCount(itemId) + amount;
            Changed?.Invoke();
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
