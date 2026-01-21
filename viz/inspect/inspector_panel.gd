class_name InspectorPanel
extends PanelContainer

## Tabbed inspector panel - shows Tile, Node, or Agent details based on selection.

@onready var tab_container: TabContainer = $MarginContainer/TabContainer
@onready var tile_inspector: TileInspector = $MarginContainer/TabContainer/Tile
@onready var node_inspector: NodeInspector = $MarginContainer/TabContainer/Node
@onready var agent_inspector: AgentInspector = $MarginContainer/TabContainer/Agent


func _ready() -> void:
	# Start with Tile tab active
	tab_container.current_tab = 0


## Update inspector based on tile selection
func update_selection(tile: Vector2i, sim_state: SimState) -> void:
	if sim_state == null:
		return
	
	var world: World = sim_state.world
	var owner_id: int = world.get_claim_owner(tile.x, tile.y)
	
	# Determine owner type
	var owner_type: String = "Unclaimed"
	if owner_id > 0:
		if World.is_faction_owner(owner_id):
			owner_type = "Faction"
		else:
			owner_type = "Agent"
	
	# Get laws for this jurisdiction
	var laws: Laws = sim_state.get_laws(owner_id)
	
	# Update tile inspector
	tile_inspector.update_tile(tile, owner_id, laws, owner_type)
	
	# Check for resource node at tile
	var resource_node: ResourceNode = world.get_node_at(tile.x, tile.y)
	var workshop: Workshop = world.get_workshop_at(tile.x, tile.y)
	
	if resource_node != null:
		node_inspector.update_resource_node(resource_node)
		# Auto-switch to Node tab when node is present
		tab_container.current_tab = 1
	elif workshop != null:
		node_inspector.update_workshop(workshop)
		# Auto-switch to Node tab when workshop is present
		tab_container.current_tab = 1
	else:
		node_inspector.clear()
		# Stay on Tile tab if no node
		tab_container.current_tab = 0



## Update inspector with selected agent
func update_agent_selection(agent_id: int, sim_state: SimState) -> void:
	if sim_state == null or agent_id < 0:
		agent_inspector.clear()
		return
	
	agent_inspector.update_agent(agent_id, sim_state)
	# Switch to Agent tab
	tab_container.current_tab = 2


## Get current agent target position for line drawing
func get_agent_target_position() -> Vector2i:
	return agent_inspector.get_agent_target_position()


## Clear the inspector (no selection)
func clear() -> void:
	tile_inspector.clear()
	node_inspector.clear()
	agent_inspector.clear()
	tab_container.current_tab = 0
