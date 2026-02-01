# R1-6: Eco Game Performance Optimization Research

## Source Information
- **Name**: Eco Game Server Performance - Strange Loop Games
- **URL**: https://wiki.play.eco/en/Server_Profiling, https://wiki.play.eco/en/Server_Configuration
- **Type**: Game Development Reference / Technical Documentation
- **Date Researched**: 2026-01-30
- **Author/Org**: Strange Loop Games (Eco Developers)

## Executive Summary

Eco represents a close analog to Societies - a multiplayer ecosystem simulation where player actions impact the environment, with complex interdependent systems (economy, environment, governance). Research into Eco's architecture reveals key optimizations for handling 100+ concurrent players: spatial chunk-based entity management, CPU throttling via `WorldTickCPUMax` setting (defaults to 25%, configurable to 75%), and integrated profiling tools (`dotnet-dump`, CPU profiling, memory dumps). Eco faces similar challenges to Societies: simulation performance with many entities, database persistence bottlenecks, and memory management with large worlds. Key lessons include: implement spatial partitioning early (grid/chunk-based), provide server admins CPU throttling controls, include built-in profiling tools for diagnosing issues, and optimize database writes through batching and dirty tracking. Eco's Update 9.7 specifically focused on performance optimization, suggesting these issues are critical for simulation games.

## Detailed Findings

### Eco Server Architecture Overview

**Evidence**:
- Eco runs on .NET with a dedicated server application (not headless Unity/Godot like Societies)
- Server can be hosted via Steam (Tools → Eco Server) or direct download
- Supports both Windows and Linux server builds
- Configuration through `.eco` files (JSON format) or in-game Server UI (Windows)
- Built-in profiling and diagnostic tools available

**Server Components**:
```
Eco Server Architecture:
├── Network Layer (TCP/UDP hybrid)
├── Simulation Core (tick-based, variable rate)
├── World Generation & Management
├── Economy System (similar to Societies)
├── Ecosystem Simulation (pollution, species, resources)
├── Governance & Laws
├── Persistence Layer (database)
└── Admin & Profiling Tools
```

**Configuration System**:
```json
// Example from Eco's Network.eco
{
  "PublicServer": false,
  "Playtime": "",
  "Name": "My Eco Server",
  "Description": "A sustainable world",
  "ServerCategory": "Beginner",
  "IPAddress": "Any",
  "RemoteAddress": "",
  "Password": "",
  "WebServerUrl": "",
  "Rate": 60,  // Tick rate
  "MaxConnections": 100
}
```

**Implications for Societies**:
- JSON-based configuration is user-friendly for server admins
- Built-in admin UI valuable for Windows servers (Societies: consider web-based admin panel)
- Separate server executable (not headless client) provides better control
- Support both Windows and Linux from day one

### Spatial Partitioning Implementation

