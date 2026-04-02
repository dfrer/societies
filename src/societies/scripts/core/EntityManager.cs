using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Societies.Core
{
    /// <summary>
    /// Tracks live world entities for debugging and later networking/persistence work.
    /// </summary>
    public partial class EntityManager : Node
    {
        public static EntityManager? Instance { get; private set; }

        private readonly Dictionary<string, Entity> _entities = new();
        private readonly Dictionary<string, List<Entity>> _entitiesByType = new();

        public int EntityCount => _entities.Count;

        public event Action<Entity>? EntitySpawned;
        public event Action<Entity>? EntityDestroyed;

        public override void _Ready()
        {
            Instance = this;
        }

        public void RegisterEntity(Entity entity)
        {
            if (_entities.ContainsKey(entity.EntityId))
            {
                return;
            }

            _entities[entity.EntityId] = entity;

            if (!_entitiesByType.TryGetValue(entity.EntityType, out List<Entity>? typeBucket))
            {
                typeBucket = new List<Entity>();
                _entitiesByType[entity.EntityType] = typeBucket;
            }

            typeBucket.Add(entity);
            EntitySpawned?.Invoke(entity);
        }

        public void UnregisterEntity(Entity entity)
        {
            if (!_entities.Remove(entity.EntityId))
            {
                return;
            }

            if (_entitiesByType.TryGetValue(entity.EntityType, out List<Entity>? typeBucket))
            {
                typeBucket.Remove(entity);
                if (typeBucket.Count == 0)
                {
                    _entitiesByType.Remove(entity.EntityType);
                }
            }

            EntityDestroyed?.Invoke(entity);
        }

        public Entity? GetEntity(string entityId)
        {
            _entities.TryGetValue(entityId, out Entity? entity);
            return entity;
        }

        public IReadOnlyList<Entity> GetEntitiesByType(string entityType)
        {
            return _entitiesByType.TryGetValue(entityType, out List<Entity>? entities)
                ? entities.ToList()
                : Array.Empty<Entity>();
        }

        public List<EntityState> GetAllEntityStates()
        {
            return _entities.Values.Select(entity => entity.SerializeState()).ToList();
        }

        public override void _ExitTree()
        {
            Instance = null;
        }
    }
}
