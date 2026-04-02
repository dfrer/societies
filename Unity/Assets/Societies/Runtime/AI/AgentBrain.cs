using UnityEngine;
using Societies.Runtime.Inventory;

namespace Societies.Runtime.AI
{
    public class AgentBrain : MonoBehaviour
    {
        // Needs (0-100)
        public float Hunger { get; set; }
        public float Energy { get; set; }
        public float Comfort { get; set; }

        // State
        public Vector3 HomePosition { get; set; }
        public InventoryManager Inventory { get; private set; }

        // Current goal
        public enum Goal
        {
            Idle,
            GatherFood,
            GatherWood,
            GatherStone,
            GoHome,
            Rest
        }

        public Goal CurrentGoal { get; private set; }

        private void Awake()
        {
            Inventory = GetComponent<InventoryManager>();
            if (Inventory == null)
            {
                Inventory = gameObject.AddComponent<InventoryManager>();
            }

            Hunger = 50f;
            Energy = 80f;
            Comfort = 50f;
        }

        private void Update()
        {
            // Decrease needs over time
            Hunger += Time.deltaTime * 0.5f;
            Energy -= Time.deltaTime * 0.3f;

            // Pick goal based on needs
            if (Hunger > 70f)
            {
                CurrentGoal = Goal.GatherFood;
            }
            else if (Energy < 20f)
            {
                CurrentGoal = Goal.Rest;
            }
            else if (Inventory.CurrentWeight / Inventory.Capacity > 0.8f)
            {
                CurrentGoal = Goal.GoHome;
            }
            else
            {
                CurrentGoal = Goal.GatherWood;
            }

            // Execute goal (simplified for MVP - just move around)
            ExecuteGoal();
        }

        private void ExecuteGoal()
        {
            // MVP: Just wander or go home
        }
    }
}
