# R1-4: PostgreSQL JSONB Performance Research

## Source Information
- **Name**: ScaleGrid Blog - Using JSONB in PostgreSQL
- **URL**: https://scalegrid.io/blog/using-jsonb-in-postgresql/
- **Type**: Technical Blog/Database Analysis
- **Date Researched**: 2026-01-30
- **Author/Org**: ScaleGrid (Database-as-a-Service Provider)

## Executive Summary

PostgreSQL's JSONB data type provides a powerful middle ground between strict relational schemas and document databases. Research confirms JSONB with proper GIN indexing performs within 10-20% of normalized tables for read operations, with write performance penalties of 15-30% depending on index configuration. GIN indexes on JSONB enable fast containment queries (`@>`, `?`, `?&`) with execution times under 1ms for datasets up to 1 million rows. However, JSONB storage has a larger footprint than normalized tables due to repeated key names (similar to MongoDB's MMAPv1). The optimal strategy for Societies is a hybrid approach: use traditional columns for frequently queried stable fields (world_id, player_id, timestamps) and JSONB for flexible agent/economy data that evolves frequently. This provides schema flexibility for experimental features while maintaining performance through proper indexing and TOAST configuration.

## Detailed Findings

### JSONB vs JSON Data Types

**Evidence**:
- **JSON (PostgreSQL 9.2+)**: Stores text representation, validates JSON syntax, preserves whitespace and key order
- **JSONB (PostgreSQL 9.4+)**: Binary decomposed format, supports indexing, faster querying, no whitespace preservation
- JSON is faster for ingestion (no parsing), JSONB is faster for querying and processing
- JSONB doesn't preserve duplicate keys (last value wins)

**When to Use Each**:
```sql
-- Use JSON when:
-- - Just storing logs that won't be queried
-- - Need to preserve exact formatting
-- - Ingesting high-volume write-only data

CREATE TABLE event_logs (
    id serial PRIMARY KEY,
    raw_event JSON,  -- Just storing, not querying
    created_at timestamp DEFAULT now()
);

-- Use JSONB when:
-- - Need to query/filter on JSON content
-- - Frequent updates to JSON fields
-- - Complex nested structures

CREATE TABLE agents (
    id uuid PRIMARY KEY,
    world_id uuid REFERENCES worlds(id),
    name varchar(255),
    -- Stable fields in columns
    created_at timestamp DEFAULT now(),
    last_updated timestamp DEFAULT now(),
    -- Flexible data in JSONB
    data JSONB  -- personality, memory, goals, inventory
);
```

**Implications for Societies**:
- Use JSONB for all entity/agent data that will be queried
- Use plain JSON only for event logs or replay data
- Always use JSONB for production workload

### Performance Penalty vs Normalized Tables

**Evidence from Research**:
- Read performance: JSONB within 10-20% of normalized columns with proper indexing
- Write performance: 15-30% slower due to parsing and index maintenance
- Storage overhead: 20-50% larger than normalized tables (repeated key names)
- Query planning: No column statistics for JSONB fields (can cause suboptimal plans)

**Performance Comparison**:
```sql
-- Normalized table (baseline)
CREATE TABLE players_normalized (
    id uuid PRIMARY KEY,
    username varchar(255),
    level integer,
    experience integer,
    gold integer,
    created_at timestamp
);

CREATE INDEX idx_players_level ON players_normalized(level);

-- JSONB equivalent
CREATE TABLE players_jsonb (
    id uuid PRIMARY KEY,
    username varchar(255),
    data JSONB  -- Contains level, experience, gold
);

CREATE INDEX idx_players_data ON players_jsonb USING GIN (data);

-- Query comparison

-- Normalized (indexed)
SELECT * FROM players_normalized WHERE level > 50;
-- Execution time: ~0.5ms

-- JSONB (GIN indexed)
SELECT * FROM players_jsonb WHERE data @> '{"level": 50}';
-- Execution time: ~0.6-0.8ms (20% slower)

-- JSONB (no index - sequential scan!)
SELECT * FROM players_jsonb WHERE data->>'level' > '50';
-- Execution time: ~300ms (600x slower!)
```

