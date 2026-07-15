using Godot;
using Societies.Simulation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Societies.Core.Tests
{
    public class PrototypeContributionTests
    {
        [Fact]
        public void ContributeToStockpile_SucceedsWithExactAtomicTransferEventAndCounter()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Inventory.AddItem("logs", 5);
            int initialStock = session.Stockpile.GetCount("logs");
            int initialEvents = session.EventLog.Entries.Count;

            PrototypeContributionResult result = session.ContributeToStockpile("logs", 3);

            Assert.True(result.Succeeded);
            Assert.Equal(3, result.RequestedQuantity);
            Assert.Equal(3, result.AppliedQuantity);
            Assert.Equal(2, session.Inventory.GetCount("logs"));
            Assert.Equal(initialStock + 3, session.Stockpile.GetCount("logs"));
            Assert.Equal(3, session.ContributionCountsByResource["logs"]);
            PrototypeEventRecord contributionEvent = Assert.Single(session.EventLog.Entries.Skip(initialEvents));
            Assert.Equal(PrototypeEventTypes.PlayerContributionSucceeded, contributionEvent.EventType);
            Assert.Equal(session.SimulationTick, contributionEvent.Tick);
            Assert.Contains("logs x3", contributionEvent.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ContributeToStockpile_InvalidItemDoesNotMutateState()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Inventory.AddItem("unknown_resource", 2);

            AssertFailureWithoutMutation(
                session,
                () => session.ContributeToStockpile("unknown_resource", 1),
                "invalid_item");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void ContributeToStockpile_InvalidAmountDoesNotMutateState(int amount)
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Inventory.AddItem("logs", 2);

            AssertFailureWithoutMutation(
                session,
                () => session.ContributeToStockpile("logs", amount),
                "invalid_amount");
        }

        [Fact]
        public void ContributeToStockpile_InsufficientQuantityDoesNotMutateState()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Inventory.AddItem("stone", 2);

            AssertFailureWithoutMutation(
                session,
                () => session.ContributeToStockpile("stone", 3),
                "insufficient_quantity");
        }

        [Fact]
        public void ContributeToStockpile_UninitializedRuntimeRejectsWithoutMutation()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeRuntimeSession session = new(
                bundle.Scenarios.Resolve("balanced_basin"),
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            session.Inventory.AddItem("logs", 1);

            AssertFailureWithoutMutation(
                session,
                () => session.ContributeToStockpile("logs", 1),
                "runtime_unavailable");
        }

        [Fact]
        public void ContributeAllEligible_EmptyInventoryReturnsStructuredFailure()
        {
            PrototypeRuntimeSession session = CreateSession();
            int initialEvents = session.EventLog.Entries.Count;

            PrototypeContributionBatchResult result = session.ContributeAllEligibleToStockpile();

            Assert.False(result.Succeeded);
            Assert.Equal("empty_inventory", result.FailureReason);
            Assert.Empty(result.Results);
            Assert.Equal(0, result.AppliedQuantity);
            Assert.Equal(initialEvents, session.EventLog.Entries.Count);
            Assert.Empty(session.ContributionCountsByResource);
        }

        [Fact]
        public void ContributeAllEligible_KeepsCraftedToolsPersonal()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Inventory.AddItem("stone_axe", 1);
            int initialStock = session.Stockpile.Items.Values.Sum();

            PrototypeContributionBatchResult result = session.ContributeAllEligibleToStockpile();

            Assert.False(result.Succeeded);
            Assert.Equal("no_eligible_resources", result.FailureReason);
            Assert.Equal(1, session.Inventory.GetCount("stone_axe"));
            Assert.Equal(initialStock, session.Stockpile.Items.Values.Sum());
            Assert.Empty(session.ContributionCountsByResource);
        }

        [Fact]
        public void ContributeAllEligible_DepositsEveryRawResourceInOrdinalOrderOnce()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Inventory.AddItem("stone", 2);
            session.Inventory.AddItem("stone_axe", 1);
            session.Inventory.AddItem("berries", 4);
            int initialBerries = session.Stockpile.GetCount("berries");
            int initialStone = session.Stockpile.GetCount("stone");

            PrototypeContributionBatchResult result = session.ContributeAllEligibleToStockpile();

            Assert.True(result.Succeeded);
            Assert.Equal(6, result.AppliedQuantity);
            Assert.Equal(new[] { "berries", "stone" }, result.Results.Select(item => item.ResourceId));
            Assert.Equal(0, session.Inventory.GetCount("berries"));
            Assert.Equal(0, session.Inventory.GetCount("stone"));
            Assert.Equal(1, session.Inventory.GetCount("stone_axe"));
            Assert.Equal(initialBerries + 4, session.Stockpile.GetCount("berries"));
            Assert.Equal(initialStone + 2, session.Stockpile.GetCount("stone"));
            Assert.Equal(4, session.ContributionCountsByResource["berries"]);
            Assert.Equal(2, session.ContributionCountsByResource["stone"]);
        }

        [Fact]
        public void ContributeAllEligible_FullDepotRejectsWholeBatchWithoutPartialMutation()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            scenario.StartingStock = new Dictionary<string, int> { ["meals"] = 120 };
            PrototypeRuntimeSession session = CreateSession(bundle, scenario);
            session.Inventory.AddItem("berries", 2);
            session.Inventory.AddItem("logs", 3);
            Dictionary<string, int> initialInventory = new(session.Inventory.Items);
            Dictionary<string, int> initialStockpile = new(session.Stockpile.Items);
            int initialEvents = session.EventLog.Entries.Count;

            PrototypeContributionBatchResult result = session.ContributeAllEligibleToStockpile();

            Assert.False(result.Succeeded);
            Assert.Equal("stockpile_rejected", result.FailureReason);
            Assert.Equal(initialInventory, session.Inventory.Items);
            Assert.Equal(initialStockpile, session.Stockpile.Items);
            Assert.Equal(initialEvents, session.EventLog.Entries.Count);
            Assert.Empty(session.ContributionCountsByResource);
        }

        [Fact]
        public void ContributionInteraction_RejectsOutOfRangeAndDuplicateFrameWithoutMutation()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Inventory.AddItem("logs", 3);
            PrototypeContributionInteraction interaction = new();
            Dictionary<string, int> initialInventory = new(session.Inventory.Items);
            Dictionary<string, int> initialStockpile = new(session.Stockpile.Items);
            int initialEvents = session.EventLog.Entries.Count;

            PrototypeContributionBatchResult outOfRange = interaction.Execute(
                session,
                session.CentralDepotPosition + new Vector3(100.0f, 0.0f, 0.0f),
                4.5f,
                10);
            PrototypeContributionBatchResult duplicate = interaction.Execute(
                session,
                session.CentralDepotPosition,
                4.5f,
                10);

            Assert.Equal("out_of_range", outOfRange.FailureReason);
            Assert.Equal("duplicate_input", duplicate.FailureReason);
            Assert.Equal(initialInventory, session.Inventory.Items);
            Assert.Equal(initialStockpile, session.Stockpile.Items);
            Assert.Equal(initialEvents, session.EventLog.Entries.Count);
            Assert.Empty(session.ContributionCountsByResource);
        }

        [Fact]
        public void ContributionInteraction_RepeatedSuccessfulFrameTransfersOnlyOnce()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Inventory.AddItem("reeds", 3);
            PrototypeContributionInteraction interaction = new();
            int initialStock = session.Stockpile.GetCount("reeds");

            PrototypeContributionBatchResult first = interaction.Execute(
                session,
                session.CentralDepotPosition,
                4.5f,
                42);
            PrototypeContributionBatchResult duplicate = interaction.Execute(
                session,
                session.CentralDepotPosition,
                4.5f,
                42);

            Assert.True(first.Succeeded);
            Assert.Equal("duplicate_input", duplicate.FailureReason);
            Assert.Equal(0, session.Inventory.GetCount("reeds"));
            Assert.Equal(initialStock + 3, session.Stockpile.GetCount("reeds"));
            Assert.Equal(3, session.ContributionCountsByResource["reeds"]);
            Assert.Single(session.EventLog.Entries.Where(entry =>
                entry.EventType == PrototypeEventTypes.PlayerContributionSucceeded));
        }

        [Fact]
        public void ContributionResultsEventsAndCountersAreDeterministic()
        {
            PrototypeRuntimeSession first = CreateSession();
            PrototypeRuntimeSession second = CreateSession();
            first.Inventory.AddItem("stone", 2);
            first.Inventory.AddItem("berries", 3);
            second.Inventory.AddItem("berries", 3);
            second.Inventory.AddItem("stone", 2);

            PrototypeContributionBatchResult firstResult = first.ContributeAllEligibleToStockpile();
            PrototypeContributionBatchResult secondResult = second.ContributeAllEligibleToStockpile();

            Assert.Equal(firstResult.Results, secondResult.Results);
            Assert.Equal(first.ContributionCountsByResource, second.ContributionCountsByResource);
            Assert.Equal(
                first.EventLog.Entries.Select(entry => (entry.Tick, entry.EventType, entry.Message)),
                second.EventLog.Entries.Select(entry => (entry.Tick, entry.EventType, entry.Message)));
        }

        [Fact]
        public void ContributedResourcesEnterNormalCitizenConsumptionPath()
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            PrototypeScenarioDefinition scenario = bundle.Scenarios.Resolve("balanced_basin");
            scenario.InitialCitizens = 1;
            scenario.InitialTrees = 0;
            scenario.InitialRocks = 0;
            scenario.InitialBerryBushes = 0;
            scenario.InitialClayDeposits = 0;
            scenario.InitialReedBeds = 0;
            scenario.StartingStock = new Dictionary<string, int>();
            PrototypeRuntimeSession session = CreateSession(bundle, scenario);
            session.Inventory.AddItem("logs", 8);

            Assert.True(session.ContributeToStockpile("logs", 8).Succeeded);
            for (int tick = 0; tick < 1200 && session.ConsumedResources.GetValueOrDefault("logs") == 0; tick++)
            {
                _ = session.Advance((float)PrototypeSimulationTime.TickIntervalSeconds, 600.0f);
            }

            Assert.True(session.ConsumedResources.GetValueOrDefault("logs") > 0);
            Assert.Contains(session.Structures, structure =>
                structure.InputStore.GetCount("logs") > 0 || structure.OutputStore.GetCount("firewood") > 0);
        }

        [Fact]
        public void ContributionStateFailsFastAtSchemaV6WhileUntouchedLegacySessionStillRoundTrips()
        {
            PrototypeRuntimeSession contributed = CreateSession();
            contributed.Inventory.AddItem("clay", 2);
            Assert.True(contributed.ContributeToStockpile("clay", 2).Succeeded);

            Assert.False(contributed.SupportsRuntimeSnapshotPersistence);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                contributed.CaptureSnapshot(Vector3.Zero));
            Assert.Contains("contribution state", exception.Message, StringComparison.Ordinal);
            Assert.Contains("schema v7", exception.Message, StringComparison.Ordinal);

            PrototypeRuntimeSession untouched = CreateSession();
            PrototypeRuntimeSnapshot snapshot = untouched.CaptureSnapshot(Vector3.Zero);
            PrototypeRuntimeSession restored = CreateSession(initialize: false);
            restored.ApplySnapshot(snapshot);
            Assert.Equal(snapshot.Stockpile, restored.Stockpile.Items);
            Assert.Empty(restored.ContributionCountsByResource);
            Assert.True(restored.SupportsRuntimeSnapshotPersistence);
        }

        private static void AssertFailureWithoutMutation(
            PrototypeRuntimeSession session,
            Func<PrototypeContributionResult> command,
            string expectedReason)
        {
            Dictionary<string, int> initialInventory = new(session.Inventory.Items);
            Dictionary<string, int> initialStockpile = new(session.Stockpile.Items);
            int initialEvents = session.EventLog.Entries.Count;
            Dictionary<string, long> initialCounters = new(session.ContributionCountsByResource);

            PrototypeContributionResult result = command();

            Assert.False(result.Succeeded);
            Assert.Equal(expectedReason, result.FailureReason);
            Assert.Equal(0, result.AppliedQuantity);
            Assert.Equal(initialInventory, session.Inventory.Items);
            Assert.Equal(initialStockpile, session.Stockpile.Items);
            Assert.Equal(initialEvents, session.EventLog.Entries.Count);
            Assert.Equal(initialCounters, session.ContributionCountsByResource);
        }

        private static PrototypeRuntimeSession CreateSession(bool initialize = true)
        {
            PrototypeCatalogBundle bundle = LoadCatalogs();
            return CreateSession(bundle, bundle.Scenarios.Resolve("balanced_basin"), initialize);
        }

        private static PrototypeRuntimeSession CreateSession(
            PrototypeCatalogBundle bundle,
            PrototypeScenarioDefinition scenario,
            bool initialize = true)
        {
            PrototypeRuntimeSession session = new(
                scenario,
                bundle.RoleQuotas.Roles,
                resourceDefinitions: bundle.Resources.Resources);
            if (initialize)
            {
                session.Initialize(8.0f);
            }

            return session;
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
    }
}
