## EnvironmentSystem - handles all environment updates (regen, pollution)
## All environment changes go through this system for clean boundaries
class_name EnvironmentSystem
extends ISimSystem

## Called once per day for environment updates
func tick_daily(sim: RefCounted, state: SimState) -> void:
	_regenerate_resources(state)
	_decay_pollution(state)
	_decay_resources(state)  # Apply perishable resource decay
	# Future Phase 3: _spread_pollution(state), _grow_flora(state)

## Called every tick - currently no per-tick environment updates
func tick(sim: RefCounted, state: SimState) -> void:
	pass

# ============================================
# RESOURCE REGENERATION
# ============================================

## Regenerate resources based on tuning and pollution
func _regenerate_resources(state: SimState) -> void:
	var berry_regen: int = state.tuning.get("berry_regen_per_day", 3)
	var tree_regen: int = state.tuning.get("tree_regen_per_day", 2)
	var pollution_impact: float = state.tuning.get("pollution_impact", 0.5)
	
	for node in state.world.resource_nodes:
		# Ore does NOT regenerate - it's a finite resource
		if node.type == "ore":
			continue
		
		var local_pollution := state.world.get_pollution(node.pos_x, node.pos_y)
		var regen_factor := 1.0 - local_pollution * pollution_impact
		regen_factor = maxf(0.0, regen_factor)  # Clamp to prevent negative
		
		if node.type == "berry":
			node.add_stock(int(berry_regen * regen_factor))
		elif node.type == "tree":
			node.add_stock(int(tree_regen * regen_factor))

# ============================================
# POLLUTION DECAY
# ============================================

## Apply decay to all pollution tiles
func _decay_pollution(state: SimState) -> void:
	var pollution_decay: float = state.tuning.get("pollution_decay_per_day", 0.05)
	
	for i in range(state.world.pollution.size()):
		state.world.pollution[i] *= (1.0 - pollution_decay)

# ============================================
# PERISHABLE RESOURCE DECAY
# ============================================

## Apply perishable decay to resources
func _decay_resources(state: SimState) -> void:
	var ticks_per_day: int = state.tuning.get("ticks_per_day", 24)
	
	for node in state.world.resource_nodes:
		node.apply_decay(state.tick, ticks_per_day)

# ============================================
# FUTURE PHASE 3 HOOKS
# ============================================

## Stub: Spread pollution to adjacent tiles
## TODO Phase 3: Implement pollution spreading mechanics
#func _spread_pollution(state: SimState) -> void:
#	pass

## Stub: Grow flora based on conditions
## TODO Phase 3: Implement flora growth mechanics
#func _grow_flora(state: SimState) -> void:
#	pass
