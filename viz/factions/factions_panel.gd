class_name FactionsPanel
extends PanelContainer

## Factions panel - displays list of factions and their details.
## Shows laws, relations, and active proposals.

var _sim_state: SimState = null
var _selected_faction_id: int = -1

# UI Elements
var _split_container: VSplitContainer
var _faction_list: ItemList
var _details_container: TabContainer

# Detail Tabs
var _overview_content: VBoxContainer
var _relations_content: VBoxContainer
var _proposals_content: VBoxContainer

func _ready() -> void:
	_setup_ui()

func _setup_ui() -> void:
	custom_minimum_size = Vector2(240, 300)
	
	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 8)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_right", 8)
	margin.add_theme_constant_override("margin_bottom", 8)
	add_child(margin)
	
	var main_vbox := VBoxContainer.new()
	main_vbox.add_theme_constant_override("separation", 4)
	margin.add_child(main_vbox)
	
	# Header
	var header := Label.new()
	header.text = "Factions"
	header.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	header.add_theme_font_size_override("font_size", 16)
	main_vbox.add_child(header)
	
	var sep := HSeparator.new()
	main_vbox.add_child(sep)
	
	# Split Container (List | Details)
	_split_container = VSplitContainer.new()
	_split_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_split_container.split_offset = 120
	main_vbox.add_child(_split_container)
	
	# Left: Faction List
	var list_container := VBoxContainer.new()
	_split_container.add_child(list_container)
	
	var list_label := Label.new()
	list_label.text = "Faction List"
	list_container.add_child(list_label)
	
	_faction_list = ItemList.new()
	_faction_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_faction_list.item_selected.connect(_on_faction_selected)
	list_container.add_child(_faction_list)
	
	# Right: Details
	_details_container = TabContainer.new()
	_details_container.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_split_container.add_child(_details_container)
	
	_setup_overview_tab()
	_setup_relations_tab()
	_setup_proposals_tab()

func _setup_overview_tab() -> void:
	var scroll := ScrollContainer.new()
	scroll.name = "Overview"
	_details_container.add_child(scroll)
	
	_overview_content = VBoxContainer.new()
	_overview_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_overview_content.add_theme_constant_override("separation", 4)
	scroll.add_child(_overview_content)

func _setup_relations_tab() -> void:
	var scroll := ScrollContainer.new()
	scroll.name = "Relations"
	_details_container.add_child(scroll)
	
	_relations_content = VBoxContainer.new()
	_relations_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_relations_content.add_theme_constant_override("separation", 4)
	scroll.add_child(_relations_content)

func _setup_proposals_tab() -> void:
	var scroll := ScrollContainer.new()
	scroll.name = "Proposals"
	_details_container.add_child(scroll)
	
	_proposals_content = VBoxContainer.new()
	_proposals_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_proposals_content.add_theme_constant_override("separation", 4)
	scroll.add_child(_proposals_content)

## Standard update method called by VisualizerMain
func update_sim_state(sim_state: SimState) -> void:
	update_factions(sim_state)

## Update panel with current sim state
func update_factions(sim_state: SimState) -> void:
	if sim_state == null:
		return
	
	_sim_state = sim_state
	
	_update_list()
	_update_details()

