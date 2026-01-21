class_name MarketPanel
extends PanelContainer

## Market panel - displays live market data with filtering.
## Shows reference prices, trade counts, active orders, taxes/tariffs, and denied orders.

var _sim_state: SimState = null

# UI state for filters
var _item_filter: String = ""  # Empty = all items
var _agent_filter: int = -1  # -1 = all agents

# Ring buffer for denied order reasons
var _denied_reasons: Array = []
const MAX_DENIED_REASONS: int = 10

# Performance caching
var _cached_items: Array = []
var _cached_agent_ids: Array = []
var _last_filter_update_tick: int = -1
var _last_market_update_tick: int = -1

# UI Elements
var _tab_container: TabContainer

# Prices tab
var _prices_scroll: ScrollContainer
var _prices_content: VBoxContainer
var _prices_filter: OptionButton

# Orders tab
var _orders_scroll: ScrollContainer
var _orders_content: VBoxContainer
var _orders_item_filter: OptionButton
var _orders_agent_filter: OptionButton

# Stats tab
var _stats_scroll: ScrollContainer
var _stats_content: VBoxContainer


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
	main_vbox.add_theme_constant_override("separation", 4)
	margin.add_child(main_vbox)
	
	# Header
	var header := Label.new()
	header.text = "Market Data"
	header.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	header.add_theme_font_size_override("font_size", 14)
	main_vbox.add_child(header)
	
	var sep := HSeparator.new()
	main_vbox.add_child(sep)
	
	# Tab container
	_tab_container = TabContainer.new()
	_tab_container.size_flags_vertical = Control.SIZE_EXPAND_FILL
	main_vbox.add_child(_tab_container)
	
	# Setup tabs
	_setup_prices_tab()
	_setup_orders_tab()
	_setup_stats_tab()


func _setup_prices_tab() -> void:
	var container := VBoxContainer.new()
	container.name = "Prices"
	_tab_container.add_child(container)
	
	# Filter row
	var filter_row := HBoxContainer.new()
	filter_row.add_theme_constant_override("separation", 4)
	container.add_child(filter_row)
	
	var filter_label := Label.new()
	filter_label.text = "Item:"
	filter_row.add_child(filter_label)
	
	_prices_filter = OptionButton.new()
	_prices_filter.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_prices_filter.item_selected.connect(_on_prices_filter_changed)
	filter_row.add_child(_prices_filter)
	
	# Scroll for prices
	_prices_scroll = ScrollContainer.new()
	_prices_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_prices_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	container.add_child(_prices_scroll)
	
	_prices_content = VBoxContainer.new()
	_prices_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_prices_content.add_theme_constant_override("separation", 2)
	_prices_scroll.add_child(_prices_content)


func _setup_orders_tab() -> void:
	var container := VBoxContainer.new()
	container.name = "Orders"
	_tab_container.add_child(container)
	
	# Filter row 1: Item
	var item_row := HBoxContainer.new()
	item_row.add_theme_constant_override("separation", 4)
	container.add_child(item_row)
	
	var item_label := Label.new()
	item_label.text = "Item:"
	item_row.add_child(item_label)
	
	_orders_item_filter = OptionButton.new()
	_orders_item_filter.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_orders_item_filter.item_selected.connect(_on_orders_item_filter_changed)
	item_row.add_child(_orders_item_filter)
	
	# Filter row 2: Agent
	var agent_row := HBoxContainer.new()
	agent_row.add_theme_constant_override("separation", 4)
	container.add_child(agent_row)
	
	var agent_label := Label.new()
	agent_label.text = "Agent:"
	agent_row.add_child(agent_label)
	
	_orders_agent_filter = OptionButton.new()
	_orders_agent_filter.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_orders_agent_filter.item_selected.connect(_on_orders_agent_filter_changed)
	agent_row.add_child(_orders_agent_filter)
	
	# Scroll for orders
	_orders_scroll = ScrollContainer.new()
	_orders_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_orders_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	container.add_child(_orders_scroll)
	
	_orders_content = VBoxContainer.new()
	_orders_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_orders_content.add_theme_constant_override("separation", 2)
	_orders_scroll.add_child(_orders_content)


func _setup_stats_tab() -> void:
	var container := VBoxContainer.new()
	container.name = "Stats"
	_tab_container.add_child(container)
	
	_stats_scroll = ScrollContainer.new()
	_stats_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_stats_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	container.add_child(_stats_scroll)
	
	_stats_content = VBoxContainer.new()
	_stats_content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_stats_content.add_theme_constant_override("separation", 2)
	_stats_scroll.add_child(_stats_content)


