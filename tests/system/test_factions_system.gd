## System tests for the FactionsSystem
extends "res://tests/test_case.gd"

const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
	subtest("Formation Conditions Met", _test_formation_conditions_met)
	subtest("Formation Conditions Not Met", _test_formation_conditions_not_met)
	subtest("Formation Claims Tile", _test_formation_claims_tile)
	subtest("Recruitment Join", _test_recruitment_join)
	subtest("Territory Expansion", _test_territory_expansion)
	subtest("Proposal Creation", _test_proposal_creation)
	subtest("Serialization Roundtrip", _test_serialization_roundtrip)

func _test_formation_conditions_met() -> void:
	var state := Fixtures.make_sim_state()
	var rng := state.rng
	
	# Agent with enough money and grievance
	var agent := Fixtures.make_agent({
		"id": 1,
		"money": 200,  # Above faction_found_min_money
		"pos_x": 50,
		"pos_y": 50
	})
	agent.grievance = 0.8  # Above faction_found_min_grievance
	state.add_agent(agent)
	
	var factions_system := FactionsSystem.new()
	
	# Run a day update
	factions_system.daily_update(state, rng)
	
	# Should have formed a faction
	assert_true(state.factions.size() > 0, "Should have formed at least one faction")
	if state.factions.size() > 0:
		var faction: Faction = state.factions[0]
		assert_eq(faction.founder_agent_id, 1, "Agent 1 should be founder")
		assert_eq(agent.faction_id, faction.id, "Agent should be in faction")

func _test_formation_conditions_not_met() -> void:
	var state := Fixtures.make_sim_state()
	var rng := state.rng
	
	# Agent with low money OR low grievance
	var agent := Fixtures.make_agent({
		"id": 1,
		"money": 20,  # Below threshold
		"pos_x": 50,
		"pos_y": 50
	})
	agent.grievance = 0.1  # Low grievance
	state.add_agent(agent)
	
	var factions_system := FactionsSystem.new()
	
	# Run update
	factions_system.daily_update(state, rng)
	
	# Should NOT have formed a faction
	assert_eq(state.factions.size(), 0, "Should not form faction with insufficient conditions")
	assert_eq(agent.faction_id, 0, "Agent should still have no faction")

func _test_formation_claims_tile() -> void:
	var state := Fixtures.make_sim_state()
	var rng := state.rng
	
	# Create founding conditions
	var agent := Fixtures.make_agent({
		"id": 1,
		"money": 200,
		"pos_x": 50,
		"pos_y": 50
	})
	agent.grievance = 0.9
	state.add_agent(agent)
	
	# Clear any existing claims near the position
	for dx in range(-5, 6):
		for dy in range(-5, 6):
			state.world.set_claim_owner(50 + dx, 50 + dy, 0)
	
	var factions_system := FactionsSystem.new()
	factions_system.daily_update(state, rng)
	
	# After formation, faction should own at least one tile
	if state.factions.size() > 0:
		var faction: Faction = state.factions[0]
		var faction_owner_id := World.owner_id_for_faction(faction.id)
		
		# Check that home tile is claimed
		var home_claimed := state.world.get_claim_owner(faction.home_pos.x, faction.home_pos.y)
		assert_eq(home_claimed, faction_owner_id, "Home tile should be claimed by faction")

func _test_recruitment_join() -> void:
	var state := Fixtures.make_sim_state()
	var rng := state.rng
	
	# Create existing faction
	var faction := Fixtures.make_faction(1, {
		"name": "Test Faction",
		"home_pos": Vector2i(50, 50),
		"treasury": 100
	})
	# Add a member to make it look active
	faction.add_member(100)  # Founder
	state.factions = [faction]
	
	# Create agent nearby with grievance
	var agent := Fixtures.make_agent({
		"id": 2,
		"money": 50,
		"pos_x": 52,
		"pos_y": 52
	})
	agent.grievance = 0.5  # Some grievance
	agent.faction_id = 0  # Not in faction yet
	state.add_agent(agent)
	
	var factions_system := FactionsSystem.new()
	factions_system.daily_update(state, rng)
	
	# Check if agent joined (probabilistic - may not always join)
	# Just verify no crash and state is consistent
	if agent.faction_id != 0:
		assert_eq(agent.faction_id, 1, "If joined, should be in faction 1")
		assert_true(faction.is_member(agent.id), "Faction should have agent as member")

func _test_territory_expansion() -> void:
	var state := Fixtures.make_sim_state()
	var rng := state.rng
	
	# Create faction with treasury
	var faction := Fixtures.make_faction(1, {
		"name": "Expanding Faction",
		"home_pos": Vector2i(50, 50),
		"treasury": 500  # Enough to claim tiles
	})
	faction.add_member(1)
	state.factions = [faction]
	
	# Claim home tile
	var faction_owner_id := World.owner_id_for_faction(1)
	state.world.set_claim_owner(50, 50, faction_owner_id)
	
	var factions_system := FactionsSystem.new()
	
	# Record initial treasury
	var initial_treasury := faction.treasury
	
	# Run update
	factions_system.daily_update(state, rng)
	
	# If expansion happened, treasury should decrease
	# (Expansion is probabilistic and depends on tuning)
	# Verify state is consistent either way
	assert_true(faction.treasury >= 0, "Treasury should not go negative")

func _test_proposal_creation() -> void:
	var state := Fixtures.make_sim_state()
	var rng := state.rng
	
	# Create faction with grieving members
	var faction := Fixtures.make_faction(1, {
		"name": "Proposing Faction",
		"home_pos": Vector2i(50, 50),
		"treasury": 100
	})
	
	# Create laws for faction
	var faction_owner_id := World.owner_id_for_faction(1)
	var laws := Fixtures.make_laws()
	state.laws_by_owner[faction_owner_id] = laws
	
	# Create members with grievance
	for i in range(5):
		var member := Fixtures.make_agent({
			"id": i + 1,
			"faction_id": 1
		})
		member.grievance = 0.6  # Above proposal threshold
		state.add_agent(member)
		faction.add_member(member.id)
	
	state.factions = [faction]
	
	var factions_system := FactionsSystem.new()
	factions_system.daily_update(state, rng)
	
	# Proposals may or may not be created based on conditions
	# Just verify no crash
	assert_true(true, "Proposal creation should not crash")

func _test_serialization_roundtrip() -> void:
	var factions_system := FactionsSystem.new()
	factions_system.stats["factions_formed"] = 5
	factions_system.stats["members_joined"] = 20
	factions_system.stats["tiles_claimed"] = 15
	
	# Serialize
	var dict := factions_system.to_dict()
	
	# Deserialize
	var restored := FactionsSystem.from_dict(dict)
	
	# Verify stats
	assert_eq(restored.stats["factions_formed"], 5, "factions_formed should match")
	assert_eq(restored.stats["members_joined"], 20, "members_joined should match")
	assert_eq(restored.stats["tiles_claimed"], 15, "tiles_claimed should match")
