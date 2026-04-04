# Day 1: Technology & Testing

> **Navigation**: [â† Previous: Performance & Scalability](04-performance-scalability.md) | [Index]([AGENTS-READ-FIRST]-index.md) | [Next: Risk Management](06-risk-management.md)
> 
> **Part of**: [Day 1 Technical Architecture]([AGENTS-READ-FIRST]-index.md)

---

## 9. Technology Stack Decision

### Confirmed Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Game Engine** | Godot 4.x + C# | Free, lightweight, excellent multiplayer support |
| **Networking** | ENet (Godot native) | UDP-based, low latency, built-in RPC |
| **Server OS** | Linux (Ubuntu) | Stable, headless Godot support |
| **Database** | PostgreSQL | Complex relational data, JSON support |
| **Dev Database** | SQLite | Local testing, single-player mode |
| **Version Control** | Git + GitHub | Collaboration, documentation hosting |
| **CI/CD** | GitHub Actions | Automated builds, testing |
| **Unit Testing** | xUnit | Industry standard .NET testing framework |
| **Integration Testing** | xUnit + Testcontainers | Database and service integration tests |
| **Mocking** | Moq or NSubstitute | Interface-based unit testing |
| **Godot Testing** | Godot.XUnit | Headless Godot scene testing |

### Technology Validation Report

**Godot 4.x + C#** [r1-godot-multiplayer-research.md, r1-godot-headless-research.md]:
- âœ… **Production-ready**: MultiplayerAPI fully functional in Godot 4.x
- âœ… **ENet integration**: Native, no external dependencies
- âœ… **Headless mode**: Stable and performant (40-60% CPU reduction)
- âœ… **C# performance**: 2-5x faster than GDScript for compute-heavy operations
- **Evidence**: Multiple indie multiplayer titles successfully using Godot 4.x

**ENet Networking** [r1-enet-protocol-research.md]:
- âœ… **Bandwidth validated**: 112 KB/s per player achievable
- âœ… **Channel separation**: 255 channels, critical for reliable vs unreliable traffic
- âœ… **Latency**: <1ms localhost, 2-10ms LAN, 20-150ms internet
- âœ… **Packet loss tolerance**: Up to 5% with automatic retransmission
- **Evidence**: Originally developed for FPS game Cube; proven in production

**PostgreSQL + JSONB** [r1-postgresql-jsonb-research.md]:
- âœ… **GIN index queries**: 0.5-0.8ms (vs 300ms+ without index)
- âœ… **Read penalty**: Only 10-20% vs normalized tables (acceptable)
- âœ… **Schema flexibility**: Add fields without ALTER TABLE
- âœ… **Scale**: Proven in production at Netflix, Instagram scale
- **Evidence**: ScaleGrid benchmarks, AWS documentation

**State Synchronization** [r1-network-sync-research.md]:
- âœ… **Bandwidth**: 0.6 KB/s per player (vs 76 KB/s snapshot interpolation)
- âœ… **No determinism required**: AI randomness and floating-point economy don't break sync
- âœ… **Mid-game join**: Easy with initial state + delta
- âœ… **Time acceleration**: Supported (unlike lockstep)
- **Evidence**: Glenn Fiedler's authoritative analysis, AAA game implementations

**C# vs GDScript** [r1-godot-headless-research.md]:
| Operation | GDScript | C# | Speedup |
|-----------|----------|-----|---------|
| Simple loop (1M) | 50ms | 2ms | 25x |
| Vector3 math (100k) | 30ms | 8ms | 3.7x |
| Pathfinding (A*) | 100ms | 25ms | 4x |
| JSON parsing | 20ms | 5ms | 4x |

### Alternative Analysis

- âŒ **Cost**: Prohibitive licensing for multiplayer servers
- âŒ **Overhead**: Heavier than Godot for our use case
- âœ… **Ecosystem**: Larger asset store, more tutorials
- **Verdict**: Rejected due to UNET deprecation and cost

**Unreal Engine**:
- âŒ **Overkill**: Too heavy for our low-poly, simulation-focused game
- âŒ **Learning curve**: Steeper than Godot for small team
- âŒ **C++ overhead**: Blueprints not suitable for complex simulation
- âœ… **Graphics**: Superior visual capabilities
- **Verdict**: Rejected - overkill for our requirements

**Custom Engine**:
- âŒ **Time prohibitive**: 2-3 years to build multiplayer infrastructure
- âŒ **Maintenance burden**: Custom netcode requires ongoing support
- âœ… **Control**: Full control over architecture
- **Verdict**: Rejected - time better spent on gameplay

