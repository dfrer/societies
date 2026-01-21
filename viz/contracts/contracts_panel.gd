class_name ContractsPanel
extends PanelContainer

## Contracts panel - lists all contracts with status, item, qty, payout, issuer, worker, deadline.
## Shows escrow totals and completions stats.

var _sim_state: SimState = null

# UI State for filters
var _status_filter: String = "All"
var _item_filter: String = "All"
var _issuer_type_filter: String = "All"
var _worker_filter: int = -1 # -1 = All

# Selected contract ID
var _selected_contract_id: int = -1

# UI Elements
var _stats_container: HBoxContainer
var _escrow_label: Label
var _stats_label: Label

var _filter_container: GridContainer
var _status_filter_opt: OptionButton
var _item_filter_opt: OptionButton
var _issuer_type_filter_opt: OptionButton
var _worker_filter_edit: LineEdit

var _content_split: VSplitContainer
var _list_scroll: ScrollContainer
var _list_content: VBoxContainer
var _details_panel: PanelContainer
var _details_label: RichTextLabel

func _ready() -> void:
	_setup_ui()

func _setup_ui() -> void:
	custom_minimum_size = Vector2(250, 0)
	
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 8)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_right", 8)
	margin.add_theme_constant_override("margin_bottom", 8)
	add_child(margin)
	
	var main_vbox := VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", 8)
	margin.add_child(main_vbox)
	
	# Header logic
	var header := Label.new()
	header.text = "Contracts"
	header.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	header.add_theme_font_size_override("font_size", 16)
	main_vbox.add_child(header)
	
	main_vbox.add_child(HSeparator.new())
	
	# Stats Section
	_stats_container = HBoxContainer.new()
	main_vbox.add_child(_stats_container)
	
	var escrow_vbox := VBoxContainer.new()
	_stats_container.add_child(escrow_vbox)
	var escrow_title := Label.new()
	escrow_title.text = "Total Escrow"
	escrow_title.add_theme_font_size_override("font_size", 10)
	escrow_vbox.add_child(escrow_title)
	_escrow_label = Label.new()
	_escrow_label.text = "0"
	escrow_vbox.add_child(_escrow_label)
	
	_stats_container.add_child(VSeparator.new())
	
	_stats_label = Label.new()
	_stats_label.text = "Posted: 0 | Accepted: 0 | Completed: 0\nExpired: 0 | Failed: 0"
	_stats_label.add_theme_font_size_override("font_size", 11)
	_stats_container.add_child(_stats_label)

	main_vbox.add_child(HSeparator.new())

	# Filters Section
	_filter_container = GridContainer.new()
	_filter_container.columns = 2
	main_vbox.add_child(_filter_container)

	# Status Filter
	var status_lbl := Label.new()
	status_lbl.text = "Status:"
	_filter_container.add_child(status_lbl)
	_status_filter_opt = OptionButton.new()
	_status_filter_opt.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_status_filter_opt.add_item("All")
	_status_filter_opt.add_item("Posted")
	_status_filter_opt.add_item("Accepted")
	_status_filter_opt.add_item("Completed")
	_status_filter_opt.add_item("Expired")
	_status_filter_opt.add_item("Failed")
	_status_filter_opt.item_selected.connect(_on_status_filter_changed)
	_filter_container.add_child(_status_filter_opt)

	# Item Filter
	var item_lbl := Label.new()
	item_lbl.text = "Item:"
	_filter_container.add_child(item_lbl)
	_item_filter_opt = OptionButton.new()
	_item_filter_opt.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_item_filter_opt.add_item("All")
	_item_filter_opt.item_selected.connect(_on_item_filter_changed)
	_filter_container.add_child(_item_filter_opt)

	# Issuer Type Filter
	var issuer_lbl := Label.new()
	issuer_lbl.text = "Issuer:"
	_filter_container.add_child(issuer_lbl)
	_issuer_type_filter_opt = OptionButton.new()
	_issuer_type_filter_opt.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_issuer_type_filter_opt.add_item("All")
	_issuer_type_filter_opt.add_item("Agent")
	_issuer_type_filter_opt.add_item("Faction")
	_issuer_type_filter_opt.item_selected.connect(_on_issuer_type_filter_changed)
	_filter_container.add_child(_issuer_type_filter_opt)

	# Worker Filter
	var worker_lbl := Label.new()
	worker_lbl.text = "Worker ID:"
	_filter_container.add_child(worker_lbl)
	_worker_filter_edit = LineEdit.new()
	_worker_filter_edit.placeholder_text = "All"
	_worker_filter_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_worker_filter_edit.text_submitted.connect(_on_worker_filter_submitted)
	_worker_filter_edit.focus_exited.connect(func(): _on_worker_filter_submitted(_worker_filter_edit.text))
	_filter_container.add_child(_worker_filter_edit)
	
	main_vbox.add_child(HSeparator.new())

	# Main Content Area (Split: List / Details)
	_content_split = VSplitContainer.new()
	_content_split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_vbox.add_child(_content_split)

	# Contract List
	var list_container := VBoxContainer.new()
	list_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_content_split.add_child(list_container)

	# List Header
	var list_header := HBoxContainer.new()
	list_header.add_theme_constant_override("separation", 4)
	list_container.add_child(list_header)
	
	var h_id := Label.new()
	h_id.text = "ID"
	h_id.custom_minimum_size.x = 30
	list_header.add_child(h_id)

	var h_status := Label.new()
	h_status.text = "Status"
	h_status.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	list_header.add_child(h_status)
	
	var h_item := Label.new()
	h_item.text = "Item"
	h_item.custom_minimum_size.x = 60
	list_header.add_child(h_item)
	
	var h_pay := Label.new()
	h_pay.text = "Pay"
	h_pay.custom_minimum_size.x = 40
	h_pay.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	list_header.add_child(h_pay)

	list_container.add_child(HSeparator.new())

	_list_scroll = ScrollContainer.new()
	_list_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_list_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	list_container.add_child(_list_scroll)
	
	_list_content = VBoxContainer.new()
	_list_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_list_scroll.add_child(_list_content)

	# Details Panel
	_details_panel = PanelContainer.new()
	_content_split.add_child(_details_panel)
	
	var details_margin := MarginContainer.new()
	details_margin.add_theme_constant_override("margin_left", 4)
	details_margin.add_theme_constant_override("margin_top", 4)
	details_margin.add_theme_constant_override("margin_right", 4)
	details_margin.add_theme_constant_override("margin_bottom", 4)
	_details_panel.add_child(details_margin)
	
	_details_label = RichTextLabel.new()
	_details_label.fit_content = true
	_details_label.bbcode_enabled = true
	_details_label.text = "[center][i]Select a contract to view details[/i][/center]"
	details_margin.add_child(_details_label)


