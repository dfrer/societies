class_name JobBoardSystem
extends ISimSystem

func tick(sim: RefCounted, state: SimState) -> void:
	if not state.get_tuning_bool("job_board_enabled", true):
		return
	_refresh_gather_activities(state)
	_refresh_contract_activities(state)
	_refresh_project_delivery_activities(state)
	_refresh_build_site_activities(state)
	_refresh_haul_activities(state)
	_refresh_craft_activities(state)
	_post_project_delivery_activities(state)
	_post_build_site_activities(state)
	_post_haul_activities(state)
	_post_craft_activities(state)
	_post_farm_task_activities(state)
	var max_inactive: int = state.get_tuning_int("job_board_max_inactive", 200)
	state.job_board.prune_inactive(max_inactive)

func tick_daily(sim: RefCounted, state: SimState) -> void:
	if not state.get_tuning_bool("job_board_enabled", true):
		return
	_post_daily_gather_activities(state)
	_post_daily_contract_activities(state)

func _post_daily_gather_activities(state: SimState) -> void:
	var daily_limit: int = state.get_tuning_int("job_board_daily_post_limit", 20)
	var node_limit: int = state.get_tuning_int("job_board_gather_node_post_limit", 20)
	if daily_limit <= 0 or node_limit <= 0:
		return

	var nodes := state.world.resource_nodes.duplicate()
	nodes.sort_custom(func(a, b): return a.id < b.id)

	var posted := 0
	var posted_nodes := 0
	for node in nodes:
		if posted >= daily_limit or posted_nodes >= node_limit:
			break
		if not node.has_stock(1):
			continue
		if state.job_board.has_activity_for_node(node.id):
			continue
		state.job_board.post_gather_node(node.id, node.type, state.tick)
		posted += 1
		posted_nodes += 1

func _post_daily_contract_activities(state: SimState) -> void:
	var limit: int = state.get_tuning_int("job_board_contract_post_limit", 10)
	if limit <= 0:
		return
	var contracts := state.contracts_system.get_available_contracts()
	contracts.sort_custom(func(a, b): return a.id < b.id)
	var posted := 0
	for contract in contracts:
		if posted >= limit:
			break
		if state.job_board.has_activity_for_contract(contract.id):
			continue
		state.job_board.post_accept_contract(contract.id, state.tick)
		posted += 1

func _refresh_gather_activities(state: SimState) -> void:
	var blocked_limit: int = state.get_tuning_int("job_board_gather_blocked_cancel_threshold", 3)
	for activity in state.job_board.activities:
		if activity.get("type", "") != JobBoard.ACTIVITY_GATHER_NODE:
			continue
		var status: String = activity.get("status", JobBoard.STATUS_AVAILABLE)
		if status == JobBoard.STATUS_COMPLETED or status == JobBoard.STATUS_CANCELLED:
			continue
		var data: Dictionary = activity.get("data", {})
		if blocked_limit > 0 and int(data.get("enforcement_blocked_count", 0)) >= blocked_limit:
			activity["status"] = JobBoard.STATUS_CANCELLED
			activity["updated_tick"] = state.tick
			activity["worker_id"] = -1
			continue
		var node_id: int = int(data.get("node_id", -1))
		var node := state.world.get_node_by_id(node_id)
		if node == null or not node.has_stock(1):
			activity["status"] = JobBoard.STATUS_CANCELLED
			activity["updated_tick"] = state.tick
			activity["worker_id"] = -1

func _refresh_contract_activities(state: SimState) -> void:
	for activity in state.job_board.activities:
		if activity.get("type", "") != JobBoard.ACTIVITY_ACCEPT_CONTRACT:
			continue
		var status: String = activity.get("status", JobBoard.STATUS_AVAILABLE)
		if status == JobBoard.STATUS_COMPLETED or status == JobBoard.STATUS_CANCELLED:
			continue
		var data: Dictionary = activity.get("data", {})
		var contract_id: int = int(data.get("contract_id", -1))
		var contract := state.contracts_system.get_contract(contract_id)
		if contract == null or not contract.is_available():
			activity["status"] = JobBoard.STATUS_CANCELLED
			activity["updated_tick"] = state.tick
			activity["worker_id"] = -1

