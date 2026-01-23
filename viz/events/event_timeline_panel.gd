extends VBoxContainer
class_name EventTimelinePanel

# Signals
signal request_jump_to_tick(tick)

# Event category colors for visual distinction
const CATEGORY_COLORS := {
	"economic": Color(0.2, 0.7, 0.3),     # Green - trades, market
	"contract": Color(0.3, 0.5, 0.9),     # Blue - contract lifecycle
	"political": Color(0.8, 0.6, 0.2),    # Orange - factions, voting
	"project": Color(0.6, 0.3, 0.8),      # Purple - communal projects
	"enforcement": Color(0.9, 0.3, 0.3),  # Red - violations, blocks
	"social": Color(0.4, 0.8, 0.8),       # Cyan - joining, forming
	"action": Color(0.5, 0.7, 0.9),       # Light blue - agent actions
	"system": Color(0.6, 0.6, 0.6)        # Gray - day started
}

# Event type to category mapping
const EVENT_CATEGORIES := {
	# Economic
	"trade_executed": "economic",
	"order_denied": "economic",
	# Contracts
	"contract_posted": "contract",
	"contract_accepted": "contract",
	"contract_completed": "contract",
	"contract_expired": "contract",
	"contract_failed": "contract",
	# Political
	"faction_formed": "political",
	"proposal_created": "political",
	"proposal_resolved": "political",
	"election_held": "political",
	"vote_cast": "political",
	# Projects
	"project_started": "project",
	"project_contribution": "project",
	"project_completed": "project",
	"project_abandoned": "project",
	"organization_project_planned": "project",
	# Enforcement
	"violation_detected": "enforcement",
	"enforcement_blocked": "enforcement",
	# Social
	"faction_joined": "social",
	"tile_claimed": "social",
	"organization_tile_claimed": "social",
	"salary_paid": "social",
	# System
	"day_started": "system"
}

# References
var _sim_run: SimRunnerNode = null
var _last_event_count: int = 0
var _last_trace_count: int = 0
var _filter_type: String = "All"
var _filter_category: String = "All"
var _filter_agent_id: int = -1
var _filter_faction_id: int = -1
var _show_actions: bool = false

# UI Nodes
@onready var event_list: VBoxContainer = $ScrollContainer/EventList
@onready var type_filter: OptionButton = $Filters/TypeFilter
@onready var category_filter: OptionButton = $Filters/CategoryFilter
@onready var agent_filter: LineEdit = $Filters/AgentFilter
@onready var faction_filter: LineEdit = $Filters/FactionFilter
@onready var show_actions_check: CheckBox = $Filters2/ShowActionsCheck

func _ready() -> void:
	_setup_ui()


func _setup_ui() -> void:
	# Initialize type filter
	type_filter.add_item("All")
	type_filter.add_item("trade_executed")
	type_filter.add_item("order_denied")
	type_filter.add_item("contract_posted")
	type_filter.add_item("contract_accepted")
	type_filter.add_item("contract_completed")
	type_filter.add_item("contract_expired")
	type_filter.add_item("contract_failed")
	type_filter.add_item("violation_detected")
	type_filter.add_item("enforcement_blocked")
	type_filter.add_item("faction_formed")
	type_filter.add_item("faction_joined")
	type_filter.add_item("tile_claimed")
	type_filter.add_item("proposal_created")
	type_filter.add_item("proposal_resolved")
	type_filter.add_item("election_held")
	type_filter.add_item("vote_cast")
	type_filter.add_item("salary_paid")
	type_filter.add_item("day_started")
	type_filter.add_item("project_started")
	type_filter.add_item("project_contribution")
	type_filter.add_item("project_completed")
	type_filter.add_item("project_abandoned")
	
	# Initialize category filter
	category_filter.add_item("All")
	category_filter.add_item("Economic")
	category_filter.add_item("Contract")
	category_filter.add_item("Political")
	category_filter.add_item("Project")
	category_filter.add_item("Enforcement")
	category_filter.add_item("Social")
	category_filter.add_item("System")
	
	type_filter.item_selected.connect(_on_filter_changed)
	category_filter.item_selected.connect(_on_category_filter_changed)
	agent_filter.text_changed.connect(_on_agent_filter_changed)
	faction_filter.text_changed.connect(_on_faction_filter_changed)
	show_actions_check.toggled.connect(_on_show_actions_toggled)

