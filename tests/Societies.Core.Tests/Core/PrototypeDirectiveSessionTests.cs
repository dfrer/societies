using Godot;
using Societies.Simulation;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypeDirectiveSessionTests
    {
        [Fact]
        public void SetDirective_RecordsOnlyRealChangesInDeterministicOrder()
        {
            PrototypeRuntimeSession session = CreateSession();
            int initialEventCount = session.EventLog.Entries.Count;

            PrototypeDirectiveChangeResult first = session.SetDirective(PrototypeSettlementDirective.FoodAndFuel);
            PrototypeDirectiveChangeResult duplicate = session.SetDirective(PrototypeSettlementDirective.FoodAndFuel);
            PrototypeDirectiveChangeResult second = session.SetDirective(PrototypeSettlementDirective.Shelter);

            Assert.True(first.Succeeded && first.Changed);
            Assert.True(duplicate.Succeeded && !duplicate.Changed);
            Assert.True(second.Succeeded && second.Changed);
            Assert.Equal(PrototypeSettlementDirective.Shelter, session.ActiveDirective);
            PrototypeEventRecord[] events = session.EventLog.Entries.Skip(initialEventCount).ToArray();
            Assert.Equal(2, events.Length);
            Assert.All(events, entry => Assert.Equal(PrototypeEventTypes.SettlementDirectiveChanged, entry.EventType));
            Assert.All(events, entry => Assert.Equal(0, entry.Tick));
            Assert.Contains("Neutral to Food & Fuel", events[0].Message, StringComparison.Ordinal);
            Assert.Contains("Food & Fuel to Shelter", events[1].Message, StringComparison.Ordinal);
        }

        [Fact]
        public void SetDirective_InvalidValueDoesNotMutateOrEmit()
        {
            PrototypeRuntimeSession session = CreateSession();
            int initialEventCount = session.EventLog.Entries.Count;

            PrototypeDirectiveChangeResult result = session.SetDirective((PrototypeSettlementDirective)999);

            Assert.False(result.Succeeded);
            Assert.False(result.Changed);
            Assert.Equal("invalid_directive", result.FailureReason);
            Assert.Equal(PrototypeSettlementDirective.Neutral, session.ActiveDirective);
            Assert.Equal(initialEventCount, session.EventLog.Entries.Count);
        }

        [Fact]
        public void DirectiveSnapshotContract_RoundTripsAtSchemaEight()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.SetDirective(PrototypeSettlementDirective.Shelter);

            PrototypeDirectiveSnapshot directive = session.CaptureDirectiveSnapshot();

            Assert.Equal("shelter", directive.DirectiveId);
            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            Assert.Equal(8, snapshot.SchemaVersion);
            Assert.Equal("shelter", snapshot.Directive!.DirectiveId);
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession restored = new(
                bundle.Scenarios.Resolve("balanced_basin"),
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            restored.ApplySnapshot(snapshot);
            Assert.Equal(PrototypeSettlementDirective.Shelter, restored.ActiveDirective);
            Assert.Equal(1, restored.CaptureTelemetrySnapshot().DirectiveChanges);
        }

        [Fact]
        public void Initialize_ResetsDirectiveToNeutral()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.SetDirective(PrototypeSettlementDirective.FoodAndFuel);

            session.Initialize(8.0f);

            Assert.Equal(PrototypeSettlementDirective.Neutral, session.ActiveDirective);
            Assert.Equal("neutral", session.CaptureDirectiveSnapshot().DirectiveId);
        }

        [Fact]
        public void RestoredMaximumDirectiveCounterRejectsNextChangeWithoutMutation()
        {
            PrototypeRuntimeSession source = CreateSession();
            PrototypeRuntimeSnapshot snapshot = source.CaptureSnapshot(Vector3.Zero);
            snapshot.Directive!.DirectiveId = "shelter";
            snapshot.Telemetry!.FirstDirectiveTick = snapshot.SimulationTick;
            snapshot.Telemetry.DirectiveChanges = int.MaxValue;
            snapshot.Telemetry.FinalDirectiveId = "shelter";
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession restored = new(
                bundle.Scenarios.Resolve("balanced_basin"),
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            restored.ApplySnapshot(snapshot);
            int eventsBefore = restored.EventLog.Entries.Count;

            PrototypeDirectiveChangeResult result =
                restored.SetDirective(PrototypeSettlementDirective.FoodAndFuel);

            Assert.False(result.Succeeded);
            Assert.False(result.Changed);
            Assert.Equal("counter_overflow", result.FailureReason);
            Assert.Equal(PrototypeSettlementDirective.Shelter, restored.ActiveDirective);
            Assert.Equal(int.MaxValue, restored.CaptureTelemetrySnapshot().DirectiveChanges);
            Assert.Equal(eventsBefore, restored.EventLog.Entries.Count);
        }

        private static PrototypeRuntimeSession CreateSession()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            PrototypeRuntimeSession session = new(
                scenario,
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            session.Initialize(8.0f);
            return session;
        }

        private static PrototypeCatalogBundle LoadCatalogs()
        {
            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, "src", "societies", "data");
                if (Directory.Exists(candidate))
                {
                    return PrototypeCatalogLoader.LoadFromDirectory(candidate);
                }

                current = Directory.GetParent(current)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not find src/societies/data.");
        }
    }
}
