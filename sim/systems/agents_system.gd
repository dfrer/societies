class_name AgentsSystem
extends ISimSystem

func tick(sim: RefCounted, state: SimState) -> void:
    var hunger_drain: float = state.get_tuning_float("hunger_drain_per_tick", 0.5)
    var pollution_mult: float = state.get_tuning_float("hunger_drain_pollution_mult", 0.5)
    var avg_pollution := state.world.get_average_pollution(state.tick)
    for agent in state.agents:
        if not agent.is_alive():
            if agent.current_activity_id >= 0:
                state.job_board.release_activity(agent.current_activity_id, state.tick)
                agent.current_activity_id = -1
            if agent.current_intent_id >= 0:
                state.resolve_intent(agent.current_intent_id, "cancelled", state.tick)
                agent.current_intent_id = -1
            continue

        # Pollution-based hunger drain (cached per tick)
        var local_pollution := state.world.get_pollution(agent.pos_x, agent.pos_y)
        var pollution_pressure := maxf(local_pollution, avg_pollution)
        var effective_drain := hunger_drain * (1.0 + pollution_pressure * pollution_mult)

        agent.set_hunger(agent.get_hunger() - effective_drain)
        var action: Dictionary = agent.decide_action(state.world, state.market, state.contracts_system,
                                          state.tuning, state.recipes, state)

        agent.current_action = action
        var action_success := Actions.execute_action(agent, action, state.world, state.market, state.crafting,
                               state.contracts_system, state.enforcement, state,
                               state.recipes, state.tuning, state.tick)

        var intent_id: int = agent.current_intent_id
        var intent_type := ""
        if intent_id >= 0:
            var intent := state.get_intent(intent_id)
            intent_type = intent.get("type", "")
        var activity_id: int = agent.current_activity_id
        var activity_type := ""
        if activity_id >= 0:
            var activity := state.job_board.get_activity(activity_id)
            activity_type = activity.get("type", "")
            if activity_type == JobBoard.ACTIVITY_ACCEPT_CONTRACT and action.get("type", "") == Actions.TYPE_ACCEPT_CONTRACT:
                if action_success:
                    state.job_board.complete_activity(activity_id, state.tick)
                    agent.current_activity_id = -1
                else:
                    state.job_board.release_activity(activity_id, state.tick)
            elif action_success and activity_type in [
                JobBoard.ACTIVITY_DELIVER_TO_PROJECT,
                JobBoard.ACTIVITY_BUILD_SITE,
                JobBoard.ACTIVITY_HAUL,
                JobBoard.ACTIVITY_FARM_TASK
            ]:
                state.job_board.complete_activity(activity_id, state.tick)
                agent.current_activity_id = -1
        if state.decision_trace_enabled:
            var sample_interval := maxi(1, state.decision_trace_sample_interval)
            if state.tick % sample_interval == 0:
                state.log_decision_trace(agent.id, intent_id, intent_type,
                    activity_id, activity_type, action.get("type", Actions.TYPE_IDLE))

func tick_daily(sim: RefCounted, state: SimState) -> void:
    pass
