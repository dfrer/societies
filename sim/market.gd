## Market class - order book with matching and price tracking
## Handles buy/sell orders with deterministic matching
class_name Market
extends RefCounted

var buy_orders: Array = []  # Array of order dictionaries
var sell_orders: Array = []  # Array of order dictionaries
var next_order_id: int = 1
var ref_price: Dictionary = {}  # item -> float (EMA reference price)
var last_trade_price: Dictionary = {}  # item -> int
var trade_counts: Dictionary = {}  # item -> int (total trades)
var total_trades: int = 0

## Trade policy counters
var orders_denied_embargo: int = 0
var tariff_collected_total: int = 0
var tariff_by_faction: Dictionary = {}  # faction_id -> int
var trades_this_day: Dictionary = {} # item -> int (reset daily)

## Order structure:
## {
##   "order_id": int,
##   "type": "buy"|"sell",
##   "item": String,
##   "qty": int,
##   "price": int,
##   "agent_id": int,
##   "created_tick": int,
##   "expires_tick": int
## }

func _init() -> void:
	buy_orders = []
	sell_orders = []
	tariff_by_faction = {}

## Initialize reference prices from items config
func init_prices(items: Dictionary) -> void:
	for item_name in items:
		var item_data: Dictionary = items[item_name]
		ref_price[item_name] = float(item_data.get("base_value", 10))
		trade_counts[item_name] = 0

## Get reference price for an item
func get_ref_price(item: String) -> float:
	return ref_price.get(item, 10.0)

## Create a buy order (does not place it yet)
func create_order(type: String, item: String, qty: int, price: int, agent_id: int, 
				  current_tick: int, ttl: int) -> Dictionary:
	var order := {
		"order_id": next_order_id,
		"type": type,
		"item": item,
		"qty": qty,
		"price": price,
		"agent_id": agent_id,
		"created_tick": current_tick,
		"expires_tick": current_tick + ttl
	}
	next_order_id += 1
	return order

## Place a buy order
func place_buy_order(order: Dictionary) -> void:
	buy_orders.append(order.duplicate())

## Place a sell order
func place_sell_order(order: Dictionary) -> void:
	sell_orders.append(order.duplicate())

## Check if agent can place an order (embargo/ban check)
## Returns Dictionary with {allowed: bool, reason_code: String, details: Dictionary}
func can_place_order(agent, state: SimState, world: World, market_pos: Vector2i) -> Dictionary:
	# Check market ban first
	if agent.is_market_banned(state.tick):
		return {"allowed": false, "reason_code": EnforcementResult.MARKET_BANNED, "details": {}}
	
	# Check embargo from market owner faction
	var market_owner_id := world.get_claim_owner(market_pos.x, market_pos.y)
	if not World.is_faction_owner(market_owner_id):
		return {"allowed": true, "reason_code": EnforcementResult.OK, "details": {}}  # Non-faction owner = no embargo
	
	var owner_faction_id := World.faction_id_from_owner(market_owner_id)
	
	# Agent in same faction = always allowed
	if agent.faction_id == owner_faction_id:
		return {"allowed": true, "reason_code": EnforcementResult.OK, "details": {}}
	
	# Look up owner faction's policy toward this agent
	var owner_faction: Faction = null
	for faction in state.factions:
		if faction.id == owner_faction_id:
			owner_faction = faction
			break
	
	if owner_faction == null:
		return {"allowed": true, "reason_code": EnforcementResult.OK, "details": {}}  # Faction not found = allow
	
	var policy := owner_faction.get_trade_policy_for_agent(agent, state.tuning)
	if policy["policy"] == "embargo":
		orders_denied_embargo += 1
		return {"allowed": false, "reason_code": EnforcementResult.EMBARGO, "details": {"policy": "embargo"}}
	
	return {"allowed": true, "reason_code": EnforcementResult.OK, "details": {}}

## Check if agent has an active order for item/type
func has_active_order(agent_id: int, item: String, order_type: String) -> bool:
	var orders := buy_orders if order_type == "buy" else sell_orders
	for order in orders:
		if order["agent_id"] == agent_id and order["item"] == item:
			return true
	return false

## Get orders by agent ID
func get_agent_orders(agent_id: int) -> Array:
	var result := []
	for order in buy_orders:
		if order["agent_id"] == agent_id:
			result.append(order)
	for order in sell_orders:
		if order["agent_id"] == agent_id:
			result.append(order)
	return result

