# Societies Simulation Kernel

Deterministic simulation with resources, market, crafting, contracts, law enforcement, and factions.

## Features

- Deterministic with save/load
- Resource gathering (berries, logs, ore)
- Market trading with order book + sales tax
- Crafting with workshops
- Contracts with escrow
- Land claims and law enforcement
- **Factions with governance and treasury**

### Ecology & Stability
- **Ecology Feedback**: Pollution reduces resource regeneration and berry yields. High pollution increases agent hunger drain.
- **Agent Adaptation**: Agents sense global scarcity and shift priorities (e.g., gathering food more aggressively if stocks are low).
- **Stability Metrics**: Daily snapshots record pollution, stocks, prices, and starvation. The system can detect "collapse" conditions (spiraling pollution or hyperinflation).

## Running the Simulation

### Visualizer (GUI)
Run the interactive visualizer to observe and control the simulation:

1. Open Godot 4.x and load the project
2. Open `res://viz/visualizer_main.tscn`
3. Press F5 or click "Run Current Scene"

**Features:**
- **New Run**: Enter a seed value and click "New Run" to start a fresh simulation
- **Load/Save JSON**: Load a previously saved state or save the current state
- **Time Controls**:
  - Pause/Play at 1x, 5x, or 20x speed
  - Step by single tick or full day
  - Jump to a specific day
- **Live Display**: Tick, day, checksum, agent count, average hunger

The visualizer does not modify simulation behavior - it only drives `Sim.step()` and displays state.

#### Visualizer Troubleshooting Checklist
If the visualizer quits immediately or closes without errors:

1. **Capture logs** by launching Godot from a terminal:
   ```bash
   godot --verbose --log-file=visualizer.log
   ```
2. **Reproduce** the issue by opening `res://viz/visualizer_main.tscn` and running the scene.
3. **Record environment details** (OS, GPU, driver version, and Godot version).
4. **Attach artifacts**: `visualizer.log`, steps to reproduce, and any crash dialog output.

### Headless Simulation Run

The simulation is fully decoupled from the GUI and runs headless for server-side/CI testing. All core simulation classes (`sim/`) extend `RefCounted` with no scene graph dependencies.

**Canonical entrypoint:**
```bash
godot --headless --script res://tools/run_sim.gd -- --seed=123 --days=10 --out=artifacts/run.json
```

**CLI Arguments:**
| Argument | Default | Description |
|----------|---------|-------------|
| `--seed=N` | 42 | Random seed for deterministic runs |
| `--days=N` | 10 | Number of simulation days to run |
| `--ticks=N` | 100 | Alternative: run specific tick count |
| `--out=PATH` | (none) | Save final state to JSON file |
| `--metrics_out=PATH` | (none) | Export daily metrics to CSV |

**Output:** Console summary with economy, factions, resources, enforcement stats, checksum, and collapse detection.

**CI Usage:**
```bash
# Run determinism verification
godot --headless --script res://tools/run_sim.gd -- --seed=42 --days=30 --out=run1.json
godot --headless --script res://tools/run_sim.gd -- --seed=42 --days=30 --out=run2.json
# Checksums should match for deterministic runs

# Run test suite
godot --headless --script res://tests/test_runner.gd
```

### Metrics Export
Export metrics from a saved state:
```bash
godot --headless -s tools/export_metrics_csv.gd -- --state=saves/final_state.json --out=metrics.csv
```

## Commands

```powershell
cd "c:\Users\hunte\OneDrive\Desktop\AIExperiments\games\societies"

# Run 15 days headless
godot --headless --script res://tools/run_sim.gd -- --seed=123 --days=15

# Run tests
godot --headless --script res://tests/test_runner.gd
```

## Factions & Governance (Prompt 7)

### Formation
- Agents with high grievance (from fines/blocking) or RNG chance form factions
- Requires `faction_found_min_money` and unclaimed tile nearby
- Founder seeds faction treasury with `faction_found_treasury_seed`

