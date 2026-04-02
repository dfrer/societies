# Error Handling and Recovery Specifications

**Session:** 1 - Technical Architecture  
**Document:** 11 - Error Handling  
**Status:** Draft  
**Last Updated:** 2026-02-01

---

## 1. Error Handling Philosophy

The Societies server operates under a **graceful degradation** paradigm with the following core principles:

1. **Fail gracefully, never crash the server** - All exceptions are caught at system boundaries
2. **Degrade functionality rather than stop completely** - Reduce features before shutting down
3. **Log everything for debugging** - Comprehensive logging enables post-mortem analysis
4. **Recover automatically where possible** - Self-healing systems reduce operational burden
5. **Maintain player state integrity** - Never lose player progress due to system failures
6. **Transparency with players** - Inform players when functionality is degraded

### Design Principles

| Principle | Implementation |
|-----------|----------------|
| Defense in Depth | Multiple layers of error handling |
| Circuit Breakers | Stop trying failing operations temporarily |
| Bulkheads | Isolate failures to prevent cascading |
| Timeouts | All operations have defined time limits |
| Idempotency | Operations can be safely retried |

---

## 2. Error Categories & Severity

### Severity Levels

```
CRITICAL: Server stability threatened
  - Tick loop overrun > 100ms
  - Database connection lost
  - Memory exhaustion
  - Disk space critical
  - Action: Immediate degradation + admin alert

HIGH: Major functionality impaired
  - Network partition affecting multiple players
  - Disk space low (< 20%)
  - AI system failure affecting all agents
  - Database performance degraded
  - Action: Degraded mode + log + alert

MEDIUM: Feature affected but core stable
  - Single player disconnect
  - Trade transaction failure
  - Missing or corrupt asset
  - Cache miss requiring DB read
  - Action: Log + retry mechanism

LOW: Minor issue, no player impact
  - Validation warnings
  - Minor desync (corrected automatically)
  - Log noise
  - Action: Log only, review in aggregates

INFO: Expected behavior logged for tracking
  - Player login/logout
  - World saves
  - Scheduled maintenance
  - Action: Log for metrics and audit
```

### Error Taxonomy

| Category | Examples | Severity Range |
|----------|----------|----------------|
| Network | Disconnect, timeout, packet loss | MEDIUM-CRITICAL |
| Database | Connection lost, query timeout, corruption | HIGH-CRITICAL |
| Simulation | Tick overrun, state corruption | HIGH-CRITICAL |
| AI | Agent exception, pathfinding failure | MEDIUM-HIGH |
| Resources | Memory pressure, disk full | MEDIUM-CRITICAL |
| Security | Authentication failure, rate limit | LOW-HIGH |

---

## 3. Network Failure Handling

### Client Disconnection

```csharp
public enum DisconnectReason {
    ClientQuit,      // Graceful disconnect
    Timeout,         // Heartbeat timeout
    Error,           // Protocol error
    ServerShutdown,  // Intentional kick
    NetworkIssue     // Connection reset
}

public class ConnectionManager {
    private Dictionary<Guid, ReconnectWindow> _reconnectWindows = new();
    private const int RECONNECT_WINDOW_SECONDS = 60;
    
    public void HandleClientDisconnect(long peerId, DisconnectReason reason) {
        var player = GetPlayerByPeerId(peerId);
        if (player == null) return;
        
        Logger.Info("Network", $"Player {player.Name} disconnecting: {reason}", 
            new { PlayerId = player.Id, Reason = reason, Tick = GameState.CurrentTick });
        
        switch (reason) {
            case DisconnectReason.ClientQuit:
                HandleGracefulDisconnect(player);
                break;
                
            case DisconnectReason.Timeout:
                HandleTimeoutDisconnect(player);
                break;
                
            case DisconnectReason.Error:
                HandleErrorDisconnect(player, reason);
                break;
                
            case DisconnectReason.ServerShutdown:
                HandleServerShutdownDisconnect(player);
                break;
        }
    }
    
    private void HandleGracefulDisconnect(Player player) {
        // Immediate state save
        try {
            SavePlayerState(player, urgent: true);
            Logger.Info("Network", $"Player {player.Name} state saved", 
                new { PlayerId = player.Id });
        }
        catch (Exception ex) {
            Logger.Error("Network", "Failed to save player state on disconnect", ex,
                new { PlayerId = player.Id });
        }
        
        // Notify other players
        BroadcastMessage(new PlayerLeftMessage {
            PlayerName = player.Name,
            Timestamp = DateTime.UtcNow
        });
        
        // Clean up
        player.SetState(PlayerState.Offline);
        ReleasePlayerResources(player);
    }
    
    private void HandleTimeoutDisconnect(Player player) {
        // Keep player "alive" for reconnect window
        var window = new ReconnectWindow {
            PlayerId = player.Id,
            ExpiresAt = DateTime.UtcNow.AddSeconds(RECONNECT_WINDOW_SECONDS),
            OriginalPeerId = player.PeerId
        };
        
        _reconnectWindows[player.Id] = window;
        player.SetState(PlayerState.Disconnected);
        
        Logger.Info("Network", $"Started reconnect window for {player.Name}",
            new { PlayerId = player.Id, WindowDuration = RECONNECT_WINDOW_SECONDS });
        
        // Schedule window expiration check
        ScheduleTask(() => CheckReconnectWindowExpired(player.Id), 
            delayMs: RECONNECT_WINDOW_SECONDS * 1000);
    }
    
    private void HandleErrorDisconnect(Player player, DisconnectReason reason) {
        Logger.Error("Network", $"Player {player.Name} disconnected unexpectedly", 
            null, new { PlayerId = player.Id, Reason = reason });
        
        // Attempt state save
        try {
            SavePlayerState(player, urgent: true);
        }
        catch (Exception ex) {
            Logger.Error("Network", "Failed to save state after error disconnect", ex);
        }
        
        // Flag for investigation
        player.SetState(PlayerState.ErrorDisconnected);
        FlagForInvestigation(player.Id, "Unexpected disconnect");
    }
    
    public void OnPlayerReconnect(long peerId, Guid playerId, string authToken) {
        // Validate auth token
        if (!ValidateReconnectToken(playerId, authToken)) {
            Logger.Warn("Security", "Invalid reconnect token attempt",
                new { PlayerId = playerId, PeerId = peerId });
            DisconnectPeer(peerId, DisconnectReason.Error);
            return;
        }
        
        if (!_reconnectWindows.TryGetValue(playerId, out var window)) {
            Logger.Warn("Network", "Reconnect attempt without active window",
                new { PlayerId = playerId });
            return;
        }
        
        if (DateTime.UtcNow > window.ExpiresAt) {
            Logger.Info("Network", "Reconnect window expired",
                new { PlayerId = playerId, ExpiredAt = window.ExpiresAt });
            _reconnectWindows.Remove(playerId);
            return;
        }
        
        var player = GetPlayer(playerId);
        if (player.State != PlayerState.Disconnected) {
            Logger.Error("Network", "Reconnect attempt for non-disconnected player",
                new { PlayerId = playerId, CurrentState = player.State });
            return;
        }
        
        // Restore session
        player.SetPeerId(peerId);
        player.SetState(PlayerState.Active);
        
        // Send world state delta since disconnect
        var delta = CalculateWorldStateDelta(player.LastUpdateTick);
        SendMessage(peerId, new WorldStateSyncMessage { Delta = delta });
        
        _reconnectWindows.Remove(playerId);
        
        Logger.Info("Network", $"Player {player.Name} reconnected successfully",
            new { PlayerId = playerId, PeerId = peerId });
    }
    
    private void CheckReconnectWindowExpired(Guid playerId) {
        if (_reconnectWindows.TryGetValue(playerId, out var window) && 
            DateTime.UtcNow > window.ExpiresAt) {
            
            var player = GetPlayer(playerId);
            if (player?.State == PlayerState.Disconnected) {
                // Window expired, treat as graceful disconnect
                Logger.Info("Network", $"Reconnect window expired for {player.Name}",
                    new { PlayerId = playerId });
                
                SavePlayerState(player, urgent: true);
                player.SetState(PlayerState.Offline);
                ReleasePlayerResources(player);
            }
            
            _reconnectWindows.Remove(playerId);
        }
    }
}
```

