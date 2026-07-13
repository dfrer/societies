using Societies.Core;
using System.Collections.Generic;

namespace Societies.Tests
{
    internal sealed class PerformanceRunConfiguration
    {
        public string ScenarioId { get; set; } = string.Empty;

        public int SimulationSeed { get; set; }

        public int CitizenCount { get; set; }

        public int WarmupTicks { get; set; }

        public int MeasuredTicks { get; set; }

        public bool MetricsEnabled { get; set; }

        public bool AllowSafetyFailure { get; set; }

        public string OutputDirectory { get; set; } = string.Empty;

        public string RunId { get; set; } = string.Empty;

        public string GitSha { get; set; } = string.Empty;

        public bool GitDirty { get; set; }

        public string ExecutionRoute { get; set; } = string.Empty;

        public string ProjectPath { get; set; } = string.Empty;

        public string RunnerExecutablePath { get; set; } = string.Empty;

        public string CacheMode { get; set; } = string.Empty;

        public string SelectorMode { get; set; } = string.Empty;

        public string ExtractionPlanningMode { get; set; } = string.Empty;

        public string ComparisonGroup { get; set; } = string.Empty;

        public int TrialIndex { get; set; }

        public string WarmupMode { get; set; } = "none";

        public bool CacheWarmupEnabled { get; set; }

        public string MeasurementMode { get; set; } = "one_manual_step_per_external_sample";

        public PerformanceBudgetProfile BudgetProfile { get; set; }
    }

    internal sealed class PerformanceRunEnvironment
    {
        public string MachineName { get; set; } = string.Empty;

        public int LogicalProcessorCount { get; set; }

        public string OperatingSystem { get; set; } = string.Empty;

        public string ProcessArchitecture { get; set; } = string.Empty;

        public string DotnetRuntime { get; set; } = string.Empty;

        public string GodotVersion { get; set; } = string.Empty;

        public string ProcessExecutablePath { get; set; } = string.Empty;

        public string ManagedBuildConfiguration { get; set; } = string.Empty;

        public string ManagedAssemblyConfiguration { get; set; } = string.Empty;

        public bool GodotDebugBuild { get; set; }

        public bool GodotReleaseFeature { get; set; }

        public bool GodotTemplateFeature { get; set; }

        public bool GodotEditorFeature { get; set; }

        public bool VerifiedReleaseExecution { get; set; }
    }

    internal sealed class PerformanceRunIntervals
    {
        public double SceneSetupMilliseconds { get; set; }

        public double BootstrapMilliseconds { get; set; }

        public double WarmupMilliseconds { get; set; }

        public double CachePreparationMilliseconds { get; set; }

        public double MeasuredTicksMilliseconds { get; set; }

        public double CoreArtifactSerializationMilliseconds { get; set; }

        public string SceneSetupBoundary { get; set; } = string.Empty;

        public string BootstrapBoundary { get; set; } = string.Empty;

        public string WarmupBoundary { get; set; } = string.Empty;

        public string CachePreparationBoundary { get; set; } = string.Empty;

        public string MeasurementBoundary { get; set; } = string.Empty;

        public string ArtifactBoundary { get; set; } = string.Empty;
    }

    internal sealed class PerformancePhaseSummary
    {
        public double SimulationTickMilliseconds { get; set; }

        public double SessionAdvanceMilliseconds { get; set; }

        public double BuildWorkOrdersMilliseconds { get; set; }

        public double HarvestApplyMilliseconds { get; set; }

        public double SceneSyncMilliseconds { get; set; }

        public double UpdateHudMilliseconds { get; set; }

        public double NavigationRebuildMilliseconds { get; set; }

        public double RouteSelectionMilliseconds { get; set; }
    }

    internal sealed class PerformanceDiagnosticsSummary
    {
        public int BatchCount { get; set; }

        public long DroppedBatchCount { get; set; }

        public long FirstMeasuredBatchPathPlanLookups { get; set; }

        public long FirstMeasuredBatchPathPlanCacheHits { get; set; }

        public long FirstMeasuredBatchPathPlanCacheMisses { get; set; }

        public long FirstMeasuredBatchNavigationInvalidations { get; set; }

        public long PathPlanLookups { get; set; }

        public long PathPlanCacheHits { get; set; }

        public long PathPlanCacheMisses { get; set; }

        public double? PathPlanCacheHitRate { get; set; }

        public int? PathPlanCacheSizeLast { get; set; }

        public int? WorkerCountLast { get; set; }

        public long IdleCitizensConsideringWorkOrders { get; set; }

        public long CandidateOrdersEvaluated { get; set; }

        public double? CandidateOrdersPerIdleCitizen { get; set; }

        public long SelectorCandidatesBounded { get; set; }

        public long SelectorCandidatesExactScored { get; set; }

        public long SelectorCandidatesPruned { get; set; }

        public long SelectorExactPathQueries { get; set; }

        public long SelectorPathCacheHits { get; set; }

