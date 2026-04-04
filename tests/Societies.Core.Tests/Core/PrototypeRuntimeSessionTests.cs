using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeRuntimeSessionTests
    {
        [Fact]
        public void BalancedBasin_ReachesEconomyStateWithMealsFuelAndBeds()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession session = new(scenario, bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);

            List<PrototypeResourceSiteState> resources = session.World!.ResourceSpawns
                .GroupBy(spawn => spawn.ResourceId)
                .OrderBy(group => group.Key)
                .SelectMany(group => group.Select((spawn, index) => new PrototypeResourceSiteState(
                    $"{spawn.ResourceId}_{index + 1}",
                    spawn.ResourceId,
                    spawn.Position,
                    spawn.UnitsRemaining,
                    spawn.ClusterId)))
                .ToList();

            for (int tick = 0; tick < 1400; tick++)
            {
                PrototypeRuntimeTickResult result = session.Advance(1.0f / 20.0f, 600.0f, resources);
                foreach (PrototypeHarvestRequest request in result.SettlementResult.HarvestRequests)
                {
                    int index = resources.FindIndex(site => site.NodeName == request.TargetNodeName);
                    Assert.True(index >= 0, $"Missing resource node {request.TargetNodeName}");

                    PrototypeResourceSiteState site = resources[index];
                    if (site.UnitsRemaining < request.Amount)
                    {
                        session.OnHarvestFailed(request.WorkerId, request.WorkerDisplayName, request.ResourceId);
                        continue;
                    }

                    resources[index] = site with
                    {
                        UnitsRemaining = site.UnitsRemaining - request.Amount
                    };
                    session.RecordAiHarvestSucceeded(request.WorkerDisplayName, request.ResourceId, request.Amount);
                }

                session.RecordSettlementEvents(result.SettlementResult.Events);
            }

            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero, resources.Select(resource => new PrototypeResourceSnapshot
            {
                ResourceId = resource.ResourceId,
                UnitsRemaining = resource.UnitsRemaining,
                Position = PrototypeSerializableVector3.FromVector3(resource.Position),
                ClusterId = resource.ClusterId
            }).ToList());

            Assert.True(
                session.Stockpile.GetCount("meals") > 0 ||
                session.Stockpile.GetCount("berries") > 0 ||
                snapshot.Settlement!.ProducedResources.GetValueOrDefault("meals") > 0);
            Assert.True(
                session.Stockpile.GetCount("firewood") > 0 ||
                session.Stockpile.GetCount("hearth_fuel") > 0 ||
                snapshot.Settlement!.ProducedResources.GetValueOrDefault("firewood") > 0 ||
                snapshot.Settlement.HearthLitTicks > 0);
            Assert.True(session.Stockpile.GetCount("beds") >= 2 || snapshot.Settlement!.Structures.Any(structure => structure.StructureKindId == "hut" && structure.IsBuilt));
            Assert.NotNull(snapshot.Settlement);
            Assert.True(snapshot.Settlement!.Citizens.Count == session.Workers.Count);
            Assert.True(snapshot.Settlement.PathSegments.Count > 0);
            Assert.True(snapshot.Settlement.LogisticsMetrics.TotalCompletedRouteDistanceMeters >= 0.0f);
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            return PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());
        }

        private static string GetCatalogDirectoryPath()
        {
            string baseDirectory = AppContext.BaseDirectory;
            string? current = baseDirectory;

            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                DirectoryInfo? parent = Directory.GetParent(current);
                current = parent?.FullName;
            }

            throw new DirectoryNotFoundException($"Could not find src/societies/data from '{baseDirectory}'.");
        }
    }
}
