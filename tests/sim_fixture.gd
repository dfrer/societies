class_name SimFixture
extends RefCounted

# Helper to create a standard sim
static func make_sim(seed_val: int) -> Sim:
	var sim := Sim.new()
	sim.init_new(seed_val)
	return sim

# Helper to claim a rect for an owner
static func claim_rect(sim: Sim, x0: int, y0: int, x1: int, y1: int, owner_id: int) -> void:
	for y in range(y0, y1 + 1):
		for x in range(x0, x1 + 1):
			# Use world.set_claim_owner which takes (x, y, int)
			sim.state.world.set_claim_owner(x, y, owner_id)

# Helper to ensure market claim exists
static func set_market_claim(sim: Sim, owner_id: int) -> void:
	var mx: int = int(sim.state.tuning.get("market_pos_x", 0))
	var my: int = int(sim.state.tuning.get("market_pos_y", 0))
	claim_rect(sim, mx, my, mx, my, owner_id)

# Helper to spawn a resource node
static func spawn_resource_node(sim: Sim, id: String, type: String, x: int, y: int, stock: int, max_stock: int) -> void:
	var node := ResourceNode.new()
	node.id = int(id) if id.is_valid_int() else sim.state.world.next_node_id
	node.type = type
	node.pos_x = x
	node.pos_y = y
	node.stock = stock
	node.max_stock = max_stock
	sim.state.world.add_resource_node(node)

# Helper to move all agents near a point
static func move_agents_near(sim: Sim, x: int, y: int) -> void:
	for agent in sim.state.agents:
		agent.pos_x = x
		agent.pos_y = y

# Helper to force agent stats
static func force_agent_state(agent: Agent, hunger: float = -1.0, risk: float = -1.0, faction_id: int = -1) -> void:
	if hunger >= 0:
		agent.set_hunger(hunger)
	if risk >= 0:
		agent.risk_tolerance = risk
	if faction_id >= 0:
		agent.faction_id = faction_id