**Decision**: Godot 4.x + C# is optimal balance of capability, performance, and development velocity.

### Godot 4.x Multiplayer Features

**@rpc Annotation System** [r1-godot-multiplayer-research.md]:
```csharp
// Server-authoritative (only server can call)
[RPC(TransferMode = TransferModeEnum.Reliable, CallLocal = false)]
public void ServerOnlyFunction(int data) {
    if (!IsMultiplayerAuthority()) return;
    // Server logic
}

// Client-to-server (any peer can call)
[RPC(CallLocal = true, Authority = MultiplayerAPI.RPCMode.AnyPeer)]
public void ClientRequest(string action) {
    // Server validates and processes
}

// Unreliable position updates
[RPC(TransferMode = TransferModeEnum.UnreliableOrdered)]
public void UpdatePosition(Vector3 pos, Vector3 vel) {
    // Client interpolation
}
```

**MultiplayerSynchronizer**:
- Automatic state replication for node properties
- Delta compression (only changed values sent)
- Configurable sync intervals (not every frame)
- Built-in interpolation for smooth updates
- "Always sync" or "on change" modes [r1-godot-multiplayer-research.md]

**Scene Replication**:
- `MultiplayerSpawner` for dynamic entities
- Automatic spawn/despawn sync across clients
- Ownership transfer support
- Configurable spawn paths and scenes [r1-godot-multiplayer-research.md]

**ENet Integration**:
- Built-in, no external dependency
- Reliable and unreliable channels
- 255 channels per connection
- Automatic congestion control
- Latency measurement built-in [r1-enet-protocol-research.md]

### Version Requirements

**Godot 4.x Required** [r1-godot-multiplayer-research.md]:
- Godot 4.0-4.2 had multiplayer bugs and API instability
- Godot 4.x has stable MultiplayerAPI
- C# integration improved in 4.x
- Headless mode fully functional

**.NET 8.0**:
- Latest LTS version
- Improved performance over .NET 6
- Native AOT compilation support (optional)
- C# 12 features available

**PostgreSQL 15+** [r1-postgresql-jsonb-research.md]:
- JSONB improvements in recent versions
- Better GIN index performance
- Connection pooling optimizations
- Required for GIN `jsonb_path_ops` operator class

**ENet**:
- Included with Godot 4.x (no separate install)
- Godot wraps ENet in `ENetMultiplayerPeer`

---

## 10. Testing Architecture

### Testing Philosophy

Our testing strategy follows these principles, validated by research [r1-research-summary.md, Decision 6]:

- **Test everything reasonably testable**: Focus on business logic, database operations, and critical paths [r1-research-summary.md]
- **CI/CD integration from day one**: Automated testing on every commit [r1-research-summary.md]
- **Dual database testing**: Both PostgreSQL and SQLite tested in CI pipeline [r1-research-summary.md, Decision 3]
- **Network testing planned**: Deferred to later prototypes but architected for testability [r1-godot-headless-research.md]

**Validated Approach**: Comprehensive testing from day one prevents technical debt accumulation and enables confident refactoring [r1-research-summary.md]

### Testing Technology Stack

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Unit Testing** | xUnit | Industry standard for .NET, excellent Godot C# support |
| **Integration Testing** | xUnit + Testcontainers | Production database parity in tests |
| **Mocking** | Moq or NSubstitute | Interface-based testing for network/database layers |
| **Godot Testing** | Godot.XUnit | Headless scene testing (future implementation) |
| **Test Runner** | `dotnet test` | Standard .NET CLI integration |
| **CI/CD** | GitHub Actions | Automated test execution on PR/push |

### Test Project Structure

```
tests/
â”œâ”€â”€ Societies.Core.Tests/          # Unit tests for pure C# logic
â”‚   â”œâ”€â”€ Entities/
â”‚   â”‚   â””â”€â”€ EntityTests.cs
â”‚   â”œâ”€â”€ Economy/
â”‚   â”‚   â””â”€â”€ MarketTests.cs
â”‚   â”œâ”€â”€ Database/
â”‚   â”‚   â””â”€â”€ RepositoryTests.cs
â”‚   â””â”€â”€ Governance/
â”‚       â””â”€â”€ LawTests.cs
â”œâ”€â”€ Societies.Integration.Tests/   # Integration tests
â”‚   â”œâ”€â”€ PostgreSQL/
â”‚   â”‚   â””â”€â”€ PostgreSQLTests.cs
â”‚   â”œâ”€â”€ SQLite/
â”‚   â”‚   â””â”€â”€ SQLiteTests.cs
â”‚   â””â”€â”€ SaveLoad/
â”‚       â””â”€â”€ SaveSystemTests.cs
â””â”€â”€ Societies.Godot.Tests/         # Godot-specific tests (future)
    â””â”€â”€ SceneTests.cs
```