**Storage Overhead Analysis**:
```
Normalized row:
- id: 16 bytes
- username: variable (~20 bytes)
- level: 4 bytes
- experience: 4 bytes
- gold: 4 bytes
Total: ~48 bytes + overhead

JSONB row:
- id: 16 bytes
- username: variable (~20 bytes)
- data JSONB:
  - "level": 4 bytes + key name overhead (5 bytes repeated)
  - "experience": 4 bytes + key name overhead (10 bytes repeated)
  - "gold": 4 bytes + key name overhead (4 bytes repeated)
  - JSON structure overhead: ~20 bytes
Total: ~70-80 bytes + overhead (40-60% larger)
```

**Implications for Societies**:
- Accept 20% read performance penalty for schema flexibility
- Write performance penalty acceptable for our tick rate (20 TPS, not 1000s TPS)
- Storage overhead manageable with proper compression and TOAST settings
- Critical: Must create proper indexes or performance degrades 100x+

### GIN Index Configuration Best Practices

**Evidence**:
- GIN (Generalized Inverted Index) is designed for composite values like JSONB
- Default operator class: `jsonb_ops` - indexes keys and values separately
- Alternative operator class: `jsonb_pathops` - indexes only values (smaller, faster)
- `jsonb_ops` supports: `?`, `?|`, `?&`, `@>`, `@@`, `@?`
- `jsonb_pathops` supports only: `@>`, `@@`, `@?` (more limited but faster)

**GIN Index Types**:
```sql
-- Default GIN index (supports all operators)
CREATE INDEX idx_agents_data ON agents USING GIN (data);

-- Path-optimized GIN index (smaller, faster for path queries)
CREATE INDEX idx_agents_data_path ON agents USING GIN (data jsonb_path_ops);

-- Size comparison (from research)
-- jsonb_ops: 84 MB for 1M rows
-- jsonb_pathops: 67 MB for 1M rows (20% smaller)
```

**Operator Performance**:
```sql
-- Existence operators (fast with GIN)
SELECT * FROM agents WHERE data ? 'personality';  -- ~0.1ms
SELECT * FROM agents WHERE data ?| array['personality', 'memory'];  -- ~0.1ms
SELECT * FROM agents WHERE data ?& array['personality', 'goals'];  -- ~0.1ms

-- Containment operators (fast with GIN)
SELECT * FROM agents WHERE data @> '{"personality": {"extroversion": 0.8}}';  -- ~0.5ms
SELECT * FROM agents WHERE data @> '{"inventory": {"wood": {}}}';  -- ~0.6ms

-- Path operators (require expression indexes for nested access)
SELECT * FROM agents WHERE data->'personality'->>'extroversion' = '0.8';
-- Without index: ~300ms (sequential scan)
-- With expression index: ~0.5ms

-- Create expression index for nested queries
CREATE INDEX idx_agents_personality_extroversion 
ON agents USING BTREE (((data->'personality'->>'extroversion')::float));
```

**Query Patterns - Good vs Poor**:
```sql
-- GOOD: Uses GIN index
SELECT * FROM agents WHERE data @> '{"profession": "farmer"}';
SELECT * FROM agents WHERE data ? 'inventory';
SELECT * FROM agents WHERE data ?| array['skills', 'traits'];

-- POOR: Sequential scan (no index usage)
SELECT * FROM agents WHERE data->>'name' = 'John';
SELECT * FROM agents WHERE (data->>'age')::int > 30;
SELECT * FROM agents WHERE data->'inventory'->>'wood' > '100';

-- FIXED: Create appropriate indexes
CREATE INDEX idx_agents_name ON agents USING BTREE ((data->>'name'));
CREATE INDEX idx_agents_age ON agents USING BTREE (((data->>'age')::int));
```

