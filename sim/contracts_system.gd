## ContractsSystem - manages contract posting, acceptance, and completion
class_name ContractsSystem
extends RefCounted

## Maximum number of inactive contracts to keep in history
const MAX_INACTIVE_CONTRACTS := 100

var next_contract_id: int = 1
var contracts: Array = []  # Array of Contract
var stats: Dictionary = {
	"posted": 0,
	"accepted": 0,
	"completed": 0,
	"expired": 0,
	"failed": 0
}

func _init() -> void:
	pass

## Get total escrow held across all contracts
func get_total_escrow() -> int:
	var total := 0
	for contract in contracts:
		total += contract.escrow
	return total

## Get contract by ID
func get_contract(contract_id: int) -> Contract:
	for contract in contracts:
		if contract.id == contract_id:
			return contract
	return null

## Get all posted contracts available for acceptance
func get_available_contracts() -> Array:
	var available := []
	for contract in contracts:
		if contract.is_available():
			available.append(contract)
	return available

## Get active contracts for an agent (as worker)
func get_agent_active_contract(agent_id: int) -> Contract:
	for contract in contracts:
		if contract.worker_id == agent_id and contract.status == Contract.STATUS_ACCEPTED:
			return contract
	return null

## Post a new contract with escrow
func post_contract(issuer: Agent, item: String, qty: int, payout: int, 
				   deadline_tick: int, delivery_x: int, delivery_y: int, 
				   current_tick: int, state: SimState) -> Contract:
	# Check if issuer can afford
	if issuer.get_available_money() < payout:
		return null
	
	# Deduct and hold in escrow
	issuer.lock_money(payout)
	
	var contract := Contract.new()
	contract.id = next_contract_id
	next_contract_id += 1
	contract.status = Contract.STATUS_POSTED
	contract.issuer_type = "agent"
	contract.issuer_id = issuer.id
	contract.item = item
	contract.qty = qty
	contract.payout = payout
	contract.escrow = payout
	contract.created_tick = current_tick
	contract.deadline_tick = deadline_tick
	contract.delivery_pos_x = delivery_x
	contract.delivery_pos_y = delivery_y
	
	contracts.append(contract)
	stats["posted"] += 1
	
	state.log_event("contract_posted", {
		"contract_id": contract.id,
		"issuer_id": issuer.id,
		"item": item,
		"qty": qty,
		"payout": payout,
		"deadline": deadline_tick
	})
	
	return contract

## Post a procurement contract funded by an organization treasury
func post_org_contract(organization: Organization, item: String, qty: int, payout: int,
					   deadline_tick: int, delivery_x: int, delivery_y: int,
					   current_tick: int, state: SimState) -> Contract:
	if organization == null:
		return null
	if organization.treasury < payout:
		return null
	organization.treasury -= payout

	var contract := Contract.new()
	contract.id = next_contract_id
	next_contract_id += 1
	contract.status = Contract.STATUS_POSTED
	contract.issuer_type = "organization"
	contract.issuer_id = organization.id
	contract.item = item
	contract.qty = qty
	contract.payout = payout
	contract.escrow = payout
	contract.created_tick = current_tick
	contract.deadline_tick = deadline_tick
	contract.delivery_pos_x = delivery_x
	contract.delivery_pos_y = delivery_y

	contracts.append(contract)
	stats["posted"] += 1

	state.log_event("contract_posted", {
		"contract_id": contract.id,
		"issuer_id": organization.id,
		"issuer_type": "organization",
		"item": item,
		"qty": qty,
		"payout": payout,
		"deadline": deadline_tick
	})

	return contract

## Accept a contract
func accept_contract(contract_id: int, agent: Agent, current_tick: int, state: SimState) -> bool:
	var contract := get_contract(contract_id)
	if contract == null or not contract.is_available():
		return false
	
	# Check agent doesn't already have an active contract
	if get_agent_active_contract(agent.id) != null:
		return false
	
	contract.accept(agent.id, current_tick)
	agent.active_contract_id = contract.id
	stats["accepted"] += 1
	
	state.log_event("contract_accepted", {
		"contract_id": contract.id,
		"agent_id": agent.id,
		"issuer_id": contract.issuer_id
	})
	
	return true

## Complete delivery for a contract
func complete_delivery(contract_id: int, agent: Agent, state: SimState) -> bool:
	var contract := get_contract(contract_id)
	if contract == null:
		return false
	if contract.worker_id != agent.id:
		return false
	if contract.status != Contract.STATUS_ACCEPTED:
		return false
	
	# Check agent has items
	if agent.get_available_item(contract.item) < contract.qty:
		return false
	
	# Remove items from agent
	agent.remove_item(contract.item, contract.qty)
	contract.delivered_qty = contract.qty
	
	# Complete and pay
	var payout := contract.complete()
	agent.money += payout
	
	# Clear worker's active contract
	agent.active_contract_id = -1
	
	# Transfer items to issuer and release escrow
	if contract.issuer_type == "organization":
		var org := state.get_organization(contract.issuer_id)
		if org:
			var stockpile := _find_org_delivery_stockpile(org, state, contract.delivery_pos_x, contract.delivery_pos_y)
			if stockpile != null:
				stockpile.add_item(contract.item, contract.qty)
	else:
		var issuer := state.get_agent(contract.issuer_id)
		if issuer:
			issuer.add_item(contract.item, contract.qty)
			# contract.complete() returns the escrow amount, so release it back to issuer
			issuer.release_locked_money(payout)
	
	stats["completed"] += 1
	
	state.log_event("contract_completed", {
		"contract_id": contract.id,
		"agent_id": agent.id,
		"payout": payout,
		"item": contract.item,
		"qty": contract.qty
	})
	
	return true