func _update_list() -> void:
	if _sim_state == null:
		return
		
	# Store currently selected ID
	# (We rely on _selected_faction_id variable, but need to re-select index)
	
	_faction_list.clear()
	
	if _sim_state.factions.is_empty():
		_selected_faction_id = -1
		var placeholder_idx := _faction_list.add_item("No factions formed yet")
		_faction_list.set_item_disabled(placeholder_idx, true)
		
		var min_money := _sim_state.get_tuning_int("faction_found_min_money", 0)
		var min_grievance := _sim_state.get_tuning_float("faction_found_min_grievance", 0.0)
		var daily_chance := _sim_state.get_tuning_float("faction_found_daily_chance", 0.0)
		var treasury_seed := _sim_state.get_tuning_int("faction_found_treasury_seed", 0)
		var money_candidates := 0
		var grievance_candidates := 0
		
		for agent in _sim_state.agents:
			if agent.faction_id != 0:
				continue
			if agent.money >= min_money and agent.money >= treasury_seed:
				money_candidates += 1
				if agent.grievance >= min_grievance:
					grievance_candidates += 1
		
		var criteria_idx := _faction_list.add_item("Formation criteria:")
		_faction_list.set_item_disabled(criteria_idx, true)
		
		var eligibility_idx := _faction_list.add_item("- Eligible agents: %d money / %d grievance" % [money_candidates, grievance_candidates])
		_faction_list.set_item_disabled(eligibility_idx, true)
		
		var money_line_idx := _faction_list.add_item("- Min money: %d" % min_money)
		_faction_list.set_item_disabled(money_line_idx, true)
		
		var grievance_line_idx := _faction_list.add_item("- Grievance: ≥ %.2f" % min_grievance)
		_faction_list.set_item_disabled(grievance_line_idx, true)
		
		var chance_percent := daily_chance * 100.0
		var chance_line_idx := _faction_list.add_item("- Daily chance: %.1f%%" % chance_percent)
		_faction_list.set_item_disabled(chance_line_idx, true)
		return
	
	var factions := _sim_state.factions.duplicate()
	factions.sort_custom(func(a, b): return a.id < b.id)
	
	var selected_idx: int = -1
	
	for i in range(factions.size()):
		var f: Faction = factions[i]
		
		# Get claims count
		var owner_id := World.owner_id_for_faction(f.id)
		var claims_count := 0
		# This is expensive to iterate all tiles every frame if world is huge.
		# Optimization: SimState.world has a method get_claims_by_owner() or similar?
		# World has get_claims_by_owner() which returns dict.
		# Let's use that if possible, but we don't want to re-compute it for every faction inside the loop.
		# We should compute it once.
		
		# Simple optimization: pass mapped counts
		
		var text := "%s (ID: %d)" % [f.name, f.id]
		var idx := _faction_list.add_item(text)
		_faction_list.set_item_metadata(idx, f.id)
		
		if f.id == _selected_faction_id:
			selected_idx = idx
	
	if selected_idx != -1:
		_faction_list.select(selected_idx)
	elif _faction_list.item_count > 0 and _selected_faction_id == -1:
		# Auto select first if none selected
		_faction_list.select(0)
		_selected_faction_id = _faction_list.get_item_metadata(0)
		_update_details()

func _on_faction_selected(index: int) -> void:
	_selected_faction_id = _faction_list.get_item_metadata(index)
	_update_details()

func _update_details() -> void:
	if _sim_state == null or _selected_faction_id == -1:
		_clear_details()
		return
	
	# Find faction
	var faction: Faction = null
	for f in _sim_state.factions:
		if f.id == _selected_faction_id:
			faction = f
			break
	
	if faction == null:
		_clear_details()
		return
	
	_update_overview(faction)
	_update_relations(faction)
	_update_proposals(faction)

func _clear_details() -> void:
	for child in _overview_content.get_children():
		child.queue_free()
	for child in _relations_content.get_children():
		child.queue_free()
	for child in _proposals_content.get_children():
		child.queue_free()

func _add_label_value(container: Control, label: String, value: String) -> void:
	var hbox := HBoxContainer.new()
	container.add_child(hbox)
	
	var l := Label.new()
	l.text = label
	l.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	l.add_theme_color_override("font_color", Color(0.7, 0.7, 0.7))
	hbox.add_child(l)
	
	var v := Label.new()
	v.text = value
	hbox.add_child(v)

func _update_overview(faction: Faction) -> void:
	for child in _overview_content.get_children():
		child.queue_free()
		
	# Basic Info
	var header := Label.new()
	header.text = faction.name
	header.add_theme_font_size_override("font_size", 18)
	header.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_overview_content.add_child(header)
	_overview_content.add_child(HSeparator.new())
	
	_add_label_value(_overview_content, "ID:", str(faction.id))
	_add_label_value(_overview_content, "Members:", str(faction.get_member_count()))
	_add_label_value(_overview_content, "Treasury:", str(faction.treasury))
	_add_label_value(_overview_content, "Home Pos:", "%s" % faction.home_pos)
	_add_label_value(_overview_content, "Openness:", "%.2f" % faction.openness)
	
	# Claims count
	var owner_id := World.owner_id_for_faction(faction.id)
	var claims_map := _sim_state.world.get_claims_by_owner()
	var claims_arr = claims_map.get(owner_id, [])
	var claims_count: int = claims_arr.size() if claims_arr is Array else 0
	_add_label_value(_overview_content, "Land Claims:", str(claims_count))
	
	_overview_content.add_child(HSeparator.new())
	
	# Laws
	var laws_header := Label.new()
	laws_header.text = "Laws & Taxes"
	laws_header.add_theme_font_size_override("font_size", 14)
	_overview_content.add_child(laws_header)
	
	var laws: Laws = _sim_state.get_laws(owner_id)
	
	_add_label_value(_overview_content, "Harvest Permit:", "Required" if laws.harvest_permit_required else "Free")
	_add_label_value(_overview_content, "Build Permit:", "Required" if laws.build_permit_required else "Free")
	_add_label_value(_overview_content, "Sales Tax:", "%d%%" % laws.sales_tax_rate)
	_add_label_value(_overview_content, "Base Fine:", str(laws.fine_base))