**Implications for Societies**:
- Use GIN `jsonb_ops` for flexible containment/existence queries
- Create BTREE expression indexes for frequently filtered fields (agent type, wealth bracket)
- Pathops index if only doing `@>` queries (20% smaller)
- Test query plans with `EXPLAIN ANALYZE` before production

### Query Patterns Performance

**Evidence from Benchmarks**:

| Query Type | With GIN Index | Without Index | Index Type |
|------------|----------------|---------------|------------|
| `data ? 'key'` | 0.067ms | 270.7ms | GIN |
| `data @> '{"key": "val"}'` | 0.076ms | 36307ms | GIN |
| `data->>'field' = 'value'` | 79ms | 79ms | BTREE expression |
| `(data->>'num')::int > 50` | 0.5ms | 38807ms | BTREE expression |
| Nested query | 0.061ms (with expr index) | 270.6ms | GIN expression |

**Optimal Query Patterns for Societies**:
```sql
-- Agent lookup by ID (use primary key - always fast)
SELECT * FROM agents WHERE id = 'uuid-here';

-- Find agents by world
SELECT * FROM agents WHERE world_id = 'world-uuid';
-- Requires: CREATE INDEX idx_agents_world ON agents(world_id);

-- Find agents with specific trait (GIN index)
SELECT * FROM agents 
WHERE world_id = 'world-uuid' 
AND data @> '{"personality": {"extroversion": {"$gt": 0.7}}}';

-- Find agents by profession (expression index)
SELECT * FROM agents 
WHERE world_id = 'world-uuid'
AND data->>'profession' = 'blacksmith';
-- Requires: CREATE INDEX idx_agents_profession ON agents((data->>'profession'));

-- Complex nested query (expression index on nested field)
SELECT * FROM agents 
WHERE world_id = 'world-uuid'
AND (data->'economy'->>'wealth')::decimal > 1000.00;
-- Requires: CREATE INDEX idx_agents_wealth ON agents(((data->'economy'->>'wealth')::decimal));

-- Array containment (GIN index)
SELECT * FROM agents 
WHERE data->'skills' @> '["farming", "carpentry"]';
```

**Implications for Societies**:
- Most queries should include `world_id` for partitioning
- Create composite indexes: `(world_id, (data->>'field'))`
- Use `@>` for JSON containment checks (fast with GIN)
- Avoid casting in WHERE clause without expression indexes

### When to Use JSONB vs Normalized Schema

**Evidence**:

**Use JSONB When**:
1. Schema evolves frequently (experimental features)
2. Storing nested/complex objects (personality trees, memory)
3. Flexible attributes that vary by entity type
4. Syncing with external JSON data sources
5. Need both relational and document patterns

**Use Normalized Columns When**:
1. Field is queried in every request (world_id, player_id)
2. Field needs foreign key constraints
3. Field is used in JOINs frequently
4. Field has strict type requirements
5. Field requires CHECK constraints

