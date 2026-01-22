class_name AgentInspector
extends VBoxContainer

## Agent inspector - displays full agent state.

var _sim_state: SimState = null
var _current_agent_id: int = -1

# UI Elements - created dynamically
var _scroll_container: ScrollContainer
var _content: VBoxContainer

# Section labels
var _header_label: Label
var _id_label: Label
var _pos_label: Label
var _alive_label: Label
var _money_label: Label
var _inventory_label: Label
var _hunger_label: Label
var _role_label: Label
var _skills_label: Label
var _traits_label: Label
var _faction_label: Label
var _market_ban_label: Label
var _action_label: Label
var _target_label: Label
var _contract_label: Label


func _ready() -> void:
	_setup_ui()
	clear()


func _setup_ui() -> void:
	# Remove placeholder if it exists
	for child in get_children():
		child.queue_free()
	
	# Header
	_header_label = Label.new()
	_header_label.text = "Agent Inspector"
	_header_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_header_label.add_theme_font_size_override("font_size", 16)
	add_child(_header_label)
	
	# Separator
	var sep := HSeparator.new()
	add_child(sep)
	
	# Scroll container for content
	_scroll_container = ScrollContainer.new()
	_scroll_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_scroll_container.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	add_child(_scroll_container)
	
	# Content container
	_content = VBoxContainer.new()
	_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_content.add_theme_constant_override("separation", 2)
	_scroll_container.add_child(_content)
	
	# Create labels
	_id_label = _create_label("ID: --")
	_pos_label = _create_label("Position: --")
	_alive_label = _create_label("Status: --")
	
	_content.add_child(HSeparator.new())
	
	_money_label = _create_label("Money: --")
	_inventory_label = _create_label("Inventory: --")
	
	_content.add_child(HSeparator.new())
	
	_hunger_label = _create_label("Hunger: --")
	_role_label = _create_label("Role: --")
	_skills_label = _create_label("Skills: --")
	_traits_label = _create_label("Traits: --")
	
	_content.add_child(HSeparator.new())
	
	_faction_label = _create_label("Faction: --")
	_market_ban_label = _create_label("Market Ban: --")
	
	_content.add_child(HSeparator.new())
	
	_action_label = _create_label("Action: --")
	_target_label = _create_label("Target: --")
	_contract_label = _create_label("Contract: --")


func _create_label(text: String) -> Label:
	var label := Label.new()
	label.text = text
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_content.add_child(label)
	return label


## Update with agent data
func update_agent(agent_id: int, sim_state: SimState) -> void:
	if sim_state == null:
		clear()
		return
	
	_sim_state = sim_state
	_current_agent_id = agent_id
	
	var agent: Agent = sim_state.get_agent(agent_id)
	if agent == null:
		clear()
		return
	
	# Basic info
	_id_label.text = "ID: %d" % agent.id
	_pos_label.text = "Position: (%d, %d)" % [agent.pos_x, agent.pos_y]
	_alive_label.text = "Status: %s" % ("Alive" if agent.is_alive() else "Dead")
	
	# Money
	if agent.locked_money > 0:
		_money_label.text = "Money: %d (%d locked)" % [agent.money, agent.locked_money]
	else:
		_money_label.text = "Money: %d" % agent.money
	
	# Inventory
	_inventory_label.text = _format_inventory(agent)
	_inventory_label.tooltip_text = _format_inventory_tooltip(agent)
	
	# Needs & Traits
	_hunger_label.text = "Hunger: %.1f" % agent.get_hunger()
	_role_label.text = "Role: %s" % agent.role
	_skills_label.text = "Skills: %s" % _format_skills(agent.skills)
	_traits_label.text = "Risk: %.2f | Eco: %.2f | Griev: %.2f" % [
		agent.risk_tolerance, agent.eco_concern, agent.grievance
	]
	
	# Faction & Market
	_faction_label.text = _format_faction(agent, sim_state)
	_market_ban_label.text = _format_market_ban(agent, sim_state.tick)
	
	# Current action
	_action_label.text = "Action: %s" % _format_action(agent.current_action)
	_action_label.tooltip_text = _format_action_tooltip(agent.current_action)
	_target_label.text = _format_target(agent, sim_state)
	_contract_label.text = _format_contract(agent, sim_state)
	_contract_label.tooltip_text = _format_contract_tooltip(agent, sim_state)