## Process contract expiration
func process_expirations(current_tick: int, agents: Array, state: SimState) -> void:
	var agent_map := {}
	for agent in agents:
		agent_map[agent.id] = agent

	for contract in contracts:
		if not contract.is_active():
			continue
		if contract.is_past_deadline(current_tick):
			# Unaccepted contracts expire, accepted contracts fail
			var is_expired: bool = (contract.status == Contract.STATUS_POSTED)
			var refund: int = contract.fail_or_expire(is_expired)
			if refund > 0:
				if contract.issuer_type == "organization":
					var org := state.get_organization(contract.issuer_id)
					if org:
						org.treasury += refund
				else:
					var issuer: Agent = agent_map.get(contract.issuer_id)
					if issuer:
						issuer.release_locked_money(refund)

			# Clear worker's active contract if there was one
			if contract.worker_id > 0:
				var worker: Agent = agent_map.get(contract.worker_id)
				if worker:
					worker.active_contract_id = -1

			var event_type := "contract_expired" if contract.status == Contract.STATUS_EXPIRED else "contract_failed"
			state.log_event(event_type, {
				"contract_id": contract.id,
				"issuer_id": contract.issuer_id,
				"worker_id": contract.worker_id, # May be 0 if expired
				"refund": refund
			})

			if contract.status == Contract.STATUS_EXPIRED:
				stats["expired"] += 1
			else:
				stats["failed"] += 1

	# Clean up old inactive contracts to prevent unbounded growth
	_prune_inactive_contracts()

## Generate contracts for a day (called at day start)
func generate_daily_contracts(state: SimState, agents: Array, market: Market, world: World, tuning: Dictionary, 
							  rng: RNG, current_tick: int) -> void:
	# Intent-driven contracts are posted by agents when needed.
	# Keeping function for compatibility with sim call sites.
	return

## Remove old inactive contracts to prevent unbounded growth
func _prune_inactive_contracts() -> void:
	var inactive: Array = []
	var active: Array = []

	for contract in contracts:
		if contract.is_active():
			active.append(contract)
		else:
			inactive.append(contract)

	# Keep all active contracts, and only most recent inactive ones
	if inactive.size() > MAX_INACTIVE_CONTRACTS:
		# Sort by created_tick descending (newest first)
		inactive.sort_custom(func(a, b): return a.created_tick > b.created_tick)
		# Keep only the newest MAX_INACTIVE_CONTRACTS
		inactive = inactive.slice(0, MAX_INACTIVE_CONTRACTS)

	contracts = active + inactive

## Score a contract for an agent
func score_contract(contract: Contract, agent: Agent, market: Market, tuning: Dictionary,
					world: World, recipes: Dictionary) -> float:
	if not contract.is_available():
		return -999999.0
	
	# Don't accept own contracts
	if contract.issuer_id == agent.id:
		return -999999.0
	
	if not _can_agent_fulfill(agent, contract, world, recipes):
		return -999999.0

	# Estimate cost
	var have: int = agent.get_available_item(contract.item)
	var need: int = maxi(0, contract.qty - have)
	var ref_price: float = market.get_ref_price(contract.item)
	var estimated_cost: float = need * ref_price
	
	# Profit estimate
	var profit: float = contract.payout - estimated_cost
	
	# Min profit check
	var min_profit: int = tuning.get("contract_accept_min_profit", 5)
	if profit < min_profit:
		return -999999.0
	
	var estimated_ticks: int = _estimate_fulfillment_time(agent, contract, world, tuning, recipes)
	var opportunity_cost: float = float(tuning.get("opportunity_cost_per_tick", 0.1))
	var trust_bonus := 0.0
	if contract.issuer_type == "agent":
		var trust_threshold: float = float(tuning.get("trade_trust_bonus_threshold", 0.6))
		var trust_bonus_value: float = float(tuning.get("trade_trust_score_bonus", 2.0))
		var trust: float = agent.get_trust(contract.issuer_id)
		if trust >= trust_threshold:
			trust_bonus = trust_bonus_value
	return profit - (estimated_ticks * opportunity_cost) + trust_bonus

