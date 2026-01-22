## Tuning - Typed config access layer with validation
## Provides typed getters and fail-fast validation for required tuning parameters
class_name TuningConfig
extends RefCounted

## Schema entry structure:
## {
##   "type": "int" | "float" | "bool" | "string",
##   "required": bool,
##   "default": value (optional, for non-required keys),
##   "min": number (optional),
##   "max": number (optional),
##   "description": String (optional)
## }

var _data: Dictionary = {}
var _schema: Dictionary = {}
var _validation_errors: Array[String] = []

func _init() -> void:
	_init_schema()

## Initialize the schema with all known tuning parameters
func _init_schema() -> void:
	# World parameters
	_add_schema("world_w", "int", true, 96, 16, 512, "World width in tiles")
	_add_schema("world_h", "int", true, 96, 16, 512, "World height in tiles")
	_add_schema("ticks_per_day", "int", true, 24, 1, 500, "Simulation ticks per day")
	
	# Hunger/needs
	_add_schema("hunger_drain_per_tick", "float", true, 0.7, 0.0, 10.0, "Hunger drain per tick")
	_add_schema("eat_threshold", "float", true, 50, 0, 100, "Hunger level below which agents seek food")
	_add_schema("urgent_hunger_threshold", "float", true, 25, 0, 100, "Hunger level for urgent food needs")
	_add_schema("emergency_hunger_threshold", "float", false, 15, 0, 100, "Hunger level for emergency eating")
	_add_schema("emergency_food_hunger_threshold", "float", false, 40, 0, 100, "Hunger level to trigger emergency food acquisition")
	_add_schema("berry_nutrition", "float", true, 20, 0, 100, "Hunger restored by eating berries")
	_add_schema("meal_nutrition", "float", true, 40, 0, 100, "Hunger restored by eating cooked meal")
	
	# Stamina
	_add_schema("stamina_max", "float", true, 100, 1, 1000, "Maximum stamina")
	_add_schema("stamina_drain_gather", "float", true, 2.0, 0, 50, "Stamina cost for gathering")
	_add_schema("stamina_drain_move", "float", true, 0.5, 0, 10, "Stamina cost for movement")
	_add_schema("stamina_drain_craft", "float", true, 3.0, 0, 50, "Stamina cost for crafting")
	_add_schema("stamina_drain_claim", "float", true, 2.0, 0, 50, "Stamina cost for claiming")
	_add_schema("stamina_drain_build", "float", true, 5.0, 0, 50, "Stamina cost for building")
	_add_schema("stamina_recover_rest", "float", true, 5.0, 0, 50, "Stamina recovered when resting")
	_add_schema("stamina_recover_sleep", "float", true, 10.0, 0, 50, "Stamina recovered when sleeping")
	_add_schema("stamina_low_threshold", "float", true, 20.0, 0, 100, "Stamina below which agent is exhausted")
	_add_schema("stamina_exhausted_yield_penalty", "float", true, 0.5, 0, 1, "Yield penalty when exhausted")
	_add_schema("stamina_rest_optimization_threshold", "float", false, 50, 0, 100, "Stamina level for optional rest")
	
	# Starting conditions
	_add_schema("starting_money", "int", true, 200, 0, 10000, "Starting money for agents")
	_add_schema("starting_berries", "int", true, 4, 0, 100, "Starting berries for agents")
	_add_schema("initial_hunger", "float", false, 100.0, 0, 100, "Initial hunger level")
	_add_schema("initial_stamina", "float", false, 100.0, 0, 100, "Initial stamina level")
	_add_schema("initial_skill_level", "float", false, 1.0, 0, 10, "Initial skill level")
	_add_schema("npc_count", "int", false, 10, 0, 1000, "Number of NPCs to spawn")
	_add_schema("starting_agents_claim_count", "int", false, 5, 0, 100, "Number of agents to give starting claims")
	
	# Resource nodes
	_add_schema("berry_nodes_count", "int", true, 50, 0, 1000, "Number of berry nodes")
	_add_schema("tree_nodes_count", "int", true, 70, 0, 1000, "Number of tree nodes")
	_add_schema("ore_nodes_count", "int", true, 20, 0, 500, "Number of ore nodes")
	_add_schema("stone_nodes_count", "int", false, 60, 0, 1000, "Number of stone nodes")
	_add_schema("berry_regen_per_day", "int", true, 3, 0, 100, "Berry regeneration per day")
	_add_schema("tree_regen_per_day", "int", true, 2, 0, 100, "Tree regeneration per day")
	_add_schema("berry_max_stock", "int", false, 10, 1, 100, "Max stock for berry nodes")
	_add_schema("tree_max_stock", "int", false, 20, 1, 100, "Max stock for tree nodes")
	_add_schema("ore_max_stock", "int", false, 30, 1, 100, "Max stock for ore nodes")
	_add_schema("stone_max_stock", "int", false, 40, 1, 100, "Max stock for stone nodes")
	_add_schema("berry_start_min", "int", false, 6, 0, 100, "Min starting stock for berry nodes")
	_add_schema("berry_start_max", "int", false, 10, 1, 100, "Max starting stock for berry nodes")
	_add_schema("tree_start_min", "int", false, 10, 0, 100, "Min starting stock for tree nodes")
	_add_schema("tree_start_max", "int", false, 20, 1, 100, "Max starting stock for tree nodes")
	_add_schema("ore_start_min", "int", false, 20, 0, 100, "Min starting stock for ore nodes")
	_add_schema("ore_start_max", "int", false, 30, 1, 100, "Max starting stock for ore nodes")
	_add_schema("stone_start_min", "int", false, 20, 0, 100, "Min starting stock for stone nodes")
	_add_schema("stone_start_max", "int", false, 40, 1, 100, "Max starting stock for stone nodes")
	_add_schema("node_spawn_attempts", "int", false, 100, 1, 1000, "Max attempts to spawn a node")
	
	# Pollution
	_add_schema("pollution_impact", "float", true, 0.5, 0, 2, "Pollution impact on resource regen")
	_add_schema("pollution_decay_per_day", "float", true, 0.05, 0, 1, "Pollution decay rate per day")
	_add_schema("pollution_per_ore", "float", true, 0.01, 0, 1, "Pollution added per ore mined")
	_add_schema("pollution_spread_rate", "float", false, 0.05, 0, 1, "Fraction of pollution spread per day")
	_add_schema("pollution_spread_threshold", "float", false, 0.05, 0, 1, "Minimum pollution before spreading")
	_add_schema("pollution_high_threshold", "float", true, 0.6, 0, 1, "Threshold for high pollution")
	_add_schema("pollution_collapse_threshold", "float", true, 0.8, 0, 1, "Pollution level causing collapse")
	_add_schema("food_yield_pollution_start", "float", true, 0.3, 0, 1, "Pollution level where food yield starts decreasing")
	_add_schema("food_yield_pollution_step", "float", true, 0.1, 0, 1, "Pollution step for food yield reduction")
	_add_schema("hunger_drain_pollution_mult", "float", true, 0.5, 0, 5, "Hunger drain multiplier from pollution")
	_add_schema("flora_growth_attempts_per_day", "int", false, 6, 0, 100, "Flora growth attempts per day")
	_add_schema("flora_growth_chance", "float", false, 0.35, 0, 1, "Chance each flora growth attempt succeeds")
	_add_schema("flora_growth_pollution_max", "float", false, 0.3, 0, 1, "Max pollution for flora growth tiles")
	_add_schema("flora_growth_berry_weight", "float", false, 0.6, 0, 1, "Probability of spawning berries vs trees")
	
	# Market
	_add_schema("market_pos_x", "int", true, 48, 0, 512, "Market X position")
	_add_schema("market_pos_y", "int", true, 48, 0, 512, "Market Y position")
	_add_schema("market_match_interval_ticks", "int", true, 1, 1, 100, "Ticks between market matching")
	_add_schema("order_ttl_ticks", "int", true, 48, 1, 1000, "Order time-to-live in ticks")
	_add_schema("price_ema_alpha", "float", true, 0.2, 0, 1, "EMA alpha for price smoothing")
	_add_schema("bid_scarcity_strength", "float", true, 0.6, 0, 2, "Bid price scarcity sensitivity")
	_add_schema("ask_surplus_strength", "float", true, 0.6, 0, 2, "Ask price surplus sensitivity")
	_add_schema("min_price", "int", true, 1, 1, 10000, "Minimum price")
	_add_schema("max_price", "int", true, 1000, 1, 100000, "Maximum price")
	_add_schema("target_food_buffer", "int", true, 5, 0, 100, "Target food inventory buffer")
	_add_schema("sell_surplus_food_over", "int", true, 6, 0, 100, "Sell berries when over this amount")
	_add_schema("sell_logs_over", "int", true, 3, 0, 100, "Sell logs when over this amount")
	_add_schema("sell_ore_over", "int", true, 2, 0, 100, "Sell ore when over this amount")
	_add_schema("sell_planks_over", "int", true, 5, 0, 100, "Sell planks when over this amount")
	_add_schema("sell_meals_over", "int", true, 3, 0, 100, "Sell meals when over this amount")
	_add_schema("miner_sell_metal_over", "int", false, 3, 0, 100, "Miners sell metal ingots when over this")
	_add_schema("food_scarcity_multiplier", "float", true, 0.4, 0, 2, "Price multiplier during food scarcity")
	_add_schema("berry_scarcity_threshold", "int", true, 100, 0, 10000, "Berry stock below which is scarcity")
	_add_schema("tree_scarcity_threshold", "int", true, 150, 0, 10000, "Tree stock below which is scarcity")
	
	# Workshops
	_add_schema("workshop_start_count", "int", true, 1, 0, 100, "Initial workshops")
	_add_schema("workshop_build_planks", "int", true, 10, 0, 100, "Planks needed to build workshop")
	_add_schema("workshop_build_ticks", "int", true, 80, 0, 1000, "Ticks to build workshop")
	_add_schema("workshop_build_min_wealth", "int", true, 150, 0, 10000, "Min wealth to consider building workshop")
	_add_schema("workshop_nearby_radius", "int", false, 20, 1, 100, "Radius to check for nearby workshops")
	
	# Crafting/roles
	_add_schema("crafter_ratio", "float", true, 0.3, 0, 1, "Ratio of crafters in population")
	_add_schema("crafter_planks_buffer", "int", true, 10, 0, 100, "Target planks buffer for crafters")
	_add_schema("crafter_meal_buffer", "int", true, 6, 0, 100, "Target meal buffer for crafters")
	_add_schema("craft_profit_margin", "float", true, 1.15, 0, 5, "Min profit margin to craft")
	_add_schema("axe_tree_bonus", "int", true, 2, 1, 10, "Extra yield when using axe")
	_add_schema("pickaxe_ore_bonus", "int", true, 2, 1, 10, "Extra yield when using pickaxe")
	_add_schema("tool_acquisition_priority_threshold", "int", true, 60, 0, 100, "Hunger level above which to prioritize tools")
	_add_schema("miner_pollution_tolerance", "float", true, 0.8, 0, 1, "Pollution tolerance for miners")
	_add_schema("trader_min_inventory_value", "int", true, 100, 0, 10000, "Min inventory value for traders to buy")
	_add_schema("role_switch_interval_ticks", "int", true, 100, 1, 1000, "Ticks between role switch evaluation")
	
	# Skills
	_add_schema("skill_xp_per_action", "float", true, 0.1, 0, 10, "XP gained per action")
	_add_schema("skill_level_xp_requirement", "float", true, 100, 1, 10000, "XP needed to level up")
	
	# Contracts
	_add_schema("daily_contract_post_chance", "float", true, 0.15, 0, 1, "Daily chance to post contract")
	_add_schema("contract_deadline_days", "int", true, 2, 1, 100, "Contract deadline in days")
	_add_schema("contract_payout_multiplier", "float", true, 1.2, 0, 10, "Contract payout multiplier")
	_add_schema("contract_accept_min_profit", "int", true, 5, 0, 1000, "Minimum profit to accept contract")
	_add_schema("max_active_contracts_per_agent", "int", true, 1, 0, 10, "Max active contracts per agent")
	_add_schema("contract_food_focus_multiplier", "float", true, 2.0, 0, 10, "Food payout multiplier during scarcity")
	_add_schema("contract_payout_multiplier_cap", "float", true, 2.5, 0, 10, "Max contract payout multiplier")
	
	# Claims / enforcement
	_add_schema("claim_cost_coins", "int", true, 20, 0, 1000, "Cost to claim a tile")
	_add_schema("claim_ticks", "int", true, 40, 1, 1000, "Ticks to complete claim")
	_add_schema("claim_search_radius", "int", true, 8, 1, 100, "Radius to search for claim targets")
	_add_schema("claim_min_nearby_resources", "int", true, 3, 0, 100, "Min resources to justify claim")
	_add_schema("tile_claim_search_radius", "int", false, 3, 1, 20, "Radius for tile value calculation")
	_add_schema("tile_value_berry_weight", "float", false, 2.0, 0, 10, "Value weight for berries")
	_add_schema("tile_value_tree_weight", "float", false, 1.0, 0, 10, "Value weight for trees")
	_add_schema("tile_value_ore_weight", "float", false, 1.5, 0, 10, "Value weight for ore")
	_add_schema("tile_value_pollution_penalty", "float", false, 0.5, 0, 2, "Value penalty per pollution")
	_add_schema("detect_chance", "float", true, 0.8, 0, 1, "Chance to detect violation")
	_add_schema("fine_base", "int", true, 10, 0, 1000, "Base fine amount")
	_add_schema("min_fine", "int", true, 5, 0, 1000, "Minimum fine amount")
	_add_schema("max_fine", "int", true, 50, 0, 10000, "Maximum fine amount")
	_add_schema("fine_step", "int", true, 5, 0, 100, "Fine adjustment step")
	_add_schema("market_ban_days_on_repeat", "int", true, 2, 0, 100, "Days of market ban for repeat offenders")
	_add_schema("repeat_threshold", "int", true, 3, 1, 100, "Violations before repeat offender status")
	_add_schema("violation_window_days", "int", true, 2, 1, 100, "Window to count violations")
	_add_schema("harvest_permit_required_default", "bool", true, true, 0, 0, "Default harvest permit requirement")
	_add_schema("build_permit_required_default", "bool", true, true, 0, 0, "Default build permit requirement")
	_add_schema("sales_tax_rate_default", "int", true, 5, 0, 100, "Default sales tax rate")
	_add_schema("sales_tax_rate_min", "int", false, 0, 0, 100, "Minimum sales tax rate")
	_add_schema("sales_tax_rate_max", "int", false, 20, 0, 100, "Maximum sales tax rate")

	# Organizations
	_add_schema("organization_starting_treasury", "int", false, 200, 0, 100000, "Initial organization treasury seed")
	_add_schema("organization_claims_per_day", "int", false, 4, 0, 100, "Claims per day for organization expansion")
	_add_schema("organization_claim_radius", "int", false, 12, 1, 200, "Max radius for organization claims")
	_add_schema("organization_stockpile_members_per", "int", false, 6, 1, 100, "Members per stockpile target")
	_add_schema("organization_workshop_members_per", "int", false, 10, 1, 200, "Members per workshop target")
	_add_schema("organization_shelter_members_per", "int", false, 4, 1, 100, "Members per shelter target")
	_add_schema("stockpile_capacity", "int", false, 150, 1, 10000, "Default stockpile capacity")
	_add_schema("shelter_capacity", "int", false, 4, 1, 1000, "Default shelter capacity")
	
	# Factions
	_add_schema("faction_found_min_money", "int", true, 80, 0, 10000, "Min money to found faction")
	_add_schema("faction_found_min_grievance", "float", true, 0.5, 0, 1, "Min grievance to found faction")
	_add_schema("faction_found_daily_chance", "float", true, 0.05, 0, 1, "Daily chance to spontaneously found")
	_add_schema("faction_found_claim_radius", "int", true, 10, 1, 100, "Claim search radius for founding")
	_add_schema("faction_found_treasury_seed", "int", true, 50, 0, 10000, "Initial treasury contribution")
	_add_schema("faction_found_grievance_reduction", "float", false, 0.3, 0, 1, "Grievance reduction on founding")
	_add_schema("faction_join_grievance_reduction", "float", false, 0.2, 0, 1, "Grievance reduction on joining")
	_add_schema("faction_join_claims_score", "float", false, 0.4, 0, 2, "Score bonus for faction claims")
	_add_schema("faction_join_scarcity_bonus", "float", false, 0.3, 0, 2, "Score bonus during scarcity")
	_add_schema("faction_join_member_bonus", "float", false, 0.02, 0, 1, "Score bonus per member")
	_add_schema("join_search_radius", "int", true, 15, 1, 200, "Radius to search for factions to join")
	_add_schema("join_min_score", "float", true, 0.3, 0, 2, "Min score to join faction")
	_add_schema("min_claims_for_join_benefit", "int", true, 3, 0, 100, "Min claims for join benefit")
	_add_schema("faction_claims_per_day", "int", true, 2, 0, 100, "Claims faction can make per day")
	_add_schema("faction_claim_cost", "int", true, 15, 0, 1000, "Cost per faction claim")
	_add_schema("faction_eval_interval_ticks", "int", true, 50, 1, 1000, "Ticks between faction evaluation")
	
	# Governance
	_add_schema("proposal_grievance_threshold", "float", true, 0.3, 0, 1, "Grievance to submit proposal")
	_add_schema("max_proposals_per_day_per_faction", "int", true, 1, 0, 100, "Max proposals per day")
	_add_schema("tax_step", "int", true, 2, 0, 50, "Tax rate change step")
	_add_schema("vote_grievance_threshold", "float", false, 0.2, 0, 1, "Grievance to vote for fine reduction")
	_add_schema("vote_eco_concern_threshold", "float", false, 0.6, 0, 1, "Eco concern to vote for stricter laws")
	_add_schema("grievance_decay_daily", "float", true, 0.1, 0, 1, "Daily grievance decay")
	_add_schema("grievance_fine_increase", "float", true, 0.15, 0, 1, "Grievance increase from fine")
	_add_schema("grievance_blocked_increase", "float", true, 0.1, 0, 1, "Grievance increase from blocked action")
	
	# Trade policy
	_add_schema("default_relation_policy", "string", true, "open", 0, 0, "Default trade relation policy")
	_add_schema("default_relation_tariff_rate", "int", true, 0, 0, 100, "Default tariff rate")
	_add_schema("default_factionless_policy", "string", true, "tariff", 0, 0, "Policy toward factionless agents")
	_add_schema("default_factionless_tariff_rate", "int", true, 5, 0, 100, "Tariff rate for factionless agents")
	_add_schema("tariff_rate_min", "int", true, 0, 0, 100, "Minimum tariff rate")
	_add_schema("tariff_rate_max", "int", true, 30, 0, 100, "Maximum tariff rate")
	_add_schema("policy_change_tariff_step", "int", true, 5, 0, 50, "Tariff step for policy changes")
	
	# Collapse detection
	_add_schema("collapse_days_window", "int", true, 5, 1, 100, "Days to check for collapse")
	_add_schema("starvation_collapse_ratio", "float", true, 0.2, 0, 1, "Death ratio indicating collapse")
	_add_schema("inflation_threshold", "float", true, 50.0, 0, 1000, "Price threshold for inflation collapse")
	
	# Exploration / memory
	_add_schema("exploration_trigger_radius", "int", true, 20, 1, 200, "Radius to check for resources before exploring")
	_add_schema("exploration_step_distance", "int", true, 5, 1, 50, "Distance per exploration step")
	_add_schema("memory_capacity", "int", false, 20, 1, 1000, "Max remembered resource locations")
	_add_schema("social_trust_initial", "float", false, 0.5, 0, 1, "Initial trust for unknown agents")
	_add_schema("social_trust_trade_increase", "float", false, 0.05, 0, 1, "Trust increase per trade")
	
	# Gather radii
	_add_schema("gather_radius_normal", "int", false, 15, 1, 200, "Normal gather search radius")
	_add_schema("gather_radius_scarce", "int", false, 50, 1, 500, "Gather search radius during scarcity")
	_add_schema("tree_radius_normal", "int", false, 15, 1, 200, "Normal tree search radius")
	_add_schema("tree_radius_scarce", "int", false, 40, 1, 500, "Tree search radius during scarcity")
	
	# Metrics
	_add_schema("metrics_enabled", "bool", true, true, 0, 0, "Whether to collect metrics")
	_add_schema("job_board_enabled", "bool", false, true, 0, 0, "Enable job board activity posting")
	_add_schema("job_board_daily_post_limit", "int", false, 20, 0, 10000, "Daily max activities to post")
	_add_schema("job_board_gather_node_post_limit", "int", false, 20, 0, 10000, "Daily gather-node activity cap")
	_add_schema("job_board_contract_post_limit", "int", false, 10, 0, 10000, "Daily contract activity cap")
	_add_schema("job_board_project_post_limit", "int", false, 10, 0, 10000, "Project activity cap")
	_add_schema("job_board_project_item_post_limit", "int", false, 20, 0, 10000, "Project item activity cap")
	_add_schema("job_board_build_site_post_limit", "int", false, 10, 0, 10000, "Build-site activity cap")
	_add_schema("job_board_haul_post_limit", "int", false, 10, 0, 10000, "Haul activity cap")
	_add_schema("job_board_farm_task_post_limit", "int", false, 10, 0, 10000, "Farm task activity cap")
	_add_schema("job_board_max_inactive", "int", false, 200, 0, 10000, "Max inactive activities to retain")
	_add_schema("task_project_system_enabled", "bool", false, true, 0, 0, "Enable task/project system phase")
	_add_schema("project_max_stale_ticks", "int", false, 240, 0, 100000, "Max ticks before abandoning projects")

