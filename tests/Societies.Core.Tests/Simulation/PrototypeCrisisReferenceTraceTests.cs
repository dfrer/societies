using Societies.Core;
using Societies.Simulation;
using System.Security.Cryptography;
using System.Text;
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
            Assert.Equal(7495, historicalFirst.EventCount);
            Assert.Equal("ab219da9ad492a0011bd70160548d29da3420569eb6a89facd74c6c63145a880", historicalFirst.TraceHash);

            RuntimeCrisisTrace captureFirst = RunNoInputSessionTrace(10.5f);
            RuntimeCrisisTrace captureSecond = RunNoInputSessionTrace(10.5f);

            Assert.Equal(captureFirst, captureSecond);
            Assert.Equal(9777, captureFirst.TerminalTick);
            Assert.InRange(captureFirst.TerminalTick, 8 * 60 * PrototypeSimulationTime.TicksPerSecond, 14 * 60 * PrototypeSimulationTime.TicksPerSecond);
            Assert.Equal(PrototypeCrisisOutcome.Collapsed, captureFirst.Outcome);
            Assert.Equal(PrototypeCrisisCollapseCause.IncapacitatedHold, captureFirst.CollapseCause);
            Assert.Equal(8148, captureFirst.EventCount);
            Assert.Equal("69f3e22402e31a53b1d4c16899883956fcc5fdb14fbe47d8a4eb8baef007174f", captureFirst.TraceHash);
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
