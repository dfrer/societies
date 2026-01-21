class_name WorkshopSystem
extends ISimSystem

func tick(sim: RefCounted, state: SimState) -> void:
    state.crafting.process_workshops(state.world.workshops, state.agents, state.recipes)

func tick_daily(sim: RefCounted, state: SimState) -> void:
    pass
