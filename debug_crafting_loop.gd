extends SceneTree

func _init() -> void:
	print("=== Crafting Loop Analysis ===")
	
	var sim = preload("res://sim/sim.gd").new()
	sim.init_new(12345)
	
	print("\n=== Analyzing Crafting Behavior ===")
	
	# Let's trace a few agents' goal progression
	var target_agents = [1, 2, 5]
	
	for tick in range(100):
		sim.step(1)
		
		if tick % 20 == 0:
			print("\n--- Tick ", tick, " ---")
			for agent_id in target_agents:
				var agent = sim.state.agents[agent_id - 1]  # 0-based index
				if not agent.is_alive():
					continue
					
				print("Agent ", agent_id, ": ", agent.current_action.get("type", "NONE"))
				print("  Hunger: ", agent.get_hunger(), " Stamina: ", agent.get_stamina())
				print("  Goal Stack Size: ", agent.goal_stack.size())
				
				if agent.goal_stack.size() > 0:
					print("  Top Goal: ", agent.goal_stack.back().get("type", "NONE"))
					var top_goal = agent.goal_stack.back()
					if top_goal.has("item"):
						print("  Target Item: ", top_goal.item, " x", top_goal.get("qty", 1))
				
				print("  Inventory:")
				for item in agent.inventory:
					print("    ", item, ": ", agent.inventory[item])
	
	print("\n=== Workshop Analysis ===")
	var workshops = sim.state.world.workshops
	for i in range(workshops.size()):
		var workshop = workshops[i]
		print("Workshop ", workshop.id, " at (", workshop.pos_x, ", ", workshop.pos_y, ")")
		print("  Queue: ", workshop.job_queue.size(), " jobs")
		print("  Built: ", workshop.is_built)
		print("  Ready: ", workshop.is_ready())
	
	print("\n=== Recipe Analysis ===")
	var recipes = sim.state.recipes
	for recipe_id in recipes:
		var recipe = recipes[recipe_id]
		print("Recipe ", recipe_id, ":")
		print("  Station: ", recipe.station)
		print("  Inputs: ", recipe.inputs)
		print("  Outputs: ", recipe.outputs)
	
	quit(0)
