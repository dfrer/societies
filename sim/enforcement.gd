## Enforcement - handles violations, fines, confiscation, and market bans
class_name Enforcement
extends RefCounted

## Violation types
const VIOLATION_ILLEGAL_HARVEST := "illegal_harvest"
const VIOLATION_ILLEGAL_BUILD := "illegal_build"
const VIOLATION_MARKET_BAN := "market_ban"

## Reason codes for structured enforcement results
## See EnforcementResult class for the canonical registry of all reason codes.
## These constants are kept here for backward compatibility with existing code.
const REASON_OK := "ok"
const REASON_NO_PERMIT := "no_permit"
const REASON_DETECTED := "detected"
const REASON_MARKET_BANNED := "market_banned"
const REASON_TRESPASSING := "trespassing"

## Severity multipliers
const SEVERITY := {
	"illegal_harvest": 1,
	"illegal_build": 2,
	"market_ban": 1
}

## Confiscation order (deterministic)
const CONFISCATION_ORDER := ["Ore", "MetalIngot", "Pickaxe", "Axe", "Planks", "Logs", "CookedMeal", "Berries"]

## Maximum violations to keep in log (prevents unbounded growth)
const MAX_VIOLATIONS_LOG := 200

var violations_log: Array = []  # Array of violation records
var fines_collected: int = 0
var violations_detected: int = 0

func _init() -> void:
	pass

## Create structured enforcement result
static func create_result(allowed: bool, reason_code: String, details: Dictionary = {}) -> Dictionary:
	return {
		"allowed": allowed,
		"reason_code": reason_code,
		"details": details
	}

## Check if action is detected (deterministic RNG)
func is_detected(rng: RNG, detect_chance: float) -> bool:
	return rng.randf() < detect_chance

## Record a violation
func record_violation(agent_id: int, violation_type: String, current_tick: int, state: SimState) -> void:
	violations_log.append({
		"agent_id": agent_id,
		"type": violation_type,
		"tick": current_tick
	})
	violations_detected += 1

	# Prune old violations to prevent unbounded growth
	if violations_log.size() > MAX_VIOLATIONS_LOG:
		violations_log = violations_log.slice(-MAX_VIOLATIONS_LOG)

	state.log_event("violation_detected", {
		"agent_id": agent_id,
		"violation_type": violation_type,
		"tick": current_tick
	})

## Count recent violations for agent within window
func count_recent_violations(agent_id: int, current_tick: int, window_ticks: int) -> int:
	var count := 0
	var cutoff := current_tick - window_ticks
	for v in violations_log:
		if v["agent_id"] == agent_id and v["tick"] >= cutoff:
			count += 1
	return count

## Apply fine to agent, return amount actually paid
## Routes fine to the jurisdiction owner (agent, faction, or sink)
func apply_fine_with_routing(agent: Agent, fine_amount: int, items: Dictionary,
							  tile_owner: int, state: SimState) -> int:
	var paid := 0
	
	# TODO: Make fine collection dynamic (policy-based). For now fines cannot touch locked_money/escrow.
	var available_cash := agent.get_available_money()
	
	# Try to pay from money first
	if available_cash >= fine_amount:
		agent.money -= fine_amount
		paid = fine_amount
	else:
		# Pay what we can
		paid = available_cash
		var remaining := fine_amount - paid
		agent.money -= paid

		
		# Confiscate items
		for item_name in CONFISCATION_ORDER:
			if remaining <= 0:
				break
			var item_data: Dictionary = items.get(item_name, {})
			var base_value: int = item_data.get("base_value", 10)
			var available := agent.get_available_item(item_name)
			
			if available > 0:
				var qty_needed := int(ceil(float(remaining) / base_value))
				var qty_to_take := mini(qty_needed, available)
				agent.remove_item(item_name, qty_to_take)
				var value_taken := qty_to_take * base_value
				remaining -= value_taken
				paid += value_taken
	
	fines_collected += paid
	
	# Route fine to owner
	if tile_owner == 0:
		# Unclaimed land - goes to world sink
		state.world_fines_sink += paid
	elif World.is_faction_owner(tile_owner):
		# Faction owner - goes to faction treasury
		var faction_id := World.faction_id_from_owner(tile_owner)
		for faction in state.factions:
			if faction.id == faction_id:
				faction.treasury += paid
				break
	else:
		# Agent owner - goes to agent money
		var owner := state.get_agent(tile_owner)
		if owner != null:
			owner.money += paid
		else:
			state.world_fines_sink += paid
	
	return paid

## Increase agent grievance due to fine
func increase_grievance(agent: Agent, tuning: Dictionary) -> void:
	var increase: float = tuning.get("grievance_fine_increase", 0.15)
	agent.grievance = minf(1.0, agent.grievance + increase)

## Check and apply market ban if needed
func check_market_ban(agent: Agent, laws: Laws, current_tick: int, ticks_per_day: int) -> void:
	var recent := count_recent_violations(agent.id, current_tick, laws.violation_window_ticks)
	if recent >= laws.repeat_threshold:
		var ban_ticks := laws.market_ban_days_on_repeat * ticks_per_day
		agent.market_ban_until_tick = current_tick + ban_ticks

