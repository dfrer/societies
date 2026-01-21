## Headless runner - CLI tool to run simulation without GUI
extends SceneTree

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var params := _parse_args(args)
	
	var seed_value: int = params.get("seed", 42)
	var out_path: String = params.get("out", "")
	
	var tick_count: int
	if params.has("days"):
		tick_count = params.get("days", 10) * 24
	else:
		tick_count = params.get("ticks", 100)
	
	var sim := Sim.new()
	sim.init_new(seed_value)
	
	if params.has("days"):
		var ticks_per_day: int = sim.state.tuning.get("ticks_per_day", 24)
		tick_count = params.get("days", 10) * ticks_per_day
	
	var ticks_per_day: int = sim.state.tuning.get("ticks_per_day", 24)
	
	print("=== Societies Simulation ===")
	print("Seed: %d" % seed_value)
	print("Running %d ticks (%d days)..." % [tick_count, tick_count / ticks_per_day])
	
	sim.step(tick_count)
	
	print("")
	print("=== Results ===")
	print("Ticks: %d | Days: %d" % [sim.get_tick(), sim.get_day()])
	print("Agents: %d total, %d alive" % [sim.get_agent_count(), sim.get_alive_agent_count()])
	print("Average hunger: %.2f" % sim.get_average_hunger())
	print("")
	print("=== Factions ===")
	print("Total factions: %d" % sim.get_faction_count())
	if sim.get_faction_count() > 0:
		var factions: Array = sim.get_factions()
		# Sort by member count descending
		var faction_list := []
		for faction in factions:
			faction_list.append({
				"faction": faction,
				"members": faction.get_member_count()
			})
		faction_list.sort_custom(func(a, b): return a["members"] > b["members"])
		print("Top factions:")
		for i in range(mini(5, faction_list.size())):
			var f = faction_list[i]["faction"]
			var claims := sim.get_faction_claims_count(f.id)
			var owner_id := World.owner_id_for_faction(f.id)
			var laws: Laws = sim.state.get_laws(owner_id)
			# Get factionless policy
			var factionless_policy: Dictionary = f.get_trade_policy_for_agent(
				sim.state.agents[0] if sim.state.agents.size() > 0 and sim.state.agents[0].faction_id == 0 else null,
				sim.state.tuning) if sim.state.agents.size() > 0 else {"policy": "open", "tariff_rate": 0}
			# Check if any agent is factionless to get real policy
			var fl_policy := "open"
			var fl_tariff := 0
			if f.relations.has("faction:0"):
				fl_policy = f.relations["faction:0"]["policy"]
				fl_tariff = f.relations["faction:0"]["tariff_rate"]
			print("  %s (ID %d): %d members, treasury %d, claims %d, tax %d%%, factionless: %s/%d%%" % [
				f.name, f.id, f.get_member_count(), f.treasury, claims, laws.sales_tax_rate,
				fl_policy, fl_tariff])
	print("Proposals: %d active, %d resolved" % [
		sim.get_active_proposals_count(), sim.get_resolved_proposals_count()])
	print("Taxes collected: %d | Tariffs collected: %d | Embargo denials: %d" % [
		sim.get_taxes_collected(), sim.state.market.tariff_collected_total, 
		sim.state.market.orders_denied_embargo])
	print("")
	print("=== Land & Enforcement ===")
	print("Claimed tiles: %d" % sim.get_claimed_tiles_count())
	var claims: Dictionary = sim.get_claims_by_owner()
	if claims.size() > 0:
		print("Top claimants:")
		var claim_list := []
		for owner_id in claims:
			claim_list.append([owner_id, claims[owner_id]])
		claim_list.sort_custom(func(a, b): return a[1] > b[1])
		for i in range(mini(5, claim_list.size())):
			var owner_id = claim_list[i][0]
			var owner_name := ""
			if World.is_faction_owner(owner_id):
				owner_name = "Faction %d" % World.faction_id_from_owner(owner_id)
			else:
				owner_name = "Agent %d" % owner_id
			print("  %s: %d tiles" % [owner_name, claim_list[i][1]])
	print("Violations detected: %d" % sim.get_violations_detected())
	print("Fines collected: %d" % sim.get_fines_collected())
	print("Banned agents: %d" % sim.get_banned_agents_count())
	print("")
	print("=== Contracts ===")
	print("Posted: %d | Accepted: %d | Completed: %d | Expired: %d" % [
		sim.get_contracts_posted(), sim.get_contracts_accepted(),
		sim.get_contracts_completed(), sim.get_contracts_expired()])
	print("Total escrow: %d" % sim.get_total_escrow())
	print("")
	print("=== Workshops & Crafting ===")
	print("Workshops: %d | Active jobs: %d" % [sim.get_workshop_count(), sim.get_active_job_count()])
	print("Crafted: Planks=%d CookedMeal=%d MetalIngot=%d" % [
		sim.get_crafted_count("Planks"), sim.get_crafted_count("CookedMeal"),
		sim.get_crafted_count("MetalIngot")])
	print("")
	print("=== Economy ===")
	print("Total money: %d | Total trades: %d" % [sim.get_total_money(), sim.get_total_trades()])
	print("")
	print("=== World Resources ===")
	print("Berry: %d | Tree: %d | Ore: %d" % [
		sim.get_total_stock("berry"), sim.get_total_stock("tree"), sim.get_total_stock("ore")])
	print("Pollution: %.4f" % sim.get_average_pollution())
	print("")
	print("Checksum: %s" % sim.checksum())
	
	if out_path != "":
		if sim.save_json(out_path):
			print("Saved state to: %s" % out_path)
		else:
			print("ERROR: Failed to save state")
	
	var metrics_path: String = params.get("metrics_out", "")
	if metrics_path != "":
		_export_metrics(sim.state.metrics_history, metrics_path)
	
	_report_last_day_metrics(sim)
	_detect_collapse(sim)
	
	print("")
	quit(0)

