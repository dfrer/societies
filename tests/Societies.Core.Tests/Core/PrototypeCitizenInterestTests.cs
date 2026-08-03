using Godot;
using Societies.Simulation;
using System.Globalization;
using Xunit;

namespace Societies.Core.Tests
{
    public sealed class PrototypeCitizenInterestTests
    {
        [Theory]
        [InlineData(12.0f, 0.0f, PrototypeCitizenInterestReason.CriticalNutrition, PrototypeCitizenNutritionBand.Critical, PrototypeCitizenFatigueBand.Rested)]
        [InlineData(12.1f, 90.0f, PrototypeCitizenInterestReason.CriticalFatigue, PrototypeCitizenNutritionBand.FoodInsecure, PrototypeCitizenFatigueBand.Exhausted)]
        [InlineData(45.0f, 89.9f, PrototypeCitizenInterestReason.FoodSecurity, PrototypeCitizenNutritionBand.FoodInsecure, PrototypeCitizenFatigueBand.NeedsRecovery)]
        [InlineData(45.1f, 62.0f, PrototypeCitizenInterestReason.RecoveryNeed, PrototypeCitizenNutritionBand.Secure, PrototypeCitizenFatigueBand.NeedsRecovery)]
        [InlineData(45.1f, 61.9f, PrototypeCitizenInterestReason.FutureReedSupply, PrototypeCitizenNutritionBand.Secure, PrototypeCitizenFatigueBand.Rested)]
        public void NeedThresholdsAndPrecedenceAreExplicit(
            float nutrition,
            float fatigue,
            PrototypeCitizenInterestReason expectedReason,
            PrototypeCitizenNutritionBand expectedNutritionBand,
            PrototypeCitizenFatigueBand expectedFatigueBand)
        {
            PrototypeCitizenInterest interest = Evaluate(
                PrototypeCitizenRole.Forager,
                nutrition,
                fatigue,
                PrototypeCivicPolicy.Neutral);

            Assert.Equal(expectedReason, interest.Reason);
            Assert.Equal(expectedNutritionBand, interest.NutritionBand);
            Assert.Equal(expectedFatigueBand, interest.FatigueBand);
            Assert.Equal(
                expectedReason == PrototypeCitizenInterestReason.FutureReedSupply
                    ? PrototypeCivicPolicy.ProtectWetland
                    : PrototypeCivicPolicy.DrawDownWetland,
                interest.PreferredPolicy);
        }

        [Theory]
        [InlineData(PrototypeCitizenRole.Forager, PrototypeCivicPolicy.ProtectWetland, PrototypeCitizenInterestReason.FutureReedSupply, "forager")]
        [InlineData(PrototypeCitizenRole.Generalist, PrototypeCivicPolicy.ProtectWetland, PrototypeCitizenInterestReason.BalancedLongTermSupply, "generalist")]
        [InlineData(PrototypeCitizenRole.Builder, PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.ImmediateShelterSupply, "builder")]
        [InlineData(PrototypeCitizenRole.Logger, PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.ImmediateMaterialSupply, "logger")]
        [InlineData(PrototypeCitizenRole.Mason, PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.ImmediateMaterialSupply, "mason")]
        [InlineData(PrototypeCitizenRole.Hauler, PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.MaterialThroughput, "hauler")]
        [InlineData(PrototypeCitizenRole.Processor, PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestReason.MaterialThroughput, "processor")]
        public void EveryRoleHasTheContractedStablePreference(
            PrototypeCitizenRole role,
            PrototypeCivicPolicy expectedPolicy,
            PrototypeCitizenInterestReason expectedReason,
            string expectedRoleId)
        {
            PrototypeCitizenInterest interest = Evaluate(role, 100.0f, 0.0f, PrototypeCivicPolicy.Neutral);

            Assert.Equal(expectedPolicy, interest.PreferredPolicy);
            Assert.Equal(expectedReason, interest.Reason);
            Assert.Equal(expectedRoleId, interest.RoleId);
            Assert.Equal($"role={expectedRoleId}", interest.Summary);
            Assert.Equal(expectedReason, interest.Reason);
        }

