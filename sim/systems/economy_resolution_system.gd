class_name EconomyResolutionSystem
extends ISimSystem

func tick(sim: RefCounted, state: SimState) -> void:
	var match_interval: int = state.tuning.get("market_match_interval_ticks", 6)
	
	if state.tick % match_interval == 0:
		var market_x: int = state.tuning.get("market_pos_x", 48)
		var market_y: int = state.tuning.get("market_pos_y", 48)
		state.market.match_orders_with_tax(state.agents, state.tuning, state, 
										   state.world, Vector2i(market_x, market_y))

func tick_daily(sim: RefCounted, state: SimState) -> void:
	state.market.process_price_decay(state.tuning)
