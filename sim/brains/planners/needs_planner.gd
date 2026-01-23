class_name NeedsPlanner
extends "res://sim/brains/planners/i_agent_planner.gd"

# Needs Planner
# Handles survival needs (Hunger, Stamina) and comfort/social needs.
# operates in two modes:
# 1. Critical Interrupts: Bypasses goals for immediate survival (Starvation/Exhaustion)
# 2. Goal Generation: Adds goals for hungry/tired states or proactive buffering

func get_priority() -> int:
	return 100 # Priority.CRITICAL_NEEDS - usually high

# --- Critical Interrupts ---
# Returns an Action if immediate survival action is needed, bypassing planner
func get_interrupt_action(agent, world) -> Dictionary:
	# 1. Starvation Check
	if agent.state.hunger < 15.0:
		# If has food, eat immediately
		if agent.has_item_category("food"):
			# Find best food
			var food = agent.inventory.get_best_item("food") # Assuming this helper exists or generic get
			# Fallback if get_best_item not implemented, exact search
			if not food:
				if agent.inventory.has("CookedMeal"): food = "CookedMeal"
				elif agent.inventory.has("Berries"): food = "Berries"
			
			if food:
				return {
					"type": "eat",
					"item": food,
					"interrupt": true
				}
	
	# 2. Exhaustion Check
	if agent.state.stamina <= 0.0:
		# Sleep immediately where you stand
		return {
			"type": "sleep",
			"interrupt": true
		}
		
	return {} # No interrupt

# --- Goal Generation ---
func maybe_add_goal(agent, ctx) -> bool:
	var tuning = ctx.tuning
	
	# 1. Reactive Hunger
	if agent.state.hunger < 50.0:
		# Low hunger: Need food logic
		# If user has food -> EAT_FOOD goal
		# If user has no food -> OBTAIN_ITEM goal
		var has_food = false
		if agent.inventory.has("Berries") or agent.inventory.has("CookedMeal"):
			has_food = true
			
		if has_food:
			agent.goals.push_front({
				"type": "EAT_FOOD",
				"is_goal": true
			})
			return true
		else:
			# Need to find food.
			# Simplified: Just ask for Berries for now, V3 spec might want generic "food"
			agent.goals.push_front({
				"type": "OBTAIN_ITEM",
				"item": "Berries",
				"qty": 5,
				"is_goal": true
			})
			return true

	# 2. Reactive Stamina
	if agent.state.stamina < 20.0:
		# Need rest.
		# Check for shelter preference (P1b)
		var shelter_id = -1
		if agent.has_method("has_personal_shelter") and agent.has_personal_shelter():
			shelter_id = agent.shelter_id
		else:
			# Look for nearby public shelter
			# This requires spatial query. For now, basic REST.
			pass
			
		# If shelter found, EFFICIENT_REST, else REST
		if shelter_id != -1:
			agent.goals.push_front({
				"type": "EFFICIENT_REST",
				"shelter_id": shelter_id,
				"is_goal": true
			})
		else:
			agent.goals.push_front({
				"type": "REST",
				"is_goal": true
			})
		return true

	# 3. Proactive Food Buffer (P1a)
	# If hunger is fine (>50) but inventory low
	var food_count = 0
	food_count += agent.inventory.count("Berries")
	food_count += agent.inventory.count("CookedMeal")
	
	var buffer_target = tuning.get("proactive_food_buffer", 5)
	
	if food_count < buffer_target:
		agent.goals.push_front({
			"type": "MAINTAIN_FOOD_BUFFER",
			"target_qty": buffer_target,
			"current_qty": food_count,
			"is_goal": true
		})
		return true

	return false