## Standard update method called by VisualizerMain
func update_sim_state(sim_state: SimState) -> void:
	update_market(sim_state)

## Update market panel with current sim state
func update_market(sim_state: SimState) -> void:
	if sim_state == null:
		return
	
	_sim_state = sim_state
	
	# Update filters only when data actually changes
	_update_filters_if_needed()
	
	# Update visible tab content
	match _tab_container.current_tab:
		0:
			_update_prices_tab()
		1:
			_update_orders_tab()
		2:
			_update_stats_tab()


## Update filters only when data actually changes
func _update_filters_if_needed() -> void:
	if _sim_state == null:
		return
	
	var current_tick = _sim_state.tick
	
	# Skip filter update if data hasn't changed
	if current_tick == _last_filter_update_tick:
		return
	
	# Check if market data changed
	var market: Market = _sim_state.market
	var items_changed := false
	var agents_changed := false
	
	# Check if items changed
	var current_items: Array = market.ref_price.keys()
	current_items.sort()
	if current_items != _cached_items:
		_cached_items = current_items.duplicate()
		items_changed = true
	
	# Check if agent list changed
	var current_agent_ids: Dictionary = {}
	for order in market.buy_orders:
		current_agent_ids[order["agent_id"]] = true
	for order in market.sell_orders:
		current_agent_ids[order["agent_id"]] = true
	
	var current_agent_list: Array = current_agent_ids.keys()
	current_agent_list.sort()
	if current_agent_list != _cached_agent_ids:
		_cached_agent_ids = current_agent_list.duplicate()
		agents_changed = true
	
	# Only rebuild filters if data actually changed
	if items_changed:
		_rebuild_item_filters(_cached_items)
	
	if agents_changed:
		_rebuild_agent_filters(_cached_agent_ids)
	
	_last_filter_update_tick = current_tick


## Rebuild item filters with new data
func _rebuild_item_filters(items: Array) -> void:
	# Update prices filter
	var current_prices_idx := _prices_filter.selected
	_prices_filter.clear()
	_prices_filter.add_item("All", 0)
	for i in range(items.size()):
		_prices_filter.add_item(items[i], i + 1)
	if current_prices_idx >= 0 and current_prices_idx < _prices_filter.item_count:
		_prices_filter.select(current_prices_idx)
	
	# Update orders item filter
	var current_orders_item_idx := _orders_item_filter.selected
	_orders_item_filter.clear()
	_orders_item_filter.add_item("All", 0)
	for i in range(items.size()):
		_orders_item_filter.add_item(items[i], i + 1)
	if current_orders_item_idx >= 0 and current_orders_item_idx < _orders_item_filter.item_count:
		_orders_item_filter.select(current_orders_item_idx)


## Rebuild agent filter with new data
func _rebuild_agent_filters(agent_ids: Array) -> void:
	var current_agent_idx := _orders_agent_filter.selected
	_orders_agent_filter.clear()
	_orders_agent_filter.add_item("All", 0)
	for i in range(agent_ids.size()):
		_orders_agent_filter.add_item("Agent #%d" % agent_ids[i], i + 1)
		_orders_agent_filter.set_item_metadata(i + 1, agent_ids[i])
	if current_agent_idx >= 0 and current_agent_idx < _orders_agent_filter.item_count:
		_orders_agent_filter.select(current_agent_idx)


## Legacy method for compatibility
func _update_filters() -> void:
	_update_filters_if_needed()


func _update_prices_tab() -> void:
	if _sim_state == null:
		return
	
	# Clear existing content
	for child in _prices_content.get_children():
		child.queue_free()
	
	var market: Market = _sim_state.market
	var items: Array = market.ref_price.keys()
	items.sort()
	
	# Apply filter
	var filter_idx := _prices_filter.selected
	if filter_idx > 0 and filter_idx <= items.size():
		items = [items[filter_idx - 1]]
	
	# Header row
	var header := _create_price_row("Item", "Ref", "Last", "Trades", true)
	_prices_content.add_child(header)
	_prices_content.add_child(HSeparator.new())
	
	# Data rows
	for item in items:
		var ref_price: float = market.ref_price.get(item, 0.0)
		var last_price: int = market.last_trade_price.get(item, 0)
		var trade_count: int = market.trade_counts.get(item, 0)
		
		var row := _create_price_row(
			item,
			"%.1f" % ref_price,
			str(last_price) if last_price > 0 else "--",
			str(trade_count)
		)
		_prices_content.add_child(row)


