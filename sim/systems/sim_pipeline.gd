class_name SimPipeline
extends RefCounted

var systems: Array[ISimSystem] = []

func add_system(system: ISimSystem) -> void:
	systems.append(system)

func execute(sim: RefCounted, state: SimState) -> void:
	var ticks_per_day: int = state.tuning.get("ticks_per_day", 24)

	# Check for daily update start
	var is_new_day := state.tick > 0 and state.tick % ticks_per_day == 0
	if is_new_day:
		state.log_event("day_started", {"day": state.tick / ticks_per_day})
		for system in systems:
			system.tick_daily(sim, state)

	for system in systems:
		system.tick(sim, state)