## Remove expired orders and release locked resources
func expire_orders(current_tick: int, agents: Array) -> void:
	# Build agent lookup
	var agent_map := {}
	for agent in agents:
		agent_map[agent.id] = agent
	
	# Expire buy orders
	var new_buy_orders := []
	for order in buy_orders:
		if order["expires_tick"] <= current_tick:
			# Release locked money
			var agent = agent_map.get(order["agent_id"])
			if agent:
				var locked_amount: int = order["qty"] * order["price"]
				agent.release_locked_money(locked_amount)
		else:
			new_buy_orders.append(order)
	buy_orders = new_buy_orders
	
	# Expire sell orders
	var new_sell_orders := []
	for order in sell_orders:
		if order["expires_tick"] <= current_tick:
			# Release locked inventory
			var agent = agent_map.get(order["agent_id"])
			if agent:
				agent.release_locked_item(order["item"], order["qty"])
		else:
			new_sell_orders.append(order)
	sell_orders = new_sell_orders

## Sort buy orders: price DESC, created_tick ASC, order_id ASC
func _sort_buy_orders() -> void:
	buy_orders.sort_custom(func(a, b):
		if a["price"] != b["price"]:
			return a["price"] > b["price"]  # Higher price first
		if a["created_tick"] != b["created_tick"]:
			return a["created_tick"] < b["created_tick"]
		return a["order_id"] < b["order_id"]
	)

## Sort sell orders: price ASC, created_tick ASC, order_id ASC
func _sort_sell_orders() -> void:
	sell_orders.sort_custom(func(a, b):
		if a["price"] != b["price"]:
			return a["price"] < b["price"]  # Lower price first
		if a["created_tick"] != b["created_tick"]:
			return a["created_tick"] < b["created_tick"]
		return a["order_id"] < b["order_id"]
	)

## Match orders and execute trades (legacy without tax)
func match_orders(agents: Array, tuning: Dictionary) -> int:
	var trades_this_cycle := 0
	var alpha: float = tuning.get("price_ema_alpha", 0.2)
	
	# Build agent lookup
	var agent_map := {}
	for agent in agents:
		agent_map[agent.id] = agent
	
	# Get unique items with orders
	var items_with_orders := {}
	for order in buy_orders:
		items_with_orders[order["item"]] = true
	for order in sell_orders:
		items_with_orders[order["item"]] = true
	
	# Match each item separately
	var items := items_with_orders.keys()
	items.sort()
	for item in items:
		trades_this_cycle += _match_item(item, agent_map, alpha, null, null, Vector2i(-1, -1))
	
	return trades_this_cycle

## Match orders with sales tax support
func match_orders_with_tax(agents: Array, tuning: Dictionary, state: SimState, 
						   world: World, market_pos: Vector2i) -> int:
	var trades_this_cycle := 0
	var alpha: float = tuning.get("price_ema_alpha", 0.2)
	
	# Build agent lookup
	var agent_map := {}
	for agent in agents:
		agent_map[agent.id] = agent
	
	# Get unique items with orders
	var items_with_orders := {}
	for order in buy_orders:
		items_with_orders[order["item"]] = true
	for order in sell_orders:
		items_with_orders[order["item"]] = true
	
	# Match each item separately
	var items := items_with_orders.keys()
	items.sort()
	for item in items:
		trades_this_cycle += _match_item(item, agent_map, alpha, state, world, market_pos)
	
	return trades_this_cycle

