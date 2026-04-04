namespace Societies.Core
{
    /// <summary>
    /// Stable event identifiers for prototype validation output.
    /// </summary>
    public static class PrototypeEventTypes
    {
        public const string RuntimeReady = "runtime.ready";
        public const string RuntimeReset = "runtime.reset";
        public const string SessionStarted = "session.started";
        public const string WorldSeeded = "world.seeded";
        public const string WeatherToggled = "world.weather.toggled";
        public const string WeatherShifted = "world.weather.shifted";
        public const string SnapshotSaved = "runtime.snapshot.saved";
        public const string SnapshotLoaded = "runtime.snapshot.loaded";
        public const string PlayerHarvestSucceeded = "player.harvest.succeeded";
        public const string PlayerCraftSucceeded = "player.craft.succeeded";
        public const string PlayerCraftFailed = "player.craft.failed";
        public const string AiTaskAssigned = "ai.task.assigned";
        public const string AiHarvestSucceeded = "ai.harvest.succeeded";
        public const string AiHarvestFailed = "ai.harvest.failed";
        public const string AiDepositCompleted = "ai.deposit.completed";
        public const string AiCraftStarted = "ai.craft.started";
        public const string AiCraftCompleted = "ai.craft.completed";
        public const string AiCraftBlocked = "ai.craft.blocked";
        public const string NeedCritical = "settlement.need.critical";
        public const string SettlementShortage = "settlement.shortage";
        public const string BuildQueueChanged = "build_queue.changed";
        public const string SettlementCacheDeposit = "settlement.cache.deposit";
        public const string SettlementHaulCompleted = "settlement.haul.completed";
        public const string SettlementStructureSupplied = "settlement.structure.supplied";
        public const string SettlementHearthRefueled = "settlement.hearth.refueled";
        public const string SettlementWorkAssigned = "settlement.work.assigned";
        public const string SettlementProcessCompleted = "settlement.process.completed";
        public const string SettlementBuildCompleted = "settlement.build.completed";
        public const string SettlementNeedRecovered = "settlement.need.recovered";
        public const string SettlementBlocked = "settlement.blocked";
        public const string SettlementPathBuilt = "settlement.path.built";
        public const string SettlementRemoteDepotEstablished = "settlement.remote_depot.established";
    }
}
