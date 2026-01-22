class_name JobBoardSystem
extends ISimSystem

func tick(sim: RefCounted, state: SimState) -> void:
	if not state.get_tuning_bool("job_board_enabled", true):
		return
	_refresh_gather_activities(state)
	_refresh_contract_activities(state)
	_refresh_project_delivery_activities(state)
	_post_project_delivery_activities(state)
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
	for activity in state.job_board.activities:
		if activity.get("type", "") != JobBoard.ACTIVITY_GATHER_NODE:
			continue
		var status: String = activity.get("status", JobBoard.STATUS_AVAILABLE)
		if status == JobBoard.STATUS_COMPLETED or status == JobBoard.STATUS_CANCELLED:
			continue
		var data: Dictionary = activity.get("data", {})
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
			activity["status"] = JobBoard.STATUS_CANCELLED
			activity["updated_tick"] = state.tick
			activity["worker_id"] = -1
			continue
		var remaining: Dictionary = project.get_remaining_resources()
		if not remaining.has(item_type):
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
			state.job_board.post_deliver_to_project(project.id, item_type, int(remaining[item_type]), state.tick)
			posted_items += 1
			posted_for_project = true
		if posted_for_project:
			posted_projects += 1