func _parse_args(args: PackedStringArray) -> Dictionary:
	var result := {}
	for arg in args:
		if arg.begins_with("--"):
			var parts := arg.substr(2).split("=", true, 1)
			if parts.size() == 2:
				var key := parts[0]
				var value := parts[1]
				if value.is_valid_int():
					result[key] = value.to_int()
				else:
					result[key] = value
			else:
				result[parts[0]] = true
	return result

func _export_metrics(history: Array, path: String) -> void:
	if history.is_empty():
		return
	var file := FileAccess.open(path, FileAccess.WRITE)
	if not file:
		print("ERROR: Could not open %s for writing" % path)
		return
	
	# Headers
	var keys = history[0].keys()
	var header := ""
	for i in range(keys.size()):
		header += keys[i] + ("," if i < keys.size() - 1 else "")
	file.store_line(header)
	
	# Data
	for snapshot in history:
		var line := ""
		for i in range(keys.size()):
			line += str(snapshot[keys[i]]) + ("," if i < keys.size() - 1 else "")
		file.store_line(line)
	
	file.close()
	print("Metrics exported to: %s" % path)

func _report_last_day_metrics(sim: Sim) -> void:
	if sim.state.metrics_history.is_empty():
		return
	var last = sim.state.metrics_history.back()
	print("=== Last Day Metrics ===")
	print("Day %d: Pollution %.4f, Berries %d, Avg Hunger %.1f, Starving %d" % [
		last["day"], last["pollution"], last["berry_stock_total"], 
		last["avg_hunger"], last["starving_agents"]
	])

func _detect_collapse(sim: Sim) -> void:
	var history: Array = sim.state.metrics_history
	if history.size() < 2: return
	
	var tuning: Dictionary = sim.state.tuning
	var window: int = tuning.get("collapse_days_window", 5)
	var starvation_ratio: float = tuning.get("starvation_collapse_ratio", 0.2)
	var inflation_threshold: float = tuning.get("inflation_threshold", 50.0)
	var pollution_threshold: float = tuning.get("pollution_collapse_threshold", 0.8)
	
	if history.size() < window: return
	
	var collapse_starvation := true
	var collapse_inflation := true
	var collapse_pollution := true
	
	for i in range(history.size() - window, history.size()):
		var snapshot = history[i]
		var agent_count = sim.get_agent_count()
		if agent_count > 0:
			if float(snapshot["starving_agents"]) / agent_count < starvation_ratio:
				collapse_starvation = false
		else:
			collapse_starvation = false
			
		if snapshot["ref_price_food"] < inflation_threshold:
			collapse_inflation = false
		if snapshot["pollution"] < pollution_threshold:
			collapse_pollution = false
	
	if collapse_starvation or collapse_inflation or collapse_pollution:
		print("!!! STABILITY ALERT: COLLAPSE DETECTED !!!")
		if collapse_starvation: print("  - Massive starvation for %d consecutive days" % window)
		if collapse_inflation: print("  - Hyperinflation in food prices for %d consecutive days" % window)
		if collapse_pollution: print("  - Ecological collapse: High pollution for %d consecutive days" % window)
		print("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")
	else:
		print("Stability: Within normal limits.")
