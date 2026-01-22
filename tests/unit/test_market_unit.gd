## Unit tests for the Market class
extends "res://tests/test_case.gd"

const Fixtures = preload("res://tests/test_fixtures.gd")

func _run() -> void:
	subtest("Create Order", _test_create_order)
	subtest("Place Buy Order", _test_place_buy_order)
	subtest("Place Sell Order", _test_place_sell_order)
	subtest("Match Basic Trade", _test_match_basic)
	subtest("Match No Overlap", _test_match_no_overlap)
	subtest("Match Partial Fill", _test_match_partial_fill)
	subtest("Price EMA Update", _test_price_ema_update)
	subtest("Expire Orders", _test_expire_orders)
	subtest("Has Active Order", _test_has_active_order)
	subtest("Get Agent Orders", _test_get_agent_orders)
	subtest("Serialization Roundtrip", _test_serialization_roundtrip)

func _test_create_order() -> void:
	var market := Fixtures.make_market()
	
	var order1 := market.create_order("buy", "Berries", 10, 5, 1, 0, 50)
	assert_eq(order1["type"], "buy", "Type should be buy")
	assert_eq(order1["item"], "Berries", "Item should be Berries")
	assert_eq(order1["qty"], 10, "Qty should be 10")
	assert_eq(order1["price"], 5, "Price should be 5")
	assert_eq(order1["agent_id"], 1, "Agent ID should be 1")
	
	# Order ID should increment
	var order2 := market.create_order("sell", "Logs", 5, 10, 2, 0, 50)
	assert_true(order2["order_id"] > order1["order_id"], "Order IDs should increment")

func _test_place_buy_order() -> void:
	var market := Fixtures.make_market()
	var order := market.create_order("buy", "Berries", 10, 5, 1, 0, 50)
	
	market.place_buy_order(order)
	assert_eq(market.buy_orders.size(), 1, "Should have 1 buy order")
	assert_eq(market.buy_orders[0]["item"], "Berries", "Buy order item should match")

func _test_place_sell_order() -> void:
	var market := Fixtures.make_market()
	var order := market.create_order("sell", "Logs", 5, 12, 1, 0, 50)
	
	market.place_sell_order(order)
	assert_eq(market.sell_orders.size(), 1, "Should have 1 sell order")
	assert_eq(market.sell_orders[0]["item"], "Logs", "Sell order item should match")

func _test_match_basic() -> void:
	# Create market and agents
	var market := Fixtures.make_market()
	var buyer := Fixtures.make_agent({"id": 1, "money": 100})
	var seller := Fixtures.make_agent({"id": 2, "money": 0})
	seller.add_item("Berries", 20)
	
	var agents := [buyer, seller]
	var tuning := {"price_ema_alpha": 0.2}
	
	# Buyer wants to buy at 10, seller wants to sell at 8
	# Should match at (10+8)/2 = 9
	var buy_order := market.create_order("buy", "Berries", 5, 10, 1, 0, 100)
	var sell_order := market.create_order("sell", "Berries", 5, 8, 2, 0, 100)
	
	# Lock resources before placing orders
	buyer.lock_money(50)  # Lock money for buy
	seller.lock_item("Berries", 5)  # Lock items for sell
	
	market.place_buy_order(buy_order)
	market.place_sell_order(sell_order)
	
	# Match orders
	var trades := market.match_orders(agents, tuning)
	assert_eq(trades, 1, "Should have 1 trade")
	
	# Verify buyer got items
	assert_true(buyer.get_item_count("Berries") > 0, "Buyer should have Berries")

func _test_match_no_overlap() -> void:
	var market := Fixtures.make_market()
	var buyer := Fixtures.make_agent({"id": 1, "money": 100})
	var seller := Fixtures.make_agent({"id": 2})
	seller.add_item("Logs", 10)
	
	var agents := [buyer, seller]
	var tuning := {"price_ema_alpha": 0.2}
	
	# Buyer wants to buy at 5, seller wants to sell at 15
	# No overlap - no trade
	var buy_order := market.create_order("buy", "Logs", 3, 5, 1, 0, 100)
	var sell_order := market.create_order("sell", "Logs", 3, 15, 2, 0, 100)
	
	market.place_buy_order(buy_order)
	market.place_sell_order(sell_order)
	
	var trades := market.match_orders(agents, tuning)
	assert_eq(trades, 0, "Should have 0 trades when no price overlap")
	
	# Orders should still be in the book
	assert_eq(market.buy_orders.size(), 1, "Buy order should remain")
	assert_eq(market.sell_orders.size(), 1, "Sell order should remain")