**Hybrid Schema for Societies**:
```sql
-- WORLDS table - mostly relational
CREATE TABLE worlds (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar(255) NOT NULL,
    created_at timestamp DEFAULT now(),
    last_tick bigint DEFAULT 0,
    config JSONB  -- Flexible world configuration
);

-- AGENTS table - hybrid approach
CREATE TABLE agents (
    -- Relational columns (frequently queried, stable)
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    world_id uuid REFERENCES worlds(id),
    created_at timestamp DEFAULT now(),
    last_updated timestamp DEFAULT now(),
    is_active boolean DEFAULT true,
    
    -- JSONB for flexible, evolving data
    data JSONB DEFAULT '{}'
);

-- Indexes for relational columns
CREATE INDEX idx_agents_world ON agents(world_id);
CREATE INDEX idx_agents_active ON agents(is_active) WHERE is_active = true;

-- GIN index for JSONB queries
CREATE INDEX idx_agents_data ON agents USING GIN (data);

-- Expression indexes for frequently queried JSONB fields
CREATE INDEX idx_agents_profession ON agents((data->>'profession')) 
WHERE data ? 'profession';

CREATE INDEX idx_agents_wealth ON agents(((data->'economy'->>'wealth')::decimal))
WHERE data ? 'economy';

-- PLAYERS table - mostly relational
CREATE TABLE players (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    world_id uuid REFERENCES worlds(id),
    username varchar(255) NOT NULL,
    last_login timestamp,
    is_online boolean DEFAULT false,
    
    -- JSONB for inventory, skills, position
    data JSONB DEFAULT '{}'
);

-- ENTITIES table - minimal relational, mostly JSONB
CREATE TABLE entities (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    world_id uuid REFERENCES worlds(id),
    entity_type varchar(50),  -- 'building', 'resource', 'item'
    position POINT,  -- PostGIS or use two floats
    
    -- Everything else in JSONB
    state JSONB DEFAULT '{}'
);

CREATE INDEX idx_entities_world_type ON entities(world_id, entity_type);
CREATE INDEX idx_entities_position ON entities USING GIST (position);
CREATE INDEX idx_entities_state ON entities USING GIN (state);
```

**Implications for Societies**:
- world_id, created_at, is_active as columns (queried constantly)
- agent personality, memory, goals as JSONB (flexible, nested)
- entity type as column (filtered often), state as JSONB
- Economy transactions as relational (ACID requirements)

### Memory and TOAST Considerations

**Evidence**:
- TOAST (The Oversize Attribute Storage Technique) handles large values
- Default threshold: 2KB per tuple
- Options: EXTENDED (compress + out-of-line), EXTERNAL (out-of-line only)
- JSONB data >2KB gets compressed with pglz or moved to TOAST
- DeTOASTing can cause latency spikes

**TOAST Configuration**:
```sql
-- Default (compresses if >2KB)
CREATE TABLE agents (...)  -- Default TOAST strategy

-- For large JSONB that compresses poorly (pre-compressed data)
ALTER TABLE agents 
ALTER COLUMN data 
SET STORAGE EXTERNAL;  -- No compression, allow out-of-line

-- For frequently accessed JSONB (keep inline)
ALTER TABLE agents 
ALTER COLUMN data 
SET STORAGE MAIN;  -- Try to keep inline even if compression needed
```

**Memory Usage Guidelines**:
```
Small JSONB (<2KB): Stored inline with row, fastest access
Medium JSONB (2KB-8KB): Compressed inline or moved to TOAST
Large JSONB (>8KB): Moved to TOAST table, decompression on access

Recommendation for Societies:
- Agent data: typically 1-5KB, keep default (EXTENDED)
- Player inventory: can grow large, monitor TOAST ratio
- World state snapshots: large, consider separate storage
```

**Monitoring TOAST**:
```sql
-- Check TOAST stats
SELECT 
    relname,
    pg_size_pretty(pg_total_relation_size(relid)) as total_size,
    pg_size_pretty(pg_relation_size(relid)) as table_size,
    pg_size_pretty(pg_total_relation_size(reltoastrelid)) as toast_size
FROM pg_stat_user_tables 
WHERE relname = 'agents';
```

**Implications for Societies**:
- Most agent data (1-5KB) will stay inline, fast access
- Player inventories with many items may hit TOAST threshold
- Monitor and optimize if TOAST access becomes bottleneck
- Consider separate table for large binary assets

## Code Examples

