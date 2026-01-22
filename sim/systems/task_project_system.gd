class_name TaskProjectSystem
extends ISimSystem

func tick(sim: RefCounted, state: SimState) -> void:
	if not state.get_tuning_bool("task_project_system_enabled", true):
		return
	state.communal_projects.process_building(state.world, state, state.tick, state.tuning)
	var max_stale: int = state.get_tuning_int("project_max_stale_ticks", 240)
	if max_stale > 0:
		state.communal_projects.abandon_stale_projects(state.tick, max_stale, state)

func tick_daily(sim: RefCounted, state: SimState) -> void:
	pass