### Network Partition Recovery

```csharp
public class NetworkHealthMonitor {
    private const int HEARTBEAT_TIMEOUT_MS = 5000;
    private const float PACKET_LOSS_THRESHOLD = 0.50f;
    private const int LATENCY_THRESHOLD_MS = 500;
    private const int DEGRADED_SYNC_TPS = 10;
    private const int NORMAL_TPS = 20;
    private const int STABILITY_CHECK_DURATION_MS = 10000;
    
    public void MonitorConnection(long peerId, ConnectionMetrics metrics) {
        var status = EvaluateConnectionHealth(metrics);
        
        switch (status) {
            case ConnectionStatus.Healthy:
                if (IsInDegradedMode(peerId)) {
                    AttemptReturnToNormal(peerId);
                }
                break;
                
            case ConnectionStatus.Degraded:
                if (!IsInDegradedMode(peerId)) {
                    EnterDegradedSyncMode(peerId);
                }
                break;
                
            case ConnectionStatus.Critical:
                if (!IsInCriticalMode(peerId)) {
                    HandleCriticalConnection(peerId);
                }
                break;
        }
    }
    
    private ConnectionStatus EvaluateConnectionHealth(ConnectionMetrics metrics) {
        if (metrics.LastHeartbeatMs > HEARTBEAT_TIMEOUT_MS ||
            metrics.PacketLoss > PACKET_LOSS_THRESHOLD ||
            metrics.AverageLatencyMs > LATENCY_THRESHOLD_MS) {
            return ConnectionStatus.Critical;
        }
        
        if (metrics.PacketLoss > 0.10f || metrics.AverageLatencyMs > 200) {
            return ConnectionStatus.Degraded;
        }
        
        return ConnectionStatus.Healthy;
    }
    
    private void EnterDegradedSyncMode(long peerId) {
        Logger.Warn("Network", "Entering degraded sync mode",
            new { PeerId = peerId });
        
        var player = GetPlayerByPeerId(peerId);
        
        // Reduce update frequency
        player.SetSyncRate(DEGRADED_SYNC_TPS);
        
        // Increase interpolation buffer
        player.SetInterpolationBufferMs(200);
        
        // Queue non-critical updates
        player.EnableUpdateQueuing();
        
        // Alert player
        SendMessage(peerId, new ConnectionWarningMessage {
            Severity = WarningSeverity.Medium,
            Message = "Connection unstable. Synchronization reduced.",
            SuggestedAction = "Check your network connection"
        });
        
        _degradedConnections.Add(peerId);
    }
    
    private void AttemptReturnToNormal(long peerId) {
        // Wait for stability period before returning to normal
        ScheduleTask(() => {
            if (IsConnectionStable(peerId, durationMs: STABILITY_CHECK_DURATION_MS)) {
                RestoreNormalMode(peerId);
            }
        }, delayMs: STABILITY_CHECK_DURATION_MS);
    }
    
    private void RestoreNormalMode(long peerId) {
        Logger.Info("Network", "Connection stabilized, returning to normal mode",
            new { PeerId = peerId });
        
        var player = GetPlayerByPeerId(peerId);
        player.SetSyncRate(NORMAL_TPS);
        player.SetInterpolationBufferMs(100);
        player.DisableUpdateQueuing();
        player.FlushQueuedUpdates();
        
        SendMessage(peerId, new ConnectionRestoredMessage());
        
        _degradedConnections.Remove(peerId);
    }
}
```

---

## 4. Database Failure Handling

### Connection Loss Recovery

```csharp
public class DatabaseResilience {
    private int _retryCount = 0;
    private const int MAX_RETRIES = 5;
    private const int INITIAL_RETRY_DELAY_MS = 1000;
    private const int MAX_RETRY_DELAY_MS = 30000;
    private const int OFFLINE_MODE_RETRY_INTERVAL_MS = 30000;
    
    private bool _isOfflineMode = false;
    private List<BufferedOperation> _operationBuffer = new();
    private const int MAX_BUFFER_SIZE = 1000;
    
    public async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation, string operationName) {
        if (_isOfflineMode) {
            // Buffer operation for later
            var bufferedOp = new BufferedOperation<T> {
                Name = operationName,
                Operation = operation,
                Timestamp = DateTime.UtcNow
            };
            
            if (_operationBuffer.Count >= MAX_BUFFER_SIZE) {
                // Force checkpoint to disk
                await FlushBufferToDisk();
            }
            
            _operationBuffer.Add(bufferedOp);
            Logger.Warn("Database", $"Operation buffered in offline mode: {operationName}");
            return default;
        }
        
        while (_retryCount < MAX_RETRIES) {
            try {
                var result = await operation();
                
                if (_retryCount > 0) {
                    Logger.Info("Database", "Database operation succeeded after retry",
                        new { Operation = operationName, RetryCount = _retryCount });
                }
                
                _retryCount = 0;
                return result;
            }
            catch (DbConnectionException ex) {
                _retryCount++;
                
                var delayMs = Math.Min(
                    INITIAL_RETRY_DELAY_MS * (int)Math.Pow(2, _retryCount - 1),
                    MAX_RETRY_DELAY_MS
                );
                
                Logger.Warn("Database", 
                    $"DB connection lost, attempt {_retryCount}/{MAX_RETRIES}: {operationName}",
                    new { RetryDelayMs = delayMs, Error = ex.Message });
                
                if (_retryCount >= MAX_RETRIES) {
                    await EnterOfflineMode();
                    throw new DatabaseUnavailableException(
                        $"Database unavailable after {MAX_RETRIES} attempts", ex);
                }
                
                await Task.Delay(delayMs);
                
                try {
                    await ReconnectDatabase();
                }
                catch (Exception reconnectEx) {
                    Logger.Error("Database", "Failed to reconnect", reconnectEx);
                }
            }
        }
        
        return default;
    }
    
    private async Task EnterOfflineMode() {
        if (_isOfflineMode) return;
        
        _isOfflineMode = true;
        Logger.Critical("Database", "ENTERING OFFLINE MODE - Database unavailable");
        
        // Stop accepting new players
        PlayerManager.LockNewConnections();
        
        // Notify admins
        await AlertService.SendCriticalAlert("Database offline mode activated");
        
        // Start reconnection attempts
        _ = Task.Run(async () => {
            while (_isOfflineMode) {
                await Task.Delay(OFFLINE_MODE_RETRY_INTERVAL_MS);
                
                try {
                    await ReconnectDatabase();
                    await ExitOfflineMode();
                    break;
                }
                catch (Exception ex) {
                    Logger.Error("Database", "Reconnection attempt failed", ex);
                }
            }
        });
        
        // Schedule forced shutdown if offline too long
        ScheduleTask(async () => {
            if (_isOfflineMode) {
                Logger.Critical("Database", "Maximum offline duration exceeded, initiating graceful shutdown");
                await WorldManager.InitiateGracefulShutdown(reason: "Database offline timeout");
            }
        }, delayMs: (int)TimeSpan.FromHours(1).TotalMilliseconds);
    }
    
    private async Task ExitOfflineMode() {
        _isOfflineMode = false;
        _retryCount = 0;
        
        Logger.Critical("Database", "EXITING OFFLINE MODE - Database reconnected");
        
        // Flush buffered operations
        await FlushBufferToDatabase();
        
        // Resume normal operations
        PlayerManager.UnlockNewConnections();
        
        await AlertService.SendInfoAlert("Database connectivity restored");
    }
    
    private async Task FlushBufferToDatabase() {
        if (_operationBuffer.Count == 0) return;
        
        Logger.Info("Database", $"Flushing {_operationBuffer.Count} buffered operations");
        
        var failedOps = new List<BufferedOperation>();
        
        foreach (var op in _operationBuffer.OrderBy(o => o.Timestamp)) {
            try {
                await op.Execute();
            }
            catch (Exception ex) {
                Logger.Error("Database", "Failed to flush buffered operation", ex,
                    new { OperationName = op.Name });
                failedOps.Add(op);
            }
        }
        
        _operationBuffer.Clear();
        
        if (failedOps.Count > 0) {
            Logger.Error("Database", $"{failedOps.Count} buffered operations failed to flush");
            // Write to disk for manual recovery
            await WriteFailedOpsToDisk(failedOps);
        }
    }
    
    private async Task FlushBufferToDisk() {
        var checkpointPath = $"db_buffer_checkpoint_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        var operations = _operationBuffer.Select(o => new {
            o.Name,
            o.Timestamp,
            o.SerializedData
        }).ToList();
        
        await File.WriteAllTextAsync(checkpointPath, 
            JsonSerializer.Serialize(operations));
        
        Logger.Info("Database", $"Buffer checkpoint written to {checkpointPath}");
        _operationBuffer.Clear();
    }
}
```

