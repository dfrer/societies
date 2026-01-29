using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Core
{
    /// <summary>
    /// Manages all entities in the world. Handles spawning, tracking, and cleanup.
    /// Singleton pattern for global access.
    /// </summary>
    public partial class EntityManager : Node
    {
        public static EntityManager Instance { get; private set; }
        
        // Entity tracking
        private Dictionary<string, Entity> _entities = new();
        private Dictionary<string, List<Entity>> _entitiesByType = new();
        
        // Performance tracking
        public int EntityCount => _entities.Count;
        public int MaxEntities { get; set; } = 5000;
        
        // Events
        public event Action<Entity>? EntitySpawned;
        public event Action<Entity>? EntityDestroyed;
        
        public override void _Ready()
        {
            Instance = this;
            GD.Print("EntityManager initialized");
        }
        
        /// <summary>
        /// Register an entity with the manager
        /// </summary>
        public void RegisterEntity(Entity entity)
        {
            if (_entities.ContainsKey(entity.EntityId))
            {
                GD.PrintErr($"Entity {entity.EntityId} already registered");
                return;
            }
            
            _entities[entity.EntityId] = entity;
            
            // Track by type
            if (!_entitiesByType.ContainsKey(entity.EntityType))
            {
                _entitiesByType[entity.EntityType] = new List<Entity>();
            }
            _entitiesByType[entity.EntityType].Add(entity);
            
            EntitySpawned?.Invoke(entity);
            
            if (_entities.Count % 100 == 0)
            {
                GD.Print($"Entity count: {_entities.Count}");
            }
        }
        
        /// <summary>
        /// Unregister an entity
        /// </summary>
        public void UnregisterEntity(Entity entity)
        {
            if (!_entities.ContainsKey(entity.EntityId))
                return;
            
            _entities.Remove(entity.EntityId);
            
            // Remove from type tracking
            if (_entitiesByType.TryGetValue(entity.EntityType, out var typeList))
            {
                typeList.Remove(entity);
            }
            
            EntityDestroyed?.Invoke(entity);
        }
        
        /// <summary>
        /// Get entity by ID
        /// </summary>
        public Entity? GetEntity(string entityId)
        {
            _entities.TryGetValue(entityId, out var entity);
            return entity;
        }
        
        /// <summary>
        /// Get all entities of a specific type
        /// </summary>
        public List<Entity> GetEntitiesByType(string entityType)
        {
            if (_entitiesByType.TryGetValue(entityType, out var list))
            {
                return list.ToList();
            }
            return new List<Entity>();
        }
        
        /// <summary>
        /// Get entities within a radius of a position
        /// </summary>
        public List<Entity> GetEntitiesInRadius(Vector3 position, float radius)
        {
            var result = new List<Entity>();
            float radiusSquared = radius * radius;
            
            foreach (var entity in _entities.Values)
            {
                if (entity.GlobalPosition.DistanceSquaredTo(position) <= radiusSquared)
                {
                    result.Add(entity);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Spawn a new entity from a scene
        /// </summary>
        public T? SpawnEntity<T>(PackedScene scene, Vector3 position, string? customId = null) where T : Entity
        {
            if (_entities.Count >= MaxEntities)
            {
                GD.PrintErr("Max entity count reached");
                return null;
            }
            
            var instance = scene.Instantiate<T>();
            if (instance == null)
            {
                GD.PrintErr("Failed to instantiate entity");
                return null;
            }
            
            if (!string.IsNullOrEmpty(customId))
            {
                instance.EntityId = customId;
            }
            
            instance.GlobalPosition = position;
            AddChild(instance);
            
            instance.OnSpawn();
            
            return instance;
        }
        
        /// <summary>
        /// Destroy an entity
        /// </summary>
        public void DestroyEntity(string entityId)
        {
            var entity = GetEntity(entityId);
            if (entity != null)
            {
                entity.QueueFree();
            }
        }
        
        /// <summary>
        /// Get all entity states for network sync
        /// </summary>
        public List<EntityState> GetAllEntityStates()
        {
            var states = new List<EntityState>();
            
            foreach (var entity in _entities.Values)
            {
                states.Add(entity.SerializeState());
            }
            
            return states;
        }
        
        /// <summary>
        /// Get entity states for a specific player (spatial culling)
        /// </summary>
        public List<EntityState> GetEntityStatesForPlayer(Vector3 playerPosition, float viewDistance)
        {
            var states = new List<EntityState>();
            float viewDistanceSquared = viewDistance * viewDistance;
            
            foreach (var entity in _entities.Values)
            {
                if (entity.GlobalPosition.DistanceSquaredTo(playerPosition) <= viewDistanceSquared)
                {
                    states.Add(entity.SerializeState());
                }
            }
            
            return states;
        }
        
        /// <summary>
        /// Clean up timed out or invalid entities
        /// </summary>
        public void CleanupEntities()
        {
            var toRemove = new List<string>();
            
            foreach (var kvp in _entities)
            {
                if (!IsInstanceValid(kvp.Value))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            
            foreach (var id in toRemove)
            {
                _entities.Remove(id);
                GD.Print($"Cleaned up invalid entity: {id}");
            }
        }
        
        public override void _ExitTree()
        {
            Instance = null;
        }
    }
}
