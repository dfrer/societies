using System.Collections.Generic;
using UnityEngine;

namespace Societies.Runtime.Economy
{
    public class EconomySystem : MonoBehaviour
    {
        public static EconomySystem Instance { get; private set; }

        public struct Listing
        {
            public string SellerId;
            public int ItemId;
            public int Quantity;
            public int PricePerUnit;
        }

        private readonly List<Listing> _listings = new();

        private void Awake()
        {
            Instance = this;
        }

        public void CreateListing(string playerId, int itemId, int quantity, int price)
        {
            _listings.Add(new Listing
            {
                SellerId = playerId,
                ItemId = itemId,
                Quantity = quantity,
                PricePerUnit = price
            });
        }

        public bool BuyItem(string buyerId, int itemId, int quantity)
        {
            foreach (var listing in _listings)
            {
                if (listing.ItemId == itemId && listing.Quantity >= quantity)
                {
                    _listings.Remove(listing);
                    return true;
                }
            }

            return false;
        }

        public List<Listing> GetListings() => _listings;
    }
}