### Query Timeout Handling

```csharp
public class QueryTimeoutHandler {
    private const int DEFAULT_QUERY_TIMEOUT_MS = 5000;
    private const int CRITICAL_QUERY_TIMEOUT_MS = 1000;
    
    private readonly ICacheService _cache;
    
    public async Task<T> ExecuteWithTimeout<T>(
        Func<Task<T>> query, 
        string queryDescription,
        int timeoutMs = DEFAULT_QUERY_TIMEOUT_MS,
        bool useCacheFallback = true) {
        
        using (var cts = new CancellationTokenSource()) {
            cts.CancelAfter(timeoutMs);
            
            try {
                return await query().WithCancellation(cts.Token);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested) {
                Logger.Error("Database", $"Query timeout after {timeoutMs}ms: {queryDescription}");
                
                if (useCacheFallback) {
                    var cached = await TryGetCachedData<T>(queryDescription);
                    if (cached != null) {
                        Logger.Warn("Database", "Returning stale cached data due to timeout",
                            new { Query = queryDescription });
                        return cached;
                    }
                }
                
                // Return default/empty result
                return GetDefaultResult<T>();
            }
        }
    }
    
    private async Task<T> TryGetCachedData<T>(string queryKey) {
        return await _cache.GetAsync<T>($"query_fallback:{queryKey}");
    }
    
    private T GetDefaultResult<T>() {
        if (typeof(T) == typeof(List<>)) {
            return (T)(object)new List<object>();
        }
        
        if (typeof(T).IsValueType) {
            return default;
        }
        
        return default;
    }
}
```

### Offline Mode (Emergency Operations)

**Offline Mode Activation Criteria:**
- Database unreachable after 5 retry attempts
- Query timeout rate exceeds 50%
- Connection pool exhausted

**Offline Mode Behavior:**

```
When database is unavailable:
  1. IMMEDIATE (0s):
     - Lock new player connections
     - Set server status to "Maintenance"
     - Notify connected players: "Save functionality temporarily limited"
  
  2. BUFFERING (0-60min):
     - Continue gameplay for existing players
     - Buffer all write operations to memory
     - Serve read requests from cache where possible
     - Retry database connection every 30 seconds
     - Log all operations for replay
  
  3. CHECKPOINTING (every 5min or at 800 ops):
     - Serialize buffered operations to disk
     - Clear memory buffer
     - Track checkpoint sequence number
  
  4. RECOVERY (on reconnect):
     - Replay buffered operations in order
     - Validate each operation succeeds
     - If replay fails: Log for manual intervention
     - Resume normal operations
     - Unlock new connections
  
  5. EMERGENCY SHUTDOWN (60min):
     - If still offline after 1 hour
     - Force final checkpoint to disk
     - Graceful shutdown with 15min warning
     - Preserve all buffered data

Maximum offline duration: 1 hour
After 1 hour: Graceful shutdown to prevent data loss
```

---

## 5. Simulation Error Recovery

### Tick Loop Overrun Management

```csharp
public class TickLoopManager {
    private const long TICK_BUDGET_MICROSECONDS = 50000; // 50ms = 20 TPS
    
    private DegradationLevel _currentDegradation = DegradationLevel.Normal;
    private int _consecutiveOverruns = 0;
    
    public void HandleTickOverrun(long actualMicroseconds) {
        var overBudget = actualMicroseconds - TICK_BUDGET_MICROSECONDS;
        
        if (overBudget <= 0) {
            // Performance restored
            if (_consecutiveOverruns > 0) {
                _consecutiveOverruns--;
                if (_consecutiveOverruns == 0 && _currentDegradation != DegradationLevel.Normal) {
                    OnPerformanceRestored();
                }
            }
            return;
        }
        
        _consecutiveOverruns++;
        
        Logger.Warn("Simulation", 
            $"Tick overrun: {overBudget}μs over budget (consecutive: {_consecutiveOverruns})");
        
        // Escalating degradation based on severity and consistency
        ApplyEscalatingDegradation(overBudget, _consecutiveOverruns);
    }
    
    private void ApplyEscalatingDegradation(long overBudget, int consecutiveOverruns) {
        // Level 1: Skip low priority (5ms or 3 consecutive overruns)
        if ((overBudget > 5000 || consecutiveOverruns >= 3) && 
            _currentDegradation < DegradationLevel.SkipLowPriority) {
            
            DegradationManager.SetLevel(DegradationLevel.SkipLowPriority);
            _currentDegradation = DegradationLevel.SkipLowPriority;
            
            Logger.Info("Simulation", "Degradation Level 1: Skipping low priority updates");
        }
        
        // Level 2: Reduce agent tick rate (15ms or 5 consecutive)
        if ((overBudget > 15000 || consecutiveOverruns >= 5) && 
            _currentDegradation < DegradationLevel.ReduceAgentTickRate) {
            
            DegradationManager.SetLevel(DegradationLevel.ReduceAgentTickRate);
            _currentDegradation = DegradationLevel.ReduceAgentTickRate;
            
            AgentManager.SetUpdateFrequency(0.5); // Half frequency
            Logger.Warn("Simulation", "Degradation Level 2: Agent update frequency reduced to 50%");
        }
        
        // Level 3: Reduce tick rate (30ms or 10 consecutive)
        if ((overBudget > 30000 || consecutiveOverruns >= 10) && 
            _currentDegradation < DegradationLevel.ReduceTickRate) {
            
            DegradationManager.SetLevel(DegradationLevel.ReduceTickRate);
            _currentDegradation = DegradationLevel.ReduceTickRate;
            
            TickRateManager.SetTargetRate(15); // Drop to 15 TPS
            BroadcastToPlayers("Server performance reduced due to high load");
            Logger.Error("Simulation", "Degradation Level 3: Tick rate reduced to 15 TPS");
        }
        
        // Level 4: Emergency mode (50ms or 20 consecutive)
        if ((overBudget > 50000 || consecutiveOverruns >= 20) && 
            _currentDegradation < DegradationLevel.Emergency) {
            
            DegradationManager.SetLevel(DegradationLevel.Emergency);
            _currentDegradation = DegradationLevel.Emergency;
            
            // Keep only critical systems
            EnableEmergencyMode();
            AlertService.SendCriticalAlert("Server in emergency degradation mode");
            Logger.Critical("Simulation", "Degradation Level 4: EMERGENCY MODE ACTIVATED");
        }
    }
    
    private void OnPerformanceRestored() {
        Logger.Info("Simulation", "Performance restored, reverting to normal operation");
        
        DegradationManager.RestoreNormal();
        AgentManager.ResetUpdateFrequency();
        TickRateManager.RestoreDefault();
        
        _currentDegradation = DegradationLevel.Normal;
        
        if (_currentDegradation >= DegradationLevel.ReduceTickRate) {
            BroadcastToPlayers("Server performance restored to normal");
        }
    }
    
    private void EnableEmergencyMode() {
        // Critical systems only
        SystemManager.DisableSystem(SystemType.Analytics);
        SystemManager.DisableSystem(SystemType.AchievementTracking);
        SystemManager.DisableSystem(SystemType.EcosystemSimulation);
        SystemManager.DisableSystem(SystemType.CombatAI);
        
        // Reduce network sync
        NetworkManager.SetSyncRate(5); // 5 TPS
        
        // Disable agent complex behaviors
        AgentManager.EnableSafeModeOnly();
    }
}
```

