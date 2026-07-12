namespace Societies.Core
{
    public readonly record struct PrototypePerformanceProbeSnapshot(
        int PathCacheEntryCount,
        long SimulationTick,
        int NavigationRulesVersion,
        bool AllPathCacheKeysMatchNavigationRulesVersion,
        long TotalNavigationInvalidations,
        int? LastPathPlanRulesVersion,
        PrototypeForcedInvalidationProbeSnapshot ForcedInvalidation);

    public readonly record struct PrototypeForcedInvalidationProbeSnapshot(
        bool Prepared,
        bool Committed,
        string PathSegmentStructureId,
        bool PathSegmentWasBuiltBefore,
        bool PathSegmentIsBuiltAfter,
        int? ChangedCellGridX,
        int? ChangedCellGridY,
        long? CompletionTick,
        int? VersionBeforeCommit,
        int? VersionAfterCommit,
        long? TotalInvalidationsBeforeCommit,
        long? TotalInvalidationsAfterCommit,
        int? CacheEntriesBeforeRebuild,
        int? CacheEntriesImmediatelyAfterRebuild,
        bool FirstPostChangeLookupObserved,
        bool FirstPostChangeLookupWasCacheMiss,
        bool FirstPostChangeLookupUsedNewVersion,
        int? ProbeStartGridX,
        int? ProbeStartGridY,
        int? ProbeEndGridX,
        int? ProbeEndGridY,
        int? PreChangeQueryVersion,
        int? PreChangePlanVersion,
        int? PostChangeQueryVersion,
        int? PostChangePlanVersion,
        bool ExactEndpointsMatch,
        bool ChangedCellIncludedInPostChangePlan,
        double? PreChangePlanCost,
        double? PostChangePlanCost,
        double? CommitToFirstLookupMilliseconds);
}