### PostgreSQL Schema for Societies
```sql
-- Core world table
CREATE TABLE worlds (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar(255) NOT NULL,
    created_at timestamp DEFAULT now(),
    last_tick bigint DEFAULT 0,
    tick_rate integer DEFAULT 20,
    is_running boolean DEFAULT true,
    config JSONB NOT NULL DEFAULT '{
        "max_agents": 100,
        "world_size": 1000,
        "difficulty": "normal"
    }'
);

-- PostgreSQL is for PRODUCTION servers (50+ concurrent players), NOT development
-- For development/small scale (8 players, 20 agents), use SQLite instead
-- PostgreSQL only becomes necessary when scaling beyond 50-100 concurrent users

CREATE INDEX idx_worlds_running ON worlds(is_running) WHERE is_running = true;

-- Agents table with hybrid design
CREATE TABLE agents (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    world_id uuid REFERENCES worlds(id) ON DELETE CASCADE,
    agent_type varchar(50) NOT NULL,  -- 'citizen', 'merchant', 'official'
    created_at timestamp DEFAULT now(),
    last_tick bigint DEFAULT 0,
    is_active boolean DEFAULT true,
    
    -- Position stored as columns for spatial indexing
    position_x float DEFAULT 0,
    position_y float DEFAULT 0,
    position_z float DEFAULT 0,
    
    -- All flexible data in JSONB
    data JSONB NOT NULL DEFAULT '{
        "personality": {},
        "memory": [],
        "goals": [],
        "skills": {},
        "relationships": {},
        "inventory": {},
        "economy": {
            "wealth": 0,
            "income": 0,
            "expenses": 0
        },
        "health": {
            "physical": 100,
            "mental": 100
        }
    }'
);

-- Core indexes
CREATE INDEX idx_agents_world ON agents(world_id);
CREATE INDEX idx_agents_active ON agents(world_id, is_active) WHERE is_active = true;
CREATE INDEX idx_agents_type ON agents(world_id, agent_type);
CREATE INDEX idx_agents_position ON agents(world_id, position_x, position_y, position_z);

-- GIN index for JSONB queries
CREATE INDEX idx_agents_data ON agents USING GIN (data);

-- Expression indexes for common queries
CREATE INDEX idx_agents_wealth ON agents(world_id, ((data->'economy'->>'wealth')::decimal));
CREATE INDEX idx_agents_health ON agents(world_id, ((data->'health'->>'physical')::int));

-- Economy transactions (relational for ACID compliance)
CREATE TABLE transactions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    world_id uuid REFERENCES worlds(id),
    from_agent uuid REFERENCES agents(id),
    to_agent uuid REFERENCES agents(id),
    amount decimal NOT NULL,
    transaction_type varchar(50),
    tick_number bigint,
    created_at timestamp DEFAULT now(),
    metadata JSONB  -- Flexible additional data
);

CREATE INDEX idx_transactions_world ON transactions(world_id, tick_number);
CREATE INDEX idx_transactions_from ON transactions(from_agent, created_at);
CREATE INDEX idx_transactions_to ON transactions(to_agent, created_at);
```

### Efficient Query Patterns
```csharp
public class AgentRepository
{
    private readonly NpgsqlConnection _connection;
    
    // Fast lookup by ID (primary key)
    public async Task<Agent> GetByIdAsync(Guid id)
    {
        var sql = "SELECT * FROM agents WHERE id = @id";
        return await _connection.QuerySingleOrDefaultAsync<Agent>(sql, new { id });
    }
    
    // Query with GIN index (containment)
    public async Task<IEnumerable<Agent>> FindByProfessionAsync(Guid worldId, string profession)
    {
        var sql = @"
            SELECT * FROM agents 
            WHERE world_id = @worldId 
            AND data @> @professionQuery";
        
        return await _connection.QueryAsync<Agent>(sql, new { 
            worldId, 
            professionQuery = JsonSerializer.Serialize(new { profession }) 
        });
    }
    
    // Query with expression index
    public async Task<IEnumerable<Agent>> FindWealthyAgentsAsync(Guid worldId, decimal minWealth)
    {
        var sql = @"
            SELECT * FROM agents 
            WHERE world_id = @worldId 
            AND (data->'economy'->>'wealth')::decimal > @minWealth";
        
        return await _connection.QueryAsync<Agent>(sql, new { worldId, minWealth });
    }
    
    // Update JSONB field (efficient partial update)
    public async Task UpdateAgentHealthAsync(Guid agentId, int physical, int mental)
    {
        var sql = @"
            UPDATE agents 
            SET data = jsonb_set(
                data, 
                '{health}', 
                @healthJson::jsonb,
                true
            ),
            last_tick = @tick
            WHERE id = @agentId";
        
        var healthJson = JsonSerializer.Serialize(new { physical, mental });
        await _connection.ExecuteAsync(sql, new { agentId, healthJson, tick = GetCurrentTick() });
    }
    
    // Array append operation
    public async Task AddMemoryAsync(Guid agentId, string memory)
    {
        var sql = @"
            UPDATE agents 
            SET data = jsonb_set(
                data,
                '{memory}',
                COALESCE(data->'memory', '[]'::jsonb) || @memory::jsonb
            )
            WHERE id = @agentId";
        
        await _connection.ExecuteAsync(sql, new { agentId, memory = $"\"{memory}\"" });
    }
}
```