### AI System Failure Isolation

```csharp
public class AgentErrorHandler {
    private HashSet<Guid> _agentsInSafeMode = new();
    private const int SAFE_MODE_COOLDOWN_MINUTES = 5;
    
    public void ExecuteAgentTick(Agent agent) {
        try {
            if (_agentsInSafeMode.Contains(agent.Id)) {
                ExecuteSafeModeBehavior(agent);
            } else {
                agent.ExecuteTick();
            }
        }
        catch (Exception ex) {
            HandleAgentException(agent, ex);
        }
    }
    
    private void HandleAgentException(Agent agent, Exception ex) {
        Logger.Error("AI", $"Agent {agent.Name} ({agent.Id}) threw exception", ex,
            new { 
                AgentId = agent.Id, 
                AgentName = agent.Name,
                CurrentState = agent.CurrentState,
                Location = agent.Position,
                Tick = GameState.CurrentTick
            });
        
        // Put agent in safe mode
        _agentsInSafeMode.Add(agent.Id);
        agent.SetSafeMode(true);
        
        // Schedule retry
        ScheduleTask(() => AttemptAgentRecovery(agent.Id), 
            delayMs: SAFE_MODE_COOLDOWN_MINUTES * 60 * 1000);
        
        // Notify admins if many agents affected
        if (_agentsInSafeMode.Count > 10) {
            AlertService.SendAlert($"Multiple agents in safe mode ({_agentsInSafeMode.Count})");
        }
        
        // Continue with other agents (don't crash the system)
    }
    
    private void ExecuteSafeModeBehavior(Agent agent) {
        // Minimal survival behavior only
        
        // Eat if hungry
        if (agent.Needs.Hunger > 0.7f) {
            var food = FindNearestFood(agent);
            if (food != null) {
                agent.Consume(food);
            }
        }
        
        // Sleep if tired
        if (agent.Needs.Energy < 0.2f) {
            agent.Sleep();
        }
        
        // Basic movement (wander randomly if stuck)
        if (agent.IsStuck) {
            agent.Wander();
        }
        
        // No complex economic, social, or political behaviors
    }
    
    private void AttemptAgentRecovery(Guid agentId) {
        if (!_agentsInSafeMode.Contains(agentId)) return;
        
        var agent = AgentManager.GetAgent(agentId);
        if (agent == null) return;
        
        try {
            // Test if agent can execute normally
            agent.ExecuteTick();
            
            // Success - remove from safe mode
            _agentsInSafeMode.Remove(agentId);
            agent.SetSafeMode(false);
            
            Logger.Info("AI", $"Agent {agent.Name} recovered from safe mode");
        }
        catch (Exception ex) {
            Logger.Warn("AI", $"Agent {agent.Name} recovery failed, remaining in safe mode", ex);
            // Reschedule for later
            ScheduleTask(() => AttemptAgentRecovery(agentId), 
                delayMs: SAFE_MODE_COOLDOWN_MINUTES * 60 * 1000);
        }
    }
}
```

### Memory Pressure Management

```csharp
public class MemoryMonitor {
    private readonly long _memoryLimitBytes;
    private const float WARNING_THRESHOLD = 0.90f;
    private const float CRITICAL_THRESHOLD = 0.95f;
    private const float EMERGENCY_THRESHOLD = 0.98f;
    
    public MemoryMonitor(long memoryLimitGb = 8) {
        _memoryLimitBytes = memoryLimitGb * 1024L * 1024L * 1024L;
    }
    
    public void CheckMemoryPressure() {
        var usedMemory = GC.GetTotalMemory(false);
        var usageRatio = (float)usedMemory / _memoryLimitBytes;
        
        if (usageRatio > EMERGENCY_THRESHOLD) {
            HandleEmergencyMemoryPressure(usedMemory);
        }
        else if (usageRatio > CRITICAL_THRESHOLD) {
            HandleCriticalMemoryPressure(usedMemory);
        }
        else if (usageRatio > WARNING_THRESHOLD) {
            HandleWarningMemoryPressure(usedMemory);
        }
    }
    
    private void HandleWarningMemoryPressure(long usedMemory) {
        Logger.Warn("Memory", $"Memory pressure: {(usedMemory / 1024 / 1024)}MB used (90%)");
        
        // Level 1: Gentle cleanup
        GC.Collect(0, GCCollectionMode.Optimized, false);
        
        // Reduce cache sizes
        CacheManager.ReduceAllCaches(factor: 0.8);
        
        // Unload distant chunks for disconnected players
        ChunkManager.UnloadDistantChunks(maxDistance: 5);
    }
    
    private void HandleCriticalMemoryPressure(long usedMemory) {
        Logger.Critical("Memory", $"Memory critical: {(usedMemory / 1024 / 1024)}MB used (95%)");
        
        // Level 2: Aggressive cleanup
        GC.Collect(2, GCCollectionMode.Aggressive, true);
        
        // Stop accepting new players
        PlayerManager.LockNewConnections();
        
        // Reduce all caches significantly
        CacheManager.ReduceAllCaches(factor: 0.5);
        
        // Unload all chunks beyond render distance
        ChunkManager.UnloadAllDistantChunks();
        
        // Reduce AI cache sizes
        AgentManager.ReduceCacheSizes(factor: 0.5);
        
        // Clear old chat history
        ChatManager.TrimHistory(keepLastN: 100);
        
        AlertService.SendAlert("Memory pressure critical - new connections locked");
    }
    
    private async void HandleEmergencyMemoryPressure(long usedMemory) {
        Logger.Critical("Memory", $"Memory EMERGENCY: {(usedMemory / 1024 / 1024)}MB used (98%)");
        
        // Level 3: Emergency measures
        
        // Force immediate world save
        await WorldManager.ForceSave();
        
        // Schedule graceful restart
        WorldManager.ScheduleRestart(
            reason: "Memory exhaustion",
            delayMinutes: 5,
            warningMessage: "Server restarting in 5 minutes for maintenance"
        );
        
        // Reduce to bare minimum operations
        EnableMinimalMode();
        
        AlertService.SendCriticalAlert("EMERGENCY: Server restart scheduled due to memory pressure");
    }
    
    private void EnableMinimalMode() {
        // Disable all non-critical systems
        SystemManager.DisableAllExcept(
            SystemType.WorldTick,
            SystemType.PlayerConnection,
            SystemType.DatabaseWrite
        );
        
        // Minimal agent updates
        AgentManager.SetGlobalUpdateRate(0.1); // 10% normal rate
        
        // Minimal network sync
        NetworkManager.SetSyncRate(2); // 2 TPS
    }
}
```

