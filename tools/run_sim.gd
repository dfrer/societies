## run_sim.gd - Canonical headless simulation runner
## Usage: godot --headless --script res://tools/run_sim.gd -- --seed=123 --days=10 --out=artifacts/run.json
extends SceneTree

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var params := _parse_args(args)
	
	var seed_value: int = params.get("seed", 42)
	var out_path: String = params.get("out", "")
	
	var tick_count: int
	if params.has("days"):
		tick_count = params.get("days", 10) * 24  # Default ticks_per_day
	else:
		tick_count = params.get("ticks", 100)
	
	var sim := Sim.new()
	sim.init_new(seed_value)
	
	# Recalculate tick_count with actual ticks_per_day from tuning
	var ticks_per_day: int = sim.state.tuning.get("ticks_per_day", 24)
	if params.has("days"):
		tick_count = params.get("days", 10) * ticks_per_day
	
	print("=== Societies Headless Simulation ===")
	print("Seed: %d" % seed_value)
	print("Running %d ticks (%d days)..." % [tick_count, tick_count / ticks_per_day])
	print("")
	
	sim.step(tick_count)
	
	_print_summary(sim, ticks_per_day)
	_detect_collapse(sim)
	
	if out_path != "":
		if sim.save_json(out_path):
			print("State saved to: %s" % out_path)
		else:
			print("ERROR: Failed to save state to: %s" % out_path)
	
	var metrics_path: String = params.get("metrics_out", "")
	if metrics_path != "":
		_export_metrics(sim.state.metrics_history, metrics_path)
	
	print("")
	quit(0)

func _print_summary(sim: Sim, ticks_per_day: int) -> void:
	print("=== Results ===")
	print("Ticks: %d | Days: %d" % [sim.get_tick(), sim.get_tick() / ticks_per_day])
	print("Agents: %d total, %d alive" % [sim.get_agent_count(), sim.get_alive_agent_count()])
	print("Average hunger: %.2f" % sim.get_average_hunger())
	print("")
	
	print("=== Economy ===")
	print("Total money: %d | Total trades: %d" % [sim.get_total_money(), sim.get_total_trades()])
	print("Taxes collected: %d | Tariffs: %d" % [sim.get_taxes_collected(), sim.state.market.tariff_collected_total])
	print("")
	
	print("=== Factions ===")
	print("Total factions: %d" % sim.get_faction_count())
	if sim.get_faction_count() > 0:
		var factions: Array = sim.get_factions()
		var faction_list := []
		for faction in factions:
			faction_list.append({"faction": faction, "members": faction.get_member_count()})
		faction_list.sort_custom(func(a, b): return a["members"] > b["members"])
		print("Top factions:")
		for i in range(mini(3, faction_list.size())):
			var f = faction_list[i]["faction"]
			print("  %s (ID %d): %d members, treasury %d" % [f.name, f.id, f.get_member_count(), f.treasury])
	print("")
	
	print("=== World Resources ===")
	print("Berry: %d | Tree: %d | Ore: %d" % [
		sim.get_total_stock("berry"), sim.get_total_stock("tree"), sim.get_total_stock("ore")])
	print("Pollution: %.4f" % sim.get_average_pollution())
	print("")
	
	print("=== Enforcement ===")
	print("Violations: %d | Fines collected: %d | Banned agents: %d" % [
		sim.get_violations_detected(), sim.get_fines_collected(), sim.get_banned_agents_count()])
	print("")
	
	print("Checksum: %s" % sim.checksum())

func _detect_collapse(sim: Sim) -> void:
	var history: Array = sim.state.metrics_history
	if history.size() < 2:
		print("Stability: Insufficient data")
		return
	
	var tuning: Dictionary = sim.state.tuning
	var window: int = tuning.get("collapse_days_window", 5)
	var starvation_ratio: float = tuning.get("starvation_collapse_ratio", 0.2)
	var pollution_threshold: float = tuning.get("pollution_collapse_threshold", 0.8)
	
	if history.size() < window:
		print("Stability: Within normal limits")
		return
	
	var collapse_starvation := true
	var collapse_pollution := true
	
	for i in range(history.size() - window, history.size()):
		var snapshot = history[i]
		var agent_count = sim.get_agent_count()
		if agent_count > 0:
			if float(snapshot.get("starving_agents", 0)) / agent_count < starvation_ratio:
				collapse_starvation = false
		else:
			collapse_starvation = false
		if snapshot.get("pollution", 0.0) < pollution_threshold:
			collapse_pollution = false
	
	if collapse_starvation or collapse_pollution:
		print("!!! STABILITY ALERT: COLLAPSE DETECTED !!!")
		if collapse_starvation:
			print("  - Massive starvation for %d consecutive days" % window)
		if collapse_pollution:
			print("  - Ecological collapse: High pollution for %d consecutive days" % window)
	else:
		print("Stability: Within normal limits")

func _export_metrics(history: Array, path: String) -> void:
	if history.is_empty():
		return
	var file := FileAccess.open(path, FileAccess.WRITE)
	if not file:
		print("ERROR: Could not open %s for writing" % path)
		return
	
	var keys = history[0].keys()
	var header := ""
	for i in range(keys.size()):
		header += keys[i] + ("," if i < keys.size() - 1 else "")
	file.store_line(header)
	
	for snapshot in history:
		var line := ""
		for i in range(keys.size()):
			line += str(snapshot[keys[i]]) + ("," if i < keys.size() - 1 else "")
		file.store_line(line)
	
	file.close()
	print("Metrics exported to: %s" % path)

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
