# Metrics Snapshot Schema

Version: **2** (as of 2026-01-20)

## Overview

The `Metrics.create_snapshot()` function produces a standardized JSON-compatible dictionary capturing the complete simulation state at a point in time. Snapshots are generated daily by `MetricsSystem.tick_daily()` and stored in `SimState.metrics_history`.

## Schema Fields

### Core
| Field | Type | Description |
|-------|------|-------------|
| `schema_version` | int | Schema version for compatibility (currently 2) |
| `day` | int | Simulation day number |
| `tick` | int | Current tick count |

### Population
| Field | Type | Description |
|-------|------|-------------|
| `population` | int | Total agent count (alive + dead) |
| `alive_agents` | int | Number of living agents |
| `dead_agents` | int | Number of dead agents |
| `starving_agents` | int | Agents with hunger <= 0 that are alive |

### Environment
| Field | Type | Description |
|-------|------|-------------|
| `avg_pollution` | float | Average pollution across all tiles |
| `total_pollution` | float | Sum of all tile pollution values |
| `max_pollution` | float | Maximum pollution on any single tile |
| `resource_totals` | Dictionary | `{type: stock}` - total stock by resource type |
| `workshop_count` | int | Total workshop count |
| `workshop_ready_count` | int | Ready workshops count |

### Inventory
| Field | Type | Description |
|-------|------|-------------|
| `inventory_totals` | Dictionary | `{item_name: qty}` - aggregate inventory across all agents |

### Market
| Field | Type | Description |
|-------|------|-------------|
| `market_buy_orders` | int | Active buy order count |
| `market_sell_orders` | int | Active sell order count |
| `market_ref_prices` | Dictionary | `{item: price}` - reference prices |
| `market_trade_volumes` | Dictionary | `{item: count}` - cumulative trades by item |
| `total_trades` | int | Cumulative total trades |
| `orders_denied_embargo` | int | Orders denied due to embargo |

### Factions
| Field | Type | Description |
|-------|------|-------------|
| `factions_count` | int | Number of factions |
| `faction_treasury_total` | int | Sum of all faction treasuries |
| `faction_member_counts` | Dictionary | `{faction_id: member_count}` |

### Economy
| Field | Type | Description |
|-------|------|-------------|
| `fines_collected` | int | Total fines collected |
| `taxes_collected` | int | Total taxes collected |

### Legacy Compatibility
These fields are maintained for backward compatibility:

| Field | Type | Description |
|-------|------|-------------|
| `pollution` | float | Alias for `avg_pollution` |
| `berry_stock_total` | int | Berry resource stock |
| `tree_stock_total` | int | Tree resource stock |
| `ore_stock_total` | int | Ore resource stock |
| `avg_hunger` | float | Average hunger across agents |
| `ref_price_food` | float | Average of Berries and CookedMeal prices |
| `trades_today` | int | Alias for `total_trades` |
| `contracts_completed_today` | int | Contracts completed count |

## Example Snapshot

```json
{
  "schema_version": 2,
  "day": 10,
  "tick": 240,
  "population": 20,
  "alive_agents": 18,
  "dead_agents": 2,
  "starving_agents": 1,
  "avg_pollution": 0.05,
  "total_pollution": 460.8,
  "max_pollution": 0.15,
  "resource_totals": {"berry": 850, "tree": 400, "ore": 200},
  "workshop_count": 3,
  "workshop_ready_count": 2,
  "inventory_totals": {"Berries": 500, "CookedMeal": 120, "Tool": 45},
  "market_buy_orders": 8,
  "market_sell_orders": 12,
  "market_ref_prices": {"Berries": 12.5, "CookedMeal": 25.0, "Tool": 50.0},
  "market_trade_volumes": {"Berries": 45, "CookedMeal": 20, "Tool": 8},
  "total_trades": 73,
  "orders_denied_embargo": 0,
  "factions_count": 2,
  "faction_treasury_total": 500,
  "faction_member_counts": {"1001": 8, "1002": 5},
  "fines_collected": 120,
  "taxes_collected": 85
}
```

## Validation

Use `Metrics.validate_snapshot(snapshot)` to check schema compliance:

```gdscript
var result := Metrics.validate_snapshot(snapshot)
if not result.valid:
    print("Missing fields: ", result.missing_fields)
```

## Stability Guarantees

- Schema version increments on breaking changes
- Legacy fields maintained for backward compatibility
- All float values use `snappedf(..., 0.00000001)` for determinism