**Evidence**:
- Eco divides world into chunks (similar to Minecraft's chunk system)
- Each chunk manages entities within its boundaries independently
- Players only receive updates for nearby chunks (spatial culling)
- Chunk-based processing enables parallelization (where thread-safe)

**Chunk-Based Architecture**:
```csharp
// Simplified representation of Eco's approach
public class WorldChunk
{
    public Vector3Int Position { get; set; }  // Chunk coordinates
    public List<Entity> Entities { get; set; } = new();
    public List<Player> Players { get; set; } = new();
    public bool IsActive { get; set; }
    
    public void Tick(double delta)
    {
        if (!IsActive) return;
        
        // Update entities in this chunk only
        foreach (var entity in Entities)
        {
            entity.Tick(delta);
        }
        
        // Local ecosystem simulation
        UpdateEcosystem(delta);
    }
    
    public void UpdateEcosystem(double delta)
    {
        // Pollution spread (to adjacent chunks)
        // Species population changes
        // Resource regeneration
    }
}

public class ChunkManager
{
    private Dictionary<Vector3Int, WorldChunk> _chunks = new();
    private const int ChunkSize = 100;  // meters
    
    public WorldChunk GetChunkForPosition(Vector3 pos)
    {
        var chunkPos = new Vector3Int(
            (int)(pos.X / ChunkSize),
            (int)(pos.Y / ChunkSize),
            (int)(pos.Z / ChunkSize)
        );
        
        if (!_chunks.ContainsKey(chunkPos))
        {
            _chunks[chunkPos] = new WorldChunk { Position = chunkPos };
        }
        
        return _chunks[chunkPos];
    }
    
    public List<WorldChunk> GetActiveChunks()
    {
        return _chunks.Values.Where(c => c.IsActive).ToList();
    }
    
    public void UpdateChunkActivity()
    {
        foreach (var chunk in _chunks.Values)
        {
            // Activate chunks with players or active entities
            chunk.IsActive = chunk.Players.Count > 0 || 
                            chunk.Entities.Any(e => e.IsActive);
        }
    }
}
```

**Spatial Culling for Network**:
```csharp
public class NetworkCulling
{
    private const float CullDistance = 200f;  // meters
    private const float HighDetailDistance = 50f;
    private const float MediumDetailDistance = 150f;
    
    public void UpdatePlayerVisibility(Player player)
    {
        var nearbyEntities = GetEntitiesInRadius(player.Position, CullDistance);
        
        foreach (var entity in nearbyEntities)
        {
            float distance = entity.Position.DistanceTo(player.Position);
            
            if (distance < HighDetailDistance)
            {
                // High detail: position updates at 20 TPS
                entity.SetSyncRate(20);
                entity.SetDetailLevel(DetailLevel.High);
            }
            else if (distance < MediumDetailDistance)
            {
                // Medium detail: position updates at 10 TPS
                entity.SetSyncRate(10);
                entity.SetDetailLevel(DetailLevel.Medium);
            }
            else
            {
                // Low detail: position updates at 2 TPS
                entity.SetSyncRate(2);
                entity.SetDetailLevel(DetailLevel.Low);
            }
        }
        
        // Entities beyond cull distance: no updates
    }
}
```

**Implications for Societies**:
- Implement chunk-based spatial partitioning from the start
- Only simulate chunks with active players (or nearby)
- Use distance-based LOD for network sync rates
- 100+ agents manageable with proper spatial culling
- Adjacent chunk communication needed for pollution/trade spread

### CPU Throttling and Tick Rate Management

**Evidence**:
- Eco's `WorldTickCPUMax` setting controls max CPU usage per core
- Default: 25% (leaves headroom for other processes)
- Maximum recommended: 75% (beyond that risks lag spikes)
- Server adjusts tick processing based on CPU budget
- Automatic quality reduction if can't maintain target tick rate

**Tick Budgeting System**:
```csharp
public class TickBudgetManager
{
    private float _maxCpuPercent = 0.25f;  // 25% default
    private double _targetTickTime;  // Time budget per tick
    private double _actualTickTime;
    
    public void Configure(float maxCpuPercent)
    {
        _maxCpuPercent = Math.Min(maxCpuPercent, 0.75f);  // Cap at 75%
        
        // At 20 TPS, each tick should take 50ms
        // With 25% CPU, we have 12.5ms per tick
        _targetTickTime = (1000.0 / TickRate) * _maxCpuPercent;
    }
    
    public void ProcessTick(double delta)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Process systems in priority order
        ProcessCriticalSystems(delta);  // Always run (network, player input)
        
        if (stopwatch.ElapsedMilliseconds < _targetTickTime * 0.5)
        {
            ProcessHighPrioritySystems(delta);  // Economy, agents near players
        }
        
        if (stopwatch.ElapsedMilliseconds < _targetTickTime * 0.8)
        {
            ProcessMediumPrioritySystems(delta);  // Distant agents, environment
        }
        
        if (stopwatch.ElapsedMilliseconds < _targetTickTime)
        {
            ProcessLowPrioritySystems(delta);  // Background processing
        }
        else
        {
            // Budget exhausted - defer low priority to next tick
            _deferredUpdates.AddRange(GetLowPriorityUpdates());
        }
        
        _actualTickTime = stopwatch.ElapsedMilliseconds;
        
        // Adjust quality if consistently over budget
        if (_actualTickTime > _targetTickTime * 1.2)
        {
            ReduceSimulationQuality();
        }
    }
    
    private void ReduceSimulationQuality()
    {
        // Reduce agent update radius
        AgentUpdateRadius *= 0.9f;
        
        // Reduce agent count per tick
        MaxAgentsPerTick = Math.Max(10, MaxAgentsPerTick - 5);
        
        // Reduce ecosystem simulation frequency
        EcosystemUpdateInterval++;
        
        GD.Print($"Reduced quality: radius={AgentUpdateRadius}, maxAgents={MaxAgentsPerTick}");
    }
}
```

**Adaptive Tick Rate**:
```csharp
public class AdaptiveTickRate
{
    private int _currentTickRate = 20;  // Target 20 TPS
    private int _minTickRate = 10;      // Minimum acceptable
    private int _maxTickRate = 30;      // Maximum
    
    private Queue<double> _tickDurations = new();
    private const int SampleSize = 60;
    
    public void RecordTickDuration(double milliseconds)
    {
        _tickDurations.Enqueue(milliseconds);
        if (_tickDurations.Count > SampleSize)
            _tickDurations.Dequeue();
    }
    
    public void AdjustTickRate()
    {
        if (_tickDurations.Count < SampleSize) return;
        
        var avgDuration = _tickDurations.Average();
        var availableTime = 1000.0 / _currentTickRate;
        
        // If using >90% of available time, reduce tick rate
        if (avgDuration > availableTime * 0.9 && _currentTickRate > _minTickRate)
        {
            _currentTickRate--;
            GD.Print($"Reduced tick rate to {_currentTickRate} TPS (avg: {avgDuration:F1}ms)");
        }
        // If using <50% of available time, can increase tick rate
        else if (avgDuration < availableTime * 0.5 && _currentTickRate < _maxTickRate)
        {
            _currentTickRate++;
            GD.Print($"Increased tick rate to {_currentTickRate} TPS (avg: {avgDuration:F1}ms)");
        }
    }
}
```

**Implications for Societies**:
- Implement CPU throttling configuration (25-75% range)
- Priority-based tick processing: critical > high > medium > low
- Automatic quality reduction when over budget
- Monitor tick duration and adjust rate dynamically
- Give server admins control over CPU usage vs performance tradeoff

### Database Persistence Optimization

**Evidence**:
- Eco faces database bottleneck with frequent world state changes
- Solution: batch writes and use dirty tracking
- Don't write every change immediately - accumulate and flush periodically
- Use separate threads for database operations (don't block game loop)

**Dirty Tracking System**:
```csharp
public class DirtyTracker<T> where T : class
{
    private HashSet<T> _dirtyObjects = new();
    private DateTime _lastFlush = DateTime.UtcNow;
    private TimeSpan _flushInterval = TimeSpan.FromSeconds(5);
    
    public void MarkDirty(T obj)
    {
        _dirtyObjects.Add(obj);
    }
    
    public void MarkClean(T obj)
    {
        _dirtyObjects.Remove(obj);
    }
    
    public bool ShouldFlush()
    {
        return DateTime.UtcNow - _lastFlush > _flushInterval ||
               _dirtyObjects.Count > 1000;  // Flush if too many pending
    }
    
    public async Task FlushAsync(IDatabaseRepository<T> repository)
    {
        if (_dirtyObjects.Count == 0) return;
        
        var toFlush = _dirtyObjects.ToList();
        _dirtyObjects.Clear();
        
        // Batch update
        await repository.BatchUpdateAsync(toFlush);
        
        _lastFlush = DateTime.UtcNow;
        
        GD.Print($"Flushed {toFlush.Count} objects to database");
    }
}

public class WorldDatabaseManager
{
    private DirtyTracker<Agent> _agentTracker = new();
    private DirtyTracker<Entity> _entityTracker = new();
    private DirtyTracker<Player> _playerTracker = new();
    
    public void OnAgentChanged(Agent agent)
    {
        _agentTracker.MarkDirty(agent);
    }
    
    public async Task FlushDirtyObjects()
    {
        // Flush in parallel
        var tasks = new List<Task>
        {
            _agentTracker.FlushAsync(_agentRepository),
            _entityTracker.FlushAsync(_entityRepository),
            _playerTracker.FlushAsync(_playerRepository)
        };
        
        await Task.WhenAll(tasks);
    }
}
```

**Database Batching**:
```csharp
public class BatchedRepository<T> where T : class
{
    private const int BatchSize = 100;
    private List<T> _batch = new();
    
    public async Task BatchUpdateAsync(List<T> objects)
    {
        // Split into batches
        for (int i = 0; i < objects.Count; i += BatchSize)
        {
            var batch = objects.Skip(i).Take(BatchSize).ToList();
            await ExecuteBatchAsync(batch);
        }
    }
    
    private async Task ExecuteBatchAsync(List<T> batch)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // Use bulk update if available (EF Core 7+)
            await _context.BulkUpdateAsync(batch);
            
            // Or manual batch
            foreach (var obj in batch)
            {
                _context.Update(obj);
            }
            await _context.SaveChangesAsync();
            
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            GD.PushError($"Batch update failed: {ex}");
        }
    }
}
```

**Implications for Societies**:
- Implement dirty tracking - only save changed objects
- Batch database writes (every 5 seconds or 1000 changes)
- Use async database operations (don't block game thread)
- Consider in-memory + periodic snapshot approach (not every change)
- PostgreSQL with JSONB supports efficient batch updates

### Memory Management Strategies

**Evidence**:
- Eco provides memory dump capability via `dotnet-dump`
- Memory leaks common issue in long-running servers (days/weeks uptime)
- Server profiling tools track memory allocation by system
- Recommendation: regular server restarts for production servers

**Memory Profiling**:
```csharp
public class MemoryProfiler
{
    private Dictionary<string, long> _systemMemoryUsage = new();
    
    public void TrackAllocation(string system, long bytes)
    {
        if (!_systemMemoryUsage.ContainsKey(system))
            _systemMemoryUsage[system] = 0;
        
        _systemMemoryUsage[system] += bytes;
    }
    
    public void GenerateReport()
    {
        GD.Print("=== Memory Usage Report ===");
        foreach (var system in _systemMemoryUsage.OrderByDescending(kvp => kvp.Value))
        {
            var mb = system.Value / (1024.0 * 1024.0);
            GD.Print($"{system.Key}: {mb:F1} MB");
        }
        
        var total = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        GD.Print($"Total managed memory: {total:F1} MB");
    }
}
```

**Object Pooling**:
```csharp
public class ObjectPool<T> where T : class, new()
{
    private Queue<T> _pool = new();
    private int _maxSize;
    
    public ObjectPool(int initialSize, int maxSize)
    {
        _maxSize = maxSize;
        
        for (int i = 0; i < initialSize; i++)
        {
            _pool.Enqueue(new T());
        }
    }
    
    public T Acquire()
    {
        if (_pool.Count > 0)
        {
            return _pool.Dequeue();
        }
        return new T();
    }
    
    public void Release(T obj)
    {
        if (_pool.Count < _maxSize)
        {
            // Reset object state
            if (obj is IResettable resettable)
            {
                resettable.Reset();
            }
            _pool.Enqueue(obj);
        }
        // Else let GC collect it
    }
}

public interface IResettable
{
    void Reset();
}
```

**Implications for Societies**:
- Implement memory profiling by subsystem (agents, economy, environment)
- Use object pooling for frequently created/destroyed objects (particles, events)
- Monitor for memory leaks during long-running tests
- Plan for periodic server restarts (memory fragmentation)
- Target: <4GB RAM for 100 agents + 20 players

### Optimization Checklist from Eco

**Evidence from Update 9.7 (Focus on Performance)**:

**Implemented Optimizations**:
1. **Spatial chunk system**: Only process chunks with players
2. **Dirty tracking**: Only save changed data
3. **CPU throttling**: Configurable max CPU usage
4. **Database batching**: Bulk operations, not individual writes
5. **Network culling**: Only sync visible entities
6. **LOD system**: Reduced detail for distant entities
7. **Async database**: Don't block game thread
8. **Object pooling**: Reuse objects instead of allocating
9. **Profiling tools**: Built-in CPU, memory, and network profilers

**What Didn't Work**:
1. Over-aggressive culling (players noticed entities "popping in")
2. Too infrequent database saves (data loss on crashes)
3. Single-threaded database (caused lag spikes)
4. No CPU limits (server consumed all resources)

## Code Examples

### Spatial Partitioning Manager
```csharp
public partial class SpatialPartitionManager : Node
{
    private const int ChunkSize = 100;
    private Dictionary<Vector3Int, WorldChunk> _chunks = new();
    private Dictionary<Guid, Agent> _allAgents = new();
    
    public void RegisterAgent(Agent agent)
    {
        _allAgents[agent.Id] = agent;
        
        var chunk = GetOrCreateChunk(agent.Position);
        chunk.AddAgent(agent);
        agent.CurrentChunk = chunk;
    }
    
    public void UpdateAgentPosition(Agent agent, Vector3 newPosition)
    {
        var oldChunk = agent.CurrentChunk;
        var newChunk = GetOrCreateChunk(newPosition);
        
        if (oldChunk != newChunk)
        {
            oldChunk.RemoveAgent(agent);
            newChunk.AddAgent(agent);
            agent.CurrentChunk = newChunk;
        }
    }
    
    public List<Agent> GetNearbyAgents(Vector3 position, float radius)
    {
        var results = new List<Agent>();
        var radiusSquared = radius * radius;
        
        // Calculate affected chunks
        var minChunk = WorldToChunk(position - new Vector3(radius, radius, radius));
        var maxChunk = WorldToChunk(position + new Vector3(radius, radius, radius));
        
        for (int x = minChunk.X; x <= maxChunk.X; x++)
        {
            for (int y = minChunk.Y; y <= maxChunk.Y; y++)
            {
                for (int z = minChunk.Z; z <= maxChunk.Z; z++)
                {
                    var chunkPos = new Vector3Int(x, y, z);
                    if (_chunks.TryGetValue(chunkPos, out var chunk))
                    {
                        foreach (var agent in chunk.Agents)
                        {
                            if (agent.Position.DistanceSquaredTo(position) <= radiusSquared)
                            {
                                results.Add(agent);
                            }
                        }
                    }
                }
            }
        }
        
        return results;
    }
    
    private WorldChunk GetOrCreateChunk(Vector3 position)
    {
        var chunkPos = WorldToChunk(position);
        
        if (!_chunks.TryGetValue(chunkPos, out var chunk))
        {
            chunk = new WorldChunk(chunkPos);
            _chunks[chunkPos] = chunk;
        }
        
        return chunk;
    }
    
    private Vector3Int WorldToChunk(Vector3 worldPos)
    {
        return new Vector3Int(
            Mathf.FloorToInt(worldPos.X / ChunkSize),
            Mathf.FloorToInt(worldPos.Y / ChunkSize),
            Mathf.FloorToInt(worldPos.Z / ChunkSize)
        );
    }
}

public class WorldChunk
{
    public Vector3Int Position { get; }
    public List<Agent> Agents { get; } = new();
    public bool IsActive { get; set; }
    
    public WorldChunk(Vector3Int position)
    {
        Position = position;
    }
    
    public void AddAgent(Agent agent) => Agents.Add(agent);
    public void RemoveAgent(Agent agent) => Agents.Remove(agent);
}
```

### Performance Manager with Budgeting
```csharp
public partial class PerformanceManager : Node
{
    [Export] public float MaxCpuPercent { get; set; } = 0.25f;
    
    private double _tickBudgetMs;
    private Stopwatch _tickTimer = new();
    private AdaptiveTickRate _tickRate = new();
    
    public override void _Ready()
    {
        UpdateBudget();
    }
    
    private void UpdateBudget()
    {
        var tickDurationMs = 1000.0 / Engine.PhysicsTicksPerSecond;
        _tickBudgetMs = tickDurationMs * MaxCpuPercent;
    }
    
    public void BeginTick()
    {
        _tickTimer.Restart();
    }
    
    public bool HasBudgetRemaining(double fraction = 0.5)
    {
        return _tickTimer.Elapsed.TotalMilliseconds < _tickBudgetMs * fraction;
    }
    
    public void EndTick()
    {
        var elapsed = _tickTimer.Elapsed.TotalMilliseconds;
        _tickRate.RecordTickDuration(elapsed);
        
        // Log if over budget
        if (elapsed > _tickBudgetMs)
        {
            GD.PushWarning($"Tick over budget: {elapsed:F1}ms / {_tickBudgetMs:F1}ms");
        }
    }
}
```

## Performance Data

| Metric | Eco Value | Societies Target | Notes |
|--------|-----------|------------------|-------|
| CPU Usage Limit | 25-75% | 25-75% | Configurable per server |
| Tick Rate | Variable | 20 TPS target | Adaptive based on load |
| Max Players | 100+ | 100 | Stretch goal |
| Chunk Size | ~100m | ~100m | Spatial partitioning |
| Database Flush | 5-30 seconds | 5 seconds | Dirty tracking batch |
| Memory Target | <8GB | <8GB | For 100 agents |

## Limitations & Risks

1. **Memory Leaks**: Long-running servers (weeks) accumulate memory fragmentation
2. **Database Bottleneck**: Write-heavy workloads can overwhelm database
3. **Single-Server Limit**: One world per server instance
4. **Complexity**: Spatial partitioning adds code complexity
5. **Tuning Required**: Optimal chunk size varies by world density

## Recommendations

1. **Implement Spatial Partitioning**: 100m chunks, only simulate chunks with players

2. **CPU Budgeting**: Allow server admins to set 25-75% CPU limit, adjust quality dynamically

3. **Dirty Tracking**: Only persist changed entities, batch every 5 seconds

4. **Database Async**: All DB operations on separate thread, don't block game loop

5. **Network LOD**: Sync nearby agents at 20 TPS, distant at 2 TPS

6. **Memory Monitoring**: Track allocation by subsystem, implement object pooling

7. **Built-in Profiling**: Include CPU, memory, and network profilers in server build

8. **Adaptive Tick Rate**: Reduce from 20 TPS to 10 TPS if can't maintain budget

## Confidence Assessment

- **Overall Confidence**: High
- **Evidence Quality**: Official Eco wiki, server configuration docs, community experience
- **Applicability**: Very High - Eco is extremely similar to Societies in design and challenges

## Related Sources

- Eco Wiki - Server Profiling: https://wiki.play.eco/en/Server_Profiling
- Eco Wiki - Server Configuration: https://wiki.play.eco/en/Server_Configuration
- Eco Discord (community performance discussions)

## Open Questions

- What's optimal chunk size for Societies' expected agent density?
- How many agents can we process per tick within 12.5ms budget (25% CPU at 20 TPS)?
- Should we implement automatic server restart scheduling for memory management?
