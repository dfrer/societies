## SimState - container for all simulation state
class_name SimState
extends RefCounted

var tick: int = 0
var rng: RNG = null
var world: World = null
var agents: Array = []
var market: Market = null
var crafting: Crafting = null
var contracts_system: ContractsSystem = null
var enforcement: Enforcement = null
var factions_system: FactionsSystem = null
var communal_projects: CommunalProjectsSystem = null
var job_board: JobBoard = null
var structures: Structures = null
var world_fines_sink: int = 0
var taxes_collected: int = 0
var laws_by_owner: Dictionary = {}  # owner_id -> Laws
var metrics_history: Array = []  # Array of snapshot dictionaries
var factions: Array = []
var organizations: Array = []
var tuning: Dictionary = {}
var tuning_config: TuningConfig = null
var items: Dictionary = {}
var recipes: Dictionary = {}
var recipe_sorted_ids: Array = []
var events: Array = [] # Array[Dictionary]
var intents_by_id: Dictionary = {} # intent_id -> Dictionary
var decision_traces: Array = [] # Array[Dictionary]
var decision_trace_enabled: bool = false
var decision_trace_sample_interval: int = 1
var next_agent_id: int = 1
var next_faction_id: int = 1001
var next_organization_id: int = 2001
var next_workshop_id: int = 1
var next_node_id: int = 1
var next_intent_id: int = 1

## Performance: O(1) agent lookup by ID
var agent_by_id: Dictionary = {}  # id -> Agent

func _init() -> void:
	rng = RNG.new()
	world = World.new()
	market = Market.new()
	crafting = Crafting.new()
	contracts_system = ContractsSystem.new()
	enforcement = Enforcement.new()
	factions_system = FactionsSystem.new()
	communal_projects = CommunalProjectsSystem.new()
	job_board = JobBoard.new()
	structures = Structures.new()
	tuning_config = TuningConfig.new()
	organizations = []

## Get laws for a jurisdiction owner
func get_laws(owner_id: int) -> Laws:
	if not laws_by_owner.has(owner_id):
		var default_laws := Laws.new()
		default_laws.init_from_tuning(tuning)
		laws_by_owner[owner_id] = default_laws
	return laws_by_owner[owner_id]

## Validate escrow/locked money invariants across agents and contracts
func validate_financial_invariants() -> void:
	var total_locked_money := 0
	for agent in agents:
		if agent.locked_money > agent.money:
			push_error("Financial invariant: agent %d locked_money %d exceeds money %d" % [agent.id, agent.locked_money, agent.money])
			agent.locked_money = agent.money
		total_locked_money += agent.locked_money

		for item_name in agent.locked_inventory:
			var locked_qty: int = agent.locked_inventory[item_name]
			var total_qty: int = agent.inventory.get(item_name, 0)
			if locked_qty > total_qty:
				push_error("Inventory invariant: agent %d locked %s=%d exceeds inventory %d" % [agent.id, item_name, locked_qty, total_qty])
				agent.locked_inventory[item_name] = total_qty

	var total_escrow := contracts_system.get_total_escrow()
	if total_escrow > total_locked_money:
		push_error("Escrow invariant: total escrow %d exceeds locked money %d" % [total_escrow, total_locked_money])

## Typed tuning access (prefers TuningConfig defaults/validation)
func get_tuning_int(key: String, fallback: int = 0) -> int:
	if tuning_config != null:
		return tuning_config.get_int(key)
	if tuning.has(key):
		return int(tuning[key])
	return fallback

func get_tuning_float(key: String, fallback: float = 0.0) -> float:
	if tuning_config != null:
		return tuning_config.get_float(key)
	if tuning.has(key):
		return float(tuning[key])
	return fallback

func get_tuning_bool(key: String, fallback: bool = false) -> bool:
	if tuning_config != null:
		return tuning_config.get_bool(key)
	if tuning.has(key):
		return bool(tuning[key])
	return fallback

func get_tuning_string(key: String, fallback: String = "") -> String:
	if tuning_config != null:
		return tuning_config.get_string(key)
	if tuning.has(key):
		return str(tuning[key])
	return fallback

func update_recipe_sorted_ids() -> void:
	recipe_sorted_ids = recipes.keys()
	recipe_sorted_ids.sort()

