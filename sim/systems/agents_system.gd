class_name AgentsSystem
extends ISimSystem

func tick(sim: RefCounted, state: SimState) -> void:
    var hunger_drain: float = state.get_tuning_float("hunger_drain_per_tick", 0.5)
    var pollution_mult: float = state.get_tuning_float("hunger_drain_pollution_mult", 0.5)
    for agent in state.agents:
        if not agent.is_alive():
            continue

        # Pollution-based hunger drain (cached per tick)
        var local_pollution := state.world.get_pollution(agent.pos_x, agent.pos_y)
        var avg_pollution := state.world.get_average_pollution(state.tick)
        var pollution_pressure := maxf(local_pollution, avg_pollution)
        var effective_drain := hunger_drain * (1.0 + pollution_pressure * pollution_mult)

        agent.set_hunger(agent.get_hunger() - effective_drain)
        var action: Dictionary = agent.decide_action(state.world, state.market, state.contracts_system,
                                          state.tuning, state.recipes, state)

        agent.current_action = action
        Actions.execute_action(agent, action, state.world, state.market, state.crafting,
                               state.contracts_system, state.enforcement, state,
                               state.recipes, state.tuning, state.tick)

func tick_daily(sim: RefCounted, state: SimState) -> void:
    pass
