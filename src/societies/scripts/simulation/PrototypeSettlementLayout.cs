using Godot;

namespace Societies.Simulation
{
    /// <summary>
    /// Shared spatial layout helpers for the prototype settlement center.
    /// </summary>
    public static class PrototypeSettlementLayout
    {
        private static readonly Vector3 StockpileOffset = new(-1.9f, 0.0f, 0.85f);
        private static readonly Vector3 WorkstationOffset = new(2.3f, 0.0f, 1.15f);
        private static readonly Vector3 CampfireOffset = new(0.0f, 0.0f, 0.85f);
        private const float WorkerHomeRadius = 3.35f;

        public static Vector3 GetStockpileWorldPosition(Vector3 settlementAnchorPosition)
        {
            return settlementAnchorPosition + StockpileOffset;
        }

        public static Vector3 GetWorkstationWorldPosition(Vector3 settlementAnchorPosition)
        {
            return settlementAnchorPosition + WorkstationOffset;
        }

        public static Vector3 GetCampfireWorldPosition(Vector3 settlementAnchorPosition)
        {
            return settlementAnchorPosition + CampfireOffset;
        }

        public static Vector3 GetWorkerHomeWorldPosition(Vector3 settlementAnchorPosition, int workerIndex, int workerCount)
        {
            int safeWorkerCount = Mathf.Max(workerCount, 1);
            float angle = (Mathf.Tau * workerIndex / safeWorkerCount) - (Mathf.Pi * 0.5f);
            return settlementAnchorPosition + new Vector3(
                Mathf.Cos(angle) * WorkerHomeRadius,
                0.0f,
                Mathf.Sin(angle) * WorkerHomeRadius);
        }

        public static string GetResourceTargetLabel(string resourceId)
        {
            return resourceId switch
            {
                "wood" => "Tree",
                "logs" => "Tree",
                "stone" => "Rock",
                "berry" => "Berry Bush",
                "berries" => "Berry Bush",
                "clay" => "Clay Deposit",
                "reeds" => "Reed Bed",
                _ => "Resource"
            };
        }
    }
}