## Serialize entire simulation state to dictionary
func to_dict() -> Dictionary:
	var agents_data := []
	for agent in agents:
		agents_data.append(agent.to_dict())
	
	var factions_data := []
	for faction in factions:
		factions_data.append(faction.to_dict())

	var organizations_data := []
	for organization in organizations:
		organizations_data.append(organization.to_dict())
	
	var recipes_data := {}
	for recipe_id in recipes:
		recipes_data[recipe_id] = recipes[recipe_id].to_dict()
	
	var laws_data := {}
	for owner_id in laws_by_owner:
		laws_data[str(owner_id)] = laws_by_owner[owner_id].to_dict()
	
	return {
		"tick": tick,
		"rng": rng.get_state(),
		"world": world.to_dict(),
		"agents": agents_data,
		"market": market.to_dict(),
		"crafting": crafting.to_dict(),
		"contracts_system": contracts_system.to_dict(),
		"enforcement": enforcement.to_dict(),
		"factions_system": factions_system.to_dict(),
		"communal_projects": communal_projects.to_dict(),
		"job_board": job_board.to_dict(),
		"structures": structures.to_dict(),
		"laws_by_owner": laws_data,
		"factions": factions_data,
		"organizations": organizations_data,
		"metrics_history": _sanitize_metrics_for_serialization(metrics_history),
		"tuning": _sanitize_tuning_for_serialization(tuning),
		"items": _sanitize_items_for_serialization(items),
		"recipes": recipes_data,
		"intents": _sanitize_intents_for_serialization(intents_by_id),
		"decision_traces": _sanitize_decision_traces_for_serialization(decision_traces),
		"next_agent_id": next_agent_id,
		"next_faction_id": next_faction_id,
		"next_organization_id": next_organization_id,
		"next_workshop_id": next_workshop_id,
		"next_node_id": next_node_id,
		"next_intent_id": next_intent_id,
		"world_fines_sink": world_fines_sink,
		"taxes_collected": taxes_collected,
		"events": _sanitize_events_for_serialization(events)
	}

func _sanitize_events_for_serialization(events_data: Array) -> Array:
	var result := []
	var int_keys := ["tick", "day", "agent_id", "contract_id", "issuer_id", "worker_id",
					 "faction_id", "founder_id", "payout", "qty", "deadline", "refund",
					 "fine", "owner_id", "proposal_id", "proposer_id", "tariff_rate",
					 "law_owner_id", "votes_for", "votes_against", "x", "y",
					 "sales_tax_rate", "fine_base", "fine_applied"]
	for event in events_data:
		var fixed_event := {}
		fixed_event["tick"] = int(event.get("tick", 0))
		fixed_event["type"] = event.get("type", "")
		var data: Dictionary = event.get("data", {})
		fixed_event["data"] = _sanitize_event_data_recursive(data, int_keys)
		result.append(fixed_event)
	return result

func _sanitize_tuning_for_serialization(d: Dictionary) -> Dictionary:
	var result := {}
	var float_keys := ["hunger_drain_per_tick", "pollution_impact", "pollution_decay_per_day",
				   "pollution_per_ore", "pollution_spread_rate", "pollution_spread_threshold",
				   "price_ema_alpha", "bid_scarcity_strength",
				   "ask_surplus_strength", "crafter_ratio", "craft_profit_margin",
				   "daily_contract_post_chance", "contract_payout_multiplier", "detect_chance",
				   "faction_found_min_grievance", "faction_found_daily_chance",
				   "join_min_score", "proposal_grievance_threshold", "grievance_decay_daily",
				   "grievance_fine_increase", "grievance_blocked_increase",
				   "inflation_threshold", "food_yield_pollution_start", "food_yield_pollution_step",
				   "hunger_drain_pollution_mult", "pollution_high_threshold", "food_scarcity_multiplier",
				   "starvation_collapse_ratio", "pollution_collapse_threshold",
				   "flora_growth_chance", "flora_growth_pollution_max", "flora_growth_berry_weight"]
	for k in d:
		if k in float_keys:
			result[k] = snappedf(float(d[k]), 0.00000001)
		elif d[k] is int:
			result[k] = int(d[k])
		elif d[k] is bool:
			result[k] = bool(d[k])
		elif d[k] is String:
			result[k] = d[k]
		else:
			result[k] = d[k]
	return result

