using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Societies.Core.Tests
{
    public class SettlementWorkloadCharacterization
    {
        private class RunRec
        {
            public string Sc; public int W; public int D;
            public int Peak; public double AG, AC, AR, AL, AH; public double HR; public int ACit;
        }

        [Fact]
        public void Characterize_SettlementWorkloadAcrossSizes()
        {
            var bundle = LoadCatalogs();
            var results = new List<RunRec>();

            var configs = new[] {
                ("balanced_basin", 3, 60), ("balanced_basin", 3, 300), ("balanced_basin", 3, 1200),
                ("balanced_basin", 6, 60), ("balanced_basin", 6, 300), ("balanced_basin", 6, 1200),
                ("balanced_basin", 12, 60), ("balanced_basin", 12, 300), ("balanced_basin", 12, 1200),
                ("balanced_basin", 18, 60), ("balanced_basin", 18, 300), ("balanced_basin", 18, 1200),
                ("food_poor_highlands", 3, 60), ("food_poor_highlands", 6, 60),
            };

            foreach (var cfg in configs)
            {
                string sid = cfg.Item1; int w = cfg.Item2; int dur = cfg.Item3;
                Console.Error.WriteLine($"  >> {sid} w={w} t={dur}...");

                var scenario = CloneScenario(bundle.Scenarios.Resolve(sid));
                scenario.InitialWorkers = w;
                var world = PrototypeWorldGenerator.Generate(scenario);
                var sim = new PrototypeSettlementSimulation(scenario, bundle.RoleQuotas.Roles, world);
                var resources = BuildResourceSites(world);

                long tG = 0, tC = 0, tR = 0, tL = 0, tH = 0, tE = 0;
                int peak = 0;
                float hour = 8.0f;

                for (int t = 0; t < dur; t++)
                {
                    var diag = sim.Diagnostics;
                    var tickResult = sim.Advance(resources, hour, PrototypeWeather.Clear);
                    if (diag.WorkOrdersGenerated > peak) peak = diag.WorkOrdersGenerated;
                    tG += diag.WorkOrdersGenerated;
                    tC += diag.WorkOrdersClaimed;
                    tR += diag.WorkOrdersRemaining;
                    tL += diag.PathPlanLookups;
                    tH += diag.PathPlanCacheHits;
                    tE += diag.CitizensEvaluated;

                    foreach (var req in tickResult.HarvestRequests)
                    {
                        int idx = resources.FindIndex(s => s.NodeName == req.TargetNodeName);
                        if (idx >= 0)
                        {
                            var site = resources[idx];
                            if (site.UnitsRemaining >= req.Amount)
                                resources[idx] = site with { UnitsRemaining = site.UnitsRemaining - req.Amount };
                            else
                                sim.OnHarvestFailed(req.WorkerId);
                        }
                    }
                    hour = AdvanceHour(hour);
                }

                results.Add(new RunRec
                {
                    Sc = sid, W = w, D = dur, Peak = peak,
                    AG = tG / (double)dur, AC = tC / (double)dur, AR = tR / (double)dur,
                    AL = tL / (double)dur, AH = tH / (double)dur,
                    HR = tL > 0 ? tH / (double)tL : 0.0,
                    ACit = (int)Math.Round(tE / (double)dur),
                });
                Console.Error.WriteLine($"  << peak={peak} AG={tG/(double)dur:F1} AC={tC/(double)dur:F1} AR={tR/(double)dur:F1} AL={tL/(double)dur:F1} HR={tH/(double)Math.Max(1,tL):F2}");
            }

            // Smoke assertions
            Assert.Equal(14, results.Count);
            foreach (var r in results)
            {
                Assert.True(r.AG >= 0);
                Assert.True(r.HR >= 0.0 && r.HR <= 1.0);
                Assert.InRange(r.ACit, r.W - 1, r.W + 1);
            }

            // Write report
            WriteReport(results);
        }

        static void WriteReport(List<RunRec> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Settlement Workload Characterization Report");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine("## Sweep Matrix");
            sb.AppendLine("| Scenario | Workers | Duration |");
            sb.AppendLine("|----------|---------|----------|");
            foreach (var r in results)
                sb.AppendLine($"| {r.Sc} | {r.W} | {r.D} ticks |");
            sb.AppendLine();
            sb.AppendLine("## Results");
            sb.AppendLine("| Scenario | Workers | Duration | Peak | AvgGen | AvgClaimed | AvgRem | AvgLookups | HitRatio | AvgCitizens |");
            sb.AppendLine("|----------|---------|----------|------|--------|-----------|--------|-----------|----------|-------------|");
            foreach (var r in results)
                sb.AppendLine($"| {r.Sc} | {r.W} | {r.D} | {r.Peak} | {r.AG:F1} | {r.AC:F1} | {r.AR:F1} | {r.AL:F1} | {r.HR:F2} | {r.ACit} |");
            sb.AppendLine();

            var bb3 = results.First(r => r.Sc == "balanced_basin" && r.W == 3 && r.D == 1200);
            var bb12 = results.First(r => r.Sc == "balanced_basin" && r.W == 12 && r.D == 1200);
            var bb18 = results.First(r => r.Sc == "balanced_basin" && r.W == 18 && r.D == 1200);

            sb.AppendLine("## Analysis");
            sb.AppendLine();
            sb.AppendLine("### Orders per worker by population size");
            sb.AppendLine($"| Workers | Avg orders/tick | Orders/worker/tick |");
            sb.AppendLine($"|---------|----------------|-------------------|");
            sb.AppendLine($"| 3  | {bb3.AG:F1} | {bb3.AG / 3.0:F2} |");
            sb.AppendLine($"| 12 | {bb12.AG:F1} | {bb12.AG / 12.0:F2} |");
            sb.AppendLine($"| 18 | {bb18.AG:F1} | {bb18.AG / 18.0:F2} |");
            sb.AppendLine();

            sb.AppendLine("### Assignment saturation (1200-tick, balanced_basin)");
            foreach (var c in new[] { bb3, bb12, bb18 })
            {
                double pct = c.AG > 0 ? c.AC / c.AG * 100 : 0;
                sb.AppendLine($"- {c.W}w: {pct:F0}% claimed, avg {c.AR:F1} unclaimed/tick");
            }
            sb.AppendLine();

            sb.AppendLine("### Path-plan pressure (1200-tick, balanced_basin)");
            foreach (var c in new[] { bb3, bb12, bb18 })
                sb.AppendLine($"- {c.W}w: {c.AL:F1} lookups/tick, {c.HR:P0} cache hit rate");
            sb.AppendLine();

            sb.AppendLine("### Stress comparison (3w, 60 ticks)");
            var fph = results.First(r => r.Sc == "food_poor_highlands" && r.W == 3 && r.D == 60);
            var bb = results.First(r => r.Sc == "balanced_basin" && r.W == 3 && r.D == 60);
            sb.AppendLine($"- balanced_basin: avg {bb.AG:F1} orders, {bb.HR:P0} cache hits");
            sb.AppendLine($"- food_poor_highlands: avg {fph.AG:F1} orders, {fph.HR:P0} cache hits");
            sb.AppendLine();

            sb.AppendLine("### Worst case observed");
            var worstR = results.Where(r => r.Sc == "balanced_basin" && r.D == 1200).OrderByDescending(r => r.AR).First();
            var worstL = results.Where(r => r.Sc == "balanced_basin" && r.D == 1200).OrderByDescending(r => r.AL).First();
            sb.AppendLine($"- Highest backlog: {worstR.W}w, avg {worstR.AR:F1} unclaimed/tick");
            sb.AppendLine($"- Highest path pressure: {worstL.W}w, avg {worstL.AL:F1} lookups/tick, {worstL.HR:P0} hit rate");

            var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "characterization-output.csv");
            var csvLines = new List<string> { "Scenario,Workers,Duration,Peak,AvgGen,AvgClaimed,AvgRem,AvgLookups,AvgHits,HitRatio,AvgCitizens" };
            foreach (var r in results)
                csvLines.Add($"{r.Sc},{r.W},{r.D},{r.Peak},{r.AG:F2},{r.AC:F2},{r.AR:F2},{r.AL:F2},{r.AH:F2},{r.HR:F4},{r.ACit}");
            File.WriteAllLines(csvPath, csvLines);

            Console.Error.WriteLine();
            Console.Error.WriteLine(sb.ToString());
        }

        static PrototypeScenarioDefinition CloneScenario(PrototypeScenarioDefinition o) =>
            new PrototypeScenarioDefinition
            {
                Id = o.Id, DisplayName = o.DisplayName, ExpectedOutcome = o.ExpectedOutcome,
                SimulationSeed = o.SimulationSeed, InitialTrees = o.InitialTrees,
                InitialRocks = o.InitialRocks, InitialBerryBushes = o.InitialBerryBushes,
                InitialWorkers = o.InitialWorkers, InitialClayDeposits = o.InitialClayDeposits,
                InitialReedBeds = o.InitialReedBeds, WorldSize = o.WorldSize,
                StartingStock = new Dictionary<string, int>(o.StartingStock),
                StartingStructures = o.StartingStructures.ToList(),
                StartingBuildQueue = o.StartingBuildQueue.ToList(),
                PathBuildPolicy = o.PathBuildPolicy, RemoteDepotPolicy = o.RemoteDepotPolicy,
            };

        static PrototypeCatalogBundle LoadCatalogs() =>
            PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDir());

        static string GetCatalogDir()
        {
            string cur = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(cur))
            {
                string c = Path.Combine(cur, "src", "societies", "data");
                if (Directory.Exists(c)) return c;
                var p = Directory.GetParent(cur);
                cur = p?.FullName;
            }
            throw new DirectoryNotFoundException("Cannot find src/societies/data");
        }

        static List<PrototypeResourceSiteState> BuildResourceSites(WorldGenerationResult world) =>
            world.ResourceSpawns.GroupBy(s => s.ResourceId).OrderBy(g => g.Key)
                .SelectMany(g => g.Select((s, i) => new PrototypeResourceSiteState(
                    $"{s.ResourceId}_{i + 1}", s.ResourceId, s.Position, s.UnitsRemaining, s.ClusterId))).ToList();

        static float AdvanceHour(float h)
        {
            double hpt = 24.0 * (1.0 / 20.0) / 600.0;
            float n = (float)(h + hpt);
            while (n >= 24.0f) n -= 24.0f;
            return n;
        }
    }
}
