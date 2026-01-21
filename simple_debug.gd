extends SceneTree

func _init() -> void:
	print("=== Agent Idle Behavior Analysis ===")
	
	var sim = preload("res://sim/sim.gd").new()
	sim.init_new(12345)
	
	print("\nInitial State:")
	print("Agent Count: ", sim.get_agent_count())
	
	print("\n=== Running 50 Ticks ===")
	var idle_count = 0
	var total_actions = 0
	
	for tick in range(50):
		sim.step(1)
		
		var tick_idle = 0
		var agents = sim.state.agents
		for i in range(agents.size()):
			var agent = agents[i]
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
	var agents = sim.state.agents
	for i in range(agents.size()):
		var agent = agents[i]
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
	
	quit(0)
