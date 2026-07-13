using Societies.Core;
using Xunit;

namespace Societies.Core.Tests
{
    public class PerformanceRunStatisticsTests
    {
        [Fact]
        public void Compute_UsesNearestRankPercentiles()
        {
            double[] samples = Enumerable.Range(1, 100).Select(value => (double)value).Reverse().ToArray();

            PerformanceSampleStatistics statistics = PerformanceRunStatistics.Compute(samples);

            Assert.Equal(100, statistics.Count);
            Assert.Equal(50.5, statistics.MeanMilliseconds);
            Assert.Equal(50.0, statistics.P50Milliseconds);
            Assert.Equal(95.0, statistics.P95Milliseconds);
            Assert.Equal(99.0, statistics.P99Milliseconds);
            Assert.Equal(100.0, statistics.MaximumMilliseconds);
            Assert.Equal(5050.0, statistics.TotalMilliseconds);
        }

        [Fact]
        public void Compute_WithSingleSample_UsesItForEveryStatistic()
        {
            PerformanceSampleStatistics statistics = PerformanceRunStatistics.Compute(new[] { 12.5 });

            Assert.Equal(1, statistics.Count);
            Assert.Equal(12.5, statistics.MeanMilliseconds);
            Assert.Equal(12.5, statistics.P50Milliseconds);
            Assert.Equal(12.5, statistics.P95Milliseconds);
            Assert.Equal(12.5, statistics.P99Milliseconds);
            Assert.Equal(12.5, statistics.MaximumMilliseconds);
            Assert.Equal(12.5, statistics.TotalMilliseconds);
        }

