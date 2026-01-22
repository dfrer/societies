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
	var post_chance: float = tuning.get("daily_contract_post_chance", 0.15)
	var deadline_days: int = tuning.get("contract_deadline_days", 2)
	var payout_mult: float = tuning.get("contract_payout_multiplier", 1.2)
	
	# Scarcity sensing
	var global_berry_stock := world.get_total_stock("berry")
	var global_tree_stock := world.get_total_stock("tree")
	var avg_pollution := world.get_average_pollution()
	var workshop_count := world.get_ready_workshop_count()
	
	var food_scarce: bool = global_berry_stock < int(tuning.get("berry_scarcity_threshold", 100))
	var wood_scarce: bool = global_tree_stock < int(tuning.get("tree_scarcity_threshold", 150))
	var pollution_high: bool = avg_pollution > float(tuning.get("pollution_high_threshold", 0.6))
	
	if food_scarce or pollution_high:
		# Increase probability of food-related contracts and payouts
		payout_mult *= tuning.get("contract_food_focus_multiplier", 2.0)
		var cap: float = tuning.get("contract_payout_multiplier_cap", 2.5)
		payout_mult = minf(payout_mult, cap)
		
	var ticks_per_day: int = tuning.get("ticks_per_day", 200)
	var market_x: int = tuning.get("market_pos_x", 48)
	var market_y: int = tuning.get("market_pos_y", 48)
	
	var deadline: int = current_tick + (deadline_days * ticks_per_day)
	
	# Iterate agents in ID order for determinism
	var sorted_agents: Array = agents.duplicate()
	sorted_agents.sort_custom(_sort_agents_by_id)
	
	for agent in sorted_agents:
		if not agent.is_alive():
			continue
		
		var roll: float = rng.randf()
		if roll >= post_chance:
			continue
		
		# Determine what to request based on needs
		var item: String
		var qty: int
		
		var hunger: float = agent.get_hunger()
		var food_count: int = agent.get_item_count("Berries") + agent.get_item_count("CookedMeal")
		
		if hunger < 40 and food_count < 3:
			# Request food
			if rng.randf() < 0.7:
				item = "CookedMeal"
				qty = rng.randi_range(1, 3)
			else:
				item = "Berries"
				qty = rng.randi_range(3, 8)
		else:
			# Request materials or tools
			# If no workshops, rarely ask for manufactured goods unless rich enough to incentivize manual labor
			var allow_manufactured: bool = (workshop_count > 0) or (rng.randf() < 0.2)
			
			var type_roll: float = rng.randf()
			if allow_manufactured and type_roll < 0.4:
				item = "Planks"
				qty = rng.randi_range(2, 5)
			elif type_roll < 0.7: # More logs if no planks
				item = "Logs"
				qty = rng.randi_range(3, 8)
			elif allow_manufactured and type_roll < 0.8:
				item = "MetalIngot"
				qty = rng.randi_range(1, 3)
			elif wood_scarce and rng.randf() < 0.5:
				item = "Logs"
				qty = rng.randi_range(5, 10)
		
		# Calculate payout
		var ref_price: float = market.get_ref_price(item)
		var payout: int = int(ceil(ref_price * qty * payout_mult))
		
		# Only post if can afford
		if agent.get_available_money() >= payout:
			post_contract(agent, item, qty, payout, deadline, market_x, market_y, current_tick, state)

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
func score_contract(contract: Contract, agent: Agent, market: Market, tuning: Dictionary) -> float:
	if not contract.is_available():
		return -999999.0
	
	# Don't accept own contracts
	if contract.issuer_id == agent.id:
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
	
	# Distance penalty (simple)
	var market_x: int = tuning.get("market_pos_x", 48)
	var market_y: int = tuning.get("market_pos_y", 48)
	var dist: int = absi(agent.pos_x - market_x) + absi(agent.pos_y - market_y)
	var time_penalty: float = dist * 0.1
	
	return profit - time_penalty

## Find best contract for an agent
func find_best_contract(agent: Agent, market: Market, tuning: Dictionary) -> Contract:
	var available: Array = get_available_contracts()
	if available.is_empty():
		return null
	
	var best_contract: Contract = null
	var best_score: float = -999999.0
	
	# Sort by ID for determinism
	available.sort_custom(_sort_contracts_by_id)
	
	for contract in available:
		var score: float = score_contract(contract, agent, market, tuning)
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
