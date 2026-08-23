namespace Societies.SnowGlobe;

public static class SnowGlobeScenario
{
    public const int FixedSeed = 240815;
    public const int FixedAgentCount = 8;
    public const int FixedTicks = 4;

    public static async Task<(SnowGlobeWorld World, SnowGlobeRunResult Result)> RunFixedSeedAsync(CancellationToken cancellationToken = default)
    {
        SnowGlobeWorld world = SnowGlobeWorld.Create(FixedSeed, FixedAgentCount);
        SequentialInferenceScheduler scheduler = new(new ScriptedInferenceAdapter());
        SnowGlobeRunResult result = await scheduler.RunAsync(world, FixedTicks, cancellationToken);
        return (world, result);
    }

    public static SnowGlobeWorld ReplayFixedSeed(IEnumerable<SnowGlobeEvent> eventLog)
    {
        SnowGlobeWorld replayed = SnowGlobeWorld.Create(FixedSeed, FixedAgentCount);
        foreach (SnowGlobeEvent entry in eventLog.OrderBy(item => item.Sequence))
        {
            replayed.Replay(entry);
        }

        // The event stream records commits, not empty queue turns. The fixed scenario's
        // run boundary includes the completed fourth tick after its final maintenance event.
        while (replayed.Tick < FixedTicks)
        {
            replayed.AdvanceTick();
        }

        return replayed;
    }
}
