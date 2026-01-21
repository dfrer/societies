extends VBoxContainer
class_name EventTimelinePanel

# Signals
signal request_jump_to_tick(tick)

# References
var _sim_run: SimRunnerNode = null
var _last_event_count: int = 0
var _filter_type: String = "All"
var _filter_agent_id: int = -1
var _filter_faction_id: int = -1

# UI Nodes
@onready var event_list: VBoxContainer = $ScrollContainer/EventList
@onready var type_filter: OptionButton = $Filters/TypeFilter
@onready var agent_filter: LineEdit = $Filters/AgentFilter
@onready var faction_filter: LineEdit = $Filters/FactionFilter

func _ready() -> void:
	_setup_ui()


func _setup_ui() -> void:
	# Initialize filters
	type_filter.add_item("All")
	type_filter.add_item("trade_executed")
	type_filter.add_item("order_denied")
	type_filter.add_item("contract_posted")
	type_filter.add_item("contract_accepted")
	type_filter.add_item("contract_completed")
	type_filter.add_item("contract_expired")
	type_filter.add_item("contract_failed")
	type_filter.add_item("violation_detected")
	type_filter.add_item("faction_formed")
	type_filter.add_item("faction_joined")
	type_filter.add_item("tile_claimed")
	type_filter.add_item("proposal_created")
	type_filter.add_item("proposal_resolved")
	
	type_filter.item_selected.connect(_on_filter_changed)
	agent_filter.text_changed.connect(_on_agent_filter_changed)
	faction_filter.text_changed.connect(_on_faction_filter_changed)

func setup(sim_runner: SimRunnerNode) -> void:
	_sim_run = sim_runner
	_sim_run.state_changed.connect(_on_state_changed)
	_sim_run.run_started.connect(_on_run_started)
	_update_list()

func _on_run_started(_seed_value: int) -> void:
	_last_event_count = 0
	_update_list()

func _on_state_changed(_snapshot: Dictionary) -> void:
	if _sim_run == null or _sim_run.sim == null or _sim_run.sim.state == null:
		return
		
	var state = _sim_run.sim.state
	if state.events.size() != _last_event_count:
		_update_list()
		_last_event_count = state.events.size()

func _on_filter_changed(_index: int) -> void:
	_filter_type = type_filter.get_item_text(type_filter.selected)
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

func _update_list() -> void:
	# Clear list
	for child in event_list.get_children():
		child.queue_free()
	
	if _sim_run == null or _sim_run.sim == null or _sim_run.sim.state == null:
		return
		
	var all_events: Array = _sim_run.sim.state.events
	# Reverse order to show newest first
	# Limit to last N events for performance? Say 100 closest to current tick filter?
	# For filtering, let's iterate backwards.
	
	var count_added := 0
	var max_items := 50
	
	for i in range(all_events.size() - 1, -1, -1):
		var event: Dictionary = all_events[i]
		
		# Apply filters
		if _filter_type != "All" and event["type"] != _filter_type:
			continue
		
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
				continue
				
		if _filter_faction_id != -1:
			if data.get("faction_id") != _filter_faction_id:
				continue
				
		# Create Item
		var item_panel := PanelContainer.new()
		var hbox := HBoxContainer.new()
		item_panel.add_child(hbox)
		
		var tick_btn := Button.new()
		tick_btn.text = "T: %d" % event["tick"]
		tick_btn.pressed.connect(func(): request_jump_to_tick.emit(event["tick"]))
		hbox.add_child(tick_btn)
		
		var label := Label.new()
		label.text = _format_event_text(event)
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		hbox.add_child(label)
		
		event_list.add_child(item_panel)
		
		count_added += 1
		if count_added >= max_items:
			break

func _format_event_text(event: Dictionary) -> String:
	var type: String = event["type"]
	var data: Dictionary = event["data"]
	
	match type:
		"trade_executed":
			return "Agent #%d bought %d %s from #%d for %d coins (Tax: %d, Tariff: %d)" % [
				data["buyer_id"], data["qty"], data["item"], data["seller_id"], 
				data["price"], data.get("tax", 0), data.get("tariff", 0)
			]
		"order_denied":
			return "Agent #%d's %s order for %s denied: %s" % [
				data["agent_id"], data["type"], data["item"], data.get("reason_code", data.get("reason", "unknown"))
			]
		"enforcement_blocked":
			return "Agent #%d's %s blocked by laws: %s" % [
				data["agent_id"], data.get("action", "action"), data.get("reason_code", "denied")
			]
		"contract_posted":
			return "Agent #%d posted contract for %d %s (Payout: %d)" % [
				data["issuer_id"], data["qty"], data["item"], data["payout"]
			]
		"contract_accepted":
			return "Agent #%d accepted contract #%d from #%d" % [
				data["agent_id"], data["contract_id"], data["issuer_id"]
			]
		"contract_completed":
			return "Agent #%d completed contract #%d (Earned %d)" % [
				data["agent_id"], data["contract_id"], data["payout"]
			]
		"contract_expired":
			return "Contract #%d expired (Issuer #%d refunded %d)" % [
				data["contract_id"], data["issuer_id"], data.get("refund", 0)
			]
		"contract_failed":
			return "Contract #%d failed (Worker #%d, Issuer #%d refunded %d)" % [
				data["contract_id"], data.get("worker_id", 0), data["issuer_id"], data.get("refund", 0)
			]
		"violation_detected":
			return "Agent #%d detected for %s" % [
				data["agent_id"], data["violation_type"]
			]
		"faction_formed":
			return "Faction '%s' (#%d) formed by #%d" % [
				data["name"], data["faction_id"], data["founder_id"]
			]
		"faction_joined":
			return "Agent #%d joined Faction #%d" % [
				data["agent_id"], data["faction_id"]
			]
		"tile_claimed":
			return "Faction #%d claimed tile at (%d, %d)" % [
				data["faction_id"], data["pos"][0], data["pos"][1]
			]
		"proposal_created":
			return "Agent #%d proposed changes for Faction #%d: %s" % [
				data["proposer_id"], data["faction_id"], str(data["changes"])
			]
		"proposal_resolved":
			var result := "PASSED" if data["passed"] else "FAILED"
			return "Proposal #%d for Faction #%d %s (%d vs %d)" % [
				data["proposal_id"], data["faction_id"], result, 
				data["votes_for"], data["votes_against"]
			]
			
	return "%s: %s" % [type, str(data)]