## Find best contract for an agent
func find_best_contract(agent: Agent, market: Market, tuning: Dictionary,
						world: World, recipes: Dictionary) -> Contract:
	var available: Array = get_available_contracts()
	if available.is_empty():
		return null
	
	var best_contract: Contract = null
	var best_score: float = -999999.0
	
	# Sort by ID for determinism
	available.sort_custom(_sort_contracts_by_id)
	
	for contract in available:
		var score: float = score_contract(contract, agent, market, tuning, world, recipes)
		if score > best_score:
			best_score = score
			best_contract = contract
	
	return best_contract

## Check if issuer already has an active contract for an item
func has_active_contract_for_issuer(issuer_type: String, issuer_id: int, item: String) -> bool:
	for contract in contracts:
		if contract.issuer_type != issuer_type or contract.issuer_id != issuer_id:
			continue
		if contract.item != item:
			continue
		if contract.is_active():
			return true
	return false

func _can_agent_fulfill(agent: Agent, contract: Contract, world: World, recipes: Dictionary) -> bool:
	if contract == null:
		return false
	var item: String = contract.item

	if item in ["Logs", "Ore"]:
		if item == "Logs" and not (agent.has_tool("Axe") or agent.has_tool("WoodenAxe")):
			return false
		if item == "Ore" and not (agent.has_tool("Pickaxe") or agent.has_tool("WoodenPickaxe")):
			return false
		return true
	if item in ["Berries", "Stone"]:
		return true

	var recipe := _find_recipe_for_output(item, recipes, world)
	if recipe == null:
		return false
	if recipe.station == "workbench" and not agent.has_available_item("Workbench"):
		return false
	if recipe.station != "hand" and recipe.station != "workbench" and not world.has_workshop_type(recipe.station):
		return false
	return true

func _estimate_fulfillment_time(agent: Agent, contract: Contract, world: World, tuning: Dictionary, recipes: Dictionary) -> int:
	if contract == null:
		return 0
	var travel_ticks: int = absi(agent.pos_x - contract.delivery_pos_x) + absi(agent.pos_y - contract.delivery_pos_y)
	var work_ticks := 0
	var item: String = contract.item

	if item in ["Berries", "Logs", "Ore", "Stone"]:
		var gather_ticks_per_item: int = int(tuning.get("contract_gather_ticks_per_item", 10))
		work_ticks = gather_ticks_per_item * contract.qty
	else:
		var recipe := _find_recipe_for_output(item, recipes, world)
		if recipe != null:
			var output_qty: int = int(recipe.outputs.get(item, 1))
			var batches: int = int(ceil(float(contract.qty) / maxf(float(output_qty), 1.0)))
			work_ticks = recipe.ticks * batches
		else:
			work_ticks = int(tuning.get("contract_unknown_item_ticks", 100))

	return travel_ticks + work_ticks

func _find_recipe_for_output(output_item: String, recipes: Dictionary, world: World) -> Recipe:
	for recipe_id in recipes:
		var recipe: Recipe = recipes[recipe_id]
		if recipe.outputs.has(output_item) and _is_recipe_allowed(recipe, world):
			return recipe
	return null

func _is_recipe_allowed(recipe: Recipe, world: World) -> bool:
	if recipe == null:
		return false
	if recipe.tier != "advanced":
		return _is_recipe_station_available(recipe, world)
	if recipe.station == "hand":
		return false
	return _is_recipe_station_available(recipe, world)

func _is_recipe_station_available(recipe: Recipe, world: World) -> bool:
	if recipe.station == "hand" or recipe.station == "workbench":
		return true
	return world.has_workshop_type(recipe.station)

func _find_org_delivery_stockpile(organization: Organization, state: SimState, target_x: int, target_y: int) -> StructureState:
	var stockpiles := state.structures.get_stockpiles_for_owner(organization.get_owner_id())
	if stockpiles.is_empty():
		return null
	var best: StructureState = null
	var best_dist := 999999
	for stockpile in stockpiles:
		var dist := absi(stockpile.pos_x - target_x) + absi(stockpile.pos_y - target_y)
		if dist < best_dist:
			best = stockpile
			best_dist = dist
	return best

static func _sort_agents_by_id(a, b) -> bool:
	return a.id < b.id

## Serialize to dictionary
func to_dict() -> Dictionary:
	var contracts_data := []
	for contract in contracts:
		contracts_data.append(contract.to_dict())
	
	return {
		"next_contract_id": next_contract_id,
		"contracts": contracts_data,
		"stats": stats.duplicate()
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> ContractsSystem:
	var system := ContractsSystem.new()
	system.next_contract_id = int(d.get("next_contract_id", 1))
	# Convert stats values to int
	var stats_data: Dictionary = d.get("stats", {
		"posted": 0, "accepted": 0, "completed": 0, "expired": 0, "failed": 0
	})
	system.stats = {}
	for key in stats_data:
		system.stats[key] = int(stats_data[key])
	
	system.contracts = []
	for contract_data in d.get("contracts", []):
		system.contracts.append(Contract.from_dict(contract_data))
	
	return system

static func _sort_contracts_by_id(a, b) -> bool:
	return a.id < b.id