---

## 6. Data Corruption Recovery

### Event Corruption Detection

```csharp
public class EventIntegrityChecker {
    public bool VerifyEventIntegrity(WorldEvent evt) {
        // Verify checksum
        var computedChecksum = ComputeChecksum(evt);
        if (computedChecksum != evt.Checksum) {
            Logger.Error("Data", "Event checksum mismatch",
                new { EventId = evt.Id, EventType = evt.Type, Tick = evt.Tick });
            return false;
        }
        
        // Verify timestamp is reasonable
        if (evt.Timestamp > DateTime.UtcNow.AddMinutes(1) || 
            evt.Timestamp < DateTime.UtcNow.AddDays(-30)) {
            Logger.Error("Data", "Event timestamp invalid",
                new { EventId = evt.Id, Timestamp = evt.Timestamp });
            return false;
        }
        
        // Verify tick sequence
        if (evt.Tick < 0 || evt.Tick > GameState.CurrentTick + 100) {
            Logger.Error("Data", "Event tick out of range",
                new { EventId = evt.Id, EventTick = evt.Tick, CurrentTick = GameState.CurrentTick });
            return false;
        }
        
        // Verify data structure
        if (!ValidateEventData(evt)) {
            Logger.Error("Data", "Event data validation failed",
                new { EventId = evt.Id, EventType = evt.Type });
            return false;
        }
        
        return true;
    }
    
    private ulong ComputeChecksum(WorldEvent evt) {
        using (var sha = SHA256.Create()) {
            var data = JsonSerializer.SerializeToUtf8Bytes(new {
                evt.Type,
                evt.Timestamp,
                evt.Tick,
                evt.EntityId,
                evt.Data
            });
            
            var hash = sha.ComputeHash(data);
            return BitConverter.ToUInt64(hash, 0);
        }
    }
}

public class EventReplayEngine {
    private const int MAX_CONSECUTIVE_CORRUPT_EVENTS = 5;
    
    public async Task<ReplayResult> ReplayEventsFromSnapshot(
        Snapshot snapshot, 
        List<WorldEvent> events) {
        
        var worldState = await LoadSnapshot(snapshot);
        var corruptEvents = new List<WorldEvent>();
        var appliedEvents = new List<WorldEvent>();
        int consecutiveCorrupt = 0;
        
        foreach (var evt in events.OrderBy(e => e.Tick)) {
            if (!EventIntegrityChecker.VerifyEventIntegrity(evt)) {
                corruptEvents.Add(evt);
                consecutiveCorrupt++;
                
                if (consecutiveCorrupt >= MAX_CONSECUTIVE_CORRUPT_EVENTS) {
                    Logger.Critical("Data", 
                        $"Too many consecutive corrupt events ({consecutiveCorrupt}), stopping replay");
                    break;
                }
                
                continue;
            }
            
            consecutiveCorrupt = 0;
            
            try {
                ApplyEvent(worldState, evt);
                appliedEvents.Add(evt);
            }
            catch (Exception ex) {
                Logger.Error("Data", $"Failed to apply event {evt.Id}", ex);
                corruptEvents.Add(evt);
            }
        }
        
        return new ReplayResult {
            FinalState = worldState,
            AppliedEvents = appliedEvents,
            CorruptEvents = corruptEvents,
            Success = corruptEvents.Count < events.Count * 0.1 // Less than 10% corrupt
        };
    }
}
```

### Database Corruption Recovery

```
Detection Mechanisms:
  - Checksum verification on table reads
  - Foreign key constraint violations
  - JSONB parse errors
  - Impossible value detection (negative money, future timestamps)
  - Row count anomalies
  - Index corruption detection

Isolation Procedure:
  1. Identify corrupted table(s) via error patterns
  2. Lock table for writes (read-only mode)
  3. Quarantine corrupted records
  4. Continue with other tables

Recovery Procedure:
  1. RESTORE:
     - Identify last known good backup (point-in-time)
     - Restore corrupted table(s) from backup
     - Verify restored data integrity
  
  2. REPLAY:
     - Identify events since backup timestamp
     - Replay events in order
     - Skip events affecting quarantined records
     - Log all replayed operations
  
  3. REPAIR:
     - For unrecoverable records:
       * Create placeholder with default values
       * Flag for manual review
       * Notify affected players
  
  4. VERIFY:
     - Run integrity checks on recovered data
     - Compare row counts and checksums
     - Test critical queries

Prevention:
  - Hourly automated snapshots
  - Nightly integrity checks (DBCC CHECKDB equivalent)
  - All writes wrapped in transactions
  - Event sourcing for audit trail
  - Regular backup restoration testing
```

```csharp
public class DatabaseCorruptionRecovery {
    public async Task RecoverCorruptedTable(string tableName, DateTime lastKnownGood) {
        Logger.Critical("Data", $"Starting corruption recovery for table: {tableName}");
        
        // 1. Lock table
        await LockTable(tableName, LockMode.ReadOnly);
        
        // 2. Find last good backup
        var backup = await FindBackup(tableName, lastKnownGood);
        
        // 3. Restore
        await RestoreTableFromBackup(tableName, backup);
        
        // 4. Replay events
        var events = await GetEventsSince(backup.Timestamp);
        var replayResult = await ReplayEvents(tableName, events);
        
        // 5. Handle failed replays
        foreach (var failedEvent in replayResult.FailedEvents) {
            await QuarantineRecord(tableName, failedEvent.EntityId);
        }
        
        // 6. Unlock and verify
        await UnlockTable(tableName);
        await VerifyTableIntegrity(tableName);
        
        Logger.Critical("Data", $"Corruption recovery complete for {tableName}",
            new { 
                RestoredFrom = backup.Timestamp,
                EventsReplayed = replayResult.AppliedCount,
                EventsFailed = replayResult.FailedCount
            });
    }
}
```

---

## 7. Graceful Degradation System

### Degradation Levels