func _sanitize_items_for_serialization(d: Dictionary) -> Dictionary:
	var result := {}
	for item_name in d:
		var item_dict: Dictionary = d[item_name]
		var sanitized := {}
		for k in item_dict:
			var val = item_dict[k]
			if k in ["base_value", "nutrition"]:
				sanitized[k] = snappedf(float(val), 0.00000001)
			elif val is int:
				sanitized[k] = int(val)
			elif val is bool:
				sanitized[k] = bool(val)
			elif val is String:
				sanitized[k] = val
			else:
				sanitized[k] = val
		result[item_name] = sanitized
	return result

func _sanitize_metrics_for_serialization(history: Array) -> Array:
	var result := []
	for entry in history:
		var sanitized := {}
		for k in entry:
			var val = entry[k]
			if val is float:
				sanitized[k] = snappedf(val, 0.00000001)
			elif val is Dictionary:
				# Recursive for sub-dicts like prices or stocks
				var sub := {}
				for sk in val:
					var sval = val[sk]
					if sval is float:
						sub[sk] = snappedf(sval, 0.00000001)
					else:
						sub[sk] = sval
				sanitized[k] = sub
			else:
				sanitized[k] = val
		result.append(sanitized)
	return result

func _sanitize_intents_for_serialization(intents_data: Variant) -> Array:
	var result := []
	var source_intents: Array = intents_data
	if intents_data is Dictionary:
		source_intents = intents_data.values()
	for intent in source_intents:
		result.append({
			"intent_id": int(intent.get("intent_id", 0)),
			"agent_id": int(intent.get("agent_id", 0)),
			"type": intent.get("type", ""),
			"status": intent.get("status", "active"),
			"created_tick": int(intent.get("created_tick", 0)),
			"updated_tick": int(intent.get("updated_tick", 0)),
			"data": _sanitize_intent_data(intent.get("data", {}))
		})
	return result

func _sanitize_intent_data(data: Dictionary) -> Dictionary:
	var fixed := {}
	for key in data:
		var val = data[key]
		if val is int or val is float:
			fixed[key] = int(val)
		elif val is bool:
			fixed[key] = bool(val)
		else:
			fixed[key] = val
	return fixed

func _sanitize_decision_traces_for_serialization(traces_data: Array) -> Array:
	var result := []
	for trace in traces_data:
		result.append({
			"tick": int(trace.get("tick", 0)),
			"agent_id": int(trace.get("agent_id", 0)),
			"intent_id": int(trace.get("intent_id", -1)),
			"intent_type": trace.get("intent_type", ""),
			"activity_id": int(trace.get("activity_id", -1)),
			"activity_type": trace.get("activity_type", ""),
			"action_type": trace.get("action_type", "")
		})
	return result