func _refresh_project_delivery_activities(state: SimState) -> void:
	for activity in state.job_board.activities:
		if activity.get("type", "") != JobBoard.ACTIVITY_DELIVER_TO_PROJECT:
			continue
		var status: String = activity.get("status", JobBoard.STATUS_AVAILABLE)
		if status == JobBoard.STATUS_COMPLETED or status == JobBoard.STATUS_CANCELLED:
			continue
		var data: Dictionary = activity.get("data", {})
		var project_id: int = int(data.get("project_id", -1))
		var item_type: String = data.get("item_type", "")
		var project := state.communal_projects.get_project(project_id)
		if project == null or project.status != CommunalProject.STATUS_COLLECTING:
			var qty: int = int(data.get("quantity", 0))
			if qty > 0:
				state.communal_projects.release_project_reservation(project_id, item_type, qty)
			activity["status"] = JobBoard.STATUS_CANCELLED
			activity["updated_tick"] = state.tick
			activity["worker_id"] = -1
			continue
		var remaining: Dictionary = project.get_remaining_resources()
		if not remaining.has(item_type):
			var qty: int = int(data.get("quantity", 0))
			if qty > 0:
				state.communal_projects.release_project_reservation(project_id, item_type, qty)
			activity["status"] = JobBoard.STATUS_CANCELLED
			activity["updated_tick"] = state.tick
			activity["worker_id"] = -1

func _post_project_delivery_activities(state: SimState) -> void:
	var project_limit: int = state.get_tuning_int("job_board_project_post_limit", 10)
	var item_limit: int = state.get_tuning_int("job_board_project_item_post_limit", 20)
	if project_limit <= 0 or item_limit <= 0:
		return
	var projects := state.communal_projects.projects.duplicate()
	projects.sort_custom(func(a, b): return a.id < b.id)
	var posted_items := 0
	var posted_projects := 0
	for project in projects:
		if posted_items >= item_limit or posted_projects >= project_limit:
			break
		if project.status != CommunalProject.STATUS_COLLECTING:
			continue
		var remaining: Dictionary = project.get_remaining_resources()
		if remaining.is_empty():
			continue
		var item_keys := remaining.keys()
		item_keys.sort()
		var posted_for_project := false
		for item_type in item_keys:
			if posted_items >= item_limit:
				break
			if state.job_board.has_activity_for_project_item(project.id, item_type):
				continue
			var desired: int = int(remaining[item_type])
			var reserved := state.communal_projects.reserve_project_resources(project.id, item_type, desired)
			if reserved <= 0:
				continue
			state.job_board.post_deliver_to_project(project.id, item_type, reserved, state.tick)
			posted_items += 1
			posted_for_project = true
		if posted_for_project:
			posted_projects += 1

func _refresh_build_site_activities(state: SimState) -> void:
	for activity in state.job_board.activities:
		if activity.get("type", "") != JobBoard.ACTIVITY_BUILD_SITE:
			continue
		var status: String = activity.get("status", JobBoard.STATUS_AVAILABLE)
		if status == JobBoard.STATUS_COMPLETED or status == JobBoard.STATUS_CANCELLED:
			continue
		var data: Dictionary = activity.get("data", {})
		var project_id: int = int(data.get("project_id", -1))
		var project := state.communal_projects.get_project(project_id)
		if project == null or project.status != CommunalProject.STATUS_BUILDING:
			activity["status"] = JobBoard.STATUS_CANCELLED
			activity["updated_tick"] = state.tick
			activity["worker_id"] = -1

func _post_build_site_activities(state: SimState) -> void:
	var project_limit: int = state.get_tuning_int("job_board_build_site_post_limit", 10)
	if project_limit <= 0:
		return
	var projects := state.communal_projects.projects.duplicate()
	projects.sort_custom(func(a, b): return a.id < b.id)
	var posted := 0
	for project in projects:
		if posted >= project_limit:
			break
		if project.status != CommunalProject.STATUS_BUILDING:
			continue
		if state.job_board.has_activity_for_build_site(project.id):
			continue
		state.job_board.post_build_site(project.id, project.id, state.tick)
		posted += 1

func _refresh_haul_activities(state: SimState) -> void:
	for activity in state.job_board.activities:
		if activity.get("type", "") != JobBoard.ACTIVITY_HAUL:
			continue
		var status: String = activity.get("status", JobBoard.STATUS_AVAILABLE)
		if status == JobBoard.STATUS_COMPLETED or status == JobBoard.STATUS_CANCELLED:
			continue
		var data: Dictionary = activity.get("data", {})
		var source_id: int = int(data.get("source_id", -1))
		var destination_id: int = int(data.get("destination_id", -1))
		var item_type: String = data.get("item_type", "")
		var quantity: int = int(data.get("quantity", 0))
		var destination_type: String = data.get("destination_type", "stockpile")
		var source := state.structures.get_structure(source_id)
		var destination_project: CommunalProject = null
		if destination_type == "project":
			destination_project = state.communal_projects.get_project(destination_id)
		if source == null or item_type == "" or quantity <= 0:
			activity["status"] = JobBoard.STATUS_CANCELLED
		elif destination_type == "project" and (destination_project == null or destination_project.status != CommunalProject.STATUS_COLLECTING):
			activity["status"] = JobBoard.STATUS_CANCELLED
		if activity.get("status", "") == JobBoard.STATUS_CANCELLED:
			if source != null and item_type != "" and quantity > 0:
				source.release_reserved_item(item_type, quantity)
			if destination_project != null and item_type != "" and quantity > 0:
				state.communal_projects.release_project_reservation(destination_project.id, item_type, quantity)
			activity["updated_tick"] = state.tick
			activity["worker_id"] = -1