```csharp
public enum DegradationLevel {
    Normal = 0,               // Full functionality
    ReducedVisuals = 1,       // Lower particle counts, simplified effects
    SkipLowPriority = 2,      // Skip analytics, detailed logging
    ReduceAgentTickRate = 3,  // AI updates less frequently
    ReduceTickRate = 4,       // Drop from 20 to 15 TPS
    SkipMediumPriority = 5,   // Skip ecosystem, weather updates
    Emergency = 6,            // Critical systems only
    SafeMode = 7              // Bare minimum to stay alive
}

public class DegradationManager {
    private DegradationLevel _currentLevel = DegradationLevel.Normal;
    private Dictionary<DegradationLevel, List<SystemType>> _disabledSystems;
    private HashSet<SystemType> _currentlyDisabled = new();
    
    public DegradationManager() {
        InitializeDegradationMap();
    }
    
    private void InitializeDegradationMap() {
        _disabledSystems = new Dictionary<DegradationLevel, List<SystemType>> {
            [DegradationLevel.ReducedVisuals] = new() { 
                SystemType.ParticleEffects, 
                SystemType.DynamicLighting 
            },
            [DegradationLevel.SkipLowPriority] = new() { 
                SystemType.Analytics, 
                SystemType.AchievementTracking,
                SystemType.DetailedLogging 
            },
            [DegradationLevel.ReduceAgentTickRate] = new() { 
                // Agent systems continue but at reduced rate
            },
            [DegradationLevel.SkipMediumPriority] = new() { 
                SystemType.EcosystemSimulation,
                SystemType.WeatherSimulation,
                SystemType.EconomicForecasting 
            },
            [DegradationLevel.Emergency] = new() { 
                SystemType.Analytics,
                SystemType.AchievementTracking,
                SystemType.EcosystemSimulation,
                SystemType.CombatAI,
                SystemType.SocialFeatures,
                SystemType.MarketSimulation
            },
            [DegradationLevel.SafeMode] = new() { 
                SystemType.Analytics,
                SystemType.AchievementTracking,
                SystemType.EcosystemSimulation,
                SystemType.CombatAI,
                SystemType.SocialFeatures,
                SystemType.MarketSimulation,
                SystemType.QuestSystem,
                SystemType.DecorationSystem
            }
        };
    }
    
    public void SetLevel(DegradationLevel level) {
        if (level <= _currentLevel) return;
        
        Logger.Warn("Degradation", $"Escalating to level: {level}");
        
        var previousLevel = _currentLevel;
        _currentLevel = level;
        
        // Apply new restrictions
        ApplyDegradation(level);
        
        // Notify monitoring
        Metrics.RecordDegradationEscalation(level);
        
        // Notify players if significant
        if (level >= DegradationLevel.ReduceTickRate) {
            BroadcastDegradationNotice(level);
        }
    }
    
    private void ApplyDegradation(DegradationLevel level) {
        // Disable systems for this level and all lower levels
        for (var l = DegradationLevel.Normal + 1; l <= level; l++) {
            if (_disabledSystems.TryGetValue(l, out var systems)) {
                foreach (var system in systems) {
                    if (!_currentlyDisabled.Contains(system)) {
                        DisableSystem(system);
                        _currentlyDisabled.Add(system);
                    }
                }
            }
        }
        
        // Special handling for specific levels
        switch (level) {
            case DegradationLevel.ReduceAgentTickRate:
                AgentManager.SetGlobalUpdateRate(0.5);
                break;
                
            case DegradationLevel.ReduceTickRate:
                TickRateManager.SetTargetRate(15);
                break;
                
            case DegradationLevel.Emergency:
                NetworkManager.SetSyncRate(5);
                break;
                
            case DegradationLevel.SafeMode:
                NetworkManager.SetSyncRate(2);
                AgentManager.SetGlobalUpdateRate(0.1);
                break;
        }
    }
    
    public void RestoreNormal() {
        if (_currentLevel == DegradationLevel.Normal) return;
        
        Logger.Info("Degradation", "Restoring normal operation");
        
        // Re-enable all systems
        foreach (var system in _currentlyDisabled) {
            EnableSystem(system);
        }
        _currentlyDisabled.Clear();
        
        // Reset special settings
        AgentManager.ResetUpdateFrequency();
        TickRateManager.RestoreDefault();
        NetworkManager.RestoreDefaultSyncRate();
        
        _currentLevel = DegradationLevel.Normal;
        
        // Notify players
        BroadcastMessage("Server performance restored to normal");
    }
    
    public bool IsSystemEnabled(SystemType system) {
        return !_currentlyDisabled.Contains(system);
    }
}
```

### Degradation Triggers

| Trigger Condition | Initial Degradation | Escalation Path |
|-------------------|---------------------|-----------------|
| Tick overrun 5ms | SkipLowPriority | → ReduceAgentTickRate (15ms) → ReduceTickRate (30ms) → Emergency (50ms) |
| Memory 90% | SkipLowPriority | → SkipMediumPriority (95%) → Emergency (98%) |
| DB timeout rate 10% | SkipLowPriority | → SkipMediumPriority (25%) → Offline mode (50%) |
| Network latency 200ms | ReducedVisuals | → ReduceTickRate (500ms) → Emergency (1000ms) |
| Agent error rate 1% | Individual safe mode | → Global safe mode (5%) → Disable AI (10%) |

---

## 8. Health Checks & Monitoring

### Health Check Endpoints

```csharp
public class HealthCheckController {
    private readonly IDatabaseService _db;
    private readonly ITickLoopService _tickLoop;
    private readonly INetworkService _network;
    
    public IActionResult Check() {
        var startTime = DateTime.UtcNow;
        
        var checks = new Dictionary<string, HealthCheckResult> {
            ["database"] = CheckDatabase(),
            ["tick_loop"] = CheckTickLoop(),
            ["memory"] = CheckMemory(),
            ["disk_space"] = CheckDiskSpace(),
            ["network"] = CheckNetworkHealth()
        };
        
        var allHealthy = checks.Values.All(c => c.Healthy);
        var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        
        var result = new HealthCheckResponse {
            Status = allHealthy ? "healthy" : "unhealthy",
            Timestamp = DateTime.UtcNow,
            ResponseTimeMs = responseTime,
            Checks = checks
        };
        
        if (allHealthy) {
            return Ok(result);
        } else {
            return StatusCode(503, result);
        }
    }
    
    private HealthCheckResult CheckDatabase() {
        try {
            var sw = Stopwatch.StartNew();
            _db.ExecuteQuery("SELECT 1");
            sw.Stop();
            
            return new HealthCheckResult {
                Healthy = sw.ElapsedMilliseconds < 1000,
                ResponseTimeMs = sw.ElapsedMilliseconds,
                Message = sw.ElapsedMilliseconds < 1000 ? "OK" : "Slow"
            };
        }
        catch (Exception ex) {
            return new HealthCheckResult {
                Healthy = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
    
    private HealthCheckResult CheckTickLoop() {
        var status = _tickLoop.GetStatus();
        
        return new HealthCheckResult {
            Healthy = status.IsRunning && status.LastTickDurationMs < 50,
            ResponseTimeMs = status.LastTickDurationMs,
            Message = $"Running: {status.IsRunning}, TPS: {status.CurrentTPS:F1}"
        };
    }
    
    private HealthCheckResult CheckMemory() {
        var usedMemory = GC.GetTotalMemory(false);
        var memoryLimit = 8L * 1024 * 1024 * 1024;
        var usagePercent = (double)usedMemory / memoryLimit * 100;
        
        return new HealthCheckResult {
            Healthy = usagePercent < 85,
            ResponseTimeMs = 0,
            Message = $"{usagePercent:F1}% used ({usedMemory / 1024 / 1024}MB / {memoryLimit / 1024 / 1024}MB)"
        };
    }
    
    private HealthCheckResult CheckDiskSpace() {
        var drive = new DriveInfo("C");
        var freePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
        
        return new HealthCheckResult {
            Healthy = freePercent > 10,
            ResponseTimeMs = 0,
            Message = $"{freePercent:F1}% free ({drive.AvailableFreeSpace / 1024 / 1024 / 1024}GB)"
        };
    }
    
    private HealthCheckResult CheckNetworkHealth() {
        var metrics = _network.GetHealthMetrics();
        
        return new HealthCheckResult {
            Healthy = metrics.AverageLatencyMs < 100 && metrics.PacketLoss < 0.01,
            ResponseTimeMs = (int)metrics.AverageLatencyMs,
            Message = $"Latency: {metrics.AverageLatencyMs:F1}ms, Loss: {metrics.PacketLoss:P2}"
        };
    }
}

public class HealthCheckResult {
    public bool Healthy { get; set; }
    public double ResponseTimeMs { get; set; }
    public string Message { get; set; }
}
```