## Deserialize simulation state from dictionary
static func from_dict(d: Dictionary) -> SimState:
	var state := SimState.new()
	state.tick = int(d.get("tick", 0))

	state.rng = RNG.new()
	state.rng.set_state(d.get("rng", {}))

	state.world = World.from_dict(d.get("world", {}))

	state.agents = []
	for agent_data in d.get("agents", []):
		state.agents.append(Agent.from_dict(agent_data))
	state.rebuild_agent_lookup()  # Build O(1) lookup after loading

	state.market = Market.from_dict(d.get("market", {}))
	state.crafting = Crafting.from_dict(d.get("crafting", {}))
	state.contracts_system = ContractsSystem.from_dict(d.get("contracts_system", {}))
	state.enforcement = Enforcement.from_dict(d.get("enforcement", {}))
	state.factions_system = FactionsSystem.from_dict(d.get("factions_system", {}))
	state.communal_projects = CommunalProjectsSystem.from_dict(d.get("communal_projects", {}))
	state.job_board = JobBoard.from_dict(d.get("job_board", {}))
	state.structures = Structures.from_dict(d.get("structures", {}))
	state.world_fines_sink = int(d.get("world_fines_sink", 0))
	state.taxes_collected = int(d.get("taxes_collected", 0))
	state.metrics_history = state._sanitize_metrics_for_serialization(d.get("metrics_history", []))

	state.laws_by_owner = {}
	var laws_data: Dictionary = d.get("laws_by_owner", {})
	for owner_str in laws_data:
		var owner_id := int(owner_str)
		state.laws_by_owner[owner_id] = Laws.from_dict(laws_data[owner_str])

	state.factions = []
	for faction_data in d.get("factions", []):
		state.factions.append(Faction.from_dict(faction_data))

	state.organizations = []
	for organization_data in d.get("organizations", []):
		state.organizations.append(Organization.from_dict(organization_data))

	# Convert tuning values - integers, floats, and bools as appropriate
	state.tuning = state._sanitize_tuning_for_serialization(d.get("tuning", {}))
	state.tuning_config = TuningConfig.new()
	state.tuning_config.load_from_dict(state.tuning)

	# Convert items values
	state.items = state._sanitize_items_for_serialization(d.get("items", {}))

	state.recipes = {}
	var recipes_data: Dictionary = d.get("recipes", {})
	for recipe_id in recipes_data:
		state.recipes[recipe_id] = Recipe.from_dict(recipes_data[recipe_id])
	state.update_recipe_sorted_ids()

	state.next_agent_id = int(d.get("next_agent_id", 1))
	state.next_faction_id = int(d.get("next_faction_id", 1001))
	state.next_organization_id = int(d.get("next_organization_id", 2001))
	state.next_workshop_id = int(d.get("next_workshop_id", 1))
	state.next_node_id = int(d.get("next_node_id", 1))
	state.next_intent_id = int(d.get("next_intent_id", 1))
	state.events = _sanitize_events_for_deserialization(d.get("events", []))
	state.intents_by_id = {}
	var intents_list := state._sanitize_intents_for_serialization(d.get("intents", []))
	for intent in intents_list:
		var intent_id := int(intent.get("intent_id", 0))
		if intent_id > 0:
			state.intents_by_id[intent_id] = intent
	state.decision_traces = state._sanitize_decision_traces_for_serialization(d.get("decision_traces", []))

	return state


## Convert event data fields back to proper types after JSON deserialization
static func _sanitize_events_for_deserialization(events_data: Array) -> Array:
	var result := []
	# Keys that should be integers in event data
	var int_keys := ["tick", "day", "agent_id", "contract_id", "issuer_id", "worker_id",
					 "faction_id", "founder_id", "payout", "qty", "deadline", "refund",
					 "fine", "owner_id", "proposal_id", "proposer_id", "tariff_rate",
					 "law_owner_id", "votes_for", "votes_against", "x", "y",
					 "sales_tax_rate", "fine_base", "fine_applied"]
	for event in events_data:
		var fixed_event := {}
		fixed_event["tick"] = int(event.get("tick", 0))
		fixed_event["type"] = event.get("type", "")
		# Sanitize the data dictionary
		var data: Dictionary = event.get("data", {})
		var fixed_data: Variant = _sanitize_event_data_recursive(data, int_keys)
		fixed_event["data"] = fixed_data
		result.append(fixed_event)
	return result


## Recursively sanitize event data, converting known int fields
static func _sanitize_event_data_recursive(data: Variant, int_keys: Array) -> Variant:
	if data is Dictionary:
		var fixed := {}
		for key in data:
			var val = data[key]
			if key in int_keys:
				fixed[key] = int(val)
			elif val is Dictionary:
				fixed[key] = _sanitize_event_data_recursive(val, int_keys)
			elif val is Array:
				fixed[key] = _sanitize_event_data_recursive(val, int_keys)
			else:
				fixed[key] = val
		return fixed
	elif data is Array:
		var fixed := []
		for item in data:
			if item is Dictionary:
				fixed.append(_sanitize_event_data_recursive(item, int_keys))
			elif item is float or item is int:
				fixed.append(int(item))
			else:
				fixed.append(item)
		return fixed
	else:
		return data

## Get agent by ID - O(1) lookup
func get_agent(agent_id: int) -> Agent:
	return agent_by_id.get(agent_id, null)

func get_organization(organization_id: int) -> Organization:
	for organization in organizations:
		if organization.id == organization_id:
			return organization
	return null

## Add an agent and update lookup
func add_agent(agent: Agent) -> void:
	agents.append(agent)
	agent_by_id[agent.id] = agent

## Rebuild agent lookup from agents array
func rebuild_agent_lookup() -> void:
	agent_by_id.clear()
	for agent in agents:
		agent_by_id[agent.id] = agent

