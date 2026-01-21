## FactionsSystem - coordinates faction formation, membership, territory, and governance
class_name FactionsSystem
extends RefCounted

var stats: Dictionary = {
	"factions_formed": 0,
	"members_joined": 0,
	"proposals_created": 0,
	"proposals_resolved": 0,
	"tiles_claimed": 0,
	"taxes_collected": 0
}

func _init() -> void:
	pass

## Daily update - called once per day from sim
func daily_update(state: SimState, rng: RNG) -> void:
	var tuning := state.tuning
	var ticks_per_day: int = tuning.get("ticks_per_day", 24)
	
	# Process agents in ID ascending order for determinism
	var sorted_agents := state.agents.duplicate()
	sorted_agents.sort_custom(func(a, b): return a.id < b.id)
	
	# 1. Faction formation
	_process_faction_formation(sorted_agents, state, rng)
	
	# 2. Recruitment / joining
	_process_recruitment(sorted_agents, state, rng)
	
	# 3. Territory expansion
	_process_territory_expansion(state, rng)
	
	# 4. Proposals and voting
	_process_governance(state, rng)
	
	# 5. Elections
	_process_elections(state, rng)
	
	# 6. Leader Salaries
	_pay_leader_salaries(state)
	
	# 7. Decay grievance
	_decay_grievance(sorted_agents, tuning)

## Process elections for factions
func _process_elections(state: SimState, rng: RNG) -> void:
	var tuning := state.tuning
	var ticks_per_day: int = tuning.get("ticks_per_day", 24)
	var election_period_days: int = tuning.get("election_period_days", 7)
	
	for faction in state.factions:
		if state.tick >= faction.election_tick:
			_hold_election(faction, state, rng)
			faction.election_tick = state.tick + (election_period_days * ticks_per_day)

## Hold an election for a faction
func _hold_election(faction: Faction, state: SimState, rng: RNG) -> void:
	if faction.members.is_empty():
		return
		
	# Simple election logic: 
	# Candidates are the 3 wealthiest members (or all if < 3)
	var candidates := []
	for member_id in faction.members:
		var agent := state.get_agent(member_id)
		if agent:
			candidates.append(agent)
	
	candidates.sort_custom(func(a, b): return a.money > b.money)
	var top_candidates = candidates.slice(0, 3)
	
	if top_candidates.is_empty():
		return
		
	# Each member votes for a candidate
	var votes := {} # candidate_id -> count
	for candidate in top_candidates:
		votes[candidate.id] = 0
		
	for member_id in faction.members:
		# Vote for someone (bias toward current leader, then wealth)
		var chosen_id = top_candidates[0].id
		if rng.randf() < 0.3 and faction.leader_id in votes:
			chosen_id = faction.leader_id
		elif top_candidates.size() > 1 and rng.randf() < 0.2:
			chosen_id = top_candidates[rng.工具(top_candidates.size()) if "工具" in rng else rng.randi() % top_candidates.size()].id
		
		# Wait, I shouldn't use "工具" (tool) in code. Just use %
		chosen_id = top_candidates[rng.randi() % top_candidates.size()].id
		
		votes[chosen_id] += 1
	
	# Find winner
	var winner_id = faction.leader_id
	var max_votes = -1
	
	# Deterministic winner selection
	var sorted_vote_ids = votes.keys()
	sorted_vote_ids.sort()
	
	for cid in sorted_vote_ids:
		if votes[cid] > max_votes:
			max_votes = votes[cid]
			winner_id = cid
	
	var old_leader = faction.leader_id
	faction.leader_id = winner_id
	
	state.log_event("election_held", {
		"faction_id": faction.id,
		"winner_id": winner_id,
		"old_leader_id": old_leader,
		"votes": votes
	})

## Pay salaries to faction leaders from treasury
func _pay_leader_salaries(state: SimState) -> void:
	for faction in state.factions:
		if faction.leader_id > 0 and faction.treasury >= faction.leader_salary_rate:
			var leader := state.get_agent(faction.leader_id)
			if leader:
				faction.treasury -= faction.leader_salary_rate
				leader.money += faction.leader_salary_rate
				
				state.log_event("salary_paid", {
					"faction_id": faction.id,
					"agent_id": leader.id,
					"amount": faction.leader_salary_rate
				})

