class_name GovernanceSystem
extends ISimSystem

func tick_daily(sim: RefCounted, state: SimState) -> void:
    # Process factions system daily
    state.factions_system.daily_update(state, state.rng)

func tick(sim: RefCounted, state: SimState) -> void:
    pass