func setup(sim_runner: SimRunnerNode) -> void:
	_sim_run = sim_runner
	_sim_run.state_changed.connect(_on_state_changed)
	_sim_run.run_started.connect(_on_run_started)
	_update_list()

func _on_run_started(_seed_value: int) -> void:
	_last_event_count = 0
	_last_trace_count = 0
	_update_list()

func _on_state_changed(_snapshot: Dictionary) -> void:
	if _sim_run == null or _sim_run.sim == null or _sim_run.sim.state == null:
		return
		
	var state = _sim_run.sim.state
	var needs_update: bool = state.events.size() != _last_event_count
	if _show_actions and state.decision_traces.size() != _last_trace_count:
		needs_update = true
	
	if needs_update:
		_update_list()
		_last_event_count = state.events.size()
		_last_trace_count = state.decision_traces.size()

func _on_filter_changed(_index: int) -> void:
	_filter_type = type_filter.get_item_text(type_filter.selected)
	_update_list()

func _on_category_filter_changed(_index: int) -> void:
	var selected_text := category_filter.get_item_text(category_filter.selected)
	_filter_category = selected_text.to_lower() if selected_text != "All" else "All"
	_update_list()

func _on_agent_filter_changed(text: String) -> void:
	if text.is_valid_int():
		_filter_agent_id = int(text)
	else:
		_filter_agent_id = -1
	_update_list()

func _on_faction_filter_changed(text: String) -> void:
	if text.is_valid_int():
		_filter_faction_id = int(text)
	else:
		_filter_faction_id = -1
	_update_list()

func _on_show_actions_toggled(pressed: bool) -> void:
	_show_actions = pressed
	_update_list()

func _update_list() -> void:
	# Clear list
	for child in event_list.get_children():
		child.queue_free()
	
	if _sim_run == null or _sim_run.sim == null or _sim_run.sim.state == null:
		return
	
	var state = _sim_run.sim.state
	var all_events: Array = state.events
	
	# Collect items to display (events + optionally actions)
	var display_items: Array = []
	
	# Add events
	for event in all_events:
		display_items.append({"source": "event", "tick": event["tick"], "data": event})
	
	# Add agent actions from decision_traces if enabled
	if _show_actions:
		for trace in state.decision_traces:
			display_items.append({"source": "action", "tick": trace["tick"], "data": trace})
	
	# Sort by tick descending (newest first)
	display_items.sort_custom(func(a, b): return a["tick"] > b["tick"])
	
	var count_added := 0
	var max_items := 100
	
	for item in display_items:
		if count_added >= max_items:
			break
		
		if item["source"] == "event":
			var event: Dictionary = item["data"]
			if not _passes_event_filters(event):
				continue
			_create_event_card(event)
			count_added += 1
		else:
			var trace: Dictionary = item["data"]
			if not _passes_action_filters(trace):
				continue
			_create_action_card(trace)
			count_added += 1

func _passes_event_filters(event: Dictionary) -> bool:
	var event_type: String = event["type"]
	
	# Type filter
	if _filter_type != "All" and event_type != _filter_type:
		return false
	
	# Category filter
	if _filter_category != "All":
		var category = EVENT_CATEGORIES.get(event_type, "system")
		if category != _filter_category:
			return false
	
	# Agent filter
	var data: Dictionary = event["data"]
	if _filter_agent_id != -1:
		var match_found := false
		if data.get("agent_id") == _filter_agent_id: match_found = true
		if data.get("buyer_id") == _filter_agent_id: match_found = true
		if data.get("seller_id") == _filter_agent_id: match_found = true
		if data.get("issuer_id") == _filter_agent_id: match_found = true
		if data.get("worker_id") == _filter_agent_id: match_found = true
		if data.get("proposer_id") == _filter_agent_id: match_found = true
		if data.get("founder_id") == _filter_agent_id: match_found = true
		if not match_found:
			return false
			
	# Faction filter
	if _filter_faction_id != -1:
		if data.get("faction_id") != _filter_faction_id:
			return false
	
	return true

func _passes_action_filters(trace: Dictionary) -> bool:
	# Agent filter for actions
	if _filter_agent_id != -1:
		if trace.get("agent_id") != _filter_agent_id:
			return false
	
	# Category filter - actions are always "action" category
	if _filter_category != "All" and _filter_category != "action":
		return false
	
	return true