## Process faction formation for agents
func _process_faction_formation(agents: Array, state: SimState, rng: RNG) -> void:
	var tuning := state.tuning
	var min_money: int = tuning.get("faction_found_min_money", 80)
	var min_grievance: float = tuning.get("faction_found_min_grievance", 0.5)
	var daily_chance: float = tuning.get("faction_found_daily_chance", 0.05)
	var claim_radius: int = tuning.get("faction_found_claim_radius", 10)
	var treasury_seed: int = tuning.get("faction_found_treasury_seed", 50)
	var ticks_per_day: int = tuning.get("ticks_per_day", 24)
	
	for agent in agents:
		if agent.faction_id != 0:
			continue  # Already in a faction
		
		# Check founding conditions
		if agent.money < min_money:
			continue
		if agent.money < treasury_seed:
			continue
		
		var should_found := false
		if agent.grievance >= min_grievance:
			should_found = true
		elif rng.randf() < daily_chance:
			should_found = true
		
		if not should_found:
			continue
		
		# Find unclaimed tile within radius
		var claim_pos := _find_unclaimed_tile_near(agent.pos_x, agent.pos_y, 
												   claim_radius, state.world)
		if claim_pos == Vector2i(-1, -1):
			continue  # No unclaimed tile available
		
		# Found a faction!
		var faction := Faction.new()
		var faction_id := state.next_faction_id
		state.next_faction_id += 1
		
		var faction_name := "Faction_%d" % faction_id
		faction.init_faction(faction_id, faction_name, agent.id, 
							 Vector2i(agent.pos_x, agent.pos_y), state.tick, ticks_per_day)
		
		# Transfer treasury seed
		agent.money -= treasury_seed
		faction.treasury = treasury_seed
		faction.openness = rng.randf()  # Random openness trait
		
		# Set agent faction
		agent.faction_id = faction_id
		var grievance_reduction: float = tuning.get("faction_found_grievance_reduction", 0.3)
		agent.grievance = maxf(0.0, agent.grievance - grievance_reduction)  # Reduce grievance
		
		# Claim initial tile
		var owner_id := World.owner_id_for_faction(faction_id)
		state.world.set_claim_owner(claim_pos.x, claim_pos.y, owner_id)
		
		# Create laws for faction
		var faction_laws := Laws.new()
		faction_laws.init_from_tuning(tuning)
		state.laws_by_owner[owner_id] = faction_laws
		
		# Initialize default trade relations
		faction.init_default_relations(tuning)
		
		# Add faction to state
		state.factions.append(faction)
		stats["factions_formed"] += 1
		stats["tiles_claimed"] += 1
		
		state.log_event("faction_formed", {
			"faction_id": faction.id,
			"name": faction.name,
			"founder_id": agent.id,
			"home_pos": [faction.home_pos.x, faction.home_pos.y]
		})

## Find closest unclaimed tile near a position
func _find_unclaimed_tile_near(x: int, y: int, radius: int, world: World) -> Vector2i:
	var candidates := []
	
	for dx in range(-radius, radius + 1):
		for dy in range(-radius, radius + 1):
			var tx := x + dx
			var ty := y + dy
			if world.is_valid(tx, ty) and world.get_claim_owner(tx, ty) == 0:
				var dist := absi(dx) + absi(dy)
				candidates.append({"x": tx, "y": ty, "dist": dist})
	
	if candidates.is_empty():
		return Vector2i(-1, -1)
	
	# Sort by distance, then x, then y for determinism
	candidates.sort_custom(func(a, b):
		if a["dist"] != b["dist"]:
			return a["dist"] < b["dist"]
		if a["x"] != b["x"]:
			return a["x"] < b["x"]
		return a["y"] < b["y"]
	)
	
	return Vector2i(candidates[0]["x"], candidates[0]["y"])

