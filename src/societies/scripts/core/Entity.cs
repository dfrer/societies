using Godot;
using System;
using System.Collections.Generic;

namespace Societies.Core
{
    /// <summary>
    /// Base class for all entities in the game world.
    /// Provides common functionality for synchronization, state management, and lifecycle.
    /// </summary>
    public partial class Entity : CharacterBody3D
    {
        [Export] public string EntityId { get; set; } = "";
        [Export] public string EntityType { get; set; } = "generic";
        [Export] public string DisplayName { get; set; } = "Entity";
        
        // Network sync
        private Vector3 _syncPosition;
        private Vector3 _syncRotation;
        private Vector3 _syncVelocity;
        
        // State tracking
        public bool IsSpawned { get; private set; }
        public DateTime SpawnTime { get; private set; }
        
        // Components
        private Dictionary<string, Node> _components = new();
        
        public override void _Ready()
        {
            if (string.IsNullOrEmpty(EntityId))
            {
                EntityId = Guid.NewGuid().ToString();
            }
            
            SpawnTime = DateTime.UtcNow;
            IsSpawned = true;
            
            // Register with entity manager
            EntityManager.Instance?.RegisterEntity(this);
        }
        
        /// <summary>
        /// Serialize entity state for network transmission
        /// </summary>
        public virtual EntityState SerializeState()
        {
            return new EntityState
            {
                EntityId = EntityId,
                EntityType = EntityType,
                Position = GlobalPosition,
                Rotation = GlobalRotation,
                Velocity = Velocity,
                Timestamp = DateTime.UtcNow.Ticks
            };
        }
        
        /// <summary>
        /// Deserialize and apply entity state
        /// </summary>
        public virtual void DeserializeState(EntityState state)
        {
            if (state.EntityId != EntityId)
            {
                GD.PrintErr($"Entity ID mismatch: {state.EntityId} vs {EntityId}");
                return;
            }
            
            // Only apply if we're not the authority
            if (!IsMultiplayerAuthority())
            {
                _syncPosition = state.Position;
                _syncRotation = state.Rotation;
                _syncVelocity = state.Velocity;
                
                // Interpolate to target position
                GlobalPosition = GlobalPosition.Lerp(_syncPosition, 0.3f);
                GlobalRotation = GlobalRotation.Lerp(_syncRotation, 0.3f);
                Velocity = _syncVelocity;
            }
        }
        
        /// <summary>
        /// Called when entity is spawned in the world
        /// </summary>
        public virtual void OnSpawn()
        {
            GD.Print($"Entity spawned: {DisplayName} ({EntityId})");
        }
        
        /// <summary>
        /// Called when entity is being destroyed
        /// </summary>
        public virtual void OnDestroy()
        {
            GD.Print($"Entity destroyed: {DisplayName} ({EntityId})");
            EntityManager.Instance?.UnregisterEntity(this);
        }
        
        /// <summary>
        /// Add a component to this entity
        /// </summary>
        public void AddComponent(string name, Node component)
        {
            _components[name] = component;
            AddChild(component);
        }
        
        /// <summary>
        /// Get a component by name
        /// </summary>
        public T? GetComponent<T>(string name) where T : Node
        {
            if (_components.TryGetValue(name, out Node? component))
            {
                return component as T;
            }
            return null;
        }
        
        /// <summary>
        /// Check if this peer has authority over this entity.
        /// In single-player, always returns true.
        /// In multiplayer, checks if this peer owns the entity.
        /// </summary>
        protected new bool IsMultiplayerAuthority()
        {
            if (!Multiplayer.HasMultiplayerPeer())
                return true; // Single player - local authority

            // Use the base Node implementation which checks GetMultiplayerAuthority() == Multiplayer.GetUniqueId()
            return base.IsMultiplayerAuthority();
        }
        
        public override void _ExitTree()
        {
            OnDestroy();
        }
    }
    
    /// <summary>
    /// Serializable entity state for network sync
    /// </summary>
    public struct EntityState
    {
        public string EntityId;
        public string EntityType;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Velocity;
        public long Timestamp;
        public Dictionary<string, object> CustomData;
    }
}
