using Godot;
using System;
using System.Collections.Generic;
using Xunit;

namespace Societies.Core.Tests
{
    /// <summary>
    /// Tests for EntityState serialization and core entity logic.
    /// These tests validate the pure C# logic without requiring Godot runtime.
    /// </summary>
    public class EntityStateTests
    {
        [Fact]
        public void EntityState_DefaultValues_AreInitialized()
        {
            // Arrange & Act
            var state = new EntityState();

            // Assert
            Assert.Null(state.EntityId);
            Assert.Null(state.EntityType);
            Assert.Equal(Vector3.Zero, state.Position);
            Assert.Equal(Vector3.Zero, state.Rotation);
            Assert.Equal(Vector3.Zero, state.Velocity);
            Assert.Equal(0L, state.Timestamp);
            Assert.Null(state.CustomData);
        }

        [Fact]
        public void EntityState_SetValues_StoresCorrectly()
        {
            // Arrange
            var state = new EntityState
            {
                EntityId = "test-entity-123",
                EntityType = "player",
                Position = new Vector3(10, 5, 20),
                Rotation = new Vector3(0, 90, 0),
                Velocity = new Vector3(1, 0, 1),
                Timestamp = DateTime.UtcNow.Ticks,
                CustomData = new Dictionary<string, object> { { "health", 100 } }
            };

            // Assert
            Assert.Equal("test-entity-123", state.EntityId);
            Assert.Equal("player", state.EntityType);
            Assert.Equal(new Vector3(10, 5, 20), state.Position);
            Assert.Equal(new Vector3(0, 90, 0), state.Rotation);
            Assert.Equal(new Vector3(1, 0, 1), state.Velocity);
            Assert.True(state.Timestamp > 0);
            Assert.NotNull(state.CustomData);
            Assert.Equal(100, state.CustomData["health"]);
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(100, 50, 200)]
        [InlineData(-50, 0, -100)]
        [InlineData(0.5f, 1.5f, 2.5f)]
        public void EntityState_Position_CanBeAnyVector(float x, float y, float z)
        {
            // Arrange
            var state = new EntityState();
            var position = new Vector3(x, y, z);

            // Act
            state.Position = position;

            // Assert
            Assert.Equal(position, state.Position);
        }

        [Fact]
        public void EntityState_Timestamp_IsAccurate()
        {
            // Arrange
            var before = DateTime.UtcNow.Ticks;

            // Act
            var state = new EntityState { Timestamp = DateTime.UtcNow.Ticks };
            var after = DateTime.UtcNow.Ticks;

            // Assert
            Assert.True(state.Timestamp >= before, "Timestamp should be after or equal to 'before'");
            Assert.True(state.Timestamp <= after, "Timestamp should be before or equal to 'after'");
        }

        [Fact]
        public void EntityState_CustomData_CanStoreVariousTypes()
        {
            // Arrange
            var state = new EntityState
            {
                CustomData = new Dictionary<string, object>
                {
                    { "health", 100 },
                    { "name", "Test Entity" },
                    { "isActive", true },
                    { "position", new Vector3(10, 0, 10) }
                }
            };

            // Assert
            Assert.IsType<int>(state.CustomData["health"]);
            Assert.IsType<string>(state.CustomData["name"]);
            Assert.IsType<bool>(state.CustomData["isActive"]);
            Assert.IsType<Vector3>(state.CustomData["position"]);
        }

        [Fact]
        public void EntityState_Equality_SameValuesAreEqual()
        {
            // Arrange
            var state1 = new EntityState
            {
                EntityId = "entity-1",
                EntityType = "player",
                Position = new Vector3(10, 0, 10)
            };

            var state2 = new EntityState
            {
                EntityId = "entity-1",
                EntityType = "player",
                Position = new Vector3(10, 0, 10)
            };

            // Act & Assert
            Assert.Equal(state1.EntityId, state2.EntityId);
            Assert.Equal(state1.EntityType, state2.EntityType);
            Assert.Equal(state1.Position, state2.Position);
        }

        [Fact]
        public void EntityState_Equality_DifferentValuesAreNotEqual()
        {
            // Arrange
            var state1 = new EntityState
            {
                EntityId = "entity-1",
                Position = new Vector3(10, 0, 10)
            };

            var state2 = new EntityState
            {
                EntityId = "entity-2",
                Position = new Vector3(20, 0, 20)
            };

            // Act & Assert
            Assert.NotEqual(state1.EntityId, state2.EntityId);
            Assert.NotEqual(state1.Position, state2.Position);
        }
    }
}
