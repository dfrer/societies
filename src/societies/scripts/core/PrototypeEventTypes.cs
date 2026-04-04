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
    }
}