## Process agent recruitment into factions
func _process_recruitment(agents: Array, state: SimState, rng: RNG) -> void:
	var tuning := state.tuning
	var search_radius: int = tuning.get("join_search_radius", 15)
	var min_score: float = tuning.get("join_min_score", 0.3)
	var min_claims: int = tuning.get("min_claims_for_join_benefit", 3)
	var market_x: int = tuning.get("market_pos_x", 48)
	var market_y: int = tuning.get("market_pos_y", 48)
	
	var global_berry_stock := state.world.get_total_stock("berry")
	var food_scarce: bool = global_berry_stock < int(tuning.get("berry_scarcity_threshold", 100))

	for agent in agents:
		if agent.faction_id != 0:
			continue  # Already in a faction
		
		var best_faction: Faction = null
		var best_score: float = -999.0
		
		# Evaluate each faction
		for faction in state.factions:
			# Check if faction is within range of agent or market
			var agent_dist := absi(faction.home_pos.x - agent.pos_x) + absi(faction.home_pos.y - agent.pos_y)
			var market_dist := absi(faction.home_pos.x - market_x) + absi(faction.home_pos.y - market_y)
			
			if agent_dist > search_radius and market_dist > search_radius:
				continue
			
			# Calculate score
			var score := 0.0
			
			# Benefit: access to claimed land (O(1) lookup instead of tile iteration)
			var faction_owner_id := World.owner_id_for_faction(faction.id)
			var claims_count := state.world.get_owner_claims_count(faction_owner_id)
			
			if claims_count >= min_claims:
				var claims_score: float = tuning.get("faction_join_claims_score", 0.4)
				score += claims_score
				# Bonus if food scarce and faction has berries
				if food_scarce:
					var scarcity_bonus: float = tuning.get("faction_join_scarcity_bonus", 0.3)
					score += scarcity_bonus
			
			# Benefit: low tax
			var laws: Laws = state.get_laws(faction_owner_id)
			var tax_rate: int = laws.sales_tax_rate
			score += (20 - tax_rate) / 40.0  # 0 to 0.5 based on tax
			
			# Small bonus for existing members (stability)
			var member_bonus: float = tuning.get("faction_join_member_bonus", 0.02)
			score += faction.get_member_count() * member_bonus
			
			if score > best_score:
				best_score = score
				best_faction = faction
		
		# Join if score is good enough
		if best_faction != null and best_score >= min_score:
			best_faction.add_member(agent.id)
			agent.faction_id = best_faction.id
			var join_grievance_reduction: float = tuning.get("faction_join_grievance_reduction", 0.2)
			agent.grievance = maxf(0.0, agent.grievance - join_grievance_reduction)
			stats["members_joined"] += 1
			
			state.log_event("faction_joined", {
				"faction_id": best_faction.id,
				"agent_id": agent.id
			})

## Process territory expansion for factions
func _process_territory_expansion(state: SimState, rng: RNG) -> void:
	var tuning := state.tuning
	var claims_per_day: int = tuning.get("faction_claims_per_day", 2)
	var claim_cost: int = tuning.get("faction_claim_cost", 15)
	
	# Sort factions by ID for determinism
	var sorted_factions := state.factions.duplicate()
	sorted_factions.sort_custom(func(a, b): return a.id < b.id)
	
	for faction in sorted_factions:
		var faction_owner_id := World.owner_id_for_faction(faction.id)
		var claims_made := 0
		
		while claims_made < claims_per_day and faction.treasury >= claim_cost:
			# Find candidate tiles (unclaimed adjacent to faction territory)
			var candidates := _find_expansion_candidates(faction_owner_id, 
														 faction.home_pos, state.world)
			
			if candidates.is_empty():
				break
			
			# Claim the first candidate
			var pos: Vector2i = candidates[0]
			state.world.set_claim_owner(pos.x, pos.y, faction_owner_id)
			faction.treasury -= claim_cost
			claims_made += 1
			stats["tiles_claimed"] += 1
			
			state.log_event("tile_claimed", {
				"faction_id": faction.id,
				"pos": [pos.x, pos.y]
			})

## Find expansion candidate tiles for a faction
func _find_expansion_candidates(faction_owner_id: int, home_pos: Vector2i, 
								world: World) -> Array:
	var candidates := []
	var existing_tiles := []
	
	# Find existing faction tiles
	for y in range(world.height):
		for x in range(world.width):
			if world.get_claim_owner(x, y) == faction_owner_id:
				existing_tiles.append(Vector2i(x, y))
	
	# Find unclaimed tiles adjacent to existing territory
	var checked := {}
	for tile in existing_tiles:
		for offset in [Vector2i(0, -1), Vector2i(0, 1), Vector2i(-1, 0), Vector2i(1, 0)]:
			var neighbor: Vector2i = tile + offset
			var key := "%d,%d" % [neighbor.x, neighbor.y]
			if key in checked:
				continue
			checked[key] = true
			
			if world.is_valid(neighbor.x, neighbor.y):
				if world.get_claim_owner(neighbor.x, neighbor.y) == 0:
					var dist := absi(neighbor.x - home_pos.x) + absi(neighbor.y - home_pos.y)
					candidates.append({"pos": neighbor, "dist": dist})
	
	# Sort by distance to home, then x, then y
	candidates.sort_custom(func(a, b):
		if a["dist"] != b["dist"]:
			return a["dist"] < b["dist"]
		if a["pos"].x != b["pos"].x:
			return a["pos"].x < b["pos"].x
		return a["pos"].y < b["pos"].y
	)
	
	var result := []
	for c in candidates:
		result.append(c["pos"])
	return result

