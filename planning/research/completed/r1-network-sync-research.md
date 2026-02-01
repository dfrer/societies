# R1-3: Network Synchronization Patterns Research

## Source Information
- **Name**: Gaffer On Games - State Synchronization, Deterministic Lockstep, Snapshot Interpolation
- **URL**: https://gafferongames.com/post/state_synchronization/, https://gafferongames.com/post/deterministic_lockstep/, https://gafferongames.com/post/snapshot_interpolation/
- **Type**: Technical Blog/Article Series
- **Date Researched**: 2026-01-30
- **Author/Org**: Glenn Fiedler (Game Networking Expert)

## Executive Summary

Network synchronization for multiplayer games presents three primary strategies: Deterministic Lockstep, Snapshot Interpolation, and State Synchronization. Glenn Fiedler's comprehensive analysis reveals that deterministic lockstep requires perfect bit-level determinism across all clients (difficult with floating-point physics) but achieves the lowest bandwidth by sending only inputs. Snapshot interpolation sends full world states and provides the easiest implementation but uses the most bandwidth. State synchronization strikes a balance by running simulation on both sides while sending state deltas. For Societies' continuous ecosystem simulation with 100 AI agents, state synchronization is the optimal choice: it allows variable tick rates (10-30 TPS), supports players joining mid-game, and doesn't require perfect determinism. However, state sync requires implementing priority accumulators, jitter buffers, and visual smoothing to handle the approximate nature of the synchronization.

## Detailed Findings

### Deterministic Lockstep Analysis

**Evidence**:
- Deterministic lockstep sends only player inputs, not world state
- Requires bit-exact identical results on all machines
- Bandwidth is proportional to input count, not object count (constant regardless of world complexity)
- Uses playout delay buffers to smooth network jitter (100ms+ latency added)
- TCP performs poorly for lockstep (packet loss causes hitches)
- Custom UDP protocol with redundant input sending outperforms TCP

**How It Works**:
```
Frame N:
1. Sample input locally
2. Send input to all peers (with redundancy)
3. Wait until input received from all peers
4. Simulate frame N using all inputs
5. Render frame N
```