        [Theory]
        [InlineData(PrototypeCivicPolicy.Neutral, PrototypeCitizenInterestPosition.Uncommitted)]
        [InlineData(PrototypeCivicPolicy.ProtectWetland, PrototypeCitizenInterestPosition.Supports)]
        [InlineData(PrototypeCivicPolicy.DrawDownWetland, PrototypeCitizenInterestPosition.Opposes)]
        public void PositionIsRelativeToTheSelectedPolicy(
            PrototypeCivicPolicy selectedPolicy,
            PrototypeCitizenInterestPosition expectedPosition)
        {
            PrototypeCitizenInterest interest = Evaluate(
                PrototypeCitizenRole.Forager,
                100.0f,
                0.0f,
                selectedPolicy);

            Assert.Equal(expectedPosition, interest.Position);
        }

        [Fact]
        public void InterestSummaryCitesTheTriggeringFactAndIsBounded()
        {
            PrototypeCitizenInterest critical = Evaluate(
                PrototypeCitizenRole.Builder,
                12.0f,
                100.0f,
                PrototypeCivicPolicy.ProtectWetland);
            PrototypeCitizenInterest role = Evaluate(
                PrototypeCitizenRole.Builder,
                100.0f,
                0.0f,
                PrototypeCivicPolicy.ProtectWetland);

            Assert.Equal("critical_nutrition", PrototypeCitizenInterestEvaluator.GetReasonCode(critical.Reason));
            Assert.Equal("nutrition=critical", critical.Summary);
            Assert.Equal("role=builder", role.Summary);
            Assert.InRange(critical.Summary.Length, 1, PrototypeCitizenInterestEvaluator.MaximumSummaryLength);
            Assert.InRange(role.Summary.Length, 1, PrototypeCitizenInterestEvaluator.MaximumSummaryLength);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        [InlineData(-0.1f)]
        [InlineData(100.1f)]
        public void InvalidNeedInputsAreRejectedBeforeInterestCreation(float invalidNeed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(
                PrototypeCitizenRole.Forager,
                invalidNeed,
                0.0f,
                PrototypeCivicPolicy.Neutral));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(
                PrototypeCitizenRole.Forager,
                100.0f,
                invalidNeed,
                PrototypeCivicPolicy.Neutral));
        }