## Get player agent
func get_player() -> Agent:
	for agent in agents:
		if agent.is_player:
			return agent
	return null

## Calculate average hunger
func get_average_hunger() -> float:
	if agents.is_empty():
		return 0.0
	var total := 0.0
	for agent in agents:
		total += agent.get_hunger()
	return total / agents.size()

## Log an event (ring buffer - oldest events removed when exceeding cap)
const MAX_EVENTS := 500
const MAX_DECISION_TRACES := 1000
const MAX_INTENTS := 1000
const INTENT_PRUNE_AGE_TICKS := 500

func log_event(type: String, data: Dictionary) -> void:
	events.append({
		"tick": tick,
		"type": type,
		"data": data
	})
	# Prune oldest events to prevent unbounded growth
	if events.size() > MAX_EVENTS:
		events.pop_front()

func create_intent(agent_id: int, intent_type: String, data: Dictionary, current_tick: int) -> int:
	var intent := {
		"intent_id": next_intent_id,
		"agent_id": agent_id,
		"type": intent_type,
		"status": "active",
		"created_tick": current_tick,
		"updated_tick": current_tick,
		"data": data.duplicate(true)
	}
	next_intent_id += 1
	intents_by_id[intent["intent_id"]] = intent
	_prune_resolved_intents(current_tick)
	return intent["intent_id"]

func resolve_intent(intent_id: int, status: String, current_tick: int) -> void:
	var intent: Dictionary = intents_by_id.get(intent_id, {})
	if intent.is_empty():
		return
	intent["status"] = status
	intent["updated_tick"] = current_tick
	_prune_resolved_intents(current_tick)

func get_intent(intent_id: int) -> Dictionary:
	return intents_by_id.get(intent_id, {})

func _prune_resolved_intents(current_tick: int) -> void:
	var cutoff := current_tick - INTENT_PRUNE_AGE_TICKS
	var to_remove := []
	for intent_id in intents_by_id:
		var intent: Dictionary = intents_by_id[intent_id]
		if intent.get("status", "active") != "active" and int(intent.get("updated_tick", 0)) <= cutoff:
			to_remove.append(intent_id)
	for intent_id in to_remove:
		intents_by_id.erase(intent_id)

func prune_intents(max_intents: int = MAX_INTENTS) -> void:
	var intents_list := intents_by_id.values()
	if intents_list.size() <= max_intents:
		return

	var active := []
	var resolved := []
	for intent in intents_list:
		if intent.get("status", "active") == "active":
			active.append(intent)
		else:
			_compact_resolved_intent(intent)
			resolved.append(intent)

	if resolved.is_empty():
		return

	var resolved_allowed := maxi(0, max_intents - active.size())
	if resolved.size() <= resolved_allowed:
		return

	resolved.sort_custom(func(a, b): return int(a.get("updated_tick", 0)) < int(b.get("updated_tick", 0)))
	var keep_from := maxi(0, resolved.size() - resolved_allowed)
	var kept_resolved := resolved.slice(keep_from, resolved.size())
	var new_intents := []
	new_intents.append_array(active)
	new_intents.append_array(kept_resolved)
	intents_by_id = {}
	for intent in new_intents:
		var intent_id := int(intent.get("intent_id", 0))
		if intent_id > 0:
			intents_by_id[intent_id] = intent

func _compact_resolved_intent(intent: Dictionary) -> void:
	if intent.get("status", "active") == "active":
		return
	var intent_id: int = int(intent.get("intent_id", 0))
	var status: String = str(intent.get("status", "resolved"))
	var updated_tick: int = int(intent.get("updated_tick", 0))
	intent.clear()
	intent["intent_id"] = intent_id
	intent["status"] = status
	intent["updated_tick"] = updated_tick

func log_decision_trace(agent_id: int, intent_id: int, intent_type: String,
		activity_id: int, activity_type: String, action_type: String) -> void:
	decision_traces.append({
		"tick": tick,
		"agent_id": agent_id,
		"intent_id": intent_id,
		"intent_type": intent_type,
		"activity_id": activity_id,
		"activity_type": activity_type,
		"action_type": action_type
	})
	if decision_traces.size() > MAX_DECISION_TRACES:
		decision_traces.pop_front()
