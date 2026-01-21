## Metrics - daily snapshot of simulation state for tracking and stability analysis
class_name Metrics
extends RefCounted

## Snapshot schema version - increment when schema changes
const SCHEMA_VERSION := 2

## Required field names for schema validation
const REQUIRED_FIELDS := [
	"schema_version", "day", "tick",
	# Population
	"population", "alive_agents", "dead_agents", "starving_agents",
	# Environment
	"avg_pollution", "total_pollution", "max_pollution", "resource_totals",
	"workshop_count", "workshop_ready_count",
	# Inventory
	"inventory_totals",
	# Market
	"market_buy_orders", "market_sell_orders", "market_ref_prices",
	"market_trade_volumes", "total_trades", "orders_denied_embargo",
	# Factions
	"factions_count", "faction_treasury_total", "faction_member_counts",
	# Economy
	"fines_collected", "taxes_collected"
]

## Create a comprehensive snapshot from SimState (UI-independent)
static func create_snapshot(state: SimState) -> Dictionary:
	var world := state.world
	var market := state.market
	var ticks_per_day: int = int(state.tuning.get("ticks_per_day", 24))
	var current_day: int = state.tick / ticks_per_day
	
	# Population stats
	var total_agents := state.agents.size()
	var alive_count := 0
	var dead_count := 0
	var starving_count := 0
	var inventory_totals: Dictionary = {}
	
	for agent in state.agents:
		if agent.is_alive():
			alive_count += 1
			if agent.get_hunger() <= 0:
				starving_count += 1
		else:
			dead_count += 1
		# Aggregate inventory
		for item_name in agent.inventory:
			var qty: int = agent.inventory[item_name]
			inventory_totals[item_name] = inventory_totals.get(item_name, 0) + qty
	
	# Environment stats
	var avg_pollution := world.get_average_pollution()
	var total_pollution := 0.0
	var max_pollution := 0.0
	for p in world.pollution:
		total_pollution += p
		if p > max_pollution:
			max_pollution = p
	
	# Resource totals by type
	var resource_totals: Dictionary = {}
	var resource_types := {}
	for node in world.resource_nodes:
		resource_types[node.type] = true
	for rtype in resource_types:
		resource_totals[rtype] = world.get_total_stock(rtype)
	
	# Market stats
	var market_ref_prices: Dictionary = {}
	for item in market.ref_price:
		market_ref_prices[item] = snappedf(market.ref_price[item], 0.00000001)
	
	var market_trade_volumes: Dictionary = {}
	for item in market.trade_counts:
		market_trade_volumes[item] = market.trade_counts[item]
	
	# Faction stats
	var faction_treasury_total := 0
	var faction_member_counts: Dictionary = {}
	for faction in state.factions:
		faction_treasury_total += faction.treasury
		faction_member_counts[str(faction.id)] = faction.members.size()
	
	return {
		"schema_version": SCHEMA_VERSION,
		"day": current_day,
		"tick": state.tick,
		# Population
		"population": total_agents,
		"alive_agents": alive_count,
		"dead_agents": dead_count,
		"starving_agents": starving_count,
		# Environment
		"avg_pollution": snappedf(avg_pollution, 0.00000001),
		"total_pollution": snappedf(total_pollution, 0.00000001),
		"max_pollution": snappedf(max_pollution, 0.00000001),
		"resource_totals": resource_totals,
		"workshop_count": world.get_workshop_count(),
		"workshop_ready_count": world.get_ready_workshop_count(),
		# Inventory
		"inventory_totals": inventory_totals,
		# Market
		"market_buy_orders": market.buy_orders.size(),
		"market_sell_orders": market.sell_orders.size(),
		"market_ref_prices": market_ref_prices,
		"market_trade_volumes": market_trade_volumes,
		"total_trades": market.total_trades,
		"orders_denied_embargo": market.orders_denied_embargo,
		# Factions
		"factions_count": state.factions.size(),
		"faction_treasury_total": faction_treasury_total,
		"faction_member_counts": faction_member_counts,
		# Economy
		"fines_collected": state.enforcement.fines_collected,
		"taxes_collected": state.taxes_collected,
		# Legacy compatibility fields
		"pollution": snappedf(avg_pollution, 0.00000001),
		"berry_stock_total": resource_totals.get("berry", 0),
		"tree_stock_total": resource_totals.get("tree", 0),
		"ore_stock_total": resource_totals.get("ore", 0),
		"avg_hunger": snappedf(state.get_average_hunger(), 0.00000001),
		"ref_price_food": snappedf((market.get_ref_price("Berries") + market.get_ref_price("CookedMeal")) / 2.0, 0.00000001),
		"trades_today": market.total_trades,
		"contracts_completed_today": state.contracts_system.stats.get("completed", 0)
	}

## Validate that a snapshot has all required fields
static func validate_snapshot(snapshot: Dictionary) -> Dictionary:
	var missing := []
	for field in REQUIRED_FIELDS:
		if not snapshot.has(field):
			missing.append(field)
	return {
		"valid": missing.is_empty(),
		"missing_fields": missing
	}

## Get the expected types for fields (for golden test validation)
static func get_field_types() -> Dictionary:
	return {
		"schema_version": TYPE_INT,
		"day": TYPE_INT,
		"tick": TYPE_INT,
		"population": TYPE_INT,
		"alive_agents": TYPE_INT,
		"dead_agents": TYPE_INT,
		"starving_agents": TYPE_INT,
		"avg_pollution": TYPE_FLOAT,
		"total_pollution": TYPE_FLOAT,
		"max_pollution": TYPE_FLOAT,
		"resource_totals": TYPE_DICTIONARY,
		"workshop_count": TYPE_INT,
		"workshop_ready_count": TYPE_INT,
		"inventory_totals": TYPE_DICTIONARY,
		"market_buy_orders": TYPE_INT,
		"market_sell_orders": TYPE_INT,
		"market_ref_prices": TYPE_DICTIONARY,
		"market_trade_volumes": TYPE_DICTIONARY,
		"total_trades": TYPE_INT,
		"orders_denied_embargo": TYPE_INT,
		"factions_count": TYPE_INT,
		"faction_treasury_total": TYPE_INT,
		"faction_member_counts": TYPE_DICTIONARY,
		"fines_collected": TYPE_INT,
		"taxes_collected": TYPE_INT
	}
