## Crafting - manages crafting jobs and production
class_name Crafting
extends RefCounted

var next_job_id: int = 1
var crafted_counts: Dictionary = {}  # item -> qty crafted total

## CraftJob schema:
## {
##   "job_id": int,
##   "recipe_id": String,
##   "agent_id": int,
##   "workshop_id": int,
##   "progress": int,
##   "total_ticks": int
## }

func _init() -> void:
	pass

## Create a craft job (does not start it)
func create_job(recipe: Recipe, agent_id: int, workshop_id: int) -> Dictionary:
	var job := {
		"job_id": next_job_id,
		"recipe_id": recipe.id,
		"agent_id": agent_id,
		"workshop_id": workshop_id,
		"progress": 0,
		"total_ticks": recipe.ticks
	}
	next_job_id += 1
	return job

## Check if agent can start a recipe (has inputs)
static func can_craft(agent: Agent, recipe: Recipe) -> bool:
	for item in recipe.inputs:
		var required: int = recipe.inputs[item]
		if agent.get_available_item(item) < required:
			return false
	return true

## Consume inputs from agent for a recipe
static func consume_inputs(agent: Agent, recipe: Recipe) -> bool:
	if not can_craft(agent, recipe):
		return false
	for item in recipe.inputs:
		var qty: int = recipe.inputs[item]
		agent.remove_item(item, qty)
	return true

## Give outputs to agent for a recipe
static func give_outputs(agent: Agent, recipe: Recipe) -> void:
	for item in recipe.outputs:
		var qty: int = recipe.outputs[item]
		agent.add_item(item, qty)

## Process all workshops for one tick
func process_workshops(workshops: Array, agents: Array, recipes: Dictionary) -> void:
	var agent_map := {}
	for agent in agents:
		agent_map[agent.id] = agent
	
	for workshop in workshops:
		if not workshop.is_ready():
			# Process construction
			workshop.build_progress += 1
			if workshop.build_progress >= workshop.build_total:
				workshop.is_built = true
			continue
		
		if workshop.queue.is_empty():
			continue
		
		# Progress the first job
		var job: Dictionary = workshop.queue[0]
		job["progress"] += 1
		
		# Check if complete
		if job["progress"] >= job["total_ticks"]:
			var agent = agent_map.get(job["agent_id"])
			var recipe_id: String = job["recipe_id"]
			var recipe: Recipe = recipes.get(recipe_id)
			
			if agent and recipe:
				# Give outputs
				give_outputs(agent, recipe)
				
				# Record stats
				for item in recipe.outputs:
					var qty: int = recipe.outputs[item]
					crafted_counts[item] = crafted_counts.get(item, 0) + qty
			
			# Remove completed job
			workshop.pop_job()

## Get crafted count for an item
func get_crafted_count(item: String) -> int:
	return crafted_counts.get(item, 0)

## Get total active jobs across all workshops
static func get_active_job_count(workshops: Array) -> int:
	var count := 0
	for workshop in workshops:
		count += workshop.queue.size()
	return count

## Serialize to dictionary
func to_dict() -> Dictionary:
	return {
		"next_job_id": next_job_id,
		"crafted_counts": crafted_counts.duplicate()
	}

## Deserialize from dictionary
static func from_dict(d: Dictionary) -> Crafting:
	var crafting := Crafting.new()
	crafting.next_job_id = int(d.get("next_job_id", 1))
	# Convert crafted_counts values to int
	var cc_data: Dictionary = d.get("crafted_counts", {})
	crafting.crafted_counts = {}
	for key in cc_data:
		crafting.crafted_counts[key] = int(cc_data[key])
	return crafting