# Main Update Method
func update_sim_state(sim_state: SimState) -> void:
	if sim_state == null:
		return
	
	_sim_state = sim_state
	
	# 1. Update Stats
	_update_stats()
	
	# 2. Update Filters (Items)
	_update_item_filter_options()
	
	# 3. Update Contract List
	_update_contract_list()
	
	# 4. Update Details
	_update_details_view()

func _update_stats() -> void:
	if _sim_state.contracts_system == null:
		return
	var cs = _sim_state.contracts_system
	
	_escrow_label.text = str(cs.get_total_escrow())
	
	var s = cs.stats
	_stats_label.text = "Posted: %d | Accepted: %d | Completed: %d\nExpired: %d | Failed: %d" % [
		s.get("posted", 0), s.get("accepted", 0), s.get("completed", 0),
		s.get("expired", 0), s.get("failed", 0)
	]

func _update_item_filter_options() -> void:
	# Populate item filter from available items or from contracts present
	# To capture all possible items, we can use SimState.items keys, or just what's in contracts.
	# Let's use sim_state.items.
	
	# Only update if the number of items changed drastically or it's empty (init)
	# For simplicity, we can rebuild if count differs or just always check existence.
	# Optimization: check if we need to rebuild.
	if _item_filter_opt.item_count <= 1: # Only "All"
		var items = _sim_state.items.keys()
		items.sort()
		for item in items:
			_item_filter_opt.add_item(item)
			
	# Note: If new items appear dynamically, this might need more robust updating.
	# But items are usually static in this sim.

func _update_contract_list() -> void:
	# Clear list
	for child in _list_content.get_children():
		child.queue_free()
		
	var cs = _sim_state.contracts_system
	var contracts: Array = cs.contracts
	
	# Filter
	var filtered_contracts = []
	for c in contracts:
		if not _pass_filters(c):
			continue
		filtered_contracts.append(c)
		
	# Sort? Maybe by ID descending (show newest first)
	filtered_contracts.sort_custom(func(a, b): return a.id > b.id)
	
	# Limit display count to prevent lag
	var max_display = 30
	var count = 0
	
	for c in filtered_contracts:
		if count >= max_display:
			var more = Label.new()
			more.text = "... and %d more" % (filtered_contracts.size() - count)
			more.add_theme_color_override("font_color", Color(0.5, 0.5, 0.5))
			_list_content.add_child(more)
			break
			
		var row = _create_contract_row(c)
		_list_content.add_child(row)
		count += 1
		
	if count == 0:
		var empty = Label.new()
		empty.text = "(No contracts match filters)"
		empty.add_theme_color_override("font_color", Color(0.5, 0.5, 0.5))
		_list_content.add_child(empty)

func _pass_filters(c: Contract) -> bool:
	# Status Filter
	if _status_filter != "All":
		# Map UI string to constant if needed, but constants are lowercase strings usually
		# Contract.STATUS_POSTED = "posted"
		# UI options capitalized "Posted"
		if c.status.to_lower() != _status_filter.to_lower():
			return false
			
	# Item Filter
	if _item_filter != "All":
		if c.item != _item_filter:
			return false
			
	# Issuer Type Filter
	if _issuer_type_filter != "All":
		if c.issuer_type.to_lower() != _issuer_type_filter.to_lower():
			return false
			
	# Worker Filter
	if _worker_filter != -1:
		if c.worker_id != _worker_filter:
			return false
			
	return true

