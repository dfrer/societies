extends SceneTree

func _init() -> void:
	print("=== Final Agent Behavior Analysis ===")
	
	var sim = preload("res://sim/sim.gd").new()
	sim.init_new(12345)
	
	# Run simulation and analyze patterns
	print("\nRunning 200 ticks...")
	var action_history = {}
	
	for tick in range(200):
		sim.step(1)
		
		if tick % 50 == 0:
			print("\n=== Tick ", tick, " ===")
			var workshop = sim.state.world.workshops[0]
			print("Workshop Queue Size: ", workshop.queue.size())
			print("Workshop Built: ", workshop.is_built)
			
			var deadlocked = 0
			var total_agents = 0
			var goal_stacks = {}
			
			var agents = sim.state.agents
			for i in range(agents.size()):
				var agent = agents[i]
				if not agent.is_alive():
					continue
				total_agents += 1
				
				var action = agent.current_action.get("type", "IDLE")
				if not action_history.has(action):
					action_history[action] = 0
				action_history[action] += 1
				
				# Check for stuck agents
				if agent.goal_stack.size() == 0:
					deadlocked += 1
				elif agent.goal_stack.size() > 2:
					var top_goal = agent.goal_stack.back().get("type", "NONE")
					if not goal_stacks.has(top_goal):
						goal_stacks[top_goal] = 0
					goal_stacks[top_goal] += 1
			
			print("Agents with no goals: ", deadlocked, "/", total_agents)
			print("Goal Distribution:")
			for goal_type in goal_stacks:
				print("  ", goal_type, ": ", goal_stacks[goal_type])
	
	print("\n=== Final Action Distribution (200 ticks) ===")
	for action in action_history:
		var percentage = float(action_history[action]) / float(200 * sim.get_alive_agent_count()) * 100.0
		print(action, ": ", action_history[action], " (", percentage, "%)")
	
	# Look at recipe chain
	print("\n=== Recipe Chain Analysis ===")
	var recipes = sim.state.recipes
	print("Available Recipes:")
	for recipe_id in recipes:
		var recipe = recipes[recipe_id]
		print("  ", recipe_id, " -> ", recipe.outputs, " (needs: ", recipe.inputs, ") at ", recipe.station)
	
	quit(0)