func _update_relations(faction: Faction) -> void:
	for child in _relations_content.get_children():
		child.queue_free()
	
	if faction.relations.is_empty():
		var l := Label.new()
		l.text = "No specific relations."
		_relations_content.add_child(l)
	else:
		# Header
		var header_row := HBoxContainer.new()
		_relations_content.add_child(header_row)
		var h1 := Label.new(); h1.text = "Target"; h1.size_flags_horizontal = Control.SIZE_EXPAND_FILL; header_row.add_child(h1)
		var h2 := Label.new(); h2.text = "Policy"; h2.custom_minimum_size.x=60; header_row.add_child(h2)
		var h3 := Label.new(); h3.text = "Tariff"; h3.custom_minimum_size.x=40; header_row.add_child(h3)
		
		var keys := faction.relations.keys()
		keys.sort()
		
		for key in keys:
			var rel = faction.relations[key]
			var row := HBoxContainer.new()
			_relations_content.add_child(row)
			
			var target_name: String = str(key)
			if key == "faction:0":
				target_name = "Factionless"
			elif key.begins_with("faction:"):
				target_name = "Faction #%s" % key.substr(8)
			
			var l1 := Label.new(); l1.text = target_name; l1.size_flags_horizontal = Control.SIZE_EXPAND_FILL; row.add_child(l1)
			
			var policy: String = rel.get("policy", "open")
			var color := Color.WHITE
			if policy == "embargo": color = Color(1, 0.5, 0.5)
			elif policy == "tariff": color = Color(1, 1, 0.7)
			else: color = Color(0.7, 1, 0.7)
			
			var l2 := Label.new(); l2.text = policy.capitalize(); l2.custom_minimum_size.x=60; 
			l2.add_theme_color_override("font_color", color)
			row.add_child(l2)
			
			var l3 := Label.new(); l3.text = "%d%%" % rel.get("tariff_rate", 0); l3.custom_minimum_size.x=40;
			if policy == "tariff": l3.add_theme_color_override("font_color", Color(1, 1, 0.7))
			elif policy == "embargo": l3.text = "-"
			row.add_child(l3)

func _update_proposals(faction: Faction) -> void:
	for child in _proposals_content.get_children():
		child.queue_free()
	
	var active_proposals: Array = faction.get_active_proposals(_sim_state.tick)
	
	if active_proposals.is_empty():
		var l := Label.new()
		l.text = "No active proposals."
		l.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
		_proposals_content.add_child(l)
	else:
		for p in active_proposals:
			var p_panel := PanelContainer.new()
			_proposals_content.add_child(p_panel)
			
			var vbox := VBoxContainer.new()
			p_panel.add_child(vbox)
			
			var title_row := HBoxContainer.new()
			vbox.add_child(title_row)
			
			var title := Label.new()
			title.text = "Proposal #%d" % p["proposal_id"]
			title.add_theme_font_size_override("font_size", 14)
			title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			title_row.add_child(title)
			
			var time_left := Label.new()
			var ticks_left: int = int(p["expires_tick"]) - _sim_state.tick
			time_left.text = "%d ticks left" % ticks_left
			title_row.add_child(time_left)
			
			vbox.add_child(HSeparator.new())
			
			# Describe changes
			var changes: Dictionary = p.get("changes", {})
			for k in changes:
				var desc := ""
				var val = changes[k]
				match k:
					"harvest_permit_required": desc = "Harvest Permit: %s" % ("On" if val else "Off")
					"build_permit_required": desc = "Build Permit: %s" % ("On" if val else "Off")
					"sales_tax_rate": desc = "Sales Tax -> %d%%" % val
					"fine_base": desc = "Base Fine -> %d" % val
					"set_relation_policy":
						var target = val.get("target_key", "?")
						if target == "faction:0": target = "Factionless"
						elif target.begins_with("faction:"): target = "Faction #%s" % target.substr(8)
						desc = "Relation %s -> %s (Tariff: %d%%)" % [target, val.get("policy"), val.get("tariff_rate")]
					_: desc = "%s: %s" % [k, str(val)]
				
				var change_label := Label.new()
				change_label.text = "• " + desc
				vbox.add_child(change_label)
			
			vbox.add_child(HSeparator.new())
			
			# Votes
			var votes_row := HBoxContainer.new()
			vbox.add_child(votes_row)
			var v_for: Array = p.get("votes_for", [])
			var v_against: Array = p.get("votes_against", [])
			
			var l_for := Label.new()
			l_for.text = "Yes: %d" % v_for.size()
			l_for.add_theme_color_override("font_color", Color(0.6, 1.0, 0.6))
			l_for.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			votes_row.add_child(l_for)
			
			var l_against := Label.new()
			l_against.text = "No: %d" % v_against.size()
			l_against.add_theme_color_override("font_color", Color(1.0, 0.6, 0.6))
			l_against.size_flags_horizontal = Control.SIZE_EXPAND_FILL
			votes_row.add_child(l_against)