### Code Organization for Testability

**Architecture Pattern**: Separate business logic from Godot dependencies

```csharp
// Testable: Pure C# business logic
public class EconomyCalculator {
    public decimal CalculateTax(decimal income, TaxBracket bracket) {
        // Pure logic, easily tested
    }
}

// Godot wrapper: Thin layer, minimal logic
public partial class EconomyManager : Node {
    private EconomyCalculator _calculator = new();
    
    public void ApplyTaxes() {
        // Call calculator, handle Godot-specific stuff
    }
}
```

**Benefits**:
- 80%+ of code in testable libraries
- Godot scripts reduced to coordination glue
- Fast unit tests (<100ms each)
- Godot tests only for UI/scene validation

### Database Testing Strategy

**PostgreSQL Testing**:
- Use Testcontainers for production database parity
- Spin up PostgreSQL in Docker for each test run
- Test schema migrations, transactions, concurrency
- Validate JSONB operations and spatial queries

**SQLite Testing**:
- In-memory database for fast unit tests
- File-based SQLite for integration tests
- Verify single-player mode operations
- Test export/import between SQLite and PostgreSQL

**Example Database Test**:
```csharp
[Fact]
public async Task SaveWorldState_PersistsToDatabase() {
    // Arrange
    var world = new World { Name = "Test World" };
    var repository = new WorldRepository(_dbContext);
    
    // Act
    await repository.SaveAsync(world);
    var retrieved = await repository.GetByIdAsync(world.Id);
    
    // Assert
    Assert.NotNull(retrieved);
    Assert.Equal("Test World", retrieved.Name);
}
```

### Godot Testing Strategy

**Headless Testing**:
```bash
# Run Godot in headless mode for CI
godot --headless --script tests/run_tests.cs
```

**Testing Approaches**:

1. **Pure C# Tests** (Primary):
   - Test all business logic outside Godot
   - Fast, reliable, no scene loading overhead
   - 90% of test coverage here

2. **Godot.XUnit Tests** (Secondary):
   - Test Node lifecycle and scene interactions
   - Validate RPC networking in controlled environment
   - UI component testing

3. **Integration Tests** (Full Stack):
   - End-to-end scenarios with real Godot instances
   - Localhost multiplayer testing
   - Save/load roundtrip validation

**Godot-Specific Test Example**:
```csharp
public class EntityNodeTests {
    [Fact]
    public void EntityNode_SynchronizesPosition() {
        // Arrange
        var entityNode = new EntityNode();
        var testPosition = new Vector3(10, 0, 20);
        
        // Act
        entityNode.Position = testPosition;
        
        // Assert
        Assert.Equal(testPosition, entityNode.Position);
    }
}
```

**Limitations & Workarounds**:
- Scene tree requires Godot runtime â†’ Use headless mode
- Visual/UI testing difficult â†’ Focus on state/logic testing
- Multiplayer requires network â†’ Mock `INetworkManager` for unit tests

### Network Testing Strategy

**Interface-Based Design**:
```csharp
public interface INetworkManager {
    event Action<PlayerId> PlayerConnected;
    event Action<PlayerId> PlayerDisconnected;
    Task SendRpc(PlayerId target, string method, params object[] args);
}

// Real implementation for production
public class ENetNetworkManager : Node, INetworkManager { ... }

// Mock implementation for testing
public class MockNetworkManager : INetworkManager { ... }
```

**Testing Layers**:

1. **Unit Tests** (Mock-based):
   - Test logic that depends on networking
   - Verify RPC calls are made correctly
   - Fast, no actual network required

2. **Integration Tests** (Loopback):
   - Test real ENet connections on 127.0.0.1
   - Validate serialization/deserialization
   - Measure latency under controlled conditions

3. **Load Tests** (Future):
   - Docker-based multi-client simulation
   - Test with 20+ concurrent connections
   - Stress test with packet loss/latency injection

**Deferred Network Testing**:
- Full multiplayer stress tests: Month 3+ (Prototype 2 validation)
- Latency simulation: Month 4+ (when core systems stable)
- Production-like testing: Alpha phase (Month 6)

### CI/CD Integration (GitHub Actions)

**Workflow Overview**:
- **Trigger**: Every PR and push to `main`
- **Matrix**: Windows + Linux (macOS optional)
- **Databases**: PostgreSQL service container + SQLite
- **Steps**: Build â†’ Unit Tests â†’ Integration Tests â†’ Coverage Report

