## System tests for the ContractsSystem
extends "res://tests/test_case.gd"

const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
	subtest("Post Contract with Escrow", _test_post_contract_escrow)
	subtest("Post Contract Insufficient Funds", _test_post_contract_insufficient_funds)
	subtest("Accept Contract", _test_accept_contract)
	subtest("Complete Contract", _test_complete_contract)
	subtest("Expire Unaccepted Contract", _test_expire_unaccepted)
	subtest("Expire Accepted Incomplete Contract", _test_expire_accepted_incomplete)
	subtest("Find Best Contract", _test_find_best_contract)
	subtest("Serialization Roundtrip", _test_serialization_roundtrip)

func _test_post_contract_escrow() -> void:
	var state := Fixtures.make_sim_state()
	var issuer := Fixtures.make_agent({"id": 1, "money": 200})
	state.add_agent(issuer)
	
	var contracts := ContractsSystem.new()
	
	# Post a contract
	var contract := contracts.post_contract(
		issuer, "Berries", 10, 50, 100, 48, 48, 0, state
	)
	
	assert_true(contract != null, "Should create contract")
	assert_eq(contract.issuer_id, 1, "Issuer ID should match")
	assert_eq(contract.item, "Berries", "Item should match")
	assert_eq(contract.qty, 10, "Qty should match")
	assert_eq(contract.payout, 50, "Payout should match")
	
	# Issuer's money should be locked
	assert_eq(issuer.locked_money, 50, "Payout should be locked as escrow")
	assert_eq(issuer.get_available_money(), 150, "Available money should be 150")
	
	# Stats should update
	assert_eq(contracts.stats["posted"], 1, "Posted stat should be 1")

func _test_post_contract_insufficient_funds() -> void:
	var state := Fixtures.make_sim_state()
	var issuer := Fixtures.make_agent({"id": 1, "money": 30})  # Not enough
	state.add_agent(issuer)
	
	var contracts := ContractsSystem.new()
	
	# Try to post a contract for 50
	var contract := contracts.post_contract(
		issuer, "Logs", 5, 50, 100, 48, 48, 0, state
	)
	
	# Should return null when insufficient funds
	assert_true(contract == null, "Should not create contract without funds")
	
	# Money unchanged
	assert_eq(issuer.money, 30, "Money should be unchanged")
	assert_eq(issuer.locked_money, 0, "No money should be locked")

func _test_accept_contract() -> void:
	var state := Fixtures.make_sim_state()
	var issuer := Fixtures.make_agent({"id": 1, "money": 200})
	var worker := Fixtures.make_agent({"id": 2, "money": 0})
	state.add_agent(issuer)
	state.add_agent(worker)
	
	var contracts := ContractsSystem.new()
	
	# Post contract
	var contract := contracts.post_contract(
		issuer, "Ore", 5, 60, 100, 48, 48, 0, state
	)
	
	# Accept contract
	var accepted := contracts.accept_contract(contract.id, worker, 10, state)
	
	assert_true(accepted, "Should accept contract")
	assert_eq(contract.status, Contract.STATUS_ACCEPTED, "Status should be ACCEPTED")
	assert_eq(contract.worker_id, 2, "Worker ID should be set")
	assert_eq(worker.active_contract_id, contract.id, "Worker should have active contract")
	
	# Stats should update
	assert_eq(contracts.stats["accepted"], 1, "Accepted stat should be 1")

func _test_complete_contract() -> void:
	var state := Fixtures.make_sim_state()
	var issuer := Fixtures.make_agent({"id": 1, "money": 200})
	var worker := Fixtures.make_agent({"id": 2, "money": 0})
	worker.add_item("Planks", 10)
	state.add_agent(issuer)
	state.add_agent(worker)
	
	var contracts := ContractsSystem.new()
	
	# Post and accept
	var contract := contracts.post_contract(
		issuer, "Planks", 5, 80, 100, 48, 48, 0, state
	)
	contracts.accept_contract(contract.id, worker, 10, state)
	
	# Complete
	var completed := contracts.complete_delivery(contract.id, worker, state)
	
	assert_true(completed, "Should complete contract")
	assert_eq(contract.status, Contract.STATUS_COMPLETED, "Status should be COMPLETED")
	
	# Worker received payout
	assert_eq(worker.money, 80, "Worker should receive payout")
	
	# Worker's items transferred
	assert_eq(worker.get_item_count("Planks"), 5, "Worker should have 5 remaining")
	
	# Issuer received items
	assert_eq(issuer.get_item_count("Planks"), 5, "Issuer should receive items")
	
	# Escrow released
	assert_eq(issuer.locked_money, 0, "Issuer's locked money should be 0")

