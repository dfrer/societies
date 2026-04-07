using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Societies.Core.Tests
{
    /// <summary>
    /// Extended (long-running) test: compares capped vs uncapped path logistics impact.
    /// Run via: dotnet test tests/Societies.Core.Tests.Extended/Societies.Core.Tests.Extended.csproj --filter "Category=Frontier"
    /// </summary>
    public class PathSegmentLogisticsImpact
    {
        class RunConfig
        {
            public string Scenario;
            public int Workers;
            public int Ticks;
            public RunConfig(string scenario, int workers, int ticks)
            {
                Scenario = scenario;
                Workers = workers;
                Ticks = ticks;
            }
            public override string ToString() => $"{Scenario}/{Workers}w/{Ticks}t";
        }

        class LogisticsMetrics
        {
            public int PathSegmentsBuilt;
            public float AvgRouteLengthMeters;
            public float AvgTravelWorkRatio;
            public float PathCoverageRatio;
            public int HutsBuilt;
            public int MealsProduced;
            public int HearthFuel;
            public int HearthLitTicks;
            public int BedCoverage;
            public string Classification = "";
            public int AvgOrdersGenerated;
            public int AvgOrdersRemaining;
            public Dictionary<string, int> RouteBacklogTicksByKind = new();
            public int TotalDepotThroughput;
        }

        [Fact(Skip = "Ultra-heavy: 8 configs including 2400-tick uncapped runs (~45 min). Run selectively with --filter.")]
        [Trait("Category", "Frontier")]
        public void Compare_PathSegmentLogistics_CappedVsUncapped_LongRuns()
        {
            var configs = new[]
            {
                // Primary run matrix
                new RunConfig("balanced_basin", 18, 1200),
                new RunConfig("balanced_basin", 18, 2400),
                new RunConfig("balanced_basin", 6, 2400),
                // Secondary (cheaper) run
                new RunConfig("food_poor_highlands", 6, 1200),
            };

            var results = new List<(RunConfig cfg, bool capped, LogisticsMetrics m, TimeSpan elapsed)>();

            foreach (var cfg in configs)
            {
                foreach (var capped in new[] { true, false })
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var metrics = RunSimulation(cfg, capped);
                    sw.Stop();
                    results.Add((cfg, capped, metrics, sw.Elapsed));
                    Console.Error.WriteLine($"[{sw.Elapsed.TotalSeconds:F1}s] {cfg} {(capped ? "CAPPED" : "UNCAPPED")}");
                }
            }

            // Build report
            var sb = new StringBuilder();
            sb.AppendLine("# Path Segment Logistics Impact Report");
            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();

            // Summary table
            sb.AppendLine("## Run Matrix: Capped vs Uncapped");
            sb.AppendLine("| Config | Mode | Time(s) | Paths | AvgRoute(m) | Travel/Work | Coverage | Huts | Meals | Fuel | LitTicks | Bed% | Class | AvgOrdGen | AvgOrdRem | Throughput |");
            sb.AppendLine("|--------|------|---------|-------|-------------|-------------|----------|------|-------|------|----------|------|-------|-----------|-----------|------------|");

            foreach (var (cfg, capped, m, el) in results)
            {
                string mode = capped ? "Capped" : "Uncap ";
                sb.AppendLine($"| {cfg} | {mode} | {el.TotalSeconds:F1} | {m.PathSegmentsBuilt} | {m.AvgRouteLengthMeters:F1} | {m.AvgTravelWorkRatio:F2} | {m.PathCoverageRatio:F4} | {m.HutsBuilt} | {m.MealsProduced} | {m.HearthFuel} | {m.HearthLitTicks} | {m.BedCoverage}% | {m.Classification} | {m.AvgOrdersGenerated} | {m.AvgOrdersRemaining} | {m.TotalDepotThroughput} |");
            }

            // Delta analysis
            sb.AppendLine();
            sb.AppendLine("## Deltas (Capped minus Uncapped)");
            sb.AppendLine("| Config | Path Delta | RouteLen Delta | Travel/Work Delta | Coverage Delta | Throughput Delta | OrdersGen Delta |");
            sb.AppendLine("|--------|------------|----------------|-------------------|----------------|------------------|-----------------|");

            foreach (var cfg in configs)
            {
                var cap = results.First(r => r.cfg.Scenario == cfg.Scenario && r.cfg.Workers == cfg.Workers && r.cfg.Ticks == cfg.Ticks && r.capped).m;
                var unc = results.First(r => r.cfg.Scenario == cfg.Scenario && r.cfg.Workers == cfg.Workers && r.cfg.Ticks == cfg.Ticks && !r.capped).m;

                sb.AppendLine($"| {cfg} | {cap.PathSegmentsBuilt - unc.PathSegmentsBuilt:+#;-#;0} | {cap.AvgRouteLengthMeters - unc.AvgRouteLengthMeters:+#.0;-#.0;0.0} | {cap.AvgTravelWorkRatio - unc.AvgTravelWorkRatio:+#.##;-##;0.0} | {cap.PathCoverageRatio - unc.PathCoverageRatio:+#.####;-#.####;0.0000} | {cap.TotalDepotThroughput - unc.TotalDepotThroughput:+#;-#;0} | {cap.AvgOrdersGenerated - unc.AvgOrdersGenerated:+#;-#;0} |");
            }

            // Route backlog breakdown
            sb.AppendLine();
            sb.AppendLine("## Route Backlog Ticks by Kind (Capped vs Uncapped)");
            sb.AppendLine();
            foreach (var cfg in configs)
            {
                var cap = results.First(r => r.cfg.Scenario == cfg.Scenario && r.cfg.Workers == cfg.Workers && r.cfg.Ticks == cfg.Ticks && r.capped).m;
                var unc = results.First(r => r.cfg.Scenario == cfg.Scenario && r.cfg.Workers == cfg.Workers && r.cfg.Ticks == cfg.Ticks && !r.capped).m;
                sb.AppendLine($"### {cfg}");
                sb.AppendLine("| Kind | Capped Ticks | Uncapped Ticks | Delta |");
                sb.AppendLine("|------|-------------|---------------|-------|");
                foreach (var kind in cap.RouteBacklogTicksByKind.Keys.Union(unc.RouteBacklogTicksByKind.Keys).OrderBy(k => k))
                {
                    int cv = cap.RouteBacklogTicksByKind.GetValueOrDefault(kind, 0);
                    int uv = unc.RouteBacklogTicksByKind.GetValueOrDefault(kind, 0);
                    sb.AppendLine($"| {kind} | {cv} | {uv} | {cv - uv:+#;-#;0} |");
                }
                sb.AppendLine();
            }

            // Outcome parity
            sb.AppendLine("## Outcome Parity");
            sb.AppendLine();
            bool parityOK = true;
            foreach (var cfg in configs)
            {
                var cap = results.First(r => r.cfg.Scenario == cfg.Scenario && r.cfg.Workers == cfg.Workers && r.cfg.Ticks == cfg.Ticks && r.capped).m;
                var unc = results.First(r => r.cfg.Scenario == cfg.Scenario && r.cfg.Workers == cfg.Workers && r.cfg.Ticks == cfg.Ticks && !r.capped).m;

                string huts = cap.HutsBuilt == unc.HutsBuilt ? "SAME" : "DIFF";
                string meals = Math.Abs(cap.MealsProduced - unc.MealsProduced) <= 1 ? "SAME" : "DIFF";
                string hearth = cap.HearthFuel == unc.HearthFuel ? "SAME" : "DIFF";
                string hlits = cap.HearthLitTicks == unc.HearthLitTicks ? "SAME" : "DIFF";
                string bed = cap.BedCoverage == unc.BedCoverage ? "SAME" : "DIFF";
                string cls = cap.Classification == unc.Classification ? "SAME" : "DIFF";

                sb.AppendLine($"### {cfg}");
                sb.AppendLine($"- Huts: {cap.HutsBuilt} vs {unc.HutsBuilt} ({huts})");
                sb.AppendLine($"- Meals: {cap.MealsProduced} vs {unc.MealsProduced} ({meals})");
                sb.AppendLine($"- Hearth fuel: {cap.HearthFuel} vs {unc.HearthFuel} ({hearth})");
                sb.AppendLine($"- Lit ticks: {cap.HearthLitTicks} vs {unc.HearthLitTicks} ({hlits})");
                sb.AppendLine($"- Bed%: {cap.BedCoverage}% vs {unc.BedCoverage}% ({bed})");
                sb.AppendLine($"- Class: {cap.Classification} vs {unc.Classification} ({cls})");
                if (huts == "DIFF" || meals == "DIFF" || hearth == "DIFF" || hlits == "DIFF" || bed == "DIFF" || cls == "DIFF")
                    parityOK = false;
                sb.AppendLine();
            }
            sb.AppendLine(parityOK ? "## Verdict: OUTCOME PARITY PRESERVED" : "## Verdict: OUTCOME PARITY BROKEN");

            string report = sb.ToString();
            Console.Error.WriteLine(report);
            var mdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "path-logistics-impact-report.md");
            File.WriteAllText(mdPath, report);
            Console.Error.WriteLine($"Report: {mdPath}");
        }

        static LogisticsMetrics RunSimulation(RunConfig cfg, bool capped)
        {
            var bundle = LoadCatalogs();
            var sc = CloneScenario(bundle.Scenarios.Resolve(cfg.Scenario));
            sc.InitialWorkers = cfg.Workers;
            var world = PrototypeWorldGenerator.Generate(sc);
            var sim = new PrototypeSettlementSimulation(sc, bundle.RoleQuotas.Roles, world, uncappedOrders: !capped);
            var resources = BuildResourceSites(world);

            long tg = 0, tr = 0;
            float hour = 8.0f;

            for (int t = 0; t < cfg.Ticks; t++)
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

            int totalThroughput = sim.DepotThroughputByDepot.Values.Sum();
            return new LogisticsMetrics
            {
                PathSegmentsBuilt = sim.PathSegments.Count(s => s.IsBuilt),
                AvgRouteLengthMeters = sim.AverageRouteLengthMeters,
                AvgTravelWorkRatio = sim.AverageTravelWorkRatio,
                PathCoverageRatio = sim.PathCoverageRatio,
                HutsBuilt = sim.Structures.Count(s => s.StructureKindId == "hut" && s.IsBuilt),
                MealsProduced = Math.Max(sim.CentralDepot.GetCount("meals"), sim.ProducedResources.GetValueOrDefault("meals", 0)),
                HearthFuel = sim.HearthFuel,
                HearthLitTicks = sim.HearthLitTicks,
                BedCoverage = sim.BedCoveragePercent,
                Classification = sim.Classification.ToString(),
                AvgOrdersGenerated = (int)Math.Round(tg / (double)cfg.Ticks),
                AvgOrdersRemaining = (int)Math.Round(tr / (double)cfg.Ticks),
                RouteBacklogTicksByKind = new Dictionary<string, int>(sim.RouteBacklogTicksByKind),
                TotalDepotThroughput = totalThroughput,
            };
        }

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