func _refresh_craft_activities(state: SimState) -> void:
	for activity in state.job_board.activities:
		if activity.get("type", "") != JobBoard.ACTIVITY_CRAFT_AT_STATION:
			continue
		var status: String = activity.get("status", JobBoard.STATUS_AVAILABLE)
		if status == JobBoard.STATUS_COMPLETED or status == JobBoard.STATUS_CANCELLED:
			continue
		var data: Dictionary = activity.get("data", {})
		var station_id: int = int(data.get("station_id", -1))
		var recipe_id: String = data.get("recipe_id", "")
		var workshop := state.world.get_workshop_by_id(station_id)
		var recipe: Recipe = state.recipes.get(recipe_id)
		if workshop == null or recipe == null or not workshop.is_ready():
			_release_craft_reservations(state, data)
			activity["status"] = JobBoard.STATUS_CANCELLED
			activity["updated_tick"] = state.tick
			activity["worker_id"] = -1

func _post_craft_activities(state: SimState) -> void:
	var post_limit: int = state.get_tuning_int("job_board_craft_post_limit", 8)
	var per_station_limit: int = state.get_tuning_int("job_board_craft_per_station_limit", 2)
	if post_limit <= 0 or per_station_limit <= 0:
		return
	if state.world.workshops.is_empty():
		return
	var recipes := _get_sorted_recipes(state)
	if recipes.is_empty():
		return
	var stockpiles := state.structures.get_communal_stockpiles_sorted()
	if stockpiles.is_empty():
		return

	var posted := 0
	var stations := state.world.workshops.duplicate()
	stations.sort_custom(func(a, b): return a.id < b.id)
	for station in stations:
		if posted >= post_limit:
			break
		if not station.is_ready():
			continue
		var station_posts := 0
		for recipe in recipes:
			if posted >= post_limit or station_posts >= per_station_limit:
				break
			if not _station_supports_recipe(station, recipe):
				continue
			if state.job_board.has_activity_for_craft(station.id, recipe.id):
				continue
			var stockpile := _find_stockpile_for_recipe(station, recipe, stockpiles)
			if stockpile == null:
				continue
			if not _should_craft_recipe(stockpile, recipe, state):
				continue
			var reserved_inputs := _reserve_recipe_inputs(stockpile, recipe)
			if reserved_inputs.is_empty():
				continue
			var activity := state.job_board.post_craft_at_station(station.id, recipe.id, state.tick)
			var data: Dictionary = activity.get("data", {})
			data["stockpile_id"] = stockpile.id
			data["reserved_inputs"] = reserved_inputs
			activity["data"] = data
			posted += 1
			station_posts += 1

func _get_sorted_recipes(state: SimState) -> Array:
	var recipes: Array = []
	var ids: Array = state.recipes.keys()
	ids.sort()
	for recipe_id in ids:
		recipes.append(state.recipes[recipe_id])
	return recipes

func _station_supports_recipe(station: Workshop, recipe: Recipe) -> bool:
	if recipe == null:
		return false
	if recipe.station == "" or recipe.station == "workshop":
		return true
	return station.workshop_type == recipe.station

func _find_stockpile_for_recipe(station: Workshop, recipe: Recipe, stockpiles: Array) -> StructureState:
	var closest: StructureState = null
	var best_dist := 999999
	for stockpile in stockpiles:
		var has_all := true
		for item in recipe.inputs:
			var required: int = recipe.inputs[item]
			if stockpile.get_available_item(item) < required:
				has_all = false
				break
		if not has_all:
			continue
		var dist := absi(stockpile.pos_x - station.pos_x) + absi(stockpile.pos_y - station.pos_y)
		if dist < best_dist:
			best_dist = dist
			closest = stockpile
	return closest

