class_name MetricsSystem
extends ISimSystem

const MAX_HISTORY_DAYS := 30

func tick_daily(sim: RefCounted, state: SimState) -> void:
    # Record daily metrics
    if state.get_tuning_bool("metrics_enabled", true):
        var snapshot := Metrics.create_snapshot(state)
        state.metrics_history.append(snapshot)
        state.reset_stockpile_throughput()
        # Prune oldest to prevent unbounded growth
        if state.metrics_history.size() > MAX_HISTORY_DAYS:
            state.metrics_history.pop_front()

func tick(sim: RefCounted, state: SimState) -> void:
    pass