### Alerting Thresholds

```csharp
public class AlertingConfiguration {
    // Performance Alerts
    public const double MIN_FPS_THRESHOLD = 15.0;
    public const int MIN_FPS_DURATION_SECONDS = 30;
    
    // Resource Alerts
    public const double MEMORY_WARNING_THRESHOLD = 0.85;
    public const int MEMORY_WARNING_DURATION_MINUTES = 5;
    public const double DISK_WARNING_THRESHOLD = 0.10;
    
    // Database Alerts
    public const int DB_RESPONSE_TIME_WARNING_MS = 1000;
    public const double DB_ERROR_RATE_WARNING = 0.01;
    
    // Network Alerts
    public const double PLAYER_DROP_RATE_WARNING = 0.50;
    public const int PLAYER_DROP_WINDOW_MINUTES = 5;
    
    // AI Alerts
    public const double AGENT_ERROR_RATE_WARNING = 0.01;
    public const int SAFE_MODE_AGENT_COUNT_WARNING = 10;
}

public class AlertService {
    public async Task CheckAndAlert() {
        // Check FPS
        var fps = Metrics.GetCurrentFPS();
        if (fps < AlertingConfiguration.MIN_FPS_THRESHOLD) {
            if (Metrics.FPSBelowThresholdFor(AlertingConfiguration.MIN_FPS_DURATION_SECONDS)) {
                await SendAlert("Server FPS critically low", AlertLevel.Warning,
                    new { FPS = fps, Duration = AlertingConfiguration.MIN_FPS_DURATION_SECONDS });
            }
        }
        
        // Check memory
        var memoryUsage = Metrics.GetMemoryUsageRatio();
        if (memoryUsage > AlertingConfiguration.MEMORY_WARNING_THRESHOLD) {
            if (Metrics.MemoryAboveThresholdFor(AlertingConfiguration.MEMORY_WARNING_DURATION_MINUTES)) {
                await SendAlert("Memory usage sustained high", AlertLevel.Warning,
                    new { Usage = $"{memoryUsage:P0}", Duration = AlertingConfiguration.MEMORY_WARNING_DURATION_MINUTES });
            }
        }
        
        // Check database
        var dbMetrics = await Metrics.GetDatabaseMetrics();
        if (dbMetrics.AverageResponseTime > AlertingConfiguration.DB_RESPONSE_TIME_WARNING_MS) {
            await SendAlert("Database response time elevated", AlertLevel.Warning,
                new { AvgResponseTime = dbMetrics.AverageResponseTime });
        }
        
        // Check player drops
        var playerDropRate = Metrics.GetPlayerDropRate(AlertingConfiguration.PLAYER_DROP_WINDOW_MINUTES);
        if (playerDropRate > AlertingConfiguration.PLAYER_DROP_RATE_WARNING) {
            await SendAlert("Unusual player disconnection rate", AlertLevel.Critical,
                new { DropRate = $"{playerDropRate:P0}", Window = AlertingConfiguration.PLAYER_DROP_WINDOW_MINUTES });
        }
        
        // Check AI errors
        var agentErrorRate = Metrics.GetAgentErrorRate();
        if (agentErrorRate > AlertingConfiguration.AGENT_ERROR_RATE_WARNING) {
            await SendAlert("Agent error rate elevated", AlertLevel.Warning,
                new { ErrorRate = $"{agentErrorRate:P2}" });
        }
        
        // Check safe mode agents
        var safeModeCount = AgentManager.GetSafeModeAgentCount();
        if (safeModeCount > AlertingConfiguration.SAFE_MODE_AGENT_COUNT_WARNING) {
            await SendAlert("Multiple agents in safe mode", AlertLevel.Warning,
                new { SafeModeCount = safeModeCount });
        }
    }
}
```

---

## 9. Rollback Procedures

### World Rollback Protocol

```
SCENARIO: Critical bug discovered, need to rollback 2 hours

TIMELINE:
  T+0min: Bug discovered and confirmed
  T+0min: Decision made to rollback
  
  T+0min: ANNOUNCE
    - Message to all players: "Server will restart in 15 minutes for emergency maintenance"
    - Message: "Approximately 2 hours of progress will be lost"
    - Message: "We sincerely apologize for the inconvenience"
    - Lock new player connections
  
  T+5min: PREPARATION
    - Stop accepting new trade transactions
    - Wait for current combat to complete
    - Alert admin team via emergency channel
    - Prepare rollback checklist
  
  T+10min: FINAL WARNING
    - Broadcast: "Server restart in 5 minutes - please complete current activities"
    - Force save all online player states
    - Generate pre-rollback snapshot
  
  T+15min: SHUTDOWN
    - Graceful server shutdown
    - Verify all processes terminated
    - Create backup of current state (for investigation)
  
  T+16min: RESTORE
    - Locate snapshot from 2 hours ago
    - Verify snapshot integrity
    - Restore database from snapshot
    - Verify database restoration
  
  T+20min: REPLAY
    - Identify events between snapshot and 1 hour ago
    - Replay safe events (movement, chat, basic trades)
    - Skip events during bug window (last hour)
    - Log all replayed events
  
  T+25min: VERIFICATION
    - Run integrity checks
    - Verify critical tables (players, inventory, world state)
    - Test login with admin account
    - Check agent states
  
  T+30min: RESTART
    - Start server
    - Monitor startup logs
    - Verify all systems initialized
    - Unlock player connections
  
  T+31min: ANNOUNCE RECOVERY
    - Message: "Server is back online"
    - Message: "Rollback complete - approximately 1 hour of progress preserved"
    - Open support channel for issues

COMPENSATION:
  - Identify affected players (logged in during rollback window)
  - Grant compensation package:
    * 5000 credits
    * Rare resource bundle
    * Experience boost (24 hours)
  - Send personal apology message to each affected player
  - Document compensation in player records

POST-INCIDENT:
  - Publish incident report on forums
  - Schedule bug fix deployment
  - Review and improve testing procedures
  - Update rollback runbook with lessons learned
```