### Membership
- Factionless agents evaluate nearby factions daily
- Join based on land access and low tax rates
- Members gain permits on faction-owned land

### Territory
- Factions expand by claiming adjacent unclaimed tiles
- Up to `faction_claims_per_day` tiles at `faction_claim_cost` each
- Expansion prioritizes tiles closer to `home_pos`

### Governance
- Members with high grievance propose law changes
- Proposals: toggle permits, adjust tax ±step, adjust fines ±step
- Majority vote resolves after `proposal_duration_ticks`

### Treasury
- Fines on faction land → faction treasury
- Sales tax at market → market tile owner treasury
- Treasury funds territory expansion

## Land Claims & Enforcement (Prompt 6)

### Claims
- Single-tile claims per agent OR faction
- Faction claim owner ID = `1000001 + faction_id`

### Laws
Per-jurisdiction rules:
- `harvest_permit_required` - Only owner/members can gather
- `build_permit_required` - Only owner/members can build
- `fine_base` - Base fine amount
- `sales_tax_rate` - Tax on market trades (0-20%)

### Violations
| Type | Severity | Penalty |
|------|----------|---------|
| Illegal harvest | 1x fine | Blocked + fine |
| Illegal build | 2x fine | Blocked + fine |

### Enforcement
- `detect_chance`: 80% detection rate
- Fine deducted from money, confiscation if can't pay
- Market ban after 3 violations in 2 days
- Fines routed to tile owner (agent/faction/sink)

## Config Parameters

### Faction Tuning
| Parameter | Default | Description |
|-----------|---------|-------------|
| `faction_found_min_money` | 80 | Money required to found |
| `faction_found_min_grievance` | 0.5 | Grievance threshold |
| `faction_found_daily_chance` | 0.05 | Random founding chance |
| `faction_found_treasury_seed` | 50 | Initial treasury |
| `faction_claims_per_day` | 2 | Max expansion tiles |
| `faction_claim_cost` | 15 | Cost per tile |
| `sales_tax_rate_default` | 5 | Default tax rate % |

## Testing
- Determinism, Save/Load, Survival
- Market, Crafting, Contracts
- Enforcement (claims, violations, fines)
- Factions (formation, membership, territory, governance)
- **Trade Policy** (embargo, tariffs, save/load)

## Trade Policy (Prompt 8)

### Policy Types
- **Open**: Full market access, no tariffs
- **Tariff**: Market access allowed, tariff % on foreign seller proceeds
- **Embargo**: Market access denied for foreign agents

### Relations
Each faction stores `relations` dictionary: `"faction:<id>"` → `{policy, tariff_rate}`
- `"faction:0"` = policy toward factionless agents
- Same faction = always open

### Tariff Collection
- Tariffs charged on foreign sellers' proceeds at trade execution
- Routed to market owner faction's treasury
- Tracked: `tariff_collected_total`, `tariff_by_faction`

### Governance
Proposals can change trade policy:
- **Inter-faction Trade Policy**: Factions can set embargoes or tariffs on other factions or the factionless.
- **Ecology & Stability Metrics**: Pollution-based yield penalties, scarcity-sensing NPC agents, and daily metrics snapshots to track simulation health.

## Core Systems
| Parameter | Default | Description |
|-----------|---------|-------------|
| `default_relation_policy` | open | Default policy toward other factions |
| `default_relation_tariff_rate` | 0 | Default tariff rate % |
| `default_factionless_policy` | tariff | Default policy toward factionless |
| `default_factionless_tariff_rate` | 5 | Default tariff on factionless |
| `tariff_rate_max` | 30 | Maximum tariff rate |

## Current Scope (Prompt 8/9)
- ✅ Faction formation
- ✅ Membership joining
- ✅ Territory expansion
- ✅ Governance (proposals + voting)
- ✅ Treasury (fines + sales tax)
- ✅ Faction-based permits
- ✅ **Trade policy (Open/Tariff/Embargo)**
- ✅ **Tariff collection**
- ✅ **Market access control**

**Not implemented**: War/combat, complex diplomacy (Prompt 9)