## Match orders for a single item (with optional tax support)
func _match_item(item: String, agent_map: Dictionary, alpha: float,
				 state: SimState, world: World, market_pos: Vector2i) -> int:
	var trades := 0
	
	# Filter orders for this item
	var item_buys := []
	var item_sells := []
	for order in buy_orders:
		if order["item"] == item:
			item_buys.append(order)
	for order in sell_orders:
		if order["item"] == item:
			item_sells.append(order)
	
	if item_buys.is_empty() or item_sells.is_empty():
		return 0
	
	# Sort orders
	item_buys.sort_custom(func(a, b):
		if a["price"] != b["price"]:
			return a["price"] > b["price"]
		if a["created_tick"] != b["created_tick"]:
			return a["created_tick"] < b["created_tick"]
		return a["order_id"] < b["order_id"]
	)
	item_sells.sort_custom(func(a, b):
		if a["price"] != b["price"]:
			return a["price"] < b["price"]
		if a["created_tick"] != b["created_tick"]:
			return a["created_tick"] < b["created_tick"]
		return a["order_id"] < b["order_id"]
	)
	
	# Determine sales tax rate based on market tile jurisdiction
	var tax_rate: int = 0
	if state != null and world != null and market_pos.x >= 0:
		var market_owner := world.get_claim_owner(market_pos.x, market_pos.y)
		var laws := state.get_laws(market_owner)
		tax_rate = laws.sales_tax_rate
	
	# Match loop
	var buy_idx := 0
	var sell_idx := 0
	
	while buy_idx < item_buys.size() and sell_idx < item_sells.size():
		var buy_order: Dictionary = item_buys[buy_idx]
		var sell_order: Dictionary = item_sells[sell_idx]
		
		# Check if can match
		if buy_order["price"] < sell_order["price"]:
			break  # No more matches possible
		
		# Don't match self-trades
		if buy_order["agent_id"] == sell_order["agent_id"]:
			sell_idx += 1
			continue
		
		# Calculate trade
		var trade_qty: int = mini(buy_order["qty"], sell_order["qty"])
		var trade_price: int = (buy_order["price"] + sell_order["price"]) / 2
		
		# Get agents
		var buyer = agent_map.get(buy_order["agent_id"])
		var seller = agent_map.get(sell_order["agent_id"])
		
		if buyer == null or seller == null:
			buy_idx += 1
			continue
		
		# Execute trade
		var total_cost: int = trade_qty * trade_price
		
		# Calculate sales tax (deducted from seller's proceeds)
		var tax_amount: int = 0
		if state != null and tax_rate > 0:
			tax_amount = int(floor(float(total_cost) * tax_rate / 100.0))
		
		# Calculate tariff for foreign sellers
		var tariff_amount: int = 0
		var owner_faction_id: int = -1
		if state != null and world != null and market_pos.x >= 0:
			var market_owner_id := world.get_claim_owner(market_pos.x, market_pos.y)
			if World.is_faction_owner(market_owner_id):
				owner_faction_id = World.faction_id_from_owner(market_owner_id)
				# Check if seller is foreign to market owner faction
				if seller.faction_id != owner_faction_id:
					var owner_faction: Faction = null
					for f in state.factions:
						if f.id == owner_faction_id:
							owner_faction = f
							break
					if owner_faction != null:
						var policy := owner_faction.get_trade_policy_for_agent(seller, state.tuning)
						if policy["policy"] == "tariff":
							var tariff_rate: int = policy["tariff_rate"]
							tariff_amount = int(floor(float(total_cost) * tariff_rate / 100.0))
		
		# Transfer from buyer's locked money to seller's money
		# Buyer Settlement:
		# 1. Release the locked money (reserved for this trade)
		var reserved_for_trade: int = trade_qty * buy_order["price"]
		buyer.release_locked_money(reserved_for_trade)
		
		# 2. Pay the actual cost from available money (which now includes the released amount)
		# Note: buyer.money includes locked_money, so we just decrement total.
		# release_locked_money only updates the locked_money counter.
		buyer.money -= total_cost
		
		# Seller receives total minus tax and tariff
		seller.money += total_cost - tax_amount - tariff_amount
		
		# Route tax to market owner
		if state != null and tax_amount > 0 and world != null and market_pos.x >= 0:
			var market_owner := world.get_claim_owner(market_pos.x, market_pos.y)
			_route_tax(tax_amount, market_owner, state)
		
		# Route tariff to market owner faction treasury
		if tariff_amount > 0 and owner_faction_id >= 0:
			tariff_collected_total += tariff_amount
			tariff_by_faction[owner_faction_id] = tariff_by_faction.get(owner_faction_id, 0) + tariff_amount
			# Route tariff through tax routing (same destination)
			var market_owner := world.get_claim_owner(market_pos.x, market_pos.y)
			_route_tax(tariff_amount, market_owner, state)
		
		# Transfer from seller's locked inventory to buyer's inventory
		seller.consume_locked_item(item, trade_qty)
		buyer.add_item(item, trade_qty)
		
		# Update order quantities
		buy_order["qty"] -= trade_qty
		sell_order["qty"] -= trade_qty
		
		# Update reference price (EMA)
		var old_ref: float = ref_price.get(item, float(trade_price))
		ref_price[item] = old_ref * (1.0 - alpha) + float(trade_price) * alpha
		last_trade_price[item] = trade_price
		trade_counts[item] = trade_counts.get(item, 0) + 1
		trades_this_day[item] = trades_this_day.get(item, 0) + 1
		total_trades += 1
		trades += 1
		
		# Log event
		if state != null:
			state.log_event("trade_executed", {
				"buyer_id": buyer.id,
				"seller_id": seller.id,
				"item": item,
				"qty": trade_qty,
				"price": trade_price,
				"tax": tax_amount,
				"tariff": tariff_amount
			})
		
		# Move to next order if depleted
		if buy_order["qty"] <= 0:
			buy_idx += 1
		if sell_order["qty"] <= 0:
			sell_idx += 1
	
	# Remove filled orders from main arrays
	_remove_filled_orders()
	
	return trades

## Route tax to jurisdiction owner
func _route_tax(tax_amount: int, owner_id: int, state: SimState) -> void:
	state.taxes_collected += tax_amount
	
	if owner_id == 0:
		# Unclaimed - goes to sink
		state.world_fines_sink += tax_amount
	elif World.is_faction_owner(owner_id):
		# Faction treasury
		var faction_id := World.faction_id_from_owner(owner_id)
		for faction in state.factions:
			if faction.id == faction_id:
				faction.treasury += tax_amount
				break
	else:
		# Agent landowner
		var owner := state.get_agent(owner_id)
		if owner != null:
			owner.money += tax_amount
		else:
			state.world_fines_sink += tax_amount

