using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Societies.Core.Tests
{
    public class FrontierCategoryStarvationScan
    {
        [Fact]
        public void Compare_CappedVsUncapped_CategoryComposition()
        {
            var configs = new[] {
                ("balanced_basin", 6, 120),
                ("balanced_basin", 18, 120),
                ("food_poor_highlands", 6, 120),
            };

            var sb = new StringBuilder();
            sb.AppendLine("# Frontier Category Starvation Scan Report");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine("## Outcome Parity (Capped vs Uncapped)");
            sb.AppendLine("| Scenario | Workers | Duration | Mode | AvgGen | AvgRem | Huts | Paths | Depots | Meals | HearthFuel | HearthLit | Bed% | Class |");
            sb.AppendLine("|----------|---------|----------|------|--------|--------|------|-------|--------|-------|------------|----------|------|-------|");

            bool starvationFound = false;

            foreach (var (sid, w, dur) in configs)
            {
                foreach (var capped in new[] { true, false })
                {
                    var r = Run(sid, w, dur, capped);
                    if (sb.Length > 0) sb.AppendLine();

                    string mode = capped ? "Capped" : "Uncap";
                    sb.AppendLine($"| {sid} | {w} | {dur} | {mode} | {r.avgGen} | {r.avgRem} | {r.huts} | {r.paths} | {r.depots} | {r.meals} | {r.hfuel} | {r.hlits} | {r.bed}% | {r.cls} |");
                }
                sb.AppendLine();
            }

            // Starvation analysis
            sb.AppendLine("## Starvation Analysis");
            sb.AppendLine();

            foreach (var (sid, w, dur) in configs)
            {
                var cap = Run(sid, w, dur, true);
                var unc = Run(sid, w, dur, false);

                sb.AppendLine($"### {sid} {w}w/{dur}t");
                sb.AppendLine($"- Huts built: {cap.huts} vs {unc.huts} ({(cap.huts == unc.huts ? "SAME" : "DIFF")})");
                sb.AppendLine($"- Paths built: {cap.paths} vs {unc.paths} ({(cap.paths == unc.paths ? "SAME" : "DIFF")})");
                sb.AppendLine($"- Depots built: {cap.depots} vs {unc.depots} ({(cap.depots == unc.depots ? "SAME" : "DIFF")})");
                sb.AppendLine($"- Meals: {cap.meals} vs {unc.meals} ({(Math.Abs(cap.meals - unc.meals) <= 1 ? "SAME" : "DIFF")})");
                sb.AppendLine($"- Hear fuel: {cap.hfuel} vs {unc.hfuel}");
                sb.AppendLine($"- Hear lit: {cap.hlits} vs {unc.hlits}");
                sb.AppendLine($"- Bed%: {cap.bed}% vs {unc.bed}%");
                sb.AppendLine($"- Class: {cap.cls} vs {unc.cls} ({(cap.cls == unc.cls ? "SAME" : "DIFF")})");

                if (cap.huts != unc.huts) starvationFound = true;
                if (cap.paths != unc.paths) starvationFound = true;
                if (cap.depots != unc.depots) starvationFound = true;
                if (cap.cls != unc.cls) starvationFound = true;

                sb.AppendLine();
            }

            sb.AppendLine("## Starvation Verdict");
            sb.AppendLine(starvationFound ? "**STARVATION DETECTED**" : "**NO STARVATION DETECTED**");

            var mdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "starvation-report.md");
            File.WriteAllText(mdPath, sb.ToString());
            Console.Error.WriteLine($"Report: {mdPath}");
            Console.Error.WriteLine(System.Environment.NewLine + sb.ToString());

            Assert.False(starvationFound, "Starvation detected - outcome metrics differ");
        }

        class RunResult
        {
            public int huts, paths, depots, meals, hfuel, hlits, bed, avgGen, avgRem;
            public string cls = "";
        }

        static RunResult Run(string sid, int workers, int duration, bool capped)
        {
            var bundle = LoadCatalogs();
            var scenario = CloneScenario(bundle.Scenarios.Resolve(sid));
            scenario.InitialWorkers = workers;
            var world = PrototypeWorldGenerator.Generate(scenario);
            var sim = new PrototypeSettlementSimulation(scenario, bundle.RoleQuotas.Roles, world, uncappedOrders: !capped);
            var resources = BuildResourceSites(world);

            long tg = 0, tr = 0;
            float hour = 8.0f;

            for (int t = 0; t < duration; t++)
            {
                var tickResult = sim.Advance(resources, hour, PrototypeWeather.Clear);
                tg += sim.Diagnostics.WorkOrdersGenerated;
                tr += sim.Diagnostics.WorkOrdersRemaining;
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

            return new RunResult {
                huts = sim.Structures.Count(s => s.StructureKindId == "hut" && s.IsBuilt),
                paths = sim.PathSegments.Count(s => s.IsBuilt),
                depots = sim.RemoteDepots.Count(d => d.IsBuilt),
                meals = Math.Max(sim.CentralDepot.GetCount("meals"), sim.ProducedResources.GetValueOrDefault("meals", 0)),
                hfuel = sim.HearthFuel,
                hlits = sim.HearthLitTicks,
                bed = sim.BedCoveragePercent,
                avgGen = (int)Math.Round(tg / (double)duration),
                avgRem = (int)Math.Round(tr / (double)duration),
                cls = sim.Classification.ToString(),
            };
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

        static PrototypeCatalogBundle LoadCatalogs()
        {
            string? cur = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(cur))
            {
                string c = Path.Combine(cur, "src", "societies", "data");
                if (Directory.Exists(c)) return PrototypeCatalogLoader.LoadFromDirectory(c);
                var p = Directory.GetParent(cur);
                cur = p?.FullName;
            }
            throw new DirectoryNotFoundException("Cannot find societies data");
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