func _create_contract_row(c: Contract) -> Button:
	var btn := Button.new()
	btn.toggle_mode = true
	btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	btn.add_theme_constant_override("h_separation", 4)
	if c.id == _selected_contract_id:
		btn.button_pressed = true
	
	# Use a container inside the button for layout
	var hbox := HBoxContainer.new()
	hbox.mouse_filter = Control.MOUSE_FILTER_IGNORE # Let button handle input
	hbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	btn.add_child(hbox)
	
	# ID
	var lbl_id := Label.new()
	lbl_id.text = str(c.id)
	lbl_id.custom_minimum_size.x = 30
	hbox.add_child(lbl_id)
	
	# Status
	var lbl_status := Label.new()
	lbl_status.text = c.status
	lbl_status.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	# Color code status
	match c.status:
		Contract.STATUS_POSTED: lbl_status.add_theme_color_override("font_color", Color.CYAN)
		Contract.STATUS_ACCEPTED: lbl_status.add_theme_color_override("font_color", Color.YELLOW)
		Contract.STATUS_COMPLETED: lbl_status.add_theme_color_override("font_color", Color.GREEN)
		Contract.STATUS_FAILED: lbl_status.add_theme_color_override("font_color", Color.RED)
		Contract.STATUS_EXPIRED: lbl_status.add_theme_color_override("font_color", Color.GRAY)
	hbox.add_child(lbl_status)
	
	# Item & Qty
	var lbl_item := Label.new()
	lbl_item.text = "%s x%d" % [c.item, c.qty]
	lbl_item.custom_minimum_size.x = 80
	lbl_item.clip_text = true
	hbox.add_child(lbl_item)
	
	# Payout
	var lbl_pay := Label.new()
	lbl_pay.text = str(c.payout)
	lbl_pay.custom_minimum_size.x = 40
	lbl_pay.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	hbox.add_child(lbl_pay)
	
	# Connect press
	btn.pressed.connect(func(): _on_contract_selected(c.id))
	
	return btn

func _on_contract_selected(id: int) -> void:
	_selected_contract_id = id
	_update_contract_list() # Rebuild to update selection state visual
	_update_details_view()

func _update_details_view() -> void:
	if _selected_contract_id == -1:
		_details_label.text = "[center][i]Select a contract to view details[/i][/center]"
		return
		
	var cs = _sim_state.contracts_system
	var c: Contract = cs.get_contract(_selected_contract_id)
	
	if c == null:
		_details_label.text = "[center][color=red]Contract not found[/color][/center]"
		return
		
	var ticks_remaining = c.deadline_tick - _sim_state.tick
	var deadline_text = "Tick %d" % c.deadline_tick
	if c.is_active():
		if ticks_remaining > 0:
			deadline_text += " (%d ticks left)" % ticks_remaining
		else:
			deadline_text += " (Overdue)"
			
	var text = "[b]Contract #%d[/b]\n" % c.id
	text += "Status: %s\n" % c.status
	text += "Item: [color=yellow]%s[/color] x%d\n" % [c.item, c.qty]
	text += "Payout: [color=green]%d[/color] (Escrow: %d)\n" % [c.payout, c.escrow]
	text += "Issuer: %s #%d\n" % [c.issuer_type.capitalize(), c.issuer_id]
	if c.worker_id > 0:
		text += "Worker: Agent #%d\n" % c.worker_id
	else:
		text += "Worker: [i]Unassigned[/i]\n"
		
	text += "Created: Tick %d\n" % c.created_tick
	if c.accepted_tick != -1:
		text += "Accepted: Tick %d\n" % c.accepted_tick
	text += "Deadline: %s\n" % deadline_text
	
	text += "Delivery Pos: (%d, %d)\n" % [c.delivery_pos_x, c.delivery_pos_y]
	text += "Delivered: %d / %d" % [c.delivered_qty, c.qty]
	
	_details_label.text = text

# Event Handlers for Filters

func _on_status_filter_changed(index: int) -> void:
	_status_filter = _status_filter_opt.get_item_text(index)
	_update_contract_list()

func _on_item_filter_changed(index: int) -> void:
	_item_filter = _item_filter_opt.get_item_text(index)
	_update_contract_list()

func _on_issuer_type_filter_changed(index: int) -> void:
	_issuer_type_filter = _issuer_type_filter_opt.get_item_text(index)
	_update_contract_list()

func _on_worker_filter_submitted(text: String) -> void:
	if text.strip_edges() == "" or text.to_lower() == "all":
		_worker_filter = -1
		_worker_filter_edit.text = "" # Clear to show placeholder
	else:
		if text.is_valid_int():
			_worker_filter = int(text)
		else:
			# If invalid, revert to all? or keep as is? 
			# Let's revert to -1 if invalid
			_worker_filter = -1
			_worker_filter_edit.text = ""
			
	_update_contract_list()
