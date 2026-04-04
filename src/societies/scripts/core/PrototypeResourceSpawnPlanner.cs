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
                    unitsRemaining,
                    string.Empty));
            }

            return plan;
        }

        public static PrototypeResourceSpawn CreateStarterSpawn(string resourceId, Vector3 settlementAnchorPosition)
        {
            Vector3 offset = resourceId switch
            {
                "wood" => new Vector3(9.0f, 0.0f, -4.5f),
                "stone" => new Vector3(-8.0f, 0.0f, 5.5f),
                "berry" => new Vector3(6.5f, 0.0f, 8.0f),
                _ => new Vector3(10.0f, 0.0f, 0.0f)
            };

            return new PrototypeResourceSpawn(
                resourceId,
                settlementAnchorPosition + offset,
                resourceId == "berry" ? 4 : 6,
                string.Empty);
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
        int UnitsRemaining,
        string ClusterId);
}