func _format_inventory(agent: Agent) -> String:
	if agent.inventory.is_empty():
		return "Inventory: (empty)"
	
	var parts := []
	for item in agent.inventory:
		var qty: int = agent.inventory[item]
		var locked: int = agent.locked_inventory.get(item, 0)
		if locked > 0:
			parts.append("%s: %d (%d locked)" % [item, qty, locked])
		else:
			parts.append("%s: %d" % [item, qty])
	
	return "Inventory:\n  " + "\n  ".join(parts)


func _format_inventory_tooltip(agent: Agent) -> String:
	if _sim_state == null or agent.inventory.is_empty():
		return ""
	
	var items := agent.inventory.keys()
	items.sort()
	var lines := []
	for item in items:
		var description := _get_item_description(item)
		if description == "":
			continue
		lines.append("%s (%d): %s" % [item, agent.inventory[item], description])
	
	return "\n".join(lines)


func _format_action_tooltip(action: Dictionary) -> String:
	var item_id := _get_action_item_id(action)
	if item_id == "":
		return ""
	return _get_item_description(item_id)


func _format_contract_tooltip(agent: Agent, sim_state: SimState) -> String:
	if agent.active_contract_id <= 0 or sim_state == null:
		return ""
	var contract: Contract = sim_state.contracts_system.get_contract(agent.active_contract_id)
	if contract == null:
		return ""
	return _get_item_description(contract.item)


func _get_action_item_id(action: Dictionary) -> String:
	var action_type: String = action.get("type", "")
	match action_type:
		"eat", "buy", "bid", "PLACE_BUY_ORDER", "sell", "ask", "PLACE_SELL_ORDER":
			return action.get("item", "")
		_:
			return ""


func _format_skills(skills: Dictionary) -> String:
	if skills.is_empty():
		return "(none)"
	
	var parts := []
	for skill in skills:
		parts.append("%s: %d" % [skill, skills[skill]])
	return ", ".join(parts)


func _get_item_description(item_id: String) -> String:
	if _sim_state == null:
		return ""
	var item_data: Dictionary = _sim_state.items.get(item_id, {})
	var description = item_data.get("description", "")
	return str(description) if description != null else ""


func _format_faction(agent: Agent, sim_state: SimState) -> String:
	if agent.faction_id == 0:
		return "Faction: None"
	
	# Try to find faction name
	for faction in sim_state.factions:
		if faction.id == agent.faction_id:
			return "Faction: #%d (%s)" % [faction.id, faction.name]
	
	return "Faction: #%d" % agent.faction_id


func _format_market_ban(agent: Agent, current_tick: int) -> String:
	if agent.market_ban_until_tick <= current_tick:
		return "Market Ban: None"
	
	var ticks_remaining: int = agent.market_ban_until_tick - current_tick
	return "Market Ban: %d ticks remaining" % ticks_remaining


func _format_action(action: Dictionary) -> String:
	var action_type: String = action.get("type", "unknown")
	
	match action_type:
		"idle":
			return "Idle"
		"move":
			var dx: int = action.get("dx", 0)
			var dy: int = action.get("dy", 0)
			return "Moving (%+d, %+d)" % [dx, dy]
		"harvest":
			var node_id: int = action.get("node_id", -1)
			return "Harvesting (node %d)" % node_id
		"eat":
			var item: String = action.get("item", "?")
			return "Eating %s" % item
		"buy", "bid", "PLACE_BUY_ORDER":
			var item: String = action.get("item", "?")
			var price: int = action.get("price", 0)
			return "Buying %s @ %d" % [item, price]
		"sell", "ask", "PLACE_SELL_ORDER":
			var item: String = action.get("item", "?")
			var price: int = action.get("price", 0)
			return "Selling %s @ %d" % [item, price]
		"craft":
			var recipe: String = action.get("recipe_id", "?")
			return "Crafting %s" % recipe
		"accept_contract":
			var cid: int = action.get("contract_id", -1)
			return "Accepting contract #%d" % cid
		"deliver":
			return "Delivering"
		"claim":
			return "Claiming tile"
		_:
			return action_type