func _create_price_row(col1: String, col2: String, col3: String, col4: String, is_header: bool = false) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	
	var label1 := Label.new()
	label1.text = col1
	label1.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label1.custom_minimum_size.x = 70
	if is_header:
		label1.add_theme_font_size_override("font_size", 12)
	row.add_child(label1)
	
	var label2 := Label.new()
	label2.text = col2
	label2.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	label2.custom_minimum_size.x = 40
	if is_header:
		label2.add_theme_font_size_override("font_size", 12)
	row.add_child(label2)
	
	var label3 := Label.new()
	label3.text = col3
	label3.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	label3.custom_minimum_size.x = 40
	if is_header:
		label3.add_theme_font_size_override("font_size", 12)
	row.add_child(label3)
	
	var label4 := Label.new()
	label4.text = col4
	label4.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	label4.custom_minimum_size.x = 40
	if is_header:
		label4.add_theme_font_size_override("font_size", 12)
	row.add_child(label4)
	
	return row


func _update_orders_tab() -> void:
	if _sim_state == null:
		return
	
	# Clear existing content
	for child in _orders_content.get_children():
		child.queue_free()
	
	var market: Market = _sim_state.market
	
	# Get filter values
	var item_filter := ""
	var item_idx := _orders_item_filter.selected
	if item_idx > 0:
		item_filter = _orders_item_filter.get_item_text(item_idx)
	
	var agent_filter := -1
	var agent_idx := _orders_agent_filter.selected
	if agent_idx > 0:
		agent_filter = _orders_agent_filter.get_item_metadata(agent_idx)
	
	# Filter and collect orders
	var buy_orders := _filter_orders(market.buy_orders, item_filter, agent_filter)
	var sell_orders := _filter_orders(market.sell_orders, item_filter, agent_filter)
	
	# Sort: buy orders by price DESC, sell by price ASC
	buy_orders.sort_custom(func(a, b): return a["price"] > b["price"])
	sell_orders.sort_custom(func(a, b): return a["price"] < b["price"])
	
	# Limit to prevent UI freeze
	const MAX_DISPLAY := 15
	
	# Buy orders section
	var buy_header := Label.new()
	buy_header.text = "BUY ORDERS (%d)" % buy_orders.size()
	buy_header.add_theme_font_size_override("font_size", 12)
	_orders_content.add_child(buy_header)
	
	if buy_orders.is_empty():
		var empty := Label.new()
		empty.text = "(none)"
		empty.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
		_orders_content.add_child(empty)
	else:
		for i in range(mini(buy_orders.size(), MAX_DISPLAY)):
			var order: Dictionary = buy_orders[i]
			var row := _create_order_row(order)
			_orders_content.add_child(row)
		if buy_orders.size() > MAX_DISPLAY:
			var more := Label.new()
			more.text = "... and %d more" % (buy_orders.size() - MAX_DISPLAY)
			more.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
			_orders_content.add_child(more)
	
	_orders_content.add_child(HSeparator.new())
	
	# Sell orders section
	var sell_header := Label.new()
	sell_header.text = "SELL ORDERS (%d)" % sell_orders.size()
	sell_header.add_theme_font_size_override("font_size", 12)
	_orders_content.add_child(sell_header)
	
	if sell_orders.is_empty():
		var empty := Label.new()
		empty.text = "(none)"
		empty.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
		_orders_content.add_child(empty)
	else:
		for i in range(mini(sell_orders.size(), MAX_DISPLAY)):
			var order: Dictionary = sell_orders[i]
			var row := _create_order_row(order)
			_orders_content.add_child(row)
		if sell_orders.size() > MAX_DISPLAY:
			var more := Label.new()
			more.text = "... and %d more" % (sell_orders.size() - MAX_DISPLAY)
			more.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
			_orders_content.add_child(more)


func _filter_orders(orders: Array, item_filter: String, agent_filter: int) -> Array:
	var result := []
	for order in orders:
		if item_filter != "" and order["item"] != item_filter:
			continue
		if agent_filter >= 0 and order["agent_id"] != agent_filter:
			continue
		result.append(order)
	return result


