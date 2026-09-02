using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Societies.Simulation;

namespace Societies.Core
{
    /// <summary>
    /// Enforces the schema-v10/v11 shell shared by public persistence, artifact preflight,
    /// and runtime restore. Voxel authority cannot carry ignored heightfield state.
    /// </summary>
    internal static class PrototypeVoxelSnapshotValidator
    {
        internal static void ValidateCanonicalShell(PrototypeRuntimeSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            PrototypeSettlementSnapshot? settlement = snapshot.Settlement;
            if (snapshot.Inventory == null || snapshot.Stockpile == null || snapshot.Resources == null ||
                snapshot.Workers == null || snapshot.ContributionCountsByResource == null || snapshot.Directive == null ||
                snapshot.Telemetry == null || snapshot.CivicPolicy == null || snapshot.Wetland == null ||
                settlement?.CentralDepot == null || settlement.CentralDepot.Items == null || settlement.SiteCaches == null ||
                settlement.Structures == null || settlement.Citizens == null || settlement.PathSegments == null ||
                settlement.RemoteDepots == null || settlement.RouteHeatCells == null || settlement.BuildQueue == null ||
                settlement.ProducedResources == null || settlement.ConsumedResources == null ||
                settlement.BlockedReasonCounts == null || settlement.StructureCompletionTicks == null ||
                settlement.LogisticsMetrics == null || settlement.LogisticsMetrics.DepotThroughputByDepot == null ||
                settlement.LogisticsMetrics.RouteBacklogTicksByKind == null)
            {
                throw new InvalidDataException("Voxel runtime snapshot shell contains a null heightfield-only state object.");
            }

            PrototypeLogisticsMetricsState logistics = settlement.LogisticsMetrics;
            PrototypeSerializableVector3 depotPosition = settlement.CentralDepot.Position;
            long previousEventTick = -1;
            foreach (VoxelChangeEvent voxelEvent in snapshot.VoxelWorld?.Events ??
                throw new InvalidDataException("Voxel runtime snapshot shell is missing its edit history."))
            {
                if (voxelEvent == null || voxelEvent.Tick < previousEventTick || voxelEvent.Tick > snapshot.SimulationTick)
                {
                    throw new InvalidDataException(
                        "Voxel runtime snapshot edit ticks must be nondecreasing and no later than the simulation tick.");
                }
                previousEventTick = voxelEvent.Tick;
            }

            bool isWorldcraft = snapshot.SchemaVersion >= 11;
            if (isWorldcraft)
            {
                ValidateWorldcraftInventory(snapshot.Inventory);
                if (snapshot.Construction == null)
                {
                    throw new InvalidDataException("Schema-v11 voxel runtime snapshot is missing construction state.");
                }
            }

            if (snapshot.SchemaVersion == 12)
            {
                PrototypeCausewayState.ValidateSnapshot(snapshot.Causeway ?? throw new InvalidDataException("Schema-v12 causeway state is missing."));
            }
            else if (snapshot.Causeway != null)
            {
                throw new InvalidDataException("Pre-v12 voxel snapshot cannot carry causeway state.");
            }

            if ((!isWorldcraft && snapshot.Inventory.Count != 0) || snapshot.Stockpile.Count != 0 || snapshot.Resources.Count != 0 ||
                snapshot.Workers.Count != 0 || snapshot.ContributionCountsByResource.Count != 0 || snapshot.Crisis != null ||
                !string.Equals(snapshot.Directive.DirectiveId, "neutral", StringComparison.Ordinal) ||
                settlement.Citizens.Count != 0 || settlement.SiteCaches.Count != 0 ||
                settlement.Structures.Count != 0 || settlement.PathSegments.Count != 0 ||
                settlement.RemoteDepots.Count != 0 || settlement.RouteHeatCells.Count != 0 ||
                settlement.BuildQueue.Count != 0 || settlement.ProducedResources.Count != 0 ||
                settlement.ConsumedResources.Count != 0 || settlement.BlockedReasonCounts.Count != 0 ||
                settlement.StructureCompletionTicks.Count != 0 || settlement.SelectedBuildQueueIndex != 0 ||
                settlement.HearthLitTicks != 0 || settlement.TotalTicks != snapshot.SimulationTick ||
                settlement.NavigationRulesVersion != 1 || !string.IsNullOrEmpty(settlement.Classification) ||
                !string.IsNullOrEmpty(settlement.CentralDepot.StoreId) ||
                !string.IsNullOrEmpty(settlement.CentralDepot.DisplayName) || settlement.CentralDepot.Capacity != 0 ||
                depotPosition.X != 0.0f || depotPosition.Y != 0.0f || depotPosition.Z != 0.0f ||
                !string.IsNullOrEmpty(settlement.CentralDepot.LinkedClusterId) || settlement.CentralDepot.Items.Count != 0 ||
                logistics.CompletedRouteCount != 0 || logistics.TotalCompletedRouteDistanceMeters != 0.0f ||
                logistics.TotalCompletedRouteTicks != 0 || logistics.TravelTicksAccumulated != 0 ||
                logistics.WorkTicksAccumulated != 0 || logistics.PathCoverageRatio != 0.0f ||
                logistics.DepotThroughputByDepot.Count != 0 || logistics.RouteBacklogTicksByKind.Count != 0 ||
                !string.Equals(JsonSerializer.Serialize(snapshot.Telemetry), JsonSerializer.Serialize(new PrototypeRuntimeTelemetrySnapshot()), StringComparison.Ordinal) ||
                !string.Equals(JsonSerializer.Serialize(snapshot.CivicPolicy), JsonSerializer.Serialize(new PrototypeCivicPolicySnapshot()), StringComparison.Ordinal) ||
                !string.Equals(JsonSerializer.Serialize(snapshot.Wetland), JsonSerializer.Serialize(new PrototypeWetlandSnapshot()), StringComparison.Ordinal))
            {
                throw new InvalidDataException("Voxel runtime snapshots cannot carry heightfield settlement or resource state.");
            }
        }

        private static void ValidateWorldcraftInventory(IReadOnlyDictionary<string, int> inventory)
        {
            long slots = 0;
            foreach ((string itemId, int amount) in inventory)
            {
                if (!VoxelWorldcraftCatalog.IsKnownItem(itemId) || amount <= 0)
                {
                    throw new InvalidDataException("Schema-v11 voxel inventory contains an unknown item or non-positive stack.");
                }
                slots += (amount + (long)VoxelWorldcraftCatalog.StackLimit - 1) / VoxelWorldcraftCatalog.StackLimit;
                if (slots > VoxelWorldcraftCatalog.HotbarSlots)
                {
                    throw new InvalidDataException("Schema-v11 voxel inventory exceeds its bounded hotbar capacity.");
                }
            }
        }
    }
}