## Add a schema entry
func _add_schema(key: String, type: String, required: bool, default_value, 
				 min_val: float = 0.0, max_val: float = 0.0, description: String = "") -> void:
	_schema[key] = {
		"type": type,
		"required": required,
		"default": default_value,
		"min": min_val,
		"max": max_val,
		"description": description
	}

## Load config from dictionary (typically from JSON)
func load_from_dict(data: Dictionary) -> void:
	_data = data.duplicate(true)

## Validate the loaded config against schema
## Returns array of error messages (empty if valid)
func validate() -> Array[String]:
	_validation_errors.clear()
	
	for key in _schema:
		var schema_entry: Dictionary = _schema[key]
		var required: bool = schema_entry["required"]
		
		if not _data.has(key):
			if required:
				_validation_errors.append("Missing required tuning key: '%s'" % key)
			continue
		
		var value = _data[key]
		var expected_type: String = schema_entry["type"]
		
		# Type validation
		match expected_type:
			"int":
				if not (value is int or value is float):
					_validation_errors.append("Key '%s' expected int, got %s" % [key, typeof(value)])
				elif schema_entry["min"] != schema_entry["max"]:
					var int_val: int = int(value)
					if int_val < schema_entry["min"] or int_val > schema_entry["max"]:
						_validation_errors.append("Key '%s' value %d out of range [%d, %d]" % [key, int_val, int(schema_entry["min"]), int(schema_entry["max"])])
			"float":
				if not (value is int or value is float):
					_validation_errors.append("Key '%s' expected float, got %s" % [key, typeof(value)])
				elif schema_entry["min"] != schema_entry["max"]:
					var float_val: float = float(value)
					if float_val < schema_entry["min"] or float_val > schema_entry["max"]:
						_validation_errors.append("Key '%s' value %f out of range [%f, %f]" % [key, float_val, schema_entry["min"], schema_entry["max"]])
			"bool":
				if not value is bool:
					_validation_errors.append("Key '%s' expected bool, got %s" % [key, typeof(value)])
			"string":
				if not value is String:
					_validation_errors.append("Key '%s' expected string, got %s" % [key, typeof(value)])
	
	return _validation_errors