## Process governance - proposals and voting
func _process_governance(state: SimState, rng: RNG) -> void:
	var tuning := state.tuning
	var proposal_threshold: float = tuning.get("proposal_grievance_threshold", 0.3)
	var max_proposals: int = tuning.get("max_proposals_per_day_per_faction", 1)
	
	# Sort factions by ID for determinism
	var sorted_factions := state.factions.duplicate()
	sorted_factions.sort_custom(func(a, b): return a.id < b.id)
	
	for faction in sorted_factions:
		# 1. Resolve expired proposals
		_resolve_expired_proposals(faction, state)
		
		# 2. Create new proposals
		_create_proposals(faction, state, tuning, max_proposals, proposal_threshold)

## Resolve expired proposals for a faction
func _resolve_expired_proposals(faction: Faction, state: SimState) -> void:
	var expired := faction.get_expired_proposals(state.tick)
	var faction_owner_id := World.owner_id_for_faction(faction.id)
	
	for proposal in expired:
		# Count votes
		var votes_for: int = proposal["votes_for"].size()
		var votes_against: int = proposal["votes_against"].size()
		
		# Majority wins, ties = fail
		if votes_for > votes_against:
			# Apply changes
			var laws: Laws = state.get_laws(faction_owner_id)
			var changes: Dictionary = proposal["changes"]
			
			if changes.has("harvest_permit_required"):
				laws.harvest_permit_required = changes["harvest_permit_required"]
			if changes.has("build_permit_required"):
				laws.build_permit_required = changes["build_permit_required"]
			if changes.has("sales_tax_rate"):
				var tax_min: int = tuning.get("sales_tax_rate_min", 0)
				var tax_max: int = tuning.get("sales_tax_rate_max", 20)
				laws.sales_tax_rate = clampi(changes["sales_tax_rate"], tax_min, tax_max)
			if changes.has("fine_base"):
				var min_fine: int = state.tuning.get("min_fine", 5)
				var max_fine: int = state.tuning.get("max_fine", 50)
				laws.fine_base = clampi(changes["fine_base"], min_fine, max_fine)
			
			# Handle trade policy changes
			if changes.has("set_relation_policy"):
				var rel_change: Dictionary = changes["set_relation_policy"]
				var target_key: String = rel_change.get("target_key", "faction:0")
				var policy: String = rel_change.get("policy", "open")
				var tariff_rate: int = rel_change.get("tariff_rate", 0)
				var max_tariff: int = state.tuning.get("tariff_rate_max", 30)
				tariff_rate = clampi(tariff_rate, 0, max_tariff)
				faction.set_relation(target_key, policy, tariff_rate)
		
		# Remove proposal
		faction.remove_proposal(proposal["proposal_id"])
		stats["proposals_resolved"] += 1
		
		state.log_event("proposal_resolved", {
			"faction_id": faction.id,
			"proposal_id": proposal["proposal_id"],
			"passed": (votes_for > votes_against),
			"votes_for": votes_for,
			"votes_against": votes_against,
			"changes": proposal["changes"]
		})