func _create_event_card(event: Dictionary) -> void:
	var type: String = event["type"]
	var category: String = EVENT_CATEGORIES.get(type, "system")
	var color: Color = CATEGORY_COLORS.get(category, CATEGORY_COLORS["system"])
	
	var item_panel := PanelContainer.new()
	var stylebox := StyleBoxFlat.new()
	stylebox.bg_color = Color(0.15, 0.15, 0.18, 0.9)
	stylebox.border_width_left = 4
	stylebox.border_color = color
	stylebox.corner_radius_top_left = 3
	stylebox.corner_radius_bottom_left = 3
	stylebox.content_margin_left = 8
	stylebox.content_margin_right = 8
	stylebox.content_margin_top = 4
	stylebox.content_margin_bottom = 4
	item_panel.add_theme_stylebox_override("panel", stylebox)
	
	var hbox := HBoxContainer.new()
	hbox.add_theme_constant_override("separation", 8)
	item_panel.add_child(hbox)
	
	# Tick button
	var tick_btn := Button.new()
	tick_btn.text = "T%d" % event["tick"]
	tick_btn.custom_minimum_size = Vector2(50, 0)
	tick_btn.pressed.connect(func(): request_jump_to_tick.emit(event["tick"]))
	tick_btn.tooltip_text = "Jump to tick %d" % event["tick"]
	hbox.add_child(tick_btn)
	
	# Content VBox
	var vbox := VBoxContainer.new()
	vbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	hbox.add_child(vbox)
	
	# Event summary (main text)
	var summary := Label.new()
	summary.text = _format_event_text(event)
	summary.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	summary.add_theme_color_override("font_color", Color(0.95, 0.95, 0.95))
	vbox.add_child(summary)
	
	# Event type tag (smaller, colored)
	var tag := Label.new()
	tag.text = type.replace("_", " ").capitalize()
	tag.add_theme_font_size_override("font_size", 11)
	tag.add_theme_color_override("font_color", color.lightened(0.3))
	vbox.add_child(tag)
	
	event_list.add_child(item_panel)

func _create_action_card(trace: Dictionary) -> void:
	var color: Color = CATEGORY_COLORS["action"]
	
	var item_panel := PanelContainer.new()
	var stylebox := StyleBoxFlat.new()
	stylebox.bg_color = Color(0.12, 0.14, 0.18, 0.8)
	stylebox.border_width_left = 3
	stylebox.border_color = color
	stylebox.corner_radius_top_left = 2
	stylebox.corner_radius_bottom_left = 2
	stylebox.content_margin_left = 8
	stylebox.content_margin_right = 8
	stylebox.content_margin_top = 3
	stylebox.content_margin_bottom = 3
	item_panel.add_theme_stylebox_override("panel", stylebox)
	
	var hbox := HBoxContainer.new()
	hbox.add_theme_constant_override("separation", 8)
	item_panel.add_child(hbox)
	
	# Tick label (no button for actions to distinguish from events)
	var tick_label := Label.new()
	tick_label.text = "T%d" % trace["tick"]
	tick_label.custom_minimum_size = Vector2(50, 0)
	tick_label.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
	hbox.add_child(tick_label)
	
	# Action text
	var action_text := Label.new()
	action_text.text = _format_action_text(trace)
	action_text.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	action_text.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	action_text.add_theme_color_override("font_color", Color(0.8, 0.85, 0.9))
	action_text.add_theme_font_size_override("font_size", 12)
	hbox.add_child(action_text)
	
	event_list.add_child(item_panel)

func _format_action_text(trace: Dictionary) -> String:
	var agent_id: int = trace.get("agent_id", 0)
	var action_type: String = trace.get("action_type", "unknown")
	var intent_type: String = trace.get("intent_type", "")
	
	var icon := _get_action_icon(action_type)
	var action_name := action_type.replace("_", " ").to_lower()
	
	if intent_type != "":
		return "%s Agent #%d: %s (%s)" % [icon, agent_id, action_name, intent_type]
	return "%s Agent #%d: %s" % [icon, agent_id, action_name]

func _get_action_icon(action_type: String) -> String:
	match action_type:
		"MOVE_TO_NODE", "MOVE_TO_MARKET", "MOVE_TO_WORKSHOP", "MOVE_TO_POSITION", "MOVE_TO_DELIVERY":
			return "🚶"
		"GATHER_NODE":
			return "⛏️"
		"EAT", "EAT_MEAL":
			return "🍎"
		"PLACE_BUY_ORDER":
			return "💵"
		"PLACE_SELL_ORDER":
			return "💰"
		"QUEUE_CRAFT":
			return "🔧"
		"REST", "SLEEP":
			return "😴"
		"CLAIM_TILE":
			return "🏴"
		"VOTE":
			return "🗳️"
		"EXPLORE":
			return "🔍"
		"BUILD_WORKSHOP", "BUILD_ROAD", "BUILD_SITE":
			return "🏗️"
		"ACCEPT_CONTRACT", "DELIVER_CONTRACT":
			return "📜"
		"CONTRIBUTE_TO_PROJECT":
			return "🤝"
		"IDLE":
			return "⏸️"
		_:
			return "▸"

