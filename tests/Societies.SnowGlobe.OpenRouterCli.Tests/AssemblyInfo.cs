using Xunit;

// This assembly exercises process-global Windows filesystem and production-state contracts.
// Keep its test classes serialized so one fixture cannot invalidate another fixture's pinned state.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
