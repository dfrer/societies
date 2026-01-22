class_name EconomySystem
extends ISimSystem

func tick_daily(sim: RefCounted, state: SimState) -> void:
    state.contracts_system.generate_daily_contracts(state, state.agents, state.market, state.world, 
                                                   state.tuning, state.rng, state.tick)

func tick(sim: RefCounted, state: SimState) -> void:
    # Expirations
    state.market.expire_orders(state.tick, state.agents, state)
    # The original code called expire_orders TWICE in _tick (lines 179-180).
    # To maintain EXACT execution path/RNG (if any), I will replicate it, 
    # though it looks like a bug. If it has side effects or uses RNG, removing it changes behavior.
    # Looking at market.gd expire_orders: it mainly iterates and modifies arrays. 
    # Calling it twice is redundant but harmless unless it triggers something twice.
    # To be safe and strict:
    state.market.expire_orders(state.tick, state.agents, state)
    
    state.contracts_system.process_expirations(state.tick, state.agents, state)
    state.validate_financial_invariants()
