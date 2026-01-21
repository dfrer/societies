## EnvironmentSystem - handles all environment updates (regen, pollution)
## All environment changes go through this system for clean boundaries
class_name EnvironmentSystem
extends ISimSystem

## Called once per day for environment updates
func tick_daily(sim: RefCounted, state: SimState) -> void:
	_regenerate_resources(state)
	_decay_pollution(state)
	_decay_resources(state)  # Apply perishable resource decay
	_spread_pollution(state)
	_grow_flora(state)

## Called every tick - currently no per-tick environment updates
func tick(sim: RefCounted, state: SimState) -> void:
	pass

# ============================================
# RESOURCE REGENERATION
# ============================================

## Regenerate resources based on tuning and pollution
func _regenerate_resources(state: SimState) -> void:
	var berry_regen: int = state.get_tuning_int("berry_regen_per_day", 3)
	var tree_regen: int = state.get_tuning_int("tree_regen_per_day", 2)
	var pollution_impact: float = state.get_tuning_float("pollution_impact", 0.5)
	
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
	var pollution_decay: float = state.get_tuning_float("pollution_decay_per_day", 0.05)
	
	for i in range(state.world.pollution.size()):
		state.world.pollution[i] *= (1.0 - pollution_decay)

# ============================================
# PERISHABLE RESOURCE DECAY
# ============================================

## Apply perishable decay to resources
func _decay_resources(state: SimState) -> void:
	var ticks_per_day: int = state.get_tuning_int("ticks_per_day", 24)
	
	for node in state.world.resource_nodes:
		node.apply_decay(state.tick, ticks_per_day)

# ============================================
# POLLUTION SPREAD
# ============================================

## Spread pollution to adjacent tiles using a simple diffusion model
func _spread_pollution(state: SimState) -> void:
	var spread_rate: float = state.get_tuning_float("pollution_spread_rate", 0.05)
	var spread_threshold: float = state.get_tuning_float("pollution_spread_threshold", 0.05)
	if spread_rate <= 0.0:
		return

	var width := state.world.width
	var height := state.world.height
	var current: Array = state.world.pollution
	var updated := current.duplicate()

	for y in range(height):
		for x in range(width):
			var idx := y * width + x
			var amount: float = current[idx]
			if amount <= spread_threshold:
				continue

			var neighbors := []
			if x > 0:
				neighbors.append(Vector2i(x - 1, y))
			if x < width - 1:
				neighbors.append(Vector2i(x + 1, y))
			if y > 0:
				neighbors.append(Vector2i(x, y - 1))
			if y < height - 1:
				neighbors.append(Vector2i(x, y + 1))

			if neighbors.is_empty():
				continue

			var spread_amount := amount * spread_rate
			updated[idx] = maxf(0.0, updated[idx] - spread_amount)
			var per_neighbor := spread_amount / float(neighbors.size())
			for neighbor in neighbors:
				var n_idx := neighbor.y * width + neighbor.x
				updated[n_idx] = clampf(updated[n_idx] + per_neighbor, 0.0, 1.0)

	state.world.pollution = updated
	state.world._pollution_cache_tick = -1

# ============================================
# FLORA GROWTH
# ============================================

## Grow flora by spawning new berry/tree nodes on low-pollution tiles
func _grow_flora(state: SimState) -> void:
	var attempts: int = state.get_tuning_int("flora_growth_attempts_per_day", 6)
	var growth_chance: float = state.get_tuning_float("flora_growth_chance", 0.35)
	var pollution_max: float = state.get_tuning_float("flora_growth_pollution_max", 0.3)
	var berry_weight: float = state.get_tuning_float("flora_growth_berry_weight", 0.6)

	if attempts <= 0 or growth_chance <= 0.0:
		return

	for attempt in range(attempts):
		if state.rng.randf() > growth_chance:
			continue

		var x := state.rng.randi_range(0, state.world.width - 1)
		var y := state.rng.randi_range(0, state.world.height - 1)
		if state.world.get_pollution(x, y) > pollution_max:
			continue
		if state.world.get_node_at(x, y) != null:
			continue

		var node_type := "berry" if state.rng.randf() < berry_weight else "tree"
		_spawn_flora_node(state, node_type, x, y)

func _spawn_flora_node(state: SimState, node_type: String, x: int, y: int) -> void:
	var max_stock_key := "%s_max_stock" % node_type
	var start_min_key := "%s_start_min" % node_type
	var start_max_key := "%s_start_max" % node_type

	var max_stock: int = state.get_tuning_int(max_stock_key, 10)
	var start_min: int = state.get_tuning_int(start_min_key, max_stock)
	var start_max: int = state.get_tuning_int(start_max_key, max_stock)
	if start_min > start_max:
		var temp := start_min
		start_min = start_max
		start_max = temp

	var node := ResourceNode.new()
	node.id = state.next_node_id
	state.next_node_id += 1
	node.type = node_type
	node.pos_x = x
	node.pos_y = y
	node.max_stock = max_stock
	node.stock = state.rng.randi_range(start_min, start_max)
	node.set_type_spoilage()
	node.quality = state.rng.randf_range(0.8, 1.2)
	state.world.add_resource_node(node)