**Sample Workflow** (see `.github/workflows/tests.yml` for full implementation):

```yaml
name: Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest]
    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: test
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '6.0'
      - run: dotnet test --configuration Release
```

**Pipeline Gates**:
- All unit tests must pass (zero tolerance)
- Integration tests must pass
- Code coverage minimum: 70% (initial), 80% (Alpha)
- Build must succeed on both platforms

### Testing Timeline by Prototype

| Prototype | Testing Focus | Coverage Target |
|-----------|--------------|-----------------|
| **Proto 1** (World) | Entity system, database ops, save/load | 50% |
| **Proto 2** (AI) | AI behavior, economy calculations | 60% |
| **Proto 3** (Governance) | Law system, voting logic | 70% |
| **Proto 4** (Progression) | Tech tree, skill system | 75% |
| **Proto 5** (Environment) | Pollution simulation, ecosystem | 75% |
| **Alpha** (Integration) | Full system integration, E2E | 80% |

### What Gets Tested vs. Deferred

**Tested Now**:
- Entity CRUD operations
- Economy calculations (pricing, taxes)
- Governance rules and law execution
- Database operations (both PostgreSQL and SQLite)
- Save/load serialization
- Utility functions and helpers

**Deferred**:
- Full multiplayer synchronization (Month 3+)
- Complex AI behavior validation (Month 2+)
- UI/UX flows (manual testing initially)
- Performance/stress tests (Alpha phase)
- Visual regression tests (Post-launch)

### Testing Best Practices

1. **Test Behavior, Not Implementation**: Test what code does, not how it does it
2. **One Assert Per Test**: Single concept per test method
3. **Arrange-Act-Assert**: Clear structure in every test
4. **Descriptive Names**: Test names should read like documentation
5. **Fast Tests**: Unit tests should complete in <100ms
6. **Isolated Tests**: No dependencies between tests
7. **Production Data**: Use realistic test data, not just "foo" and "bar"

### Example Test Patterns

**Entity Test**:
```csharp
public class EntityTests {
    [Fact]
    public void Entity_WithPosition_IsWithinWorldBounds() {
        // Arrange
        var world = new World(width: 100, height: 100);
        var entity = new Entity(world, x: 50, y: 50);
        
        // Act & Assert
        Assert.True(world.IsWithinBounds(entity.Position));
    }
    
    [Fact]
    public void Entity_MovesToPosition_UpdatesCoordinates() {
        // Arrange
        var entity = new Entity { Position = new Vector3(0, 0, 0) };
        var newPosition = new Vector3(10, 0, 10);
        
        // Act
        entity.MoveTo(newPosition);
        
        // Assert
        Assert.Equal(newPosition, entity.Position);
    }
}
```

**Economy Test**:
```csharp
public class MarketTests {
    [Theory]
    [InlineData(100, 0.10, 10)]  // 10% tax on 100 = 10
    [InlineData(50, 0.20, 10)]   // 20% tax on 50 = 10
    [InlineData(0, 0.10, 0)]     // No tax on zero
    public void CalculateTax_ReturnsCorrectAmount(
        decimal income, decimal rate, decimal expected) {
        // Arrange
        var calculator = new TaxCalculator();
        
        // Act
        var result = calculator.Calculate(income, rate);
        
        // Assert
        Assert.Equal(expected, result);
    }
}
```

### Test Data Management

**Test Fixtures**:
- Reusable world states for different scenarios
- Pre-configured agents with specific personalities
- Mock economies with known supply/demand
- Deterministic random seeds for reproducible tests

**Factories**:
```csharp
public static class TestDataFactory {
    public static World CreateTestWorld(int width = 100, int height = 100) {
        return new World {
            Width = width,
            Height = height,
            Seed = 12345 // Deterministic
        };
    }
    
    public static Agent CreateAgentWithWealth(decimal wealth) {
        return new Agent {
            Name = "Test Agent",
            Wealth = wealth,
            Personality = TestPersonalities.Average()
        };
    }
}
```

### Coverage Goals

| Phase | Line Coverage | Branch Coverage | Test Count |
|-------|--------------|-----------------|------------|
| Week 2 (Setup) | 0% â†’ 30% | 0% â†’ 20% | 10-20 |
| Proto 1 Complete | 50% | 40% | 50-100 |
| Proto 2 Complete | 60% | 50% | 100-200 |
| Alpha | 80% | 70% | 500+ |
| Beta | 85% | 75% | 1000+ |

---

**Previous**: [â† Performance & Scalability](04-performance-scalability.md) | **Next**: [Risk Management â†’](06-risk-management.md)
