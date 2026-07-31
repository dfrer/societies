using Societies.Core;
using Societies.Simulation;
using Godot;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeCrisisReferenceTraceTests
    {
        [Fact]
        public void EmptyStores_NoInputSessionTraceCollapsesWithinTargetAndRepeatsExactly()
        {
            RuntimeCrisisTrace historicalFirst = RunNoInputSessionTrace(8.0f);
            RuntimeCrisisTrace historicalSecond = RunNoInputSessionTrace(8.0f);

            Assert.Equal(historicalFirst, historicalSecond);
            Assert.Equal(9777, historicalFirst.TerminalTick);
            Assert.InRange(historicalFirst.TerminalTick, 8 * 60 * PrototypeSimulationTime.TicksPerSecond, 14 * 60 * PrototypeSimulationTime.TicksPerSecond);
            Assert.Equal(PrototypeCrisisOutcome.Collapsed, historicalFirst.Outcome);
            Assert.Equal(PrototypeCrisisCollapseCause.IncapacitatedHold, historicalFirst.CollapseCause);
            Assert.Equal(7496, historicalFirst.EventCount);
            Assert.Equal("9ce28bf44c480d43b1313a48abfdea82900c266afc04f5351eff6e61d4e6b93a", historicalFirst.TraceHash);

            RuntimeCrisisTrace captureFirst = RunNoInputSessionTrace(10.5f);
            RuntimeCrisisTrace captureSecond = RunNoInputSessionTrace(10.5f);

            Assert.Equal(captureFirst, captureSecond);
            Assert.Equal(9777, captureFirst.TerminalTick);
            Assert.InRange(captureFirst.TerminalTick, 8 * 60 * PrototypeSimulationTime.TicksPerSecond, 14 * 60 * PrototypeSimulationTime.TicksPerSecond);
            Assert.Equal(PrototypeCrisisOutcome.Collapsed, captureFirst.Outcome);
            Assert.Equal(PrototypeCrisisCollapseCause.IncapacitatedHold, captureFirst.CollapseCause);
            Assert.Equal(8149, captureFirst.EventCount);
            Assert.Equal("8a0239837c5f96ac5ef0e470e9e91178d620b7362213cf47eaa2aa20b637eecc", captureFirst.TraceHash);
        }

        [Fact]
        public void EmptyStores_FoodAndFuelThenShelterCheckpointResumeMatchesExactlyAndStabilizes()
        {
            PrototypeCatalogBundle bundle = PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("empty_stores");
            PrototypeRuntimeSession continuous = CreateScriptedSession(bundle, scenario);
            const int checkpointTick = 600;
            while (continuous.SimulationTick < checkpointTick)
            {
                ApplyFoodThenShelterSchedule(continuous);
                _ = continuous.Advance((float)PrototypeSimulationTime.TickIntervalSeconds, 600.0f);
            }

            PrototypeRuntimeSnapshot checkpoint = continuous.CaptureSnapshot(Vector3.Zero);
            List<PrototypeEventRecord> checkpointEvents = continuous.EventLog.Entries
                .Select(entry => new PrototypeEventRecord
                {
                    Tick = entry.Tick,
                    EventType = entry.EventType,
                    Message = entry.Message
                })
                .ToList();
            PrototypeRuntimeSession resumed = new(
                scenario,
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            resumed.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(checkpoint)));
            resumed.RestoreArtifacts(checkpointEvents, null);
            Assert.Equal(
                PrototypePersistenceService.SerializeSnapshot(checkpoint),
                PrototypePersistenceService.SerializeSnapshot(resumed.CaptureSnapshot(Vector3.Zero)));

            while (!continuous.Crisis!.IsTerminal && continuous.SimulationTick < 5000)
            {
                Assert.False(resumed.Crisis!.IsTerminal);
                ApplyFoodThenShelterSchedule(continuous);
                ApplyFoodThenShelterSchedule(resumed);
                _ = continuous.Advance((float)PrototypeSimulationTime.TickIntervalSeconds, 600.0f);
                _ = resumed.Advance((float)PrototypeSimulationTime.TickIntervalSeconds, 600.0f);
                Assert.True(
                    continuous.Stockpile.Items.OrderBy(pair => pair.Key).SequenceEqual(
                        resumed.Stockpile.Items.OrderBy(pair => pair.Key)),
                    $"Checkpoint/resume stockpile diverged at tick {continuous.SimulationTick}: " +
                    $"continuous={string.Join(',', continuous.Stockpile.Items.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"))}; " +
                    $"resumed={string.Join(',', resumed.Stockpile.Items.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"))}.");
            }

            Assert.True(continuous.Crisis.IsTerminal, "The scripted directive sequence must terminate within 5,000 ticks.");
            Assert.True(resumed.Crisis!.IsTerminal);
            Assert.Equal(PrototypeCrisisOutcome.Stable, continuous.Crisis.Outcome);
            Assert.Equal(1253, continuous.SimulationTick);
            Assert.Equal(PrototypeSettlementDirective.Shelter, continuous.ActiveDirective);
            Assert.Equal(4, continuous.CaptureTelemetrySnapshot().DirectiveChanges);
            string continuousSnapshot = PrototypePersistenceService.SerializeSnapshot(
                continuous.CaptureSnapshot(Vector3.Zero));
            string resumedSnapshot = PrototypePersistenceService.SerializeSnapshot(
                resumed.CaptureSnapshot(Vector3.Zero));
            string continuousEvents = JsonSerializer.Serialize(continuous.EventLog.Entries);
            string resumedEvents = JsonSerializer.Serialize(resumed.EventLog.Entries);
            Assert.Equal(continuousSnapshot, resumedSnapshot);
            Assert.Equal(continuousEvents, resumedEvents);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(continuousSnapshot + continuousEvents))),
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resumedSnapshot + resumedEvents))));
            Assert.Single(continuous.EventLog.Entries.Where(entry =>
                entry.EventType == PrototypeEventTypes.CrisisStabilized));
        }

        private static RuntimeCrisisTrace RunNoInputSessionTrace(float initialHour)
        {
            PrototypeCatalogBundle bundle = PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());
            PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("empty_stores"), bundle.RoleQuotas.Roles);
            session.Initialize(initialHour);
            StringBuilder trace = new();
            int recordedEventCount = 0;

            while (!session.Crisis!.IsTerminal)
            {
                _ = session.Advance((float)PrototypeSimulationTime.TickIntervalSeconds, 600.0f);
                AppendTick(trace, session, ref recordedEventCount);
            }

            AppendTerminalState(trace, session);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trace.ToString()))).ToLowerInvariant();
            return new RuntimeCrisisTrace(
                checked((int)session.SimulationTick),
                session.Crisis.Outcome,
                session.Crisis.CollapseCause,
                hash,
                session.EventLog.Entries.Count);
        }

        private static PrototypeRuntimeSession CreateScriptedSession(
            PrototypeCatalogBundle bundle,
            PrototypeScenarioDefinition scenario)
        {
            PrototypeRuntimeSession session = new(
                scenario,
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            session.Initialize(8.0f);
            session.Inventory.AddItem("berries", 1000);
            session.Inventory.AddItem("logs", 1000);
            session.Inventory.AddItem("reeds", 1000);
            Assert.True(session.SetDirective(PrototypeSettlementDirective.FoodAndFuel).Changed);
            Assert.True(session.SetDirective(PrototypeSettlementDirective.Shelter).Changed);
            Assert.True(session.ContributeToStockpile("logs", 30).Succeeded);
            Assert.True(session.ContributeToStockpile("reeds", 12).Succeeded);
            Assert.True(session.ContributeToStockpile("berries", 18).Succeeded);
            return session;
        }

        private static void ApplyFoodThenShelterSchedule(PrototypeRuntimeSession session)
        {
            bool hutBuilt = session.Structures.Any(structure =>
                string.Equals(structure.StructureId, "hut_3", StringComparison.Ordinal) &&
                structure.IsBuilt);
            if (hutBuilt && session.ActiveDirective == PrototypeSettlementDirective.Shelter)
            {
                Assert.True(session.SetDirective(PrototypeSettlementDirective.FoodAndFuel).Changed);
            }

            if (session.Crisis!.StableHoldTicks == session.Crisis.Definition.StableHoldTicks - 1 &&
                session.ActiveDirective == PrototypeSettlementDirective.FoodAndFuel)
            {
                Assert.True(session.SetDirective(PrototypeSettlementDirective.Shelter).Changed);
            }

            if (session.SimulationTick > 0 &&
                session.SimulationTick % 250 == 0 &&
                session.CentralDepotOccupiedQuantity <= 60)
            {
                Assert.True(session.ContributeToStockpile("logs", 8).Succeeded);
                Assert.True(session.ContributeToStockpile("berries", 8).Succeeded);
                if (!hutBuilt)
                {
                    Assert.True(session.ContributeToStockpile("reeds", 4).Succeeded);
                }
            }
        }

        private static void AppendTick(StringBuilder trace, PrototypeRuntimeSession session, ref int recordedEventCount)
        {
            PrototypeCrisisState crisis = session.Crisis!;
            PrototypeCrisisObservation observation = crisis.LastObservation;
            trace.Append(session.SimulationTick).Append('|')
                .Append(crisis.ElapsedTicks).Append('|')
                .Append(crisis.StableHoldTicks).Append('|')
                .Append(crisis.CollapseHoldTicks).Append('|')
                .Append((int)crisis.Outcome).Append('|')
                .Append((int)crisis.CollapseCause).Append('|')
                .Append(observation.CapableCitizens).Append('|')
                .Append(observation.Meals).Append('|')
                .Append(observation.HearthFuel).Append('|')
                .Append(observation.BedCoveragePercent).Append('|')
                .Append(session.ResourceRevision).Append('|');

            foreach (PrototypeWorkerState worker in session.Workers.OrderBy(worker => worker.WorkerId, StringComparer.Ordinal))
            {
                trace.Append(worker.WorkerId).Append(':')
                    .Append((int)worker.Phase).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(worker.Needs.Nutrition)).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(worker.Needs.Fatigue)).Append(':')
                    .Append(worker.CurrentOrderId).Append(':')
                    .Append(worker.CarryItemId).Append(':')
                    .Append(worker.CarryAmount).Append(':')
                    .Append(worker.TicksRemaining).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(worker.Position.X)).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(worker.Position.Y)).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(worker.Position.Z)).Append(':')
                    .Append(worker.Navigation.CurrentWaypointIndex).Append(':')
                    .Append(worker.Navigation.CachedRouteVersion).Append(';');
            }

            trace.Append('|');
            AppendItems(trace, session.Stockpile.Items);
            for (; recordedEventCount < session.EventLog.Entries.Count; recordedEventCount++)
            {
                PrototypeEventRecord entry = session.EventLog.Entries[recordedEventCount];
                trace.Append('|').Append(entry.Tick).Append(':').Append(entry.EventType).Append(':').Append(entry.Message);
            }

            trace.Append('\n');
        }

        private static void AppendTerminalState(StringBuilder trace, PrototypeRuntimeSession session)
        {
            trace.Append("resources|");
            foreach (PrototypeResourceSnapshot resource in session.ResourceSnapshots.OrderBy(resource => resource.SiteId, StringComparer.Ordinal))
            {
                trace.Append(resource.SiteId).Append(':').Append(resource.UnitsRemaining).Append(';');
            }

            trace.Append("|structures|");
            foreach (PrototypeStructureState structure in session.Structures.OrderBy(structure => structure.StructureId, StringComparer.Ordinal))
            {
                trace.Append(structure.StructureId).Append(':')
                    .Append(structure.IsBuilt ? 1 : 0).Append(':')
                    .Append(structure.HearthFuel).Append(':')
                    .Append(BitConverter.SingleToInt32Bits(structure.Progress)).Append(':');
                AppendItems(trace, structure.InputStore.Items);
                trace.Append(':');
                AppendItems(trace, structure.OutputStore.Items);
                trace.Append(';');
            }

            trace.Append("|queue|");
            foreach (PrototypeBuildQueueEntry entry in session.BuildQueue.OrderBy(entry => entry.Priority).ThenBy(entry => entry.EntryId, StringComparer.Ordinal))
            {
                trace.Append(entry.EntryId).Append(':')
                    .Append(entry.IsPaused ? 1 : 0).Append(':')
                    .Append(entry.IsCompleted ? 1 : 0).Append(';');
            }
        }

        private static void AppendItems(StringBuilder trace, IReadOnlyDictionary<string, int> items)
        {
            foreach ((string itemId, int amount) in items.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                trace.Append(itemId).Append(':').Append(amount).Append(',');
            }
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

        private readonly record struct RuntimeCrisisTrace(
            int TerminalTick,
            PrototypeCrisisOutcome Outcome,
            PrototypeCrisisCollapseCause CollapseCause,
            string TraceHash,
            int EventCount);
    }
}
