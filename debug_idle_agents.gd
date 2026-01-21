## Debug test to analyze agent idle behavior
extends SceneTree

func _init() -> void:
	print("=== Agent Idle Behavior Analysis ===")
	
	# Create simulation with deterministic seed
	var sim = preload("res://sim/sim.gd").new()
	sim.init_new(12345)
	
	print("\nInitial State:")
	print("Agent Count: ", sim.get_agent_count())
	print("World Size: ", sim.state.world.width, "x", sim.state.world.height)
	print("Berry Nodes: ", sim.state.world.get_total_stock("berry"))
	print("Tree Nodes: ", sim.state.world.get_total_stock("tree"))
	print("Ore Nodes: ", sim.state.world.get_total_stock("ore"))
	
	# Run for 50 ticks and analyze behavior
	print("\n=== Running 50 Ticks ===")
	var idle_count = 0
	var total_actions = 0
	
	for tick in range(50):
		sim.step(1)
		
		# Count idle actions this tick
		var tick_idle = 0
		for agent in sim.state.agents:
			if not agent.is_alive():
				continue
			total_actions += 1
			if agent.current_action.get("type", "") == "IDLE":
				idle_count += 1
				tick_idle += 1
		
		if tick % 10 == 0:
			print("Tick ", tick, ": ", tick_idle, "/", sim.get_alive_agent_count(), " agents idle")
	
	print("\n=== Summary ===")
	print("Total Actions: ", total_actions)
	print("Idle Actions: ", idle_count)
	print("Idle Percentage: ", float(idle_count) / float(total_actions) * 100.0, "%")
	
	print("\n=== Agent Details ===")
	for agent in sim.state.agents:
		if not agent.is_alive():
			continue
			
		print("\nAgent ", agent.id, " (", agent.role, "):")
		print("  Position: (", agent.pos_x, ", ", agent.pos_y, ")")
		print("  Hunger: ", agent.get_hunger())
		print("  Stamina: ", agent.get_stamina())
		print("  Money: ", agent.money)
		print("  Current Action: ", agent.current_action.get("type", "NONE"))
		print("  Goal Stack: ", agent.goal_stack.size(), " goals")
		
		if agent.goal_stack.size() > 0:
			var top_goal = agent.goal_stack.back()
			print("  Top Goal: ", top_goal.get("type", "NONE"))
		
		print("  Inventory:")
		for item in agent.inventory:
			print("    ", item, ": ", agent.inventory[item])
		
		print("  Personality:")
		for trait in agent.personality:
			print("    ", trait, ": ", agent.personality[trait])
	
	quit(0)
