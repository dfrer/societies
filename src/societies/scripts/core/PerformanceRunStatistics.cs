using System;
using System.Collections.Generic;

namespace Societies.Core
{
    public static class PerformanceExecutionContract
    {
        public const string ExportReleaseAssemblyConfiguration = "ExportRelease";

        public static bool IsVerifiedReleaseExecution(
            string managedAssemblyConfiguration,
            bool godotDebugBuild,
            bool godotReleaseFeature,
            bool godotTemplateFeature,
            bool godotEditorFeature)
        {
            return string.Equals(
                    managedAssemblyConfiguration,
                    ExportReleaseAssemblyConfiguration,
                    StringComparison.Ordinal) &&
                !godotDebugBuild &&
                godotReleaseFeature &&
                godotTemplateFeature &&
                !godotEditorFeature;
        }
    }

    public readonly record struct PerformanceSampleStatistics(
        int Count,
        double MeanMilliseconds,
        double P50Milliseconds,
        double P95Milliseconds,
        double P99Milliseconds,
        double MaximumMilliseconds,
        double TotalMilliseconds);

    public static class PerformanceRunStatistics
    {
        public static PerformanceSampleStatistics Compute(IReadOnlyList<double> samples)
        {
            ArgumentNullException.ThrowIfNull(samples);
            if (samples.Count == 0)
            {
                throw new ArgumentException("At least one performance sample is required.", nameof(samples));
            }

            var sortedSamples = new double[samples.Count];
            double totalMilliseconds = 0.0;
            for (int index = 0; index < samples.Count; index++)
            {
                double sample = samples[index];
                if (!double.IsFinite(sample) || sample < 0.0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(samples),
                        "Performance samples must be finite and non-negative.");
                }

                sortedSamples[index] = sample;
                totalMilliseconds += sample;
                if (!double.IsFinite(totalMilliseconds))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(samples),
                        "The total performance sample duration must be finite.");
                }
            }

            Array.Sort(sortedSamples);
            return new PerformanceSampleStatistics(
                sortedSamples.Length,
                totalMilliseconds / sortedSamples.Length,
                NearestRank(sortedSamples, 0.50),
                NearestRank(sortedSamples, 0.95),
                NearestRank(sortedSamples, 0.99),
                sortedSamples[^1],
                totalMilliseconds);
        }

        private static double NearestRank(IReadOnlyList<double> sortedSamples, double percentile)
        {
            int index = (int)Math.Ceiling(percentile * sortedSamples.Count) - 1;
            return sortedSamples[index];
        }
    }

    public readonly record struct PerformanceBudgetComponentResult(
        double ActualMilliseconds,
        double LimitMilliseconds,
        bool IsApplied,
        bool Passed);

    public readonly record struct PerformanceBudgetAssessment(
        bool? TargetPassed,
        bool? SafetyPassed,
        PerformanceBudgetComponentResult TargetBootstrap,
        PerformanceBudgetComponentResult TargetP95,
        PerformanceBudgetComponentResult TargetP99,
        PerformanceBudgetComponentResult TargetMaximum,
        PerformanceBudgetComponentResult TargetTotal,
        PerformanceBudgetComponentResult SafetyBootstrap,
        PerformanceBudgetComponentResult SafetyP95,
        PerformanceBudgetComponentResult SafetyMaximum)
    {
        public PerformanceBudgetProfile Profile { get; init; } = default;

        public bool MeetsTarget => TargetPassed == true;

        public bool MeetsSafety => SafetyPassed == true;

        public bool HasTargetGate =>
            TargetBootstrap.IsApplied ||
            TargetP95.IsApplied ||
            TargetP99.IsApplied ||
            TargetMaximum.IsApplied ||
            TargetTotal.IsApplied;

        public bool HasSafetyGate =>
            SafetyBootstrap.IsApplied ||
            SafetyP95.IsApplied ||
            SafetyMaximum.IsApplied;
    }

    public enum PerformanceBudgetProfile
    {
        ReleaseReference300,
        ReleaseSoak1000,
        Stress24Characterization,
        DebugCharacterization,
        Characterization,
        MetricsDiagnostic
    }

    public static class PerformanceRunBudgets
    {
        public const double TargetBootstrapMilliseconds = 3000.0;
        public const double TargetP95Milliseconds = 25.0;
        public const double TargetP99Milliseconds = 50.0;
        public const double TargetMaximumMilliseconds = 100.0;
        public const double TargetReferenceTotalMilliseconds = 6000.0;
        public const double SafetyBootstrapMilliseconds = 5000.0;
        public const double SafetyP95Milliseconds = 50.0;
        public const double SafetyMaximumMilliseconds = 250.0;
        public const double StressTargetP95Milliseconds = 50.0;
        public const double StressTargetMaximumMilliseconds = 200.0;

        public static PerformanceBudgetAssessment Evaluate(
            PerformanceSampleStatistics statistics,
            double bootstrapMilliseconds,
            PerformanceBudgetProfile profile)
        {
            if (!double.IsFinite(bootstrapMilliseconds) || bootstrapMilliseconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bootstrapMilliseconds),
                    "Bootstrap duration must be finite and non-negative.");
            }

            ValidateStatistics(statistics);

            bool releaseReference = profile == PerformanceBudgetProfile.ReleaseReference300;
            bool releaseSoak = profile == PerformanceBudgetProfile.ReleaseSoak1000;
            bool stressCharacterization = profile == PerformanceBudgetProfile.Stress24Characterization;

            PerformanceBudgetComponentResult targetBootstrap = releaseReference || releaseSoak
                ? Applied(bootstrapMilliseconds, TargetBootstrapMilliseconds)
                : NotApplied(bootstrapMilliseconds, TargetBootstrapMilliseconds);
            PerformanceBudgetComponentResult targetP95 = releaseReference || releaseSoak
                ? Applied(statistics.P95Milliseconds, TargetP95Milliseconds)
                : stressCharacterization
                    ? Applied(statistics.P95Milliseconds, StressTargetP95Milliseconds)
                    : NotApplied(statistics.P95Milliseconds, TargetP95Milliseconds);
            PerformanceBudgetComponentResult targetP99 = releaseReference
                ? Applied(statistics.P99Milliseconds, TargetP99Milliseconds)
                : NotApplied(statistics.P99Milliseconds, TargetP99Milliseconds);
            PerformanceBudgetComponentResult targetMaximum = releaseReference
                ? Applied(statistics.MaximumMilliseconds, TargetMaximumMilliseconds)
                : stressCharacterization
                    ? Applied(statistics.MaximumMilliseconds, StressTargetMaximumMilliseconds)
                    : NotApplied(statistics.MaximumMilliseconds, TargetMaximumMilliseconds);
            PerformanceBudgetComponentResult targetTotal = releaseReference
                ? Applied(statistics.TotalMilliseconds, TargetReferenceTotalMilliseconds)
                : NotApplied(statistics.TotalMilliseconds, TargetReferenceTotalMilliseconds);

            PerformanceBudgetComponentResult safetyBootstrap = releaseReference || releaseSoak
                ? Applied(bootstrapMilliseconds, SafetyBootstrapMilliseconds)
                : NotApplied(bootstrapMilliseconds, SafetyBootstrapMilliseconds);
            PerformanceBudgetComponentResult safetyP95 = releaseReference || releaseSoak
                ? Applied(statistics.P95Milliseconds, SafetyP95Milliseconds)
                : NotApplied(statistics.P95Milliseconds, SafetyP95Milliseconds);
            PerformanceBudgetComponentResult safetyMaximum = releaseReference || releaseSoak
                ? Applied(statistics.MaximumMilliseconds, SafetyMaximumMilliseconds)
                : NotApplied(statistics.MaximumMilliseconds, SafetyMaximumMilliseconds);

            bool hasTargetGate =
                targetBootstrap.IsApplied ||
                targetP95.IsApplied ||
                targetP99.IsApplied ||
                targetMaximum.IsApplied ||
                targetTotal.IsApplied;
            bool hasSafetyGate = safetyBootstrap.IsApplied || safetyP95.IsApplied || safetyMaximum.IsApplied;
            bool? targetPassed = hasTargetGate
                ? targetBootstrap.Passed &&
                    targetP95.Passed &&
                    targetP99.Passed &&
                    targetMaximum.Passed &&
                    targetTotal.Passed
                : null;
            bool? safetyPassed = hasSafetyGate
                ? safetyBootstrap.Passed && safetyP95.Passed && safetyMaximum.Passed
                : null;

            return new PerformanceBudgetAssessment(
                targetPassed,
                safetyPassed,
                targetBootstrap,
                targetP95,
                targetP99,
                targetMaximum,
                targetTotal,
                safetyBootstrap,
                safetyP95,
                safetyMaximum)
            {
                Profile = profile
            };
        }

        public static PerformanceBudgetAssessment EvaluateForRun(
            PerformanceSampleStatistics statistics,
            double bootstrapMilliseconds,
            string scenarioId,
            int simulationSeed,
            int citizenCount,
            int requestedTicks,
            bool metricsEnabled,
            bool releaseBuild)
        {
            if (statistics.Count != requestedTicks)
            {
                throw new ArgumentException(
                    $"Statistics contain {statistics.Count} samples but the run requested {requestedTicks} ticks.",
                    nameof(statistics));
            }

            PerformanceBudgetProfile profile = ResolveProfile(
                scenarioId,
                simulationSeed,
                citizenCount,
                requestedTicks,
                metricsEnabled,
                releaseBuild);
            return Evaluate(statistics, bootstrapMilliseconds, profile);
        }

        public static PerformanceBudgetProfile ResolveProfile(
            string scenarioId,
            int simulationSeed,
            int citizenCount,
            int requestedTicks,
            bool metricsEnabled,
            bool releaseBuild)
        {
            if (metricsEnabled)
            {
                return PerformanceBudgetProfile.MetricsDiagnostic;
            }

            if (!releaseBuild)
            {
                return PerformanceBudgetProfile.DebugCharacterization;
            }

            bool benchmarkIdentity =
                string.Equals(scenarioId, "balanced_basin", StringComparison.Ordinal) &&
                simulationSeed == 1337;
            bool releaseIdentity = benchmarkIdentity && citizenCount == 16;
            if (releaseIdentity && requestedTicks == 300)
            {
                return PerformanceBudgetProfile.ReleaseReference300;
            }

            if (releaseIdentity && requestedTicks == 1000)
            {
                return PerformanceBudgetProfile.ReleaseSoak1000;
            }

            if (benchmarkIdentity && citizenCount == 24 && requestedTicks == 300)
            {
                return PerformanceBudgetProfile.Stress24Characterization;
            }

            return PerformanceBudgetProfile.Characterization;
        }

        private static PerformanceBudgetComponentResult Applied(double actualMilliseconds, double limitMilliseconds)
        {
            return new PerformanceBudgetComponentResult(
                actualMilliseconds,
                limitMilliseconds,
                IsApplied: true,
                Passed: actualMilliseconds <= limitMilliseconds);
        }

        private static PerformanceBudgetComponentResult NotApplied(double actualMilliseconds, double limitMilliseconds)
        {
            return new PerformanceBudgetComponentResult(
                actualMilliseconds,
                limitMilliseconds,
                IsApplied: false,
                Passed: true);
        }

        private static void ValidateStatistics(PerformanceSampleStatistics statistics)
        {
            if (statistics.Count <= 0 ||
                !IsValidDuration(statistics.MeanMilliseconds) ||
                !IsValidDuration(statistics.P50Milliseconds) ||
                !IsValidDuration(statistics.P95Milliseconds) ||
                !IsValidDuration(statistics.P99Milliseconds) ||
                !IsValidDuration(statistics.MaximumMilliseconds) ||
                !IsValidDuration(statistics.TotalMilliseconds))
            {
                throw new ArgumentException("Performance statistics must contain valid finite durations.", nameof(statistics));
            }
        }

        private static bool IsValidDuration(double value) => double.IsFinite(value) && value >= 0.0;
    }
}