## Get an integer value (fail-fast if required and missing)
func get_int(key: String) -> int:
	if _data.has(key):
		return int(_data[key])
	if _schema.has(key):
		if _schema[key]["required"]:
			push_error("TuningConfig: Missing required key '%s'" % key)
			assert(false, "Missing required tuning key: %s" % key)
		return int(_schema[key]["default"])
	push_error("TuningConfig: Unknown key '%s'" % key)
	return 0

## Get a float value (fail-fast if required and missing)
func get_float(key: String) -> float:
	if _data.has(key):
		return float(_data[key])
	if _schema.has(key):
		if _schema[key]["required"]:
			push_error("TuningConfig: Missing required key '%s'" % key)
			assert(false, "Missing required tuning key: %s" % key)
		return float(_schema[key]["default"])
	push_error("TuningConfig: Unknown key '%s'" % key)
	return 0.0

## Get a boolean value (fail-fast if required and missing)
func get_bool(key: String) -> bool:
	if _data.has(key):
		return bool(_data[key])
	if _schema.has(key):
		if _schema[key]["required"]:
			push_error("TuningConfig: Missing required key '%s'" % key)
			assert(false, "Missing required tuning key: %s" % key)
		return bool(_schema[key]["default"])
	push_error("TuningConfig: Unknown key '%s'" % key)
	return false

