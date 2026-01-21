extends SceneTree

func _init() -> void:
	print("=== Extended Agent Behavior Analysis ===")
	
	var sim = preload("res://sim/sim.gd").new()
	sim.init_new(12345)
	
	print("\nInitial State:")
	print("Agent Count: ", sim.get_agent_count())
	
	print("\n=== Running 500 Ticks ===")
	var idle_count = 0
	var total_actions = 0
	var action_counts = {}
	
	for tick in range(500):
		sim.step(1)
		
		var agents = sim.state.agents
		for i in range(agents.size()):
			var agent = agents[i]
			if not agent.is_alive():
				continue
			total_actions += 1
			var action_type = agent.current_action.get("type", "IDLE")
			
			if action_counts.has(action_type):
				action_counts[action_type] += 1
			else:
				action_counts[action_type] = 1
			
			if action_type == "IDLE":
				idle_count += 1
		
		if tick % 100 == 0:
			var tick_idle = 0
			var agents_check = sim.state.agents
			for j in range(agents_check.size()):
				var agent_check = agents_check[j]
				if not agent_check.is_alive():
					continue
				if agent_check.current_action.get("type", "") == "IDLE":
					tick_idle += 1
			print("Tick ", tick, ": ", tick_idle, "/", sim.get_alive_agent_count(), " agents idle")
	
	print("\n=== Summary (500 ticks) ===")
	print("Total Actions: ", total_actions)
	print("Idle Actions: ", idle_count)
	print("Idle Percentage: ", float(idle_count) / float(total_actions) * 100.0, "%")
	
	print("\n=== Action Distribution ===")
	for action in action_counts:
		print(action, ": ", action_counts[action], " (", float(action_counts[action]) / float(total_actions) * 100.0, "%)")
	
	print("\n=== Final Agent Details ===")
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