        public long SelectorPathCacheMisses { get; set; }

        public double? SelectorPathCacheHitRate { get; set; }

        public long SelectorSelectedRouteReuses { get; set; }

        public long NavigationInvalidations { get; set; }

        public long WorkOrdersGenerated { get; set; }

        public long WorkOrdersGeneratedUncapped { get; set; }

        public long WorkOrdersClaimed { get; set; }

        public int? WorkOrdersRemainingLast { get; set; }

        public PerformancePhaseSummary Phases { get; set; } = new();
    }

    internal sealed class PerformanceRunHashes
    {
        public string SnapshotSha256 { get; set; } = string.Empty;

        public string EventLogSha256 { get; set; } = string.Empty;

        public string DeterministicStateAndEventSha256 { get; set; } = string.Empty;
    }

    internal sealed class PerformanceRunArtifacts
    {
        public string Snapshot { get; set; } = string.Empty;

        public string EventLog { get; set; } = string.Empty;

        public string RunSummary { get; set; } = string.Empty;

        public string WorldSummary { get; set; } = string.Empty;

        public string DeterministicMetricsCsv { get; set; } = string.Empty;

        public string? RuntimeMetricsCsv { get; set; }

        public string PerformanceResults { get; set; } = string.Empty;

        public string PerformanceMatrix { get; set; } = string.Empty;

        public string PerformanceSummary { get; set; } = string.Empty;

        public string ValidationManifest { get; set; } = string.Empty;
    }

    internal sealed class PerformanceCacheEvidence
    {
        public string CacheMode { get; set; } = string.Empty;

        public string PreparationStrategy { get; set; } = string.Empty;

        public int ClearedEntryCount { get; set; }

        public string PreparedForcedPathSegmentStructureId { get; set; } = string.Empty;

        public PrototypePerformanceProbeSnapshot AfterBootstrap { get; set; }

        public PrototypePerformanceProbeSnapshot AfterNaturalWarmup { get; set; }

        public PrototypePerformanceProbeSnapshot BeforeMeasurement { get; set; }

        public PrototypePerformanceProbeSnapshot AfterMeasurement { get; set; }

        public PerformanceForcedInvalidationEvidence? ForcedInvalidation { get; set; }
    }

    internal sealed class PerformanceForcedInvalidationEvidence
    {
        public long NavigationInvalidationCount { get; set; }

        public PrototypeForcedInvalidationProbeSnapshot Probe { get; set; }
    }

    internal sealed class PerformanceRunResult
    {
        public int SchemaVersion { get; set; } = 5;

        public string CapturedUtc { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string AssessmentScope { get; set; } = "single_run_indicator_not_median_of_three_gate";

        public string ExactInvocation { get; set; } = string.Empty;

        public PerformanceRunConfiguration Configuration { get; set; } = new();

        public PerformanceRunEnvironment Environment { get; set; } = new();

        public PerformanceRunIntervals Intervals { get; set; } = new();

        public PerformanceCacheEvidence CacheEvidence { get; set; } = new();

        public PerformanceSampleStatistics ExternalTickStatistics { get; set; }

        public PerformanceSampleStatistics? InternalTickStatistics { get; set; }

        public PerformanceDiagnosticsSummary? Diagnostics { get; set; }

        public PerformanceBudgetAssessment Budget { get; set; }

        public long MeasuredStartSimulationTick { get; set; }

        public long FinalSimulationTick { get; set; }

        public PerformanceRunHashes Hashes { get; set; } = new();

        public PerformanceRunArtifacts Artifacts { get; set; } = new();

        public List<string> Notes { get; set; } = new();
    }

    internal sealed class PerformanceValidationManifest
    {
        public int SchemaVersion { get; set; } = 5;

        public string CapturedUtc { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string ExactInvocation { get; set; } = string.Empty;

        public string AssessmentScope { get; set; } = string.Empty;

        public string GitSha { get; set; } = string.Empty;

        public bool GitDirty { get; set; }

        public PerformanceRunConfiguration Configuration { get; set; } = new();

        public PerformanceRunEnvironment Environment { get; set; } = new();

        public PerformanceRunIntervals Intervals { get; set; } = new();

        public PerformanceCacheEvidence CacheEvidence { get; set; } = new();

        public PerformanceRunHashes Hashes { get; set; } = new();

        public PerformanceBudgetAssessment Budget { get; set; }

        public PerformanceRunArtifacts Artifacts { get; set; } = new();
    }

    internal sealed class PerformanceFailureResult
    {
        public int SchemaVersion { get; set; } = 5;

        public string CapturedUtc { get; set; } = string.Empty;

        public string Status { get; set; } = "failed";

        public string ErrorType { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;

        public string ExactInvocation { get; set; } = string.Empty;

        public PerformanceRunConfiguration? Configuration { get; set; }

        public PerformanceCacheEvidence? CacheEvidence { get; set; }
    }
}