func _format_event_text(event: Dictionary) -> String:
	var type: String = event["type"]
	var data: Dictionary = event["data"]
	
	match type:
		"trade_executed":
			return "💹 Agent #%d bought %d %s from #%d for %d coins" % [
				data["buyer_id"], data["qty"], data["item"], data["seller_id"], 
				data["price"]
			]
		"order_denied":
			return "🚫 Agent #%d's %s order for %s denied: %s" % [
				data["agent_id"], data["type"], data["item"], data.get("reason_code", data.get("reason", "unknown"))
			]
		"enforcement_blocked":
			return "⛔ Agent #%d's %s blocked: %s" % [
				data["agent_id"], data.get("action", "action"), data.get("reason_code", "denied")
			]
		"contract_posted":
			return "📋 Agent #%d posted contract: %d %s for %d coins" % [
				data["issuer_id"], data["qty"], data["item"], data["payout"]
			]
		"contract_accepted":
			return "✅ Agent #%d accepted contract #%d" % [
				data["agent_id"], data["contract_id"]
			]
		"contract_completed":
			return "🎉 Agent #%d completed contract #%d (+%d coins)" % [
				data["agent_id"], data["contract_id"], data["payout"]
			]
		"contract_expired":
			return "⏰ Contract #%d expired (refund: %d)" % [
				data["contract_id"], data.get("refund", 0)
			]
		"contract_failed":
			return "❌ Contract #%d failed (refund: %d)" % [
				data["contract_id"], data.get("refund", 0)
			]
		"violation_detected":
			return "⚠️ Agent #%d caught: %s" % [
				data["agent_id"], data["violation_type"]
			]
		"faction_formed":
			return "🏛️ Faction '%s' formed by #%d" % [
				data["name"], data["founder_id"]
			]
		"faction_joined":
			return "🤝 Agent #%d joined Faction #%d" % [
				data["agent_id"], data["faction_id"]
			]
		"tile_claimed":
			return "🏴 Faction #%d claimed tile (%d, %d)" % [
				data["faction_id"], data["pos"][0], data["pos"][1]
			]
		"proposal_created":
			return "📝 Agent #%d proposed law changes" % [
				data["proposer_id"]
			]
		"proposal_resolved":
			var result := "✅ PASSED" if data["passed"] else "❌ FAILED"
			return "🗳️ Proposal #%d %s (%d-%d)" % [
				data["proposal_id"], result, data["votes_for"], data["votes_against"]
			]
		"election_held":
			return "🗳️ Faction #%d held election, winner: #%d" % [
				data.get("faction_id", 0), data.get("winner_id", 0)
			]
		"vote_cast":
			return "🗳️ Agent #%d voted on Proposal #%d" % [
				data.get("agent_id", 0), data.get("proposal_id", 0)
			]
		"salary_paid":
			return "💰 Faction #%d paid #%d salary: %d" % [
				data.get("faction_id", 0), data.get("agent_id", 0), data.get("amount", 0)
			]
		"day_started":
			return "🌅 Day %d started" % [data.get("day", 0)]
		"project_started":
			return "🔨 Project #%d started: %s" % [
				data.get("project_id", 0), data.get("type", "unknown")
			]
		"project_contribution":
			return "⚒️ Agent #%d contributed to Project #%d" % [
				data.get("agent_id", 0), data.get("project_id", 0)
			]
		"project_completed":
			return "✅ Project #%d completed!" % [data.get("project_id", 0)]
		"project_abandoned":
			return "❌ Project #%d abandoned" % [data.get("project_id", 0)]
		"organization_project_planned":
			return "📋 Org #%d planned %s project" % [
				data.get("organization_id", 0), data.get("type", "unknown")
			]
		"organization_tile_claimed":
			return "🏴 Org #%d claimed tile (%d, %d)" % [
				data.get("organization_id", 0), data.get("x", 0), data.get("y", 0)
			]
		
	return "%s: %s" % [type, str(data)]
