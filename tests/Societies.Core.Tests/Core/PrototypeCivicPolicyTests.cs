using Godot;
using Societies.Simulation;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypeCivicPolicyTests
    {
        [Fact]
        public void SessionInitializesWithNeutralVersionZeroPolicy()
        {
            PrototypeRuntimeSession session = CreateSession();

            AssertNeutral(session.CivicPolicy);
            Assert.Empty(session.EventLog.Entries.Where(IsCivicEvent));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1200)]
        public void InclusiveWindowBoundariesAcceptExactlyOneChoice(long tick)
        {
            PrototypeRuntimeSession session = CreateSessionAtTick(tick);

            PrototypeCivicPolicyCommandResult result = session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland,
                ExpectedVersion: 0,
                IssuedTick: tick));

            Assert.True(result.Succeeded);
            Assert.Equal(string.Empty, result.FailureReason);
            Assert.Equal("protect_wetland", result.State.PolicyId);
            Assert.Equal(tick, result.State.SelectedTick);
            Assert.Equal(1, result.State.Version);
            PrototypeEventRecord selected = Assert.Single(session.EventLog.Entries.Where(IsCivicEvent));
            Assert.Equal(tick, selected.Tick);
            Assert.Equal("Civic policy selected: protect_wetland", selected.Message);
        }

        [Fact]
        public void TickAfterWindowRejectsWithoutMutationOrEvent()
        {
            PrototypeRuntimeSession session = CreateSessionAtTick(1201);

            AssertFailureWithoutMutation(
                session,
                new PrototypeCivicPolicyCommand(
                    PrototypeCivicPolicy.DrawDownWetland,
                    ExpectedVersion: 0,
                    IssuedTick: 1201),
                "outside_selection_window");
        }

        [Theory]
        [InlineData(-1, 0, "stale_tick")]
        [InlineData(1, 0, "stale_tick")]
        [InlineData(0, 1, "stale_version")]
        public void StaleTickAndVersionRejectWithoutMutationOrEvent(
            long issuedTick,
            int expectedVersion,
            string expectedReason)
        {
            PrototypeRuntimeSession session = CreateSession();

            AssertFailureWithoutMutation(
                session,
                new PrototypeCivicPolicyCommand(
                    PrototypeCivicPolicy.ProtectWetland,
                    expectedVersion,
                    issuedTick),
                expectedReason);
        }

        [Theory]
        [InlineData(PrototypeCivicPolicy.Neutral, "neutral_policy")]
        [InlineData((PrototypeCivicPolicy)99, "invalid_policy")]
        public void NeutralAndUndefinedPoliciesRejectWithoutMutationOrEvent(
            PrototypeCivicPolicy policy,
            string expectedReason)
        {
            PrototypeRuntimeSession session = CreateSession();

            AssertFailureWithoutMutation(
                session,
                new PrototypeCivicPolicyCommand(policy, ExpectedVersion: 0, IssuedTick: 0),
                expectedReason);
        }

        [Theory]
        [InlineData(PrototypeCivicPolicy.ProtectWetland)]
        [InlineData(PrototypeCivicPolicy.DrawDownWetland)]
        public void SameOrDifferentSecondChoiceRejectsIrreversibly(
            PrototypeCivicPolicy secondPolicy)
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            PrototypeCivicPolicySnapshot before = session.CivicPolicy;
            int eventCount = session.EventLog.Entries.Count;

            PrototypeCivicPolicyCommandResult result = session.SelectCivicPolicy(new(
                secondPolicy,
                ExpectedVersion: 1,
                IssuedTick: 0));

            Assert.False(result.Succeeded);
            Assert.Equal("already_selected", result.FailureReason);
            AssertCivicPolicyEqual(before, result.State);
            AssertCivicPolicyEqual(before, session.CivicPolicy);
            Assert.Equal(eventCount, session.EventLog.Entries.Count);
        }

        [Fact]
        public void CivicDirectiveAndContributionEventsPreserveCommandCallOrder()
        {
            PrototypeRuntimeSession first = CreateSession();
            first.Inventory.AddItem("logs", 1);
            Assert.True(first.SetDirective(PrototypeSettlementDirective.Shelter).Changed);
            Assert.True(first.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            Assert.True(first.ContributeToStockpile("logs", 1).Succeeded);
            Assert.Equal(
                new[]
                {
                    PrototypeEventTypes.SettlementDirectiveChanged,
                    PrototypeEventTypes.CivicPolicySelected,
                    PrototypeEventTypes.PlayerContributionSucceeded
                },
                first.EventLog.Entries.Select(entry => entry.EventType));

            PrototypeRuntimeSession second = CreateSession();
            second.Inventory.AddItem("logs", 1);
            Assert.True(second.ContributeToStockpile("logs", 1).Succeeded);
            Assert.True(second.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            Assert.True(second.SetDirective(PrototypeSettlementDirective.FoodAndFuel).Changed);
            Assert.Equal(
                new[]
                {
                    PrototypeEventTypes.PlayerContributionSucceeded,
                    PrototypeEventTypes.CivicPolicySelected,
                    PrototypeEventTypes.SettlementDirectiveChanged
                },
                second.EventLog.Entries.Select(entry => entry.EventType));
        }

        [Fact]
        public void InitializeResetsSelectedPolicyVersionTickAndEvents()
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);

            session.Initialize(9.0f);

            AssertNeutral(session.CivicPolicy);
            Assert.Empty(session.EventLog.Entries);
        }

        private static void AssertFailureWithoutMutation(
            PrototypeRuntimeSession session,
            PrototypeCivicPolicyCommand command,
            string expectedReason)
        {
            PrototypeCivicPolicySnapshot before = session.CivicPolicy;
            int eventCount = session.EventLog.Entries.Count;

            PrototypeCivicPolicyCommandResult result = session.SelectCivicPolicy(command);

            Assert.False(result.Succeeded);
            Assert.Equal(expectedReason, result.FailureReason);
            AssertCivicPolicyEqual(before, result.State);
            AssertCivicPolicyEqual(before, session.CivicPolicy);
            Assert.Equal(eventCount, session.EventLog.Entries.Count);
        }

        private static void AssertNeutral(PrototypeCivicPolicySnapshot state)
        {
            Assert.Equal("neutral", state.PolicyId);
            Assert.Null(state.SelectedTick);
            Assert.Equal(0, state.Version);
            Assert.Equal(0, state.WindowStartTick);
            Assert.Equal(1200, state.WindowEndTick);
        }

        private static void AssertCivicPolicyEqual(
            PrototypeCivicPolicySnapshot expected,
            PrototypeCivicPolicySnapshot actual)
        {
            Assert.Equal(expected.PolicyId, actual.PolicyId);
            Assert.Equal(expected.SelectedTick, actual.SelectedTick);
            Assert.Equal(expected.Version, actual.Version);
            Assert.Equal(expected.WindowStartTick, actual.WindowStartTick);
            Assert.Equal(expected.WindowEndTick, actual.WindowEndTick);
        }

        private static bool IsCivicEvent(PrototypeEventRecord entry)
        {
            return entry.EventType == PrototypeEventTypes.CivicPolicySelected;
        }

        private static PrototypeRuntimeSession CreateSessionAtTick(long tick)
        {
            PrototypeRuntimeSession session = CreateSession();
            if (tick == 0)
            {
                return session;
            }

            PrototypeRuntimeSnapshot snapshot = session.CaptureSnapshot(Vector3.Zero);
            snapshot.SimulationTick = tick;
            snapshot.Settlement!.TotalTicks = checked((int)tick);
            session.ApplySnapshot(snapshot);
            return session;
        }

        private static PrototypeRuntimeSession CreateSession()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(
                bundle.Scenarios.Resolve("balanced_basin"),
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