func _create_order_row(order: Dictionary) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 4)
	
	# Agent ID
	var agent_label := Label.new()
	agent_label.text = "#%d" % order["agent_id"]
	agent_label.custom_minimum_size.x = 35
	row.add_child(agent_label)
	
	# Item
	var item_label := Label.new()
	item_label.text = order["item"]
	item_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	item_label.clip_text = true
	row.add_child(item_label)
	
	# Qty
	var qty_label := Label.new()
	qty_label.text = "×%d" % order["qty"]
	qty_label.custom_minimum_size.x = 30
	row.add_child(qty_label)
	
	# Price
	var price_label := Label.new()
	price_label.text = "@%d" % order["price"]
	price_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	price_label.custom_minimum_size.x = 40
	row.add_child(price_label)
	
	return row


func _update_stats_tab() -> void:
	if _sim_state == null:
		return
	
	# Clear existing content
	for child in _stats_content.get_children():
		child.queue_free()
	
	var market: Market = _sim_state.market
	
	# Trade totals
	var trades_header := Label.new()
	trades_header.text = "TRADE STATISTICS"
	trades_header.add_theme_font_size_override("font_size", 12)
	_stats_content.add_child(trades_header)
	
	var total_label := Label.new()
	total_label.text = "Total trades: %d" % market.total_trades
	_stats_content.add_child(total_label)
	
	# Trade counts by item
	var items: Array = market.trade_counts.keys()
	items.sort()
	for item in items:
		var count: int = market.trade_counts.get(item, 0)
		if count > 0:
			var item_label := Label.new()
			item_label.text = "  %s: %d" % [item, count]
			_stats_content.add_child(item_label)
	
	_stats_content.add_child(HSeparator.new())
	
	# Taxes & Tariffs
	var taxes_header := Label.new()
	taxes_header.text = "TAXES & TARIFFS"
	taxes_header.add_theme_font_size_override("font_size", 12)
	_stats_content.add_child(taxes_header)
	
	var taxes_label := Label.new()
	taxes_label.text = "Sales tax collected: %d" % _sim_state.taxes_collected
	_stats_content.add_child(taxes_label)
	
	var tariffs_label := Label.new()
	tariffs_label.text = "Tariffs collected: %d" % market.tariff_collected_total
	_stats_content.add_child(tariffs_label)
	
	# Tariffs by faction
	if not market.tariff_by_faction.is_empty():
		var faction_ids: Array = market.tariff_by_faction.keys()
		faction_ids.sort()
		for faction_id in faction_ids:
			var amount: int = market.tariff_by_faction[faction_id]
			var faction_label := Label.new()
			faction_label.text = "  Faction #%d: %d" % [faction_id, amount]
			_stats_content.add_child(faction_label)
	
	_stats_content.add_child(HSeparator.new())
	
	# Denied orders
	var denied_header := Label.new()
	denied_header.text = "DENIED ORDERS"
	denied_header.add_theme_font_size_override("font_size", 12)
	_stats_content.add_child(denied_header)
	
	var embargo_label := Label.new()
	embargo_label.text = "Denied (embargo): %d" % market.orders_denied_embargo
	_stats_content.add_child(embargo_label)
	
	# Show recent denied reasons if we have any
	if not _denied_reasons.is_empty():
		var reasons_label := Label.new()
		reasons_label.text = "Recent denials:"
		_stats_content.add_child(reasons_label)
		for reason in _denied_reasons:
			var reason_label := Label.new()
			reason_label.text = "  • " + reason
			reason_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			_stats_content.add_child(reason_label)


## Add a denied order reason to the ring buffer
func add_denied_reason(reason: String) -> void:
	_denied_reasons.append(reason)
	if _denied_reasons.size() > MAX_DENIED_REASONS:
		_denied_reasons.pop_front()


## Clear the panel
func clear() -> void:
	_sim_state = null
	_denied_reasons.clear()
	
	for child in _prices_content.get_children():
		child.queue_free()
	for child in _orders_content.get_children():
		child.queue_free()
	for child in _stats_content.get_children():
		child.queue_free()


# Filter signal handlers
func _on_prices_filter_changed(_index: int) -> void:
	_update_prices_tab()


func _on_orders_item_filter_changed(_index: int) -> void:
	_update_orders_tab()


func _on_orders_agent_filter_changed(_index: int) -> void:
	_update_orders_tab()
