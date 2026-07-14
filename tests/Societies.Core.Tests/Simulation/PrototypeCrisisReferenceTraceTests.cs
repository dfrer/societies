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
            RuntimeCrisisTrace first = RunNoInputSessionTrace();
            RuntimeCrisisTrace second = RunNoInputSessionTrace();

            Assert.Equal(first, second);
            Assert.Equal(9735, first.TerminalTick);
            Assert.InRange(first.TerminalTick, 8 * 60 * PrototypeSimulationTime.TicksPerSecond, 14 * 60 * PrototypeSimulationTime.TicksPerSecond);
            Assert.Equal(PrototypeCrisisOutcome.Collapsed, first.Outcome);
            Assert.Equal(PrototypeCrisisCollapseCause.IncapacitatedHold, first.CollapseCause);
            Assert.Equal(7532, first.EventCount);
            Assert.Equal("e99b79066ae85fc5617ef21295a29ae1dfc4591932ffea127eae7788073daa36", first.TraceHash);
        }

        private static RuntimeCrisisTrace RunNoInputSessionTrace()
        {
            PrototypeCatalogBundle bundle = PrototypeCatalogLoader.LoadFromDirectory(GetCatalogDirectoryPath());
            PrototypeRuntimeSession session = new(bundle.Scenarios.Resolve("empty_stores"), bundle.RoleQuotas.Roles);
            session.Initialize(8.0f);
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
