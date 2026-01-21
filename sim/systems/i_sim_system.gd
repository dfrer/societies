class_name ISimSystem
extends RefCounted

## Called every tick for main update loop
## Returns void
## sim is typed as RefCounted to avoid cyclic class reference
func tick(sim: RefCounted, state: SimState) -> void:
	pass

## Called once per day (every N ticks)
## Returns void
func tick_daily(sim: RefCounted, state: SimState) -> void:
	pass