        [Fact]
        public void InvalidWorkerFactsMakePolicySelectionAtomic()
        {
            PrototypeRuntimeSession session = CreateSession();
            session.Workers[0].Needs.Nutrition = float.NaN;

            Assert.Throws<ArgumentOutOfRangeException>(() => session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)));

            Assert.Equal("neutral", session.CivicPolicy.PolicyId);
            Assert.Equal(0, session.CivicPolicy.Version);
            Assert.Empty(session.EventLog.Entries);
        }

        [Fact]
        public void AggregateSummaryIsInvariantAndWithinTheEventLimit()
        {
            PrototypeCitizenInterest[] interests =
            {
                Evaluate(PrototypeCitizenRole.Forager, 100.0f, 0.0f, PrototypeCivicPolicy.Neutral),
                Evaluate(PrototypeCitizenRole.Builder, 100.0f, 0.0f, PrototypeCivicPolicy.Neutral)
            };
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-SA");
                string summary = PrototypeCitizenInterestEvaluator.BuildAggregateSummary(interests);

                Assert.Equal(
                    "Civic preferences: protect=1; draw_down=1; reasons=critical_nutrition=0,critical_fatigue=0,food_security=0,recovery_need=0,future_reed_supply=1,balanced_long_term_supply=0,immediate_shelter_supply=1,immediate_material_supply=0,material_throughput=0",
                    summary);
                Assert.InRange(summary.Length, 1, PrototypeRunArtifactManager.MaximumMessageLength);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void BlankWorkerIdentityIsRejected(string workerId)
        {
            Assert.Throws<ArgumentException>(() => PrototypeCitizenInterestEvaluator.Evaluate(
                workerId,
                PrototypeCitizenRole.Forager,
                100.0f,
                0.0f,
                PrototypeCivicPolicy.Neutral));
        }

        [Fact]
        public void DuplicateWorkerIdsAreRejectedWithOrdinalIdentity()
        {
            PrototypeWorkerState first = new()
            {
                WorkerId = "citizen",
                Role = PrototypeCitizenRole.Forager
            };
            PrototypeWorkerState second = new()
            {
                WorkerId = "citizen",
                Role = PrototypeCitizenRole.Builder
            };

            Assert.Throws<InvalidOperationException>(() => PrototypeCitizenInterestEvaluator.Capture(
                new[] { first, second },
                PrototypeCivicPolicy.Neutral));
        }

        [Fact]
        public void NullIdentityAndUndefinedRoleAreRejectedAtThePublicBoundary()
        {
            Assert.Throws<ArgumentException>(() => PrototypeCitizenInterestEvaluator.Evaluate(
                null!,
                PrototypeCitizenRole.Forager,
                100.0f,
                0.0f,
                PrototypeCivicPolicy.Neutral));
            Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate(
                (PrototypeCitizenRole)99,
                100.0f,
                0.0f,
                PrototypeCivicPolicy.Neutral));
        }

        [Fact]
        public void SessionCaptureIsOrdinalSortedAndPureWithConflictingCitizens()
        {
            PrototypeRuntimeSession session = CreateSession();
            PrototypeWorkerState first = session.Workers[0];
            PrototypeWorkerState second = session.Workers[1];
            first.WorkerId = "worker-z";
            first.Role = PrototypeCitizenRole.Forager;
            first.Needs.Nutrition = 100.0f;
            first.Needs.Fatigue = 0.0f;
            second.WorkerId = "worker-a";
            second.Role = PrototypeCitizenRole.Builder;
            second.Needs.Nutrition = 100.0f;
            second.Needs.Fatigue = 0.0f;
            string snapshotBefore = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
            string eventsBefore = PrototypePersistenceService.SerializeEventLog(session.EventLog);

            IReadOnlyList<PrototypeCitizenInterest> firstCapture = session.CaptureCitizenInterests();
            IReadOnlyList<PrototypeCitizenInterest> secondCapture = session.CaptureCitizenInterests();

            Assert.Equal(firstCapture.ToArray(), secondCapture.ToArray());
            Assert.Equal(firstCapture.OrderBy(interest => interest.WorkerId, StringComparer.Ordinal), firstCapture);
            Assert.Equal(PrototypeCivicPolicy.ProtectWetland, firstCapture.Single(interest => interest.WorkerId == "worker-z").PreferredPolicy);
            Assert.Equal(PrototypeCivicPolicy.DrawDownWetland, firstCapture.Single(interest => interest.WorkerId == "worker-a").PreferredPolicy);
            Assert.Equal(snapshotBefore, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(eventsBefore, PrototypePersistenceService.SerializeEventLog(session.EventLog));
        }

        [Fact]
        public void SnapshotRestoreRecomputesTheSameInterestsWithoutSchemaChange()
        {
            PrototypeRuntimeSession source = CreateSession();
            source.Workers[0].Role = PrototypeCitizenRole.Forager;
            source.Workers[0].Needs.Nutrition = 100.0f;
            source.Workers[1].Role = PrototypeCitizenRole.Builder;
            source.Workers[1].Needs.Nutrition = 12.0f;
            Assert.True(source.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            PrototypeRuntimeSnapshot snapshot = source.CaptureSnapshot(Vector3.Zero);
            PrototypeRuntimeSession restored = CreateSession();

            restored.ApplySnapshot(PrototypePersistenceService.DeserializeSnapshot(
                PrototypePersistenceService.SerializeSnapshot(snapshot)));

            Assert.Equal(8, snapshot.SchemaVersion);
            Assert.Equal(
                source.CaptureCitizenInterests().ToArray(),
                restored.CaptureCitizenInterests().ToArray());
            Assert.Equal(PrototypeCitizenInterestPosition.Supports,
                restored.CaptureCitizenInterests().Single(interest =>
                    interest.WorkerId == source.Workers[0].WorkerId).Position);
            Assert.Equal(PrototypeCitizenInterestPosition.Opposes,
                restored.CaptureCitizenInterests().Single(interest =>
                    interest.WorkerId == source.Workers[1].WorkerId).Position);
        }

        [Fact]
        public void SuccessfulSelectionRecordsOrderedBoundedAggregateWithoutPerCitizenEvents()
        {
            PrototypeRuntimeSession session = CreateSession();
            foreach (PrototypeWorkerState worker in session.Workers)
            {
                worker.Role = PrototypeCitizenRole.Forager;
                worker.Needs.Nutrition = 100.0f;
                worker.Needs.Fatigue = 0.0f;
            }

            session.Workers[0].Role = PrototypeCitizenRole.Builder;
            string expectedSummary = PrototypeCitizenInterestEvaluator.BuildAggregateSummary(
                session.CaptureCitizenInterests());

            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);

            PrototypeEventRecord[] civicEvents = session.EventLog.Entries.ToArray();
            Assert.Equal(PrototypeEventTypes.CivicPolicySelected, civicEvents[^2].EventType);
            Assert.Equal(PrototypeEventTypes.CivicPreferenceSummary, civicEvents[^1].EventType);
            Assert.Equal(civicEvents[^2].Tick, civicEvents[^1].Tick);
            Assert.Equal(
                expectedSummary,
                civicEvents[^1].Message);
            Assert.True(civicEvents[^1].Message.IndexOf("critical_nutrition=", StringComparison.Ordinal) <
                civicEvents[^1].Message.IndexOf("critical_fatigue=", StringComparison.Ordinal));
            Assert.True(civicEvents[^1].Message.IndexOf("future_reed_supply=", StringComparison.Ordinal) <
                civicEvents[^1].Message.IndexOf("immediate_shelter_supply=", StringComparison.Ordinal));
            Assert.InRange(civicEvents[^1].Message.Length, 1, PrototypeRunArtifactManager.MaximumMessageLength);
            Assert.Equal(2, civicEvents.Count(entry => entry.EventType.StartsWith("civic.", StringComparison.Ordinal)));
        }

        [Fact]
        public void FailedAndRepeatedSelectionEmitNoAdditionalPreferenceEvent()
        {
            PrototypeRuntimeSession session = CreateSession();
            Assert.False(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.Neutral,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            Assert.Empty(session.EventLog.Entries);
            Assert.True(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.DrawDownWetland,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            int eventCount = session.EventLog.Entries.Count;

            Assert.False(session.SelectCivicPolicy(new(
                PrototypeCivicPolicy.ProtectWetland,
                ExpectedVersion: 1,
                IssuedTick: 0)).Succeeded);

            Assert.Equal(eventCount, session.EventLog.Entries.Count);
            Assert.Single(session.EventLog.Entries.Where(entry =>
                entry.EventType == PrototypeEventTypes.CivicPreferenceSummary));
        }

        [Fact]
        public void InterestCaptureDoesNotChangeDirectiveOrCriticalWorkSelectionInputs()
        {
            PrototypeRuntimeSession session = CreateSession();
            PrototypeWorkerState worker = session.Workers[0];
            worker.Needs.Nutrition = 12.0f;
            worker.Needs.Fatigue = 90.0f;
            Assert.True(session.SetDirective(PrototypeSettlementDirective.Shelter).Changed);
            PrototypeDirectiveSnapshot directive = session.CaptureDirectiveSnapshot();
            string snapshotBeforeCapture = PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero));
            string eventsBeforeCapture = PrototypePersistenceService.SerializeEventLog(session.EventLog);
            _ = session.CaptureCitizenInterests();
            _ = session.CaptureCitizenInterests();

            Assert.Equal("shelter", directive.DirectiveId);
            Assert.Equal(directive.DirectiveId, session.CaptureDirectiveSnapshot().DirectiveId);
            Assert.Equal(12.0f, worker.Needs.Nutrition);
            Assert.Equal(90.0f, worker.Needs.Fatigue);
            Assert.Equal(snapshotBeforeCapture, PrototypePersistenceService.SerializeSnapshot(session.CaptureSnapshot(Vector3.Zero)));
            Assert.Equal(eventsBeforeCapture, PrototypePersistenceService.SerializeEventLog(session.EventLog));
            Assert.Equal(PrototypeCitizenInterestReason.CriticalNutrition, session.CaptureCitizenInterests()[0].Reason);
        }

        [Theory]
        [InlineData(PrototypeCivicPolicy.ProtectWetland)]
        [InlineData(PrototypeCivicPolicy.DrawDownWetland)]
        public void CriticalNeedAndRecoveryOrdersAreUnchangedUnderEitherCivicPolicy(
            PrototypeCivicPolicy selectedPolicy)
        {
            PrototypeWorkerState eatWorker = SelectNeedDrivenOrder(selectedPolicy, 12.0f, 0.0f);
            PrototypeWorkerState sleepWorker = SelectNeedDrivenOrder(selectedPolicy, 100.0f, 90.0f);
            PrototypeWorkerState recoveryWorker = SelectNeedDrivenOrder(selectedPolicy, 100.0f, 62.0f);

            Assert.Equal(PrototypeWorkOrderKind.Eat, eatWorker.CurrentOrderKind);
            Assert.Equal("critical nutrition", eatWorker.CurrentOrderReason);
            Assert.Equal(PrototypeWorkOrderKind.Sleep, sleepWorker.CurrentOrderKind);
            Assert.Equal("critical fatigue", sleepWorker.CurrentOrderReason);
            Assert.Equal(PrototypeWorkOrderKind.Sleep, recoveryWorker.CurrentOrderKind);
            Assert.Equal("rest cycle", recoveryWorker.CurrentOrderReason);
        }

        private static PrototypeCitizenInterest Evaluate(
            PrototypeCitizenRole role,
            float nutrition,
            float fatigue,
            PrototypeCivicPolicy selectedPolicy)
        {
            return PrototypeCitizenInterestEvaluator.Evaluate(new PrototypeWorkerState
            {
                WorkerId = "worker",
                Role = role,
                Needs = new PrototypeNeedState { Nutrition = nutrition, Fatigue = fatigue }
            }, selectedPolicy);
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

        private static PrototypeWorkerState SelectNeedDrivenOrder(
            PrototypeCivicPolicy selectedPolicy,
            float nutrition,
            float fatigue)
        {
            PrototypeRuntimeSession session = CreateSession();
            PrototypeWorkerState worker = session.Workers[0];
            foreach (PrototypeWorkerState other in session.Workers.Skip(1))
            {
                other.Phase = PrototypeWorkerPhase.Incapacitated;
            }

            worker.Phase = PrototypeWorkerPhase.Idle;
            worker.Needs.Nutrition = nutrition;
            worker.Needs.Fatigue = fatigue;
            _ = session.CaptureCitizenInterests();
            Assert.True(session.SelectCivicPolicy(new(
                selectedPolicy,
                ExpectedVersion: 0,
                IssuedTick: 0)).Succeeded);
            _ = session.CaptureCitizenInterests();
            _ = session.Advance(1.0f / 20.0f, 600.0f);
            return worker;
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
