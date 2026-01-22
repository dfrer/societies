class_name OrganizationsSystem
extends ISimSystem

func tick(sim: RefCounted, state: SimState) -> void:
	pass

func tick_daily(sim: RefCounted, state: SimState) -> void:
	for organization in state.organizations:
		organization.plan_daily(state)