## Remove orders with qty <= 0
func _remove_filled_orders() -> void:
	buy_orders = buy_orders.filter(func(o): return o["qty"] > 0)
	sell_orders = sell_orders.filter(func(o): return o["qty"] > 0)

## Get trade count for an item
func get_trade_count(item: String) -> int:
	return trade_counts.get(item, 0)

## Clear all orders
func clear_orders() -> void:
	buy_orders.clear()
	sell_orders.clear()

## Serialize market to dictionary
func to_dict() -> Dictionary:
	# Serialize tariff_by_faction with string keys for JSON
	var tariff_data := {}
	for faction_id in tariff_by_faction:
		tariff_data[str(faction_id)] = tariff_by_faction[faction_id]
	
	return {
		"buy_orders": buy_orders.duplicate(true),
		"sell_orders": sell_orders.duplicate(true),
		"next_order_id": next_order_id,
		"ref_price": _serialize_ref_price(),
		"last_trade_price": last_trade_price.duplicate(),
		"trade_counts": trade_counts.duplicate(),
		"total_trades": total_trades,
		"orders_denied_embargo": orders_denied_embargo,
		"tariff_collected_total": tariff_collected_total,
		"tariff_by_faction": tariff_data
	}

func process_price_decay(tuning: Dictionary) -> void:
	var decay_rate: float = tuning.get("market_decay_rate", 0.02)
	var min_price: float = float(tuning.get("min_price", 1))
	var items_to_decay := []
	
	for item in ref_price:
		if trades_this_day.get(item, 0) == 0:
			items_to_decay.append(item)
			
	for item in items_to_decay:
		ref_price[item] = maxf(min_price, ref_price[item] * (1.0 - decay_rate))
	
	# Reset daily tracker
	trades_this_day.clear()

func _serialize_ref_price() -> Dictionary:
	var result := {}
	for k in ref_price:
		result[k] = snappedf(float(ref_price[k]), 0.00000001)
	return result

## Deserialize market from dictionary
static func from_dict(d: Dictionary) -> Market:
	var market := Market.new()
	# Convert order values to proper types
	market.buy_orders = []
	for order in d.get("buy_orders", []):
		var fixed_order := {}
		fixed_order["order_id"] = int(order.get("order_id", 0))
		fixed_order["type"] = order.get("type", "buy")
		fixed_order["item"] = order.get("item", "")
		fixed_order["qty"] = int(order.get("qty", 0))
		fixed_order["price"] = int(order.get("price", 0))
		fixed_order["agent_id"] = int(order.get("agent_id", 0))
		fixed_order["created_tick"] = int(order.get("created_tick", 0))
		fixed_order["expires_tick"] = int(order.get("expires_tick", 0))
		market.buy_orders.append(fixed_order)
	market.sell_orders = []
	for order in d.get("sell_orders", []):
		var fixed_order := {}
		fixed_order["order_id"] = int(order.get("order_id", 0))
		fixed_order["type"] = order.get("type", "sell")
		fixed_order["item"] = order.get("item", "")
		fixed_order["qty"] = int(order.get("qty", 0))
		fixed_order["price"] = int(order.get("price", 0))
		fixed_order["agent_id"] = int(order.get("agent_id", 0))
		fixed_order["created_tick"] = int(order.get("created_tick", 0))
		fixed_order["expires_tick"] = int(order.get("expires_tick", 0))
		market.sell_orders.append(fixed_order)
	market.next_order_id = int(d.get("next_order_id", 1))
	# Convert ref_price values to float
	var ref_data: Dictionary = d.get("ref_price", {})
	market.ref_price = {}
	for key in ref_data:
		market.ref_price[key] = snappedf(float(ref_data[key]), 0.00000001)
	# Convert last_trade_price values to int
	var ltp_data: Dictionary = d.get("last_trade_price", {})
	market.last_trade_price = {}
	for key in ltp_data:
		market.last_trade_price[key] = int(ltp_data[key])
	# Convert trade_counts values to int
	var tc_data: Dictionary = d.get("trade_counts", {})
	market.trade_counts = {}
	for key in tc_data:
		market.trade_counts[key] = int(tc_data[key])
	market.total_trades = int(d.get("total_trades", 0))
	
	# Deserialize trade policy counters
	market.orders_denied_embargo = int(d.get("orders_denied_embargo", 0))
	market.tariff_collected_total = int(d.get("tariff_collected_total", 0))
	var tariff_data: Dictionary = d.get("tariff_by_faction", {})
	market.tariff_by_faction = {}
	for faction_str in tariff_data:
		market.tariff_by_faction[int(faction_str)] = int(tariff_data[faction_str])
	
	return market
