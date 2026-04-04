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
        public void BalancedBasin_CraftsCampfireWithin320Ticks()
        {
            PrototypeScenarioDefinition scenario = LoadCatalogs().Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession session = new(scenario);
            session.Initialize(8.0f);

            List<PrototypeResourceSiteState> resources = session.World!.ResourceSpawns
                .GroupBy(spawn => spawn.ResourceId)
                .OrderBy(group => group.Key)
                .SelectMany(group => group
                    .Select((spawn, index) => new PrototypeResourceSiteState(
                        $"{spawn.ResourceId}_{index + 1}",
                        spawn.ResourceId,
                        spawn.Position,
                        spawn.UnitsRemaining)))
                .ToList();

            for (int tick = 0; tick < 320; tick++)
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

            string workerSummary = string.Join(
                "; ",
                session.Workers.Select(worker => $"{worker.DisplayName}:{worker.Phase}:{worker.CarryItemId}x{worker.CarryAmount}:{worker.TargetLabel}"));
            Assert.True(
                session.Stockpile.GetCount("campfire") == 1,
                $"Expected one campfire by tick 320 but stockpile was wood={session.Stockpile.GetCount("wood")}, stone={session.Stockpile.GetCount("stone")}, berry={session.Stockpile.GetCount("berry")}, campfire={session.Stockpile.GetCount("campfire")}. Workers={workerSummary}");
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