        [Fact]
        public void Compute_RejectsMissingNegativeAndNonFiniteSamples()
        {
            Assert.Throws<ArgumentNullException>(() => PerformanceRunStatistics.Compute(null!));
            Assert.Throws<ArgumentException>(() => PerformanceRunStatistics.Compute(Array.Empty<double>()));
            Assert.Throws<ArgumentOutOfRangeException>(() => PerformanceRunStatistics.Compute(new[] { -0.01 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => PerformanceRunStatistics.Compute(new[] { double.NaN }));
            Assert.Throws<ArgumentOutOfRangeException>(() => PerformanceRunStatistics.Compute(new[] { double.PositiveInfinity }));
        }

        [Fact]
        public void Evaluate_DistinguishesTargetFromSafetyAndRequiresVerifiedReleaseExecution()
        {
            PerformanceSampleStatistics safetyOnlyStatistics = PerformanceRunStatistics.Compute(
                new[] { 20.0, 20.0, 30.0, 30.0, 30.0 });

            PerformanceBudgetAssessment safetyOnly = PerformanceRunBudgets.Evaluate(
                safetyOnlyStatistics,
                bootstrapMilliseconds: 4000.0,
                PerformanceBudgetProfile.ReleaseReference300);

            Assert.False(safetyOnly.TargetPassed);
            Assert.True(safetyOnly.SafetyPassed);
            Assert.False(safetyOnly.TargetBootstrap.Passed);
            Assert.False(safetyOnly.TargetP95.Passed);
            Assert.True(safetyOnly.TargetTotal.IsApplied);
            Assert.True(safetyOnly.TargetTotal.Passed);

            PerformanceSampleStatistics targetStatistics = PerformanceRunStatistics.Compute(
                Enumerable.Repeat(20.0, 300).ToArray());
            PerformanceBudgetAssessment target = PerformanceRunBudgets.Evaluate(
                targetStatistics,
                bootstrapMilliseconds: 3000.0,
                PerformanceBudgetProfile.ReleaseReference300);

            Assert.True(target.TargetPassed);
            Assert.True(target.SafetyPassed);
            Assert.True(target.TargetTotal.IsApplied);
            Assert.True(target.TargetTotal.Passed);
            Assert.Equal(6000.0, target.TargetTotal.ActualMilliseconds);

            PerformanceSampleStatistics unsafeStatistics = PerformanceRunStatistics.Compute(
                new[] { 251.0, 251.0, 251.0 });
            PerformanceBudgetAssessment unsafeResult = PerformanceRunBudgets.Evaluate(
                unsafeStatistics,
                bootstrapMilliseconds: 5001.0,
                PerformanceBudgetProfile.ReleaseReference300);

            Assert.False(unsafeResult.TargetPassed);
            Assert.False(unsafeResult.SafetyPassed);
            Assert.False(unsafeResult.SafetyBootstrap.Passed);
            Assert.False(unsafeResult.SafetyP95.Passed);
            Assert.False(unsafeResult.SafetyMaximum.Passed);

            PerformanceBudgetAssessment soak = PerformanceRunBudgets.Evaluate(
                unsafeStatistics,
                bootstrapMilliseconds: 4000.0,
                PerformanceBudgetProfile.ReleaseSoak1000);
            Assert.True(soak.TargetBootstrap.IsApplied);
            Assert.True(soak.TargetP95.IsApplied);
            Assert.False(soak.TargetP99.IsApplied);
            Assert.False(soak.TargetMaximum.IsApplied);
            Assert.False(soak.TargetTotal.IsApplied);
            Assert.True(soak.HasSafetyGate);

            PerformanceSampleStatistics stressStatistics = PerformanceRunStatistics.Compute(
                new[] { 40.0, 40.0, 199.0 });
            PerformanceBudgetAssessment stress = PerformanceRunBudgets.Evaluate(
                stressStatistics,
                bootstrapMilliseconds: 9000.0,
                PerformanceBudgetProfile.Stress24Characterization);
            Assert.True(stress.TargetP95.IsApplied);
            Assert.Equal(50.0, stress.TargetP95.LimitMilliseconds);
            Assert.True(stress.TargetMaximum.IsApplied);
            Assert.Equal(200.0, stress.TargetMaximum.LimitMilliseconds);
            Assert.False(stress.HasSafetyGate);
            Assert.Null(stress.SafetyPassed);

            PerformanceBudgetAssessment characterization = PerformanceRunBudgets.Evaluate(
                unsafeStatistics,
                bootstrapMilliseconds: 5001.0,
                PerformanceBudgetProfile.Characterization);
            Assert.False(characterization.HasTargetGate);
            Assert.False(characterization.HasSafetyGate);
            Assert.Null(characterization.TargetPassed);
            Assert.Null(characterization.SafetyPassed);

            PerformanceBudgetAssessment diagnostic = PerformanceRunBudgets.EvaluateForRun(
                unsafeStatistics,
                bootstrapMilliseconds: 5001.0,
                scenarioId: "balanced_basin",
                simulationSeed: 1337,
                citizenCount: 16,
                requestedTicks: 3,
                metricsEnabled: true,
                releaseBuild: false);
            Assert.Equal(PerformanceBudgetProfile.MetricsDiagnostic, diagnostic.Profile);
            Assert.Null(diagnostic.TargetPassed);
            Assert.Null(diagnostic.SafetyPassed);

            Assert.Equal(
                PerformanceBudgetProfile.DebugCharacterization,
                PerformanceRunBudgets.ResolveProfile(
                    "balanced_basin",
                    simulationSeed: 1337,
                    citizenCount: 16,
                    requestedTicks: 300,
                    metricsEnabled: false,
                    releaseBuild: false));

            Assert.Equal(
                PerformanceBudgetProfile.Stress24Characterization,
                PerformanceRunBudgets.ResolveProfile(
                    "balanced_basin",
                    simulationSeed: 1337,
                    citizenCount: 24,
                    requestedTicks: 300,
                    metricsEnabled: false,
                    releaseBuild: true));
            Assert.Equal(
                PerformanceBudgetProfile.Characterization,
                PerformanceRunBudgets.ResolveProfile(
                    "ridge_frontier",
                    simulationSeed: 1337,
                    citizenCount: 24,
                    requestedTicks: 300,
                    metricsEnabled: false,
                    releaseBuild: true));
            Assert.Equal(
                PerformanceBudgetProfile.Characterization,
                PerformanceRunBudgets.ResolveProfile(
                    "balanced_basin",
                    simulationSeed: 42,
                    citizenCount: 24,
                    requestedTicks: 300,
                    metricsEnabled: false,
                    releaseBuild: true));

            Assert.True(
                PerformanceExecutionContract.IsVerifiedReleaseExecution(
                    "ExportRelease",
                    godotDebugBuild: false,
                    godotReleaseFeature: true,
                    godotTemplateFeature: true,
                    godotEditorFeature: false));
            Assert.False(
                PerformanceExecutionContract.IsVerifiedReleaseExecution(
                    "Release",
                    godotDebugBuild: false,
                    godotReleaseFeature: true,
                    godotTemplateFeature: true,
                    godotEditorFeature: false));
            Assert.False(
                PerformanceExecutionContract.IsVerifiedReleaseExecution(
                    "ExportRelease",
                    godotDebugBuild: true,
                    godotReleaseFeature: true,
                    godotTemplateFeature: true,
                    godotEditorFeature: false));
            Assert.False(
                PerformanceExecutionContract.IsVerifiedReleaseExecution(
                    "ExportRelease",
                    godotDebugBuild: false,
                    godotReleaseFeature: false,
                    godotTemplateFeature: true,
                    godotEditorFeature: false));
            Assert.False(
                PerformanceExecutionContract.IsVerifiedReleaseExecution(
                    "ExportRelease",
                    godotDebugBuild: false,
                    godotReleaseFeature: true,
                    godotTemplateFeature: false,
                    godotEditorFeature: false));
            Assert.False(
                PerformanceExecutionContract.IsVerifiedReleaseExecution(
                    "ExportRelease",
                    godotDebugBuild: false,
                    godotReleaseFeature: true,
                    godotTemplateFeature: true,
                    godotEditorFeature: true));
        }

        [Theory]
        [InlineData(4L, 4L, 7L, 7L, true)]
        [InlineData(4L, 9L, 7L, 12L, true)]
        [InlineData(9L, 4L, 12L, 7L, false)]
        [InlineData(4L, 9L, 12L, 16L, false)]
        [InlineData(4L, 8L, 12L, 17L, false)]
        public void NaturalNavigationInvalidations_RequireEqualNonnegativeDeltas(
            long navigationVersionBefore,
            long navigationVersionAfter,
            long totalInvalidationsBefore,
            long totalInvalidationsAfter,
            bool expected)
        {
            Assert.Equal(
                expected,
                PerformanceExecutionContract.HasConsistentNaturalNavigationInvalidations(
                    navigationVersionBefore,
                    navigationVersionAfter,
                    totalInvalidationsBefore,
                    totalInvalidationsAfter));
        }
    }
}