func _test_expire_unaccepted() -> void:
	var state := Fixtures.make_sim_state()
	var issuer := Fixtures.make_agent({"id": 1, "money": 200})
	state.add_agent(issuer)
	
	var contracts := ContractsSystem.new()
	
	# Post contract with deadline at tick 50
	var contract := contracts.post_contract(
		issuer, "CookedMeal", 3, 100, 50, 48, 48, 0, state
	)
	
	assert_eq(issuer.locked_money, 100, "Money should be locked")
	
	# Process expirations at tick 60
	contracts.process_expirations(60, state.agents, state)
	
	# Contract should be expired
	assert_eq(contract.status, Contract.STATUS_EXPIRED, "Should be expired")
	
	# Issuer should get refund
	assert_eq(issuer.locked_money, 0, "Locked money should be released")
	assert_eq(issuer.money, 200, "Money should be refunded")

func _test_expire_accepted_incomplete() -> void:
	var state := Fixtures.make_sim_state()
	var issuer := Fixtures.make_agent({"id": 1, "money": 200})
	var worker := Fixtures.make_agent({"id": 2, "money": 0})
	state.add_agent(issuer)
	state.add_agent(worker)
	
	var contracts := ContractsSystem.new()
	
	# Post and accept contract but don't complete
	var contract := contracts.post_contract(
		issuer, "Axe", 1, 150, 50, 48, 48, 0, state
	)
	contracts.accept_contract(contract.id, worker, 10, state)
	
	# Process expirations at tick 60
	contracts.process_expirations(60, state.agents, state)
	
	# Contract should be failed (not expired) since it was accepted
	assert_eq(contract.status, Contract.STATUS_FAILED, "Should be failed")
	
	# Worker should NOT get payout for incomplete contract
	assert_eq(worker.money, 0, "Worker should not receive payout for failed contract")

func _test_find_best_contract() -> void:
	var state := Fixtures.make_sim_state()
	var issuer := Fixtures.make_agent({"id": 1, "money": 500})
	var worker := Fixtures.make_agent({"id": 2, "money": 0, "pos_x": 48, "pos_y": 48})
	state.add_agent(issuer)
	state.add_agent(worker)
	
	var contracts := ContractsSystem.new()
	var market := Fixtures.make_market()
	var tuning: Dictionary = state.tuning.duplicate() if state.tuning else {}
	
	# Post multiple contracts
	contracts.post_contract(issuer, "Berries", 5, 20, 100, 48, 48, 0, state)  # Low profit
	contracts.post_contract(issuer, "Logs", 3, 100, 100, 48, 48, 0, state)    # High profit
	contracts.post_contract(issuer, "Ore", 2, 50, 100, 48, 48, 0, state)      # Medium profit
	
	# Find best contract for worker
	var best := contracts.find_best_contract(worker, market, tuning)
	
	assert_true(best != null, "Should find a contract")
	# Best should be the highest profit contract
	assert_eq(best.item, "Logs", "Best contract should be the high-profit Logs contract")

func _test_serialization_roundtrip() -> void:
	var state := Fixtures.make_sim_state()
	var issuer := Fixtures.make_agent({"id": 1, "money": 200})
	state.add_agent(issuer)
	
	var contracts := ContractsSystem.new()
	contracts.post_contract(issuer, "Berries", 5, 30, 100, 48, 48, 10, state)
	contracts.post_contract(issuer, "Logs", 3, 40, 150, 48, 48, 20, state)
	contracts.stats["custom_stat"] = 42
	
	# Serialize
	var dict: Dictionary = contracts.to_dict()
	
	# Deserialize
	var restored: ContractsSystem = ContractsSystem.from_dict(dict)
	
	# Verify
	assert_eq(restored.contracts.size(), 2, "Should have 2 contracts")
	assert_eq(restored.next_contract_id, contracts.next_contract_id, "next_contract_id should match")
	assert_eq(restored.stats["posted"], 2, "posted stat should match")
