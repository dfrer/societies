using Godot;
using System.Collections.Generic;

namespace Societies.Core
{
    /// <summary>
    /// Deterministic resource spawn planning for Prototype 1.
    /// </summary>
    public static class PrototypeResourceSpawnPlanner
    {
        public static List<PrototypeResourceSpawn> CreatePlan(
            string resourceId,
            int count,
            PrototypeSpawnBounds bounds,
            DeterministicRandom rng)
        {
            List<PrototypeResourceSpawn> plan = new(count);

            for (int i = 0; i < count; i++)
            {
                int unitsRemaining = resourceId == "berry"
                    ? rng.NextIntInclusive(3, 5)
                    : rng.NextIntInclusive(4, 7);

                plan.Add(new PrototypeResourceSpawn(
                    resourceId,
                    GetRandomPoint(bounds, rng),
                    unitsRemaining));
            }

            return plan;
        }

        public static Vector3 GetRandomPoint(PrototypeSpawnBounds bounds, DeterministicRandom rng)
        {
            while (true)
            {
                float x = rng.NextFloat(-bounds.WorldHalfSize + bounds.SafeMargin, bounds.WorldHalfSize - bounds.SafeMargin);
                float z = rng.NextFloat(-bounds.WorldHalfSize + bounds.SafeMargin, bounds.WorldHalfSize - bounds.SafeMargin);
                Vector2 flat = new(x, z);

                if (flat.Length() >= bounds.MinDistanceFromSpawn)
                {
                    return new Vector3(x, bounds.GroundHeight, z);
                }
            }
        }
    }

    public readonly record struct PrototypeSpawnBounds(
        float WorldHalfSize,
        float GroundHeight,
        float SafeMargin = 24.0f,
        float MinDistanceFromSpawn = 18.0f);

    public readonly record struct PrototypeResourceSpawn(
        string ResourceId,
        Vector3 Position,
        int UnitsRemaining);
}
