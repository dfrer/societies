class_name TimeSystem
extends ISimSystem

func tick(sim: RefCounted, state: SimState) -> void:
    state.tick += 1

func tick_daily(sim: RefCounted, state: SimState) -> void:
    pass