```csharp
public class RollbackManager {
    public async Task ExecuteRollback(TimeSpan rollbackDuration, string reason) {
        var targetTime = DateTime.UtcNow - rollbackDuration;
        
        Logger.Critical("Rollback", $"Initiating rollback to {targetTime:yyyy-MM-dd HH:mm:ss}",
            new { Reason = reason, RollbackDuration = rollbackDuration.ToString() });
        
        // Phase 1: Announce and prepare
        await AnnounceRollback(rollbackDuration);
        await Task.Delay(TimeSpan.FromMinutes(5));
        
        // Phase 2: Final warning and save
        await BroadcastMessage("Server restart in 5 minutes - final warning");
        await ForceSaveAllPlayers();
        await Task.Delay(TimeSpan.FromMinutes(5));
        
        // Phase 3: Shutdown
        await GracefulShutdown();
        
        // Phase 4: Restore
        var snapshot = await FindNearestSnapshot(targetTime);
        await RestoreFromSnapshot(snapshot);
        
        // Phase 5: Replay
        var events = await GetEventsSince(snapshot.Timestamp);
        var cutoffTime = DateTime.UtcNow - TimeSpan.FromHours(1); // Replay up to 1 hour ago
        var eventsToReplay = events.Where(e => e.Timestamp < cutoffTime).ToList();
        
        await ReplayEvents(eventsToReplay);
        
        // Phase 6: Verify and restart
        await VerifyWorldIntegrity();
        await StartServer();
        
        // Phase 7: Compensate
        var affectedPlayers = await IdentifyAffectedPlayers(targetTime);
        await CompensatePlayers(affectedPlayers);
        
        Logger.Critical("Rollback", "Rollback complete",
            new { AffectedPlayers = affectedPlayers.Count });
    }
    
    private async Task CompensatePlayers(List<Player> players) {
        foreach (var player in players) {
            try {
                await GrantCompensation(player, new CompensationPackage {
                    Credits = 5000,
                    Resources = new[] { "RareOre", "AncientArtifact", "PremiumFood" },
                    ExperienceBoostDuration = TimeSpan.FromHours(24)
                });
                
                await SendMessage(player.Id, new ApologyMessage {
                    Subject = "Compensation for Service Disruption",
                    Body = "We apologize for the recent rollback..."
                });
            }
            catch (Exception ex) {
                Logger.Error("Rollback", $"Failed to compensate player {player.Name}", ex);
            }
        }
    }
}
```

---

## 10. Logging Strategy

### Log Levels

```
TRACE (0): 
  - Method entry/exit
  - Variable values
  - Loop iterations
  - DISABLED in production
  
DEBUG (1):
  - State transitions
  - Cache hits/misses
  - Timing information
  - DISABLED in production
  
INFO (2):
  - Player login/logout
  - World saves
  - Scheduled maintenance
  - Configuration changes
  - Expected state changes
  
WARN (3):
  - Recoverable issues
  - Performance degradation
  - Retried operations
  - Deprecated feature usage
  - Unusual but valid states
  
ERROR (4):
  - Failed operations
  - Data inconsistencies
  - Connection failures
  - Exceptions caught
  - Required manual intervention
  
FATAL (5):
  - Server crash imminent
  - Data corruption
  - Security breach
  - Immediate attention required
```

### Structured Log Format

```csharp
public struct LogEntry {
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Category { get; set; }      // "Network", "Database", "AI", etc.
    public string Message { get; set; }
    public Guid? EntityId { get; set; }       // Related entity (player, agent, etc.)
    public long? Tick { get; set; }           // Game tick (optional)
    public string TraceId { get; set; }       // Distributed tracing ID
    public Dictionary<string, object> Context { get; set; }
    public string StackTrace { get; set; }    // For errors
    public string ServerInstance { get; set; }
    public string Version { get; set; }
}

public static class Logger {
    private static readonly string _version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
    private static readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
    
    public static void Log(LogLevel level, string category, string message, 
        Exception exception = null, object context = null) {
        
        var entry = new LogEntry {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Category = category,
            Message = message,
            Tick = GameState?.CurrentTick,
            TraceId = Activity.Current?.Id,
            ServerInstance = _instanceId,
            Version = _version,
            Context = context != null ? ConvertToDictionary(context) : null,
            StackTrace = exception?.StackTrace
        };
        
        // Write to appropriate sinks
        WriteLog(entry);
        
        // Alert if critical
        if (level >= LogLevel.Error) {
            AlertService.SendLogAlert(entry);
        }
    }
    
    // Convenience methods
    public static void Trace(string category, string message, object context = null) 
        => Log(LogLevel.Trace, category, message, null, context);
    
    public static void Debug(string category, string message, object context = null) 
        => Log(LogLevel.Debug, category, message, null, context);
    
    public static void Info(string category, string message, object context = null) 
        => Log(LogLevel.Info, category, message, null, context);
    
    public static void Warn(string category, string message, object context = null) 
        => Log(LogLevel.Warn, category, message, null, context);
    
    public static void Error(string category, string message, Exception ex = null, object context = null) 
        => Log(LogLevel.Error, category, message, ex, context);
    
    public static void Critical(string category, string message, Exception ex = null, object context = null) 
        => Log(LogLevel.Fatal, category, message, ex, context);
    
    private static Dictionary<string, object> ConvertToDictionary(object context) {
        if (context is Dictionary<string, object> dict) return dict;
        
        var json = JsonSerializer.Serialize(context);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
    }
}

// JSON Output Format:
// {
//   "timestamp": "2026-02-01T12:34:56.789Z",
//   "level": "ERROR",
//   "category": "Database",
//   "message": "Connection timeout after 5000ms",
//   "entity_id": "550e8400-e29b-41d4-a716-446655440000",
//   "tick": 1234567,
//   "trace_id": "abc123def456",
//   "context": {
//     "retry_count": 3,
//     "query_type": "SELECT",
//     "table": "agents",
//     "duration_ms": 5000
//   },
//   "stack_trace": "...",
//   "server_instance": "a1b2c3d4",
//   "version": "1.2.3"
// }
```

### Log Storage and Rotation

```
Log Retention Policy:
  
  TRACE/DEBUG:
    - Retention: 0 days (not stored in production)
    - Storage: Local only (development)
  
  INFO:
    - Retention: 7 days hot, 30 days cold
    - Storage: Elasticsearch/Splunk
    - Volume: ~1GB/day
  
  WARN:
    - Retention: 30 days hot, 90 days cold
    - Storage: Elasticsearch/Splunk
    - Volume: ~100MB/day
  
  ERROR:
    - Retention: 90 days hot, 1 year cold
    - Storage: Elasticsearch + S3 archive
    - Volume: ~50MB/day
  
  FATAL:
    - Retention: Permanent
    - Storage: Elasticsearch + S3 + PagerDuty
    - Volume: ~5MB/day

Rotation Strategy:
  - Rotate every 100MB or 1 hour
  - Compress rotated logs (gzip)
  - Upload to S3 after 24 hours
  - Delete local copies after 7 days

Sampling:
  - INFO: 100% (all logged)
  - DEBUG: 1% in production (canary instances)
  - TRACE: Disabled
```

---

## Summary

This error handling specification ensures the Societies server maintains stability and player experience even under adverse conditions. Key takeaways:

1. **Graceful Degradation**: The system degrades functionality progressively rather than failing completely
2. **Automatic Recovery**: Most failures trigger automatic recovery procedures without human intervention
3. **Data Integrity**: Player progress is protected through buffering, checkpoints, and transaction safety
4. **Transparency**: Players are informed when performance is degraded
5. **Observability**: Comprehensive logging enables rapid diagnosis and resolution

All error handling code must:
- Catch exceptions at system boundaries
- Never expose internal details to clients
- Log sufficient context for debugging
- Attempt recovery before escalation
- Maintain player state consistency

---

## Related Documents

- [09-network-architecture.md](./09-network-architecture.md) - Network protocol details
- [10-data-persistence.md](./10-data-persistence.md) - Database and state management
- [Session 3: Core Gameplay Loops](../session-3-core-gameplay-loops/) - Game systems that require error handling
- [Session 4: Progression & Balance](../session-4-progression-and-balance/) - Player state management

---

*Document Version: 1.0*  
*Last Updated: 2026-02-01*