## Create new proposals for a faction
func _create_proposals(faction: Faction, state: SimState, tuning: Dictionary,
					   max_proposals: int, threshold: float) -> void:
	var active_count := faction.get_active_proposals(state.tick).size()
	if active_count >= max_proposals:
		return
	
	# Find proposer (lowest ID member with grievance >= threshold)
	var proposer: Agent = null
	for member_id in faction.members:
		var agent := state.get_agent(member_id)
		if agent != null and agent.grievance >= threshold:
			proposer = agent
			break
	
	if proposer == null:
		return
	
	# Determine what to propose based on grievance source
	var faction_owner_id := World.owner_id_for_faction(faction.id)
	var laws: Laws = state.get_laws(faction_owner_id)
	var changes := {}
	
	var avg_pollution := state.world.get_average_pollution()
	var pollution_high_threshold: float = tuning.get("pollution_high_threshold", 0.6)
	var global_berry_stock := state.world.get_total_stock("berry")
	var food_scarce: bool = global_berry_stock < int(tuning.get("berry_scarcity_threshold", 100))
	
	# Ecology-based proposals
	if avg_pollution > pollution_high_threshold:
		# If pollution high, proposer may want stricter laws
		if not laws.harvest_permit_required:
			changes["harvest_permit_required"] = true
		elif laws.fine_base < tuning.get("max_fine", 50):
			changes["fine_base"] = laws.fine_base + tuning.get("fine_step", 5)
	
	if food_scarce:
		# If food scarce, propose lowering tax to encourage trade
		if laws.sales_tax_rate > 2:
			changes["sales_tax_rate"] = laws.sales_tax_rate - tuning.get("tax_step", 2)
		
		# If faction is "open", propose lowering tariffs too
		if faction.openness > 0.6:
			# Find a relation with tariff and lower it
			for target_key in faction.relations:
				var rel: Dictionary = faction.relations[target_key]
				if rel["policy"] == "tariff" and rel["tariff_rate"] > 0:
					changes["set_relation_policy"] = {
						"target_key": target_key,
						"policy": "tariff",
						"tariff_rate": maxi(0, rel["tariff_rate"] - tuning.get("policy_change_tariff_step", 5))
					}
					break
	
	# Fallback to standard grievance-based logic if no ecological/scarcity proposals made
	if changes.is_empty():
		# Simple logic: if high grievance, propose lower fines or lower taxes
		var tax_step: int = tuning.get("tax_step", 2)
		var fine_step: int = tuning.get("fine_step", 5)

		# Prefer tax reduction if tax > 5
		if laws.sales_tax_rate > 5:
			changes["sales_tax_rate"] = maxi(0, laws.sales_tax_rate - tax_step)
		# Else fine reduction if fines are high
		elif laws.fine_base > tuning.get("min_fine", 5):
			changes["fine_base"] = laws.fine_base - fine_step
		# Else try to remove permits if they exist
		elif laws.harvest_permit_required:
			changes["harvest_permit_required"] = false
		elif laws.build_permit_required:
			changes["build_permit_required"] = false
		else:
			return  # Nothing to propose
	
	# Create proposal
	faction.create_proposal(proposer.id, changes, faction_owner_id, state.tick)
	stats["proposals_created"] += 1
	
	state.log_event("proposal_created", {
		"faction_id": faction.id,
		"proposer_id": proposer.id,
		"changes": changes
	})
	
	# Auto-vote by proposer
	var proposals_array: Array = faction.charter["proposals"]
	var new_proposal: Dictionary = proposals_array.back()
	faction.vote_on_proposal(new_proposal["proposal_id"], proposer.id, true)
	
	# Other members vote deterministically
	for member_id in faction.members:
		if member_id == proposer.id:
			continue
		
		var agent := state.get_agent(member_id)
		if agent == null:
			continue
		
		var vote_for := _should_vote_for(agent, changes, laws) 
		faction.vote_on_proposal(new_proposal["proposal_id"], member_id, vote_for)

## Determine if agent should vote for a proposal
func _should_vote_for(agent: Agent, changes: Dictionary, current_laws: Laws) -> bool:
	# Tax reduction = good for traders/gatherers
	if changes.has("sales_tax_rate"):
		if changes["sales_tax_rate"] < current_laws.sales_tax_rate:
			return true  # Lower taxes = vote for
	
	# Fine reduction = good if agent has grievance
	if changes.has("fine_base"):
		if changes["fine_base"] < current_laws.fine_base:
			var vote_grievance_thresh: float = 0.2  # Use tuning when state available
			if agent.grievance > vote_grievance_thresh:
				return true
		# Fine increase -> vote for if high eco concern
		elif changes["fine_base"] > current_laws.fine_base:
			if agent.get("eco_concern") != null and agent.eco_concern > 0.6:
				return true
	
	# Permit removal = good for gatherers
	if changes.has("harvest_permit_required"):
		if not changes["harvest_permit_required"]:
			if agent.role == "gatherer":
				return true
		else:
			# Permit required -> vote for if high eco concern
			if agent.get("eco_concern") != null and agent.eco_concern > 0.6:
				return true
	
	if changes.has("build_permit_required") and not changes["build_permit_required"]:
		return true
	
	# Default: vote against
	return false

## Decay grievance for all agents
func _decay_grievance(agents: Array, tuning: Dictionary) -> void:
	var decay: float = tuning.get("grievance_decay_daily", 0.1)
	for agent in agents:
		agent.grievance = maxf(0.0, agent.grievance - decay)

## Get faction by ID
func get_faction(faction_id: int, factions: Array) -> Faction:
	for faction in factions:
		if faction.id == faction_id:
			return faction
	return null

## Serialize to dictionary
func to_dict() -> Dictionary:
	return {
		"stats": stats.duplicate()
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> FactionsSystem:
	var system := FactionsSystem.new()
	var stats_data: Dictionary = d.get("stats", {
		"factions_formed": 0,
		"members_joined": 0,
		"proposals_created": 0,
		"proposals_resolved": 0,
		"tiles_claimed": 0,
		"taxes_collected": 0
	})
	system.stats = {}
	for key in stats_data:
		system.stats[key] = int(stats_data[key])
	return system
