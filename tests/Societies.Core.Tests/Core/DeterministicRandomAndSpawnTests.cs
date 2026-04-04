using Godot;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class DeterministicRandomAndSpawnTests
    {
        [Fact]
        public void DeterministicRandom_SameSeed_ProducesSameSequence()
        {
            DeterministicRandom first = new(1337);
            DeterministicRandom second = new(1337);

            int[] firstValues =
            {
                first.NextIntInclusive(1, 10),
                first.NextIntInclusive(1, 10),
                first.NextIntInclusive(1, 10)
            };
            int[] secondValues =
            {
                second.NextIntInclusive(1, 10),
                second.NextIntInclusive(1, 10),
                second.NextIntInclusive(1, 10)
            };

            Assert.Equal(firstValues, secondValues);
        }

        [Fact]
        public void ResourceSpawnPlanner_SameSeed_ProducesStablePlan()
        {
            PrototypeSpawnBounds bounds = new(250.0f, 0.0f);
            DeterministicRandom firstRandom = new(9001);
            DeterministicRandom secondRandom = new(9001);

            var firstPlan = PrototypeResourceSpawnPlanner.CreatePlan("wood", 5, bounds, firstRandom);
            var secondPlan = PrototypeResourceSpawnPlanner.CreatePlan("wood", 5, bounds, secondRandom);

            Assert.Equal(firstPlan, secondPlan);
        }

        [Fact]
        public void ResourceSpawnPlanner_PointsStayWithinBoundsAndOutsideSpawnRadius()
        {
            PrototypeSpawnBounds bounds = new(250.0f, 2.0f);
            DeterministicRandom random = new(42);

            var plan = PrototypeResourceSpawnPlanner.CreatePlan("berry", 25, bounds, random);

            Assert.All(plan, spawn =>
            {
                Assert.InRange(spawn.Position.X, -226.0f, 226.0f);
                Assert.InRange(spawn.Position.Z, -226.0f, 226.0f);
                Assert.Equal(2.0f, spawn.Position.Y);
                Assert.True(new Vector2(spawn.Position.X, spawn.Position.Z).Length() >= 18.0f);
                Assert.InRange(spawn.UnitsRemaining, 3, 5);
            });
            Assert.Equal(25, plan.Count);
            Assert.True(plan.Select(spawn => spawn.Position).Distinct().Count() > 10);
        }
    }
}