func _format_target(agent: Agent, sim_state: SimState) -> String:
	var parts := []
	
	# Target node
	if agent.target_node_id >= 0:
		var node := sim_state.world.get_node_by_id(agent.target_node_id)
		if node != null:
			parts.append("Node: %s @ (%d,%d)" % [node.type, node.pos_x, node.pos_y])
		else:
			parts.append("Node: #%d" % agent.target_node_id)

	# Target workshop
	if agent.target_workshop_id >= 0:
		var ws := sim_state.world.get_workshop_by_id(agent.target_workshop_id)
		if ws != null:
			parts.append("Workshop @ (%d,%d)" % [ws.pos_x, ws.pos_y])
		else:
			parts.append("Workshop #%d" % agent.target_workshop_id)

	# Target market (check action)
	var action_type: String = agent.current_action.get("type", "")
	if action_type in ["buy", "sell", "bid", "ask", "PLACE_BUY_ORDER", "PLACE_SELL_ORDER"]:
		var market_x: int = sim_state.tuning.get("market_pos_x", 48)
		var market_y: int = sim_state.tuning.get("market_pos_y", 48)
		parts.append("Market @ (%d,%d)" % [market_x, market_y])
	
	if parts.is_empty():
		return "Target: None"
	
	return "Target: " + ", ".join(parts)


func _format_contract(agent: Agent, sim_state: SimState) -> String:
	if agent.active_contract_id <= 0:
		return "Contract: None"
	
	var contract: Contract = sim_state.contracts_system.get_contract(agent.active_contract_id)
	if contract == null:
		return "Contract: #%d (not found)" % agent.active_contract_id
	
	return "Contract: #%d - %s×%d for %d coins" % [
		contract.id, contract.item, contract.qty, contract.payout
	]


## Get target position for line drawing (returns Vector2i(-1,-1) if no target)
func get_agent_target_position() -> Vector2i:
	if _sim_state == null or _current_agent_id < 0:
		return Vector2i(-1, -1)
	
	var agent: Agent = _sim_state.get_agent(_current_agent_id)
	if agent == null:
		return Vector2i(-1, -1)
	
	# Prioritize: workshop > node > market
	if agent.target_workshop_id >= 0:
		var ws := _sim_state.world.get_workshop_by_id(agent.target_workshop_id)
		if ws != null:
			return Vector2i(ws.pos_x, ws.pos_y)
	
	if agent.target_node_id >= 0:
		var node := _sim_state.world.get_node_by_id(agent.target_node_id)
		if node != null:
			return Vector2i(node.pos_x, node.pos_y)
	
	# Check if heading to market
	var action_type: String = agent.current_action.get("type", "")
	if action_type in ["buy", "sell", "bid", "ask", "deliver", "PLACE_BUY_ORDER", "PLACE_SELL_ORDER"]:
		var market_x: int = _sim_state.tuning.get("market_pos_x", 48)
		var market_y: int = _sim_state.tuning.get("market_pos_y", 48)
		return Vector2i(market_x, market_y)
	
	return Vector2i(-1, -1)


## Clear the inspector
func clear() -> void:
	_sim_state = null
	_current_agent_id = -1
	
	if _id_label:
		_id_label.text = "ID: --"
		_pos_label.text = "Position: --"
		_alive_label.text = "Status: --"
		_money_label.text = "Money: --"
		_inventory_label.text = "Inventory: --"
		_inventory_label.tooltip_text = ""
		_hunger_label.text = "Hunger: --"
		_role_label.text = "Role: --"
		_skills_label.text = "Skills: --"
		_traits_label.text = "Traits: --"
		_faction_label.text = "Faction: --"
		_market_ban_label.text = "Market Ban: --"
		_action_label.text = "Action: --"
		_action_label.tooltip_text = ""
		_target_label.text = "Target: --"
		_contract_label.text = "Contract: --"
		_contract_label.tooltip_text = ""
