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
        public void DirectiveSnapshotContract_IsFrozenWhileSchemaSixFailsFast()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.SetDirective(PrototypeSettlementDirective.Shelter);

            PrototypeDirectiveSnapshot snapshot = session.CaptureDirectiveSnapshot();

            Assert.Equal("shelter", snapshot.DirectiveId);
            Assert.False(session.SupportsRuntimeSnapshotPersistence);
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => session.CaptureSnapshot(Vector3.Zero));
            Assert.Contains("directive state", error.Message, StringComparison.Ordinal);
            Assert.Contains("schema v7", error.Message, StringComparison.Ordinal);
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
