## agent_observer.gd - Runs a short sim and prints a detailed activity log
## Usage: godot --headless --script res://tools/agent_observer.gd -- --ticks=24
extends SceneTree

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var params := _parse_args(args)
	
	var ticks: int = params.get("ticks", 48)
	var filter_agent: int = params.get("agent", -1)
	var interval: int = params.get("interval", 1)
	var out_path: String = params.get("out", "")
	
	var file: FileAccess = null
	if out_path != "":
		file = FileAccess.open(out_path, FileAccess.WRITE)
		if file == null:
			print("Error opening file: " + out_path)
			return

	var log_msg = func(msg: String):
		print(msg)
		if file:
			file.store_line(msg)
	
	log_msg.call("=== Agent Observer Tool ===")
	log_msg.call("Running for %d ticks (Interval: %d)..." % [ticks, interval])
	
	var sim := Sim.new()
	sim.init_new(42) # Fixed seed
	
	for i in range(ticks):
		sim.step(1)
		var current_tick = sim.get_tick()
		
		if current_tick % interval == 0:
			# Events
			var events = sim.state.events
			var relevant_events = []
			for e in events:
				if e.tick == current_tick:
					if filter_agent == -1 or _event_involves_agent(e, filter_agent):
						relevant_events.append(e)
			
			if not relevant_events.is_empty():
				log_msg.call("\n--- Tick %d Events ---" % current_tick)
				for e in relevant_events:
					log_msg.call("[%s] %s" % [e.type.to_upper(), str(e.data)])
			
			# Activities
			for agent in sim.state.agents:
				if not agent.is_alive(): continue
				if filter_agent != -1 and agent.id != filter_agent: continue
				
				var action = agent.current_action
				var action_type = action.get("type", "IDLE")
				var details = ""
				match action_type:
					"MOVE_TO_NODE": details = "Node %d" % action.get("node_id")
					"MOVE_TO_MARKET": details = "Market"
					"GATHER_NODE": details = "Node %d" % action.get("node_id")
					"PLACE_BUY_ORDER": details = "%s" % action.get("item")
					"PLACE_SELL_ORDER": details = "%s" % action.get("item")
					"EAT": details = "Berries"
					"EAT_MEAL": details = "Meal"
					"REST": details = "Resting"
					"SLEEP": details = "Sleeping"
					_: details = str(action)
				
				var status = "Hunger: %.1f Stamina: %.1f Money: %d" % [
					agent.get_hunger(), agent.get_stamina(), agent.get_available_money()
				]
				log_msg.call("Tick %d | Agent %d | %s (%s) | %s" % [
					current_tick, agent.id, action_type, details, status
				])
				
	log_msg.call("===========================")
	log_msg.call("Observation Complete")
	if file: file.close()
	quit(0)
	
# Helper stubs to make the above valid without redeclaring everything
func _event_involves_agent(event: Dictionary, agent_id: int) -> bool:
	var data = event.get("data", {})
	if data.get("agent_id") == agent_id: return true
	if data.get("buyer_id") == agent_id: return true
	if data.get("seller_id") == agent_id: return true
	return false



func _parse_args(args: PackedStringArray) -> Dictionary:
	var result := {}
	for arg in args:
		if arg.begins_with("--"):
			var parts := arg.substr(2).split("=", true, 1)
			if parts.size() == 2:
				var val = parts[1]
				if val.is_valid_int():
					result[parts[0]] = val.to_int()
				else:
					result[parts[0]] = val
	return result