## Process illegal harvest attempt - uses state for treasury routing
## Returns structured result: {allowed: bool, reason_code: String, details: Dictionary}
func process_illegal_harvest_with_state(agent: Agent, tile_owner: int, laws: Laws, 
					  rng: RNG, tuning: Dictionary, items: Dictionary,
					  current_tick: int, state: SimState) -> Dictionary:
	# Check if permit required and agent doesn't have it
	if laws.has_harvest_permit(agent.id, agent.faction_id, tile_owner):
		return create_result(true, REASON_OK, {"permit_type": "harvest"})
	
	# Agent is blocked - increase grievance for being blocked
	var blocked_increase: float = tuning.get("grievance_blocked_increase", 0.1)
	agent.grievance = minf(1.0, agent.grievance + blocked_increase)
	
	# No permit, check if detected
	var detect_chance: float = tuning.get("detect_chance", 0.8)
	if is_detected(rng, detect_chance):
		record_violation(agent.id, VIOLATION_ILLEGAL_HARVEST, current_tick, state)
		# TODO: fines will become dynamic later - keep exact behavior
		var fine := laws.fine_base * SEVERITY[VIOLATION_ILLEGAL_HARVEST]
		apply_fine_with_routing(agent, fine, items, tile_owner, state)
		increase_grievance(agent, tuning)
		var ticks_per_day: int = tuning.get("ticks_per_day", 24)
		check_market_ban(agent, laws, current_tick, ticks_per_day)
		return create_result(false, REASON_DETECTED, {
			"violation_type": VIOLATION_ILLEGAL_HARVEST,
			"fine_applied": fine,
			"owner_id": tile_owner
		})
	
	# Not detected - allowed but no permit (sneaky)
	return create_result(true, REASON_NO_PERMIT, {"owner_id": tile_owner, "undetected": true})

## Process illegal build attempt - uses state for treasury routing
## Returns structured result: {allowed: bool, reason_code: String, details: Dictionary}
func process_illegal_build_with_state(agent: Agent, tile_owner: int, laws: Laws,
				   rng: RNG, tuning: Dictionary, items: Dictionary,
				   current_tick: int, state: SimState) -> Dictionary:
	if laws.has_build_permit(agent.id, agent.faction_id, tile_owner):
		return create_result(true, REASON_OK, {"permit_type": "build"})
	
	# Agent is blocked - increase grievance
	var blocked_increase: float = tuning.get("grievance_blocked_increase", 0.1)
	agent.grievance = minf(1.0, agent.grievance + blocked_increase)
	
	var detect_chance: float = tuning.get("detect_chance", 0.8)
	if is_detected(rng, detect_chance):
		record_violation(agent.id, VIOLATION_ILLEGAL_BUILD, current_tick, state)
		# TODO: fines will become dynamic later - keep exact behavior
		var fine := laws.fine_base * SEVERITY[VIOLATION_ILLEGAL_BUILD]
		apply_fine_with_routing(agent, fine, items, tile_owner, state)
		increase_grievance(agent, tuning)
		var ticks_per_day: int = tuning.get("ticks_per_day", 24)
		check_market_ban(agent, laws, current_tick, ticks_per_day)
		return create_result(false, REASON_DETECTED, {
			"violation_type": VIOLATION_ILLEGAL_BUILD,
			"fine_applied": fine,
			"owner_id": tile_owner
		})
	
	# Not detected - allowed but no permit (sneaky)
	return create_result(true, REASON_NO_PERMIT, {"owner_id": tile_owner, "undetected": true})

## Check if agent is banned from market (legacy bool version)
func is_market_banned(agent: Agent, current_tick: int) -> bool:
	return agent.market_ban_until_tick > current_tick

## Check if agent is banned from market - structured result version
func check_market_ban_result(agent: Agent, current_tick: int) -> Dictionary:
	if agent.market_ban_until_tick > current_tick:
		return create_result(false, REASON_MARKET_BANNED, {
			"ban_until_tick": agent.market_ban_until_tick,
			"ticks_remaining": agent.market_ban_until_tick - current_tick
		})
	return create_result(true, REASON_OK, {})

## Serialize to dictionary
func to_dict() -> Dictionary:
	return {
		"violations_log": violations_log.duplicate(true),
		"fines_collected": fines_collected,
		"violations_detected": violations_detected
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> Enforcement:
	var enforcement := Enforcement.new()
	# Convert violations_log entries with proper types
	enforcement.violations_log = []
	for v in d.get("violations_log", []):
		var fixed_v := {}
		fixed_v["agent_id"] = int(v.get("agent_id", 0))
		fixed_v["type"] = v.get("type", "")
		fixed_v["tick"] = int(v.get("tick", 0))
		enforcement.violations_log.append(fixed_v)
	enforcement.fines_collected = int(d.get("fines_collected", 0))
	enforcement.violations_detected = int(d.get("violations_detected", 0))
	return enforcement