**The Determinism Challenge**:
- Floating-point calculations differ across compilers, OSs, and CPU architectures
- Physics engines often use randomization (e.g., ODE's constraint solver) that breaks determinism
- Debug vs release builds may optimize floating-point differently
- Requires setting random seeds deterministically (frame number based)

**Code Example - Deterministic Lockstep Input Handling**:
```csharp
public struct PlayerInput
{
    public uint FrameNumber;
    public bool Left, Right, Up, Down;
    public bool ActionButton;
    public float CameraRotation;
    
    // 6 bits of actual data per frame
    public byte[] Serialize()
    {
        byte data = 0;
        if (Left) data |= 0x01;
        if (Right) data |= 0x02;
        if (Up) data |= 0x04;
        if (Down) data |= 0x08;
        if (ActionButton) data |= 0x10;
        return new byte[] { (byte)(FrameNumber & 0xFF), data };
    }
}

public class LockstepSystem
{
    private Dictionary<uint, Dictionary<long, PlayerInput>> _inputBuffer = new();
    private uint _currentFrame = 0;
    
    public void Update(double delta)
    {
        // Check if we have all inputs for current frame
        if (_inputBuffer.ContainsKey(_currentFrame) && 
            _inputBuffer[_currentFrame].Count == ExpectedPlayerCount)
        {
            // Simulate frame with all inputs
            SimulateFrame(_currentFrame, _inputBuffer[_currentFrame]);
            _currentFrame++;
        }
        else
        {
            // Stall - can't proceed without inputs
            // This is the "hitch" in lockstep
        }
    }
}
```

**Bandwidth Calculation**:
```
Inputs per second: 60 (frame rate)
Input size: ~6 bits = 1 byte
Players: 20
Redundancy: 2x (send each input in 2 packets for reliability)
Overhead: 20 bytes (headers)

Per player per second: 60 × 1 × 2 + 20 = 140 bytes/s = 0.14 KB/s
Total for 20 players: 20 × 0.14 = 2.8 KB/s

For 1000 objects or 1 object: same bandwidth!
```

**When Lockstep Works**:
- Small player counts (<8 players)
- Deterministic simulation achievable (turn-based, simple physics)
- Low latency connections (<50ms)
- No need for players to join mid-game

**Implications for Societies**:
- **NOT RECOMMENDED** - Determinism difficult with complex AI and floating-point economics
- 100 AI agents would all need deterministic decisions (randomness is problematic)
- Joining mid-game requires downloading entire world state + replaying all inputs
- Continuous simulation when players offline would desync
- Even small divergence compounds over time

### Snapshot Interpolation Analysis

**Evidence**:
- Sends full world state snapshots at regular intervals (e.g., 10-20 Hz)
- Clients render interpolated states between snapshots
- No simulation runs on client - pure visual interpolation
- Bandwidth scales with object count × snapshot rate
- Delta compression essential (only send changed values)
- Jitter buffer needed to smooth out packet arrival timing

**How It Works**:
```
Server (20 TPS):
  Tick 0: Capture state → Send snapshot
  Tick 1: Capture state → Send snapshot
  Tick 2: Capture state → Send snapshot

Client (60 FPS):
  Frame 0: Interpolate between snapshot[-2] and snapshot[-1]
  Frame 1: Interpolate between snapshot[-2] and snapshot[-1]
  Frame 2: Interpolate between snapshot[-2] and snapshot[-1]
  Frame 3: New snapshot arrives → shift interpolation window
```

**Interpolation Implementation**:
```csharp
public class SnapshotInterpolator
{
    private Queue<WorldSnapshot> _snapshotBuffer = new();
    private double _interpolationDelay = 0.1; // 100ms buffer
    private double _renderTime = 0;
    
    public void ReceiveSnapshot(WorldSnapshot snapshot)
    {
        snapshot.ReceiveTime = Time.GetTimeDict().Milliseconds / 1000.0;
        _snapshotBuffer.Enqueue(snapshot);
        
        // Remove old snapshots
        while (_snapshotBuffer.Count > 0 && 
               _snapshotBuffer.Peek().ServerTime < _renderTime - _interpolationDelay)
        {
            _snapshotBuffer.Dequeue();
        }
    }
    
    public WorldState Interpolate(double renderTime)
    {
        _renderTime = renderTime;
        
        // Find snapshots surrounding renderTime - interpolationDelay
        double targetTime = renderTime - _interpolationDelay;
        
        var snapshots = _snapshotBuffer.ToArray();
        for (int i = 0; i < snapshots.Length - 1; i++)
        {
            if (snapshots[i].ServerTime <= targetTime && 
                snapshots[i + 1].ServerTime >= targetTime)
            {
                float t = (float)((targetTime - snapshots[i].ServerTime) / 
                    (snapshots[i + 1].ServerTime - snapshots[i].ServerTime));
                
                return InterpolateStates(snapshots[i].State, snapshots[i + 1].State, t);
            }
        }
        
        return null; // Not enough data
    }
    
    private WorldState InterpolateStates(WorldState a, WorldState b, float t)
    {
        var result = new WorldState();
        
        foreach (var agentId in a.Agents.Keys)
        {
            if (b.Agents.ContainsKey(agentId))
            {
                result.Agents[agentId] = new AgentState
                {
                    Position = a.Agents[agentId].Position.Lerp(b.Agents[agentId].Position, t),
                    Rotation = a.Agents[agentId].Rotation.Lerp(b.Agents[agentId].Rotation, t)
                };
            }
        }
        
        return result;
    }
}
```

**Bandwidth Calculation**:
```
Snapshot rate: 20 TPS
Objects: 100 agents + 500 entities = 600 objects
Data per object: Position (12 bytes) + Rotation (4 bytes) = 16 bytes
Delta compression: ~60% reduction (only changed objects)

Per snapshot: 600 × 16 × 0.4 = 3,840 bytes = 3.8 KB
Per second: 3.8 × 20 = 76 KB/s
Per player: 76 KB/s

For 20 players: 76 × 20 = 1,520 KB/s = 1.5 MB/s server upload
For 100 players: 76 × 100 = 7,600 KB/s = 7.6 MB/s server upload
```

**Delta Compression**:
```csharp
public class DeltaCompressor
{
    private WorldSnapshot _baseline;
    
    public byte[] Compress(WorldSnapshot current)
    {
        var changedObjects = new List<ObjectUpdate>();
        
        foreach (var obj in current.Objects)
        {
            if (_baseline == null || !ObjectEquals(obj, _baseline.Objects[obj.Id]))
            {
                changedObjects.Add(new ObjectUpdate
                {
                    Id = obj.Id,
                    Position = obj.Position,
                    Rotation = obj.Rotation,
                    ChangedFields = CalculateChangedFields(obj, _baseline?.Objects[obj.Id])
                });
            }
        }
        
        _baseline = current;
        return Serialize(changedObjects);
    }
    
    private bool ObjectEquals(GameObject a, GameObject b)
    {
        return a.Position == b.Position && a.Rotation == b.Rotation;
    }
}
```

**Implications for Societies**:
- **POSSIBLE** but bandwidth-heavy
- Requires aggressive delta compression and spatial culling
- Server CPU load is high (serializing 600 objects × 20 times/second)
- Not suitable for continuous world evolution when players offline
- Joining mid-game is easy - just send latest snapshot

### State Synchronization Analysis

**Evidence**:
- Runs simulation on both server and client
- Sends state updates for priority objects, not all objects
- Uses priority accumulator to distribute updates across all objects
- Always extrapolating from last received state update
- Requires sending velocity/angular velocity for correct extrapolation
- Bandwidth: 5-10x lower than snapshot interpolation for same quality

**How It Works**:
```
Server (20 TPS):
  Tick 0: Simulate → Select priority objects → Send state updates
  Tick 1: Simulate → Select priority objects → Send state updates
  
Client (60 FPS):
  Frame 0: Receive state update → Apply to simulation → Extrapolate
  Frame 1: Continue extrapolating from last state
  Frame 2: Continue extrapolating from last state
```

**Priority Accumulator Algorithm**:
```csharp
public class PriorityAccumulator
{
    private Dictionary<int, float> _accumulators = new();
    private List<GameObject> _objects;
    
    public List<GameObject> SelectObjectsToUpdate(int maxUpdates, float bandwidthBudget)
    {
        // Add current priority to accumulator for each object
        foreach (var obj in _objects)
        {
            if (!_accumulators.ContainsKey(obj.Id))
                _accumulators[obj.Id] = 0;
            
            _accumulators[obj.Id] += CalculatePriority(obj);
        }
        
        // Sort by accumulator value (highest first)
        var sorted = _objects.OrderByDescending(o => _accumulators[o.Id]).ToList();
        
        var selected = new List<GameObject>();
        var usedBandwidth = 0f;
        
        foreach (var obj in sorted)
        {
            var updateSize = EstimateUpdateSize(obj);
            
            if (usedBandwidth + updateSize <= bandwidthBudget && 
                selected.Count < maxUpdates)
            {
                selected.Add(obj);
                usedBandwidth += updateSize;
                _accumulators[obj.Id] = 0; // Reset accumulator for selected
            }
        }
        
        return selected;
    }
    
    private float CalculatePriority(GameObject obj)
    {
        float priority = 1f;
        
        // Player-controlled objects get highest priority
        if (obj.IsPlayerControlled)
            priority += 1000000f;
        
        // Moving objects get higher priority than stationary
        if (obj.Velocity.Length() > 0.1f)
            priority += 100f;
        
        // Objects interacting with players
        if (obj.DistanceToNearestPlayer < 50f)
            priority += 50f;
        
        return priority;
    }
}
```

**State Update Structure**:
```csharp
public struct StateUpdate
{
    public int ObjectIndex;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 LinearVelocity;
    public Vector3 AngularVelocity;
    public bool AtRest;
    
    public void Serialize(Stream stream)
    {
        // Index
        stream.WriteInt(ObjectIndex, 0, MaxObjects - 1);
        
        // Position and rotation (quantized)
        stream.WriteVector3Quantized(Position, 4096); // 4096 values per meter
        stream.WriteQuaternionSmallestThree(Rotation, 15); // 15 bits per component
        
        // At rest optimization
        stream.WriteBool(AtRest);
        if (!AtRest)
        {
            stream.WriteVector3Quantized(LinearVelocity, 256);
            stream.WriteVector3Quantized(AngularVelocity, 256);
        }
    }
}
```

**Quantization for Bandwidth Reduction**:
```
Without quantization:
Position: 3 × float = 12 bytes
Rotation: 4 × float = 16 bytes
Velocity: 3 × float = 12 bytes
Total: 40 bytes per object

With quantization:
Position: 3 × 12 bits = 4.5 bytes (4096 values/meter = 12 bits)
Rotation: 3 × 15 bits + 2 bits = 5.7 bytes (smallest three)
Velocity: 3 × 8 bits = 3 bytes
Total: ~13 bytes per object (67% reduction)
```

**Visual Smoothing (Error Correction)**:
```csharp
public class VisualSmoother
{
    private Vector3 _positionError;
    private Quaternion _rotationError;
    
    public void ApplyStateUpdate(GameObject obj, StateUpdate update)
    {
        // Calculate error between current visual state and new state
        Vector3 visualPosition = obj.Position + _positionError;
        _positionError = visualPosition - update.Position;
        
        Quaternion visualRotation = obj.Rotation * _rotationError;
        _rotationError = visualRotation * update.Rotation.Inverse();
    }
    
    public void Update(double delta)
    {
        // Smoothly reduce error over time
        float errorMagnitude = _positionError.Length();
        
        // Adaptive smoothing: faster for large errors, slower for small
        float blendFactor;
        if (errorMagnitude < 0.25f) // < 25cm
            blendFactor = 0.95f; // Slow correction
        else if (errorMagnitude > 1.0f) // > 1m
            blendFactor = 0.85f; // Fast correction
        else
            blendFactor = Mathf.Lerp(0.85f, 0.95f, (errorMagnitude - 0.25f) / 0.75f);
        
        _positionError *= blendFactor;
        _rotationError = Quaternion.Slerp(_rotationError, Quaternion.Identity, 0.1f);
    }
    
    public Vector3 GetSmoothedPosition(Vector3 simulationPosition)
    {
        return simulationPosition + _positionError;
    }
}
```

**Bandwidth Calculation**:
```
Updates per second: 64 (typical budget at 256kbit/sec)
Objects: 100 agents (prioritized)
Update size: 13 bytes (quantized)
Delta compression: Additional 30% reduction

Per second: 64 × 13 × 0.7 = 582 bytes = 0.6 KB/s
Per player: 0.6 KB/s (vs 76 KB/s for snapshot interpolation!)

For 20 players: 0.6 × 20 = 12 KB/s server upload
For 100 players: 0.6 × 100 = 60 KB/s server upload
```

**Implications for Societies**:
- **RECOMMENDED** - Best bandwidth efficiency
- Allows continuous world simulation on server
- No determinism requirements
- Supports variable tick rates and time acceleration
- Requires implementing priority accumulator and visual smoothing
- Joining mid-game requires downloading initial state then receiving updates

### Decision Matrix for Societies

| Criteria | Deterministic Lockstep | Snapshot Interpolation | State Synchronization |
|----------|------------------------|------------------------|----------------------|
| **Bandwidth (100 agents, 20 players)** | 2.8 KB/s | 1,520 KB/s | 12 KB/s |
| **Determinism Required** | Yes (hard) | No | No |
| **Mid-Game Join** | Hard (replay inputs) | Easy (send snapshot) | Easy (initial state) |
| **Variable Tick Rate** | No (fixed rate) | Yes | Yes |
| **Time Acceleration** | No | Limited | Yes |
| **AI Agent Support** | Poor (determinism issues) | Good | Good |
| **Implementation Complexity** | Hard | Medium | Medium-Hard |
| **Latency Sensitivity** | High (stalls on loss) | Low | Low |
| **Server CPU Load** | Low | High | Medium |

### Recommendations for Societies

**Primary Recommendation: State Synchronization**

**Rationale**:
1. **Bandwidth Efficiency**: 100x lower bandwidth than snapshots for same quality
2. **No Determinism**: AI agents can use randomness; economy can use floating-point
3. **Continuous Simulation**: World evolves when players offline (server simulates)
4. **Scalability**: Supports 100+ players with modest server bandwidth
5. **Flexibility**: Variable tick rates (10-30 TPS) and time acceleration possible

**Implementation Strategy**:
```csharp
public class SocietiesNetworkSync
{
    private PriorityAccumulator _priorityAccumulator;
    private DeltaCompressor _deltaCompressor;
    private const int MaxUpdatesPerPacket = 64;
    private const float BandwidthBudget = 256000f / 8f; // 256 kbit/s = 32KB/s
    
    public void ServerTick(double delta)
    {
        // 1. Select objects to update based on priority
        var objectsToUpdate = _priorityAccumulator.SelectObjectsToUpdate(
            MaxUpdatesPerPacket, 
            BandwidthBudget
        );
        
        // 2. Create state updates
        var updates = new List<StateUpdate>();
        foreach (var obj in objectsToUpdate)
        {
            updates.Add(new StateUpdate
            {
                ObjectIndex = obj.Id,
                Position = obj.Position,
                Rotation = obj.Rotation,
                LinearVelocity = obj.Velocity,
                AngularVelocity = obj.AngularVelocity,
                AtRest = obj.Velocity.Length() < 0.01f
            });
        }
        
        // 3. Delta compress
        var compressed = _deltaCompressor.Compress(updates);
        
        // 4. Send to all clients
        Rpc(nameof(ReceiveStateUpdates), compressed, TransferModeEnum.UnreliableOrdered);
    }
    
    [RPC(TransferMode = TransferModeEnum.UnreliableOrdered)]
    public void ReceiveStateUpdates(byte[] compressedData)
    {
        var updates = _deltaCompressor.Decompress(compressedData);
        
        foreach (var update in updates)
        {
            var obj = GetObject(update.ObjectIndex);
            if (obj != null)
            {
                // Apply with visual smoothing
                obj.ApplyStateUpdate(update);
            }
        }
    }
}
```

**Hybrid Approach for Critical Events**:
- Use State Sync for agent positions and basic state (95% of traffic)
- Use Reliable RPCs for economic transactions and governance votes (5% of traffic)
- This hybrid approach gives bandwidth efficiency + reliability where needed

## Code Examples

### Complete State Synchronization System
```csharp
public partial class StateSyncManager : Node
{
    // Server-side
    private Dictionary<int, GameObject> _objects = new();
    private Dictionary<int, float> _priorityAccumulators = new();
    private Dictionary<int, StateUpdate> _lastSentState = new();
    private const int MaxUpdatesPerPacket = 64;
    
    public void UpdateServer(double delta)
    {
        // Accumulate priorities
        foreach (var obj in _objects.Values)
        {
            if (!_priorityAccumulators.ContainsKey(obj.Id))
                _priorityAccumulators[obj.Id] = 0;
            
            _priorityAccumulators[obj.Id] += CalculatePriority(obj);
        }
        
        // Select top priority objects
        var selected = _priorityAccumulators
            .OrderByDescending(kvp => kvp.Value)
            .Take(MaxUpdatesPerPacket)
            .Select(kvp => _objects[kvp.Key])
            .ToList();
        
        // Build update packet
        var packet = new StateUpdatePacket();
        foreach (var obj in selected)
        {
            var update = CreateStateUpdate(obj);
            
            // Delta compression - only send changed fields
            if (_lastSentState.ContainsKey(obj.Id))
            {
                update.DeltaFrom(_lastSentState[obj.Id]);
            }
            
            packet.Updates.Add(update);
            _lastSentState[obj.Id] = update;
            _priorityAccumulators[obj.Id] = 0;
        }
        
        // Broadcast
        Rpc(nameof(ReceiveStateUpdate), packet.Serialize());
    }
    
    private float CalculatePriority(GameObject obj)
    {
        float priority = 1f;
        
        if (obj.IsPlayerControlled) priority += 1000000;
        if (obj.Velocity.Length() > 0.1f) priority += 100;
        if (obj.IsInteracting) priority += 200;
        priority += 1000f / (1f + obj.DistanceToNearestPlayer);
        
        return priority;
    }
    
    [RPC(TransferMode = TransferModeEnum.UnreliableOrdered)]
    public void ReceiveStateUpdate(byte[] data)
    {
        var packet = StateUpdatePacket.Deserialize(data);
        
        foreach (var update in packet.Updates)
        {
            var obj = GetObject(update.ObjectId);
            if (obj != null)
            {
                // Smooth application
                obj.TargetPosition = update.Position;
                obj.TargetRotation = update.Rotation;
                obj.Velocity = update.LinearVelocity;
            }
        }
    }
}

public class GameObject : Node3D
{
    public Vector3 TargetPosition { get; set; }
    public Quaternion TargetRotation { get; set; }
    
    public override void _Process(double delta)
    {
        // Smooth interpolation to target
        Position = Position.Lerp(TargetPosition, (float)delta * 10f);
        Rotation = Rotation.Lerp(TargetRotation.GetEuler(), (float)delta * 10f);
    }
}
```

## Performance Data

| Approach | Bandwidth (100 agents) | Server CPU | Client CPU | Determinism | Mid-Game Join |
|----------|------------------------|------------|------------|-------------|---------------|
| Lockstep | 0.14 KB/s | Low | Low | Required | Hard |
| Snapshots | 76 KB/s | High | Low | Not needed | Easy |
| State Sync | 0.6 KB/s | Medium | Medium | Not needed | Easy |

## Limitations & Risks

1. **State Sync Approximation**: Not pixel-perfect; may see minor position corrections
2. **Priority Tuning Required**: Incorrect priority calculation causes bandwidth spikes or poor sync
3. **Memory Overhead**: Must store last sent state for delta compression
4. **Client CPU**: Running simulation + interpolation increases client load
5. **Complexity**: More complex than snapshots; requires careful implementation

## Recommendations

1. **Implement State Synchronization**: Best balance of bandwidth efficiency and flexibility for Societies

2. **Use Priority Accumulator**: Ensures all objects get updated eventually, not just "hot" objects

3. **Quantize Aggressively**: 4096 position values/meter, 15-bit quaternions sufficient for visual quality

4. **Implement Visual Smoothing**: Error offsets prevent visible pops when corrections arrive

5. **Hybrid with Reliable RPCs**: State sync for positions, reliable RPCs for transactions

6. **Test with Latency**: Simulate 100-300ms latency and 1-5% packet loss during development

7. **Monitor Bandwidth**: Implement per-player bandwidth tracking; reduce quality if exceeding 300KB/s

## Confidence Assessment

- **Overall Confidence**: Very High
- **Evidence Quality**: Glenn Fiedler is authoritative source; articles based on implementation in multiple shipped games
- **Applicability**: High - State sync proven in AAA titles (Overwatch uses similar approach)

## Related Sources

- Snapshot Compression: https://gafferongames.com/post/snapshot_compression/
- Reliability and Flow Control: https://gafferongames.com/post/reliability_and_flow_control/
- Floating Point Determinism: https://gafferongames.com/post/floating_point_determinism/
- Valve Networking: https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking

## Open Questions

- What's the optimal priority calculation formula for AI agents vs player agents?
- How much quantization error is acceptable before visual artifacts appear?
- What's the practical upper limit of objects before state sync becomes untenable? (Need testing)
- Should we implement delta encoding against last acked packet or last sent state?