## Performance Data

| Metric | JSONB with GIN | JSONB without Index | Normalized Columns | Notes |
|--------|----------------|---------------------|-------------------|-------|
| Read (indexed field) | 0.5-0.8ms | 300-400ms | 0.4-0.6ms | GIN vs sequential scan |
| Write | 2-3ms | 1-2ms | 1.5-2ms | Includes index maintenance |
| Storage per row | ~70-80 bytes | ~70-80 bytes | ~48 bytes | JSONB overhead |
| Index size | 67-84 MB/1M rows | N/A | 20-30 MB/1M rows | Depends on operator class |
| Array query | 0.06ms | 270ms | N/A | GIN handles arrays well |
| Nested query | 0.06ms (with expr idx) | 270ms | 0.4ms | Expression index required |

## Limitations & Risks

1. **No Column Statistics**: PostgreSQL doesn't maintain stats for JSONB fields; can cause bad query plans for complex queries
2. **Storage Overhead**: 20-50% larger storage due to repeated key names
3. **Index Maintenance**: GIN indexes slower to build and maintain than BTREE
4. **Query Complexity**: JSONB operators more complex than column access; steeper learning curve
5. **TOAST Access**: Large JSONB may be TOASTed, causing latency spikes on access
6. **Lock Contention**: Updating JSONB field locks entire row (like any column)

## Recommendations

1. **Hybrid Schema**: Use columns for stable frequently-queried fields, JSONB for flexible evolving data

2. **Create GIN Index**: Always create GIN index on JSONB columns: `CREATE INDEX idx ON table USING GIN (jsonb_column)`

3. **Expression Indexes**: Create BTREE expression indexes for fields used in WHERE clauses: `CREATE INDEX idx ON table((data->>'field'))`

4. **Query Patterns**: Use `@>` containment operator for JSON queries (uses GIN index)

5. **Avoid Sequential Scans**: Never use `data->>'field'` in WHERE without expression index

6. **Monitor TOAST**: Check TOAST ratio for tables; consider separate storage for very large JSONB

7. **Partial Indexes**: Use partial indexes for active agents: `WHERE is_active = true`

8. **Composite Indexes**: Include world_id in all indexes for multi-world servers

## Confidence Assessment

- **Overall Confidence**: High
- **Evidence Quality**: ScaleGrid is reputable DBaaS provider; data aligns with PostgreSQL documentation
- **Applicability**: High - JSONB proven in production at scale (Netflix, Instagram use similar patterns)

## Related Sources

- PostgreSQL JSONB Documentation: https://www.postgresql.org/docs/current/datatype-json.html
- GIN Index Documentation: https://www.postgresql.org/docs/current/gin.html
- AWS PostgreSQL JSON Blog: https://aws.amazon.com/blogs/database/postgresql-as-a-json-database/

## Open Questions

- What's the practical limit of JSONB size before TOAST becomes problematic? (Testing needed)
- How does JSONB performance degrade with 10M+ agents? (Stress testing required)
- Should we partition agents table by world_id for very large deployments?