## Get a string value (fail-fast if required and missing)
func get_string(key: String) -> String:
	if _data.has(key):
		return str(_data[key])
	if _schema.has(key):
		if _schema[key]["required"]:
			push_error("TuningConfig: Missing required key '%s'" % key)
			assert(false, "Missing required tuning key: %s" % key)
		return str(_schema[key]["default"])
	push_error("TuningConfig: Unknown key '%s'" % key)
	return ""

## Get raw value with Dictionary-style fallback (for legacy compatibility)
func get_value(key: String, default_value = null):
	if _data.has(key):
		return _data[key]
	if _schema.has(key):
		return _schema[key]["default"]
	return default_value

## Check if key exists
func has_key(key: String) -> bool:
	return _data.has(key) or _schema.has(key)

## Get all schema entries (for documentation/tests)
func get_schema() -> Dictionary:
	return _schema.duplicate(true)

## Get full data set with defaults applied for missing keys
func get_data_with_defaults() -> Dictionary:
	var merged := _data.duplicate(true)
	for key in _schema:
		var schema_entry: Dictionary = _schema[key]
		if not merged.has(key):
			merged[key] = schema_entry["default"]
	return merged

## Get all loaded data
func get_data() -> Dictionary:
	return _data.duplicate(true)

## Create and load from file path
static func load_from_file(path: String) -> TuningConfig:
	var config := TuningConfig.new()
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		push_error("TuningConfig: Could not open file '%s'" % path)
		return config
	
	var json := JSON.new()
	var error := json.parse(file.get_as_text())
	file.close()
	
	if error != OK:
		push_error("TuningConfig: JSON parse error in '%s': %s" % [path, json.get_error_message()])
		return config
	
	config.load_from_dict(json.data)
	return config
