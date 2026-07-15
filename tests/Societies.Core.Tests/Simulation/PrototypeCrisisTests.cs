using Godot;
using Societies.Core;
using Societies.Simulation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeCrisisTests
    {
        private const float TickIntervalSeconds = 1.0f / 20.0f;
        private const float DayLengthSeconds = 600.0f;

        [Fact]
        public void Catalog_EmptyStoresFreezesTheContract()
        {
            PrototypeScenarioDefinition scenario = LoadCatalogs().Scenarios.Resolve("empty_stores");
            PrototypeCrisisDefinition crisis = Assert.IsType<PrototypeCrisisDefinition>(scenario.Crisis);

            Assert.Equal(1701, scenario.SimulationSeed);
            Assert.Equal(12, scenario.InitialCitizens);
            Assert.Equal(2, scenario.StartingStructures.Count(id => id == "hut"));
            Assert.Equal("hut", scenario.StartingBuildQueue[0]);
            Assert.Equal(2, scenario.StartingStock["meals"]);
            Assert.Equal(1, scenario.InitialHearthFuel);
            Assert.True(scenario.InitialBerryBushes >= 12);
            Assert.True(scenario.InitialTrees >= 24);
            Assert.Equal("empty_stores", crisis.Id);
            Assert.Equal(20, crisis.TicksPerSecond);
            Assert.Equal(18 * 60 * 20, crisis.DeadlineTicks);
            Assert.Equal(9, crisis.RequiredCapableCitizens);
            Assert.Equal(6, crisis.RequiredMeals);
            Assert.Equal(4, crisis.RequiredHearthFuel);
            Assert.Equal(50, crisis.RequiredBedCoveragePercent);
            Assert.Equal(45 * 20, crisis.StableHoldTicks);
            Assert.Equal(6, crisis.CollapseIncapacitatedCitizens);
            Assert.Equal(10 * 20, crisis.CollapseHoldTicks);
            Assert.Equal(0.0875f, crisis.CitizenNeedRateMultiplier);
        }

        [Fact]
        public void Catalog_CrisisValidationRejectsMalformedContracts()
        {
            PrototypeCatalogBundle invalidDuration = LoadCatalogs();
            invalidDuration.Scenarios.Resolve("empty_stores").Crisis!.DeadlineTicks = 0;
            Assert.Throws<InvalidOperationException>(() => invalidDuration.Scenarios.Validate());

            PrototypeCatalogBundle invalidPopulation = LoadCatalogs();
            invalidPopulation.Scenarios.Resolve("empty_stores").Crisis!.RequiredCapableCitizens = 13;
            Assert.Throws<InvalidOperationException>(() => invalidPopulation.Scenarios.Validate());

            PrototypeCatalogBundle invalidCoverage = LoadCatalogs();
            invalidCoverage.Scenarios.Resolve("empty_stores").Crisis!.RequiredBedCoveragePercent = 101;
            Assert.Throws<InvalidOperationException>(() => invalidCoverage.Scenarios.Validate());

            PrototypeCatalogBundle invalidFuel = LoadCatalogs();
            invalidFuel.Scenarios.Resolve("empty_stores").InitialHearthFuel = -1;
            Assert.Throws<InvalidOperationException>(() => invalidFuel.Scenarios.Validate());
        }

        [Fact]
        public void Session_EmptyStoresInitializationIsDeterministicAndComplete()
        {
            PrototypeCatalogBundle firstBundle = LoadCatalogs();
            PrototypeCatalogBundle secondBundle = LoadCatalogs();
            PrototypeRuntimeSession first = new(firstBundle.Scenarios.Resolve("empty_stores"), firstBundle.RoleQuotas.Roles);
            PrototypeRuntimeSession second = new(secondBundle.Scenarios.Resolve("empty_stores"), secondBundle.RoleQuotas.Roles);

            first.Initialize(8.0f);
            second.Initialize(8.0f);

            Assert.Equal(first.WorldHash, second.WorldHash);
            Assert.Equal(12, first.Workers.Count);
            Assert.Equal(2, first.Structures.Count(structure => structure.StructureKindId == "hut" && structure.IsBuilt));
            Assert.Equal(1, first.BuildQueue.Count(entry => entry.StructureKindId == "hut" && !entry.IsCompleted));
            Assert.Equal(33, first.BedCoveragePercent);
            Assert.Equal(2, first.Stockpile.GetCount("meals"));
            Assert.Equal(1, first.HearthFuel);
            Assert.Contains(first.ActiveResourceSnapshots, resource => resource.ResourceId == "berries");
            Assert.Contains(first.ActiveResourceSnapshots, resource => resource.ResourceId == "logs");
            Assert.Equal(
                JsonSerializer.Serialize(first.Crisis!.CaptureSnapshot()),
                JsonSerializer.Serialize(second.Crisis!.CaptureSnapshot()));
            Assert.False(first.SupportsRuntimeSnapshotPersistence);
            Assert.Throws<InvalidOperationException>(() => first.CaptureSnapshot(Vector3.Zero));
            Assert.Throws<InvalidDataException>(() => first.ApplySnapshot(new PrototypeRuntimeSnapshot()));
        }

        [Fact]
        public void ExistingScenariosRemainCrisisFreeWithLegacyHearthFuel()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            string[] legacyScenarioIds = { "balanced_basin", "long_haul_quarry", "food_poor_highlands", "wetland_builder" };

            foreach (string scenarioId in legacyScenarioIds)
            {
                PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve(scenarioId);
                Assert.Null(scenario.Crisis);
                Assert.Equal(2, scenario.InitialHearthFuel);
            }

            PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("balanced_basin"), bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);
            Assert.Null(session.Crisis);
            Assert.Equal(2, session.HearthFuel);
            Assert.True(session.SupportsRuntimeSnapshotPersistence);
            Assert.Equal(6, session.CaptureSnapshot(Vector3.Zero).SchemaVersion);
        }

        [Fact]
        public void StableHoldResetsAndRequiresEveryContractTick()
        {
            PrototypeCrisisState state = CreateState();
            PrototypeCrisisObservation stable = StableObservation();

            Advance(state, stable, state.Definition.StableHoldTicks - 1);
            Assert.Equal(PrototypeCrisisOutcome.Active, state.Outcome);
            Assert.Equal(state.Definition.StableHoldTicks - 1, state.StableHoldTicks);

            state.Advance(stable with { Meals = 5 });
            Assert.Equal(0, state.StableHoldTicks);

            Advance(state, stable, state.Definition.StableHoldTicks);
            Assert.Equal(PrototypeCrisisOutcome.Stable, state.Outcome);
            Assert.Equal(state.Definition.StableHoldTicks, state.StableHoldTicks);
        }

        [Fact]
        public void CollapseHoldResetsAndRequiresEveryContractTick()
        {
            PrototypeCrisisState state = CreateState();
            PrototypeCrisisObservation collapsed = CollapsedObservation();

            Advance(state, collapsed, state.Definition.CollapseHoldTicks - 1);
            Assert.Equal(PrototypeCrisisOutcome.Active, state.Outcome);
            Assert.Equal(state.Definition.CollapseHoldTicks - 1, state.CollapseHoldTicks);

            state.Advance(collapsed with { CapableCitizens = 7 });
            Assert.Equal(0, state.CollapseHoldTicks);

            Advance(state, collapsed, state.Definition.CollapseHoldTicks);
            Assert.Equal(PrototypeCrisisOutcome.Collapsed, state.Outcome);
            Assert.Equal(PrototypeCrisisCollapseCause.IncapacitatedHold, state.CollapseCause);
        }

        [Fact]
        public void DeadlineCollapsesWithoutAOneTickEarlyOutcome()
        {
            PrototypeCrisisState state = CreateState();
            PrototypeCrisisObservation strained = StrainedObservation();

            Advance(state, strained, state.Definition.DeadlineTicks - 1);
            Assert.Equal(PrototypeCrisisOutcome.Active, state.Outcome);
            Assert.Equal(1, state.RemainingTicks);

            state.Advance(strained);
            Assert.Equal(PrototypeCrisisOutcome.Collapsed, state.Outcome);
            Assert.Equal(PrototypeCrisisCollapseCause.Deadline, state.CollapseCause);
            Assert.Equal(0, state.RemainingTicks);
        }

        [Fact]
        public void PausedAndTerminalTicksDoNotAdvanceCrisisState()
        {
            PrototypeCrisisState state = CreateState();
            Advance(state, StableObservation(), 100, simulationPaused: true);
            Assert.Equal(0, state.ElapsedTicks);
            Assert.Equal(0, state.StableHoldTicks);
            Assert.False(state.HasObservation);

            Advance(state, StableObservation(), state.Definition.StableHoldTicks);
            PrototypeCrisisStateSnapshot terminal = state.CaptureSnapshot();
            Advance(state, CollapsedObservation(), 100);
            Assert.Equal(JsonSerializer.Serialize(terminal), JsonSerializer.Serialize(state.CaptureSnapshot()));
        }

        [Fact]
        public void CrisisCheckpointResumePreservesHoldProgressAndFinalState()
        {
            PrototypeCrisisState continuous = CreateState();
            Advance(continuous, StableObservation(), 450);
            PrototypeCrisisStateSnapshot checkpoint = continuous.CaptureSnapshot();

            PrototypeCrisisState resumed = CreateState();
            resumed.Restore(JsonSerializer.Deserialize<PrototypeCrisisStateSnapshot>(JsonSerializer.Serialize(checkpoint))!);

            Advance(continuous, StableObservation(), 450);
            Advance(resumed, StableObservation(), 450);

            Assert.Equal(PrototypeCrisisOutcome.Stable, resumed.Outcome);
            Assert.Equal(
                JsonSerializer.Serialize(continuous.CaptureSnapshot()),
                JsonSerializer.Serialize(resumed.CaptureSnapshot()));

            PrototypeCrisisStateSnapshot impossible = new()
            {
                CrisisId = checkpoint.CrisisId,
                ElapsedTicks = 1,
                StableHoldTicks = 450,
                HasObservation = true,
                LastObservation = StableObservation()
            };
            Assert.Throws<ArgumentException>(() => CreateState().Restore(impossible));

            impossible.ElapsedTicks = 450;
            impossible.LastObservation = StrainedObservation();
            Assert.Throws<ArgumentException>(() => CreateState().Restore(impossible));

            impossible.ElapsedTicks = continuous.Definition.DeadlineTicks;
            impossible.StableHoldTicks = continuous.Definition.StableHoldTicks;
            impossible.Outcome = PrototypeCrisisOutcome.Stable;
            impossible.LastObservation = StableObservation();
            Assert.Throws<ArgumentException>(() => CreateState().Restore(impossible));

            impossible.StableHoldTicks = 0;
            impossible.CollapseHoldTicks = continuous.Definition.CollapseHoldTicks;
            impossible.Outcome = PrototypeCrisisOutcome.Collapsed;
            impossible.CollapseCause = PrototypeCrisisCollapseCause.Deadline;
            impossible.LastObservation = CollapsedObservation();
            Assert.Throws<ArgumentException>(() => CreateState().Restore(impossible));
        }

        [Fact]
        public void FixedSeedScriptedCommandsStabilizeAndRepeatExactly()
        {
            CrisisTraceResult first = RunCatalogScriptedSuccessSchedule();
            CrisisTraceResult second = RunCatalogScriptedSuccessSchedule();

            Assert.Equal(PrototypeCrisisOutcome.Stable, first.Outcome);
            Assert.Equal(PrototypeCrisisCollapseCause.None, first.CollapseCause);
            Assert.Equal(1253, first.TerminalTick);
            Assert.Equal(245, first.HutBuiltSwitchTick);
            Assert.Equal("a81d7083649911a3244446bcde991f215dc0237ea2aa8967b94bfc0c9a98bd3b", first.Hash);
            Assert.Equal(first, second);
        }

        [Fact]
        public void ShortUnitNoInputCollapseRepeatsWithOneTerminalEvent()
        {
            CrisisTraceResult first = RunNoInputCollapseSchedule();
            CrisisTraceResult second = RunNoInputCollapseSchedule();

            Assert.Equal(first, second);
            Assert.Equal(PrototypeCrisisOutcome.Collapsed, first.Outcome);
            Assert.Equal(PrototypeCrisisCollapseCause.Deadline, first.CollapseCause);
            Assert.Equal(6, first.TerminalTick);
        }

        [Fact]
        public void TerminalEventIsExactlyOnceAndStrictCheckpointPreventsReemission()
        {
            PrototypeCrisisState state = CreateState();
            Advance(state, StableObservation(), state.Definition.StableHoldTicks);

            Assert.True(state.TryMarkTerminalEventEmitted());
            Assert.False(state.TryMarkTerminalEventEmitted());
            PrototypeCrisisStateSnapshot terminal = JsonSerializer.Deserialize<PrototypeCrisisStateSnapshot>(
                JsonSerializer.Serialize(state.CaptureSnapshot()))!;
            Assert.True(terminal.TerminalEventEmitted);

            PrototypeCrisisState resumed = CreateState();
            resumed.Restore(terminal);
            Assert.True(resumed.IsTerminal);
            Assert.True(resumed.TerminalEventEmitted);
            Assert.False(resumed.TryMarkTerminalEventEmitted());

            PrototypeCrisisStateSnapshot beforeAdvance = resumed.CaptureSnapshot();
            resumed.Advance(CollapsedObservation());
            Assert.Equal(JsonSerializer.Serialize(beforeAdvance), JsonSerializer.Serialize(resumed.CaptureSnapshot()));

            PrototypeCrisisState active = CreateState();
            active.Advance(StrainedObservation());
            PrototypeCrisisStateSnapshot impossibleActive = active.CaptureSnapshot();
            impossibleActive.TerminalEventEmitted = true;
            Assert.Throws<ArgumentException>(() => CreateState().Restore(impossibleActive));

            Advance(active, StableObservation(), active.Definition.StableHoldTicks);
            Assert.True(active.TryMarkTerminalEventEmitted());
        }

        [Fact]
        public void StrictCheckpointResumePreservesCollapseHoldAndTerminalEventState()
        {
            PrototypeCrisisState continuous = CreateState();
            Advance(continuous, CollapsedObservation(), 100);
            PrototypeCrisisState resumed = CreateState();
            resumed.Restore(JsonSerializer.Deserialize<PrototypeCrisisStateSnapshot>(
                JsonSerializer.Serialize(continuous.CaptureSnapshot()))!);

            Advance(continuous, CollapsedObservation(), continuous.Definition.CollapseHoldTicks - 100);
            Advance(resumed, CollapsedObservation(), resumed.Definition.CollapseHoldTicks - 100);
            Assert.Equal(PrototypeCrisisOutcome.Collapsed, resumed.Outcome);
            Assert.True(continuous.TryMarkTerminalEventEmitted());
            Assert.True(resumed.TryMarkTerminalEventEmitted());
            Assert.Equal(JsonSerializer.Serialize(continuous.CaptureSnapshot()), JsonSerializer.Serialize(resumed.CaptureSnapshot()));
        }

        [Fact]
        public void SessionTerminalEventIsExactlyOnceAndInitializeResetsReplayState()
        {
            PrototypeRuntimeSession session = CreateShortUnitCrisisSession(stable: true);
            RunShortUnitSuccessCommands(session);
            string firstTrace = BuildSessionTrace(session);
            Assert.Single(session.EventLog.Entries.Where(entry => entry.EventType == PrototypeEventTypes.CrisisStabilized));
            Assert.True(session.Crisis!.TerminalEventEmitted);

            _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            Assert.Single(session.EventLog.Entries.Where(entry => entry.EventType == PrototypeEventTypes.CrisisStabilized));
            Assert.Equal(2, session.Crisis.ElapsedTicks);

            session.Initialize(8.0f);
            Assert.False(session.Crisis!.HasObservation);
            Assert.False(session.Crisis.TerminalEventEmitted);
            Assert.Empty(session.ContributionCountsByResource);
            Assert.Empty(session.EventLog.Entries);

            RunShortUnitSuccessCommands(session);
            Assert.Equal(firstTrace, BuildSessionTrace(session));
        }

        [Fact]
        public void RuntimeSessionAdvancesCrisisOncePerUnpausedSimulationTick()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("empty_stores"), bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);

            _ = session.Advance(TickIntervalSeconds, DayLengthSeconds, simulationPaused: true);
            Assert.Equal(0, session.SimulationTick);
            Assert.Equal(0, session.Crisis!.ElapsedTicks);

            _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            Assert.Equal(2, session.SimulationTick);
            Assert.Equal(2, session.Crisis.ElapsedTicks);
            Assert.Equal(12, session.Crisis.LastObservation.TotalCitizens);
        }

        [Fact]
        public void RenderFrameCadenceDoesNotChangeCrisisTickState()
        {
            PrototypeCrisisState steady = RunFrameCadence(Enumerable.Repeat(0.05, 40));
            PrototypeCrisisState irregular = RunFrameCadence(Enumerable.Repeat(new[] { 0.01, 0.09, 0.03, 0.07 }, 10).SelectMany(values => values));

            Assert.Equal(40, steady.ElapsedTicks);
            Assert.Equal(
                JsonSerializer.Serialize(steady.CaptureSnapshot()),
                JsonSerializer.Serialize(irregular.CaptureSnapshot()));
        }

        private static PrototypeCrisisState RunFrameCadence(IEnumerable<double> frameDeltas)
        {
            FixedStepAccumulator accumulator = new(PrototypeSimulationTime.TickIntervalSeconds, maxTicksPerFrame: 12);
            PrototypeCrisisState state = CreateState();
            foreach (double delta in frameDeltas)
            {
                Advance(state, StrainedObservation(), accumulator.Consume(delta));
            }

            while (accumulator.HasBacklog)
            {
                Advance(state, StrainedObservation(), accumulator.Consume(0.0));
            }

            return state;
        }

        private static CrisisTraceResult RunCatalogScriptedSuccessSchedule()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("empty_stores");
            AssertFrozenEmptyStoresContract(scenario);
            PrototypeRuntimeSession session = new(scenario, bundle.RoleQuotas.Roles, resourceDefinitions: bundle.Resources.Resources);
            session.Initialize(8.0f);

            Assert.Equal("collapsed", scenario.ExpectedOutcome);
            session.Inventory.AddItem("berries", 1000);
            session.Inventory.AddItem("logs", 1000);
            session.Inventory.AddItem("reeds", 1000);
            Assert.True(session.SetDirective(PrototypeSettlementDirective.Shelter).Changed);
            Assert.True(session.ContributeToStockpile("logs", 30).Succeeded);
            Assert.True(session.ContributeToStockpile("reeds", 12).Succeeded);
            Assert.True(session.ContributeToStockpile("berries", 18).Succeeded);
            int hutBuiltSwitchTick = -1;

            while (!session.Crisis!.IsTerminal)
            {
                bool hutBuilt = session.Structures.Any(structure =>
                    string.Equals(structure.StructureId, "hut_3", StringComparison.Ordinal) && structure.IsBuilt);
                if (hutBuilt && hutBuiltSwitchTick < 0)
                {
                    hutBuiltSwitchTick = checked((int)session.SimulationTick);
                    Assert.True(session.SetDirective(PrototypeSettlementDirective.FoodAndFuel).Changed);
                }

                if (session.SimulationTick % 250 == 0 && session.CentralDepotOccupiedQuantity <= 60)
                {
                    Assert.True(session.ContributeToStockpile("logs", 8).Succeeded);
                    Assert.True(session.ContributeToStockpile("berries", 8).Succeeded);
                    if (!hutBuilt)
                    {
                        Assert.True(session.ContributeToStockpile("reeds", 4).Succeeded);
                    }
                }

                _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            }

            Assert.True(
                session.Crisis.Outcome == PrototypeCrisisOutcome.Stable,
                $"Scripted catalog run ended {session.Crisis.Outcome}/{session.Crisis.CollapseCause} at {session.SimulationTick}; " +
                $"obs={session.Crisis.LastObservation}; contributions={string.Join(',', session.ContributionCountsByResource.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"))}; " +
                $"stockpile={string.Join(',', session.Stockpile.Items.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"))}; " +
                $"hutBuiltSwitchTick={hutBuiltSwitchTick}; structures={string.Join(';', session.Structures.Where(structure => structure.StructureKindId is "hut" or "cookfire" or "wood_yard").OrderBy(structure => structure.StructureId).Select(structure => $"{structure.StructureId}[{structure.IsBuilt},{string.Join(',', structure.InputStore.Items.Select(pair => $"{pair.Key}:{pair.Value}"))},{string.Join(',', structure.OutputStore.Items.Select(pair => $"{pair.Key}:{pair.Value}"))}]") )}.");
            Assert.True(session.ContributionCountsByResource.Values.Sum() > 0);
            Assert.Contains(session.EventLog.Entries, entry => entry.EventType == PrototypeEventTypes.PlayerContributionSucceeded);
            Assert.Contains(session.EventLog.Entries, entry => entry.EventType == PrototypeEventTypes.SettlementDirectiveChanged);
            return BuildSessionTraceResult(session, hutBuiltSwitchTick);
        }

        private static CrisisTraceResult RunNoInputCollapseSchedule()
        {
            PrototypeRuntimeSession session = CreateShortUnitCrisisSession(stable: false);
            while (!session.Crisis!.IsTerminal)
            {
                _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            }

            return BuildSessionTraceResult(session);
        }

        private static void RunShortUnitSuccessCommands(PrototypeRuntimeSession session)
        {
            session.Inventory.AddItem("logs", 3);
            Assert.True(session.SetDirective(PrototypeSettlementDirective.Shelter).Changed);
            Assert.True(session.ContributeToStockpile("logs", 3).Succeeded);
            while (!session.Crisis!.IsTerminal)
            {
                _ = session.Advance(TickIntervalSeconds, DayLengthSeconds);
            }
        }

        private static PrototypeRuntimeSession CreateShortUnitCrisisSession(bool stable)
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = JsonSerializer.Deserialize<PrototypeScenarioDefinition>(
                JsonSerializer.Serialize(bundle.Scenarios.Resolve("empty_stores")))!;
            PrototypeCrisisDefinition crisis = scenario.Crisis!;
            crisis.DeadlineTicks = stable ? 12 : 6;
            crisis.StableHoldTicks = 2;
            crisis.CollapseHoldTicks = 3;
            crisis.CollapseIncapacitatedCitizens = 100;
            crisis.RequiredCapableCitizens = stable ? 1 : scenario.InitialCitizens + 1;
            crisis.RequiredMeals = 0;
            crisis.RequiredHearthFuel = 0;
            crisis.RequiredBedCoveragePercent = 0;
            PrototypeRuntimeSession session = new(scenario, bundle.RoleQuotas.Roles, resourceDefinitions: bundle.Resources.Resources);
            session.Initialize(8.0f);
            return session;
        }

        private static CrisisTraceResult BuildSessionTraceResult(PrototypeRuntimeSession session, int hutBuiltSwitchTick = -1)
        {
            PrototypeCrisisState crisis = session.Crisis!;
            string trace = BuildSessionTrace(session);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace))).ToLowerInvariant();
            return new CrisisTraceResult(crisis.ElapsedTicks, crisis.Outcome, crisis.CollapseCause, hutBuiltSwitchTick, hash);
        }

        private static string BuildSessionTrace(PrototypeRuntimeSession session)
        {
            StringBuilder trace = new();
            AppendCanonicalJson(trace, session.EventLog.Entries);
            trace.Append('|');
            AppendCanonicalJson(trace, session.Crisis!.CaptureSnapshot());
            trace.Append('|');
            AppendCanonicalJson(trace, session.CaptureDirectiveSnapshot());
            trace.Append('|');
            AppendCanonicalJson(trace, session.ContributionCountsByResource);
            trace.Append('|');
            AppendCanonicalJson(trace, session.Inventory.Items);
            trace.Append('|');
            AppendCanonicalJson(trace, session.Stockpile.Items);
            trace.Append('|');
            AppendCanonicalJson(trace, session.ResourceSnapshots.OrderBy(resource => resource.SiteId, StringComparer.Ordinal).ToArray());
            trace.Append('|');
            AppendCanonicalJson(trace, session.CaptureSettlementSnapshotForTesting());
            return trace.ToString();
        }

        private static void AppendCanonicalJson(StringBuilder trace, object value)
        {
            using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(value));
            AppendCanonicalJsonElement(trace, document.RootElement);
        }

        private static void AppendCanonicalJsonElement(StringBuilder trace, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    trace.Append('{');
                    bool firstProperty = true;
                    foreach (JsonProperty property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                    {
                        if (!firstProperty)
                        {
                            trace.Append(',');
                        }

                        trace.Append(JsonSerializer.Serialize(property.Name)).Append(':');
                        AppendCanonicalJsonElement(trace, property.Value);
                        firstProperty = false;
                    }

                    trace.Append('}');
                    break;

                case JsonValueKind.Array:
                    trace.Append('[');
                    int index = 0;
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        if (index++ > 0)
                        {
                            trace.Append(',');
                        }

                        AppendCanonicalJsonElement(trace, item);
                    }

                    trace.Append(']');
                    break;

                default:
                    trace.Append(element.GetRawText());
                    break;
            }
        }

        private static void AssertFrozenEmptyStoresContract(PrototypeScenarioDefinition scenario)
        {
            PrototypeCrisisDefinition crisis = Assert.IsType<PrototypeCrisisDefinition>(scenario.Crisis);
            Assert.Equal(1701, scenario.SimulationSeed);
            Assert.Equal(21600, crisis.DeadlineTicks);
            Assert.Equal(9, crisis.RequiredCapableCitizens);
            Assert.Equal(6, crisis.RequiredMeals);
            Assert.Equal(4, crisis.RequiredHearthFuel);
            Assert.Equal(50, crisis.RequiredBedCoveragePercent);
            Assert.Equal(900, crisis.StableHoldTicks);
            Assert.Equal(6, crisis.CollapseIncapacitatedCitizens);
            Assert.Equal(200, crisis.CollapseHoldTicks);
            Assert.Equal(0.0875f, crisis.CitizenNeedRateMultiplier);
        }

        private static PrototypeCrisisState CreateState()
        {
            return new PrototypeCrisisState(LoadCatalogs().Scenarios.Resolve("empty_stores").Crisis!);
        }

        private static PrototypeCrisisObservation StableObservation() => new(12, 9, 6, 4, 50);

        private static PrototypeCrisisObservation StrainedObservation() => new(12, 12, 2, 1, 50);

        private static PrototypeCrisisObservation CollapsedObservation() => new(12, 6, 0, 0, 50);

        private static void Advance(
            PrototypeCrisisState state,
            PrototypeCrisisObservation observation,
            int ticks,
            bool simulationPaused = false)
        {
            for (int tick = 0; tick < ticks; tick++)
            {
                state.Advance(observation, simulationPaused);
            }
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            return PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());
        }

        private static string GetCatalogDirectoryPath()
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not find src/societies/data.");
        }

        private readonly record struct CrisisTraceResult(
            int TerminalTick,
            PrototypeCrisisOutcome Outcome,
            PrototypeCrisisCollapseCause CollapseCause,
            int HutBuiltSwitchTick,
            string Hash);
    }
}