func _should_craft_recipe(stockpile: StructureState, recipe: Recipe, state: SimState) -> bool:
	for item in recipe.outputs:
		var target := _get_craft_target(item, state)
		if target <= 0:
			continue
		if stockpile.get_available_item(item) < target:
			return true
	return false

func _get_craft_target(item: String, state: SimState) -> int:
	match item:
		"Planks":
			return state.get_tuning_int("organization_target_planks", 20)
		"MetalIngot":
			return state.get_tuning_int("organization_target_metal_ingot", 6)
		"Axe", "Pickaxe", "Mallet", "Shovel":
			return state.get_tuning_int("organization_target_tools", 2)
	return 0

func _reserve_recipe_inputs(stockpile: StructureState, recipe: Recipe) -> Dictionary:
	var reserved := {}
	for item in recipe.inputs:
		var required: int = recipe.inputs[item]
		var locked := stockpile.reserve_item(item, required)
		if locked < required:
			for reserved_item in reserved:
				stockpile.release_reserved_item(reserved_item, reserved[reserved_item])
			return {}
		reserved[item] = locked
	return reserved

func _release_craft_reservations(state: SimState, data: Dictionary) -> void:
	var stockpile_id: int = int(data.get("stockpile_id", -1))
	var reserved_inputs: Dictionary = data.get("reserved_inputs", {})
	if stockpile_id < 0 or reserved_inputs.is_empty():
		return
	var stockpile := state.structures.get_structure(stockpile_id)
	if stockpile == null:
		return
	for item in reserved_inputs:
		stockpile.release_reserved_item(item, int(reserved_inputs[item]))

func _post_haul_activities(state: SimState) -> void:
	var haul_limit: int = state.get_tuning_int("job_board_haul_post_limit", 10)
	if haul_limit <= 0:
		return
	var projects := state.communal_projects.projects.duplicate()
	projects.sort_custom(func(a, b): return a.id < b.id)
	var posted := 0
	for project in projects:
		if posted >= haul_limit:
			break
		if project.status != CommunalProject.STATUS_COLLECTING:
			continue
		var remaining: Dictionary = project.get_remaining_resources()
		if remaining.is_empty():
			continue
		var item_keys := remaining.keys()
		item_keys.sort()
		for item_type in item_keys:
			if posted >= haul_limit:
				break
			var desired: int = int(remaining[item_type])
			if desired <= 0:
				continue
			var source := state.structures.find_communal_stockpile_with_item(item_type, 1)
			if source == null:
				continue
			var reserved_from_project := state.communal_projects.reserve_project_resources(project.id, item_type, desired)
			if reserved_from_project <= 0:
				continue
			var reserved_from_stockpile := source.reserve_item(item_type, reserved_from_project)
			if reserved_from_stockpile <= 0:
				state.communal_projects.release_project_reservation(project.id, item_type, reserved_from_project)
				continue
			state.job_board.post_haul(source.id, project.id, item_type, reserved_from_stockpile, state.tick, "project")
			posted += 1

func _post_farm_task_activities(state: SimState) -> void:
	if not state.world.has_method("get_farm_plots"):
		return
	var farm_limit: int = state.get_tuning_int("job_board_farm_task_post_limit", 10)
	if farm_limit <= 0:
		return
	var plots: Array = state.world.get_farm_plots()
	if plots == null:
		return
	var posted := 0
	for plot in plots:
		if posted >= farm_limit:
			break
		# Expect plot fields: id, task_type, crop_type
		if plot.get("task_type", "") == "":
			continue
		var plot_id: int = int(plot.get("id", 0))
		var task_type: String = plot.get("task_type", "")
		if state.job_board.has_activity_for_farm_plot(plot_id, task_type):
			continue
		var stockpile_id: int = -1
		if task_type == "DELIVER":
			stockpile_id = _find_farm_delivery_stockpile(state, plot)
		state.job_board.post_farm_task(plot_id, task_type,
			plot.get("crop_type", ""), state.tick, stockpile_id)
		posted += 1

func _find_farm_delivery_stockpile(state: SimState, plot: Dictionary) -> int:
	var owner_id: int = int(plot.get("owner_id", 0))
	var stockpiles := state.structures.get_stockpiles_for_owner(owner_id)
	if stockpiles.is_empty():
		return -1
	var plot_x: int = int(plot.get("x", 0))
	var plot_y: int = int(plot.get("y", 0))
	var best_id := -1
	var best_dist := 999999
	for stockpile in stockpiles:
		var dist := absi(stockpile.pos_x - plot_x) + absi(stockpile.pos_y - plot_y)
		if dist < best_dist:
			best_dist = dist
			best_id = stockpile.id
	return best_id
