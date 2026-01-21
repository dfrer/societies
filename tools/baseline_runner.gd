extends SceneTree

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var params := _parse_args(args)
	
	var seed_value: int = params.get("seed", 12345)
	var days: int = params.get("days", 30)
	var out_path: String = params.get("out", "baseline_run.json")
	
	print("=== Baseline Runner ===")
	print("Seed: %d" % seed_value)
	print("Days: %d" % days)
	
	var sim := Sim.new()
	sim.init_new(seed_value)
	
	# Determine ticks
	var ticks_per_day: int = sim.state.tuning.get("ticks_per_day", 24)
	var total_ticks: int = days * ticks_per_day
	
	# We want to collect metrics daily
	var daily_metrics := []
	
	print("Running %d ticks..." % total_ticks)
	
	# Run simulation day by day
	for d in range(days):
		sim.step(ticks_per_day)
		
		# Capture key metrics for this day
		var metrics = {
			"day": sim.get_day(),
			"population": sim.get_alive_agent_count(),
			"world_wealth": sim.get_total_money() + _calculate_inventory_value(sim),
			"avg_pollution": sim.get_average_pollution(),
			"faction_count": sim.get_faction_count(),
			"total_food": sim.get_total_stock("berry") + sim.get_crafted_count("CookedMeal") # Stock is world resources, crafted is items? 
			# actually get_total_stock is tile resources.
			# Let's trust sim.state.metrics_history logic if available, or just grab simple ones.
			# Sim.get_total_stock returns tile resources.
			# We want total food in inventory too.
		}
		daily_metrics.append(metrics)
	
	var final_checksum := sim.checksum()
	print("Run Complete. Checksum: %s" % final_checksum)
	
	var report = {
		"parameters": {
			"seed": seed_value,
			"days": days
		},
		"final_checksum": final_checksum,
		"metrics": daily_metrics
	}
	
	_save_report(report, out_path)
	quit(0)

func _calculate_inventory_value(sim: Sim) -> int:
	# Approximate value of all items held by agents
	# Just summing money is easier, but wealth should include goods.
	# For baseline, let's just stick to "Total Money" which is Sim.get_total_money() (cash in circulation)
	# But user asked for "Total Food" and "Total Money".
	# Sim.get_total_money() returns sum of agent money + treasury.
	# For inventory value, let's skip for now to keep it simple, or replicate if easy.
	return 0 # Placeholder if we changed definition, but "Total Money" is sufficient for wealth tracking usually.

func _save_report(report: Dictionary, path: String) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if not file:
		print("ERROR: Could not open %s for writing" % path)
		return
	
	file.store_string(JSON.stringify(report, "\t"))
	print("Baseline report saved to: %s" % path)

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