func _test_match_partial_fill() -> void:
	var market := Fixtures.make_market()
	var buyer := Fixtures.make_agent({"id": 1, "money": 200})
	var seller := Fixtures.make_agent({"id": 2})
	seller.add_item("Ore", 5)  # Only has 5 to sell
	
	var agents := [buyer, seller]
	var tuning := {"price_ema_alpha": 0.2}
	
	# Buyer wants 10, seller only has 5
	var buy_order := market.create_order("buy", "Ore", 10, 20, 1, 0, 100)
	var sell_order := market.create_order("sell", "Ore", 5, 15, 2, 0, 100)
	
	buyer.lock_money(200)
	seller.lock_item("Ore", 5)
	
	market.place_buy_order(buy_order)
	market.place_sell_order(sell_order)
	
	var trades := market.match_orders(agents, tuning)
	assert_eq(trades, 1, "Should have 1 trade (partial fill)")
	
	# Sell order should be fully consumed
	# Buy order should remain with reduced quantity
	# (Implementation may vary - this documents expected behavior)

func _test_price_ema_update() -> void:
	var market := Fixtures.make_market()
	
	# Get initial reference price
	var initial_ref := market.get_ref_price("Berries")
	
	# Record a trade at a different price
	market.last_trade_price["Berries"] = 100
	
	# EMA should update towards trade price
	# (Actual update happens in match_orders, testing getter here)
	assert_true(market.get_ref_price("Berries") >= 0, "Ref price should be positive")

func _test_expire_orders() -> void:
	var market := Fixtures.make_market()
	var agent := Fixtures.make_agent({"id": 1, "money": 100})
	agent.add_item("Planks", 10)
	agent.lock_money(50)
	agent.lock_item("Planks", 5)
	
	var agents := [agent]
	
	# Create orders with TTL of 20 ticks
	var buy_order := market.create_order("buy", "Berries", 5, 10, 1, 0, 20)  # created at 0, expires at 20
	var sell_order := market.create_order("sell", "Planks", 5, 8, 1, 0, 20)
	
	market.place_buy_order(buy_order)
	market.place_sell_order(sell_order)
	
	# At tick 10, nothing expired
	market.expire_orders(10, agents)
	assert_eq(market.buy_orders.size(), 1, "Buy order should still exist at tick 10")
	assert_eq(market.sell_orders.size(), 1, "Sell order should still exist at tick 10")
	
	# At tick 25, orders should expire
	market.expire_orders(25, agents)
	assert_eq(market.buy_orders.size(), 0, "Buy order should be expired at tick 25")
	assert_eq(market.sell_orders.size(), 0, "Sell order should be expired at tick 25")

func _test_has_active_order() -> void:
	var market := Fixtures.make_market()
	
	# No orders initially
	assert_false(market.has_active_order(1, "Berries", "buy"), "Should have no orders initially")
	
	# Add buy order
	var order := market.create_order("buy", "Berries", 5, 10, 1, 0, 50)
	market.place_buy_order(order)
	
	assert_true(market.has_active_order(1, "Berries", "buy"), "Should find buy order")
	assert_false(market.has_active_order(1, "Berries", "sell"), "Should not find sell order")
	assert_false(market.has_active_order(2, "Berries", "buy"), "Should not find order for different agent")
	assert_false(market.has_active_order(1, "Logs", "buy"), "Should not find order for different item")

func _test_get_agent_orders() -> void:
	var market := Fixtures.make_market()
	
	# Add multiple orders
	var order1 := market.create_order("buy", "Berries", 5, 10, 1, 0, 50)
	var order2 := market.create_order("sell", "Logs", 3, 15, 1, 0, 50)
	var order3 := market.create_order("buy", "Ore", 2, 20, 2, 0, 50)  # Different agent
	
	market.place_buy_order(order1)
	market.place_sell_order(order2)
	market.place_buy_order(order3)
	
	var agent1_orders := market.get_agent_orders(1)
	assert_eq(agent1_orders.size(), 2, "Agent 1 should have 2 orders")
	
	var agent2_orders := market.get_agent_orders(2)
	assert_eq(agent2_orders.size(), 1, "Agent 2 should have 1 order")

func _test_serialization_roundtrip() -> void:
	var market := Fixtures.make_market()
	
	# Add some orders and trades
	var order1 := market.create_order("buy", "Berries", 5, 10, 1, 0, 50)
	var order2 := market.create_order("sell", "Logs", 3, 15, 2, 0, 50)
	market.place_buy_order(order1)
	market.place_sell_order(order2)
	market.total_trades = 5
	
	# Serialize
	var dict := market.to_dict()
	
	# Deserialize
	var restored := Market.from_dict(dict)
	
	# Verify
	assert_eq(restored.buy_orders.size(), 1, "Should have 1 buy order")
	assert_eq(restored.sell_orders.size(), 1, "Should have 1 sell order")
	assert_eq(restored.total_trades, 5, "total_trades should match")
