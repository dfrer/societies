using Godot;
using System;
using System.Collections.Generic;

namespace Societies.Core
{
    /// <summary>
    /// Base world entity for prototype simulation objects.
    /// </summary>
    public partial class Entity : Node3D
    {
        [Export] public string EntityId { get; set; } = string.Empty;
        [Export] public string EntityType { get; set; } = "generic";
        [Export] public string DisplayName { get; set; } = "Entity";

        public DateTime SpawnTime { get; private set; }
        public bool IsRegistered { get; private set; }

        public override void _Ready()
        {
            if (string.IsNullOrWhiteSpace(EntityId))
            {
                EntityId = Guid.NewGuid().ToString("N");
            }

            SpawnTime = DateTime.UtcNow;
            EntityManager.Instance?.RegisterEntity(this);
            IsRegistered = true;
        }

        public virtual EntityState SerializeState()
        {
            return new EntityState
            {
                EntityId = EntityId,
                EntityType = EntityType,
                Position = GlobalPosition,
                Rotation = GlobalRotation,
                Velocity = Vector3.Zero,
                Timestamp = DateTime.UtcNow.Ticks,
                CustomData = new Dictionary<string, object>()
            };
        }

        public virtual void DeserializeState(EntityState state)
        {
            GlobalPosition = state.Position;
            GlobalRotation = state.Rotation;
        }

        public override void _ExitTree()
        {
            if (IsRegistered)
            {
                EntityManager.Instance?.UnregisterEntity(this);
                IsRegistered = false;
            }
        }
    }

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
